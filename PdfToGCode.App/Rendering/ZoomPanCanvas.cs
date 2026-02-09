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
        private Point _lastMousePosition;
        private bool _isDragging;

        public ZoomPanCanvas()
        {
            var group = new TransformGroup();
            _scaleTransform = new ScaleTransform();
            _translateTransform = new TranslateTransform();
            group.Children.Add(_scaleTransform);
            group.Children.Add(_translateTransform);
            this.RenderTransform = group;

            this.MouseWheel += OnMouseWheel;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;
            this.MouseMove += OnMouseMove;

            // Clip to bounds so we don't draw outside
            this.ClipToBounds = true;
            this.Background = Brushes.Transparent; // Hit test
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(this);
            double zoom = e.Delta > 0 ? 1.1 : 0.9;

            _scaleTransform.ScaleX *= zoom;
            _scaleTransform.ScaleY *= zoom;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(this);
            this.CaptureMouse();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var pos = e.GetPosition(this);
                var delta = pos - _lastMousePosition;
                _translateTransform.X += delta.X;
                _translateTransform.Y += delta.Y;
                _lastMousePosition = pos;
            }
        }

        public void Reset()
        {
             _scaleTransform.ScaleX = 1;
             _scaleTransform.ScaleY = 1;
             _translateTransform.X = 0;
             _translateTransform.Y = 0;
        }
    }
}
