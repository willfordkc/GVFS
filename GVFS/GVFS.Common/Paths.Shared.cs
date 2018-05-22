using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace GVFS.Common
{
    public static partial class Paths
    {
        public static bool TryGetGVFSEnlistmentRoot(string directory, out string enlistmentRoot, out string errorMessage)
        {
            enlistmentRoot = null;

            string finalDirectory;
            if (!Paths.TryGetNormalizedPath(directory, out finalDirectory, out errorMessage))
            {
                return false;
            }

            enlistmentRoot = GetRoot(finalDirectory, GVFSConstants.DotGVFS.Root);
            if (enlistmentRoot == null)
            {
                errorMessage = $"Failed to find the root directory for {GVFSConstants.DotGVFS.Root} in {finalDirectory}";
                return false;
            }

            return true;
        }

        public static string GetGitEnlistmentRoot(string directory)
        {
            return GetRoot(directory, GVFSConstants.DotGit.Root);
        }

        public static string GetNamedPipeName(string enlistmentRoot)
        {
            return "GVFS_" + enlistmentRoot.ToUpper().Replace(':', '_');
        }

        public static string GetServiceDataRoot(string serviceName)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create),
                "GVFS",
                serviceName);
        }

        public static string GetServiceLogsPath(string serviceName)
        {
            return Path.Combine(GetServiceDataRoot(serviceName), "Logs");
        }

        public static bool TryGetNormalizedPath(string path, out string normalizedPath, out string errorMessage)
        {
            normalizedPath = null;
            errorMessage = null;
            try
            {
                // The folder might not be on disk yet, walk up the path until we find a folder that's on disk
                Stack<string> removedPathParts = new Stack<string>();
                string parentPath = path;
                while (!string.IsNullOrWhiteSpace(parentPath) && !Directory.Exists(parentPath))
                {
                    removedPathParts.Push(Path.GetFileName(parentPath));
                    parentPath = Path.GetDirectoryName(parentPath);
                }

                if (string.IsNullOrWhiteSpace(parentPath))
                {
                    errorMessage = "Could not get path root. Specified path does not exist and unable to find ancestor of path on disk";
                    return false;
                }

                normalizedPath = NativeMethods.GetFinalPathName(parentPath);

                // normalizedPath now consists of all parts of the path currently on disk, re-add any parts of the path that were popped off 
                while (removedPathParts.Count > 0)
                {
                    normalizedPath = Path.Combine(normalizedPath, removedPathParts.Pop());
                }
            }
            catch (Win32Exception e)
            {
                errorMessage = "Could not get path root. Failed to determine volume: " + e.Message;
                return false;
            }

            return true;
        }

        private static string GetRoot(string startingDirectory, string rootName)
        {
            startingDirectory = startingDirectory.TrimEnd(GVFSConstants.PathSeparator);
            DirectoryInfo dirInfo;

            try
            {
                dirInfo = new DirectoryInfo(startingDirectory);
            }
            catch (Exception)
            {
                return null;
            }

            while (dirInfo != null)
            {
                if (dirInfo.Exists)
                {
                    DirectoryInfo[] dotGVFSDirs = new DirectoryInfo[0];

                    try
                    {
                        dotGVFSDirs = dirInfo.GetDirectories(rootName);
                    }
                    catch (IOException)
                    {
                    }

                    if (dotGVFSDirs.Count() == 1)
                    {
                        return dirInfo.FullName;
                    }
                }

                dirInfo = dirInfo.Parent;
            }

            return null;
        }
    }
}
