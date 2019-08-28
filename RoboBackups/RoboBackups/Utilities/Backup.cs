using RoboBackups.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace RoboBackups.Utilities
{
    enum BackupResult
    {
        None,
        ConfigError,
        CopyError
    };

    class BackupException : Exception
    { 
        public BackupException(BackupResult rc, string message) : base(message)
        {
            this.Result = rc;
        }

        public BackupResult Result { get; set; }
    };

    abstract class BackupLog
    {
        public abstract void WriteLine(string message);
    }


    class Backup
    {
        BackupLog log;
        CancellationTokenSource cancellation;
        bool complete;
        Process process;

        public bool Complete { get => complete; set => complete = value; }

        public void Run(BackupLog log, CancellationTokenSource cancellation)
        {
            this.log = log;
            this.cancellation = cancellation;

            var model = Settings.Instance.Model;
            var targetPath = Settings.Instance.BackupPath;
            if (string.IsNullOrEmpty(targetPath))
            {
                throw new BackupException(BackupResult.ConfigError, "Missing target path");
            }
            if (!Directory.Exists(targetPath))
            {
                throw new BackupException(BackupResult.ConfigError, "Target path not found");
            }
            var realItems = (from folder in model.Items where folder.Path != SourceFolder.NewPath select folder);
            if (realItems.Count() == 0)
            {
                throw new BackupException(BackupResult.ConfigError, "No source folders configured");
            }
            
            foreach (var item in realItems)
            {
                Robocopy(item.Path, targetPath);
            }
        }

        string FindRobocopy()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string path = Path.Combine(windows, "system32", "robocopy.exe");
            if (!File.Exists(path))
            {
                throw new BackupException(BackupResult.ConfigError, path + " not found");
            }
            return path;
        }

        private void Robocopy(string sourcePath, string targetPath)
        {
            this.complete = false;
            string stem = sourcePath.Substring(Path.GetPathRoot(sourcePath).Length);
            string target = Path.Combine(targetPath, stem);
            string robocopy = FindRobocopy();
            if (!Directory.Exists(target))
            {
                Directory.CreateDirectory(target);
            }
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(robocopy,
                string.Format("{0} {1} /S /NP /XA:HS /R:3 /W:10 /NFL", sourcePath, target));
            info.CreateNoWindow = true;
            info.RedirectStandardError = true;
            info.RedirectStandardOutput = true;
            info.StandardErrorEncoding = System.Text.Encoding.UTF8;
            info.StandardOutputEncoding = System.Text.Encoding.UTF8;
            info.UseShellExecute = false;

            this.process = System.Diagnostics.Process.Start(info);

            Task.Run(new Action(() =>
            {
                ReadOutputThread(process.StandardError);
            }));
            Task.Run(new Action(() =>
            {
                ReadOutputThread(process.StandardOutput);
            }));

            while (!cancellation.IsCancellationRequested)
            {
                if (process.WaitForExit(1000))
                {
                    complete = true;
                    break;
                }
            }
            if (cancellation.IsCancellationRequested && !complete)
            {
                complete = true;
                process.Kill();
                throw new OperationCanceledException("Backup was cancelled.");
            }
        }

        void ReadOutputThread(StreamReader reader)
        {
            try
            {
                while (!complete)
                {
                    string line = reader.ReadLine();
                    lock (this.log)
                    {
                        this.log.WriteLine(line);
                    }
                }
            }
            catch { }
        }

        internal void Shutdown()
        {
            log.WriteLine("Shutting down in 60 seconds...");
            var psi = new ProcessStartInfo("shutdown", "/s /t 60");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }

        internal void CancelShutdown()
        {
            log.WriteLine("Shutdown cancelled.");
            var psi = new ProcessStartInfo("shutdown", "/a");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }
    }
}
