﻿using GVFS.Common;
using GVFS.Common.Git;
using GVFS.Common.Tracing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace GVFS.Common.Prefetch.Git
{
    public class GitIndexGenerator
    {
        private const long EntryCountOffset = 8;

        private const ushort ExtendedBit = 0x4000;
        private const ushort SkipWorktreeBit = 0x4000;

        private static readonly byte[] PaddingBytes = new byte[8];

        private static readonly byte[] IndexHeader = new byte[]
        {
            (byte)'D', (byte)'I', (byte)'R', (byte)'C', // Magic Signature
        };

        // We can't accurated fill times and length in realtime, so we block write the zeroes and probably save time.
        private static readonly byte[] EntryHeader = new byte[] 
        {
            0, 0, 0, 0,
            0, 0, 0, 0, // ctime
            0, 0, 0, 0,
            0, 0, 0, 0, // mtime
            0, 0, 0, 0, // stat(2) dev
            0, 0, 0, 0, // stat(2) ino
            0, 0, 0x81, 0xA4, // filemode (0x81A4 in little endian)
            0, 0, 0, 0, // stat(2) uid
            0, 0, 0, 0, // stat(2) gid
            0, 0, 0, 0  // file length
        };

        private readonly string indexLockPath;

        private Enlistment enlistment;
        private ITracer tracer;
        private bool shouldHashIndex;

        private uint entryCount = 0;

        private BlockingCollection<LsTreeEntry> entryQueue = new BlockingCollection<LsTreeEntry>();
        
        public GitIndexGenerator(ITracer tracer, Enlistment enlistment, bool shouldHashIndex)
        {
            this.tracer = tracer;
            this.enlistment = enlistment;
            this.shouldHashIndex = shouldHashIndex;
            
            this.indexLockPath = Path.Combine(enlistment.DotGitRoot, GVFSConstants.DotGit.IndexName + GVFSConstants.DotGit.LockExtension);
        }

        public bool HasFailures { get; private set; }

        public void CreateFromHeadTree(uint indexVersion, HashSet<string> sparseCheckoutEntries = null)
        {
            using (ITracer updateIndexActivity = this.tracer.StartActivity("CreateFromHeadTree", EventLevel.Informational))
            {
                Thread entryWritingThread = new Thread(() => this.WriteAllEntries(indexVersion, sparseCheckoutEntries));
                entryWritingThread.Start();

                GitProcess git = new GitProcess(this.enlistment);
                GitProcess.Result result = git.LsTree(
                    GVFSConstants.DotGit.HeadName,
                    this.EnqueueEntriesFromLsTree,
                    recursive: true,
                    showAllTrees: false);

                if (result.HasErrors)
                {
                    this.tracer.RelatedError("LsTree failed during index generation: {0}", result.Errors);
                    this.HasFailures = true;
                }

                this.entryQueue.CompleteAdding();
                entryWritingThread.Join();
            }
        }

        private void EnqueueEntriesFromLsTree(string line)
        {
            LsTreeEntry entry = LsTreeEntry.ParseFromLsTreeLine(line);
            if (entry != null)
            {
                this.entryQueue.Add(entry);
            }
        }

        private void WriteAllEntries(uint version, HashSet<string> sparseCheckoutEntries)
        {
            try
            {
                using (Stream indexStream = new FileStream(this.indexLockPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (BinaryWriter writer = new BinaryWriter(indexStream))
                {
                    writer.Write(IndexHeader);
                    writer.Write(EndianHelper.Swap(version));
                    writer.Write((uint)0); // Number of entries placeholder

                    uint lastStringLength = 0;
                    LsTreeEntry entry;
                    while (this.entryQueue.TryTake(out entry, Timeout.Infinite))
                    {
                        bool skipWorkTree = 
                            sparseCheckoutEntries != null && 
                            !sparseCheckoutEntries.Contains(entry.Filename) && 
                            !sparseCheckoutEntries.Contains(this.GetDirectoryNameForGitPath(entry.Filename));
                        this.WriteEntry(writer, version, entry.Sha, entry.Filename, skipWorkTree, ref lastStringLength);
                    }

                    // Update entry count
                    writer.BaseStream.Position = EntryCountOffset;
                    writer.Write(EndianHelper.Swap(this.entryCount));
                    writer.Flush();
                }

                this.AppendIndexSha();
                this.ReplaceExistingIndex();
            }
            catch (Exception e)
            {
                this.tracer.RelatedError("Failed to generate index: {0}", e.ToString());
                this.HasFailures = true;
            }
        }

        private string GetDirectoryNameForGitPath(string filename)
        {
            int idx = filename.LastIndexOf('/');
            if (idx < 0)
            {
                return "/";
            }

            return filename.Substring(0, idx + 1);
        }

        private void WriteEntry(BinaryWriter writer, uint version, string sha, string filename, bool skipWorktree, ref uint lastStringLength)
        {
            long startPosition = writer.BaseStream.Position;

            this.entryCount++;
            
            writer.Write(EntryHeader, 0, EntryHeader.Length);
            
            writer.Write(SHA1Util.BytesFromHexString(sha));

            byte[] filenameBytes = Encoding.UTF8.GetBytes(filename);

            ushort flags = (ushort)(filenameBytes.Length & 0xFFF);
            flags |= version >= 3 && skipWorktree ? ExtendedBit : (ushort)0;
            writer.Write(EndianHelper.Swap(flags));

            if (version >= 3 && skipWorktree)
            {
                writer.Write(EndianHelper.Swap(SkipWorktreeBit));
            }

            if (version >= 4)
            {
                this.WriteReplaceLength(writer, lastStringLength);
                lastStringLength = (uint)filenameBytes.Length;
            }

            writer.Write(filenameBytes);

            writer.Flush();
            long endPosition = writer.BaseStream.Position;
            
            // Version 4 requires a nul-terminated string.
            int numPaddingBytes = 1;
            if (version < 4)
            {
                // Version 2-3 has between 1 and 8 padding bytes including nul-terminator.
                numPaddingBytes = 8 - ((int)(endPosition - startPosition) % 8);
                if (numPaddingBytes == 0)
                {
                    numPaddingBytes = 8;
                }
            }

            writer.Write(PaddingBytes, 0, numPaddingBytes);

            writer.Flush();
        }

        private void WriteReplaceLength(BinaryWriter writer, uint value)
        {
            List<byte> bytes = new List<byte>();
            do
            {
                byte nextByte = (byte)(value & 0x7F);
                value = value >> 7;
                bytes.Add(nextByte);
            }
            while (value != 0);

            bytes.Reverse();
            for (int i = 0; i < bytes.Count; ++i)
            {
                byte toWrite = bytes[i];
                if (i < bytes.Count - 1)
                {
                    toWrite -= 1;
                    toWrite |= 0x80;
                }

                writer.Write(toWrite);
            }
        }

        private void AppendIndexSha()
        {
            byte[] sha = this.GetIndexHash();

            using (Stream indexStream = new FileStream(this.indexLockPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                indexStream.Seek(0, SeekOrigin.End);
                indexStream.Write(sha, 0, sha.Length);
            }
        }

        private byte[] GetIndexHash()
        {
            if (this.shouldHashIndex)
            {
                using (Stream fileStream = new FileStream(this.indexLockPath, FileMode.Open, FileAccess.Read, FileShare.Write))
                using (HashingStream hasher = new HashingStream(fileStream))
                {
                    hasher.CopyTo(Stream.Null);
                    return hasher.Hash;
                }
            }

            return new byte[20];
        }

        private void ReplaceExistingIndex()
        {
            string indexPath = Path.Combine(this.enlistment.DotGitRoot, GVFSConstants.DotGit.IndexName);
            File.Delete(indexPath);
            File.Move(this.indexLockPath, indexPath);
        }

        private class LsTreeEntry
        {
            public LsTreeEntry()
            {
                this.Filename = string.Empty;
            }

            public string Filename { get; private set; }
            public string Sha { get; private set; }

            public static LsTreeEntry ParseFromLsTreeLine(string line)
            {
                int blobIndex = line.IndexOf(DiffTreeResult.BlobMarker);
                if (blobIndex >= 0)
                {
                    LsTreeEntry blobEntry = new LsTreeEntry();
                    blobEntry.Sha = line.Substring(blobIndex + DiffTreeResult.BlobMarker.Length, GVFSConstants.ShaStringLength);
                    blobEntry.Filename = GitPathConverter.ConvertPathOctetsToUtf8(line.Substring(line.LastIndexOf("\t") + 1).Trim('"'));

                    return blobEntry;
                }
                
                return null;
            }
        }
    }
}
