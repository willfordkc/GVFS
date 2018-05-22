﻿using GVFS.Common;
using GVFS.Common.Git;
using GVFS.Common.Http;
using GVFS.Common.Prefetch;
using GVFS.Common.Prefetch.Git;
using GVFS.Common.Prefetch.Jobs;
using GVFS.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FastFetch
{
    public class CheckoutFetchHelper : PrefetchHelper
    {
        private readonly bool allowIndexMetadataUpdateFromWorkingTree;
        private readonly int checkoutThreadCount;

        public CheckoutFetchHelper(
            ITracer tracer,
            Enlistment enlistment,
            GitObjectsHttpRequestor objectRequestor,
            int chunkSize,
            int searchThreadCount,
            int downloadThreadCount,
            int indexThreadCount,
            int checkoutThreadCount,
            bool allowIndexMetadataUpdateFromWorkingTree) : base(tracer, enlistment, objectRequestor, chunkSize, searchThreadCount, downloadThreadCount, indexThreadCount)
        {
            this.checkoutThreadCount = checkoutThreadCount;
            this.allowIndexMetadataUpdateFromWorkingTree = allowIndexMetadataUpdateFromWorkingTree;
        }

        /// <param name="branchOrCommit">A specific branch to filter for, or null for all branches returned from info/refs</param>
        public override void Prefetch(string branchOrCommit, bool isBranch)
        {
            if (string.IsNullOrWhiteSpace(branchOrCommit))
            {
                throw new FetchException("Must specify branch or commit to fetch");
            }

            GitRefs refs = null;
            string commitToFetch;
            if (isBranch)
            {
                refs = this.ObjectRequestor.QueryInfoRefs(branchOrCommit);
                if (refs == null)
                {
                    throw new FetchException("Could not query info/refs from: {0}", this.Enlistment.RepoUrl);
                }
                else if (refs.Count == 0)
                {
                    throw new FetchException("Could not find branch {0} in info/refs from: {1}", branchOrCommit, this.Enlistment.RepoUrl);
                }

                commitToFetch = refs.GetTipCommitId(branchOrCommit);
            }
            else
            {
                commitToFetch = branchOrCommit;
            }

            this.DownloadMissingCommit(commitToFetch, this.GitObjects);

            // Configure pipeline
            // Checkout uses DiffHelper when running checkout.Start(), which we use instead of LsTreeHelper like in FetchHelper.cs
            // Checkout diff output => FindMissingBlobs => BatchDownload => IndexPack => Checkout available blobs
            CheckoutJob checkout = new CheckoutJob(this.checkoutThreadCount, this.FolderList, commitToFetch, this.Tracer, this.Enlistment);
            FindMissingBlobsJob blobFinder = new FindMissingBlobsJob(this.SearchThreadCount, checkout.RequiredBlobs, checkout.AvailableBlobShas, this.Tracer, this.Enlistment);
            BatchObjectDownloadJob downloader = new BatchObjectDownloadJob(this.DownloadThreadCount, this.ChunkSize, blobFinder.MissingBlobs, checkout.AvailableBlobShas, this.Tracer, this.Enlistment, this.ObjectRequestor, this.GitObjects);
            IndexPackJob packIndexer = new IndexPackJob(this.IndexThreadCount, downloader.AvailablePacks, checkout.AvailableBlobShas, this.Tracer, this.GitObjects);

            // Start pipeline
            downloader.Start();
            blobFinder.Start();
            checkout.Start();

            blobFinder.WaitForCompletion();
            this.HasFailures |= blobFinder.HasFailures;

            // Delay indexing. It interferes with FindMissingBlobs, and doesn't help Bootstrapping.
            packIndexer.Start();

            downloader.WaitForCompletion();
            this.HasFailures |= downloader.HasFailures;

            packIndexer.WaitForCompletion();
            this.HasFailures |= packIndexer.HasFailures;

            // Since pack indexer is the last to finish before checkout finishes, it should propagate completion.
            // This prevents availableObjects from completing before packIndexer can push its objects through this link.
            checkout.AvailableBlobShas.CompleteAdding();
            checkout.WaitForCompletion();
            this.HasFailures |= checkout.HasFailures;

            if (!this.SkipConfigUpdate && !this.HasFailures)
            {
                this.UpdateRefs(branchOrCommit, isBranch, refs);

                if (isBranch)
                {
                    // Update the refspec before setting the upstream or git will complain the remote branch doesn't exist
                    this.HasFailures |= !this.UpdateRefSpec(this.Tracer, this.Enlistment, branchOrCommit, refs);

                    using (ITracer activity = this.Tracer.StartActivity("SetUpstream", EventLevel.Informational))
                    {
                        string remoteBranch = refs.GetBranchRefPairs().Single().Key;
                        GitProcess git = new GitProcess(this.Enlistment);
                        GitProcess.Result result = git.SetUpstream(branchOrCommit, remoteBranch);
                        if (result.HasErrors)
                        {
                            activity.RelatedError("Could not set upstream for {0} to {1}: {2}", branchOrCommit, remoteBranch, result.Errors);
                            this.HasFailures = true;
                        }
                    }
                }

                bool shouldSignIndex = !this.GetIsIndexSigningOff();

                // Update the index
                EventMetadata updateIndexMetadata = new EventMetadata();
                updateIndexMetadata.Add("IndexSigningIsOff", shouldSignIndex);
                using (ITracer activity = this.Tracer.StartActivity("UpdateIndex", EventLevel.Informational, Keywords.Telemetry, updateIndexMetadata))
                {
                    Index sourceIndex = this.GetSourceIndex();
                    GitIndexGenerator indexGen = new GitIndexGenerator(this.Tracer, this.Enlistment, shouldSignIndex);
                    indexGen.CreateFromHeadTree(indexVersion: 2);
                    this.HasFailures |= indexGen.HasFailures;

                    if (!indexGen.HasFailures)
                    {
                        Index newIndex = new Index(
                            this.Enlistment.EnlistmentRoot, 
                            this.Tracer,
                            Path.Combine(this.Enlistment.DotGitRoot, GVFSConstants.DotGit.IndexName), 
                            readOnly: false);

                        // Update from disk only if the caller says it is ok via command line
                        // or if we updated the whole tree and know that all files are up to date
                        bool allowIndexMetadataUpdateFromWorkingTree = this.allowIndexMetadataUpdateFromWorkingTree || checkout.UpdatedWholeTree;
                        newIndex.UpdateFileSizesAndTimes(checkout.AddedOrEditedLocalFiles, allowIndexMetadataUpdateFromWorkingTree, shouldSignIndex, sourceIndex);
                    }
                }
            }
        }
        
        /// <summary>
        /// * Updates local branch (N/A for checkout to detached HEAD)
        /// * Updates HEAD
        /// * Calls base to update shallow file and remote branch.
        /// </summary>
        protected override void UpdateRefs(string branchOrCommit, bool isBranch, GitRefs refs)
        {
            if (isBranch)
            {
                KeyValuePair<string, string> remoteRef = refs.GetBranchRefPairs().Single();
                string remoteBranch = remoteRef.Key;

                string fullLocalBranchName = branchOrCommit.StartsWith(RefsHeadsGitPath) ? branchOrCommit : (RefsHeadsGitPath + branchOrCommit);
                this.HasFailures |= !this.UpdateRef(this.Tracer, fullLocalBranchName, remoteRef.Value);
                this.HasFailures |= !this.UpdateRef(this.Tracer, "HEAD", fullLocalBranchName);
            }
            else
            {
                this.HasFailures |= !this.UpdateRef(this.Tracer, "HEAD", branchOrCommit);
            }

            base.UpdateRefs(branchOrCommit, isBranch, refs);
        }
        
        private Index GetSourceIndex()
        {
            string indexPath = Path.Combine(this.Enlistment.DotGitRoot, GVFSConstants.DotGit.IndexName);
            string backupIndexPath = Path.Combine(this.Enlistment.DotGitRoot, GVFSConstants.DotGit.IndexName + ".backup");

            if (File.Exists(indexPath))
            {
                // Note that this moves the current index, leaving nothing behind
                // This is intentional as we only need it for the purpose of updating the
                // new index and leaving it behind can make updating slower.
                this.Tracer.RelatedEvent(EventLevel.Informational, "CreateBackup", new EventMetadata() { { "BackupIndexName", backupIndexPath } });
                File.Delete(backupIndexPath);
                File.Move(indexPath, backupIndexPath);
                
                Index output = new Index(this.Enlistment.EnlistmentRoot, this.Tracer, backupIndexPath, readOnly: true);
                output.Parse();
                return output;
            }

            return null;
        }

        private bool GetIsIndexSigningOff()
        {
            // The first bit of core.gvfs is set if index signing is turned off.
            const uint CoreGvfsUnsignedIndexFlag = 1;

            GitProcess git = new GitProcess(this.Enlistment);
            GitProcess.Result configCoreGvfs = git.GetFromConfig("core.gvfs");
            uint valueCoreGvfs;

            // No errors getting the configuration and it is either "true" or numeric with the right bit set.
            return !configCoreGvfs.HasErrors &&
                !string.IsNullOrEmpty(configCoreGvfs.Output) &&
                (configCoreGvfs.Output.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                (uint.TryParse(configCoreGvfs.Output, out valueCoreGvfs) &&
                ((valueCoreGvfs & CoreGvfsUnsignedIndexFlag) == CoreGvfsUnsignedIndexFlag)));
        }
    }
}
