﻿using GVFS.Tests.Should;
using NUnit.Framework;
using System;
using System.Linq;
using System.Text;
using static GVFS.Virtualization.Projection.GitIndexProjection;

namespace GVFS.UnitTests.Virtualization.Git
{
    [TestFixture]
    public class GitIndexEntryTests
    {
        [TestCase]
        public void TopLevelPath()
        {
            string[] pathParts = new[] { ".gitignore" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(".gitignore");
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);
        }

        [TestCase]
        public void TwoLevelPath()
        {
            string[] pathParts = new[] { "folder", "file.txt" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(string.Join("/", pathParts));
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);
        }

        [TestCase]
        public void ReplaceFileName()
        {
            string[] pathParts = new[] { "folder", "file.txt" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(string.Join("/", pathParts));
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);

            string[] pathParts2 = new[] { "folder", "newfile.txt" };
            this.ParsePathForIndexEntry(indexEntry, string.Join("/", pathParts2), replaceIndex: 7);
            this.TestPathParts(indexEntry, pathParts2, hasSameParent: true);
        }

        [TestCase]
        public void ReplaceFileNameShorter()
        {
            string[] pathParts = new[] { "MergedComponents", "InstrumentedBinCatalogs", "dirs" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(string.Join("/", pathParts));
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);

            string[] pathParts2 = new[] { "MergedComponents", "InstrumentedBinCatalogs", "pgi", "sources.dep" };
            this.ParsePathForIndexEntry(indexEntry, string.Join("/", pathParts2), replaceIndex: 41);
            this.TestPathParts(indexEntry, pathParts2, hasSameParent: false);
        }

        [TestCase]
        public void TestComponentsWithSimilarNames()
        {
            string[] pathParts = new[] { "MergedComponents", "SDK", "FCIBBinaries.kml" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(string.Join("/", pathParts));
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);

            string[] pathParts2 = new[] { "MergedComponents", "SDK", "FCIBBinaries", "TH2Legacy", "amd64", "mdmerge.exe" };
            this.ParsePathForIndexEntry(indexEntry, string.Join("/", pathParts2), replaceIndex: 17);
            this.TestPathParts(indexEntry, pathParts2, hasSameParent: false);
        }

        [TestCase]
        public void AddFolder()
        {
            string[] pathParts = new[] { "folder", "file.txt" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(string.Join("/", pathParts));
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);

            string[] pathParts2 = new[] { "folder", "folder2", "file.txt" };
            this.ParsePathForIndexEntry(indexEntry, string.Join("/", pathParts2), replaceIndex: 8);
            this.TestPathParts(indexEntry, pathParts2, hasSameParent: false);
        }

        [TestCase]
        public void RemoveFolder()
        {
            string[] pathParts = new[] { "folder", "folder2", "file.txt" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(string.Join("/", pathParts));
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);

            string[] pathParts2 = new[] { "folder", "file.txt" };
            this.ParsePathForIndexEntry(indexEntry, string.Join("/", pathParts2), replaceIndex: 8);
            this.TestPathParts(indexEntry, pathParts2, hasSameParent: false);
        }

        [TestCase]
        public void NewSimilarRootFolder()
        {
            string[] pathParts = new[] { "folder", "file.txt" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(string.Join("/", pathParts));
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);

            string[] pathParts2 = new[] { "folder1", "file.txt" };
            this.ParsePathForIndexEntry(indexEntry, string.Join("/", pathParts2), replaceIndex: 6);
            this.TestPathParts(indexEntry, pathParts2, hasSameParent: false);
        }

        [TestCase]
        public void ReplaceFullPath()
        {
            string[] pathParts = new[] { "folder", "file.txt" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(string.Join("/", pathParts));
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);

            string[] pathParts2 = new[] { "another", "one", "new.txt" };
            this.ParsePathForIndexEntry(indexEntry, string.Join("/", pathParts2), replaceIndex: 0);
            this.TestPathParts(indexEntry, pathParts2, hasSameParent: false);
        }

        [TestCase]
        public void ClearLastParent()
        {
            string[] pathParts = new[] { "folder", "one", "file.txt" };
            GitIndexEntry indexEntry = this.SetupIndexEntry(string.Join("/", pathParts));
            this.TestPathParts(indexEntry, pathParts, hasSameParent: false);

            string[] pathParts2 = new[] { "folder", "one", "newfile.txt" };
            this.ParsePathForIndexEntry(indexEntry, string.Join("/", pathParts2), replaceIndex: 12);
            this.TestPathParts(indexEntry, pathParts2, hasSameParent: true);
            indexEntry.LastParent = new FolderData();
            indexEntry.ClearLastParent();
            indexEntry.HasSameParentAsLastEntry.ShouldBeFalse();
            indexEntry.LastParent.ShouldBeNull();
        }

        private GitIndexEntry SetupIndexEntry(string path)
        {
            GitIndexEntry indexEntry = new GitIndexEntry();
            this.ParsePathForIndexEntry(indexEntry, path, replaceIndex: 0);
            return indexEntry;
        }

        private void ParsePathForIndexEntry(GitIndexEntry indexEntry, string path, int replaceIndex)
        {
            byte[] pathBuffer = Encoding.ASCII.GetBytes(path);
            Buffer.BlockCopy(pathBuffer, 0, indexEntry.PathBuffer, 0, path.Length);
            indexEntry.PathLength = path.Length;
            indexEntry.ReplaceIndex = replaceIndex;
            indexEntry.ParsePath();
        }

        private void TestPathParts(GitIndexEntry indexEntry, string[] pathParts, bool hasSameParent)
        {
            indexEntry.HasSameParentAsLastEntry.ShouldEqual(hasSameParent, nameof(indexEntry.HasSameParentAsLastEntry));
            indexEntry.NumParts.ShouldEqual(pathParts.Length, nameof(indexEntry.NumParts));
            for (int i = 0; i < pathParts.Length; i++)
            {
                indexEntry.PathParts[i].ShouldNotBeNull();
                indexEntry.PathParts[i].GetString().ShouldEqual(pathParts[i]);
            }

            indexEntry.GetChildName().GetString().ShouldEqual(pathParts[pathParts.Length - 1]);
            indexEntry.GetFullPath().ShouldEqual(string.Join("/", pathParts));
        }
    }
}
