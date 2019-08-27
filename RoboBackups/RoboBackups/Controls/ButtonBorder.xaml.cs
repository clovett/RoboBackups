using RoboBackups.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RoboBackups.Controls
{
    /// <summary>
    /// Interaction logic for ButtonBorder.xaml
    /// </summary>
    public partial class ButtonBorder : Border
    {
        Brush _highlight;
        Brush _pressed;
        Brush _normal;
        Button _button;
        bool _isInside;

        public ButtonBorder()
        {
            InitializeComponent();          
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            GetOwningButton();
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            this.Background = GetHighlightBrush();
            this._isInside = true;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            this.Background = IsPressed ? GetPressedBrush() : _normal;
            this._isInside = false;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            this.Background = GetPressedBrush();
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseUp(e);
            this.Background = this._isInside ? GetHighlightBrush() : _normal;
        }

        Button GetOwningButton()
        {
            if (_button == null)
            {
                _button = this.FindAncestorOfType<Button>();
            }
            return _button;
        }

        bool IsPressed
        {
            get { return _button.IsPressed; }
        }

        Brush GetHighlightBrush()
        {
            if (_normal == null)
            {
                this._normal = this.Background;
            }
            if (_highlight == null)
            {
                SolidColorBrush temp = this._normal as SolidColorBrush;
                if (temp != null)
                {
                    var hls = new HlsColor(temp.Color);
                    hls.Lighten(0.25f);
                    _highlight = new SolidColorBrush(hls.Color);
                }
                else
                {
                    _highlight = new SolidColorBrush(Color.FromArgb(0xff, 0x3E, 0x3E, 0x40));
                }
            }
            return _highlight;
        }

        Brush GetPressedBrush()
        {
            if (_normal == null)
            {
                this._normal = this.Background;
            }
            if (_pressed == null)
            {
                SolidColorBrush temp = this._normal as SolidColorBrush;
                if (temp != null)
                {
                    var hls = new HlsColor(temp.Color);
                    hls.Darken(0.25f);
                    _pressed = new SolidColorBrush(hls.Color);
                }
                else
                {
                    _pressed = new SolidColorBrush(Color.FromArgb(0xff, 0x00, 0x7A, 0xCC));
                }
            }
            return _highlight;
        }

    }
}
