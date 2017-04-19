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

        public override bool Execute()
        {            
            if (string.IsNullOrWhiteSpace(SccmToolsExe) || !File.Exists(SccmToolsExe))
            {
                LogError($"Could not find sccmtools.exe '{SccmToolsExe}'", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            if (string.IsNullOrWhiteSpace(PackageDefinitionFile) || !File.Exists(PackageDefinitionFile))
            {
                LogError($"Could not find package definition file '{PackageDefinitionFile}'", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            var sccmToolsExeArguments = $"CreateApplicationFromDefinition /packageDefinitionFile=\"{PackageDefinitionFile}\"";
            var exitCode = ProcessOperation.StartProcess(SccmToolsExe, sccmToolsExeArguments, OnOut, OnError, Log);
            return ContinueBuildOnFailure || (exitCode == 0);
        }

        private void OnError(object sender, DataReceivedEventArgs e)
        {
            LogError(e.Data, ContinueBuildOnFailure);
        }

        private void OnOut(object sender, DataReceivedEventArgs e)
        {
            Log.LogMessage(e.Data);
        }

        public string SccmToolsExe
        {
            get { return _sccmToolsExe ?? (_sccmToolsExe = GetSccmToolsExe()); }
            set { _sccmToolsExe = value; }
        }

        [Required]
        public string PackageDefinitionFile { get; set; }

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

        public bool ContinueBuildOnFailure { get; set; }

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
