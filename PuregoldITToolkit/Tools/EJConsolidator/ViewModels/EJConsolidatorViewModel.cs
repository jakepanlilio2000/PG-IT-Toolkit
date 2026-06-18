using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using System;
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

        // Settings
        private string _storeCode;
        private string _targetTrxNumber;
        private string _posLanes;
        private string _liveServerIp = "192.92.92.50";
        private int _totalPosCount = 6;
        private DateTime _startDate = DateTime.Now.AddDays(-2); // Default to 2 days ago for EJ
        private DateTime _endDate = DateTime.Now;
        private bool _mergeAllIntoOneFile;

        // Filters
        private string _specificCashier;
        private string _specificBagger;
        private bool _secondReceiptOnly;
        private bool _gcashBarcodeOnly;
        private bool _hacsOnlineOnly;
        private bool _xReadZReadOnly;

        // Status
        private string _statusMessage = "Ready.";
        private string _statusColor = "#7F8FA6";
        private int _progressValue = 0;
        private bool _isBusy;

        // Accessors
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
            if (string.IsNullOrWhiteSpace(StoreCode) || !Regex.IsMatch(StoreCode.Trim(), @"^\d+$"))
            {
                SetStatus("Validation Failed: Store Code must be a valid numeric value.", "#E74C3C");
                return;
            }

            if (IsModeTrxFinder && string.IsNullOrWhiteSpace(TargetTrxNumber))
            {
                SetStatus("Validation Failed: Transaction Number is required in Finder Mode.", "#E74C3C");
                return;
            }

            if (StartDate.Date > EndDate.Date)
            {
                SetStatus("Validation Failed: Start Date cannot be later than End Date.", "#E74C3C");
                return;
            }

            if (!string.IsNullOrWhiteSpace(PosLanes) && !Regex.IsMatch(PosLanes.Replace(",", ""), @"^\d+$"))
            {
                SetStatus("Validation Failed: POS Lanes must be numbers separated by commas.", "#E74C3C");
                return;
            }

            IsBusy = true;
            ProgressValue = 0;
            SetStatus("Initializing Offline Cache & Extractor...", "#0097E6");

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

                var textProgress = new Progress<string>(msg => SetStatus(msg, "#0097E6"));
                var pctProgress = new Progress<int>(pct => ProgressValue = pct);

                int resultCount = await _consolidatorService.ProcessConsolidationAsync(options, textProgress, pctProgress);

                if (resultCount > 0)
                {
                    SetStatus($"Process Completed! {resultCount} matching blocks saved to Desktop\\SOD_Output.", "#44BD32");
                }
                else
                {
                    SetStatus("Process finished, but NO files were downloaded or NO receipts matched your filters.", "#F39C12");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Critical Error: {ex.Message}", "#E74C3C");
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