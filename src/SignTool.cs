//Source: https://gist.githubusercontent.com/gregmac/4cfacea5aaf702365724/raw/c57fe6f58d04a09f4ec15411b8e3a8ff4e3f45b1/signtool.msbuild.tasks

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;

namespace MSBuildCustomTasks
{
    public class SignTool : CallTarget
    {
        public override bool Execute()
        {
            var pfxFileExists = !string.IsNullOrWhiteSpace(PfxFile) && File.Exists(PfxFile);
            var sha1ThumbprintExists = !string.IsNullOrWhiteSpace(PfxSha1Thumbprint);
            if (!( pfxFileExists || sha1ThumbprintExists))
            {
                LogError("PfxSha1Thumbprint for an imported certificate has not been specified.", ContinueBuildOnFailure);
                LogError("PfxFile does not exists: " + PfxFile , ContinueBuildOnFailure);
                LogError("Either PfxSha1Thumbprint or PfxFile is required.", ContinueBuildOnFailure);
                LogError("Skipping signing.", ContinueBuildOnFailure);
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
            var signResult = SignResult.Success;
            foreach (var timeStampServer in timeStampServers)
            {
                var signtoolParams = GetSignToolArguments(signFiles, PfxSha1Thumbprint, PfxFile, PfxPassword, timeStampServer, Description);

                signResult = ExecuteSignTool(signtoolParams);
                if (signResult != SignResult.TimeServerError)
                    break;
            }
            return ContinueBuildOnFailure || (signResult == SignResult.Success);
        }

        private SignResult ExecuteSignTool(string signtoolParams)
        {
            var pfxPassword = GetPfxPassword(PfxPassword);

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

        private string GetSignToolArguments(IEnumerable<string> signFiles, string pfxSha1Thumbprint, string pfxFile, string pfxPassword, string timestampServer, string description)
        {
            var signToolArgumentsBuilder = new StringBuilder();
            signToolArgumentsBuilder.Append("sign");
            if (!string.IsNullOrWhiteSpace(PfxSha1Thumbprint))
            {
                signToolArgumentsBuilder.Append(" /sha1 " + EncodeParameterArgument(pfxSha1Thumbprint));
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(pfxFile) && File.Exists(pfxFile))
                {
                    signToolArgumentsBuilder.Append(" /f " + EncodeParameterArgument(pfxFile));
                }
                if (!string.IsNullOrWhiteSpace(pfxPassword))
                {
                    signToolArgumentsBuilder.Append(" /p " + EncodeParameterArgument(pfxPassword));
                }
            }
            if(!string.IsNullOrWhiteSpace(timestampServer))
            {
                signToolArgumentsBuilder.Append(" /t " + EncodeParameterArgument(timestampServer));
            }
            if(!string.IsNullOrWhiteSpace(description))
            {
                signToolArgumentsBuilder.Append(" /d " + EncodeParameterArgument(description));
            }
            signToolArgumentsBuilder.Append(" " + string.Join(" ", signFiles.Select(EncodeParameterArgument)));
            var signToolArguments = signToolArgumentsBuilder.ToString();
            return signToolArguments;
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

        private string CensoreMessage(string message, string secretString)
        {
            if (!string.IsNullOrWhiteSpace(secretString))
            {
                var censoredString = new string('*', secretString.Length);
                message = message.Replace(secretString, censoredString);
            }
            return message;
        }

        private void LogCensoredMessage(string message, string pfxPassword)
        {
            message = CensoreMessage(message, pfxPassword);
            Log.LogMessage(message);
        }

        private void LogCensoredError(string message, string pfxPassword)
        {
            message = CensoreMessage(message, pfxPassword);
            LogError(message, ContinueBuildOnFailure);
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

        private string GetPfxPassword(string pfxPassword)
        {
            return pfxPassword ?? string.Empty;
        }

        public string SignToolExe { get; set; }
        
        public string PfxFile { get; set; }

        public string PfxPassword { get; set; }

        public string PfxSha1Thumbprint { get; set; }

        [Required]
        public ITaskItem[] TimeStampServer { get; set; }

        public string Description { get; set; }

        [Required]
        public ITaskItem[] SignFiles { get; set; }

        [Required]
        public bool ContinueBuildOnFailure { get; set; }
    }
}