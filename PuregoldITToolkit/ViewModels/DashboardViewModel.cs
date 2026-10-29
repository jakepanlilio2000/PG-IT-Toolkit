using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly Action<string> _navigateAction;

        private string _storeCode = "722";
        public string StoreCode { get => _storeCode; set => SetProperty(ref _storeCode, value); }

        private string _storeName = "San Fernando";
        public string StoreName { get => _storeName; set => SetProperty(ref _storeName, value); }

        private string _ftpHost = "192.168.200.177";
        public string FtpHost { get => _ftpHost; set => SetProperty(ref _ftpHost, value); }

        private string _ftpStatusText = "Checking...";
        public string FtpStatusText { get => _ftpStatusText; set => SetProperty(ref _ftpStatusText, value); }

        private string _ftpStatusColor = "#7F8FA6";
        public string FtpStatusColor { get => _ftpStatusColor; set => SetProperty(ref _ftpStatusColor, value); }

        private string _localPathsStatusText = "Checking...";
        public string LocalPathsStatusText { get => _localPathsStatusText; set => SetProperty(ref _localPathsStatusText, value); }

        private string _localPathsStatusColor = "#7F8FA6";
        public string LocalPathsStatusColor { get => _localPathsStatusColor; set => SetProperty(ref _localPathsStatusColor, value); }

        private string _currentTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
        public string CurrentTime { get => _currentTime; set => SetProperty(ref _currentTime, value); }

        public ICommand NavigateCommand { get; }
        public ICommand RefreshStatusCommand { get; }
        public ICommand OpenOutputDirCommand { get; }
        public ICommand OpenLogFolderCommand { get; }

        public DashboardViewModel(Action<string> navigateAction)
        {
            _navigateAction = navigateAction;

            NavigateCommand = new RelayCommand<string>(dest => _navigateAction?.Invoke(dest));
            RefreshStatusCommand = new AsyncRelayCommand(RefreshStatusAsync);
            OpenOutputDirCommand = new RelayCommand(OpenOutputDir);
            OpenLogFolderCommand = new RelayCommand(() => LoggingService.Instance.OpenLogFolder());

            LoadSettingsData();
            _ = RefreshStatusAsync();
        }

        public void LoadSettingsData()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            StoreCode = settings.StoreCode;
            StoreName = settings.StoreName;
            FtpHost = settings.FtpHost;
            CurrentTime = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
        }

        public async Task RefreshStatusAsync()
        {
            LoadSettingsData();
            FtpStatusText = "Checking FTP...";
            FtpStatusColor = "#2563EB";
            LocalPathsStatusText = "Checking folders...";
            LocalPathsStatusColor = "#2563EB";

            var settings = SettingsViewModel.GetCurrentSettings();

            // 1. Verify Directories
            await Task.Run(() =>
            {
                try
                {
                    bool inputOk = Directory.Exists(settings.EjInputDirectory);
                    bool outputOk = Directory.Exists(settings.EjOutputDirectory);

                    if (!outputOk && !string.IsNullOrWhiteSpace(settings.EjOutputDirectory))
                    {
                        try { Directory.CreateDirectory(settings.EjOutputDirectory); outputOk = true; } catch { }
                    }

                    if (inputOk && outputOk)
                    {
                        LocalPathsStatusText = "Store folders are available";
                        LocalPathsStatusColor = "#27AE60";
                    }
                    else
                    {
                        LocalPathsStatusText = "Check your folders in Settings";
                        LocalPathsStatusColor = "#E67E22";
                    }
                }
                catch
                {
                    LocalPathsStatusText = "Path Check Failed";
                    LocalPathsStatusColor = "#C41E3A";
                }
            });

            // 2. Ping / Socket test to FTP
            await Task.Run(() =>
            {
                try
                {
                    using (var client = new System.Net.Sockets.TcpClient())
                    {
                        var task = client.ConnectAsync(settings.FtpHost, settings.FtpPort > 0 ? settings.FtpPort : 21);
                        if (task.Wait(2000))
                        {
                            FtpStatusText = $"FTP Online ({settings.FtpHost}:{settings.FtpPort})";
                            FtpStatusColor = "#27AE60";
                        }
                        else
                        {
                            FtpStatusText = $"FTP Offline ({settings.FtpHost})";
                            FtpStatusColor = "#E67E22";
                        }
                    }
                }
                catch
                {
                    FtpStatusText = "FTP Unreachable";
                    FtpStatusColor = "#C41E3A";
                }
            });
        }

        private void OpenOutputDir()
        {
            try
            {
                var settings = SettingsViewModel.GetCurrentSettings();
                string path = string.IsNullOrWhiteSpace(settings.EjOutputDirectory)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SOD_Output")
                    : settings.EjOutputDirectory;

                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{path}\"", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Cannot open output folder", ex);
            }
        }
    }
}

