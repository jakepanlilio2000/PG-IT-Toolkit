using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.EJConsolidator.ViewModels
{
    public class EJConsolidatorViewModel : ViewModelBase
    {
        private readonly IEJConsolidatorService _service;

        private string _storeCode;
        private string _storeName; // NEW
        private string _liveServerIp;
        private string _totalPosCount = "6";
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today;

        private bool _isModeConsolidator = true;
        private bool _isModeTrxFinder;
        private string _posLanes;
        private string _targetTrxNumber;

        private bool _secondReceiptOnly;
        private bool _gcashBarcodeOnly;
        private bool _hacsOnlineOnly;
        private bool _xReadZReadOnly;
        private bool _mergeAllIntoOneFile;

        private string _specificCashier;
        private string _specificBagger;
        private string _filterExactAmount;
        private string _filterMemberName;
        private string _filterCardLast4;
        private string _filterProductOrSku;

        // --- Excel Report Parameters ---
        private bool _generateScReport;
        private bool _generatePwdReport;
        private int _reportChunkDays = 3;
        private string _reportUserId; // NEW

        private string _statusMessage = "Ready.";
        private string _statusColor = "#7F8FA6";
        private int _progressValue;
        private bool _isBusy;

        public string StoreCode { get => _storeCode; set => SetProperty(ref _storeCode, value); }
        public string StoreName { get => _storeName; set => SetProperty(ref _storeName, value); } // NEW
        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public string TotalPosCount { get => _totalPosCount; set => SetProperty(ref _totalPosCount, value); }
        public DateTime StartDate { get => _startDate; set { SetProperty(ref _startDate, value); if (EndDate < value) EndDate = value; } }
        public DateTime EndDate { get => _endDate; set { SetProperty(ref _endDate, value); if (StartDate > value) StartDate = value; } }

        public bool IsModeConsolidator { get => _isModeConsolidator; set => SetProperty(ref _isModeConsolidator, value); }
        public bool IsModeTrxFinder { get => _isModeTrxFinder; set => SetProperty(ref _isModeTrxFinder, value); }
        public string PosLanes { get => _posLanes; set => SetProperty(ref _posLanes, value); }
        public string TargetTrxNumber { get => _targetTrxNumber; set => SetProperty(ref _targetTrxNumber, value); }

        public bool SecondReceiptOnly { get => _secondReceiptOnly; set => SetProperty(ref _secondReceiptOnly, value); }
        public bool GcashBarcodeOnly { get => _gcashBarcodeOnly; set => SetProperty(ref _gcashBarcodeOnly, value); }
        public bool HacsOnlineOnly { get => _hacsOnlineOnly; set => SetProperty(ref _hacsOnlineOnly, value); }
        public bool XReadZReadOnly { get => _xReadZReadOnly; set => SetProperty(ref _xReadZReadOnly, value); }
        public bool MergeAllIntoOneFile { get => _mergeAllIntoOneFile; set => SetProperty(ref _mergeAllIntoOneFile, value); }

        public string SpecificCashier { get => _specificCashier; set => SetProperty(ref _specificCashier, value); }
        public string SpecificBagger { get => _specificBagger; set => SetProperty(ref _specificBagger, value); }
        public string FilterExactAmount { get => _filterExactAmount; set => SetProperty(ref _filterExactAmount, value); }
        public string FilterMemberName { get => _filterMemberName; set => SetProperty(ref _filterMemberName, value); }
        public string FilterCardLast4 { get => _filterCardLast4; set => SetProperty(ref _filterCardLast4, value); }
        public string FilterProductOrSku { get => _filterProductOrSku; set => SetProperty(ref _filterProductOrSku, value); }

        public bool GenerateScReport { get => _generateScReport; set => SetProperty(ref _generateScReport, value); }
        public bool GeneratePwdReport { get => _generatePwdReport; set => SetProperty(ref _generatePwdReport, value); }
        public int ReportChunkDays { get => _reportChunkDays; set => SetProperty(ref _reportChunkDays, value); }
        public string ReportUserId { get => _reportUserId; set => SetProperty(ref _reportUserId, value); } // NEW

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }
        public int ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand ProcessCommand { get; }

        public EJConsolidatorViewModel(IEJConsolidatorService service)
        {
            _service = service;
            ProcessCommand = new AsyncRelayCommand(ExecuteProcessAsync);

            var settings = SettingsViewModel.GetCurrentSettings();
            StoreCode = settings.DefaultStoreCode;
            LiveServerIp = settings.DefaultLiveServerIp;
        }

        private async Task ExecuteProcessAsync()
        {
            if (string.IsNullOrWhiteSpace(StoreCode)) { StatusMessage = "Store Code is required."; StatusColor = "#C0392B"; return; }
            if (IsModeTrxFinder && string.IsNullOrWhiteSpace(TargetTrxNumber)) { StatusMessage = "Transaction Number is required."; StatusColor = "#C0392B"; return; }

            IsBusy = true;
            ProgressValue = 0;
            StatusColor = "#3498DB";

            var dates = new List<DateTime>();
            for (DateTime dt = StartDate.Date; dt <= EndDate.Date; dt = dt.AddDays(1)) dates.Add(dt);

            var lanes = new List<string>();
            if (!string.IsNullOrWhiteSpace(PosLanes))
                lanes = PosLanes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();

            var options = new EJFilterOptions
            {
                StoreCode = StoreCode.Trim(),
                StoreName = StoreName?.Trim(), // NEW
                LiveServerIp = LiveServerIp?.Trim(),
                TargetDates = dates,
                PosLanes = lanes,
                IsModeConsolidator = IsModeConsolidator,
                IsModeTrxFinder = IsModeTrxFinder,
                TargetTrxNumber = TargetTrxNumber?.Trim(),
                SecondReceiptOnly = SecondReceiptOnly,
                GcashBarcodeOnly = GcashBarcodeOnly,
                HacsOnlineOnly = HacsOnlineOnly,
                XReadZReadOnly = XReadZReadOnly,
                MergeAllIntoOneFile = MergeAllIntoOneFile,
                SpecificCashier = SpecificCashier?.Trim(),
                SpecificBagger = SpecificBagger?.Trim(),
                FilterExactAmount = FilterExactAmount?.Trim(),
                FilterMemberName = FilterMemberName?.Trim(),
                FilterCardLast4 = FilterCardLast4?.Trim(),
                FilterProductOrSku = FilterProductOrSku?.Trim(),
                GenerateScReport = GenerateScReport,
                GeneratePwdReport = GeneratePwdReport,
                ReportChunkDays = ReportChunkDays <= 0 ? 3 : ReportChunkDays,
                ReportUserId = ReportUserId?.Trim() // NEW
            };

            try
            {
                var textProgress = new Progress<string>(msg => StatusMessage = msg);
                var pctProgress = new Progress<int>(pct => ProgressValue = pct);

                int totalFound = await _service.ProcessConsolidationAsync(options, textProgress, pctProgress);

                StatusMessage = $"Process Complete! Successfully saved {totalFound} records to Desktop/SOD_Output.";
                StatusColor = "#27AE60";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fatal Error: {ex.Message}";
                StatusColor = "#C0392B";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}