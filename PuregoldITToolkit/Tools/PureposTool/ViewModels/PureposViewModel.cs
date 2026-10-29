using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.PureposTool.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.Models;
using PuregoldITToolkit.Tools.PureposTool.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.PureposTool.ViewModels
{
    public class PureposViewModel : ViewModelBase
    {
        private readonly IPureposService _service;
        private readonly ToolkitDiagnosticsService _diagService;

        public AutoPermissionModel PermissionData { get; } = new AutoPermissionModel();
        public EjSaveModel EjData { get; } = new EjSaveModel();
        public EodModel EodData { get; } = new EodModel();
        public AutoSyncModel AutoSyncData { get; } = new AutoSyncModel();

        // ==========================================
        // DIAGNOSTICS & NETWORK UTILITIES
        // ==========================================
        public ObservableCollection<PingTargetResult> PingTargets { get; } = new ObservableCollection<PingTargetResult>();
        public ObservableCollection<PortCheckResult> PortResults { get; } = new ObservableCollection<PortCheckResult>();
        public ObservableCollection<NetworkAdapterDetail> NetworkAdapters { get; } = new ObservableCollection<NetworkAdapterDetail>();
        public ObservableCollection<CacheDirectoryInfo> CacheDirectories { get; } = new ObservableCollection<CacheDirectoryInfo>();

        private string _diagnosticGatewayIp = "192.92.92.1";
        public string DiagnosticGatewayIp { get => _diagnosticGatewayIp; set => SetProperty(ref _diagnosticGatewayIp, value); }

        private string _diagnosticServerIp = "192.92.92.51";
        public string DiagnosticServerIp { get => _diagnosticServerIp; set => SetProperty(ref _diagnosticServerIp, value); }

        private int _diagnosticTotalLanes = 10;
        public int DiagnosticTotalLanes { get => _diagnosticTotalLanes; set => SetProperty(ref _diagnosticTotalLanes, value); }

        private string _portCheckTargetIp = "192.92.92.51";
        public string PortCheckTargetIp { get => _portCheckTargetIp; set => SetProperty(ref _portCheckTargetIp, value); }

        public ObservableCollection<string> AvailableServices { get; } = new ObservableCollection<string>
        {
            "Spooler",
            "W32Time",
            "LanmanServer",
            "PureposService",
            "MpsSvc"
        };

        private string _selectedServiceToRestart = "Spooler";
        public string SelectedServiceToRestart { get => _selectedServiceToRestart; set => SetProperty(ref _selectedServiceToRestart, value); }

        private NetworkAdapterDetail _selectedAdapter;
        public NetworkAdapterDetail SelectedAdapter { get => _selectedAdapter; set => SetProperty(ref _selectedAdapter, value); }

        // ==========================================
        // CLI PROPERTIES
        // ==========================================
        private string _cliTargetIp = "192.92.92.51";
        private string _cliUsername;
        private string _cliPassword;
        private string _cliCommand = "ls -la";

        public string CliTargetIp { get => _cliTargetIp; set => SetProperty(ref _cliTargetIp, value); }
        public string CliUsername { get => _cliUsername; set => SetProperty(ref _cliUsername, value); }
        public string CliPassword { get => _cliPassword; set => SetProperty(ref _cliPassword, value); }
        public string CliCommand { get => _cliCommand; set => SetProperty(ref _cliCommand, value); }

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

        private string _cliOutput = "Ready.\n";
        public string CliOutput { get => _cliOutput; set => SetProperty(ref _cliOutput, value); }

        public string EodPreviewTo { get; private set; }
        public string EodPreviewCc { get; private set; }
        public string EodPreviewSubject { get; private set; }
        public string EodPreviewBody { get; private set; }
        public string EodPreviewAttachment { get; private set; }

        private readonly List<IAsyncRelayCommand> _operations = new List<IAsyncRelayCommand>();
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { if (SetProperty(ref _isBusy, value)) foreach (var command in _operations) command.NotifyCanExecuteChanged(); }
        }
        private string _operationStatus = "Ready";
        public string OperationStatus { get => _operationStatus; set => SetProperty(ref _operationStatus, value); }
        private IAsyncRelayCommand CreateOperation(Func<Task> operation)
        {
            var command = new AsyncRelayCommand(async () =>
            {
                if (IsBusy) return;
                IsBusy = true;
                OperationStatus = "Working...";
                try { await operation(); OperationStatus = "Finished."; }
                catch (Exception ex)
                {
                    OperationStatus = "Failed · see console";
                    AppendLog("[ERROR] " + ex.Message);
                    PuregoldITToolkit.Services.LoggingService.Instance.LogError("PurePOS operation failed", ex);
                }
                finally { IsBusy = false; }
            }, () => !IsBusy);
            _operations.Add(command);
            return command;
        }
        private static void ValidateLan(string server, int lanes)
        {
            if (!IPAddress.TryParse(server, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Enter a valid IPv4 server address.");
            if (lanes < 1 || lanes > 204) throw new ArgumentException("Lane count must be between 1 and 204 (POS addresses use 50 + lane).");
        }
        private void ValidateCli(string host)
        {
            if (string.IsNullOrWhiteSpace(host) || Uri.CheckHostName(host.Trim()) == UriHostNameType.Unknown)
                throw new ArgumentException("Enter a valid target host or IP address.");
            if (string.IsNullOrWhiteSpace(CliCommand)) throw new ArgumentException("Enter an SSH command.");
        }
        public ICommand ExportDiagnosticsCommand { get; }
        public ICommand SaveConsoleCommand { get; }


        // Commands
        public ICommand RunNetworkPingSuiteCommand { get; }
        public ICommand RunPortCheckSuiteCommand { get; }
        public ICommand ScanCachesCommand { get; }
        public ICommand CleanCachesCommand { get; }
        public ICommand ArchiveLogsCommand { get; }
        public ICommand RestartServiceCommand { get; }
        public ICommand RefreshNetworkAdaptersCommand { get; }
        public ICommand CopyNetworkInfoCommand { get; }
        public ICommand OpenCriticalPathCommand { get; }

        public ICommand RunPermissionCommand { get; }
        public ICommand RunEjSaveCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand RunEodCommand { get; }
        public ICommand RunSingleCliCommand { get; }
        public ICommand RunAllPosCliCommand { get; }
        public ICommand RunConsoCliCommand { get; }
        public ICommand OpenPuttyCommand { get; }
        public ICommand RunBirEisCommand { get; }
        public ICommand RunCrmPointsCommand { get; }
        public ICommand RunPromoAvailmentCommand { get; }

        public PureposViewModel(IPureposService service, ToolkitDiagnosticsService diagService = null)
        {
            _service = service;
            _diagService = diagService ?? new ToolkitDiagnosticsService();

            // Diagnostics Commands
            RunNetworkPingSuiteCommand = CreateOperation(ExecuteNetworkPingSuiteAsync);
            RunPortCheckSuiteCommand = CreateOperation(ExecutePortCheckSuiteAsync);
            ScanCachesCommand = CreateOperation(ExecuteScanCachesAsync);
            CleanCachesCommand = CreateOperation(ExecuteCleanCachesAsync);
            ArchiveLogsCommand = CreateOperation(ExecuteArchiveLogsAsync);
            RestartServiceCommand = CreateOperation(ExecuteRestartServiceAsync);
            RefreshNetworkAdaptersCommand = new RelayCommand(ExecuteRefreshNetworkAdapters);
            CopyNetworkInfoCommand = new RelayCommand(ExecuteCopyNetworkInfo);
            OpenCriticalPathCommand = new RelayCommand<string>(ExecuteOpenCriticalPath);

            ExportDiagnosticsCommand = new RelayCommand(() => SaveReport(BuildDiagnosticsReport(), "PurePOS_Diagnostics"));
            SaveConsoleCommand = new RelayCommand(() => SaveReport(CliOutput, "PurePOS_Console"));

            // Existing POS commands
            RunPermissionCommand = CreateOperation(ExecutePermissionAsync);
            RunEjSaveCommand = CreateOperation(ExecuteEjSaveAsync);
            ClearLogCommand = new RelayCommand(() => CliOutput = "Logs cleared.\n");
            RunEodCommand = CreateOperation(ExecuteEodAsync);
            RunSingleCliCommand = CreateOperation(ExecuteSingleCliAsync);
            RunAllPosCliCommand = CreateOperation(ExecuteAllPosCliAsync);
            RunConsoCliCommand = CreateOperation(ExecuteConsoCliAsync);
            OpenPuttyCommand = new RelayCommand(ExecutePutty);

            RunBirEisCommand = CreateOperation(async () =>
            {
                ValidateLan(AutoSyncData.LiveServerIp, 1);
                await _service.RunBirEisSyncAsync(AutoSyncData, AppendLog);
            });

            RunCrmPointsCommand = CreateOperation(async () =>
            {
                ValidateLan(AutoSyncData.LiveServerIp, 1);
                await _service.RunCrmPointsSyncAsync(AutoSyncData, AppendLog);
            });

            RunPromoAvailmentCommand = CreateOperation(async () =>
            {
                ValidateLan(AutoSyncData.LiveServerIp, 1);
                if (AutoSyncData.DateFrom.Date > AutoSyncData.DateTo.Date) throw new ArgumentException("Promo start date must not exceed end date.");
                await _service.RunPromoAvailmentSyncAsync(AutoSyncData, AppendLog);
            });

            var settings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();
            CliUsername = settings.PosUsername;
            CliPassword = settings.PosPassword;
            if (!string.IsNullOrEmpty(PermissionData.LiveServerIp))
            {
                DiagnosticServerIp = PermissionData.LiveServerIp;
                PortCheckTargetIp = PermissionData.LiveServerIp;
            }

            EodData.PropertyChanged += (s, e) => UpdateEodPreview();
            UpdateEodPreview();

            DiagnosticTotalLanes = PermissionData.TotalLanes = settings.PosLanesCount;
            CliTargetIp = settings.DefaultLiveServerIp;
            if (IPAddress.TryParse(settings.DefaultLiveServerIp, out var serverAddress) && serverAddress.AddressFamily == AddressFamily.InterNetwork)
                DiagnosticGatewayIp = settings.DefaultLiveServerIp.Substring(0, settings.DefaultLiveServerIp.LastIndexOf('.') + 1) + "1";
            ExecuteRefreshNetworkAdapters();
        }

        // ==========================================
        // DIAGNOSTICS & STORE UTILITIES IMPLEMENTATION
        // ==========================================
        public async Task ExecuteNetworkPingSuiteAsync()
        {
            ValidateLan(DiagnosticServerIp, DiagnosticTotalLanes);
            PingTargets.Clear();
            AppendLog($"\n[DIAGNOSTICS] Starting Automated Ping Suite across LAN registers & servers...");

            // 1. Gateway
            var gatewayTarget = new PingTargetResult { TargetName = "Default Gateway", IpAddress = DiagnosticGatewayIp, Status = "Testing..." };
            PingTargets.Add(gatewayTarget);

            // 2. CONSO / Live Server
            var serverTarget = new PingTargetResult { TargetName = "Live Server / CONSO", IpAddress = DiagnosticServerIp, Status = "Testing..." };
            PingTargets.Add(serverTarget);

            // 3. POS Registers (50 + Lane No.)
            string ipPrefix = DiagnosticServerIp.Contains('.')
                ? DiagnosticServerIp.Substring(0, DiagnosticServerIp.LastIndexOf('.') + 1)
                : "192.92.92.";

            for (int i = 1; i <= DiagnosticTotalLanes; i++)
            {
                string laneIp = $"{ipPrefix}{50 + i}";
                PingTargets.Add(new PingTargetResult { TargetName = $"POS Lane {i:D2}", IpAddress = laneIp, Status = "Testing..." });
            }

            var pingTasks = PingTargets.Select(async target =>
            {
                var res = await _diagService.PingHostAsync(target.TargetName, target.IpAddress);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    target.Status = res.Status;
                    target.LatencyMs = res.LatencyMs;
                    target.StatusColor = res.StatusColor;
                    AppendLog($"[PING] {target.TargetName} ({target.IpAddress}) -> {res.Status} ({res.LatencyMs}ms)");
                });
            });

            await Task.WhenAll(pingTasks);
            AppendLog("[DIAGNOSTICS] Ping test suite completed.\n");
        }

        public async Task ExecutePortCheckSuiteAsync()
        {
            PortResults.Clear();
            AppendLog($"\n[PORT CHECK] Checking standard ports on {PortCheckTargetIp}...");

            var portsToCheck = new (int Port, string Name)[]
            {
                (21, "FTP (Port 21)"),
                (22, "SSH (Port 22)"),
                (3306, "MySQL Database (Port 3306)"),
                (1433, "MSSQL Database (Port 1433)"),
                (5432, "PostgreSQL Database (Port 5432)"),
                (4899, "Radmin Remote (Port 4899)"),
                (5900, "VNC Remote (Port 5900)"),
                (3389, "RDP Remote Desktop (Port 3389)")
            };

            foreach (var p in portsToCheck)
            {
                var item = new PortCheckResult { Port = p.Port, ServiceName = p.Name, Status = "Testing..." };
                PortResults.Add(item);
            }

            var tasks = PortResults.Select(async item =>
            {
                var res = await _diagService.CheckPortAsync(PortCheckTargetIp, item.Port, item.ServiceName);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    item.Status = res.Status;
                    item.LatencyMs = res.LatencyMs;
                    item.StatusColor = res.StatusColor;
                    AppendLog($"[PORT] {item.ServiceName} -> {res.Status} ({res.LatencyMs}ms)");
                });
            });

            await Task.WhenAll(tasks);
            AppendLog("[PORT CHECK] Fast port checking complete.\n");
        }

        public async Task ExecuteScanCachesAsync()
        {
            var list = await Task.Run(() => _diagService.ScanCacheDirectories());
            CacheDirectories.Clear();
            foreach (var item in list) CacheDirectories.Add(item);
            AppendLog($"[MAINTENANCE] Scanned {list.Count} cache directories.");
        }

        public async Task ExecuteCleanCachesAsync()
        {
            AppendLog("\n[MAINTENANCE] Cleaning temporary caches and transaction dumps...");
            int deleted = await _diagService.CleanCacheDirectoriesAsync(AppendLog);
            await ExecuteScanCachesAsync();
            AppendLog($"[MAINTENANCE] Cleanup complete. Removed {deleted} temporary cache files.\n");
        }

        public async Task ExecuteArchiveLogsAsync()
        {
            AppendLog("\n[MAINTENANCE] Archiving application log directory into zip package...");
            string path = await _diagService.ArchiveLogsAsync(AppendLog);
            if (!string.IsNullOrEmpty(path))
            {
                AppendLog($"[MAINTENANCE] Archive saved to {path}");
            }
        }

        public async Task ExecuteRestartServiceAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedServiceToRestart)) return;

            AppendLog($"\n[SERVICE] Initiating restart for service '{SelectedServiceToRestart}'...");
            await _diagService.RestartWindowsServiceAsync(SelectedServiceToRestart, AppendLog);
        }

        public void ExecuteRefreshNetworkAdapters()
        {
            NetworkAdapters.Clear();
            var list = _diagService.GetNetworkAdapters();
            foreach (var a in list) NetworkAdapters.Add(a);
            SelectedAdapter = NetworkAdapters.FirstOrDefault();
        }

        public void ExecuteCopyNetworkInfo()
        {
            if (SelectedAdapter == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== PUREGOLD POS NETWORK ADAPTER CONFIGURATION ===");
            sb.AppendLine($"Adapter Name:     {SelectedAdapter.Name}");
            sb.AppendLine($"Description:      {SelectedAdapter.Description}");
            sb.AppendLine($"IPv4 Address:     {SelectedAdapter.IpAddress}");
            sb.AppendLine($"Subnet Mask:      {SelectedAdapter.SubnetMask}");
            sb.AppendLine($"Default Gateway:  {SelectedAdapter.Gateway}");
            sb.AppendLine($"DNS Servers:      {SelectedAdapter.DnsServers}");
            sb.AppendLine($"MAC Address:      {SelectedAdapter.MacAddress}");
            sb.AppendLine($"Link Speed:       {SelectedAdapter.Speed}");
            sb.AppendLine($"Status:           {SelectedAdapter.OperationalStatus}");

            try
            {
                Clipboard.SetText(sb.ToString());
                AppendLog("[NETWORK] Network adapter details copied to clipboard.");
            }
            catch (Exception ex)
            {
                AppendLog($"[NETWORK] Could not copy to clipboard: {ex.Message}");
            }
        }

        public void ExecuteOpenCriticalPath(string pathType)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetPath = baseDir;

            switch (pathType?.ToLower())
            {
                case "ejcache":
                    targetPath = Path.Combine(baseDir, "EJ_Cache");
                    break;
                case "output":
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    targetPath = Path.Combine(desktop, "Puregold_Forms_Output");
                    break;
                case "temp":
                    targetPath = Path.GetTempPath();
                    break;
                case "logs":
                    targetPath = Path.Combine(baseDir, "Logs");
                    break;
                case "app":
                default:
                    targetPath = baseDir;
                    break;
            }

            try
            {
                if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);
                Process.Start(new ProcessStartInfo { FileName = targetPath, UseShellExecute = true });
                AppendLog($"[EXPLORER] Opened path: {targetPath}");
            }
            catch (Exception ex)
            {
                AppendLog($"[EXPLORER] Error opening {targetPath}: {ex.Message}");
            }
        }

        // ==========================================
        // EXISTING POS OPERATIONS & LOGIC
        // ==========================================
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
            void Add()
            {
                string next = CliOutput + message + Environment.NewLine;
                CliOutput = next.Length > 200000 ? next.Substring(next.Length - 200000) : next;
            }
            if (Application.Current == null || Application.Current.Dispatcher.CheckAccess()) Add();
            else Application.Current.Dispatcher.Invoke(Add);
        }

        private bool LaunchInteractiveTerminal(string ip, string user, string pass, string command)
        {
            string cmdLower = command.Trim().ToLower();
            string baseCmd = cmdLower.StartsWith("sudo ") ? cmdLower.Substring(5).Trim() : cmdLower;

            if (baseCmd.StartsWith("nano") || baseCmd.StartsWith("vi") || baseCmd.StartsWith("vim") || baseCmd.StartsWith("top") || baseCmd.StartsWith("htop"))
            {
                try
                {
                    string tempScript = Path.Combine(Path.GetTempPath(), $"purepos_cmd_{Guid.NewGuid().ToString("N").Substring(0, 8)}.sh");
                    File.WriteAllText(tempScript, command);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "putty.exe",
                        Arguments = $"-ssh {user}@{ip} -pw {pass} -t -m \"{tempScript}\"",
                        UseShellExecute = true
                    });

                    AppendLog($"\n[INFO] Interactive editor detected! Launched Putty window for {ip}.");
                    return true;
                }
                catch
                {
                    AppendLog("\n[ERROR] Tried to launch Putty for interactive command, but failed.");
                    return true;
                }
            }
            return false;
        }

        private async Task ExecuteEodAsync()
        {
            ValidateLan(EodData.LiveServerIp, 1);
            if (string.IsNullOrWhiteSpace(EodData.StoreName)) throw new ArgumentException("Enter the EOD store name.");
            if (string.IsNullOrWhiteSpace(EodData.StoreCode)) throw new ArgumentException("Store code is required for EOD.");
            EodData.SaveCache();
            await _service.RunEodGeneratorAsync(EodData, AppendLog);
        }

        private async Task ExecutePermissionAsync()
        {
            ValidateLan(PermissionData.LiveServerIp, PermissionData.TotalLanes);
            AppendLog("\n--- STARTING AUTO PERMISSION ---");
            await _service.RunAutoPermissionAsync(PermissionData, AppendLog);
        }

        private async Task ExecuteEjSaveAsync()
        {
            if (EjData.DateFrom.Date > EjData.DateTo.Date) throw new ArgumentException("EJ start date must not exceed end date.");
            ValidateLan(EjData.LiveServerIp, 1);
            AppendLog($"\n--- STARTING MANUAL EJ SAVE ---");
            AppendLog($"Date Range: {EjData.DateFrom:MM/dd/yyyy} to {EjData.DateTo:MM/dd/yyyy}");
            await _service.RunManualEjSaveAsync(EjData, AppendLog);
        }

        private async Task ExecuteSingleCliAsync()
        {
            ValidateCli(CliTargetIp);
            AppendLog($"\n> Executing on {CliTargetIp}: {CliCommand}");

            if (!LaunchInteractiveTerminal(CliTargetIp, CliUsername, CliPassword, CliCommand))
            {
                string result = await _service.RunSshCommandAsync(CliTargetIp, CliUsername, CliPassword, CliCommand);
                AppendLog(result);
            }
        }

        private async Task ExecuteAllPosCliAsync()
        {
            ValidateCli(PermissionData.LiveServerIp);
            ValidateLan(PermissionData.LiveServerIp, PermissionData.TotalLanes);
            AppendLog($"\n> Executing on ALL POS LANES: {CliCommand}");

            string ipPrefix = PermissionData.LiveServerIp.Substring(0, PermissionData.LiveServerIp.LastIndexOf('.') + 1);

            string cmdLower = CliCommand.Trim().ToLower();
            string baseCmd = cmdLower.StartsWith("sudo ") ? cmdLower.Substring(5).Trim() : cmdLower;

            if (baseCmd.StartsWith("nano") || baseCmd.StartsWith("vi") || baseCmd.StartsWith("vim") || baseCmd.StartsWith("top") || baseCmd.StartsWith("htop"))
            {
                AppendLog("[INFO] Interactive command in Batch Mode. Opening Putty windows...");
                for (int i = 1; i <= PermissionData.TotalLanes; i++)
                {
                    string posIp = $"{ipPrefix}{50 + i}";
                    LaunchInteractiveTerminal(posIp, CliUsername, CliPassword, CliCommand);
                }
                return;
            }

            string user = CliUsername, password = CliPassword, command = CliCommand;
            using (var throttle = new SemaphoreSlim(6))
            {
                var tasks = Enumerable.Range(1, PermissionData.TotalLanes).Select(async lane =>
                {
                    await throttle.WaitAsync();
                    try { AppendLog(await _service.RunSshCommandAsync($"{ipPrefix}{50 + lane}", user, password, command)); }
                    finally { throttle.Release(); }
                });
                await Task.WhenAll(tasks);
            }
        }

        private async Task ExecuteConsoCliAsync()
        {
            ValidateCli(PermissionData.LiveServerIp);
            AppendLog($"\n> Executing on CONSO ({PermissionData.LiveServerIp}): {CliCommand}");

            var globalSettings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();

            if (!LaunchInteractiveTerminal(PermissionData.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword, CliCommand))
            {
                string result = await _service.RunSshCommandAsync(PermissionData.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword, CliCommand);
                AppendLog(result);
            }
        }

        private string BuildDiagnosticsReport()
        {
            var report = new StringBuilder();
            report.AppendLine($"PurePOS diagnostics · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Server: {DiagnosticServerIp} | Lanes: {DiagnosticTotalLanes}");
            foreach (var target in PingTargets) report.AppendLine($"{target.TargetName} | {target.IpAddress} | {target.Status} | {target.LatencyMs} ms");
            report.AppendLine($"Port checks: {PortCheckTargetIp}");
            foreach (var port in PortResults) report.AppendLine($"{port.Port} | {port.ServiceName} | {port.Status} | {port.LatencyMs} ms");
            foreach (var adapter in NetworkAdapters) report.AppendLine($"{adapter.Name} | IPv4 {adapter.IpAddress} | Gateway {adapter.Gateway} | DNS {adapter.DnsServers} | {adapter.OperationalStatus}");
            return report.ToString();
        }
        private void SaveReport(string contents, string name)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.txt" };
            if (dialog.ShowDialog() != true) return;
            try { File.WriteAllText(dialog.FileName, contents, Encoding.UTF8); AppendLog("[SAVED] " + dialog.FileName); }
            catch (Exception ex) { AppendLog("[ERROR] Cannot save report: " + ex.Message); }
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
                AppendLog("\n[ERROR] Could not launch Putty. Make sure putty.exe is in PATH or application folder.");
            }
        }
    }
}
