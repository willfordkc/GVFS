﻿using CommandLine;
using GVFS.Common;
using System.IO;
using System.Linq;

namespace GVFS.CLI.CommandLine
{
    [Verb(LogVerb.LogVerbName, HelpText = "Show the most recent GVFS log files")]
    public class LogVerb : GVFSVerb
    {
        private const string LogVerbName = "log";

        [Value(
            0,
            Required = false,
            Default = "",
            MetaName = "Enlistment Root Path",
            HelpText = "Full or relative path to the GVFS enlistment root")]
        public override string EnlistmentRootPathParameter { get; set; }

        [Option(
            "type",
            Default = null,
            HelpText = "The type of log file to display on the console")]
        public string LogType { get; set; }

        protected override string VerbName
        {
            get { return LogVerbName; }
        }

        public override void Execute()
        {
            this.ValidatePathParameter(this.EnlistmentRootPathParameter);

            this.Output.WriteLine("Most recent log files:");

            string errorMessage;
            string enlistmentRoot;
            if (!Paths.TryGetGVFSEnlistmentRoot(this.EnlistmentRootPathParameter, out enlistmentRoot, out errorMessage))
            { 
                this.ReportErrorAndExit(
                    "Error: '{0}' is not a valid GVFS enlistment",
                    this.EnlistmentRootPathParameter);
            }

            string gvfsLogsRoot = Path.Combine(
                enlistmentRoot,
                GVFSConstants.DotGVFS.LogPath);

            if (this.LogType == null)
            {
                this.DisplayMostRecent(gvfsLogsRoot, GVFSConstants.LogFileTypes.Clone);

                // By using MountPrefix ("mount") DisplayMostRecent will display either mount_verb, mount_upgrade, or mount_process, whichever is more recent
                this.DisplayMostRecent(gvfsLogsRoot, GVFSConstants.LogFileTypes.MountPrefix);
                this.DisplayMostRecent(gvfsLogsRoot, GVFSConstants.LogFileTypes.Prefetch);
                this.DisplayMostRecent(gvfsLogsRoot, GVFSConstants.LogFileTypes.Dehydrate);
                this.DisplayMostRecent(gvfsLogsRoot, GVFSConstants.LogFileTypes.Repair);

                string serviceLogsRoot = Paths.GetServiceLogsPath(this.ServiceName);
                this.DisplayMostRecent(serviceLogsRoot, GVFSConstants.LogFileTypes.Service);
            }
            else
            {
                string logFile = FindNewestFileInFolder(gvfsLogsRoot, this.LogType);
                if (logFile == null)
                {
                    this.ReportErrorAndExit("No log file found");
                }
                else
                {
                    foreach (string line in File.ReadAllLines(logFile))
                    {
                        this.Output.WriteLine(line);
                    }
                }
            }
        }

        private static string FindNewestFileInFolder(string folderName, string logFileType)
        {
            string logFilePattern = GetLogFilePatternForType(logFileType);

            DirectoryInfo logDirectory = new DirectoryInfo(folderName);
            if (!logDirectory.Exists)
            {
                return null;
            }

            FileInfo[] files = logDirectory.GetFiles(logFilePattern ?? "*");
            if (files.Length == 0)
            {
                return null;
            }

            return
                files
                .OrderByDescending(fileInfo => fileInfo.CreationTime)
                .First()
                .FullName;
        }
        
        private static string GetLogFilePatternForType(string logFileType)
        {
            return "gvfs_" + logFileType + "_*.log";
        }

        private void DisplayMostRecent(string logFolder, string logFileType)
        {
            string logFile = FindNewestFileInFolder(logFolder, logFileType);
            this.Output.WriteLine(
                "  {0, -10}: {1}",
                logFileType,
                logFile == null ? "None" : logFile);
        }
    }
}
