using GVFS.Common.FileSystem;
using GVFS.Common.Git;
using GVFS.Common.Tracing;
using GVFS.DiskLayoutUpgrades;
using System;
using System.Collections.Generic;
using System.IO.Pipes;

namespace GVFS.Common
{
    public abstract class GVFSPlatform
    {
        public static GVFSPlatform Instance { get; private set; }

        public abstract IKernelDriver KernelDriver { get; }
        public abstract IGitInstallation GitInstallation { get; }
        public abstract IDiskLayoutUpgradeData DiskLayoutUpgrade { get; }

        public static void Register(GVFSPlatform platform)
        {
            if (GVFSPlatform.Instance != null)
            {
                throw new InvalidOperationException("Cannot register more than one platform");
            }

            GVFSPlatform.Instance = platform;
        }

        public abstract NamedPipeServerStream CreatePipeByName(string pipeName);
        public abstract string GetOSVersionInformation();
        public abstract void InitializeEnlistmentACLs(string enlistmentPath);
        public abstract bool IsElevated();
        public abstract string GetCurrentUser();
    }
}