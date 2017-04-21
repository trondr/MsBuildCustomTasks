using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using MSBuildCustomTasks.Common;

namespace MSBuildCustomTasks
{
    public class ResolveScriptInstallPackage: CallTarget
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Normal, "Copying script install package template '{0}' to script install package target '{1}'...", ScriptInstallPackageSourcePath, ScriptInstallPackageTargetPath);
            DirectoryOperation.CopyDirectory(ScriptInstallPackageSourcePath, ScriptInstallPackageTargetPath);
            
            Log.LogMessage(MessageImportance.Normal, "Copying Windows Installer file '{0}' to script install package target '{1}'...", SourceMsiFile, TargetMsiFile);
            File.Copy(SourceMsiFile, TargetMsiFile, true);

            Log.LogMessage(MessageImportance.Normal, "Updating vendor install ini '{0}'...", VendorInstallIni);
            if (!File.Exists(VendorInstallIni))
                throw new FileNotFoundException("Vendor install ini file not found: " + VendorInstallIni);
            var iniFileOperation = new IniFileOperation();
            iniFileOperation.Write(VendorInstallIni, "VendorInstall", "MsiFile", Path.GetFileName(TargetMsiFile));

            Log.LogMessage(MessageImportance.Normal, "Updating package definition file '{0}'...", PackageDefinitionSms);

            iniFileOperation.Write(PackageDefinitionSms, "Package Definition", "Name", PackageDefinitionName);
            iniFileOperation.Write(PackageDefinitionSms, "Package Definition", "Version", PackageDefinitionVersion);
            iniFileOperation.Write(PackageDefinitionSms, "Package Definition", "Publisher", PackageDefinitionPublisher);
            iniFileOperation.Write(PackageDefinitionSms, "Package Definition", "Comment", PackageDefinitionComment);
            iniFileOperation.Write(PackageDefinitionSms, "INSTALL", "CommandLine", PackageDefinitionInstallCommandLine);            
            iniFileOperation.Write(PackageDefinitionSms, "UNINSTALL", "CommandLine", PackageDefinitionUnInstallCommandLine);
            var msiFileOperation = new MsiFileOperation();
            iniFileOperation.Write(PackageDefinitionSms, "DetectionMethod", "MsiProductCode", msiFileOperation.GetMsiProductCode(TargetMsiFile));

            WriteDependencies(iniFileOperation, PackageDefinitionSms);

            return base.Execute();
        }        

        private void WriteDependencies(IniFileOperation iniFileOperation, string packageDefinitionSms)
        {
            WriteDependency(iniFileOperation, packageDefinitionSms, "Dependency1", PackageDefinitionDependenciesDependency1);
            WriteDependency(iniFileOperation, packageDefinitionSms, "Dependency2", PackageDefinitionDependenciesDependency2);
            WriteDependency(iniFileOperation, packageDefinitionSms, "Dependency3", PackageDefinitionDependenciesDependency3);
            WriteDependency(iniFileOperation, packageDefinitionSms, "Dependency4", PackageDefinitionDependenciesDependency4);
            WriteDependency(iniFileOperation, packageDefinitionSms, "Dependency5", PackageDefinitionDependenciesDependency5);
            WriteDependency(iniFileOperation, packageDefinitionSms, "Dependency6", PackageDefinitionDependenciesDependency6);
            WriteDependency(iniFileOperation, packageDefinitionSms, "Dependency7", PackageDefinitionDependenciesDependency7);
            WriteDependency(iniFileOperation, packageDefinitionSms, "Dependency8", PackageDefinitionDependenciesDependency8);
            WriteDependency(iniFileOperation, packageDefinitionSms, "Dependency9", PackageDefinitionDependenciesDependency9);
        }

        private static void WriteDependency(IniFileOperation iniFileOperation, string packageDefinitionSms, string dependencyKeyName, string dependencyValueName)
        {
            if (!string.IsNullOrWhiteSpace(dependencyValueName))
            {
                iniFileOperation.Write(packageDefinitionSms, "Dependencies", dependencyKeyName, dependencyValueName);
            }
        }

        [Required]
        public string ScriptInstallPackageSourcePath { get; set; }

        [Required]
        public string ScriptInstallPackageTargetPath { get; set; }

        [Required]
        public string SourceMsiFile { get; set; }
        
        [Required]
        public string TargetMsiFile { get; set; }
       
        [Required]
        public string VendorInstallIni { get; set; }

        [Required]
        public string PackageDefinitionSms { get; set; }

        [Required]
        public string PackageDefinitionName { get; set; }

        [Required]
        public string PackageDefinitionVersion { get; set; }

        [Required]
        public string PackageDefinitionPublisher { get; set; }

        [Required]
        public string PackageDefinitionInstallCommandLine { get; set; }

        [Required]
        public string PackageDefinitionUnInstallCommandLine { get; set; }

        [Required]
        public string PackageDefinitionComment { get; set; }

        public string PackageDefinitionDependenciesDependency1 { get; set; }
        public string PackageDefinitionDependenciesDependency2 { get; set; }
        public string PackageDefinitionDependenciesDependency3 { get; set; }
        public string PackageDefinitionDependenciesDependency4 { get; set; }
        public string PackageDefinitionDependenciesDependency5 { get; set; }
        public string PackageDefinitionDependenciesDependency6 { get; set; }
        public string PackageDefinitionDependenciesDependency7 { get; set; }
        public string PackageDefinitionDependenciesDependency8 { get; set; }
        public string PackageDefinitionDependenciesDependency9 { get; set; }

    }
}
