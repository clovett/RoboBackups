using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using RoboBackups.Utilities;

namespace RoboBackups.Controls
{
    /// <summary>
    /// Interaction logic for AppSettings.xaml
    /// </summary>
    public partial class AppSettings : UserControl
    {
        bool initialized; 
        SourceFolderModel sourceModel;

        public AppSettings()
        {
            InitializeComponent();
            ComboTargetDrive.SelectionChanged += ComboTargetDrive_SelectionChanged;
            ComboTargetDrive.DropDownOpened += ComboTargetDrive_DropDownOpened;
            ComboTargetFolder.SelectionChanged += ComboTargetFolder_SelectionChanged;
            ComboTargetFolder.DropDownOpened += ComboTargetFolder_DropDownOpened;
            ComboTargetFolder.LostFocus += ComboTargetFolder_LostFocus;
            ComboTargetFolder.PreviewKeyDown += ComboTargetFolder_PreviewKeyDown;

            sourceModel = Settings.Instance.Model;
            sourceModel.GetOrCreateItem(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            sourceModel.GetOrCreateItem(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            sourceModel.GetOrCreateItem(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            sourceModel.EnsureNewItem();
            SourceFolders.ItemsSource = sourceModel.Items;

            UpdateDriveSelection();
            UpdateFolderSelection();
            initialized = true;
        }

        private HashSet<string> GetSourceDrives()
        {
            HashSet<string> result = new HashSet<string>();
            foreach (var item in sourceModel.Items)
            {
                if (item.Path != "<add folder>")
                {
                    string sourceDrive = System.IO.Path.GetPathRoot(item.Path.ToLowerInvariant());
                    result.Add(sourceDrive);
                }
            }
            return result;
        }

        private void ComboTargetFolder_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                CheckNewFolderName();
            }
        }

        private void ComboTargetFolder_LostFocus(object sender, RoutedEventArgs e)
        {
            CheckNewFolderName();
        }

        void CheckNewFolderName()
        {
            StatusText.Text = "";
            if (!string.IsNullOrEmpty(ComboTargetFolder.Text))
            {
                try
                {
                    string path = ComboTargetFolder.Text;
                    if (!Path.IsPathRooted(path))
                    {
                        // has no drive so grab the drive from current backup path
                        string root = null;
                        if (ComboTargetDrive.SelectedItem != null)
                        {
                            root = ((DriveItem)ComboTargetDrive.SelectedItem).RootDirectory;
                        }
                        if (!string.IsNullOrEmpty(root))
                        {
                            path = Path.Combine(root, path);
                        }
                    }
                    
                    if (!System.IO.Directory.Exists(path))
                    {
                        if (MessageBox.Show("Folder '" + path + "' does not exist, do you want to create it?", "Create new folder", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            DirectoryInfo info = System.IO.Directory.CreateDirectory(path);
                            ComboTargetFolder.Items.Add(info);
                            ComboTargetFolder.SelectedItem = info;
                        }
                        else
                        {
                            return; 
                        }
                    }
                    SetBackupPath(path);
                }
                catch (Exception ex)
                {
                    StatusText.Text = ex.Message;
                }
            }
        }

        void SetBackupPath(string path)
        {
            Debug.WriteLine("Setting backup path: " + path);
            Settings.Instance.BackupPath = path;
        }
        
        private void ComboTargetDrive_DropDownOpened(object sender, EventArgs e)
        {
            UpdateDriveSelection();
        }

        void UpdateDriveSelection()
        {
            var backupDrive = Settings.Instance.SelectedBackupDrive;
            var backupDrives = new HashSet<string>(Settings.Instance.Targets.TargetDrives, StringComparer.CurrentCultureIgnoreCase);
            DriveItem biggest = null;
            ComboTargetDrive.Items.Clear();
            var sourceDrives = GetSourceDrives();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (sourceDrives.Contains(drive.Name.ToLowerInvariant()))
                {
                    continue; // don't allow backing up to a source drive.
                }
                var item = new DriveItem(drive);
                ComboTargetDrive.Items.Add(item);
                if (string.Compare(drive.Name, backupDrive, StringComparison.OrdinalIgnoreCase) == 0 && ComboTargetDrive.SelectedItem == null)
                {
                    ComboTargetDrive.SelectedItem = item;
                }
                if (backupDrives.Contains(drive.RootDirectory.Name))
                {
                    item.IsSelected = true;
                    backupDrives.Remove(drive.RootDirectory.Name);
                }
                if (biggest == null || item.TotalFreeSpace > biggest.TotalFreeSpace)
                {
                    biggest = item;
                }
            }

            foreach (var path in Settings.Instance.Targets.TargetPaths)
            {
                var drive = System.IO.Path.GetPathRoot(path);
                if (backupDrives.Contains(drive))
                {
                    var item = new DriveItem(drive, drive, "drive is offline") { IsSelected = true };
                    ComboTargetDrive.Items.Add(item);
                    ComboTargetDrive.SelectedItem = item;
                }
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void ThemeSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                Settings.Instance.Theme = (AppTheme)e.AddedItems[0];
            }
        }

        private void ComboTargetDrive_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!initialized)
            {
                return;
            }
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                DriveItem item = e.AddedItems[0] as DriveItem;
                var backupDrive = Settings.Instance.SelectedBackupDrive;
                if (string.Compare(item.Name, backupDrive, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    // user is picking a new backup drive
                    Settings.Instance.SelectedBackupDrive = item.RootDirectory;
                    ComboTargetFolder.Items.Clear();
                    var backupPath = Settings.Instance.BackupPath;
                    ComboTargetFolder.Text = backupPath;
                    Settings.Instance.Targets.AddTarget(Settings.Instance.GetFullBackupPath(item.RootDirectory));
                }
            }
        }

        private void ComboTargetFolder_DropDownOpened(object sender, EventArgs e)
        {
            UpdateFolderSelection();
        }

        void UpdateFolderSelection()
        {
            DriveItem drive = ComboTargetDrive.SelectedItem as DriveItem;
            if (drive != null)
            {
                string backupPath = Settings.Instance.GetFullBackupPath(drive.RootDirectory);
                var item = ComboTargetDrive.SelectedItem as DriveItem;
                bool found = false;
                try
                {
                    ComboTargetFolder.Items.Clear();
                    DirectoryInfo rootDir = new DirectoryInfo(item.RootDirectory);
                    ComboTargetFolder.Items.Add(rootDir);
                    foreach (var dir in rootDir.GetDirectories())
                    {
                        if ((dir.Attributes & FileAttributes.Hidden) == 0 && (dir.Attributes & FileAttributes.System) == 0 && (dir.Attributes & FileAttributes.ReadOnly) == 0)
                        {
                            ComboTargetFolder.Items.Add(dir);
                            if (string.Compare(dir.FullName, backupPath, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                found = true;
                                ComboTargetFolder.SelectedItem = dir;
                            }
                        }
                    }
                }
                catch
                {
                }
                if (ComboTargetFolder.SelectedItem == null && !string.IsNullOrEmpty(Settings.Instance.BackupPath))
                {
                    ComboTargetFolder.Text = Settings.Instance.BackupPath;
                }
            }
        }

        private void ComboTargetFolder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!initialized)
            {
                return;
            }
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                DirectoryInfo dir = e.AddedItems[0] as DirectoryInfo;
                if (dir != null)
                {
                    var path = dir.FullName;
                    var item = ComboTargetDrive.SelectedItem as DriveItem;
                    if (item != null)
                    {
                        // unroot the backup path.
                        path = path.Substring(item.RootDirectory.Length);
                    }
                    // this is the backup path!
                    SetBackupPath(path);
                    UpdateDriveSelection();
                }
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            FrameworkElement f = sender as FrameworkElement;
            if (f != null)
            {
                SourceFolder item = f.DataContext as SourceFolder;
                if (item != null)
                {
                    if (!item.IsNew())
                    {
                        sourceModel.RemoveItem(item);
                    }
                }
                else
                {
                    DriveItem drive = f.DataContext as DriveItem;
                    if (drive != null)
                    {
                        var removed = Settings.Instance.Targets.RemoveTargetDrive(drive.RootDirectory);
                        var selected = (from i in removed where string.Compare(i, Settings.Instance.BackupPath, StringComparison.CurrentCultureIgnoreCase) == 0 select i).Any();
                        if (selected)
                        {
                            // pick a different backup folder then.
                            if (Settings.Instance.Targets.TargetPaths.Count > 0)
                            {
                                Settings.Instance.BackupPath = Settings.Instance.Targets.TargetPaths[0];
                            } else
                            {
                                Settings.Instance.BackupPath = null;
                            }
                        }
                        UpdateDriveSelection();
                        UpdateFolderSelection();
                    }
                }
            }
        }

        private void OnLabelTextBoxFocussed(object sender, EventArgs e)
        {
            EditableTextBlock edit = sender as EditableTextBlock;
            if (edit != null)
            {
                if (edit.Label == SourceFolder.NewPath)
                {
                    Ookii.Dialogs.Wpf.VistaFolderBrowserDialog fd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();                    
                    if (fd.ShowDialog() == true)
                    {
                        SourceFolder item = edit.DataContext as SourceFolder;
                        item.Path = fd.SelectedPath;
                        sourceModel.EnsureNewItem();
                    }
                }
            }
        }
    }

    public class DriveItem
    {
        public DriveItem(DriveInfo info)
        {
            this.Name = info.Name;
            try
            {
                this.RootDirectory = info.RootDirectory.FullName;
                this.VolumeLabel = info.VolumeLabel;
                this.TotalFreeSpace = info.TotalFreeSpace;
            }
            catch (Exception e)
            {
                VolumeLabel = e.Message;
            }
        }

        public DriveItem(string name, string path, string volumeLabel, long totalFreeSpace = 0)
        {
            this.Name = name;
            this.VolumeLabel = volumeLabel;
            this.RootDirectory = path;
            this.TotalFreeSpace = totalFreeSpace;
        }

        public string Name { get; set; }

        public string VolumeLabel { get; set; }

        public long TotalFreeSpace { get; set; }

        public string RootDirectory { get; internal set; }

        public string DisplayLabel {  get { return this.ToString(); } }

        public bool IsSelected { get; set; }

        public override string ToString()
        {
            string s = this.Name;
            string connector = "\t";
            if (!string.IsNullOrEmpty(this.VolumeLabel))
            {
                s += "\t" + this.VolumeLabel;
                connector = ", ";
            }
            if (this.TotalFreeSpace > 0)
            { 
                s += connector + FormatDriveFreeSpace() + " free";
            }
            return s;
        }

        string FormatDriveFreeSpace()
        {
            double kilobytes = this.TotalFreeSpace / 1024.0;
            if (kilobytes > 1024)
            {
                double megabytes = kilobytes / 1024.0;
                if (megabytes > 1024)
                {
                    double gigabytes = megabytes / 1024.0;
                    if (gigabytes > 1024)
                    {
                        double terrabytes = gigabytes / 1024.0;
                        if (terrabytes > 1000)
                        {
                            double petabytes = gigabytes / 1024.0;
                            return string.Format("{0:0.0} petabytes", petabytes);
                        }
                        return string.Format("{0:0.0} terrabytes", terrabytes);
                    }
                    return string.Format("{0:0.0} gigabytes", gigabytes);
                }
                return string.Format("{0:0.0} megabytes", megabytes);
            }
            return string.Format("{0:0.0} kilobytes", kilobytes);
        }

    }
}
