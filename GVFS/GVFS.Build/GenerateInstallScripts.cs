using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;

namespace GVFS.PreBuild
{
    public class GenerateInstallScripts : Task
    {
        [Required]
        public string G4WInstallerPath { get; set; }

        [Required]
        public string GVFSSetupPath { get; set; }

        [Required]
        public string BuildOutputPath { get; set; }

        public override bool Execute()
        {
            this.Log.LogMessage(MessageImportance.High, "Creating install script for " + this.G4WInstallerPath);

            File.WriteAllText(
                Path.Combine(this.BuildOutputPath, "GVFS.Build", "InstallG4W.bat"),
                this.G4WInstallerPath + @" /DIR=""C:\Program Files\Git"" /NOICONS /COMPONENTS=""ext,ext\shellhere,ext\guihere,assoc,assoc_sh"" /GROUP=""Git"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART");

            File.WriteAllText(
                Path.Combine(this.BuildOutputPath, "GVFS.Build", "InstallGVFS.bat"),
                this.GVFSSetupPath + " /VERYSILENT /SUPPRESSMSGBOXES /NORESTART");

            return true;
        }
    }
}
