﻿using GVFS.Common;
using GVFS.Common.FileSystem;
using GVFS.Tests.Should;
using GVFS.UnitTests.Category;
using GVFS.UnitTests.Mock;
using GVFS.Virtualization.Background;
using NUnit.Framework;
using System.IO;
using System.Text;

namespace GVFS.UnitTests.Common
{
    [TestFixture]
    public class FileSystemTaskQueueTests
    {
        private const string MockEntryFileName = "mock:\\entries.dat";

        private const string NonAsciiString = @"ريلٌأكتوبرûمارسأغسطسºٰٰۂْٗ۵ريلٌأك";

        private const string Item1EntryText = "A 1\00\0mock:\\VirtualPath\0" + NonAsciiString + "\r\n";
        private const string Item2EntryText = "A 2\01\0mock:\\VirtualPath2\0mock:\\OldVirtualPath2\r\n";

        private const string CorruptEntryText = Item1EntryText + "A 1\0\"item1";

        private static readonly FileSystemTask Item1Payload = new FileSystemTask(FileSystemTask.OperationType.Invalid, "mock:\\VirtualPath", NonAsciiString);
        private static readonly FileSystemTask Item2Payload = new FileSystemTask(FileSystemTask.OperationType.OnFileCreated, "mock:\\VirtualPath2", "mock:\\OldVirtualPath2");
        
        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void ReturnsFalseWhenOpenFails()
        {
            MockFileSystem fs = new MockFileSystem();
            fs.File = new ReusableMemoryStream(string.Empty);
            fs.ThrowDuringOpen = true;

            string error;
            FileSystemTaskQueue dut;
            FileSystemTaskQueue.TryCreate(null, MockEntryFileName, fs, out dut, out error).ShouldEqual(false);
            dut.ShouldBeNull();
            error.ShouldNotBeNull();
        }

        [TestCase]
        public void TryPeekDoesNotDequeue()
        {
            MockFileSystem fs = new MockFileSystem();
            FileSystemTaskQueue dut = CreateFileBasedQueue(fs, Item1EntryText);

            for (int i = 0; i < 5; ++i)
            {
                FileSystemTask item;
                dut.TryPeek(out item).ShouldEqual(true);
                item.ShouldEqual(Item1Payload);
            }

            fs.File.ReadAsString().ShouldEqual(Item1EntryText);
        }

        [TestCase]
        public void StoresAddRecord()
        {
            MockFileSystem fs = new MockFileSystem();
            FileSystemTaskQueue dut = CreateFileBasedQueue(fs, string.Empty);

            dut.EnqueueAndFlush(Item1Payload);
            
            fs.File.ReadAsString().ShouldEqual(Item1EntryText);
        }

        [TestCase]
        public void TruncatesWhenEmpty()
        {
            MockFileSystem fs = new MockFileSystem();
            FileSystemTaskQueue dut = CreateFileBasedQueue(fs, Item1EntryText);

            dut.DequeueAndFlush(Item1Payload);

            fs.File.Length.ShouldEqual(0);
        }

        [TestCase]
        public void RecoversWhenCorrupt()
        {
            MockFileSystem fs = new MockFileSystem();
            FileSystemTaskQueue dut = CreateFileBasedQueue(fs, CorruptEntryText);
            
            fs.File.ReadAsString().ShouldEqual(Item1EntryText);
            dut.Count.ShouldEqual(1);
        }

        [TestCase]
        public void StoresDeleteRecord()
        {
            const string DeleteRecord = "D 1\r\n";

            MockFileSystem fs = new MockFileSystem();
            FileSystemTaskQueue dut = CreateFileBasedQueue(fs, Item1EntryText);
            
            // Add a second entry to keep FileBasedQueue from setting the stream length to 0
            dut.EnqueueAndFlush(Item2Payload);

            fs.File.ReadAsString().ShouldEqual(Item1EntryText + Item2EntryText);
            fs.File.ReadAt(fs.File.Length - 2, 2).ShouldEqual("\r\n");

            dut.DequeueAndFlush(Item1Payload);
            dut.Count.ShouldEqual(1);

            FileSystemTask item;
            dut.TryPeek(out item).ShouldEqual(true);
            item.ShouldEqual(Item2Payload);

            fs.File.Length.ShouldEqual(Encoding.UTF8.GetByteCount(Item1EntryText) + Item2EntryText.Length + DeleteRecord.Length);
            fs.File.ReadAt(Encoding.UTF8.GetByteCount(Item1EntryText) + Item2EntryText.Length, DeleteRecord.Length).ShouldEqual(DeleteRecord);
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void WrapsIOExceptionsDuringWrite()
        {
            MockFileSystem fs = new MockFileSystem();
            FileSystemTaskQueue dut = CreateFileBasedQueue(fs, Item1EntryText);

            fs.File.TruncateWrites = true;

            Assert.Throws<FileBasedCollectionException>(() => dut.EnqueueAndFlush(Item2Payload));
            
            fs.File.TruncateWrites = false;
            fs.File.ReadAt(fs.File.Length - 2, 2).ShouldNotEqual("\r\n", "Bad Test: The file is supposed to be corrupt.");

            string error;
            FileSystemTaskQueue.TryCreate(null, MockEntryFileName, fs, out dut, out error).ShouldEqual(true);
            using (dut)
            {
                FileSystemTask output;
                dut.TryPeek(out output).ShouldEqual(true);
                output.ShouldEqual(Item1Payload);
                dut.DequeueAndFlush(output);
            }
        }

        private static FileSystemTaskQueue CreateFileBasedQueue(MockFileSystem fs, string initialContents)
        {
            fs.File = new ReusableMemoryStream(initialContents);
            fs.ExpectedPath = MockEntryFileName;

            string error;
            FileSystemTaskQueue dut;
            FileSystemTaskQueue.TryCreate(null, MockEntryFileName, fs, out dut, out error).ShouldEqual(true, error);
            dut.ShouldNotBeNull();
            return dut;
        }

        private class MockFileSystem : PhysicalFileSystem
        {
            public bool ThrowDuringOpen { get; set; }

            public string ExpectedPath { get; set; }
            public ReusableMemoryStream File { get; set; }
            
            public override void CreateDirectory(string path)
            {
            }

            public override Stream OpenFileStream(string path, FileMode fileMode, FileAccess fileAccess, FileShare shareMode, FileOptions options, bool flushesToDisk)
            {
                if (this.ThrowDuringOpen)
                {
                    throw new IOException("Test Error");
                }

                path.ShouldEqual(this.ExpectedPath);
                return this.File;
            }

            public override bool FileExists(string path)
            {
                return true;
            }
        }
    }
}
