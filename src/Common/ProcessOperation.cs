using System.Diagnostics;
using Microsoft.Build.Utilities;

namespace MSBuildCustomTasks.Common
{
    public class ProcessOperation
    {
        public static int StartProcess(string exePath, string arguments, DataReceivedEventHandler onOut, DataReceivedEventHandler onError, TaskLoggingHelper log)
        {
        
            using (var process = new Process())
            {
                var startInfo = new ProcessStartInfo(exePath, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                process.ErrorDataReceived+= onError;
                process.OutputDataReceived += onOut;
                process.Exited += (sender, args) =>
                {
                    var proc = (Process) sender;                   
                    log.LogMessage($"Exiting process: \"{exePath}\" {arguments}. Exitcode: {proc.ExitCode}");
                };                
                process.StartInfo = startInfo;
                log.LogMessage($"Starting process: \"{exePath}\" {arguments}");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}
