﻿using GVFS.Common;
using GVFS.Tests.Should;
using PrjFSLib.Managed;
using System;
using System.Threading;

namespace GVFS.UnitTests.Mock.Mac
{
    public class MockVirtualizationInstance : VirtualizationInstance, IDisposable
    {
        private AutoResetEvent commandCompleted;

        public MockVirtualizationInstance()
        {
            this.commandCompleted = new AutoResetEvent(false);
            this.CreatedPlaceholders = new ConcurrentHashSet<string>();
            this.WriteFileReturnResult = Result.Success;
        }

        public Result CompletionResult { get; set; }
        public uint BytesWritten { get; private set; }
        public Result WriteFileReturnResult { get; set; }
        public Result UpdatePlaceholderIfNeededResult { get; set; }
        public UpdateFailureCause UpdatePlaceholderIfNeededFailureCause { get; set; }
        public Result DeleteFileResult { get; set; }
        public UpdateFailureCause DeleteFileUpdateFailureCause { get; set; }

        public ConcurrentHashSet<string> CreatedPlaceholders { get; private set; }

        public override EnumerateDirectoryCallback OnEnumerateDirectory { get; set; }
        public override GetFileStreamCallback OnGetFileStream { get; set; }

        public override Result StartVirtualizationInstance(
            string virtualizationRootFullPath,
            uint poolThreadCount)
        {
            poolThreadCount.ShouldBeAtLeast(1U, "poolThreadCount must be greater than 0");
            return Result.Success;
        }

        public override Result StopVirtualizationInstance()
        {
            return Result.Success;
        }

        public override Result WriteFileContents(
            IntPtr fileHandle,
            byte[] bytes,
            uint byteCount)
        {
            this.BytesWritten = byteCount;
            return this.WriteFileReturnResult;
        }

        public override Result DeleteFile(
            string relativePath,
            UpdateType updateFlags,
            out UpdateFailureCause failureCause)
        {
            failureCause = this.DeleteFileUpdateFailureCause;
            return this.DeleteFileResult;
        }

        public override Result WritePlaceholderDirectory(
            string relativePath)
        {
            throw new NotImplementedException();
        }

        public override Result WritePlaceholderFile(
            string relativePath,
            byte[] providerId,
            byte[] contentId,
            ulong fileSize,
            ushort fileMode)
        {
            this.CreatedPlaceholders.Add(relativePath);
            return Result.Success;
        }

        public override Result UpdatePlaceholderIfNeeded(
            string relativePath,
            byte[] providerId,
            byte[] contentId,
            ulong fileSize,
            UpdateType updateFlags,
            out UpdateFailureCause failureCause)
        {
            failureCause = this.UpdatePlaceholderIfNeededFailureCause;
            return this.UpdatePlaceholderIfNeededResult;
        }

        public override Result CompleteCommand(
            ulong commandId,
            Result result)
        {
            this.CompletionResult = result;
            this.commandCompleted.Set();
            return Result.Success;
        }

        public Result WaitForCompletionStatus()
        {
            this.commandCompleted.WaitOne();
            return this.CompletionResult;
        }

        public override Result ConvertDirectoryToPlaceholder(
            string relativeDirectoryPath)
        {
            throw new NotImplementedException();
        }

        public override Result ConvertDirectoryToVirtualizationRoot(string fullPath)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (this.commandCompleted != null)
            {
                this.commandCompleted.Dispose();
                this.commandCompleted = null;
            }
        }
    }
}
