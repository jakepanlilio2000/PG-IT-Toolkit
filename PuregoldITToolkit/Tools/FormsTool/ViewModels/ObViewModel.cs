using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FormsTool.Interfaces;
using PuregoldITToolkit.Tools.FormsTool.Models;
using System;
using System.IO;
using System.Linq;
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

        private int _previewRevision;
        private bool _pageConfigured;
        public ICommand ExportCommand { get; }
        public ICommand BrowseSignatureCommand { get; }
        public ICommand RemoveImageCommand { get; }
        public ICommand ResetImageCommand { get; }

        public ObViewModel(IFormsExportService exportService)
        {
            _exportService = exportService;
            ExportCommand = new AsyncRelayCommand(ExportDataAsync);
            BrowseSignatureCommand = new RelayCommand(BrowseSignature);
            RemoveImageCommand = new RelayCommand(() => FormData.SignatureImagePath = null);
            ResetImageCommand = new RelayCommand(() => { FormData.ImageWidth = 200; FormData.ImageX = 80; FormData.ImageY = 440; });
            FormData.PropertyChanged += OnFormDataChanged;
            _ = UpdatePreviewAsync();

        }

        private async void OnFormDataChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            int revision = ++_previewRevision;
            await Task.Delay(80);
            if (revision == _previewRevision) await UpdatePreviewAsync();
        }

        private async Task UpdatePreviewAsync()
        {
            try
            {
                LivePreviewImage = await _exportService.GenerateObPreviewAsync(FormData, "OBFormTemplate.png");
                var bitmap = LivePreviewImage as System.Windows.Media.Imaging.BitmapSource;
                if (!_pageConfigured && bitmap != null)
                {
                    _pageConfigured = true;
                    FormData.ConfigurePage(bitmap.PixelWidth, bitmap.PixelHeight);
                }
            }
            catch (Exception ex) { StatusMessage = "Preview unavailable: " + ex.Message; }
        }

        private void BrowseSignature()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",
                Title = "Attach image (original resolution)"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    using (var stream = File.OpenRead(dlg.FileName))
                    {
                        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream,
                            System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                        FormData.ConfigureImage(decoder.Frames[0].PixelWidth, decoder.Frames[0].PixelHeight);
                    }
                    FormData.SignatureImagePath = dlg.FileName;
                    StatusMessage = "Image attached. Click the preview and use arrow keys; hold Shift for larger steps.";
                }
                catch (Exception ex) { StatusMessage = "Cannot load image: " + ex.Message; }
            }
        }

        private async Task ExportDataAsync()
        {
            try
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

            string cleanName = string.Concat(FormData.EmployeeName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Replace(" ", "_");
            string fileName = $"OB_Form_{cleanName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(folderPath, fileName);

            bool success = await _exportService.ExportObToImageAsync(FormData, "OBFormTemplate.png", fullPath);

            if (success)
            {
                StatusMessage = "Success! Image generated and opened.";
                OutputImagePath = fullPath;

            }
            else
            {
                StatusMessage = "Error generating image. Please try again.";
            }
            }
            catch (Exception ex) { StatusMessage = "Export failed: " + ex.Message; }
        }
    }
}
