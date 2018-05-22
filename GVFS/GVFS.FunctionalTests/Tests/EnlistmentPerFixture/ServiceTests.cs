using GVFS.FunctionalTests.FileSystemRunners;
using GVFS.FunctionalTests.Should;
using GVFS.FunctionalTests.Tools;
using GVFS.Tests.Should;
using Microsoft.Win32;
using NUnit.Framework;
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace GVFS.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    [NonParallelizable]
    [Category(Categories.FullSuiteOnly)]
    public class ServiceTests : TestsWithEnlistmentPerFixture
    {
        private const string NativeLibPath = @"C:\Program Files\GVFS\ProjectedFSLib.dll";
        private const string PrjFltAutoLoggerKey = "SYSTEM\\CurrentControlSet\\Control\\WMI\\Autologger\\Microsoft-Windows-ProjFS-Filter-Log";
        private const string PrjFltAutoLoggerStartValue = "Start";

        private FileSystemRunner fileSystem;

        public ServiceTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [TestCase]
        public void MountAsksServiceToEnsurePrjFltServiceIsHealthy()
        {
            if (!GVFSTestConfig.TestGVFSOnPath)
            {
                Assert.Ignore("Skipping test, test only enabled when --test-gvfs-on-path is set");
            }

            this.Enlistment.UnmountGVFS();
            StopPrjFlt();

            // Disable the ProjFS autologger
            RegistryHelper.GetValueFromRegistry(RegistryHive.LocalMachine, PrjFltAutoLoggerKey, PrjFltAutoLoggerStartValue).ShouldNotBeNull();
            RegistryHelper.TrySetDWordInRegistry(RegistryHive.LocalMachine, PrjFltAutoLoggerKey, PrjFltAutoLoggerStartValue, 0).ShouldBeTrue();

            this.Enlistment.MountGVFS();
            IsPrjFltRunning().ShouldBeTrue();

            // The service should have re-enabled the autologger
            Convert.ToInt32(RegistryHelper.GetValueFromRegistry(RegistryHive.LocalMachine, PrjFltAutoLoggerKey, PrjFltAutoLoggerStartValue)).ShouldEqual(1);
        }

        [TestCase]
        public void ServiceStartsPrjFltService()
        {
            if (!GVFSTestConfig.TestGVFSOnPath)
            {
                Assert.Ignore("Skipping test, test only enabled when --test-gvfs-on-path is set");
            }

            this.Enlistment.UnmountGVFS();
            StopPrjFlt();
            GVFSServiceProcess.StopService();
            GVFSServiceProcess.StartService();

            ServiceController controller = new ServiceController("prjflt");
            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            controller.Status.ShouldEqual(ServiceControllerStatus.Running);

            this.Enlistment.MountGVFS();
        }

        private static bool IsPrjFltRunning()
        {
            ServiceController controller = new ServiceController("prjflt");
            return controller.Status.Equals(ServiceControllerStatus.Running);
        }

        private static void StopPrjFlt()
        {
            IsPrjFltRunning().ShouldBeTrue();

            ServiceController controller = new ServiceController("prjflt");
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped);
        }        

        /// <summary>
        /// Get the build number of the OS
        /// </summary>
        /// <returns>Build number</returns>
        /// <remarks>
        /// For this method to work correctly, the calling application must have a manifest file
        /// that indicates the application supports Windows 10.
        /// See https://msdn.microsoft.com/en-us/library/windows/desktop/ms724451(v=vs.85).aspx for details
        /// </remarks>
        private static uint GetWindowsBuildNumber()
        {
            OSVersionInfo versionInfo = new OSVersionInfo();
            versionInfo.OSVersionInfoSize = (uint)Marshal.SizeOf(versionInfo);
            GetVersionEx(ref versionInfo).ShouldBeTrue();

            // 14393 -> RS1 build number
            versionInfo.BuildNumber.ShouldBeAtLeast(14393U);
            return versionInfo.BuildNumber;
        }

        private static bool IsProjFSInbox()
        {
            const uint MinRS4inboxVersion = 17121;
            const uint FirstRS5Version = 17600;
            const uint MinRS5inboxVersion = 17626;

            uint buildNumber = GetWindowsBuildNumber();
            return !(buildNumber < MinRS4inboxVersion || (buildNumber >= FirstRS5Version && buildNumber < MinRS5inboxVersion));
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetVersionEx([In, Out] ref OSVersionInfo versionInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OSVersionInfo
        {
            public uint OSVersionInfoSize;
            public uint MajorVersion;
            public uint MinorVersion;
            public uint BuildNumber;
            public uint PlatformId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string CSDVersion;
        }
    }
}
