using RoboBackups.Controls;
using RoboBackups.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace RoboBackups
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        DelayedActions delayedActions = new DelayedActions();

        public MainWindow()
        {
            InitializeComponent();

            UiDispatcher.Initialize();

            RestoreSettings();

            this.SizeChanged += OnWindowSizeChanged;
            this.LocationChanged += OnWindowLocationChanged;

            Settings.Instance.PropertyChanged += OnSettingsPropertyChanged;
            Settings.Instance.Model.Items.CollectionChanged += OnModelItemsChanged;
            foreach (SourceFolder f in Settings.Instance.Model.Items)
            {
                f.PropertyChanged += OnSourceFolderPropertyChanged;
            }

            string filename = Settings.LogFile;
            if (File.Exists(filename))
            {
                LoadLogFile(filename);
            }
        }

        private void OnModelItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (SourceFolder f in e.NewItems)
                {
                    f.PropertyChanged += OnSourceFolderPropertyChanged;
                }
            }
            DelayedSaveSettings();
        }

        private void OnSourceFolderPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            DelayedSaveSettings();
        }

        private void OnSettingsPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            DelayedSaveSettings();
        }

        void DelayedSaveSettings()
        {
            Settings.Instance.Model.EnsureNewItem();
            delayedActions.StartDelayedAction("SaveSettings", OnSaveSettings, TimeSpan.FromSeconds(1));
        }

        private async void OnSaveSettings()
        {
            try
            {
                await Settings.Instance.SaveAsync();
            }
            catch
            {
                // todo: try again in a bit.
            }
        }

        private void OnOpenFile(object sender, RoutedEventArgs e)
        {

        }

        private void OnClear(object sender, RoutedEventArgs e)
        {

        }

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            XamlExtensions.Flyout(AppSettingsPanel);
        }

        private void OnWindowLocationChanged(object sender, EventArgs e)
        {
            delayedActions.StartDelayedAction("SaveWindowLocation", SavePosition, TimeSpan.FromMilliseconds(1000));
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            delayedActions.StartDelayedAction("SaveWindowLocation", SavePosition, TimeSpan.FromMilliseconds(1000));
        }

        private void RestoreSettings()
        {
            Settings settings = Settings.Instance;
            if (settings.WindowLocation.X != 0 && settings.WindowSize.Width != 0 && settings.WindowSize.Height != 0)
            {
                // make sure it is visible on the user's current screen configuration.
                var bounds = new System.Drawing.Rectangle(
                    XamlExtensions.ConvertFromDeviceIndependentPixels(settings.WindowLocation.X),
                    XamlExtensions.ConvertFromDeviceIndependentPixels(settings.WindowLocation.Y),
                    XamlExtensions.ConvertFromDeviceIndependentPixels(settings.WindowSize.Width),
                    XamlExtensions.ConvertFromDeviceIndependentPixels(settings.WindowSize.Height));
                var screen = System.Windows.Forms.Screen.FromRectangle(bounds);
                bounds.Intersect(screen.WorkingArea);

                this.Left = XamlExtensions.ConvertToDeviceIndependentPixels(bounds.X);
                this.Top = XamlExtensions.ConvertToDeviceIndependentPixels(bounds.Y);
                this.Width = XamlExtensions.ConvertToDeviceIndependentPixels(bounds.Width);
                this.Height = XamlExtensions.ConvertToDeviceIndependentPixels(bounds.Height);
            }
            this.Visibility = Visibility.Visible;
        }

        void SavePosition()
        {
            var bounds = this.RestoreBounds;
            Settings settings = Settings.Instance;
            settings.WindowLocation = bounds.TopLeft;
            settings.WindowSize = bounds.Size;
        }

        Backup backup;
        CancellationTokenSource cancel;

        private void StopBackup()
        {
            if (cancel != null && !cancel.IsCancellationRequested)
            {
                cancel.Cancel();
            }
            if (_log != null)
            {
                _log.Dispose();
            }
        }

        private void OnBackup(object sender, RoutedEventArgs e)
        {
            Backup(false);
        }

        private void OnBackupAndShutdown(object sender, RoutedEventArgs e)
        {
            Backup(true);
        }

        void Backup(bool shutdown)
        {
            LogDocument.Blocks.Clear();
            StopBackup();
            backup = new Backup();
            var log = new FlowDocumentLog(ConsoleTextBox);
            this._log = log;
            this.cancel = new CancellationTokenSource();
            Task.Run(() =>
            {
                try
                {
                    backup.Run(log, cancel);
                }
                catch (Exception ex)
                {
                    log.WriteLine(ex.Message);
                }
                if (shutdown)
                {
                    backup.Shutdown();
                }
            });
        }

        private void LoadLogFile(string filename)
        {
            List<string> lines = new List<string>();
            using (var reader = new StreamReader(filename))
            {
                string line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            var flow = new FlowDocumentLog(ConsoleTextBox);
            foreach (string line in lines)
            {
                flow.WriteLine(line.Trim());
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (this.backup != null && !this.backup.Complete)
            {
                if (MessageBox.Show("Backup is running, do you want to stop the backup?", "Terminate backup", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            StopBackup();
            base.OnClosing(e);
        }

        FlowDocumentLog _log;

        class FlowDocumentLog : BackupLog, IDisposable
        {
            RichTextBox box;
            FlowDocument doc;
            List<string> pending = new List<string>();
            DelayedActions delayedActions = new DelayedActions();
            bool actionPending;
            TextWriter logFile;

            public FlowDocumentLog(RichTextBox box)
            {
                this.box = box;
                this.doc = box.Document;
                string filename = Settings.LogFile;
                logFile = new StreamWriter(filename, false, System.Text.Encoding.UTF8);
            }

            public void Dispose()
            {
                if (logFile != null)
                {
                    logFile.Close();
                    logFile = null;
                }
            }

            public override void WriteLine(string message)
            {
                if (message == null)
                {
                    return;
                }
                if (logFile != null)
                {
                    logFile.WriteLine(message);
                    logFile.Flush();
                }

                lock (pending)
                {
                    pending.Add(message);
                }
                if (!actionPending)
                {
                    actionPending = true;
                    delayedActions.StartDelayedAction("Update", OnUpdate, TimeSpan.FromMilliseconds(30)); // 30fps is plenty
                }
            }

            void OnUpdate()
            {
                actionPending = false;
                string[] toUpdate = null;
                lock (pending)
                {
                    if (pending.Count > 0) {
                        toUpdate = new string[pending.Count];
                        pending.CopyTo(toUpdate);
                        pending.Clear();
                    }
                }

                bool scrollToEnd = true;
                var ptr = box.Selection.End;
                var end = box.Document.ContentEnd;
                if (ptr.GetOffsetToPosition(end) > 10)
                {
                    scrollToEnd = false;
                }

                foreach (string line in toUpdate)
                {
                    if (doc.Blocks.Count == 0)
                    {
                        Paragraph p = new Paragraph();
                        doc.Blocks.Add(p);
                    }
                    Paragraph lines = (Paragraph)doc.Blocks.FirstOrDefault();
                    lines.Inlines.Add(new Run() { Text = line });
                    lines.Inlines.Add(new LineBreak());
                }

                if (scrollToEnd)
                {
                    box.Selection.Select(doc.ContentEnd, doc.ContentEnd);
                    box.ScrollToEnd();
                }
            }

        }

    }
}
