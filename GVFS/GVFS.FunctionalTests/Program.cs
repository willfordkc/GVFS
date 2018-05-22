using GVFS.Tests;
using System;
using System.Diagnostics;

namespace GVFS.FunctionalTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            NUnitRunner runner = new NUnitRunner(args);
            
            if (runner.HasCustomArg("--no-shared-gvfs-cache"))
            {
                Console.WriteLine("Running without a shared git object cache");
                GVFSTestConfig.NoSharedCache = true;
            }

            if (runner.HasCustomArg("--test-gvfs-on-path"))
            {
                Console.WriteLine("Running tests against GVFS on path");
                GVFSTestConfig.TestGVFSOnPath = true;
            }

            GVFSTestConfig.LocalCacheRoot = runner.GetCustomArgWithParam("--shared-gvfs-cache-root");

            if (runner.HasCustomArg("--full-suite"))
            {
                Console.WriteLine("Running the full suite of tests");
                GVFSTestConfig.UseAllRunners = true;
            }
            else
            {
                runner.ExcludeCategory(Categories.FullSuiteOnly);
            }

            GVFSTestConfig.RepoToClone =
                runner.GetCustomArgWithParam("--repo-to-clone")
                ?? Properties.Settings.Default.RepoToClone;
            
            Environment.ExitCode = runner.RunTests();

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Tests completed. Press Enter to exit.");
                Console.ReadLine();
            }
        }
    }
}