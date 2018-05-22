using System;
using System.IO;
using CommandLine;
using PrjFSLib.Managed;

namespace MirrorProvider
{
    [Verb("mount")]
    public class MountVerb
    {
        private Enlistment enlistment;
        private VirtualizationInstance virtualizationInstance;

        [Value(
            0,
            Required = true,
            MetaName = "Enlistment root",
            HelpText = "The path to create the virtual enlistment in")]
        public string EnlistmentRoot { get; set; }

        public void Execute()
        {
            this.enlistment = Enlistment.LoadExistingEnlistment(this.EnlistmentRoot);
            if (this.enlistment == null)
            {
                Console.WriteLine("Error: Unable to load enlistment");
            }

            Console.WriteLine();
            Console.WriteLine($"Mounting {Path.GetFullPath(this.enlistment.EnlistmentRoot)}");

            this.virtualizationInstance = new VirtualizationInstance();
            this.virtualizationInstance.OnEnumerateDirectory = this.OnEnumerateDirectory;
            this.virtualizationInstance.OnGetFileStream = this.OnGetFileStream;

            Result result = this.virtualizationInstance.StartVirtualizationInstance(
                this.enlistment.SrcRoot,
                (uint)Environment.ProcessorCount * 2);

            if (result == Result.Success)
            {
                Console.WriteLine("Virtualization instance started successfully");

                Console.WriteLine("Press Enter to end the instance");
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("Virtualization instance failed to start: " + result);
            }
        }

        private Result OnEnumerateDirectory(ulong commandId, string relativePath, int triggeringProcessId, string triggeringProcessName)
        {
            Console.WriteLine($"MirrorProvider.OnEnumerateDirectory({commandId}, '{relativePath}', {triggeringProcessId}, {triggeringProcessName})");

            try
            {
                string fullPathInMirror = Path.Combine(this.enlistment.MirrorRoot, relativePath);
                DirectoryInfo dirInfo = new DirectoryInfo(fullPathInMirror);

                if (!dirInfo.Exists)
                {
                    return Result.EFileNotFound;
                }

                byte[] providerId = ToVersionIdByteArray(1);

                foreach (FileInfo file in dirInfo.GetFiles())
                {
                    // The MirrorProvider marks every file as executable (mode 755), but this is just a shortcut to avoid the pain of
                    // having to p/invoke to determine if the original file is exectuable or not.
                    // GVFS will get this info out of the git index along with all the other info for projecting files.
                    UInt16 fileMode = Convert.ToUInt16("755", 8);

                    Result result = this.virtualizationInstance.WritePlaceholderFile(
                        Path.Combine(relativePath, file.Name),
                        providerId,
                        ToVersionIdByteArray(0), // contentId is not used in the MirrorProvider
                        (ulong)file.Length,
                        fileMode);
                    if (result != Result.Success)
                    {
                        Console.WriteLine("WritePlaceholderFile failed: " + result);
                        return result;
                    }
                }

                foreach (DirectoryInfo subDirectory in dirInfo.GetDirectories())
                {
                    Result result = this.virtualizationInstance.WritePlaceholderDirectory(
                        Path.Combine(relativePath, subDirectory.Name));
                    if (result != Result.Success)
                    {
                        Console.WriteLine("WritePlaceholderDirectory failed: " + result);
                        return result;
                    }
                }
            }
            catch (IOException e)
            {
                Console.WriteLine("IOException in MirrorProvider.OnEnumerateDirectory: " + e.Message);
                return Result.EIOError;
            }

            return Result.Success;
        }

        private Result OnGetFileStream(ulong commandId, string relativePath, byte[] providerId, byte[] contentId, int triggeringProcessId, string triggeringProcessName, IntPtr fileHandle)
        {
            Console.WriteLine($"MirrorProvider.OnGetFileStream({commandId}, '{relativePath}', {contentId.Length}/{contentId[0]}:{contentId[1]}, {providerId.Length}/{providerId[0]}:{providerId[1]}, {triggeringProcessId}, {triggeringProcessName}, 0x{fileHandle.ToInt64():X})");

            try
            {
                string fullPathInMirror = Path.Combine(this.enlistment.MirrorRoot, relativePath);
                if (!File.Exists(fullPathInMirror))
                {
                    return Result.EFileNotFound;
                }

                using (FileStream fs = new FileStream(fullPathInMirror, FileMode.Open))
                {
                    long remainingData = fs.Length;
                    byte[] buffer = new byte[4096];

                    while (remainingData > 0)
                    {
                        int bytesToCopy = (int)Math.Min(remainingData, buffer.Length);
                        if (fs.Read(buffer, 0, bytesToCopy) != bytesToCopy)
                        {
                            Console.WriteLine("Failed to read requested bytes");
                            return Result.EIOError;
                        }

                        Result result = this.virtualizationInstance.WriteFileContents(
                            fileHandle,
                            buffer,
                            (uint)bytesToCopy);
                        if (result != Result.Success)
                        {
                            Console.WriteLine("WriteFileContents failed: " + result);
                            return result;
                        }

                        remainingData -= bytesToCopy;
                    }
                }
            }
            catch (IOException e)
            {
                Console.WriteLine("IOException in MirrorProvider.OnGetFileStream: " + e.Message);
                return Result.EIOError;
            }

            return Result.Success;
        }

        private static byte[] ToVersionIdByteArray(byte version)
        {
            byte[] bytes = new byte[VirtualizationInstance.PlaceholderIdLength];
            bytes[0] = version;

            return bytes;
        }
    }
}
