﻿using GVFS.FunctionalTests.FileSystemRunners;
using GVFS.FunctionalTests.Should;
using GVFS.FunctionalTests.Tools;
using GVFS.Tests.Should;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace GVFS.FunctionalTests.Tests.EnlistmentPerTestCase
{
    [TestFixture]
    public class PersistedModifiedPathsTests : TestsWithEnlistmentPerTestCase
    {
        private const string FileToAdd = @"GVFS\TestAddFile.txt";
        private const string FileToUpdate = @"GVFS\GVFS\Program.cs";
        private const string FileToDelete = "Readme.md";
        private const string FileToRename = @"GVFS\GVFS.Mount\MountVerb.cs";
        private const string RenameFileTarget = @"GVFS\GVFS.Mount\MountVerb2.cs";
        private const string FolderToCreate = "PersistedSparseExcludeTests_NewFolder";
        private const string FolderToRename = "PersistedSparseExcludeTests_NewFolderForRename";
        private const string RenameFolderTarget = "PersistedSparseExcludeTests_NewFolderForRename2";
        private const string DotGitFileToCreate = @".git\TestFileFromDotGit.txt";
        private const string RenameNewDotGitFileTarget = "TestFileFromDotGit.txt";
        private const string FileToCreateOutsideRepo = "PersistedSparseExcludeTests_outsideRepo.txt";
        private const string FolderToCreateOutsideRepo = "PersistedSparseExcludeTests_outsideFolder";
        private const string FolderToDelete = "Scripts";
        private const string ExpectedModifiedFilesContents = @"A .gitattributes
A GVFS/TestAddFile.txt
A GVFS/GVFS/Program.cs
A Readme.md
A GVFS/GVFS.Mount/MountVerb.cs
A GVFS/GVFS.Mount/MountVerb2.cs
A PersistedSparseExcludeTests_NewFolder/
A PersistedSparseExcludeTests_NewFolderForRename/
A PersistedSparseExcludeTests_NewFolderForRename2/
A TestFileFromDotGit.txt
A PersistedSparseExcludeTests_outsideRepo.txt
A PersistedSparseExcludeTests_outsideFolder/
A Scripts/CreateCommonAssemblyVersion.bat
A Scripts/CreateCommonCliAssemblyVersion.bat
A Scripts/CreateCommonVersionHeader.bat
A Scripts/RunFunctionalTests.bat
A Scripts/RunUnitTests.bat
A Scripts/
";

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void ExcludeSparseFileSavedAfterRemount(FileSystemRunner fileSystem)
        {
            string fileToAdd = this.Enlistment.GetVirtualPathTo(FileToAdd);
            fileSystem.WriteAllText(fileToAdd, "Contents for the new file");

            string fileToUpdate = this.Enlistment.GetVirtualPathTo(FileToUpdate);
            fileSystem.AppendAllText(fileToUpdate, "// Testing");

            string fileToDelete = this.Enlistment.GetVirtualPathTo(FileToDelete);
            fileSystem.DeleteFile(fileToDelete);
            fileToDelete.ShouldNotExistOnDisk(fileSystem);

            string fileToRename = this.Enlistment.GetVirtualPathTo(FileToRename);
            fileSystem.MoveFile(fileToRename, this.Enlistment.GetVirtualPathTo(RenameFileTarget));

            string folderToCreate = this.Enlistment.GetVirtualPathTo(FolderToCreate);
            fileSystem.CreateDirectory(folderToCreate);

            string folderToRename = this.Enlistment.GetVirtualPathTo(FolderToRename);
            fileSystem.CreateDirectory(folderToRename);
            string folderToRenameTarget = this.Enlistment.GetVirtualPathTo(RenameFolderTarget);
            fileSystem.MoveDirectory(folderToRename, folderToRenameTarget);

            // Moving the new folder out of the repo should not change the always_exclude file
            string folderTargetOutsideSrc = Path.Combine(this.Enlistment.EnlistmentRoot, RenameFolderTarget);
            folderTargetOutsideSrc.ShouldNotExistOnDisk(fileSystem);
            fileSystem.MoveDirectory(folderToRenameTarget, folderTargetOutsideSrc);
            folderTargetOutsideSrc.ShouldBeADirectory(fileSystem);
            folderToRenameTarget.ShouldNotExistOnDisk(fileSystem);

            // Moving a file from the .git folder to the working directory should add the file to the sparse-checkout
            string dotGitfileToAdd = this.Enlistment.GetVirtualPathTo(DotGitFileToCreate);
            fileSystem.WriteAllText(dotGitfileToAdd, "Contents for the new file in dot git");
            fileSystem.MoveFile(dotGitfileToAdd, this.Enlistment.GetVirtualPathTo(RenameNewDotGitFileTarget));

            // Move a file from outside of src into src
            string fileToCreateOutsideRepoPath = Path.Combine(this.Enlistment.EnlistmentRoot, FileToCreateOutsideRepo);
            fileSystem.WriteAllText(fileToCreateOutsideRepoPath, "Contents for the new file outside of repo");
            string fileToCreateOutsideRepoTargetPath = this.Enlistment.GetVirtualPathTo(FileToCreateOutsideRepo);
            fileToCreateOutsideRepoTargetPath.ShouldNotExistOnDisk(fileSystem);
            fileSystem.MoveFile(fileToCreateOutsideRepoPath, fileToCreateOutsideRepoTargetPath);
            fileToCreateOutsideRepoTargetPath.ShouldBeAFile(fileSystem);
            fileToCreateOutsideRepoPath.ShouldNotExistOnDisk(fileSystem);

            // Move a folder from outside of src into src
            string folderToCreateOutsideRepoPath = Path.Combine(this.Enlistment.EnlistmentRoot, FolderToCreateOutsideRepo);
            fileSystem.CreateDirectory(folderToCreateOutsideRepoPath);
            folderToCreateOutsideRepoPath.ShouldBeADirectory(fileSystem);
            string folderToCreateOutsideRepoTargetPath = this.Enlistment.GetVirtualPathTo(FolderToCreateOutsideRepo);
            folderToCreateOutsideRepoTargetPath.ShouldNotExistOnDisk(fileSystem);
            fileSystem.MoveDirectory(folderToCreateOutsideRepoPath, folderToCreateOutsideRepoTargetPath);
            folderToCreateOutsideRepoTargetPath.ShouldBeADirectory(fileSystem);
            folderToCreateOutsideRepoPath.ShouldNotExistOnDisk(fileSystem);

            string folderToDelete = this.Enlistment.GetVirtualPathTo(FolderToDelete);
            fileSystem.DeleteDirectory(folderToDelete);
            folderToDelete.ShouldNotExistOnDisk(fileSystem);

            // Remount
            this.Enlistment.UnmountGVFS();
            this.Enlistment.MountGVFS();

            this.Enlistment.WaitForBackgroundOperations().ShouldEqual(true, "Background operations failed to complete.");

            string modifiedPathsDatabase = Path.Combine(this.Enlistment.DotGVFSRoot, TestConstants.Databases.ModifiedPaths);
            modifiedPathsDatabase.ShouldBeAFile(fileSystem);
            using (StreamReader reader = new StreamReader(File.Open(modifiedPathsDatabase, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                reader.ReadToEnd().ShouldEqual(ExpectedModifiedFilesContents);
            }
        }      
    }
}
