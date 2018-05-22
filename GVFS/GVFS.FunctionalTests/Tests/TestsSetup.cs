using GVFS.FunctionalTests.Tools;
using NUnit.Framework;
using System;
using System.IO;

namespace GVFS.FunctionalTests.Tests
{
    [SetUpFixture]
    public class TestsSetup
    {
        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            string servicePath =
                GVFSTestConfig.TestGVFSOnPath ?
                Properties.Settings.Default.PathToGVFSService :
                Path.Combine(TestContext.CurrentContext.TestDirectory, Properties.Settings.Default.PathToGVFSService);

            GVFSServiceProcess.InstallService(servicePath);
        }

        [OneTimeTearDown]
        public void RunAfterAllTests()
        {
            string serviceLogFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "GVFS",
                GVFSServiceProcess.TestServiceName,
                "Logs");

            Console.WriteLine("GVFS.Service logs at '{0}' attached below.\n\n", serviceLogFolder);
            foreach (string filename in TestResultsHelper.GetAllFilesInDirectory(serviceLogFolder))
            {
                TestResultsHelper.OutputFileContents(filename);
            }

            GVFSServiceProcess.UninstallService();

            PrintTestCaseStats.PrintRunTimeStats();
        }
    }
}
