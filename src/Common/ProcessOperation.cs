using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Build.Utilities;

namespace MSBuildCustomTasks.Common
{
    public class ProcessOperation
    {
        public static int StartProcess(string exePath, string arguments, DataReceivedEventHandler onOut, DataReceivedEventHandler onError, TaskLoggingHelper log)
        {
            var taskCompletionSource = new TaskCompletionSource<int>();

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
                    taskCompletionSource.SetResult(proc.ExitCode);
                    log.LogMessage($"Exiting process: \"{exePath}\" {arguments}. Exitcode: {proc.ExitCode}");
                };                
                process.StartInfo = startInfo;
                log.LogMessage($"Starting process: \"{exePath}\" {arguments}");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                taskCompletionSource.Task.Wait();
                return taskCompletionSource.Task.Result;
            }
        }
    }
}
