﻿using GVFS.Common.Tracing;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GVFS.Common.Git
{
    public class LibGit2Repo : IDisposable
    {
        private bool disposedValue = false;

        public LibGit2Repo(ITracer tracer, string repoPath)
        {
            this.Tracer = tracer;

            Native.Init();

            IntPtr repoHandle;
            if (Native.Repo.Open(out repoHandle, repoPath) != Native.SuccessCode)
            {
                string reason = Native.GetLastError();
                string message = "Couldn't open repo at " + repoPath + ": " + reason;
                tracer.RelatedError(message);

                Native.Shutdown();
                throw new InvalidDataException(message);
            }

            this.RepoHandle = repoHandle;
        }
        
        protected LibGit2Repo()
        {
        }

        ~LibGit2Repo()
        {
            this.Dispose(false);
        }

        protected ITracer Tracer { get; }
        protected IntPtr RepoHandle { get; private set; }

        public bool IsBlob(string sha)
        {
            IntPtr objHandle;
            if (Native.RevParseSingle(out objHandle, this.RepoHandle, sha) != Native.SuccessCode)
            {
                return false;
            }

            try
            {
                switch (Native.Object.GetType(objHandle))
                {
                    case Native.ObjectTypes.Blob:
                        return true;

                    default:
                        return false;
                }
            }
            finally
            {
                Native.Object.Free(objHandle);
            }
        }

        public virtual string GetTreeSha(string commitish)
        {
            IntPtr objHandle;
            if (Native.RevParseSingle(out objHandle, this.RepoHandle, commitish) != Native.SuccessCode)
            {
                return null;
            }

            try
            {
                switch (Native.Object.GetType(objHandle))
                {
                    case Native.ObjectTypes.Commit:
                        GitOid output = Native.IntPtrToGitOid(Native.Commit.GetTreeId(objHandle));
                        return output.ToString();
                }
            }
            finally
            {
                Native.Object.Free(objHandle);
            }

            return null;
        }

        public virtual bool CommitAndRootTreeExists(string commitish)
        {
            string treeSha = this.GetTreeSha(commitish);
            if (treeSha == null)
            {
                return false;
            }

            return this.ObjectExists(treeSha.ToString());
        }

        public virtual bool ObjectExists(string sha)
        {
            IntPtr objHandle;
            if (Native.RevParseSingle(out objHandle, this.RepoHandle, sha) != Native.SuccessCode)
            {
                return false;
            }

            Native.Object.Free(objHandle);
            return true;
        }

        public virtual bool TryGetObjectSize(string sha, out long size)
        {
            size = -1;

            IntPtr objHandle;
            if (Native.RevParseSingle(out objHandle, this.RepoHandle, sha) != Native.SuccessCode)
            {
                return false;
            }

            try
            {
                switch (Native.Object.GetType(objHandle))
                {
                    case Native.ObjectTypes.Blob:
                        size = Native.Blob.GetRawSize(objHandle);
                        return true;
                }
            }
            finally
            {
                Native.Object.Free(objHandle);
            }

            return false;
        }
        
        public virtual bool TryCopyBlob(string sha, Action<Stream, long> writeAction)
        {
            IntPtr objHandle;
            if (Native.RevParseSingle(out objHandle, this.RepoHandle, sha) != Native.SuccessCode)
            {
                return false;
            }

            try
            {
                unsafe
                {
                    switch (Native.Object.GetType(objHandle))
                    {
                        case Native.ObjectTypes.Blob:
                            byte* originalData = Native.Blob.GetRawContent(objHandle);
                            long originalSize = Native.Blob.GetRawSize(objHandle);
                            
                            // TODO 938696: UnmanagedMemoryStream marshals content even for CopyTo
                            // If GetRawContent changed to return IntPtr and ProjFS changed WriteBuffer to expose an IntPtr,
                            // We could probably pinvoke memcpy and avoid marshalling.
                            using (Stream mem = new UnmanagedMemoryStream(originalData, originalSize))
                            { 
                                writeAction(mem, originalSize);
                            }

                            break;
                        default:
                            throw new NotSupportedException("Copying object types other than blobs is not supported.");
                    }
                }
            }
            finally
            {
                Native.Object.Free(objHandle);
            }

            return true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                Native.Repo.Free(this.RepoHandle);
                Native.Shutdown();
                this.disposedValue = true;
            }
        }

        public static class Native
        {
            public const uint SuccessCode = 0;

            public const string Git2DllName = "git2.dll";

            public enum ObjectTypes
            {
                Commit = 1,
                Tree = 2,
                Blob = 3,
            }

            public static GitOid IntPtrToGitOid(IntPtr oidPtr)
            {
                return Marshal.PtrToStructure<GitOid>(oidPtr);
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern SafeFileHandle CreateFile(
                [MarshalAs(UnmanagedType.LPTStr)] string filename,
                [MarshalAs(UnmanagedType.U4)] FileAccess access,
                [MarshalAs(UnmanagedType.U4)] FileShare share,
                IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
                [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
                [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
                IntPtr templateFile);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static unsafe extern bool WriteFile(SafeFileHandle file, byte* buffer, uint numberOfBytesToWrite, out uint numberOfBytesWritten, IntPtr overlapped);

            [DllImport(Git2DllName, EntryPoint = "git_libgit2_init")]
            public static extern void Init();

            [DllImport(Git2DllName, EntryPoint = "git_libgit2_shutdown")]
            public static extern int Shutdown();

            [DllImport(Git2DllName, EntryPoint = "git_revparse_single")]
            public static extern uint RevParseSingle(out IntPtr objectHandle, IntPtr repoHandle, string oid);

            [DllImport(Git2DllName, EntryPoint = "git_oid_fromstr")]
            public static extern void OidFromString(ref GitOid oid, string hash);

            public static string GetLastError()
            {
                IntPtr ptr = GetLastGitError();
                if (ptr == IntPtr.Zero)
                {
                    return "Operation was successful";
                }

                return Marshal.PtrToStructure<GitError>(ptr).Message;
            }

            [DllImport(Git2DllName, EntryPoint = "giterr_last")]
            private static extern IntPtr GetLastGitError();

            [StructLayout(LayoutKind.Sequential)]
            private struct GitError
            {
                [MarshalAs(UnmanagedType.LPStr)]
                public string Message;

                public int Klass;
            }

            public static class Repo
            {
                [DllImport(Git2DllName, EntryPoint = "git_repository_open")]
                public static extern uint Open(out IntPtr repoHandle, string path);

                [DllImport(Git2DllName, EntryPoint = "git_tree_free")]
                public static extern void Free(IntPtr repoHandle);
            }

            public static class Object
            {
                [DllImport(Git2DllName, EntryPoint = "git_object_type")]
                public static extern ObjectTypes GetType(IntPtr objectHandle);

                [DllImport(Git2DllName, EntryPoint = "git_object_free")]
                public static extern void Free(IntPtr objHandle);
            }

            public static class Commit
            {
                /// <returns>A handle to an oid owned by LibGit2</returns>
                [DllImport(Git2DllName, EntryPoint = "git_commit_tree_id")]
                public static extern IntPtr GetTreeId(IntPtr commitHandle);
            }

            public static class Blob
            {
                [DllImport(Git2DllName, EntryPoint = "git_blob_rawsize")]
                [return: MarshalAs(UnmanagedType.U8)]
                public static extern long GetRawSize(IntPtr objectHandle);

                [DllImport(Git2DllName, EntryPoint = "git_blob_rawcontent")]
                public static unsafe extern byte* GetRawContent(IntPtr objectHandle);
            }
        }
    }
}