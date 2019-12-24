using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RoboBackups.Controls
{
    public class TargetFolderModel
    {
        private ObservableCollection<string> drives;
        private ObservableCollection<string> items;

        public TargetFolderModel()
        {
            drives = new ObservableCollection<string>();
            items = new ObservableCollection<string>();
        }

        [XmlIgnore]
        public ObservableCollection<string> TargetDrives { get { UpdateDrives();  return drives; } }

        public ObservableCollection<string> TargetPaths { get { return items; } set { items = value; UpdateDrives(); } }

        internal void AddTarget(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            string item = (from i in TargetPaths where string.Compare(i, path, StringComparison.OrdinalIgnoreCase) == 0 select i).FirstOrDefault();
            if (item == null)
            {
                items.Add(path);
            }
            UpdateDrives();
        }

        internal void RemoveTarget(string path)
        {
            this.items.Remove(path);
            UpdateDrives();
        }

        private void UpdateDrives()
        {
            HashSet<string> referencedDrives = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var path in this.TargetPaths)
            {
                var root = System.IO.Path.GetPathRoot(path);
                referencedDrives.Add(root);
            }
            // remove drives that no longer exist
            foreach (var item in drives.ToArray())
            {
                if (!referencedDrives.Contains(item))
                {
                    drives.Remove(item);
                }
            }
            // add new referenced drives
            foreach (var item in referencedDrives)
            {
                if (!drives.Contains(item))
                {
                    drives.Add(item);
                }
            }
        }

        internal List<string> RemoveTargetDrive(string drive)
        {
            List<string> removed = new List<string>();
            foreach (var item in items.ToArray())
            {
                var root = System.IO.Path.GetPathRoot(item);
                if (string.Compare(drive, root, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    items.Remove(item);
                    removed.Add(item);
                }
            }
            if (removed.Count > 0)
            {
                UpdateDrives();
            }
            return removed;
        }
    }

    public class SourceFolderModel
    {
        private ObservableCollection<SourceFolder> items;

        public SourceFolderModel()
        {
            items = new ObservableCollection<SourceFolder>();
        }

        public ObservableCollection<SourceFolder> Items { get { return items; } set { items = value; } }

        internal SourceFolder GetOrCreateItem(string path)
        {
            SourceFolder item = (from i in Items where string.Compare(i.Path, path, StringComparison.OrdinalIgnoreCase) == 0 select i).FirstOrDefault();
            if (item == null) 
            {
                item = new SourceFolder() { Path = path };
                this.items.Add(item);
            }
            return item;
        }

        internal void RemoveItem(SourceFolder item)
        {
            this.items.Remove(item);
        }

        internal void EnsureNewItem()
        {
            GetOrCreateItem(SourceFolder.NewPath);
        }
    }

    public class SourceFolder : INotifyPropertyChanged
    {
        string _path;
        public const string NewPath = "<add folder>";

        public SourceFolder() { }

        public string Path
        {
            get { return string.IsNullOrEmpty(_path) ? NewPath : _path; }
            set
            {
                if (_path != value)
                {
                    _path = value;
                    OnPropertyChanged("Path");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        internal bool IsNew()
        {
            return _path == NewPath;
        }

        public override string ToString()
        {
            return this.Path;
        }
    }

}
