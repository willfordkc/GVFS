﻿using GVFS.Common;
using GVFS.Common.Prefetch.Git;
using GVFS.Common.Tracing;
using System.Collections.Generic;
using System.IO;

namespace GVFS.CLI.RepairJobs
{
    public class GitIndexRepairJob : RepairJob
    {
        private readonly string indexPath;
        private readonly string sparseCheckoutPath;
        
        public GitIndexRepairJob(ITracer tracer, TextWriter output, GVFSEnlistment enlistment)
            : base(tracer, output, enlistment)
        {
            this.indexPath = Path.Combine(this.Enlistment.DotGitRoot, GVFSConstants.DotGit.IndexName);
            this.sparseCheckoutPath = Path.Combine(this.Enlistment.WorkingDirectoryRoot, GVFSConstants.DotGit.Info.SparseCheckoutPath);
        }

        public override string Name
        {
            get { return @".git\index"; }
        }

        public override IssueType HasIssue(List<string> messages)
        {
            if (!File.Exists(this.indexPath))
            {
                messages.Add(".git\\index not found");
                return IssueType.Fixable;
            }
            else
            {
                return this.TryParseIndex(this.indexPath, messages);
            }
        }

        public override FixResult TryFixIssues(List<string> messages)
        {
            string indexBackupPath = null;
            if (File.Exists(this.indexPath))
            {
                if (!this.TryRenameToBackupFile(this.indexPath, out indexBackupPath, messages))
                {
                    return FixResult.Failure;
                }
            }

            GitIndexGenerator indexGen = new GitIndexGenerator(this.Tracer, this.Enlistment, shouldHashIndex: false);
            indexGen.CreateFromHeadTree(indexVersion: 4);

            if (indexGen.HasFailures || this.TryParseIndex(this.indexPath, messages) != IssueType.None)
            {
                if (indexBackupPath != null)
                {
                    this.RestoreFromBackupFile(indexBackupPath, this.indexPath, messages);
                }

                return FixResult.Failure;
            }

            if (indexBackupPath != null)
            {
                if (!this.TryDeleteFile(indexBackupPath))
                {
                    messages.Add("Warning: Could not delete backed up .git\\index at: " + indexBackupPath);
                }
            }

            return FixResult.Success;
        }
    }
}
