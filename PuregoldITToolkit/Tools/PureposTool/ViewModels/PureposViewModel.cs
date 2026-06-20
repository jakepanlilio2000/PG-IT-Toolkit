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

        // CLI Properties
        private string _cliTargetIp = "192.92.92.51";
        private string _cliUsername = "cashier";
        private string _cliPassword = "cashier";
        private string _cliCommand = "ls -la";

        public string CliTargetIp { get => _cliTargetIp; set => SetProperty(ref _cliTargetIp, value); }
        public string CliUsername { get => _cliUsername; set => SetProperty(ref _cliUsername, value); }
        public string CliPassword { get => _cliPassword; set => SetProperty(ref _cliPassword, value); }
        public string CliCommand { get => _cliCommand; set => SetProperty(ref _cliCommand, value); }

        private string _cliOutput = "Ready to execute...\n";
        public string CliOutput { get => _cliOutput; set => SetProperty(ref _cliOutput, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand RunPermissionCommand { get; }
        public ICommand RunEjSaveCommand { get; }
        public ICommand ClearLogCommand { get; }

        // New CLI Commands
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

            RunSingleCliCommand = new AsyncRelayCommand(ExecuteSingleCliAsync);
            RunAllPosCliCommand = new AsyncRelayCommand(ExecuteAllPosCliAsync);
            RunConsoCliCommand = new AsyncRelayCommand(ExecuteConsoCliAsync);
            OpenPuttyCommand = new RelayCommand(ExecutePutty);
        }

        private void AppendLog(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CliOutput += $"{message}\n";
            });
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
            string result = await _service.RunSshCommandAsync(PermissionData.LiveServerIp, PermissionData.ConsoUser, PermissionData.ConsoPassword, CliCommand);
            AppendLog(result);
            IsBusy = false;
        }

        private void ExecutePutty()
        {
            try
            {
                // Launches Putty with auto-login parameters
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