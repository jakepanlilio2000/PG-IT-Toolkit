using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.SodChecker.Interfaces;
using PuregoldITToolkit.Tools.SodChecker.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.SodChecker.ViewModels
{
    public class SodCheckerViewModel : ViewModelBase
    {
        private readonly ISodCheckerService _sodService;

        // UI States
        private string _statusMessage = "Ready.";
        private int _scanProgress = 0;
        private bool _isBusy;
        private bool _isStoreManagerOpen;

        // Mode Toggles
        private bool _isModeDaily = true;
        private bool _isModeHistory = false;
        private bool _isModeTargeted = false;

        public bool IsModeDaily { get => _isModeDaily; set { if (SetProperty(ref _isModeDaily, value) && value) { IsModeHistory = false; IsModeTargeted = false; UpdateColumnVisibility(); RefreshDisplayGrid(); } } }
        public bool IsModeHistory { get => _isModeHistory; set { if (SetProperty(ref _isModeHistory, value) && value) { IsModeDaily = false; IsModeTargeted = false; UpdateColumnVisibility(); RefreshDisplayGrid(); } } }
        public bool IsModeTargeted { get => _isModeTargeted; set { if (SetProperty(ref _isModeTargeted, value) && value) { IsModeDaily = false; IsModeHistory = false; UpdateColumnVisibility(); RefreshDisplayGrid(); } } }

        // Parameters
        private DateTime _targetDate = DateTime.Now;
        private DateTime _startDate = DateTime.Now.AddDays(-7);
        private DateTime _endDate = DateTime.Now;
        private StoreConfig _selectedHistoryStore;
        private string _selectedColumn = "EJ";

        public DateTime TargetDate { get => _targetDate; set { if (SetProperty(ref _targetDate, value)) RefreshDisplayGrid(); } }
        public DateTime StartDate { get => _startDate; set { if (SetProperty(ref _startDate, value)) RefreshDisplayGrid(); } }
        public DateTime EndDate { get => _endDate; set { if (SetProperty(ref _endDate, value)) RefreshDisplayGrid(); } }
        public StoreConfig SelectedHistoryStore { get => _selectedHistoryStore; set { if (SetProperty(ref _selectedHistoryStore, value)) RefreshDisplayGrid(); } }
        public string SelectedColumn { get => _selectedColumn; set { if (SetProperty(ref _selectedColumn, value)) { UpdateColumnVisibility(); RefreshDisplayGrid(); } } }

        // --- NEW: Column Visibility Toggles ---
        public bool IsColEjVisible => !IsModeTargeted || SelectedColumn == "EJ";
        public bool IsColPollogVisible => !IsModeTargeted || SelectedColumn == "Pollog";
        public bool IsColCrmVisible => !IsModeTargeted || SelectedColumn == "CRM";
        public bool IsColPromoVisible => !IsModeTargeted || SelectedColumn == "Promo";
        public bool IsColBirVisible => !IsModeTargeted || SelectedColumn == "BIR";
        public bool IsColNonTradeVisible => !IsModeTargeted || SelectedColumn == "NonTrade";
        public bool IsColDosVisible => !IsModeTargeted || SelectedColumn == "DOS Log";
        public bool IsColMobPriceVisible => !IsModeTargeted || SelectedColumn == "Mob Price";
        public bool IsColPlusKuVisible => !IsModeTargeted || SelectedColumn == "PlusKu";
        public bool IsColRegPriceVisible => !IsModeTargeted || SelectedColumn == "Reg Price";
        public bool IsColSis98Visible => !IsModeTargeted || SelectedColumn == "SIS98";
        public bool IsColShelftagVisible => !IsModeTargeted || SelectedColumn == "Shelftag";
        public bool IsColKioskVisible => !IsModeTargeted || SelectedColumn == "Kiosk";

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public int ScanProgress { get => _scanProgress; set => SetProperty(ref _scanProgress, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public bool IsStoreManagerOpen { get => _isStoreManagerOpen; set => SetProperty(ref _isStoreManagerOpen, value); }

        // Data
        public StoreConfig NewStore { get; } = new StoreConfig();
        public ObservableCollection<StoreConfig> AvailableStores { get; } = new ObservableCollection<StoreConfig>();
        public ObservableCollection<SodStoreResult> StoreResults { get; } = new ObservableCollection<SodStoreResult>();
        public ObservableCollection<string> AvailableColumns { get; } = new ObservableCollection<string>
        { "EJ", "Pollog", "CRM", "Promo", "BIR", "NonTrade", "DOS Log", "Mob Price", "PlusKu", "Reg Price", "SIS98", "Shelftag", "Kiosk" };

        public ICommand ScanCommand { get; }
        public ICommand ToggleStoreManagerCommand { get; }
        public ICommand AddStoreCommand { get; }
        public ICommand DeleteStoreCommand { get; }

        public SodCheckerViewModel(ISodCheckerService sodService)
        {
            _sodService = sodService;
            ScanCommand = new AsyncRelayCommand(ExecuteScanAsync, () => !IsBusy);
            ToggleStoreManagerCommand = new RelayCommand(() => IsStoreManagerOpen = !IsStoreManagerOpen);
            AddStoreCommand = new AsyncRelayCommand(AddStoreAsync);
            DeleteStoreCommand = new AsyncRelayCommand<StoreConfig>(DeleteStoreAsync);

            _ = LoadStoresAsync();
        }

        private void UpdateColumnVisibility()
        {
            OnPropertyChanged(nameof(IsColEjVisible));
            OnPropertyChanged(nameof(IsColPollogVisible));
            OnPropertyChanged(nameof(IsColCrmVisible));
            OnPropertyChanged(nameof(IsColPromoVisible));
            OnPropertyChanged(nameof(IsColBirVisible));
            OnPropertyChanged(nameof(IsColNonTradeVisible));
            OnPropertyChanged(nameof(IsColDosVisible));
            OnPropertyChanged(nameof(IsColMobPriceVisible));
            OnPropertyChanged(nameof(IsColPlusKuVisible));
            OnPropertyChanged(nameof(IsColRegPriceVisible));
            OnPropertyChanged(nameof(IsColSis98Visible));
            OnPropertyChanged(nameof(IsColShelftagVisible));
            OnPropertyChanged(nameof(IsColKioskVisible));
        }

        private async void RefreshDisplayGrid()
        {
            StoreResults.Clear();
            try
            {
                if (IsModeDaily)
                {
                    foreach (var s in AvailableStores)
                        StoreResults.Add(new SodStoreResult { DisplayId = s.StoreCode, DisplayName = s.StoreName, TargetStoreCode = s.StoreCode, TargetDate = TargetDate });
                }
                else if (IsModeHistory && SelectedHistoryStore != null)
                {
                    if (StartDate > EndDate) return;
                    for (DateTime d = EndDate.Date; d >= StartDate.Date; d = d.AddDays(-1))
                    {
                        StoreResults.Add(new SodStoreResult { DisplayId = d.ToString("yyyy-MM-dd"), DisplayName = d.DayOfWeek.ToString(), TargetStoreCode = SelectedHistoryStore.StoreCode, TargetDate = d });
                    }
                }
                else if (IsModeTargeted)
                {
                    if (StartDate > EndDate) return;
                    for (DateTime d = EndDate.Date; d >= StartDate.Date; d = d.AddDays(-1))
                    {
                        foreach (var s in AvailableStores)
                        {
                            var result = new SodStoreResult { DisplayId = $"{s.StoreCode} ({d:MM/dd})", DisplayName = s.StoreName, TargetStoreCode = s.StoreCode, TargetDate = d };
                            SetInitialSkippedStates(result, SelectedColumn);
                            StoreResults.Add(result);
                        }
                    }
                }

                if (!StoreResults.Any()) return;

                StatusMessage = "Loading local cache records...";
                await _sodService.LoadCachedDataAsync(StoreResults);
                StatusMessage = "Loaded from Local Cache.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cache Error: {ex.Message}";
            }
        }

        private void SetInitialSkippedStates(SodStoreResult row, string col)
        {
            row.EjStatus.State = col == "EJ" ? ScanState.Missing : ScanState.Skipped;
            row.PollogStatus.State = col == "Pollog" ? ScanState.Missing : ScanState.Skipped;
            row.CrmStatus.State = col == "CRM" ? ScanState.Missing : ScanState.Skipped;
            row.PromoStatus.State = col == "Promo" ? ScanState.Missing : ScanState.Skipped;
            row.BirStatus.State = col == "BIR" ? ScanState.Missing : ScanState.Skipped;
            row.NonTradeStatus.State = col == "NonTrade" ? ScanState.Missing : ScanState.Skipped;
            row.DosStatus.State = col == "DOS Log" ? ScanState.Missing : ScanState.Skipped;
            row.MobilePriceStatus.State = col == "Mob Price" ? ScanState.Missing : ScanState.Skipped;
            row.PlusKuPriceStatus.State = col == "PlusKu" ? ScanState.Missing : ScanState.Skipped;
            row.RegPriceStatus.State = col == "Reg Price" ? ScanState.Missing : ScanState.Skipped;
            row.Sis98Status.State = col == "SIS98" ? ScanState.Missing : ScanState.Skipped;
            row.ShelftagStatus.State = col == "Shelftag" ? ScanState.Missing : ScanState.Skipped;
            row.KioskStatus.State = col == "Kiosk" ? ScanState.Missing : ScanState.Skipped;
        }

        private async Task LoadStoresAsync()
        {
            var stores = await _sodService.GetStoreListAsync();
            AvailableStores.Clear();
            foreach (var store in stores) AvailableStores.Add(store);
            if (AvailableStores.Any()) SelectedHistoryStore = AvailableStores.First();
            RefreshDisplayGrid();
        }

        private async Task AddStoreAsync()
        {
            if (string.IsNullOrWhiteSpace(NewStore.StoreCode) || string.IsNullOrWhiteSpace(NewStore.StoreName)) return;
            AvailableStores.Add(new StoreConfig { StoreCode = NewStore.StoreCode.Trim(), StoreName = NewStore.StoreName.Trim().ToUpper(), StoreType = NewStore.StoreType.Trim().ToUpper() });
            await _sodService.SaveStoresAsync(AvailableStores);
            NewStore.Clear();
        }

        private async Task DeleteStoreAsync(StoreConfig store)
        {
            if (store != null)
            {
                AvailableStores.Remove(store);
                await _sodService.SaveStoresAsync(AvailableStores);
            }
        }

        private async Task ExecuteScanAsync()
        {
            if (!StoreResults.Any()) return;

            IsBusy = true;
            ScanProgress = 0;
            ((AsyncRelayCommand)ScanCommand).NotifyCanExecuteChanged();

            try
            {
                string targetColToScan = IsModeTargeted ? SelectedColumn : "ALL";

                var textProgress = new Progress<string>(msg => StatusMessage = msg);
                var percentProgress = new Progress<int>(pct => ScanProgress = pct);

                await _sodService.ScanRowsAsync(StoreResults, targetColToScan, textProgress, percentProgress);
                StatusMessage = "Scan Complete! Local cache updated.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan Failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                ((AsyncRelayCommand)ScanCommand).NotifyCanExecuteChanged();
            }
        }
    }
}