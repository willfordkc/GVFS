﻿using CommandLine;
using GVFS.CLI.CommandLine;
using GVFS.Common;
using GVFS.Windows;
using System;
using System.Linq;

// This is to keep the reference to GVFS.Mount
// so that the exe will end up in the output directory of GVFS
using GVFS.Mount;

namespace GVFS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GVFSPlatform.Register(new WindowsPlatform());

            Type[] verbTypes = new Type[]
            {
                typeof(CacheServerVerb),
                typeof(CloneVerb),
                typeof(DehydrateVerb),
                typeof(DiagnoseVerb),
                typeof(LogVerb),
                typeof(MountVerb),
                typeof(PrefetchVerb),
                typeof(RepairVerb),
                typeof(ServiceVerb),
                typeof(StatusVerb),
                typeof(UnmountVerb),
            };

            try
            {
                new Parser(
                    settings =>
                    {
                        settings.CaseSensitive = false;
                        settings.EnableDashDash = true;
                        settings.IgnoreUnknownArguments = false;
                        settings.HelpWriter = Console.Error;
                    })
                    .ParseArguments(args, verbTypes)
                    .WithNotParsed(
                        errors =>
                        {
                            if (errors.Any(error => error is TokenError))
                            {
                                Environment.Exit((int)ReturnCode.ParsingError);
                            }
                        })
                    .WithParsed<CloneVerb>(
                        clone =>
                        {
                            // We handle the clone verb differently, because clone cares if the enlistment path
                            // was not specified vs if it was specified to be the current directory
                            clone.Execute();
                            Environment.Exit((int)ReturnCode.Success);
                        })
                    .WithParsed<ServiceVerb>(
                        service =>
                        {
                            // The service verb doesn't operate on a repo, so it doesn't use the enlistment
                            // path at all.
                            service.Execute();
                            Environment.Exit((int)ReturnCode.Success);
                        })
                    .WithParsed<GVFSVerb>(
                        verb =>
                        {
                            // For all other verbs, they don't care if the enlistment root is explicitly
                            // specified or implied to be the current directory
                            if (string.IsNullOrEmpty(verb.EnlistmentRootPathParameter))
                            {
                                verb.EnlistmentRootPathParameter = Environment.CurrentDirectory;
                            }

                            verb.Execute();
                            Environment.Exit((int)ReturnCode.Success);
                        });
            }
            catch (GVFSVerb.VerbAbortedException e)
            {
                // Calling Environment.Exit() is required, to force all background threads to exit as well
                Environment.Exit((int)e.Verb.ReturnCode);
            }
        }
    }
}
