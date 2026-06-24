using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.PureposTool.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.PureposTool.ViewModels
{
    public class PureposViewModel : ViewModelBase
    {
        private readonly IPureposService _service;

        public AutoPermissionModel PermissionData { get; } = new AutoPermissionModel();
        public EjSaveModel EjData { get; } = new EjSaveModel();
        public EodModel EodData { get; } = new EodModel();

        // CLI Properties
        private string _cliTargetIp = "***.***.***.51";
        private string _cliUsername = "cashier";
        private string _cliPassword = "cashier";
        private string _cliCommand = "ls -la";

        public string CliTargetIp { get => _cliTargetIp; set => SetProperty(ref _cliTargetIp, value); }
        public string CliUsername { get => _cliUsername; set => SetProperty(ref _cliUsername, value); }
        public string CliPassword { get => _cliPassword; set => SetProperty(ref _cliPassword, value); }
        public string CliCommand { get => _cliCommand; set => SetProperty(ref _cliCommand, value); }

        private string _cliOutput = "Ready to execute...\n";
        public string CliOutput { get => _cliOutput; set => SetProperty(ref _cliOutput, value); }

        public string EodPreviewTo { get; private set; }
        public string EodPreviewCc { get; private set; }
        public string EodPreviewSubject { get; private set; }
        public string EodPreviewBody { get; private set; }
        public string EodPreviewAttachment { get; private set; } 

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand RunPermissionCommand { get; }
        public ICommand RunEjSaveCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand RunEodCommand { get; }

        public ICommand RunSingleCliCommand { get; }
        public ICommand RunAllPosCliCommand { get; }
        public ICommand RunConsoCliCommand { get; }
        public ICommand OpenPuttyCommand { get; }

        public PureposViewModel(IPureposService service)
        {
            _service = service;
            RunPermissionCommand = new AsyncRelayCommand(ExecutePermissionAsync);
            RunEjSaveCommand = new AsyncRelayCommand(ExecuteEjSaveAsync);
            ClearLogCommand = new RelayCommand(() => CliOutput = "Logs cleared.\n");
            RunEodCommand = new AsyncRelayCommand(ExecuteEodAsync);

            RunSingleCliCommand = new AsyncRelayCommand(ExecuteSingleCliAsync);
            RunAllPosCliCommand = new AsyncRelayCommand(ExecuteAllPosCliAsync);
            RunConsoCliCommand = new AsyncRelayCommand(ExecuteConsoCliAsync);
            OpenPuttyCommand = new RelayCommand(ExecutePutty);
            EodData.PropertyChanged += (s, e) => UpdateEodPreview();
            UpdateEodPreview();
        }

        private void UpdateEodPreview()
        {
            var settings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();

            EodPreviewTo = EodData.TestModeEmail ? settings.SmtpUser : "AllITDataControllersMMS@puregold.com.ph";
            EodPreviewCc = EodData.TestModeEmail ? "" : "jymendoza@puregold.com.ph, allITzone11@puregold.com.ph";

            DateTime targetDate = EodData.UseYesterday ? DateTime.Now.AddDays(-1) : DateTime.Now;

            EodPreviewSubject = $"{EodData.StoreCode} - {EodData.StoreName} - EOD File Purepos POLLOG {targetDate:MM-dd-yy}";
            EodPreviewAttachment = $"📎 Attached: {EodData.StoreCode}_{targetDate:yyyyMMdd}.zip";
            EodPreviewBody = $"Masayang Araw!\n\nPlease see attached POLLOG file for {EodData.StoreCode} - {EodData.StoreName}\n\nStore Manager: {EodData.StoreManager}\nStore Officers: {EodData.StoreOfficer}\n\n[HTML Signature attached internally]";

            OnPropertyChanged(nameof(EodPreviewTo));
            OnPropertyChanged(nameof(EodPreviewCc));
            OnPropertyChanged(nameof(EodPreviewSubject));
            OnPropertyChanged(nameof(EodPreviewAttachment));
            OnPropertyChanged(nameof(EodPreviewBody));
        }

        private void AppendLog(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => { CliOutput += $"{message}\n"; });
        }

        private async Task ExecuteEodAsync()
        {
            IsBusy = true;
            await _service.RunEodGeneratorAsync(EodData, AppendLog);
            IsBusy = false;
        }

        private async Task ExecutePermissionAsync()
        {
            IsBusy = true;
            AppendLog("\n--- STARTING AUTO PERMISSION ---");
            await _service.RunAutoPermissionAsync(PermissionData, AppendLog);
            IsBusy = false;
        }

        private async Task ExecuteEjSaveAsync()
        {
            IsBusy = true;
            AppendLog($"\n--- STARTING MANUAL EJ SAVE ---");
            AppendLog($"Date Range: {EjData.DateFrom:MM/dd/yyyy} to {EjData.DateTo:MM/dd/yyyy}");
            await _service.RunManualEjSaveAsync(EjData, AppendLog);
            IsBusy = false;
        }

        private async Task ExecuteSingleCliAsync()
        {
            IsBusy = true;
            AppendLog($"\n> Executing on {CliTargetIp}: {CliCommand}");
            string result = await _service.RunSshCommandAsync(CliTargetIp, CliUsername, CliPassword, CliCommand);
            AppendLog(result);
            IsBusy = false;
        }

        private async Task ExecuteAllPosCliAsync()
        {
            IsBusy = true;
            AppendLog($"\n> Executing on ALL POS LANES: {CliCommand}");
            string ipPrefix = PermissionData.LiveServerIp.Substring(0, PermissionData.LiveServerIp.LastIndexOf('.') + 1);

            var tasks = new List<Task<string>>();
            for (int i = 1; i <= PermissionData.TotalLanes; i++)
            {
                string posIp = $"{ipPrefix}{50 + i}";
                tasks.Add(_service.RunSshCommandAsync(posIp, "cashier", "cashier", CliCommand));
            }

            var results = await Task.WhenAll(tasks);
            foreach (var res in results) AppendLog(res);
            IsBusy = false;
        }

        private async Task ExecuteConsoCliAsync()
        {
            IsBusy = true;
            AppendLog($"\n> Executing on CONSO ({PermissionData.LiveServerIp}): {CliCommand}");

            var globalSettings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();

            string result = await _service.RunSshCommandAsync(PermissionData.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword, CliCommand);
            AppendLog(result);
            IsBusy = false;
        }

        private void ExecutePutty()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "putty.exe",
                    Arguments = $"-ssh {CliUsername}@{CliTargetIp} -pw {CliPassword}",
                    UseShellExecute = true
                });
                AppendLog($"\n[INFO] Launched external Putty session to {CliTargetIp}.");
            }
            catch
            {
                AppendLog("\n[ERROR] Could not launch Putty. Make sure putty.exe is installed and added to your system PATH, or placed in the application folder.");
            }
        }
    }
}