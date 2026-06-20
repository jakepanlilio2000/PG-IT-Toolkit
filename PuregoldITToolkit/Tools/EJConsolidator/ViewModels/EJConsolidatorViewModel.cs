using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.EJConsolidator.ViewModels
{
    public class EJConsolidatorViewModel : ViewModelBase
    {
        private readonly IEJConsolidatorService _consolidatorService;

        // Modes
        private bool _isModeConsolidator = true;
        private bool _isModeTrxFinder = false;

        public bool IsModeConsolidator { get => _isModeConsolidator; set { if (SetProperty(ref _isModeConsolidator, value) && value) IsModeTrxFinder = false; } }
        public bool IsModeTrxFinder { get => _isModeTrxFinder; set { if (SetProperty(ref _isModeTrxFinder, value) && value) IsModeConsolidator = false; } }

        private string _storeCode;
        private string _targetTrxNumber;
        private string _posLanes;
        private string _liveServerIp = "192.92.92.50";
        private int _totalPosCount = 6;
        private DateTime _startDate = DateTime.Now.AddDays(-2); 
        private DateTime _endDate = DateTime.Now;
        private bool _mergeAllIntoOneFile;

        private string _specificCashier;
        private string _specificBagger;
        private bool _secondReceiptOnly;
        private bool _gcashBarcodeOnly;
        private bool _hacsOnlineOnly;
        private bool _xReadZReadOnly;
        private string _statusMessage = "Ready.";
        private string _statusColor = "#7F8FA6";
        private int _progressValue = 0;
        private bool _isBusy;

        public string StoreCode { get => _storeCode; set => SetProperty(ref _storeCode, value); }
        public string TargetTrxNumber { get => _targetTrxNumber; set => SetProperty(ref _targetTrxNumber, value); }
        public string PosLanes { get => _posLanes; set => SetProperty(ref _posLanes, value); }
        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public int TotalPosCount { get => _totalPosCount; set => SetProperty(ref _totalPosCount, value); }
        public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }
        public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }
        public bool MergeAllIntoOneFile { get => _mergeAllIntoOneFile; set => SetProperty(ref _mergeAllIntoOneFile, value); }

        public string SpecificCashier { get => _specificCashier; set => SetProperty(ref _specificCashier, value); }
        public string SpecificBagger { get => _specificBagger; set => SetProperty(ref _specificBagger, value); }
        public bool SecondReceiptOnly { get => _secondReceiptOnly; set => SetProperty(ref _secondReceiptOnly, value); }
        public bool GcashBarcodeOnly { get => _gcashBarcodeOnly; set => SetProperty(ref _gcashBarcodeOnly, value); }
        public bool HacsOnlineOnly { get => _hacsOnlineOnly; set => SetProperty(ref _hacsOnlineOnly, value); }
        public bool XReadZReadOnly { get => _xReadZReadOnly; set => SetProperty(ref _xReadZReadOnly, value); }

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }
        public int ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand ProcessCommand { get; }

        public EJConsolidatorViewModel(IEJConsolidatorService consolidatorService)
        {
            _consolidatorService = consolidatorService;
            ProcessCommand = new RelayCommand(async () => await ExecuteProcessAsync(), () => !IsBusy);
        }

        private async Task ExecuteProcessAsync()
        {
            if (string.IsNullOrWhiteSpace(StoreCode))
            {
                SetStatus("Validation Error: Store Code cannot be empty.", "#C41E3A");
                return;
            }
            if (!Regex.IsMatch(StoreCode.Trim(), @"^\d+$"))
            {
                SetStatus("Validation Error: Store Code must contain only numbers.", "#C41E3A");
                return;
            }

            // 2. Live Server IP Validation
            if (string.IsNullOrWhiteSpace(LiveServerIp))
            {
                SetStatus("Validation Error: Live Server IP cannot be empty.", "#C41E3A");
                return;
            }
            if (!IPAddress.TryParse(LiveServerIp.Trim(), out _))
            {
                SetStatus("Validation Error: Live Server IP is not a valid IPv4 address.", "#C41E3A");
                return;
            }

            // 3. POS Count Validation
            if (TotalPosCount <= 0)
            {
                SetStatus("Validation Error: Total POS Lanes must be greater than 0.", "#C41E3A");
                return;
            }

            // 4. Date Validation
            if (StartDate.Date > EndDate.Date)
            {
                SetStatus("Validation Error: Start Date cannot be later than End Date.", "#C41E3A");
                return;
            }
            if (EndDate.Date > DateTime.Now.Date)
            {
                SetStatus("Validation Error: Cannot process future dates.", "#C41E3A");
                return;
            }

            // 5. Target Lanes Format Validation (Allows numbers, commas, and spaces)
            if (!string.IsNullOrWhiteSpace(PosLanes))
            {
                if (!Regex.IsMatch(PosLanes, @"^[\d\s,]+$"))
                {
                    SetStatus("Validation Error: Target Lanes must be numbers separated by commas (e.g. 1, 3, 10).", "#C41E3A");
                    return;
                }
            }

            // 6. Mode-Specific Validations
            if (IsModeTrxFinder && string.IsNullOrWhiteSpace(TargetTrxNumber))
            {
                SetStatus("Validation Error: Transaction Number is required in Finder Mode.", "#C41E3A");
                return;
            }

            // --- END VALIDATION ---

            IsBusy = true;
            ProgressValue = 0;
            SetStatus("Initializing Offline Cache & Extractor...", "#2980B9");

            try
            {
                var options = new EJFilterOptions
                {
                    IsModeTrxFinder = this.IsModeTrxFinder,
                    TargetTrxNumber = this.TargetTrxNumber?.Trim(),
                    StoreCode = this.StoreCode.Trim(),
                    LiveServerIp = this.LiveServerIp.Trim(),
                    TotalPosCount = this.TotalPosCount,
                    MergeAllIntoOneFile = this.MergeAllIntoOneFile,
                    SpecificCashier = this.SpecificCashier?.Trim(),
                    SpecificBagger = this.SpecificBagger?.Trim(),
                    SecondReceiptOnly = this.SecondReceiptOnly,
                    GcashBarcodeOnly = this.GcashBarcodeOnly,
                    HacsOnlineOnly = this.HacsOnlineOnly,
                    XReadZReadOnly = this.XReadZReadOnly
                };

                for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1)) options.TargetDates.Add(d);

                if (!string.IsNullOrWhiteSpace(PosLanes))
                {
                    foreach (var lane in PosLanes.Split(','))
                    {
                        if (!string.IsNullOrWhiteSpace(lane)) options.PosLanes.Add(lane.Trim());
                    }
                }

                var textProgress = new Progress<string>(msg => SetStatus(msg, "#2980B9"));
                var pctProgress = new Progress<int>(pct => ProgressValue = pct);

                int resultCount = await _consolidatorService.ProcessConsolidationAsync(options, textProgress, pctProgress);

                if (resultCount > 0)
                {
                    SetStatus($"Process Completed! {resultCount} matching blocks saved to Desktop\\SOD_Output.", "#27AE60");
                }
                else
                {
                    SetStatus("Process finished, but NO files were downloaded or NO receipts matched your filters.", "#E67E22");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Critical Error: {ex.Message}", "#C41E3A");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetStatus(string message, string hexColor)
        {
            StatusMessage = message;
            StatusColor = hexColor;
        }
    }
}