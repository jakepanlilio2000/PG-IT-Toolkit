using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.PureposTool.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
        private string _cliTargetIp = "192.92.92.51";
        private string _cliUsername;
        private string _cliPassword;
        private string _cliCommand = "ls -la";

        public string CliTargetIp { get => _cliTargetIp; set => SetProperty(ref _cliTargetIp, value); }
        public string CliUsername { get => _cliUsername; set => SetProperty(ref _cliUsername, value); }
        public string CliPassword { get => _cliPassword; set => SetProperty(ref _cliPassword, value); }
        public string CliCommand { get => _cliCommand; set => SetProperty(ref _cliCommand, value); }

        // Scripts Dropdown
        public ObservableCollection<string> AvailableScripts { get; } = new ObservableCollection<string>
        {
            "mysql_backup.sh",
            "crm_ftp_transfer.sh",
            "permission.sh",
            "transfer_previous_day_ftp_lftp.sh",
            "ftp_transfer_purepos.sh",
            "ftp_transfer_nontrade_purepos.sh",
            "eis_ftp_transfer_purepos.sh",
            "pull_ejreceipt_from_pos_to_conso.sh",
            "eis_directory.sh",
            "crm_directory.sh",
            "ftp_upload.sh",
            "pricelist_transfer.sh",
            "generate_regular_price.sh",
            "default_ftp_transfer_nontrade_purepos.sh",
            "directory_creation.sh"
        };

        private string _selectedScript;
        public string SelectedScript
        {
            get => _selectedScript;
            set
            {
                if (SetProperty(ref _selectedScript, value) && !string.IsNullOrEmpty(value))
                {
                    CliCommand = $"sh /opt/purepos/scripts/{value}";
                }
            }
        }

        private string _cliOutput = "Ready to execute...\n";
        public string CliOutput { get => _cliOutput; set => SetProperty(ref _cliOutput, value); }

        // EOD Preview Properties
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

            var settings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();
            CliUsername = settings.PosUsername;
            CliPassword = settings.PosPassword;

            EodData.PropertyChanged += (s, e) => UpdateEodPreview();
            UpdateEodPreview();
        }

        private void UpdateEodPreview()
        {
            var settings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();

            EodPreviewTo = EodData.TestModeEmail ? settings.SmtpUser : "AllITDataControllersMMS@puregold.com.ph";
            EodPreviewCc = EodData.TestModeEmail ? "" : "jymendoza@puregold.com.ph, allITzone11@puregold.com.ph";

            DateTime manilaTime = DateTime.UtcNow.AddHours(8);
            DateTime targetDate = EodData.UseYesterday ? manilaTime.AddDays(-1) : manilaTime;

            EodPreviewSubject = $"{EodData.StoreCode} - {EodData.StoreName} - EOD File Purepos POLLOG {targetDate:MM-dd-yy}";
            EodPreviewAttachment = $"📎 Attached: {EodData.StoreCode}_{targetDate:yyyyMMdd}.zip";
            EodPreviewBody = $"Masayang Araw!\n\nPlease see attached POLLOG file for {EodData.StoreCode} - {EodData.StoreName}\n\nStore Manager: {EodData.StoreManager}\nStore Offices: {EodData.StoreOfficer}\n\n[HTML Signature attached internally]";

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

        // --- NEW: INTERACTIVE COMMAND HANDLER (NANO/VI/TOP) ---
        private bool LaunchInteractiveTerminal(string ip, string user, string pass, string command)
        {
            string cmdLower = command.Trim().ToLower();
            if (cmdLower.StartsWith("nano") || cmdLower.StartsWith("vi") || cmdLower.StartsWith("vim") || cmdLower.StartsWith("top") || cmdLower.StartsWith("htop"))
            {
                try
                {
                    // Create an ephemeral script to execute the interactive command 
                    string tempScript = Path.Combine(Path.GetTempPath(), $"purepos_cmd_{Guid.NewGuid().ToString("N").Substring(0, 8)}.sh");
                    File.WriteAllText(tempScript, command);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "putty.exe",
                        Arguments = $"-ssh {user}@{ip} -pw {pass} -t -m \"{tempScript}\"",
                        UseShellExecute = true
                    });
                    AppendLog($"\n[INFO] Interactive editor '{command.Split(' ')[0]}' detected! Launched temporary Putty PTY window for {ip} so you can edit the file securely.");
                    return true;
                }
                catch
                {
                    AppendLog("\n[ERROR] Tried to launch Putty for an interactive command, but failed. Make sure putty.exe is accessible in the app folder or system PATH.");
                    return true;
                }
            }
            return false;
        }

        private async Task ExecuteEodAsync()
        {
            IsBusy = true;
            EodData.SaveCache();
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

            // Intercept interactive commands like Nano
            if (!LaunchInteractiveTerminal(CliTargetIp, CliUsername, CliPassword, CliCommand))
            {
                string result = await _service.RunSshCommandAsync(CliTargetIp, CliUsername, CliPassword, CliCommand);
                AppendLog(result);
            }
            IsBusy = false;
        }

        private async Task ExecuteAllPosCliAsync()
        {
            IsBusy = true;
            AppendLog($"\n> Executing on ALL POS LANES: {CliCommand}");

            string ipPrefix = PermissionData.LiveServerIp.Substring(0, PermissionData.LiveServerIp.LastIndexOf('.') + 1);

            // Handle Nano on all lanes simultaneously
            if (CliCommand.Trim().ToLower().StartsWith("nano") || CliCommand.Trim().ToLower().StartsWith("vi"))
            {
                AppendLog("[INFO] Interactive command detected on Batch Mode. Opening isolated Putty windows for all selected targets...");
                for (int i = 1; i <= PermissionData.TotalLanes; i++)
                {
                    string posIp = $"{ipPrefix}{50 + i}";
                    LaunchInteractiveTerminal(posIp, "cashier", "cashier", CliCommand);
                }
                IsBusy = false;
                return;
            }

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

            if (!LaunchInteractiveTerminal(PermissionData.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword, CliCommand))
            {
                string result = await _service.RunSshCommandAsync(PermissionData.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword, CliCommand);
                AppendLog(result);
            }
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