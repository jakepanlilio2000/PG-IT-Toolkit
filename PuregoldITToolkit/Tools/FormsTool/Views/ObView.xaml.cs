using PuregoldITToolkit.Tools.FormsTool.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.FormsTool.Views
{
    public partial class ObView : UserControl
    {
        private Point _dragOffset;
        public ObView() { InitializeComponent(); }

        private void PreviewSurface_KeyDown(object sender, KeyEventArgs e)
        {
            var model = (DataContext as ObViewModel)?.FormData;
            if (model == null || string.IsNullOrEmpty(model.SignatureImagePath)) return;
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
            switch (e.Key)
            {
                case Key.Left: model.ImageX -= step; break;
                case Key.Right: model.ImageX += step; break;
                case Key.Up: model.ImageY -= step; break;
                case Key.Down: model.ImageY += step; break;
                default: return;
            }
            e.Handled = true;
        }

        private Point? GetPagePoint(MouseEventArgs e)
        {
            var model = (DataContext as ObViewModel)?.FormData;
            if (model == null || PreviewImage.ActualWidth <= 0 || PreviewImage.ActualHeight <= 0) return null;
            double scale = Math.Min(PreviewImage.ActualWidth / model.PageWidth, PreviewImage.ActualHeight / model.PageHeight);
            var point = e.GetPosition(PreviewImage);
            return new Point((point.X - (PreviewImage.ActualWidth - model.PageWidth * scale) / 2) / scale,
                (point.Y - (PreviewImage.ActualHeight - model.PageHeight * scale) / 2) / scale);
        }

        private void PreviewSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PreviewSurface.Focus();
            var model = (DataContext as ObViewModel)?.FormData;
            var point = GetPagePoint(e);
            if (model == null || !point.HasValue || string.IsNullOrEmpty(model.SignatureImagePath)) return;
            if (point.Value.X < 0 || point.Value.Y < 0 || point.Value.X > model.PageWidth || point.Value.Y > model.PageHeight) return;
            var imageBounds = new Rect(model.ImageX, model.ImageY, model.ImageWidth, model.ImageHeight);
            _dragOffset = imageBounds.Contains(point.Value)
                ? new Point(point.Value.X - model.ImageX, point.Value.Y - model.ImageY)
                : new Point(model.ImageWidth / 2, model.ImageHeight / 2);
            model.ImageX = point.Value.X - _dragOffset.X;
            model.ImageY = point.Value.Y - _dragOffset.Y;
            PreviewSurface.CaptureMouse();
            e.Handled = true;
        }

        private void PreviewSurface_MouseMove(object sender, MouseEventArgs e)
        {
            if (!PreviewSurface.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
            var model = (DataContext as ObViewModel)?.FormData;
            var point = GetPagePoint(e);
            if (model == null || !point.HasValue) return;
            model.ImageX = point.Value.X - _dragOffset.X;
            model.ImageY = point.Value.Y - _dragOffset.Y;
        }

        private void PreviewSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { PreviewSurface.ReleaseMouseCapture(); }
    }
}
