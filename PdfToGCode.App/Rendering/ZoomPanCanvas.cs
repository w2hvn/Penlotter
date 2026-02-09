using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfToGCode.App.Rendering
{
    public class ZoomPanCanvas : Canvas
    {
        private ScaleTransform _scaleTransform;
        private TranslateTransform _translateTransform;
        private TransformGroup _transformGroup;
        private Point _lastMousePosition;
        private bool _isPanning;

        public ZoomPanCanvas()
        {
            _scaleTransform = new ScaleTransform();
            _translateTransform = new TranslateTransform();
            _transformGroup = new TransformGroup();
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);

            this.RenderTransform = _transformGroup;

            this.MouseWheel += OnMouseWheel;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;
            this.MouseMove += OnMouseMove;

            this.Focusable = true;
            this.Background = Brushes.Transparent;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var parent = this.Parent as UIElement;
            if (parent == null) return;

            var pos = e.GetPosition(parent);
            var scale = e.Delta > 0 ? 1.1 : 1.0 / 1.1;

            double currentScale = _scaleTransform.ScaleX;
            double newScale = currentScale * scale;

            if (newScale < 0.1) newScale = 0.1;
            if (newScale > 50) newScale = 50;

            scale = newScale / currentScale;

            _scaleTransform.ScaleX = newScale;
            _scaleTransform.ScaleY = newScale;

            // Translate_new = Mouse - (Mouse - Translate_old) * scale
            _translateTransform.X = pos.X - (pos.X - _translateTransform.X) * scale;
            _translateTransform.Y = pos.Y - (pos.Y - _translateTransform.Y) * scale;

            e.Handled = true;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var parent = this.Parent as UIElement;
            if (parent == null) return;

            _lastMousePosition = e.GetPosition(parent);
            _isPanning = true;
            this.CaptureMouse();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            this.ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var parent = this.Parent as UIElement;
                if (parent == null) return;

                var currentPos = e.GetPosition(parent);
                var diff = currentPos - _lastMousePosition;

                _translateTransform.X += diff.X;
                _translateTransform.Y += diff.Y;

                _lastMousePosition = currentPos;
            }
        }

        public void Reset()
        {
            _scaleTransform.ScaleX = 1.0;
            _scaleTransform.ScaleY = 1.0;
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
        }
    }
}
