using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RoboBackups.Utilities;

namespace RoboBackups.Controls
{
    public class ComboBoxItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SelectedTemplate { get; set; }
        public DataTemplate DropDownTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item,
                                                    DependencyObject container)
        {
            ComboBoxItem comboBoxItem = container.FindAncestorOfType<ComboBoxItem>();
            if (comboBoxItem == null)
            {
                return SelectedTemplate;
            }
            return DropDownTemplate;
        }
    }
}
