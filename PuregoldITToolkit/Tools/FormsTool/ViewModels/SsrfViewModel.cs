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
    public class SsrfViewModel : ViewModelBase
    {
        private readonly IFormsExportService _exportService;

        public SsrfModel FormData { get; } = new SsrfModel();

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        // --- NEW PREVIEW PROPERTIES ---
        private ImageSource _livePreviewImage;
        public ImageSource LivePreviewImage { get => _livePreviewImage; set => SetProperty(ref _livePreviewImage, value); }
        private bool _isUpdatingPreview;

        public ICommand PrintCommand { get; }

        public SsrfViewModel(IFormsExportService exportService)
        {
            _exportService = exportService;
            PrintCommand = new AsyncRelayCommand(PrintBatchAsync);
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
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SSRF_Template.jpg");
            LivePreviewImage = await _exportService.GenerateSsrfPreviewAsync(FormData, templatePath);
        }

        private async Task PrintBatchAsync()
        {
            if (string.IsNullOrWhiteSpace(FormData.BaseSsrfNumber))
            {
                StatusMessage = "Error: Starting SSRF Number is required.";
                return;
            }

            if (FormData.PrintQuantity < 1 || FormData.PrintQuantity > 100)
            {
                StatusMessage = "Error: Quantity must be between 1 and 100.";
                return;
            }

            StatusMessage = "Preparing Batch Print Job...";
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SSRF_Template.jpg");

            if (!File.Exists(templatePath))
            {
                StatusMessage = "Error: SSRF_Template.jpg not found in application folder.";
                return;
            }

            bool success = await _exportService.PrintSsrfBatchAsync(FormData, templatePath);

            if (success)
                StatusMessage = $"Success! Sent {FormData.PrintQuantity} copies to printer.";
            else
                StatusMessage = "Print job cancelled or failed.";
        }
    }
}