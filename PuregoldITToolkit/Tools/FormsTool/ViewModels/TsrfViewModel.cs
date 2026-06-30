using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FormsTool.Interfaces;
using PuregoldITToolkit.Tools.FormsTool.Models;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace PuregoldITToolkit.Tools.FormsTool.ViewModels
{
    public class TsrfViewModel : ViewModelBase
    {
        private readonly IFormsExportService _exportService;

        public TsrfModel FormData { get; } = new TsrfModel();

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private ImageSource _livePreviewImage;
        public ImageSource LivePreviewImage { get => _livePreviewImage; set => SetProperty(ref _livePreviewImage, value); }

        public ICommand ExportCommand { get; }

        private bool _isUpdatingPreview;

        public TsrfViewModel(IFormsExportService exportService)
        {
            _exportService = exportService;
            ExportCommand = new AsyncRelayCommand(ExportDataAsync);

            // Hook for real-time live preview
            FormData.PropertyChanged += OnFormDataChanged;

            // Trigger initial render
            _ = UpdatePreviewAsync();
        }

        private async void OnFormDataChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isUpdatingPreview) return;
            _isUpdatingPreview = true;

            await Task.Delay(250); // Debounce typing speed
            await UpdatePreviewAsync();

            _isUpdatingPreview = false;
        }

        private async Task UpdatePreviewAsync()
        {
            LivePreviewImage = await _exportService.GenerateTsrfPreviewAsync(FormData, "TSRF.png");
        }

        private async Task ExportDataAsync()
        {
            if (string.IsNullOrWhiteSpace(FormData.Company) ||
                string.IsNullOrWhiteSpace(FormData.Others) ||
                string.IsNullOrWhiteSpace(FormData.AssetDescription) ||
                string.IsNullOrWhiteSpace(FormData.ProblemDetails))
            {
                StatusMessage = "Error: Company, Others, Asset Description, and Problem Details are required.";
                return;
            }

            StatusMessage = "Saving Final TSRF image...";
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string folderPath = Path.Combine(desktopPath, "Puregold_Forms_Output");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string cleanName = FormData.TsrfNumber ?? "UNTITLED";
            string fileName = $"TSRF_Form_{cleanName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(folderPath, fileName);

            bool success = await _exportService.ExportTsrfToImageAsync(FormData, "TSRF.png", fullPath);

            if (success) StatusMessage = "Success! Saved to Desktop\\Puregold_Forms_Output";
            else StatusMessage = "Error generating final image.";
        }
    }
}