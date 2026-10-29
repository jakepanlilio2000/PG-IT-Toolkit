using PuregoldITToolkit.Core.Base;
using System;

namespace PuregoldITToolkit.Tools.FormsTool.Models
{
    public class ObModel : ViewModelBase
    {
        private string _employeeName;
        private string _position;
        private string _department;
        private DateTime? _dateFiled = DateTime.Now;
        private string _obSchedule;
        private string _employeeSignature;
        private string _signatureImagePath;
        private double _imageX = 80;
        private double _imageY = 440;
        private double _imageWidth = 200;
        private double _imageAspectRatio = 3.333333;
        private double _pageWidth = 800;
        private double _pageHeight = 600;

        // Coordinates use template pixels, independently of the preview's display size.
        public double PageWidth => _pageWidth;
        public double PageHeight => _pageHeight;
        public double ImageX { get => _imageX; set => SetProperty(ref _imageX, Clamp(value, 0, Math.Max(0, PageWidth - ImageWidth))); }
        public double ImageY { get => _imageY; set => SetProperty(ref _imageY, Clamp(value, 0, Math.Max(0, PageHeight - ImageHeight))); }
        public double ImageWidth
        {
            get => _imageWidth;
            set
            {
                if (SetProperty(ref _imageWidth, Clamp(value, 10, Math.Min(PageWidth, PageHeight * _imageAspectRatio))))
                {
                    OnPropertyChanged(nameof(ImageHeight));
                    ImageX = ImageX;
                    ImageY = ImageY;
                }
            }
        }
        public double ImageHeight => ImageWidth / _imageAspectRatio;

        public void ConfigurePage(double width, double height)
        {
            _pageWidth = width;
            _pageHeight = height;
            OnPropertyChanged(nameof(PageWidth));
            OnPropertyChanged(nameof(PageHeight));
            ImageWidth = ImageWidth;
            ImageX = ImageX;
            ImageY = ImageY;
        }

        public void ConfigureImage(int width, int height)
        {
            _imageAspectRatio = width / (double)Math.Max(1, height);
            ImageWidth = Math.Min(200, width);
            OnPropertyChanged(nameof(ImageHeight));
            ImageX = ImageX;
            ImageY = ImageY;
        }

        private static double Clamp(double value, double min, double max) =>
            double.IsNaN(value) || double.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));

        // Auto-Capitalize as the user types
        public string EmployeeName { get => _employeeName; set => SetProperty(ref _employeeName, value?.ToUpper()); }
        public string Position { get => _position; set => SetProperty(ref _position, value?.ToUpper()); }

        public string Department { get => _department; set => SetProperty(ref _department, value); }
        public DateTime? DateFiled { get => _dateFiled; set => SetProperty(ref _dateFiled, value); }
        public string ObSchedule { get => _obSchedule; set => SetProperty(ref _obSchedule, value); }
        public string EmployeeSignature { get => _employeeSignature; set => SetProperty(ref _employeeSignature, value); }
        public string SignatureImagePath { get => _signatureImagePath; set => SetProperty(ref _signatureImagePath, value); }
    }
}
