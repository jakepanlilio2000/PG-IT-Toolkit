using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.PureposTool.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.Models;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.PureposTool.ViewModels
{
    public class PureposViewModel : ViewModelBase
    {
        private readonly IPureposService _service;

        public AutoPermissionModel PermissionData { get; } = new AutoPermissionModel();
        public EjSaveModel EjData { get; } = new EjSaveModel();

        private string _cliOutput = "Ready to execute...\n";
        public string CliOutput { get => _cliOutput; set => SetProperty(ref _cliOutput, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand RunPermissionCommand { get; }
        public ICommand RunEjSaveCommand { get; }
        public ICommand ClearLogCommand { get; }

        public PureposViewModel(IPureposService service)
        {
            _service = service;
            RunPermissionCommand = new AsyncRelayCommand(ExecutePermissionAsync);
            RunEjSaveCommand = new AsyncRelayCommand(ExecuteEjSaveAsync);
            ClearLogCommand = new RelayCommand(() => CliOutput = "Logs cleared.\n");
        }

        private void AppendLog(string message)
        {
            // Ensure thread safety when updating the UI from background Tasks
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
    }
}