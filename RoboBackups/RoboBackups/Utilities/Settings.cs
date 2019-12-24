using Microsoft.Storage;
using RoboBackups.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace RoboBackups.Utilities
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public class Settings : INotifyPropertyChanged
    {
        const string SettingsFileName = "settings.xml";
        string backupDrive;
        string backupPath;
        Point windowLocation;
        Size windowSize;
        AppTheme theme = AppTheme.Dark;
        TargetFolderModel targets = new TargetFolderModel();
        SourceFolderModel model = new SourceFolderModel();

        static Settings _instance;

        public Settings()
        {
            _instance = this;
        }

        public static string SettingsFolder
        {
            get
            {
                string appSetttingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"LovettSoftware\RoboBackups");
                Directory.CreateDirectory(appSetttingsPath);
                return appSetttingsPath;
            }
        }

        public static string LogFile
        {
            get
            {
                string appSetttingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"LovettSoftware\RoboBackups");
                Directory.CreateDirectory(appSetttingsPath);
                return Path.Combine(appSetttingsPath, "log.txt");
            }
        }

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    return new Settings();
                }
                return _instance;
            }
        }

        public Point WindowLocation
        {
            get { return this.windowLocation; }
            set
            {
                if (this.windowLocation != value)
                {
                    this.windowLocation = value;
                    OnPropertyChanged("WindowLocation");
                }
            }
        }

        public Size WindowSize
        {
            get { return this.windowSize; }
            set
            {
                if (this.windowSize != value)
                {
                    this.windowSize = value;
                    OnPropertyChanged("WindowSize");
                }
            }
        }

        public AppTheme Theme
        {
            get { return this.theme; }
            set
            {
                if (this.theme != value)
                {
                    this.theme = value;
                    OnPropertyChanged("Theme");
                }
            }
        }

        public string SelectedBackupDrive
        {
            get
            {
                return this.backupDrive;
            }
            set
            {
                if (this.backupDrive != value)
                {
                    this.backupDrive = value;
                    OnPropertyChanged("SelectedBackupDrive");
                }
            }
        }

        public string BackupPath
        {
            get
            {
                return this.backupPath;
            }
            set
            {
                if (value != null && System.IO.Path.IsPathRooted(value))
                {
                    SelectedBackupDrive = System.IO.Path.GetPathRoot(value);
                    this.Targets.AddTarget(value);
                    value = value.Substring(SelectedBackupDrive.Length);
                    this.Migrated = true;
                }
                if (this.backupPath != value)
                {
                    this.backupPath = value;
                    OnPropertyChanged("BackupPath");
                }
            }
        }

        [XmlIgnore]
        public bool Migrated { get; set; }

        internal string GetFullBackupPath(string rootDirectory)
        {
            string path = this.backupPath;
            if (!string.IsNullOrEmpty(path))
            {
                return System.IO.Path.Combine(rootDirectory, path);
            }
            return null;
        }

        public TargetFolderModel Targets
        {
            get
            {
                return this.targets;
            }
            set
            {
                if (this.targets != value)
                {
                    this.targets = value;
                    OnPropertyChanged("Targets");
                }
            }
        }

        public SourceFolderModel Model
        {
            get
            {
                return model;
            }
            set
            {
                if (this.model != value)
                {
                    this.model = value;
                    OnPropertyChanged("Model");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                UiDispatcher.RunOnUIThread(() =>
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
                });
            }
        }

        public static Settings Load()
        {
            var store = new IsolatedStorage<Settings>();
            Settings result = null;
            try
            {
                Debug.WriteLine("Loading settings from : " + SettingsFolder);
                result = store.LoadFromFile(SettingsFolder, SettingsFileName);
            }
            catch
            {
            }
            if (result == null)
            {
                result = new Settings();
            }
            return result;
        }

        bool saving;

        public async Task SaveAsync()
        {
            var store = new IsolatedStorage<Settings>();
            if (!saving)
            {
                saving = true;
                try
                {
                    Debug.WriteLine("Saving settings to : " + SettingsFolder);
                    await store.SaveToFileAsync(SettingsFolder, SettingsFileName, this);
                }
                finally
                {
                    saving = false;
                }
            }
        }
    }


}
