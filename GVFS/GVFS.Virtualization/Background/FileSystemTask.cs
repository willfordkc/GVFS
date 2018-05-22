﻿using Newtonsoft.Json;

namespace GVFS.Virtualization.Background
{
    public struct FileSystemTask
    {
        public FileSystemTask(OperationType operation, string virtualPath, string oldVirtualPath)
        {
            this.Operation = operation;
            this.VirtualPath = virtualPath;
            this.OldVirtualPath = oldVirtualPath;
        }

        public enum OperationType
        {
            Invalid = 0,

            OnFileCreated,
            OnFileRenamed,
            OnFileDeleted,
            OnFileOverwritten,
            OnFileSuperseded,
            OnFileConvertedToFull,
            OnFailedPlaceholderDelete,
            OnFailedPlaceholderUpdate,
            OnFolderCreated,
            OnFolderRenamed,
            OnFolderDeleted,
            OnFolderFirstWrite,
            OnIndexWriteWithoutProjectionChange,
            OnPlaceholderCreationsBlockedForGit
        }

        public OperationType Operation { get; }

        public string VirtualPath { get; }
        public string OldVirtualPath { get; }

        public static FileSystemTask OnFileCreated(string virtualPath)
        {
            return new FileSystemTask(OperationType.OnFileCreated, virtualPath, oldVirtualPath: null);
        }

        public static FileSystemTask OnFileRenamed(string oldVirtualPath, string newVirtualPath)
        {
            return new FileSystemTask(OperationType.OnFileRenamed, newVirtualPath, oldVirtualPath);
        }

        public static FileSystemTask OnFileDeleted(string virtualPath)
        {
            return new FileSystemTask(OperationType.OnFileDeleted, virtualPath, oldVirtualPath: null);
        }

        public static FileSystemTask OnFileOverwritten(string virtualPath)
        {
            return new FileSystemTask(OperationType.OnFileOverwritten, virtualPath, oldVirtualPath: null);
        }

        public static FileSystemTask OnFileSuperseded(string virtualPath)
        {
            return new FileSystemTask(OperationType.OnFileSuperseded, virtualPath, oldVirtualPath: null);
        }

        public static FileSystemTask OnFileConvertedToFull(string virtualPath)
        {
            return new FileSystemTask(OperationType.OnFileConvertedToFull, virtualPath, oldVirtualPath: null);
        }

        public static FileSystemTask OnFailedPlaceholderDelete(string virtualPath)
        {
            return new FileSystemTask(OperationType.OnFailedPlaceholderDelete, virtualPath, oldVirtualPath: null);
        }

        public static FileSystemTask OnFailedPlaceholderUpdate(string virtualPath)
        {
            return new FileSystemTask(OperationType.OnFailedPlaceholderUpdate, virtualPath, oldVirtualPath: null);
        }

        public static FileSystemTask OnFolderCreated(string virtualPath)
        {
            return new FileSystemTask(OperationType.OnFolderCreated, virtualPath, oldVirtualPath: null);
        }

        public static FileSystemTask OnFolderRenamed(string oldVirtualPath, string newVirtualPath)
        {
            return new FileSystemTask(OperationType.OnFolderRenamed, newVirtualPath, oldVirtualPath);
        }

        public static FileSystemTask OnFolderDeleted(string virtualPath)
        {
            return new FileSystemTask(OperationType.OnFolderDeleted, virtualPath, oldVirtualPath: null);
        }

        public static FileSystemTask OnIndexWriteWithoutProjectionChange()
        {
            return new FileSystemTask(OperationType.OnIndexWriteWithoutProjectionChange, virtualPath: null, oldVirtualPath: null);
        }

        public static FileSystemTask OnPlaceholderCreationsBlockedForGit()
        {
            return new FileSystemTask(OperationType.OnPlaceholderCreationsBlockedForGit, virtualPath: null, oldVirtualPath: null);
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
