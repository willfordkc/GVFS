using GVFS.Common;
using GVFS.Platform.Mac;
using GVFS.Tests.Should;
using GVFS.UnitTests.Mock.Git;
using GVFS.UnitTests.Mock.Mac;
using GVFS.UnitTests.Mock.Virtualization.Background;
using GVFS.UnitTests.Mock.Virtualization.BlobSize;
using GVFS.UnitTests.Mock.Virtualization.Projection;
using GVFS.UnitTests.Virtual;
using GVFS.Virtualization;
using GVFS.Virtualization.FileSystem;
using NUnit.Framework;
using PrjFSLib.Managed;
using System;
using System.Collections.Generic;

namespace GVFS.UnitTests.Platform.Mac
{
    [TestFixture]
    public class MacFileSystemVirtualizerTests : TestsWithCommonRepo
    {
        private static readonly Dictionary<Result, FSResult> MappedResults = new Dictionary<Result, FSResult>()
        {
            { Result.Success, FSResult.Ok },
            { Result.EFileNotFound, FSResult.FileOrPathNotFound },
            { Result.EPathNotFound, FSResult.FileOrPathNotFound },
        };

        [TestCase]
        public void ResultToFSResultMapsHResults()
        {
            foreach (Result result in Enum.GetValues(typeof(Result)))
            {
                if (MappedResults.ContainsKey(result))
                {
                    MacFileSystemVirtualizer.ResultToFSResult(result).ShouldEqual(MappedResults[result]);
                }
                else
                {
                    MacFileSystemVirtualizer.ResultToFSResult(result).ShouldEqual(FSResult.IOError);
                }
            }
        }

        [TestCase]
        public void DeleteFile()
        {
            using (MockVirtualizationInstance mockVirtualization = new MockVirtualizationInstance())
            using (MacFileSystemVirtualizer virtualizer = new MacFileSystemVirtualizer(this.Repo.Context, this.Repo.GitObjects, mockVirtualization))
            {
                UpdateFailureReason failureReason = UpdateFailureReason.NoFailure;

                mockVirtualization.DeleteFileResult = Result.Success;
                mockVirtualization.DeleteFileUpdateFailureCause = UpdateFailureCause.NoFailure;
                virtualizer
                    .DeleteFile("test.txt", UpdatePlaceholderType.AllowReadOnly, out failureReason)
                    .ShouldEqual(new FileSystemResult(FSResult.Ok, (int)mockVirtualization.DeleteFileResult));
                failureReason.ShouldEqual((UpdateFailureReason)mockVirtualization.DeleteFileUpdateFailureCause);

                mockVirtualization.DeleteFileResult = Result.EFileNotFound;
                mockVirtualization.DeleteFileUpdateFailureCause = UpdateFailureCause.NoFailure;
                virtualizer
                    .DeleteFile("test.txt", UpdatePlaceholderType.AllowReadOnly, out failureReason)
                    .ShouldEqual(new FileSystemResult(FSResult.FileOrPathNotFound, (int)mockVirtualization.DeleteFileResult));
                failureReason.ShouldEqual((UpdateFailureReason)mockVirtualization.DeleteFileUpdateFailureCause);

                // TODO: What will the result be when the UpdateFailureCause is DirtyData
                mockVirtualization.DeleteFileResult = Result.EInvalidOperation;

                // TODO: The result should probably be VirtualizationInvalidOperation but for now it's IOError
                mockVirtualization.DeleteFileUpdateFailureCause = UpdateFailureCause.DirtyData;
                virtualizer
                    .DeleteFile("test.txt", UpdatePlaceholderType.AllowReadOnly, out failureReason)
                    .ShouldEqual(new FileSystemResult(FSResult.IOError, (int)mockVirtualization.DeleteFileResult));
                failureReason.ShouldEqual((UpdateFailureReason)mockVirtualization.DeleteFileUpdateFailureCause);
            }
        }

        [TestCase]
        public void UpdatePlaceholderIfNeeded()
        {
            using (MockVirtualizationInstance mockVirtualization = new MockVirtualizationInstance())
            using (MacFileSystemVirtualizer virtualizer = new MacFileSystemVirtualizer(this.Repo.Context, this.Repo.GitObjects, mockVirtualization))
            {
                UpdateFailureReason failureReason = UpdateFailureReason.NoFailure;

                mockVirtualization.UpdatePlaceholderIfNeededResult = Result.Success;
                mockVirtualization.UpdatePlaceholderIfNeededFailureCause = UpdateFailureCause.NoFailure;
                virtualizer
                    .UpdatePlaceholderIfNeeded(
                        "test.txt",
                        DateTime.Now,
                        DateTime.Now,
                        DateTime.Now,
                        DateTime.Now,
                        0,
                        15,
                        string.Empty,
                        UpdatePlaceholderType.AllowReadOnly,
                        out failureReason)
                    .ShouldEqual(new FileSystemResult(FSResult.Ok, (int)mockVirtualization.UpdatePlaceholderIfNeededResult));
                failureReason.ShouldEqual((UpdateFailureReason)mockVirtualization.UpdatePlaceholderIfNeededFailureCause);

                mockVirtualization.UpdatePlaceholderIfNeededResult = Result.EFileNotFound;
                mockVirtualization.UpdatePlaceholderIfNeededFailureCause = UpdateFailureCause.NoFailure;
                virtualizer
                    .UpdatePlaceholderIfNeeded(
                        "test.txt",
                        DateTime.Now,
                        DateTime.Now,
                        DateTime.Now,
                        DateTime.Now,
                        0,
                        15,
                        string.Empty,
                        UpdatePlaceholderType.AllowReadOnly,
                        out failureReason)
                    .ShouldEqual(new FileSystemResult(FSResult.FileOrPathNotFound, (int)mockVirtualization.UpdatePlaceholderIfNeededResult));
                failureReason.ShouldEqual((UpdateFailureReason)mockVirtualization.UpdatePlaceholderIfNeededFailureCause);

                // TODO: What will the result be when the UpdateFailureCause is DirtyData
                mockVirtualization.UpdatePlaceholderIfNeededResult = Result.EInvalidOperation;
                mockVirtualization.UpdatePlaceholderIfNeededFailureCause = UpdateFailureCause.DirtyData;

                // TODO: The result should probably be VirtualizationInvalidOperation but for now it's IOError
                virtualizer
                    .UpdatePlaceholderIfNeeded(
                        "test.txt",
                        DateTime.Now,
                        DateTime.Now,
                        DateTime.Now,
                        DateTime.Now,
                        0,
                        15,
                        string.Empty,
                        UpdatePlaceholderType.AllowReadOnly,
                        out failureReason)
                    .ShouldEqual(new FileSystemResult(FSResult.IOError, (int)mockVirtualization.UpdatePlaceholderIfNeededResult));
                failureReason.ShouldEqual((UpdateFailureReason)mockVirtualization.UpdatePlaceholderIfNeededFailureCause);
            }
        }

        [TestCase]
        public void ClearNegativePathCacheIsNoOp()
        {
            using (MockVirtualizationInstance mockVirtualization = new MockVirtualizationInstance())
            using (MacFileSystemVirtualizer virtualizer = new MacFileSystemVirtualizer(this.Repo.Context, this.Repo.GitObjects, mockVirtualization))
            {
                uint totalEntryCount = 0;
                virtualizer.ClearNegativePathCache(out totalEntryCount).ShouldEqual(new FileSystemResult(FSResult.Ok, (int)Result.Success));
                totalEntryCount.ShouldEqual(0U);
            }
        }

        [TestCase]
        public void OnEnumerateDirectoryReturnsSuccessWhenResultsNotInMemory()
        {
            using (MockBackgroundFileSystemTaskRunner backgroundTaskRunner = new MockBackgroundFileSystemTaskRunner())
            using (MockVirtualizationInstance mockVirtualization = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            using (MacFileSystemVirtualizer virtualizer = new MacFileSystemVirtualizer(this.Repo.Context, this.Repo.GitObjects, mockVirtualization))
            using (FileSystemCallbacks fileSystemCallbacks = new FileSystemCallbacks(
                this.Repo.Context,
                this.Repo.GitObjects,
                RepoMetadata.Instance,
                new MockBlobSizes(),
                gitIndexProjection,
                backgroundFileSystemTaskRunner: backgroundTaskRunner,
                fileSystemVirtualizer: virtualizer))
            {
                string error;
                fileSystemCallbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();
                gitIndexProjection.EnumerationInMemory = false;
                mockVirtualization.OnEnumerateDirectory(1, "test", triggeringProcessId: 1, triggeringProcessName: "UnitTests").ShouldEqual(Result.Success);
                mockVirtualization.CreatedPlaceholders.ShouldContain(name => name.Equals(@"test\test.txt"));
                fileSystemCallbacks.Stop();
            }
        }

        [TestCase]
        public void OnEnumerateDirectoryReturnsSuccessWhenResultsInMemory()
        {
            using (MockBackgroundFileSystemTaskRunner backgroundTaskRunner = new MockBackgroundFileSystemTaskRunner())
            using (MockVirtualizationInstance mockVirtualization = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            using (MacFileSystemVirtualizer virtualizer = new MacFileSystemVirtualizer(this.Repo.Context, this.Repo.GitObjects, mockVirtualization))
            using (FileSystemCallbacks fileSystemCallbacks = new FileSystemCallbacks(
                this.Repo.Context,
                this.Repo.GitObjects,
                RepoMetadata.Instance,
                new MockBlobSizes(),
                gitIndexProjection,
                backgroundFileSystemTaskRunner: backgroundTaskRunner,
                fileSystemVirtualizer: virtualizer))
            {
                string error;
                fileSystemCallbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();
                gitIndexProjection.EnumerationInMemory = true;
                mockVirtualization.OnEnumerateDirectory(1, "test", triggeringProcessId: 1, triggeringProcessName: "UnitTests").ShouldEqual(Result.Success);
                mockVirtualization.CreatedPlaceholders.ShouldContain(name => name.Equals(@"test\test.txt"));
                fileSystemCallbacks.Stop();
            }
        }

        [TestCase]
        public void OnGetFileStreamReturnsSuccessWhenFileStreamAvailable()
        {
            using (MockBackgroundFileSystemTaskRunner backgroundTaskRunner = new MockBackgroundFileSystemTaskRunner())
            using (MockVirtualizationInstance mockVirtualization = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            using (MacFileSystemVirtualizer virtualizer = new MacFileSystemVirtualizer(this.Repo.Context, this.Repo.GitObjects, mockVirtualization))
            using (FileSystemCallbacks fileSystemCallbacks = new FileSystemCallbacks(
                this.Repo.Context,
                this.Repo.GitObjects,
                RepoMetadata.Instance,
                new MockBlobSizes(),
                gitIndexProjection,
                backgroundFileSystemTaskRunner: backgroundTaskRunner,
                fileSystemVirtualizer: virtualizer))
            {
                string error;
                fileSystemCallbacks.TryStart(out error).ShouldEqual(true);

                byte[] contentId = FileSystemVirtualizer.ConvertShaToContentId("0123456789012345678901234567890123456789");
                byte[] placeholderVersion = FileSystemVirtualizer.GetPlaceholderVersionId();

                uint fileLength = 100;
                MockGVFSGitObjects mockGVFSGitObjects = this.Repo.GitObjects as MockGVFSGitObjects;
                mockGVFSGitObjects.FileLength = fileLength;
                mockVirtualization.WriteFileReturnResult = Result.Success;

                mockVirtualization.OnGetFileStream(
                    commandId: 1,
                    relativePath: "test.txt",
                    providerId: placeholderVersion,
                    contentId: contentId,
                    triggeringProcessId: 2,
                    triggeringProcessName: "UnitTest",
                    fileHandle: IntPtr.Zero).ShouldEqual(Result.Success);

                mockVirtualization.BytesWritten.ShouldEqual(fileLength);

                fileSystemCallbacks.Stop();
            }
        }

        [TestCase]
        public void OnGetFileStreamReturnsErrorWhenWriteFileContentsFails()
        {
            using (MockBackgroundFileSystemTaskRunner backgroundTaskRunner = new MockBackgroundFileSystemTaskRunner())
            using (MockVirtualizationInstance mockVirtualization = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            using (MacFileSystemVirtualizer virtualizer = new MacFileSystemVirtualizer(this.Repo.Context, this.Repo.GitObjects, mockVirtualization))
            using (FileSystemCallbacks fileSystemCallbacks = new FileSystemCallbacks(
                this.Repo.Context,
                this.Repo.GitObjects,
                RepoMetadata.Instance,
                new MockBlobSizes(),
                gitIndexProjection,
                backgroundFileSystemTaskRunner: backgroundTaskRunner,
                fileSystemVirtualizer: virtualizer))
            {
                string error;
                fileSystemCallbacks.TryStart(out error).ShouldEqual(true);

                byte[] contentId = FileSystemVirtualizer.ConvertShaToContentId("0123456789012345678901234567890123456789");
                byte[] placeholderVersion = FileSystemVirtualizer.GetPlaceholderVersionId();

                uint fileLength = 100;
                MockGVFSGitObjects mockGVFSGitObjects = this.Repo.GitObjects as MockGVFSGitObjects;
                mockGVFSGitObjects.FileLength = fileLength;
                mockVirtualization.WriteFileReturnResult = Result.EIOError;

                mockVirtualization.OnGetFileStream(
                    commandId: 1,
                    relativePath: "test.txt",
                    providerId: placeholderVersion,
                    contentId: contentId,
                    triggeringProcessId: 2,
                    triggeringProcessName: "UnitTest",
                    fileHandle: IntPtr.Zero).ShouldEqual(Result.EIOError);

                fileSystemCallbacks.Stop();
            }
        }
    }
}
