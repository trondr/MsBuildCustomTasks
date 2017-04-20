using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Win32;
using MSBuildCustomTasks.Common;

namespace MSBuildCustomTasks
{
    public class CreateSccmApplication: CallTarget
    {
        private string _sccmToolsExe;
        private string _applicationTargetRootFolder;

        public override bool Execute()
        {            
            if (string.IsNullOrWhiteSpace(SccmToolsExe) || !File.Exists(SccmToolsExe))
            {
                LogError($"Could not find sccmtools.exe '{SccmToolsExe}'", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            if (!Directory.Exists(ApplicationTargetRootFolder))
            {
                LogError($"Application source root folder (ApplicationTargetRootFolder={ApplicationTargetRootFolder}) does not exist.", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            if (string.IsNullOrWhiteSpace(ApplicationSourceFolder) || !Directory.Exists(ApplicationSourceFolder))
            {
                LogError($"Could not find application source folder '{ApplicationSourceFolder}'", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            if (string.IsNullOrWhiteSpace(PackageDefinitionFile) || !File.Exists(PackageDefinitionFile))
            {
                LogError($"Could not find package definition file '{PackageDefinitionFile}'", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            Log.LogMessage(MessageImportance.Normal,$"Preparing to move '{ApplicationSourceFolder}' to application target root folder '{ApplicationTargetRootFolder}'");

            var applicationFolderName = Path.GetFileName(ApplicationSourceFolder);
            var applicationTargetFolder = Path.Combine(ApplicationTargetRootFolder, applicationFolderName);

            if (Directory.Exists(applicationTargetFolder))
            {
                LogError($"Application target folder '{applicationTargetFolder}' allready exists", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            Log.LogMessage(MessageImportance.Normal, $"Moving '{ApplicationSourceFolder}'->'{applicationTargetFolder}'...");
            var moved = MoveApplication(ApplicationSourceFolder, applicationTargetFolder);
            if (!moved)
            {
                LogError($"Failed to move application '{ApplicationSourceFolder}->{applicationTargetFolder}' allready exists", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            var targetPackageDefinitionFile = PackageDefinitionFile.Replace(ApplicationSourceFolder, applicationTargetFolder);
            if (string.IsNullOrWhiteSpace(targetPackageDefinitionFile) || !File.Exists(targetPackageDefinitionFile))
            {
                LogError($"Could not find target package definition file '{targetPackageDefinitionFile}'", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            var sccmToolsExeArguments = $"CreateApplicationFromDefinition /packageDefinitionFile=\"{targetPackageDefinitionFile}\"";
            var exitCode = ProcessOperation.StartProcess(SccmToolsExe, sccmToolsExeArguments, OnOut, OnError, Log);
            if(exitCode != 0)
                LogError("SccmTools.exe failed to create application.", ContinueBuildOnFailure);
            return ContinueBuildOnFailure || exitCode == 0;
        }

        private bool MoveApplication(string applicationSourceFolder, string applicationTargetFolder)
        {
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(applicationSourceFolder, applicationTargetFolder);
                return true;
            }
            catch (Exception e)
            {
                LogError(e.Message, ContinueBuildOnFailure);
                return false;
            }
        }

        private void OnError(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                LogError(e.Data, ContinueBuildOnFailure);
        }

        private void OnOut(object sender, DataReceivedEventArgs e)
        {
            if(e.Data != null)
                Log.LogMessage(e.Data);
        }

        [Required]
        public string PackageDefinitionFile { get; set; }
        
        [Required]
        public string ApplicationSourceFolder { get; set; }

        public string SccmToolsExe
        {
            get { return _sccmToolsExe ?? (_sccmToolsExe = GetSccmToolsExe()); }
            set { _sccmToolsExe = value; }
        }

        public string ApplicationTargetRootFolder
        {
            get { return _applicationTargetRootFolder ?? (_applicationTargetRootFolder = GetApplicationTargetRootFolder()); }
            set { _applicationTargetRootFolder = value; }
        }

        private string GetApplicationTargetRootFolder()
        {
            var environmentVariableName = "MSBuildCustomTasks_CreateSccmApplication_ApplicationTargetRootFolder";
            var applicationSourceRootFolder =  Environment.GetEnvironmentVariable(environmentVariableName);
            if (string.IsNullOrWhiteSpace(applicationSourceRootFolder))
            {
                LogError($"The required 'ApplicationTargetRootFolder' property on the {this.GetType().Name} MSBuild task has not been defined. Please define this property directly on the {this.GetType().Name} task in the build script OR preferably by defining the environment variable '{environmentVariableName}'. Example: {environmentVariableName}=\\\\<servername>\\pkgsrc\\Applications", ContinueBuildOnFailure);
                return null;
            }
            return applicationSourceRootFolder;
        }

        public bool ContinueBuildOnFailure { get; set; }

        private string GetSccmToolsExe()
        {
            const string appKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\SccmTools.exe";
            string valueName = null;
            var rootKey = Registry.LocalMachine;

            var sccmToolsExe = RegistryOperation.GetValue(rootKey, appKeyPath, valueName) as string;
            if (!string.IsNullOrWhiteSpace(sccmToolsExe) && File.Exists(sccmToolsExe))
                return sccmToolsExe;

            const string appKeyPath32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\SccmTools.exe";
            sccmToolsExe = RegistryOperation.GetValue(rootKey, appKeyPath32, valueName) as string;
            if (!string.IsNullOrWhiteSpace(sccmToolsExe) && File.Exists(sccmToolsExe))
                return sccmToolsExe;

            Log.LogWarning($"Did not find SccmTools.exe by look up in Apps Paths (neither '{appKeyPath}' or '{appKeyPath32}'). SccmTools.exe do not seem to be installed. Please download and install SccmTools from https://github.com/trondr/SccmTools/releases or manually specify path to SccmTools.exe");
            return null;
        }

        private void LogError(string message, bool continueBuildOnFailure)
        {            
            if (continueBuildOnFailure)
            {
                Log.LogWarning(message);
            }
            else
            {
                Log.LogError(message);
            }
        }
    }
}
