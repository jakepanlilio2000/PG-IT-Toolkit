using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.EJConsolidator.ViewModels
{
    public class EJConsolidatorViewModel : ViewModelBase
    {
        private readonly IEJConsolidatorService _consolidatorService;
        private CancellationTokenSource _cts;

        // ==========================================
        // 1. Execution Mode (Local Own Store vs Remote FTP)
        // ==========================================
        private EJExecutionMode _selectedSource = EJExecutionMode.Local;
        private string _ownStoreCode;
        private string _ownStoreName;
        private bool _catalogLoaded;
        private int _storeLookupRevision;
        public bool IsModeLocal { get => _selectedSource == EJExecutionMode.Local; set { if (value) SelectSource(EJExecutionMode.Local); } }
        public bool IsModeFtp { get => _selectedSource == EJExecutionMode.Ftp; set { if (value) SelectSource(EJExecutionMode.Ftp); } }
        public bool IsModeFtpOthers { get => _selectedSource == EJExecutionMode.FtpOthers; set { if (value) SelectSource(EJExecutionMode.FtpOthers); } }
        public bool IsFtpSettingsActive => !IsModeLocal;
        public bool IsLocalSettingsActive => IsModeLocal;
        private void SelectSource(EJExecutionMode source)
        {
            _selectedSource = source;
            if (source != EJExecutionMode.FtpOthers)
            {
                ProcessAllStores = false;
                if (_ownStoreCode != null) StoreCode = _ownStoreCode;
            }
            OnPropertyChanged(nameof(IsModeLocal)); OnPropertyChanged(nameof(IsModeFtp)); OnPropertyChanged(nameof(IsModeFtpOthers));
            OnPropertyChanged(nameof(IsFtpSettingsActive)); OnPropertyChanged(nameof(IsLocalSettingsActive));
            ResolveStore();
        }
        public ObservableCollection<EJStoreInfo> KnownStores { get; } = new ObservableCollection<EJStoreInfo>();
        private EJStoreInfo _selectedStore;
        public EJStoreInfo SelectedStore
        {
            get => _selectedStore;
            set { if (SetProperty(ref _selectedStore, value) && value != null) StoreCode = value.StoreCode; }
        }
        private string _detectedStore = "Configured store";
        public string DetectedStore { get => _detectedStore; set => SetProperty(ref _detectedStore, value); }
        private void ResolveStore()
        {
            var found = KnownStores.FirstOrDefault(s => s.StoreCode.TrimStart('0') == (StoreCode ?? "").TrimStart('0'));
            DetectedStore = found?.DisplayName ?? ((StoreCode ?? "").TrimStart('0') == (_ownStoreCode ?? "").TrimStart('0')
                ? $"{StoreCode} · {_ownStoreName}" : _catalogLoaded ? $"Store {StoreCode} · not found on the selected FTP server" : $"Store {StoreCode} · detecting its name from FTP...");
        }

        private bool _processAllStores;
        public bool ProcessAllStores
        {
            get => _processAllStores;
            set { if (SetProperty(ref _processAllStores, value)) { if (value) IsModeFtpOthers = true; ResolveStore(); } }
        }

        // ==========================================
        // 2. Operation Modes (Consolidator vs Trx Finder)
        // ==========================================
        private bool _isModeConsolidator = true;
        private bool _isModeTrxFinder = false;

        public bool IsModeConsolidator
        {
            get => _isModeConsolidator;
            set
            {
                if (SetProperty(ref _isModeConsolidator, value) && value)
                {
                    _isModeTrxFinder = false;
                    OnPropertyChanged(nameof(IsModeTrxFinder));
                }
            }
        }

        public bool IsModeTrxFinder
        {
            get => _isModeTrxFinder;
            set
            {
                if (SetProperty(ref _isModeTrxFinder, value) && value)
                {
                    _isModeConsolidator = false;
                    OnPropertyChanged(nameof(IsModeConsolidator));
                }
            }
        }

        // ==========================================
        // 3. Store, Path, and Network Settings
        // ==========================================
        private string _storeCode = "722";
        private string _localStoreDirectory;
        private string _outputDirectory;

        private string _ftpHost = "192.168.200.177";
        private int _ftpPort = 21;
        private int _ftpTimeoutSeconds = 30;
        private bool _ftpPassiveMode = true;

        private string _liveServerIp = "192.92.92.50";
        private DateTime _startDate = DateTime.Now.AddDays(-2);
        private DateTime _endDate = DateTime.Now;
        private bool _mergeAllIntoOneFile;
        private string _posLanes;
        private string _targetTrxNumber;

        public string StoreCode
        {
            get => _storeCode;
            set
            {
                if (!SetProperty(ref _storeCode, value)) return;
                if (_ownStoreCode != null && !string.IsNullOrWhiteSpace(value) && value.TrimStart('0') != _ownStoreCode.TrimStart('0'))
                    SelectSource(EJExecutionMode.FtpOthers);
                ResolveStore();
                int revision = ++_storeLookupRevision;
                if (_ownStoreCode != null && IsModeFtpOthers && Regex.IsMatch(value ?? "", @"^\d{1,6}$") &&
                    !KnownStores.Any(store => store.StoreCode.TrimStart('0') == value.TrimStart('0')))
                    _ = AutoResolveStoreAsync(revision);
            }
        }
        private async Task AutoResolveStoreAsync(int revision)
        {
            await Task.Delay(500);
            if (revision != _storeLookupRevision || !IsModeFtpOthers || IsBusy || _catalogLoaded) return;
            await RefreshStoresAsync();
        }
        public string LocalStoreDirectory { get => _localStoreDirectory; set => SetProperty(ref _localStoreDirectory, value); }
        public string OutputDirectory { get => _outputDirectory; set => SetProperty(ref _outputDirectory, value); }

        public string FtpHost { get => _ftpHost; set { if (SetProperty(ref _ftpHost, value)) _catalogLoaded = false; } }
        public int FtpPort { get => _ftpPort; set => SetProperty(ref _ftpPort, value); }
        public int FtpTimeoutSeconds { get => _ftpTimeoutSeconds; set => SetProperty(ref _ftpTimeoutSeconds, value); }
        public bool FtpPassiveMode { get => _ftpPassiveMode; set => SetProperty(ref _ftpPassiveMode, value); }

        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }
        public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }
        public bool MergeAllIntoOneFile { get => _mergeAllIntoOneFile; set => SetProperty(ref _mergeAllIntoOneFile, value); }
        public string PosLanes { get => _posLanes; set => SetProperty(ref _posLanes, value); }
        public string TargetTrxNumber { get => _targetTrxNumber; set => SetProperty(ref _targetTrxNumber, value); }

        // ==========================================
        // 4. Content Filters
        // ==========================================
        private string _specificCashier;
        private string _specificBagger;
        private bool _secondReceiptOnly;
        private bool _gcashBarcodeOnly;
        private bool _hacsOnlineOnly;
        private bool _xReadZReadOnly;
        private bool _seniorCitizenOnly;
        private bool _pwdOnly;
        private string _filterScPwdId;

        private string _filterCardLast4;
        private string _filterMemberName;
        private string _filterExactAmount;
        private string _filterProductOrSku;

        public string SpecificCashier { get => _specificCashier; set => SetProperty(ref _specificCashier, value); }
        public string SpecificBagger { get => _specificBagger; set => SetProperty(ref _specificBagger, value); }
        public bool SecondReceiptOnly { get => _secondReceiptOnly; set => SetProperty(ref _secondReceiptOnly, value); }
        public bool GcashBarcodeOnly { get => _gcashBarcodeOnly; set => SetProperty(ref _gcashBarcodeOnly, value); }
        public bool HacsOnlineOnly { get => _hacsOnlineOnly; set => SetProperty(ref _hacsOnlineOnly, value); }
        public bool XReadZReadOnly { get => _xReadZReadOnly; set => SetProperty(ref _xReadZReadOnly, value); }
        public bool SeniorCitizenOnly { get => _seniorCitizenOnly; set => SetProperty(ref _seniorCitizenOnly, value); }
        public bool PwdOnly { get => _pwdOnly; set => SetProperty(ref _pwdOnly, value); }
        public string FilterScPwdId { get => _filterScPwdId; set => SetProperty(ref _filterScPwdId, value); }

        public string FilterCardLast4 { get => _filterCardLast4; set => SetProperty(ref _filterCardLast4, value); }
        public string FilterMemberName { get => _filterMemberName; set => SetProperty(ref _filterMemberName, value); }
        public string FilterExactAmount { get => _filterExactAmount; set => SetProperty(ref _filterExactAmount, value); }
        public string FilterProductOrSku { get => _filterProductOrSku; set => SetProperty(ref _filterProductOrSku, value); }

        private string _searchText, _excludeText, _minimumAmount, _maximumAmount, _timeFrom, _timeTo;
        private string _paymentMethod = "Any", _receiptType = "Any";
        public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
        public string ExcludeText { get => _excludeText; set => SetProperty(ref _excludeText, value); }
        public string MinimumAmount { get => _minimumAmount; set => SetProperty(ref _minimumAmount, value); }
        public string MaximumAmount { get => _maximumAmount; set => SetProperty(ref _maximumAmount, value); }
        public string TimeFrom { get => _timeFrom; set => SetProperty(ref _timeFrom, value); }
        public string TimeTo { get => _timeTo; set => SetProperty(ref _timeTo, value); }
        public string PaymentMethod { get => _paymentMethod; set => SetProperty(ref _paymentMethod, value); }
        public string ReceiptType { get => _receiptType; set => SetProperty(ref _receiptType, value); }
        public string[] PaymentMethods { get; } = { "Any", "Cash", "GCash", "Maya", "Credit", "Debit", "Card", "HACS" };
        public string[] ReceiptTypes { get; } = { "Any", "Sale", "Return", "Void" };
        private bool _useCachedData;
        public bool UseCachedData { get => _useCachedData; set => SetProperty(ref _useCachedData, value); }
        public ObservableCollection<EJReceiptResult> Results { get; } = new ObservableCollection<EJReceiptResult>();
        private EJReceiptResult _selectedResult;
        public EJReceiptResult SelectedResult { get => _selectedResult; set => SetProperty(ref _selectedResult, value); }
        private bool _isResultsExpanded;
        public bool IsResultsExpanded { get => _isResultsExpanded; set => SetProperty(ref _isResultsExpanded, value); }
        private int _missingDates;
        private string _resultsSummary = "Receipt previews appear here after a search (up to 200). All matches are saved to the output folder.";
        public string ResultsSummary { get => _resultsSummary; set => SetProperty(ref _resultsSummary, value); }
        private string _lastOutputDirectory;
        public string LastOutputDirectory { get => _lastOutputDirectory; set => SetProperty(ref _lastOutputDirectory, value); }
        public ICommand ResetFiltersCommand { get; }
        public ICommand RefreshStoresCommand { get; }
        private bool _loadingStores;
        private async Task RefreshStoresAsync()
        {
            if (_loadingStores) return;
            _loadingStores = true;
            (ProcessCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            try
            {
                var stores = await _consolidatorService.GetStoresAsync(new EJFilterOptions { FtpHost = FtpHost, FtpPort = FtpPort,
                    FtpTimeoutSeconds = FtpTimeoutSeconds, FtpPassiveMode = FtpPassiveMode }, CancellationToken.None);
                _catalogLoaded = true;
                KnownStores.Clear();
                foreach (var store in stores) KnownStores.Add(store);
                ResolveStore();
                SetStatus($"Detected {stores.Count} store folders from FTP.", "#27AE60");
            }
            catch (Exception ex) { SetStatus("Cannot detect FTP stores: " + ex.Message, "#C41E3A"); }
            finally { _loadingStores = false; (ProcessCommand as IRelayCommand)?.NotifyCanExecuteChanged(); }
        }
        private void ResetFilters()
        {
            SpecificCashier = SpecificBagger = FilterCardLast4 = FilterMemberName = FilterExactAmount = FilterProductOrSku = FilterScPwdId = null;
            SearchText = ExcludeText = MinimumAmount = MaximumAmount = TimeFrom = TimeTo = PosLanes = TargetTrxNumber = null;
            PaymentMethod = ReceiptType = "Any";
            SecondReceiptOnly = GcashBarcodeOnly = HacsOnlineOnly = XReadZReadOnly = SeniorCitizenOnly = PwdOnly = false;
        }

        // ==========================================
        // 5. Status & Progress
        // ==========================================
        private string _statusMessage = "Ready.";
        private string _statusColor = "#7F8FA6";
        private int _progressValue = 0;
        private string _progressText = "0 / 0 dates";
        private bool _isBusy;

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }
        public int ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }
        public string ProgressText { get => _progressText; set => SetProperty(ref _progressText, value); }
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanCancel));
                    ((AsyncRelayCommand)ProcessCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)CancelCommand).NotifyCanExecuteChanged();
                }
            }
        }
        public bool CanCancel => IsBusy;

        // ==========================================
        // Commands
        // ==========================================
        public ICommand ProcessCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseLocalDirCommand { get; }
        public ICommand BrowseOutputDirCommand { get; }
        public ICommand OpenOutputDirCommand { get; }

        public EJConsolidatorViewModel(IEJConsolidatorService consolidatorService)
        {
            _consolidatorService = consolidatorService;

            ResetFiltersCommand = new RelayCommand(ResetFilters);
            RefreshStoresCommand = new AsyncRelayCommand(RefreshStoresAsync);
            ProcessCommand = new AsyncRelayCommand(ExecuteProcessAsync, () => !IsBusy && !_loadingStores);
            CancelCommand = new RelayCommand(CancelProcess, () => IsBusy);

            BrowseLocalDirCommand = new RelayCommand(() => BrowseFolder(p => LocalStoreDirectory = p));
            BrowseOutputDirCommand = new RelayCommand(() => BrowseFolder(p => OutputDirectory = p));
            OpenOutputDirCommand = new RelayCommand(OpenOutputDirectory);

            // Load settings
            var globalSettings = SettingsViewModel.GetCurrentSettings();
            StoreCode = globalSettings.StoreCode;
            _ownStoreCode = StoreCode;
            _ownStoreName = globalSettings.StoreName;
            try
            {
                string catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StoreProfilesDatabase.json");
                if (File.Exists(catalogPath))
                {
                    var stores = System.Text.Json.JsonSerializer.Deserialize<List<PuregoldITToolkit.Tools.ServiceDeskTool.Models.StoreProfileModel>>(File.ReadAllText(catalogPath));
                    foreach (var store in stores ?? new List<PuregoldITToolkit.Tools.ServiceDeskTool.Models.StoreProfileModel>())
                        if (!string.IsNullOrWhiteSpace(store.StoreCode) && !KnownStores.Any(k => k.StoreCode == store.StoreCode))
                            KnownStores.Add(new EJStoreInfo { StoreCode = store.StoreCode, StoreName = store.StoreName });
                }
                string cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FtpStoresCache.json");
                if (File.Exists(cachePath))
                {
                    var stores = System.Text.Json.JsonSerializer.Deserialize<List<PuregoldITToolkit.Tools.FtpBrowserTool.Models.FtpStoreModel>>(File.ReadAllText(cachePath));
                    foreach (var store in stores ?? new List<PuregoldITToolkit.Tools.FtpBrowserTool.Models.FtpStoreModel>())
                        if (!string.IsNullOrWhiteSpace(store.StoreCode) && !KnownStores.Any(k => k.StoreCode == store.StoreCode))
                            KnownStores.Add(new EJStoreInfo { StoreCode = store.StoreCode, StoreName = store.StoreName, FolderName = store.OriginalFolderName });
                }
            }
            catch (Exception ex) { LoggingService.Instance.LogWarning("Store catalog could not be loaded: " + ex.Message); }
            ResolveStore();
            LiveServerIp = globalSettings.DefaultLiveServerIp;
            LocalStoreDirectory = globalSettings.EjInputDirectory;
            OutputDirectory = globalSettings.EjOutputDirectory;

            FtpHost = globalSettings.FtpHost;
            FtpPort = globalSettings.FtpPort > 0 ? globalSettings.FtpPort : 21;
            FtpTimeoutSeconds = globalSettings.FtpTimeoutSeconds > 0 ? globalSettings.FtpTimeoutSeconds : 30;
            FtpPassiveMode = globalSettings.FtpPassiveMode;
        }

        private void BrowseFolder(Action<string> setter)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select Directory";
                dlg.ShowNewFolderButton = true;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    setter(dlg.SelectedPath);
                }
            }
        }

        private void OpenOutputDirectory()
        {
            try
            {
                string path = !string.IsNullOrWhiteSpace(LastOutputDirectory) ? LastOutputDirectory : string.IsNullOrWhiteSpace(OutputDirectory)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SOD_Output")
                    : OutputDirectory;

                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{path}\"", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SetStatus($"Cannot open folder: {ex.Message}", "#C41E3A");
            }
        }

        private void CancelProcess()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                SetStatus("Cancellation requested... Aborting operations cleanly.", "#E67E22");
                LoggingService.Instance.LogWarning("Consolidation cancellation requested by user.");
                _cts.Cancel();
            }
        }

        private async Task ExecuteProcessAsync()
        {
            if (!ProcessAllStores && string.IsNullOrWhiteSpace(StoreCode))
            {
                SetStatus("Error: Store Code cannot be empty.", "#C41E3A");
                return;
            }

            if (IsModeLocal && string.IsNullOrWhiteSpace(LiveServerIp)) { SetStatus("Enter the live/CONSO server address.", "#C41E3A"); return; }
            if (IsFtpSettingsActive && (string.IsNullOrWhiteSpace(FtpHost) || FtpPort < 1 || FtpPort > 65535 || FtpTimeoutSeconds < 1))
            {
                SetStatus("Error: Specify an FTP host, a port from 1 to 65535, and a positive timeout.", "#C41E3A");
                return;
            }

            if (StartDate.Date > EndDate.Date)
            {
                SetStatus("Error: Start Date cannot be later than End Date.", "#C41E3A");
                return;
            }

            if (IsModeTrxFinder && string.IsNullOrWhiteSpace(TargetTrxNumber))
            {
                SetStatus("Error: Transaction Number is required in Finder Mode.", "#C41E3A");
                return;
            }

            if (!ProcessAllStores && !Regex.IsMatch(StoreCode ?? "", @"^\d{1,6}$")) { SetStatus("Enter a numeric store code (1–6 digits).", "#C41E3A"); return; }
            if (IsModeTrxFinder && !Regex.IsMatch(TargetTrxNumber ?? "", @"^\d+(?:-\d+)*$")) { SetStatus("Enter a numeric transaction or store-lane-invoice number.", "#C41E3A"); return; }
            if ((EndDate.Date - StartDate.Date).TotalDays > 366) { SetStatus("Select a date range of at most 367 days.", "#C41E3A"); return; }
            if (!string.IsNullOrWhiteSpace(FilterCardLast4) && !Regex.IsMatch(FilterCardLast4, @"^\d{4}$")) { SetStatus("Card last 4 must be exactly four digits.", "#C41E3A"); return; }
            decimal? min = Services.ReceiptFilterService.ParseAmount(MinimumAmount), max = Services.ReceiptFilterService.ParseAmount(MaximumAmount);
            if ((!string.IsNullOrWhiteSpace(MinimumAmount) && !min.HasValue) || (!string.IsNullOrWhiteSpace(MaximumAmount) && !max.HasValue) ||
                (!string.IsNullOrWhiteSpace(FilterExactAmount) && !Services.ReceiptFilterService.ParseAmount(FilterExactAmount).HasValue) || min > max)
            { SetStatus("Enter valid amounts with a decimal point; minimum must not exceed maximum.", "#C41E3A"); return; }
            TimeSpan? from = null, to = null;
            if (!string.IsNullOrWhiteSpace(TimeFrom)) { if (!TimeSpan.TryParseExact(TimeFrom.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var parsed)) { SetStatus("Start time must use HH:mm (00:00–23:59).", "#C41E3A"); return; } from = parsed; }
            if (!string.IsNullOrWhiteSpace(TimeTo)) { if (!TimeSpan.TryParseExact(TimeTo.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var parsed)) { SetStatus("End time must use HH:mm (00:00–23:59).", "#C41E3A"); return; } to = parsed; }
            if (from >= TimeSpan.FromDays(1) || to >= TimeSpan.FromDays(1) || from > to) { SetStatus("Start time must not exceed end time, within the same day.", "#C41E3A"); return; }
            var lanes = new List<string>();
            if (!string.IsNullOrWhiteSpace(PosLanes))
            {
                foreach (string token in PosLanes.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var range = Regex.Match(token.Trim(), @"^(\d{1,3})(?:-(\d{1,3}))?$");
                    if (!range.Success) { SetStatus("Use lane numbers or ranges, e.g. 1,3-5.", "#C41E3A"); return; }
                    int start = int.Parse(range.Groups[1].Value), end = range.Groups[2].Success ? int.Parse(range.Groups[2].Value) : start;
                    if (start < 1 || end > 999 || start > end) { SetStatus("Lanes must be between 1 and 999 in ascending ranges.", "#C41E3A"); return; }
                    for (int lane = start; lane <= end; lane++) if (!lanes.Contains(lane.ToString())) lanes.Add(lane.ToString());
                }
            }

            IsBusy = true;
            ProgressValue = 0;
            _missingDates = 0;
            Results.Clear(); SelectedResult = null; IsResultsExpanded = false; LastOutputDirectory = null;
            ResultsSummary = "Searching...";
            ProgressText = "Initializing...";
            SetStatus($"Starting { (IsModeLocal ? "LOCAL (Own Store)" : "FTP (All Stores)") } Consolidation...", "#2563EB");

            _cts = new CancellationTokenSource();

            try
            {
                var dates = new List<DateTime>();
                for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1)) dates.Add(d);

                var options = new EJFilterOptions
                {
                    ExecutionMode = _selectedSource,
                    ProcessAllStores = this.ProcessAllStores && IsModeFtpOthers,
                    LocalStoreDirectory = this.LocalStoreDirectory?.Trim(),
                    OutputDirectory = this.OutputDirectory?.Trim(),

                    FtpHost = this.FtpHost?.Trim(),
                    FtpPort = this.FtpPort,
                    FtpTimeoutSeconds = this.FtpTimeoutSeconds,
                    FtpPassiveMode = this.FtpPassiveMode,

                    IsModeTrxFinder = this.IsModeTrxFinder,
                    TargetTrxNumber = this.TargetTrxNumber?.Trim(),
                    StoreCode = this.StoreCode?.Trim(),
                    LiveServerIp = this.LiveServerIp?.Trim(),
                    TargetDates = dates,
                    MergeAllIntoOneFile = this.MergeAllIntoOneFile,

                    SpecificCashier = this.SpecificCashier?.Trim(),
                    SpecificBagger = this.SpecificBagger?.Trim(),
                    SecondReceiptOnly = this.SecondReceiptOnly,
                    GcashBarcodeOnly = this.GcashBarcodeOnly,
                    HacsOnlineOnly = this.HacsOnlineOnly,
                    XReadZReadOnly = this.XReadZReadOnly,
                    SeniorCitizenOnly = this.SeniorCitizenOnly,
                    PwdOnly = this.PwdOnly,
                    FilterScPwdId = this.FilterScPwdId?.Trim(),
                    FilterCardLast4 = this.FilterCardLast4?.Trim(),
                    FilterMemberName = this.FilterMemberName?.Trim(),
                    FilterExactAmount = this.FilterExactAmount?.Trim(),
                    FilterProductOrSku = this.FilterProductOrSku?.Trim(),

                    SearchText = SearchText?.Trim(), ExcludeText = ExcludeText?.Trim(), PaymentMethod = PaymentMethod, ReceiptType = ReceiptType,
                    MinimumAmount = min, MaximumAmount = max, TimeFrom = from, TimeTo = to, UseCachedData = UseCachedData,
                    PosLanes = lanes
                };

                var progress = new Progress<EJProgressReport>(report =>
                {
                    if (report.Receipt != null)
                    {
                        Results.Add(report.Receipt);
                        if (SelectedResult == null) SelectedResult = report.Receipt;
                        IsResultsExpanded = true;
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(report.DetectedStore)) DetectedStore = report.DetectedStore;
                    if (!string.IsNullOrWhiteSpace(report.OutputDirectory)) LastOutputDirectory = report.OutputDirectory;
                    _missingDates = Math.Max(_missingDates, report.MissingDates);
                    ProgressValue = report.Percentage;
                    ProgressText = $"{report.ProcessedFiles} / {report.TotalFiles} dates ({report.Percentage}%)";
                    SetStatus(report.StatusMessage, "#2563EB");
                });

                int resultCount = await _consolidatorService.ProcessConsolidationAsync(options, progress, _cts.Token);

                ResultsSummary = $"{resultCount} matching receipts · {_missingDates} dates without data · previewing {Results.Count} (maximum 200). All matches are saved in the output folder.";
                if (resultCount > 0)
                {
                    string outPath = LastOutputDirectory ?? OutputDirectory;
                    SetStatus($"Finished. {resultCount} matching receipts saved to {outPath}.", "#27AE60");
                }
                else
                {
                    SetStatus("Process finished, but no receipts matched your filters or date range.", "#E67E22");
                }
            }
            catch (OperationCanceledException)
            {
                ResultsSummary = $"Cancelled · {Results.Count} receipt previews from completed dates.";
                SetStatus("Operation cancelled. Completed dates remain in the output folder; incomplete cache data will be refreshed.", "#E67E22");
            }
            catch (Exception ex)
            {
                ResultsSummary = "Search failed · see the status message and application log.";
                SetStatus($"Could not finish: {ex.Message}", "#C41E3A");
                LoggingService.Instance.LogError("EJ Consolidation Critical Error", ex);
            }
            finally
            {
                IsBusy = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void SetStatus(string message, string hexColor)
        {
            StatusMessage = message;
            StatusColor = hexColor;
        }
    }
}
