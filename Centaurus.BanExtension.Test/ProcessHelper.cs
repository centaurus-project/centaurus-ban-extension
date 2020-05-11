using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Centaurus.BanExtension.Test
{
    public static class ProcessHelper
    {
        public static Process StartNewProcess(string fileName, string arguments, string workingDir = null)
        {
            var pi = new ProcessStartInfo();
            //the file name should be full path, otherwise FileNotFoundException would be thrown, when UseShellExecute is set to true
            var filePath = FindFilePath(fileName);

            pi.FileName = filePath;
            pi.Arguments = arguments;
            pi.UseShellExecute = true;
            pi.WindowStyle = ProcessWindowStyle.Normal;
            pi.WorkingDirectory = Path.GetFullPath(workingDir ?? Path.GetDirectoryName(filePath));

            Debug.WriteLine($"About to start '{fileName} {arguments}'");

            return Process.Start(pi);
        }

        public static string FindFilePath(string file)
        {
            if (!File.Exists(file))
            {
                file = Environment.ExpandEnvironmentVariables(file);
                if (!File.Exists(file))
                {
                    if (Path.GetDirectoryName(file) == String.Empty)
                    {
                        foreach (string test in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                        {
                            string path = test.Trim();
                            if (string.IsNullOrEmpty(path))
                                continue;
                            var currentPath = Path.Combine(path, file);
                            if (File.Exists(currentPath))
                                return Path.GetFullPath(currentPath);
                            currentPath += ".exe";
                            if (File.Exists(currentPath))
                                return Path.GetFullPath(currentPath);
                        }
                    }
                    throw new FileNotFoundException(new FileNotFoundException().Message, file);
                }
            }
            return Path.GetFullPath(file);
        }
    }
}
