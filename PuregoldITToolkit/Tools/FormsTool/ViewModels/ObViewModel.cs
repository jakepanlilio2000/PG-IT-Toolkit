using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FormsTool.Interfaces;
using PuregoldITToolkit.Tools.FormsTool.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace PuregoldITToolkit.Tools.FormsTool.ViewModels
{
    public class ObViewModel : ViewModelBase
    {
        private readonly IFormsExportService _exportService;

        public ObModel FormData { get; } = new ObModel();

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        private string _outputImagePath;
        public string OutputImagePath { get => _outputImagePath; set => SetProperty(ref _outputImagePath, value); }

        private ImageSource _livePreviewImage;
        public ImageSource LivePreviewImage { get => _livePreviewImage; set => SetProperty(ref _livePreviewImage, value); }

        private bool _isUpdatingPreview;
        public ICommand ExportCommand { get; }
        public ICommand BrowseSignatureCommand { get; }

        public ObViewModel(IFormsExportService exportService)
        {
            _exportService = exportService;
            ExportCommand = new AsyncRelayCommand(ExportDataAsync);
            BrowseSignatureCommand = new RelayCommand(BrowseSignature);
            FormData.PropertyChanged += OnFormDataChanged;
            _ = UpdatePreviewAsync();

        }

        private async void OnFormDataChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isUpdatingPreview) return;
            _isUpdatingPreview = true;

            await Task.Delay(250);
            await UpdatePreviewAsync();

            _isUpdatingPreview = false;
        }

        private async Task UpdatePreviewAsync()
        {
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OBFormTemplate.png");
            LivePreviewImage = await _exportService.GenerateObPreviewAsync(FormData, templatePath);
        }

        private void BrowseSignature()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PNG Images (*.png)|*.png",
                Title = "Select Transparent Signature (PNG)"
            };

            if (dlg.ShowDialog() == true)
            {
                FormData.SignatureImagePath = dlg.FileName;
            }
        }

        private async Task ExportDataAsync()
        {
            // Date is fully optional, only Name, Position, and Department are strictly checked.
            if (string.IsNullOrWhiteSpace(FormData.EmployeeName) ||
                string.IsNullOrWhiteSpace(FormData.Position) ||
                string.IsNullOrWhiteSpace(FormData.Department))
            {
                StatusMessage = "Error: Name, Position, and Department are required.";
                return;
            }

            StatusMessage = "Stamping image template...";
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string folderPath = Path.Combine(desktopPath, "Puregold_Forms_Output");

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OBFormTemplate.png");

            if (!File.Exists(templatePath))
            {
                StatusMessage = "Error: OBFormTemplate.png not found in application folder.";
                return;
            }

            string cleanName = FormData.EmployeeName.Replace(" ", "_");
            string fileName = $"OB_Form_{cleanName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(folderPath, fileName);

            bool success = await _exportService.ExportObToImageAsync(FormData, templatePath, fullPath);

            if (success)
            {
                StatusMessage = "Success! Image generated and opened.";
                OutputImagePath = fullPath;
                FormData.EmployeeSignature = string.Empty;
            }
            else
            {
                StatusMessage = "Error generating image. Please try again.";
            }
        }
    }
}