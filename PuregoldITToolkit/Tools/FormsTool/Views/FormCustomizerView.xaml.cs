using PuregoldITToolkit.Tools.FormsTool.Models;
using PuregoldITToolkit.Tools.FormsTool.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.FormsTool.Views
{
    public partial class FormCustomizerView : UserControl
    {
        private bool _isDraggingElement;
        private bool _isDraggingSignature;
        private Point _dragStartPoint;
        private FormElementModel _draggedElement;

        public FormCustomizerView()
        {
            InitializeComponent();
        }

        private FormCustomizerViewModel ViewModel => DataContext as FormCustomizerViewModel;

        // --- Text Element Dragging ---
        private void OnElementMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is FormElementModel element)
            {
                _draggedElement = element;
                if (ViewModel != null)
                {
                    ViewModel.SelectedElement = element;
                }

                _isDraggingElement = true;
                _dragStartPoint = e.GetPosition(DesignerCanvas);
                fe.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnElementMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingElement && _draggedElement != null && sender is FrameworkElement fe)
            {
                Point currentPoint = e.GetPosition(DesignerCanvas);
                double deltaX = currentPoint.X - _dragStartPoint.X;
                double deltaY = currentPoint.Y - _dragStartPoint.Y;

                double newX = _draggedElement.X + deltaX;
                double newY = _draggedElement.Y + deltaY;

                ViewModel?.UpdateElementPosition(_draggedElement, newX, newY);
                _dragStartPoint = currentPoint;
            }
        }

        private void OnElementMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingElement && sender is FrameworkElement fe)
            {
                _isDraggingElement = false;
                _draggedElement = null;
                fe.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        // --- Dedicated Signature Dragging ---
        private void OnSignatureMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && ViewModel != null)
            {
                ViewModel.IsSignatureSelected = true;
                _isDraggingSignature = true;
                _dragStartPoint = e.GetPosition(DesignerCanvas);
                fe.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnSignatureMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSignature && ViewModel != null)
            {
                Point currentPoint = e.GetPosition(DesignerCanvas);
                double deltaX = currentPoint.X - _dragStartPoint.X;
                double deltaY = currentPoint.Y - _dragStartPoint.Y;

                double newX = ViewModel.Signature.X + deltaX;
                double newY = ViewModel.Signature.Y + deltaY;

                ViewModel.UpdateSignaturePosition(newX, newY);
                _dragStartPoint = currentPoint;
            }
        }

        private void OnSignatureMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSignature && sender is FrameworkElement fe)
            {
                _isDraggingSignature = false;
                fe.ReleaseMouseCapture();
                e.Handled = true;
            }
        }
    }
}

