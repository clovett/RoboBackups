using RoboBackups.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.ObjectModel;

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
        BackupLog errorLog;
        CancellationTokenSource cancellation;
        bool complete;
        Process process;

        public Backup()
        {
            AvailableBackupDrives = new ObservableCollection<string>();
        }

        public bool Complete { get => complete; set => complete = value; }

        public string Error { get; set; }

        public void Run(BackupLog log, BackupLog errorLog, CancellationTokenSource cancellation)
        {
            this.Error = null;
            this.log = log;
            this.errorLog = errorLog;
            this.cancellation = cancellation;

            var model = Settings.Instance.Model;
            if (string.IsNullOrEmpty(Settings.Instance.BackupPath))
            {
                throw new BackupException(BackupResult.ConfigError, "Missing target path - please use Settings button to setup your backup.");
            }

            // assume the BackupPath set via the AppSettings dialog is good to go as a backup path.
            if (!string.IsNullOrEmpty(Settings.Instance.BackupPath))
            {
                Settings.Instance.Targets.AddTarget(Settings.Instance.BackupPath);
            }

            if (AvailableBackupDrives.Count == 0)
            {
                throw new BackupException(BackupResult.ConfigError, 
                    string.Format("Cannot find your target backup drive{0} {1}\nPlease use the Settings button to fix your backup location.", Settings.Instance.Targets.TargetDrives.Count > 1 ? "s":"",
                    string.Join(", ", Settings.Instance.Targets.TargetDrives)));
            }

            foreach (var targetPath in Settings.Instance.Targets.TargetPaths)
            {
                var targetDrive = Path.GetPathRoot(targetPath);
                if (AvailableBackupDrives.Contains(targetDrive))
                {
                    if (!Directory.Exists(targetPath))
                    {
                        Directory.CreateDirectory(targetPath);
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
            }

        }

        public ObservableCollection<string> AvailableBackupDrives { get; set; }

        // Background task that runs periodically
        internal async void MonitorBackupDrives(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                HashSet<string> targetDrives = new HashSet<string>(Settings.Instance.Targets.TargetDrives, StringComparer.CurrentCultureIgnoreCase);
                HashSet<string> availableDrives = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                foreach (var drive in DriveInfo.GetDrives())
                {
                    var root = drive.RootDirectory.Name;
                    if (targetDrives.Contains(root))
                    { 
                        availableDrives.Add(root);
                    }
                }

                lock (AvailableBackupDrives)
                {
                    // remove drives that are not currently available.
                    foreach (var item in AvailableBackupDrives.ToArray())
                    {
                        if (!availableDrives.Contains(item))
                        {
                            AvailableBackupDrives.Remove(item);
                        }
                    }

                    // add drives that are now available
                    foreach (var item in availableDrives)
                    {
                        if (!AvailableBackupDrives.Contains(item))
                        {
                            AvailableBackupDrives.Add(item);
                        }
                    }
                }

                await Task.Delay(2000);
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
                string.Format("\"{0}\" \"{1}\" /S /NP /XA:HS /R:3 /W:10 /NFL", sourcePath, target));
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
                    lock (this.log)
                    {
                        this.log.WriteLine(string.Format("Robocopy returned {0}", process.ExitCode));
                    }
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
                    if (line == null)
                    {
                        return;
                    }
                    lock (this.log)
                    {
                        this.log.WriteLine(line);
                    }
                    if (line.Contains(" ERROR "))
                    {
                        lock (this.errorLog) {
                            this.errorLog.WriteLine(line);
                        }
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
