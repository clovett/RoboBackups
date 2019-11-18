using RoboBackups.Controls;
using RoboBackups.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

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

            System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandledException);
            App.Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(OnDispatcherUnhandledException);
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception != null)
            {
                HandleUnhandledException(e.Exception);
            }
            e.SetObserved();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log it to error window instead of crashing the app
            if (e.IsTerminating)
            {
                string msg = null;
                if (e.ExceptionObject != null)
                {
                    msg = "The reason is:\n" + e.ExceptionObject.ToString();
                }

                MessageBoxEx.Show("The program is terminating", "Terminating", msg, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (e.ExceptionObject != null)
            {
                HandleUnhandledException(e.ExceptionObject);
            }
        }

        bool HandleUnhandledException(object exceptionObject)
        {
            Exception ex = exceptionObject as Exception;
            string message = null;
            string details = null;
            if (ex == null && exceptionObject != null)
            {
                message = exceptionObject.GetType().FullName;
                details = exceptionObject.ToString();
            }
            else
            {
                message = ex.Message;
                details = ex.ToString();
            }

            try
            {
                UiDispatcher.RunOnUIThread(() =>
                {
                    MessageBoxEx.Show(message, "Unhandled Exception", details, MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return true;
            }
            catch (Exception)
            {
                // hmmm, if we can't show the dialog then perhaps this is some sort of stack overflow.
                // save the details to a file, terminate the process and                 
            }
            return false;
        }

        // stop re-entrancy
        bool handlingException;

        void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (handlingException)
            {
                e.Handled = false;
            }
            else
            {
                handlingException = true;
                UiDispatcher.RunOnUIThread(new Action(() =>
                {
                    try
                    {
                        e.Handled = HandleUnhandledException(e.Exception);
                    }
                    catch (Exception)
                    {
                    }
                    handlingException = false;
                }));
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
        Brush defaultButtonBrush;
        CancellationTokenSource cancel;
        ErrorLog _errors;

        private void StopBackup()
        {
            if (cancel != null && !cancel.IsCancellationRequested)
            {
                cancel.Cancel();
            }
            if (ButtonBackup.Tag != null)
            {
                ButtonBackup.Content = ButtonBackup.Tag;
                ButtonBackup.Tag = null;
                if (defaultButtonBrush != null)
                {
                    ButtonBackup.Background = defaultButtonBrush;
                }
            }

            if (ButtonShutdownBackup.Tag != null)
            {
                ButtonShutdownBackup.Content = ButtonShutdownBackup.Tag;
                ButtonShutdownBackup.Tag = null;
                if (defaultButtonBrush != null)
                {
                    ButtonShutdownBackup.Background = defaultButtonBrush;
                }
            }
            ButtonBackup.IsEnabled = true;
            ButtonShutdownBackup.IsEnabled = true;
            if (_log != null)
            {
                _log.OnUpdate();
                if (_errors != null)
                {
                    string errors = _errors.ToString();
                    _log.WriteErrors(errors.Split('\n'));
                    _errors = null;
                }

                _log.WriteLine("===========================================================");
                _log.WriteLine("BACKUP COMPLETE ");
                _log.WriteLine("===========================================================");
            }
        }

        private void OnBackup(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            if (button.Tag != null)
            {
                StopBackup();
            }
            else
            {
                LogDocument.Blocks.Clear();
                StopBackup();
                ButtonShutdownBackup.IsEnabled = false;
                ButtonBackup.Tag = ButtonBackup.Content;
                ButtonBackup.Content = "Cancel";
                if (defaultButtonBrush == null)
                {
                    defaultButtonBrush = ButtonBackup.Background;
                }
                ButtonBackup.Background = new SolidColorBrush(Color.FromRgb(0x97, 0x36, 0x27));

                // give UI a chance to update before we start the big thing
                delayedActions.StartDelayedAction("Backup", () => Backup(false), TimeSpan.FromMilliseconds(100));
            }
        }

        private void OnBackupShutdown(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            if (button.Tag != null)
            {
                StopBackup();
            }
            else
            {
                LogDocument.Blocks.Clear();
                StopBackup();
                ButtonBackup.IsEnabled = false;
                ButtonShutdownBackup.Tag = ButtonShutdownBackup.Content;
                ButtonShutdownBackup.Content = "Cancel";
                if (defaultButtonBrush == null)
                {
                    defaultButtonBrush = ButtonShutdownBackup.Background;
                }
                ButtonShutdownBackup.Background = new SolidColorBrush(Color.FromRgb(0x97, 0x36, 0x27));

                // give UI a chance to update before we start the big thing
                delayedActions.StartDelayedAction("Backup", () => Backup(true), TimeSpan.FromMilliseconds(100));
            }
        }

        void Backup(bool shutdown)
        {
            backup = new Backup();
            var log = new FlowDocumentLog(ConsoleTextBox);
            this._errors = new ErrorLog();
            this._log = log;
            this.cancel = new CancellationTokenSource();
            Task.Run(() =>
            {
                try
                {
                    backup.Run(log, this._errors, cancel);
                }
                catch (Exception ex)
                {
                    backup.Error = ex.Message;
                    backup.Complete = true;
                    log.WriteLine(ex.Message);
                }
                if (shutdown)
                {
                    backup.Shutdown();
                    UiDispatcher.RunOnUIThread(ShowCancelShutdown);
                }

                UiDispatcher.RunOnUIThread(StopBackup);
            });
        }

        void ShowCancelShutdown()
        {
            ButtonCancelShutdown.Visibility = Visibility.Visible;
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
                if (MessageBoxEx.Show("Backup is running, do you want to stop the backup?", "Terminate backup", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            StopBackup();
            base.OnClosing(e);
        }

        class ErrorLog : BackupLog
        {
            StringBuilder text = new StringBuilder();

            public override void WriteLine(string message)
            {
                text.AppendLine(message);
            }

            public override string ToString()
            {
                return text.ToString();
            }
        }

        FlowDocumentLog _log;

        class FlowDocumentLog : BackupLog
        {
            RichTextBox box;
            FlowDocument doc;
            List<string> pending = new List<string>();
            DelayedActions delayedActions = new DelayedActions();
            bool actionPending;
            bool uiUpdated;

            public FlowDocumentLog(RichTextBox box)
            {
                this.box = box;
                this.doc = box.Document;
                string filename = Settings.LogFile;
            }

            public override void WriteLine(string message)
            {
                if (message == null)
                {
                    return;
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

            public void OnUpdate()
            {
                string[] toUpdate = null;
                lock (pending)
                {
                    if (pending.Count > 0) {
                        toUpdate = new string[pending.Count];
                        pending.CopyTo(toUpdate);
                        pending.Clear();
                    }
                }
                if (toUpdate != null)
                {
                    AppendLines(toUpdate);
                }
                box.Dispatcher.BeginInvoke(new Action(OnUiUpdated), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }

            public void AppendLines(string[] lines, Brush foreground = null)
            {
                bool scrollToEnd = true;
                var ptr = box.Selection.End;
                var end = box.Document.ContentEnd;
                if (ptr.GetOffsetToPosition(end) > 10)
                {
                    scrollToEnd = false;
                }

                foreach (string line in lines)
                {
                    if (doc.Blocks.Count == 0)
                    {
                        doc.Blocks.Add(new Paragraph());
                    }
                    Paragraph p = (Paragraph)doc.Blocks.FirstOrDefault();
                    var run = new Run() { Text = line };
                    if (foreground != null) {
                        run.Foreground = foreground;
                    }
                    p.Inlines.Add(run);
                    p.Inlines.Add(new LineBreak());
                }

                if (scrollToEnd)
                {
                    box.Selection.Select(doc.ContentEnd, doc.ContentEnd);
                    box.ScrollToEnd();
                }
            }

            void OnUiUpdated()
            {
                actionPending = false;
            }

            internal void WriteErrors(string[] errors)
            {
                AppendLines(errors, Brushes.Red);
            }
        }

        private void OnCancelShutdown(object sender, RoutedEventArgs e)
        {
            ButtonCancelShutdown.Visibility = Visibility.Hidden;
            backup.CancelShutdown();
        }
    }
}
