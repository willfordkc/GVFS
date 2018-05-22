using System;
using System.IO;
using CommandLine;
using PrjFSLib.Managed;

namespace MirrorProvider
{
    [Verb("clone")]
    public class CloneVerb
    {
        [Value(
            0,
            Required = true,
            MetaName = "Path to mirror",
            HelpText = "The local path to mirror from")]
        public string PathToMirror { get; set; }

        [Value(
            1,
            Required = true,
            MetaName = "Enlistment root",
            HelpText = "The path to create the virtual enlistment in")]
        public string EnlistmentRoot { get; set;  }

        public void Execute()
        {   
            Console.WriteLine($"Cloning from {Path.GetFullPath(this.PathToMirror)} to {Path.GetFullPath(this.EnlistmentRoot)}");

            if (Directory.Exists(this.EnlistmentRoot))
            {
                Console.WriteLine($"Error: Directory {this.EnlistmentRoot} already exists");
                return;
            }

            Enlistment enlistment = Enlistment.CreateNewEnlistment(this.EnlistmentRoot, this.PathToMirror);
            if (enlistment == null)
            {
                Console.WriteLine("Error: Unable to create enlistment");
                return;
            }

            VirtualizationInstance virtualizationInstance = new VirtualizationInstance();
            Result result = virtualizationInstance.ConvertDirectoryToVirtualizationRoot(enlistment.SrcRoot);

            if (result == Result.Success)
            {
                Console.WriteLine($"Virtualization root created successfully at {this.EnlistmentRoot}");
            }
            else
            {
                Console.WriteLine("Error: Failed to create virtualization root: " + result);
            }
        }
    }
}
