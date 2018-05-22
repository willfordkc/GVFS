﻿using GVFS.Common;
using GVFS.Common.Git;
using GVFS.Common.NamedPipes;
using GVFS.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;

namespace GVFS.Hooks
{
    public class Program
    {
        private const string PreCommandHook = "pre-command";
        private const string PostCommandHook = "post-command";

        private const string GitPidArg = "--git-pid=";
        private const int InvalidProcessId = -1;

        private const int PostCommandSpinnerDelayMs = 500;

        private static Dictionary<string, string> specialArgValues = new Dictionary<string, string>();
        private static string enlistmentRoot;
        private static string enlistmentPipename;

        private delegate void LockRequestDelegate(bool unattended, string[] args, int pid, Process parentProcess, NamedPipeClient pipeClient);

        public static void Main(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    ExitWithError("Usage: gvfs.hooks.exe --git-pid=<pid> <hook> <git verb> [<other arguments>]");
                }

                bool unattended = GVFSEnlistment.IsUnattended(tracer: null);

                string errorMessage;
                string normalizedCurrentDirectory;
                if (!Paths.TryGetNormalizedPath(Environment.CurrentDirectory, out normalizedCurrentDirectory, out errorMessage))
                {
                    ExitWithError($"Failed to determine final path for current directory {Environment.CurrentDirectory}. Error: {errorMessage}");
                }

                if (!Paths.TryGetGVFSEnlistmentRoot(Environment.CurrentDirectory, out enlistmentRoot, out errorMessage))
                {
                    // Nothing to hook when being run outside of a GVFS repo.
                    // This is also the path when run with --git-dir outside of a GVFS directory, see Story #949665
                    Environment.Exit(0);
                }

                enlistmentPipename = Paths.GetNamedPipeName(enlistmentRoot);

                switch (GetHookType(args))
                {
                    case PreCommandHook:
                        CheckForLegalCommands(args);
                        RunLockRequest(args, unattended, AcquireGVFSLockForProcess);
                        RunPreCommands(args);
                        break;

                    case PostCommandHook:
                        // Do not release the lock if this request was only run to see if it could acquire the GVFSLock,
                        // but did not actually acquire it.
                        if (!CheckGVFSLockAvailabilityOnly(args))
                        {
                            RunLockRequest(args, unattended, ReleaseGVFSLock);
                        }

                        break;

                    default:
                        ExitWithError("Unrecognized hook: " + string.Join(" ", args));
                        break;
                }
            }
            catch (Exception ex)
            {
                ExitWithError("Unexpected exception: " + ex.ToString());
            }
        }

        private static void RunPreCommands(string[] args)
        {
            string command = GetGitCommand(args);
            switch (command)
            {
                case "fetch":
                case "pull":
                    ProcessHelper.Run("gvfs", "prefetch --commits", redirectOutput: false);
                    break;
            }
        }

        private static void ExitWithError(params string[] messages)
        {
            foreach (string message in messages)
            {
                Console.WriteLine(message);
            }

            Environment.Exit(1);
        }

        private static void CheckForLegalCommands(string[] args)
        {
            string command = GetGitCommand(args);
            switch (command)
            {
                case "update-index":
                    if (ContainsArg(args, "--split-index") ||
                        ContainsArg(args, "--no-split-index"))
                    {
                        ExitWithError("Split index is not supported on a GVFS repo");
                    }

                    if (ContainsArg(args, "--index-version"))
                    {
                        ExitWithError("Changing the index version is not supported on a GVFS repo");
                    }

                    if (ContainsArg(args, "--skip-worktree") || 
                        ContainsArg(args, "--no-skip-worktree"))
                    {
                        ExitWithError("Modifying the skip worktree bit is not supported on a GVFS repo");
                    }

                    break;

                case "fsck":
                case "gc":
                case "prune":
                case "repack":
                    ExitWithError("'git " + command + "' is not supported on a GVFS repo");
                    break;

                case "submodule":
                    ExitWithError("Submodule operations are not supported on a GVFS repo");
                    break;

                case "status":
                    VerifyRenameDetectionSettings(args);
                    break;

                case "worktree":
                    ExitWithError("Worktree operations are not supported on a GVFS repo");
                    break;

                case "gui":
                    ExitWithError("To access the 'git gui' in a GVFS repo, please invoke 'git-gui.exe' instead.");
                    break;
            }
        }

        private static void VerifyRenameDetectionSettings(string[] args)
        {
            string srcRoot = Path.Combine(enlistmentRoot, GVFSConstants.WorkingDirectoryRootName);
            if (File.Exists(Path.Combine(srcRoot, GVFSConstants.DotGit.MergeHead)) ||
                File.Exists(Path.Combine(srcRoot, GVFSConstants.DotGit.RevertHead)))
            {
                // If no-renames and no-breaks are specified, avoid reading config.
                if (!args.Contains("--no-renames") || !args.Contains("--no-breaks"))
                {
                    Dictionary<string, GitConfigSetting> statusConfig = GitConfigHelper.GetSettings(
                        File.ReadAllLines(Path.Combine(srcRoot, GVFSConstants.DotGit.Config)),
                        "status");

                    if (!IsRunningWithParamOrSetting(args, statusConfig, "--no-renames", "renames") ||
                        !IsRunningWithParamOrSetting(args, statusConfig, "--no-breaks", "breaks"))
                    {
                        ExitWithError(
                            "git status requires rename detection to be disabled during a merge or revert conflict.",
                            "Run 'git status --no-renames --no-breaks'");
                    }
                }
            }
        }

        private static bool IsRunningWithParamOrSetting(
            string[] args, 
            Dictionary<string, GitConfigSetting> configSettings, 
            string expectedArg, 
            string expectedSetting)
        {
            return 
                args.Contains(expectedArg) ||
                configSettings.ContainsKey(expectedSetting);
        }

        private static void RunLockRequest(string[] args, bool unattended, LockRequestDelegate requestToRun)
        { 
            try
            {
                if (ShouldLock(args))
                {
                    using (NamedPipeClient pipeClient = new NamedPipeClient(enlistmentPipename))
                    {
                        if (!pipeClient.Connect())
                        {
                            ExitWithError("The repo does not appear to be mounted. Use 'gvfs status' to check.");
                        }

                        int pid = GetParentPid(args);

                        Process parentProcess = null;
                        if (pid == Program.InvalidProcessId ||
                            !ProcessHelper.TryGetProcess(pid, out parentProcess))
                        {
                            ExitWithError("GVFS.Hooks: Unable to find parent git.exe process " + "(PID: " + pid + ").");
                        }

                        requestToRun(unattended, args, pid, parentProcess, pipeClient);
                    }
                }
            }
            catch (Exception exc)
            {
                ExitWithError(
                    "Unable to initialize Git command.",
                    "Ensure that GVFS is running.",
                    exc.ToString());
            }
        }

        private static string GenerateFullCommand(string[] args)
        {
            return "git " + string.Join(" ", args.Skip(1).Where(arg => !arg.StartsWith(GitPidArg)));
        }

        private static int GetParentPid(string[] args)
        {
            string pidArg = args.SingleOrDefault(x => x.StartsWith(GitPidArg));
            if (!string.IsNullOrEmpty(pidArg))
            {
                pidArg = pidArg.Remove(0, GitPidArg.Length);
                int pid;
                if (int.TryParse(pidArg, out pid))
                {
                    return pid;
                }
            }

            ExitWithError(
                "Git did not supply the process Id.",
                "Ensure you are using the correct version of the git client.");

            return Program.InvalidProcessId;
        }

        private static void AcquireGVFSLockForProcess(bool unattended, string[] args, int pid, Process parentProcess, NamedPipeClient pipeClient)
        {
            string result;
            bool checkGvfsLockAvailabilitOnly = CheckGVFSLockAvailabilityOnly(args);
            string fullCommand = GenerateFullCommand(args);

            if (!GVFSLock.TryAcquireGVFSLockForProcess(
                    unattended,
                    pipeClient,
                    fullCommand,
                    pid,
                    WindowsPlatform.IsElevatedImplementation(),
                    checkGvfsLockAvailabilitOnly,
                    parentProcess,
                    null, // gvfsEnlistmentRoot
                    out result))
            {
                ExitWithError(result);
            }
        }

        private static void ReleaseGVFSLock(bool unattended, string[] args, int pid, Process parentProcess, NamedPipeClient pipeClient)
        {
            string fullCommand = GenerateFullCommand(args);

            GVFSLock.ReleaseGVFSLock(
                unattended,
                pipeClient,
                fullCommand,
                pid,
                WindowsPlatform.IsElevatedImplementation(),
                parentProcess,
                response =>
                {
                    if (response == null || response.ResponseData == null)
                    {
                        Console.WriteLine("\nError communicating with GVFS: Run 'git status' to check the status of your repo");
                    }
                    else if (response.ResponseData.HasFailures)
                    {
                        if (response.ResponseData.FailureCountExceedsMaxFileNames)
                        {
                            Console.WriteLine(
                                "\nGVFS failed to update {0} files, run 'git status' to check the status of files in the repo",
                                response.ResponseData.FailedToDeleteCount + response.ResponseData.FailedToUpdateCount);
                        }
                        else
                        {
                            string deleteFailuresMessage = BuildUpdatePlaceholderFailureMessage(response.ResponseData.FailedToDeleteFileList, "delete", "git clean -f ");
                            if (deleteFailuresMessage.Length > 0)
                            {
                                Console.WriteLine(deleteFailuresMessage);
                            }

                            string updateFailuresMessage = BuildUpdatePlaceholderFailureMessage(response.ResponseData.FailedToUpdateFileList, "update", "git checkout -- ");
                            if (updateFailuresMessage.Length > 0)
                            {
                                Console.WriteLine(updateFailuresMessage);
                            }
                        }
                    }
                },
                gvfsEnlistmentRoot: null,
                waitingMessage: "Waiting for GVFS to parse index and update placeholder files", 
                spinnerDelay: PostCommandSpinnerDelayMs);
        }

        private static bool CheckGVFSLockAvailabilityOnly(string[] args)
        {
            try
            {
                // Don't acquire the GVFS lock if the git command is not acquiring locks.
                // This enables tools to run status commands without to the index and
                // blocking other commands from running. The git argument
                // "--no-optional-locks" results in a 'negative'
                // value GIT_OPTIONAL_LOCKS environment variable.
                return GetGitCommand(args).Equals("status", StringComparison.OrdinalIgnoreCase) &&
                    (args.Any(arg => arg.Equals("--no-lock-index", StringComparison.OrdinalIgnoreCase)) ||
                    IsGitEnvVarDisabled("GIT_OPTIONAL_LOCKS"));
            }
            catch (Exception e)
            {
                ExitWithError("Failed to determine if GVFS should aquire GVFS lock: " + e.ToString());
            }

            return false;
        }

        private static string BuildUpdatePlaceholderFailureMessage(List<string> fileList, string failedOperation, string recoveryCommand)
        {
            if (fileList == null || fileList.Count == 0)
            {
                return string.Empty;
            }

            fileList.Sort(StringComparer.OrdinalIgnoreCase);
            string message = "\nGVFS was unable to " + failedOperation + " the following files. To recover, close all handles to the files and run these commands:";
            message += string.Concat(fileList.Select(x => "\n    " + recoveryCommand + x));
            return message;
        }

        private static bool TryRemoveArg(ref string[] args, string argName, out string output)
        {
            output = null;
            int argIdx = Array.IndexOf(args, argName);
            if (argIdx >= 0)
            {
                if (argIdx + 1 < args.Length)
                {
                    output = args[argIdx + 1];
                    args = args.Take(argIdx).Concat(args.Skip(argIdx + 2)).ToArray();
                    return true;
                }
                else
                {
                    ExitWithError("Missing value for {0}.", argName);
                }
            }

            return false;
        }

        private static bool IsGitEnvVarDisabled(string envVar)
        {
                string envVarValue = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(envVarValue))
                {
                    if (string.Equals(envVarValue, "false", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(envVarValue, "no", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(envVarValue, "off", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(envVarValue, "0", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

            return false;
        }

        private static bool ShouldLock(string[] args)
        {
            string gitCommand = GetGitCommand(args);

            switch (gitCommand)
            {
                // Keep these alphabetically sorted
                case "blame":
                case "branch":
                case "cat-file":
                case "check-attr":
                case "commit-graph":
                case "config":
                case "credential":
                case "diff":
                case "diff-files":
                case "diff-index":
                case "diff-tree":
                case "difftool":
                case "fetch":
                case "for-each-ref":
                case "help":
                case "index-pack":
                case "log":
                case "ls-files":
                case "ls-tree":
                case "merge-base":
                case "midx":
                case "name-rev":
                case "push":
                case "remote":
                case "rev-list":
                case "rev-parse":
                case "show":
                case "show-ref":
                case "symbolic-ref":
                case "tag":
                case "unpack-objects":
                case "update-ref":
                case "version":
                case "web--browse":
                    return false;
            }

            if (gitCommand == "reset" && args.Contains("--soft"))
            {
                return false;
            }

            if (!KnownGitCommands.Contains(gitCommand) &&
                IsAlias(gitCommand))
            {
                return false;
            }

            return true;
        }

        private static bool ContainsArg(string[] actualArgs, string expectedArg)
        {
            return actualArgs.Contains(expectedArg, StringComparer.OrdinalIgnoreCase);
        }

        private static string GetHookType(string[] args)
        {
            return args[0].ToLowerInvariant();
        }

        private static string GetGitCommand(string[] args)
        {
            string command = args[1].ToLowerInvariant();
            if (command.StartsWith("git-"))
            {
                command = command.Substring(4);
            }

            return command;
        }

        private static bool IsAlias(string command)
        {
            ProcessResult result = ProcessHelper.Run("git", "config --get alias." + command);

            return !string.IsNullOrEmpty(result.Output);
        }
    }
}