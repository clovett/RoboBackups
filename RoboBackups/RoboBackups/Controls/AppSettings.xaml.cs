using System;
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
            if (ComboTargetFolder.SelectedIndex == -1 && !string.IsNullOrEmpty(ComboTargetFolder.Text))
            {
                try
                {
                    string path = ComboTargetFolder.Text;
                    string root = Path.GetPathRoot(path);
                    if (!root.Contains(":"))
                    {
                        // has no drive so grab the drive from current backup path
                        string backupPath = Settings.Instance.BackupPath;
                        if (string.IsNullOrEmpty(backupPath) && ComboTargetDrive.SelectedItem != null)
                        {
                            backupPath = ((DriveItem)ComboTargetDrive.SelectedItem).DriveInfo.Name;
                        }
                        if (!string.IsNullOrEmpty(backupPath))
                        {
                            path = Path.Combine(Path.GetPathRoot(backupPath), path);
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
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = ex.Message;
                }
            }
        }

        SourceFolderViewModel sourceModel;

        string GetCurrentBackupDrive()
        {
            string backupPath = Settings.Instance.BackupPath;
            string backupDrive = null;
            if (!string.IsNullOrEmpty(backupPath))
            {
                backupDrive = System.IO.Path.GetPathRoot(backupPath);
            }
            return backupDrive;
        }

        private void ComboTargetDrive_DropDownOpened(object sender, EventArgs e)
        {
            UpdateDriveSelection();
        }

        void UpdateDriveSelection()
        { 
            var backupDrive = GetCurrentBackupDrive();
            DriveItem biggest = null;
            ComboTargetDrive.Items.Clear();
            foreach (var drive in DriveInfo.GetDrives())
            {
                var item = new DriveItem(drive);
                ComboTargetDrive.Items.Add(item);
                if (string.Compare(drive.Name, backupDrive, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ComboTargetDrive.SelectedItem = item;
                }
                if (biggest == null || item.TotalFreeSpace > biggest.TotalFreeSpace)
                {
                    biggest = item;
                }
            }

            if (ComboTargetDrive.SelectedItem == null)
            {
                ComboTargetDrive.SelectedItem = biggest;
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
                var backupDrive = GetCurrentBackupDrive();
                if (string.Compare(item.DriveInfo.Name, backupDrive, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    // user is picking new drive.
                    Settings.Instance.BackupPath = item.DriveInfo.Name;
                    ComboTargetFolder.Items.Clear();
                }
            }
        }

        private void ComboTargetFolder_DropDownOpened(object sender, EventArgs e)
        {
            UpdateFolderSelection();
        }

        void UpdateFolderSelection()
        { 
            if (ComboTargetDrive.SelectedItem != null)
            {
                string backupPath = Settings.Instance.BackupPath;
                var item = ComboTargetDrive.SelectedItem as DriveItem;
                try
                {
                    ComboTargetFolder.Items.Clear();
                    var rootDir = item.DriveInfo.RootDirectory;
                    bool found = false;
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
                    if (!found)
                    {
                        ComboTargetFolder.Text = backupPath;
                    }
                }
                catch
                {

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
                // this is the backup path!
                Settings.Instance.BackupPath = dir.FullName;
                UpdateDriveSelection();
            }
        }

        private void OnLabelKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            FrameworkElement f = sender as FrameworkElement;
            if (f != null)
            {
                SourceFolder item = f.DataContext as SourceFolder;
                if (!item.IsNew())
                {
                    sourceModel.RemoveItem(item);
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
            this.DriveInfo = info;
        }

        public long TotalFreeSpace
        {
            get
            {
                try
                {
                    return DriveInfo.TotalFreeSpace;
                }
                catch { }
                return 0;
            }
        }

        public DriveInfo DriveInfo { get; set; }

        public override string ToString()
        {
            string s = DriveInfo.ToString();
            string connector = "\t";
            try
            {
                if (!string.IsNullOrEmpty(DriveInfo.VolumeLabel))
                {
                    s += "\t" + DriveInfo.VolumeLabel;
                    connector = ", ";
                }
            }
            catch
            {
            }
            try
            {
                s += connector + FormatDriveFreeSpace() + " free";
            }
            catch (Exception ex)
            {
                s += connector + ex.Message;
            }
            return s;
        }

        string FormatDriveFreeSpace()
        {
            double kilobytes = DriveInfo.TotalFreeSpace / 1024.0;
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
