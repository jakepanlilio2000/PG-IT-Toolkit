using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.SettingsTool.Interfaces;
using PuregoldITToolkit.Tools.SettingsTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.SettingsTool.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private SettingsModel _settingsData;
        public SettingsModel SettingsData
        {
            get => _settingsData;
            set => SetProperty(ref _settingsData, value);
        }

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private string _statusColor = "#2C3E50";
        public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (!SetProperty(ref _isBusy, value)) return;
                (SaveSettingsCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (TestFtpCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (ValidatePathsCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        private string _diagnosticOutput = "Click 'Test FTP Connection' or 'Validate Paths' to run hardware diagnostics.";
        public string DiagnosticOutput { get => _diagnosticOutput; set => SetProperty(ref _diagnosticOutput, value); }

        public ICommand SaveSettingsCommand { get; }
        public ICommand TestFtpCommand { get; }
        public ICommand ValidatePathsCommand { get; }
        public ICommand BrowseBackOfficeDirCommand { get; }
        public ICommand BrowseEjInputDirCommand { get; }
        public ICommand BrowseEjOutputDirCommand { get; }

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _settingsData = new SettingsModel();

            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
            TestFtpCommand = new AsyncRelayCommand(TestFtpAsync, () => !IsBusy);
            ValidatePathsCommand = new AsyncRelayCommand(ValidatePathsAsync, () => !IsBusy);

            BrowseBackOfficeDirCommand = new RelayCommand(() => BrowseFolder(p => SettingsData.BackOfficeRootDir = p));
            BrowseEjInputDirCommand = new RelayCommand(() => BrowseFolder(p => SettingsData.EjInputDirectory = p));
            BrowseEjOutputDirCommand = new RelayCommand(() => BrowseFolder(p => SettingsData.EjOutputDirectory = p));

            _ = LoadSettingsAsync();
        }

        private void BrowseFolder(Action<string> setter)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select Folder";
                dlg.ShowNewFolderButton = true;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    setter(dlg.SelectedPath);
                }
            }
        }

        private async Task LoadSettingsAsync()
        {
            SettingsData = await _settingsService.LoadSettingsAsync();
        }

        private async Task SaveSettingsAsync()
        {
            // 1. Run Input Validation
            if (!SettingsData.Validate(out List<string> errors))
            {
                string errorList = string.Join("\n• ", errors);
                StatusMessage = "Configuration validation failed.";
                StatusColor = "#C41E3A";
                DiagnosticOutput = $"Validation Errors:\n• {errorList}";

                System.Windows.MessageBox.Show(
                    $"Please fix the following validation errors before saving:\n\n• {errorList}",
                    "Validation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Saving configurations...";
            StatusColor = "#2563EB";

            try
            {
                bool success = await _settingsService.SaveSettingsAsync(SettingsData);

                if (success)
                {
                    StatusMessage = "Settings saved.";
                    StatusColor = "#27AE60";
                    DiagnosticOutput = $"[{DateTime.Now:HH:mm:ss}] Settings saved.";
                    System.Windows.MessageBox.Show("Settings saved.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "Failed to save settings.";
                    StatusColor = "#C41E3A";
                    System.Windows.MessageBox.Show("Failed to save settings. Please inspect system logs.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task TestFtpAsync()
        {
            IsBusy = true;
            StatusMessage = "Testing FTP Connection...";
            StatusColor = "#2563EB";
            DiagnosticOutput = $"[{DateTime.Now:HH:mm:ss}] Initiating socket and FTP handshake to {SettingsData.FtpHost}:{SettingsData.FtpPort}...";

            try
            {
                var result = await _settingsService.TestFtpConnectionAsync(
                    SettingsData.FtpHost,
                    SettingsData.FtpPort,
                    SettingsData.FtpUsername,
                    SettingsData.FtpPassword,
                    SettingsData.FtpTimeoutSeconds,
                    SettingsData.FtpPassiveMode);

                if (result.Success)
                {
                    StatusMessage = "FTP Connection Successful!";
                    StatusColor = "#27AE60";
                    DiagnosticOutput = $"[{DateTime.Now:HH:mm:ss}] ✅ SUCCESS: {result.Message}";
                    LoggingService.Instance.LogSuccess($"FTP Test passed for {SettingsData.FtpHost}:{SettingsData.FtpPort}");
                }
                else
                {
                    StatusMessage = "FTP Connection Failed!";
                    StatusColor = "#C41E3A";
                    DiagnosticOutput = $"[{DateTime.Now:HH:mm:ss}] ❌ FAILED: {result.Message}";
                    LoggingService.Instance.LogWarning($"FTP Test failed for {SettingsData.FtpHost}:{SettingsData.FtpPort}: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "FTP Test Encountered an Error.";
                StatusColor = "#C41E3A";
                DiagnosticOutput = $"[{DateTime.Now:HH:mm:ss}] ❌ ERROR: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ValidatePathsAsync()
        {
            IsBusy = true;
            StatusMessage = "Validating directory paths...";
            StatusColor = "#2563EB";

            try
            {
                var result = await _settingsService.ValidateDirectoriesAsync(SettingsData);
                string report = string.Join("\n", result.Details);
                DiagnosticOutput = $"Directory Path Verification Report ({DateTime.Now:HH:mm:ss}):\n{report}";

                if (result.AllValid)
                {
                    StatusMessage = "All directory paths are verified and writable!";
                    StatusColor = "#27AE60";
                    LoggingService.Instance.LogSuccess("All local store directories validated successfully.");
                }
                else
                {
                    StatusMessage = "One or more directory paths failed validation.";
                    StatusColor = "#C41E3A";
                    LoggingService.Instance.LogWarning("Some directory paths failed validation.");
                }
            }
            catch (Exception ex)
            {
                DiagnosticOutput = $"Directory verification error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public static SettingsModel GetCurrentSettings()
        {
            return SettingsService.LoadSettingsInternal();
        }
    }
}
