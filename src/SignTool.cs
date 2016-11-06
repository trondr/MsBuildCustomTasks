//Source: https://gist.githubusercontent.com/gregmac/4cfacea5aaf702365724/raw/c57fe6f58d04a09f4ec15411b8e3a8ff4e3f45b1/signtool.msbuild.tasks

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;

namespace MSBuildCustomTasks
{
    public class SignTool : CallTarget
    {
        public override bool Execute()
        {
            var pfxFileExists = File.Exists(PfxFile);
            if (!pfxFileExists)
            {
                LogError("Pfx file does not exist. Skipping signing.", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            if (!FindSignToolExe())
            {
                LogError("Could not find signtool.exe", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            var timeStampServers = TimeStampServer.Select(item => item.ItemSpec).ToList();
            if (timeStampServers.Count == 0)
            {
                LogError("Time stamp servers has not been specified.", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            var signFiles = SignFiles.Select(item => item.ItemSpec).ToList();
            if (signFiles.Count == 0)
            {
                LogError("Number of files to sign is zero.", ContinueBuildOnFailure);
                return ContinueBuildOnFailure;
            }

            Log.LogMessage("Number of files to sign: " + signFiles.Count);
            var pfxPassword = GetPfxPassword(PfxPassword, PfxPasswordEncryptionKey);
            var signResult = SignResult.Success;
            foreach (var timeStampServer in timeStampServers)
            {
                signResult = ExecuteSignTool(signFiles, PfxFile, pfxPassword, Description, timeStampServer);
                if (signResult != SignResult.TimeServerError)
                    break;
            }
            return ContinueBuildOnFailure || (signResult == SignResult.Success);
        }

        private SignResult ExecuteSignTool(IEnumerable<string> signFiles, string pfxFile, string pfxPassword, string description, string timestampServer)
        {
            var signtoolParams = "sign /f " + EncodeParameterArgument(pfxFile);
            if (!string.IsNullOrEmpty(pfxPassword)) signtoolParams += " /p " + EncodeParameterArgument(pfxPassword);
            if (!string.IsNullOrEmpty(timestampServer)) signtoolParams += " /t " + EncodeParameterArgument(timestampServer);
            if (!string.IsNullOrEmpty(description)) signtoolParams += " /d " + EncodeParameterArgument(description);
            signtoolParams += " " + string.Join(" ", signFiles.Select(x => EncodeParameterArgument(x)));

            LogCensoredMessage(string.Format("Executing: {0} {1}", SignToolExe, signtoolParams), pfxPassword);

            SignResult signResult;
            using (var process = new Process())
            {
                var startInfo = new ProcessStartInfo(SignToolExe, signtoolParams)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                signResult = SignResult.Failed;
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        LogCensoredError(args.Data, pfxPassword);
                        // "Error: The specified timestamp server could not be reached. "
                        // "Error: The specified timestamp server either could not be reached or returned an invalid response."
                        if (args.Data.Contains("timestamp server") && args.Data.Contains("could not be reached"))
                            signResult = SignResult.TimeServerError;
                    }
                };

                var nullReceived = false;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) LogCensoredMessage(e.Data, pfxPassword);
                    else nullReceived = true;
                };
                process.StartInfo = startInfo;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                // note, WaitForExit does not mean we're done reading output, so we wait until we get a NULL
                while (!nullReceived) System.Threading.Thread.Sleep(100);
                if (process.ExitCode != 0)
                {
                    LogCensoredError(string.Format("Signtool exited code {0}", process.ExitCode), pfxPassword);
                    signResult = signResult == SignResult.TimeServerError ? SignResult.TimeServerError : SignResult.Failed;
                }
                else
                {
                    LogCensoredMessage(string.Format("Signtool exited code {0}", process.ExitCode), pfxPassword);
                    signResult = SignResult.Success;
                }
                return signResult;
            }
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

        private void LogCensoredMessage(string message, string pfxPassword)
        {
            var censored = new String('*', pfxPassword.Length);
            Log.LogMessage(message.Replace(PfxPassword, censored));
        }

        private void LogCensoredError(string message, string pfxPassword)
        {
            var censored = new String('*', PfxPassword.Length);
            LogError(message.Replace(PfxPassword, censored), ContinueBuildOnFailure);
        }

        private bool FindSignToolExe()
        {
            if (!string.IsNullOrEmpty(SignToolExe)) return File.Exists(SignToolExe);

            var programFilesx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var signToolExeArray = new string[]
            {
                Path.Combine(programFilesx86,"Windows Kits","10","bin","x86","signtool.exe"),
                Path.Combine(programFilesx86,"Windows Kits","8.1","bin","x86","signtool.exe"),
                Path.Combine(programFilesx86,"Windows Kits","8.0","bin","x86","signtool.exe"),
                Path.Combine(programFilesx86,"Microsoft SDKs","Windows","v7.0A","bin","signtool.exe"),

                Path.Combine(programFiles,"Windows Kits","10","bin","x86","signtool.exe"),
                Path.Combine(programFiles,"Windows Kits","8.1","bin","x86","signtool.exe"),
                Path.Combine(programFiles,"Windows Kits","8.0","bin","x86","signtool.exe"),
                Path.Combine(programFiles,"Microsoft SDKs","Windows","v7.0A","bin","signtool.exe")
            };
            SignToolExe = signToolExeArray.FirstOrDefault(File.Exists);

            return !string.IsNullOrEmpty(SignToolExe) && File.Exists(SignToolExe);
        }

        /// from http://stackoverflow.com/a/12364234/7913
        /// <summary>
        /// Encodes an argument for passing into a program
        /// </summary>
        /// <param name="original">The value that should be received by the program</param>
        /// <returns>The value which needs to be passed to the program for the original value 
        /// to come through</returns>
        public static string EncodeParameterArgument(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;
            var value = Regex.Replace(original, @"(\\*)" + "\"", @"$1\$0");
            value = Regex.Replace(value, @"^(.*\s.*?)(\\*)$", "\"$1$2$2\"");
            return value;
        }

        private string GetPfxPassword(string pfxPassword, string pfxPasswordEncryptionKey)
        {
            if (string.IsNullOrEmpty(pfxPasswordEncryptionKey))
            {
                return pfxPassword;
            }
            throw new System.NotImplementedException("Decrypt pfx password");
        }

        public string SignToolExe { get; set; }

        [Required]
        public string PfxFile { get; set; }

        [Required]
        public string PfxPassword { get; set; }

        public string PfxPasswordEncryptionKey { get; set; }

        [Required]
        public ITaskItem[] TimeStampServer { get; set; }

        public string Description { get; set; }

        [Required]
        public ITaskItem[] SignFiles { get; set; }

        [Required]
        public bool ContinueBuildOnFailure { get; set; }
    }
}