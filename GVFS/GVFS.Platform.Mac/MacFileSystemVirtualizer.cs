﻿using GVFS.Common;
using GVFS.Common.Git;
using GVFS.Common.Tracing;
using GVFS.Virtualization.BlobSize;
using GVFS.Virtualization.FileSystem;
using GVFS.Virtualization.Projection;
using PrjFSLib.Managed;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GVFS.Platform.Mac
{
    public class MacFileSystemVirtualizer : FileSystemVirtualizer
    {
        private VirtualizationInstance virtualizationInstance;

        public MacFileSystemVirtualizer(GVFSContext context, GVFSGitObjects gitObjects)
            : this(context, gitObjects, virtualizationInstance: null)
        {
        }

        public MacFileSystemVirtualizer(
            GVFSContext context,
            GVFSGitObjects gitObjects,
            VirtualizationInstance virtualizationInstance)
            : base(context, gitObjects)
        {
            this.virtualizationInstance = virtualizationInstance ?? new VirtualizationInstance();
        }

        public static FSResult ResultToFSResult(Result result)
        {
            switch (result)
            {
                case Result.Invalid:
                    return FSResult.IOError;

                case Result.Success:
                    return FSResult.Ok;

                case Result.EFileNotFound:
                case Result.EPathNotFound:
                    return FSResult.FileOrPathNotFound;

                default:
                    return FSResult.IOError;
            }
        }

        public override FileSystemResult ClearNegativePathCache(out uint totalEntryCount)
        {
            totalEntryCount = 0;
            return new FileSystemResult(FSResult.Ok, rawResult: unchecked((int)Result.Success));
        }

        public override FileSystemResult DeleteFile(string relativePath, UpdatePlaceholderType updateFlags, out UpdateFailureReason failureReason)
        {
            UpdateFailureCause failureCause;
            Result result = this.virtualizationInstance.DeleteFile(relativePath, (UpdateType)updateFlags, out failureCause);
            failureReason = (UpdateFailureReason)failureCause;
            return new FileSystemResult(ResultToFSResult(result), unchecked((int)result));
        }

        public override void Stop()
        {
            this.Context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(this.Stop)}_StopRequested", metadata: null);
        }

        public override FileSystemResult UpdatePlaceholderIfNeeded(
            string relativePath,
            DateTime creationTime,
            DateTime lastAccessTime,
            DateTime lastWriteTime,
            DateTime changeTime,
            uint fileAttributes,
            long endOfFile,
            string shaContentId,
            UpdatePlaceholderType updateFlags,
            out UpdateFailureReason failureReason)
        {
            UpdateFailureCause failureCause = UpdateFailureCause.NoFailure;
            Result result = this.virtualizationInstance.UpdatePlaceholderIfNeeded(
                relativePath,
                GetPlaceholderVersionId(),
                ConvertShaToContentId(shaContentId),
                (ulong)endOfFile,
                (UpdateType)updateFlags,
                out failureCause);
            failureReason = (UpdateFailureReason)failureCause;
            return new FileSystemResult(ResultToFSResult(result), unchecked((int)result));
        }

        protected override bool TryStart(out string error)
        {
            error = string.Empty;

            // Callbacks
            this.virtualizationInstance.OnEnumerateDirectory = this.OnEnumerateDirectory;
            this.virtualizationInstance.OnGetFileStream = this.OnGetFileStream;

            uint threadCount = (uint)Environment.ProcessorCount * 2;

            Result result = this.virtualizationInstance.StartVirtualizationInstance(
                this.Context.Enlistment.WorkingDirectoryRoot,
                threadCount);

            if (result != Result.Success)
            {
                this.Context.Tracer.RelatedError($"{nameof(this.virtualizationInstance.StartVirtualizationInstance)} failed: " + result.ToString("X") + "(" + result.ToString("G") + ")");
                error = "Failed to start virtualization instance (" + result.ToString() + ")";
                return false;
            }

            this.Context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(this.TryStart)}_StartedVirtualization", metadata: null);
            return true;
        }

        private Result OnGetFileStream(
            ulong commandId,
            string relativePath,
            byte[] providerId,
            byte[] contentId,
            int triggeringProcessId,
            string triggeringProcessName,
            IntPtr fileHandle)
        {
            try
            {
                if (contentId == null)
                {
                    this.Context.Tracer.RelatedError($"{nameof(this.OnGetFileStream)} called with null contentId, path: " + relativePath);
                    return Result.EInvalidOperation;
                }

                if (providerId == null)
                {
                    this.Context.Tracer.RelatedError($"{nameof(this.OnGetFileStream)} called with null epochId, path: " + relativePath);
                    return Result.EInvalidOperation;
                }

                string sha = GetShaFromContentId(contentId);
                byte placeholderVersion = GetPlaceholderVersionFromProviderId(providerId);

                EventMetadata metadata = this.CreateEventMetadata(relativePath);
                metadata.Add(nameof(triggeringProcessId), triggeringProcessId);
                metadata.Add(nameof(triggeringProcessName), triggeringProcessName);
                metadata.Add(nameof(sha), sha);
                metadata.Add(nameof(placeholderVersion), placeholderVersion);
                metadata.Add(nameof(commandId), commandId);
                ITracer activity = this.Context.Tracer.StartActivity("GetFileStream", EventLevel.Verbose, Keywords.Telemetry, metadata);

                if (!this.FileSystemCallbacks.IsMounted)
                {
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, $"{nameof(this.OnGetFileStream)} failed, mount has not yet completed");
                    activity.RelatedEvent(EventLevel.Informational, $"{nameof(this.OnGetFileStream)}_MountNotComplete", metadata);
                    activity.Dispose();

                    // TODO: Is this the correct Result to return?
                    return Result.EIOError;
                }

                if (placeholderVersion != FileSystemVirtualizer.PlaceholderVersion)
                {
                    activity.RelatedError(metadata, nameof(this.OnGetFileStream) + ": Unexpected placeholder version");
                    activity.Dispose();

                    // TODO: Is this the correct Result to return?
                    return Result.EIOError;
                }

                try
                {
                    if (!this.GitObjects.TryCopyBlobContentStream(
                        sha,
                        CancellationToken.None,
                        GVFSGitObjects.RequestSource.FileStreamCallback,
                        (stream, blobLength) =>
                        {
                            long remainingData = stream.Length;
                            byte[] buffer = new byte[4096];

                            while (remainingData > 0)
                            {
                                int bytesToCopy = (int)Math.Min(remainingData, buffer.Length);
                                if (stream.Read(buffer, 0, bytesToCopy) != bytesToCopy)
                                {
                                    activity.RelatedError(metadata, $"{nameof(this.OnGetFileStream)}: Failed to read requested bytes.");
                                    throw new GetFileStreamException(Result.EIOError);
                                }

                                Result result = this.virtualizationInstance.WriteFileContents(
                                    fileHandle,
                                    buffer,
                                    (uint)bytesToCopy);
                                if (result != Result.Success)
                                {
                                    activity.RelatedError(metadata, $"{nameof(this.virtualizationInstance.WriteFileContents)} failed, error: " + result.ToString("X") + "(" + result.ToString("G") + ")");
                                    throw new GetFileStreamException(result);
                                }

                                remainingData -= bytesToCopy;
                            }
                        }))
                    {
                        activity.RelatedError(metadata, $"{nameof(this.OnGetFileStream)}: TryCopyBlobContentStream failed");

                        // TODO: Is this the correct Result to return?
                        return Result.EFileNotFound;
                    }
                }
                catch (GetFileStreamException e)
                {
                    return e.Result;
                }

                return Result.Success;

            }
            catch (Exception e)
            {
                EventMetadata metadata = this.CreateEventMetadata(relativePath, e);
                metadata.Add(nameof(triggeringProcessId), triggeringProcessId);
                metadata.Add(nameof(triggeringProcessName), triggeringProcessName);
                metadata.Add(nameof(commandId), commandId);
                this.LogUnhandledExceptionAndExit(nameof(this.OnGetFileStream), metadata);
            }

            return Result.EIOError;
        }

        private Result OnEnumerateDirectory(
            ulong commandId,
            string relativePath,
            int triggeringProcessId,
            string triggeringProcessName)
        {
            try
            {
                if (!this.FileSystemCallbacks.IsMounted)
                {
                    EventMetadata metadata = this.CreateEventMetadata(relativePath);
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, nameof(this.OnEnumerateDirectory) + ": Failed enumeration, mount has not yet completed");
                    this.Context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(this.OnEnumerateDirectory)}_MountNotComplete", metadata);

                    // TODO: Is this the correct Result to return?
                    return Result.EIOError;
                }

                Result result;
                try
                {
                    IEnumerable<ProjectedFileInfo> projectedItems;

                    // TODO: Pool these connections or schedule this work to run asynchronously using TryScheduleFileOrNetworkRequest
                    using (BlobSizes.BlobSizesConnection blobSizesConnection = this.FileSystemCallbacks.BlobSizes.CreateConnection())
                    {
                        projectedItems = this.FileSystemCallbacks.GitIndexProjection.GetProjectedItems(CancellationToken.None, blobSizesConnection, relativePath);
                    }

                    result = this.CreateEnumerationPlaceholders(relativePath, projectedItems);
                }
                catch (SizesUnavailableException e)
                {
                    // TODO: Is this the correct Result to return?
                    result = Result.EIOError;

                    EventMetadata metadata = this.CreateEventMetadata(relativePath, e);
                    metadata.Add("commandId", commandId);
                    metadata.Add(nameof(result), result.ToString("X") + "(" + result.ToString("G") + ")");
                    this.Context.Tracer.RelatedError(metadata, nameof(this.OnEnumerateDirectory) + ": caught SizesUnavailableException");
                }

                return result;
            }
            catch (Exception e)
            {
                EventMetadata metadata = this.CreateEventMetadata(relativePath, e);
                metadata.Add("commandId", commandId);
                this.LogUnhandledExceptionAndExit(nameof(this.OnEnumerateDirectory), metadata);
            }

            return Result.EIOError;
        }

        private Result CreateEnumerationPlaceholders(string relativePath, IEnumerable<ProjectedFileInfo> projectedItems)
        {
            foreach (ProjectedFileInfo fileInfo in projectedItems)
            {
                Result result;
                if (fileInfo.IsFolder)
                {
                    result = this.virtualizationInstance.WritePlaceholderDirectory(Path.Combine(relativePath, fileInfo.Name));
                }
                else
                {
                    // TODO: Get the fileMode out of the index
                    ushort fileMode = Convert.ToUInt16("755", 8);

                    result = this.virtualizationInstance.WritePlaceholderFile(
                        Path.Combine(relativePath, fileInfo.Name),
                        FileSystemVirtualizer.GetPlaceholderVersionId(),
                        FileSystemVirtualizer.ConvertShaToContentId(fileInfo.Sha.ToString()),
                        (ulong)fileInfo.Size,
                        fileMode);
                }

                if (result != Result.Success)
                {
                    EventMetadata metadata = this.CreateEventMetadata(relativePath);
                    metadata.Add("fileInfo.Name", fileInfo.Name);
                    metadata.Add("fileInfo.Size", fileInfo.Size);
                    metadata.Add("fileInfo.IsFolder", fileInfo.IsFolder);
                    this.Context.Tracer.RelatedError(metadata, $"{nameof(this.CreateEnumerationPlaceholders)}: Write placeholder failed");

                    return result;
                }
            }

            return Result.Success;
        }

        private class GetFileStreamException : Exception
        {
            public GetFileStreamException(Result errorCode)
                : this("GetFileStreamException exception, error: " + errorCode.ToString(), errorCode)
            {
            }

            public GetFileStreamException(string message, Result result)
                : base(message)
            {
                this.Result = result;
            }

            public Result Result { get; }
        }
    }
}
