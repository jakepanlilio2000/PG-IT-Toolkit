using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.SettingsTool.Interfaces;
using PuregoldITToolkit.Tools.SettingsTool.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.SettingsTool.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;

        public static readonly string SignatureFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmailSignature.txt");

        public SettingsModel SettingsData { get; } = new SettingsModel();

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public ICommand SaveSignatureCommand { get; }
        public ICommand ClearSignatureCommand { get; }

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            SaveSignatureCommand = new AsyncRelayCommand(SaveSignatureAsync);
            ClearSignatureCommand = new AsyncRelayCommand(ClearSignatureAsync);

            // Load the signature when the viewmodel initializes
            _ = LoadSignatureAsync();
        }

        private async Task LoadSignatureAsync()
        {
            SettingsData.SignatureHtml = await _settingsService.LoadSignatureAsync();
        }

        private async Task SaveSignatureAsync()
        {
            bool success = await _settingsService.SaveSignatureAsync(SettingsData.SignatureHtml);
            StatusMessage = success
                ? "Signature successfully saved! It will now automatically apply to all outgoing tool emails."
                : "Failed to save signature.";
        }

        private async Task ClearSignatureAsync()
        {
            SettingsData.SignatureHtml = string.Empty;
            bool success = await _settingsService.ClearSignatureAsync();
            StatusMessage = success ? "Signature cleared." : "Failed to clear signature.";
        }
    }
}