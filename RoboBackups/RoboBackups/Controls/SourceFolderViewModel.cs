using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoboBackups.Controls
{
    public class SourceFolderViewModel
    {
        private ObservableCollection<SourceFolder> items;

        public SourceFolderViewModel()
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
