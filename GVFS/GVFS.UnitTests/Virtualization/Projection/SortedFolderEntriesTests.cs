﻿using GVFS.Tests.Should;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using static GVFS.Virtualization.Projection.GitIndexProjection;

namespace GVFS.UnitTests.Virtualization.Git
{
    [TestFixture]
    public class SortedFolderEntriesTests
    {
        private static string[] defaultFiles = new string[]
        {
            "_test",
            "zero",
            "a",
            "{file}",
            "(1)",
            "file.txt",
            "01",
        };

        private static string[] defaultFolders = new string[]
        {
            "zf",
            "af",
            "01f",
            "{folder}",
            "_f",
            "(1f)",
            "folder",
        };

        [TestCase]
        public void EmptyFolderEntries_NotFound()
        {
            SortedFolderEntries sfe = new SortedFolderEntries();
            LazyUTF8String findName = ConstructLazyUTF8String("Anything");
            sfe.TryGetValue(findName, out FolderEntryData folderEntryData).ShouldBeFalse();
            folderEntryData.ShouldBeNull();
        }

        [TestCase]
        public void EntryNotFound()
        {
            SortedFolderEntries sfe = SetupDefaultEntries();
            LazyUTF8String findName = ConstructLazyUTF8String("Anything");
            sfe.TryGetValue(findName, out FolderEntryData folderEntryData).ShouldBeFalse();
            folderEntryData.ShouldBeNull();
        }

        [TestCase]
        public void EntryFoundMatchingCase()
        {
            SortedFolderEntries sfe = SetupDefaultEntries();
            LazyUTF8String findName = ConstructLazyUTF8String("folder");
            sfe.TryGetValue(findName, out FolderEntryData folderEntryData).ShouldBeTrue();
            folderEntryData.ShouldNotBeNull();
        }

        [TestCase]
        public void EntryFoundDifferentCase()
        {
            SortedFolderEntries sfe = SetupDefaultEntries();
            LazyUTF8String findName = ConstructLazyUTF8String("Folder");
            sfe.TryGetValue(findName, out FolderEntryData folderEntryData).ShouldBeTrue();
            folderEntryData.ShouldNotBeNull();
        }

        [TestCase]
        public void AddItemAtEnd()
        {
            SortedFolderEntries sfe = SetupDefaultEntries();
            LazyUTF8String name = ConstructLazyUTF8String("{{shouldbeattheend");
            sfe.AddFolder(name);
            sfe[defaultFiles.Length + defaultFolders.Length].Name.ShouldEqual(name, "Item added at incorrect index.");
        }

        [TestCase]
        public void AddItemAtTheBeginning()
        {
            SortedFolderEntries sfe = SetupDefaultEntries();
            LazyUTF8String name = ConstructLazyUTF8String("((shouldbeatthestart");
            sfe.AddFolder(name);
            sfe[0].Name.ShouldEqual(name, "Item added at incorrect index.");
        }

        [TestCase]
        public void ValidateOrderOfDefaultEntries()
        {
            List<string> allEntries = new List<string>(defaultFiles);
            allEntries.AddRange(defaultFolders);
            allEntries.Sort(CaseInsensitiveStringCompare);
            SortedFolderEntries sfe = SetupDefaultEntries();
            sfe.Count.ShouldEqual(14);
            for (int i = 0; i < allEntries.Count; i++)
            {
                sfe[i].Name.GetString().ShouldEqual(allEntries[i]);
            }
        }

        [TestCase]
        public void Clear()
        {
            SortedFolderEntries sfe = SetupDefaultEntries();
            sfe.Clear();
            sfe.Count.ShouldEqual(0);
        }

        private static int CaseInsensitiveStringCompare(string x, string y)
        {
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private static SortedFolderEntries SetupDefaultEntries()
        {
            SortedFolderEntries sfe = new SortedFolderEntries();
            AddFiles(sfe, defaultFiles);
            AddFolders(sfe, defaultFolders);
            sfe.Count.ShouldEqual(defaultFiles.Length + defaultFolders.Length);
            return sfe;
        }

        private static unsafe LazyUTF8String ConstructLazyUTF8String(string name)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(name);
            fixed (byte* bufferPtr = buffer)
            {
                return LazyUTF8String.FromByteArray(bufferPtr, name.Length);
            }
        }

        private static void AddFiles(SortedFolderEntries entries, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                LazyUTF8String entryString = ConstructLazyUTF8String(names[i]);
                entries.AddFile(entryString, new byte[20]);
            }
        }

        private static void AddFolders(SortedFolderEntries entries, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                LazyUTF8String entryString = ConstructLazyUTF8String(names[i]);
                entries.AddFolder(entryString);
            }
        }
    }
}
