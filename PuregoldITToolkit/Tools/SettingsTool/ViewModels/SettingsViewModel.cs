using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.SettingsTool.Interfaces;
using PuregoldITToolkit.Tools.SettingsTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.Services;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.SettingsTool.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;

        // The backing field and property with both GET and SET to fix CS0200 and IDE0044
        private SettingsModel _settingsData;
        public SettingsModel SettingsData
        {
            get => _settingsData;
            set => SetProperty(ref _settingsData, value);
        }

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public ICommand SaveSettingsCommand { get; }

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            // Initialize empty model so the UI doesn't crash before loading
            _settingsData = new SettingsModel();

            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);

            // Load the settings asynchronously on startup
            _ = LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            // Call the correct generic LoadSettingsAsync method to fix CS1061
            SettingsData = await _settingsService.LoadSettingsAsync();
        }

        private async Task SaveSettingsAsync()
        {
            bool success = await _settingsService.SaveSettingsAsync(SettingsData);
            StatusMessage = success ? "Global configurations successfully saved!" : "Failed to save settings.";
        }

        // Helper method for other tools (like EOD Generator) to quickly pull settings
        public static SettingsModel GetCurrentSettings()
        {
            if (File.Exists(SettingsService.ConfigFilePath))
            {
                try
                {
                    return JsonSerializer.Deserialize<SettingsModel>(File.ReadAllText(SettingsService.ConfigFilePath));
                }
                catch { }
            }
            return new SettingsModel();
        }
    }
}