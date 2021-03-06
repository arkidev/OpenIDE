using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using OpenIDE.Core.FileSystem;
using OpenIDE.Core.CommandBuilding;

namespace CoreExtensions
{
    public static class ProcessExtensions
    {
        private static Action<string> _logger = (msg) => {};

        public static void SetLogger(this Process proc, Action<string> logger) {
            _logger = logger;
        }

        public static Func<string,string> GetInterpreter = (file) => null;

        public static void Write(this Process proc, string msg) {
            try {
                proc.StandardInput.WriteLine(msg);
                proc.StandardInput.Flush();
            } catch {
            }
        }

        public static void Run(this Process proc, string command, string arguments,
                               bool visible, string workingDir) {
            Run(proc, command, arguments, visible, workingDir, new KeyValuePair<string,string>[] {});
        }
        
        public static void Run(this Process proc, string command, string arguments,
                               bool visible, string workingDir,
                               IEnumerable<KeyValuePair<string,string>> replacements) {
            if (handleOiLnk(ref command, ref arguments, workingDir, (e,l) => {}, replacements))
                return;
			arguments = replaceArgumentPlaceholders(arguments, replacements);
            prepareInterpreter(ref command, ref arguments);
			arguments = replaceArgumentPlaceholders(arguments, replacements);
            prepareProcess(proc, command, arguments, visible, workingDir);
            proc.Start();
			proc.WaitForExit();
        }

        public static void Spawn(this Process proc, string command, string arguments,
                                 bool visible, string workingDir) {
            Spawn(proc, command, arguments, visible, workingDir, new KeyValuePair<string,string>[] {});
        }

        public static void Spawn(this Process proc, string command, string arguments,
                                 bool visible, string workingDir,
                                 IEnumerable<KeyValuePair<string,string>> replacements) {
            if (handleOiLnk(ref command, ref arguments, workingDir, (e,l) => {}, replacements))
                return;
			arguments = replaceArgumentPlaceholders(arguments, replacements);
            prepareInterpreter(ref command, ref arguments);
            prepareProcess(proc, command, arguments, visible, workingDir);
            proc.Start();
        }

        public static IEnumerable<string> QueryAll(this Process proc, string command, string arguments,
                                                   bool visible, string workingDir,
                                                   out string[] errors) {
            return QueryAll(proc, command, arguments, visible, workingDir, new KeyValuePair<string,string>[] {}, out errors);
        }

        public static IEnumerable<string> QueryAll(this Process proc, string command, string arguments,
                                                   bool visible, string workingDir,
                                                   IEnumerable<KeyValuePair<string,string>> replacements,
                                                   out string[] errors) {
            errors = new string[] {};
            if (handleOiLnk(ref command, ref arguments, workingDir, (e,l) => {}, replacements))
                return new string[] {};
			arguments = replaceArgumentPlaceholders(arguments, replacements);
            prepareInterpreter(ref command, ref arguments);
            prepareProcess(proc, command, arguments, visible, workingDir);
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            errors = 
                proc.StandardError.ReadToEnd()
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            proc.WaitForExit();
            return output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        }

        public static void Query(this Process proc, string command, string arguments,
                                 bool visible, string workingDir,
                                 Action<bool, string> onRecievedLine) {
             Query(proc, command, arguments, visible, workingDir, onRecievedLine, new KeyValuePair<string,string>[] {});
        }

        public static void Query(this Process proc, string command, string arguments,
                                 bool visible, string workingDir,
                                 Action<bool, string> onRecievedLine,
                                 IEnumerable<KeyValuePair<string,string>> replacements) {
            Query(proc, command, arguments, visible, workingDir, onRecievedLine, new KeyValuePair<string,string>[] {}, (args) => {});
        }

        public static void Query(this Process proc, string command, string arguments,
                                 bool visible, string workingDir,
                                 Action<bool, string> onRecievedLine,
                                 IEnumerable<KeyValuePair<string,string>> replacements,
                                 Action<string> preparedArguments) {
            _logger("Running process");
            var process = proc;
            var retries = 0;
            var exitCode = 255;
            _logger("About to start process");
            while (exitCode == 255 && retries < 5) {
                _logger("Running query");
                exitCode = query(process, command, arguments, visible, workingDir, onRecievedLine, replacements, preparedArguments);
                _logger("Done running with " + exitCode.ToString());
                retries++;
                // Seems to happen on linux when a file is beeing executed while being modified (locked)
                if (exitCode == 255) {
                    _logger("Recreating process");
                    process = new Process();
                    Thread.Sleep(100);
                }
                _logger("Done running process");
            }
        }

        private static int query(this Process proc, string command, string arguments,
                                 bool visible, string workingDir,
                                 Action<bool, string> onRecievedLine,
                                 IEnumerable<KeyValuePair<string,string>> replacements,
                                 Action<string> preparedArguments) {
            string tempFile = null;
            if (handleOiLnk(ref command, ref arguments, workingDir, onRecievedLine, replacements))
                return 0;
			arguments = replaceArgumentPlaceholders(arguments, replacements);
            if (!prepareInterpreter(ref command, ref arguments)) {
                if (Environment.OSVersion.Platform != PlatformID.Unix &&
                    Environment.OSVersion.Platform != PlatformID.MacOSX)
                {
                    if (Path.GetExtension(command).ToLower() != ".exe") {
                        arguments = getBatchArguments(command, arguments, ref tempFile);
                        command = "cmd.exe";
                    }
                }
            }
			
            prepareProcess(proc, command, arguments, visible, workingDir);
            preparedArguments(proc.StartInfo.Arguments);
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;

            DataReceivedEventHandler onOutputLine = 
                (s, data) => {
                    if (data.Data != null)
                        onRecievedLine(false, data.Data);
                };
            DataReceivedEventHandler onErrorLine = 
                (s, data) => {
                    if (data.Data != null)
                        onRecievedLine(true, data.Data);
                };

			proc.OutputDataReceived += onOutputLine;
            proc.ErrorDataReceived += onErrorLine;
            if (proc.Start())
            {
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
            }
            proc.OutputDataReceived -= onOutputLine;
            proc.ErrorDataReceived -= onErrorLine;
            
            if (tempFile != null && File.Exists(tempFile))
                File.Delete(tempFile);
            return proc.ExitCode;
        }

        private static bool processExists(int id) {
            return Process.GetProcesses().Any(x => x.Id == id);
        }

        private static bool handleOiLnk(ref string command, ref string arguments,
                                        string workingDir,
                                        Action<bool, string> onRecievedLine,
                                        IEnumerable<KeyValuePair<string,string>> replacements) {
            if (Path.GetExtension(command) != ".oilnk")
                return false;
            var args = new CommandStringParser(' ').Parse(arguments);
            var lnk = OiLnkReader.Read(File.ReadAllText(command));
            foreach (var handler in lnk.Handlers) {
                if (handler.Matches(args.ToArray())) {
                    handler.WriteResponses((line) => onRecievedLine(false, line));
                    return true;
                }
            }
            if (lnk.LinkCommand == null)
                return true;
            
            var fileDir = Path.GetDirectoryName(command);
            if (fileDir != null && File.Exists(Path.Combine(fileDir, lnk.LinkCommand)))
                command = Path.Combine(fileDir, lnk.LinkCommand);
            else if (File.Exists(Path.Combine(workingDir, lnk.LinkCommand)))
                command = Path.Combine(workingDir, lnk.LinkCommand);
            else
                command = lnk.LinkCommand;

            var originalArguments = arguments;
            foreach (var replacement in replacements)
                originalArguments = originalArguments.Replace(replacement.Key, "");
            arguments = 
                lnk.LinkArguments
                    .Replace("{args}", originalArguments).Trim();
            return false;
        }

        private static bool prepareInterpreter(ref string command, ref string arguments) {
            var interpreter = GetInterpreter(command);
            if (interpreter != null) {
                command = interpreter;
                arguments = "\"" + command + "\" " + arguments;
                return true;
            }
            return false;
        }

        private static string getBatchArguments(string command, string arguments, ref string tempFile) {
            var illagalChars = new[] {"&", "<", ">", "(", ")", "@", "^", "|"};
            if (command.Contains(" ") ||
                illagalChars.Any(x => arguments.Contains(x))) {
                // Windows freaks when getting the | character
                // Have it run a temporary bat file with command as contents
                tempFile = Path.GetTempFileName() + ".bat";
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
                File.WriteAllText(tempFile, "\"" + command + "\" " + arguments);
                arguments = "/c " + tempFile;
            } else {
                arguments = "/c " + 
                    "^\"" + batchEscape(command) + "^\" " +
                    batchEscape(arguments);
            }
            return arguments;
        }

        private static string batchEscape(string text) {
            foreach (var str in new[] { "^", " ", "&", "(", ")", "[", "]", "{", "}", "=", ";", "!", "'", "+", ",", "`", "~", "\"" })
                text = text.Replace(str, "^" + str);
            return text;
        }

		private static string replaceArgumentPlaceholders(string arg,  IEnumerable<KeyValuePair<string,string>> replacements)
		{
			foreach (var replacement in replacements)
				arg = arg.Replace(replacement.Key, replacement.Value);
			return arg;
		}
        
        private static void prepareProcess(
            Process proc,
            string command,
            string arguments,
            bool visible,
            string workingDir)
        {
           
            var info = new ProcessStartInfo(command, arguments);
            info.CreateNoWindow = !visible;
            if (!visible)
                info.WindowStyle = ProcessWindowStyle.Hidden;
            info.WorkingDirectory = workingDir;
            proc.StartInfo = info;
        }                
    }
}
