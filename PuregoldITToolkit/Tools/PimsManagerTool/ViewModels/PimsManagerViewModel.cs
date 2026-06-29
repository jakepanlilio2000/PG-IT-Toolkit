using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.PimsManagerTool.Interfaces;
using PuregoldITToolkit.Tools.PimsManagerTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.PimsManagerTool.ViewModels
{
    public class PimsManagerViewModel : ViewModelBase
    {
        private readonly IPimsRepository _repository;

        public ObservableCollection<VendorModel> VendorsList { get; } = new ObservableCollection<VendorModel>();

        private string _inputVendorCode;
        private string _inputVendorName;
        public string InputVendorCode { get => _inputVendorCode; set { SetProperty(ref _inputVendorCode, value); CheckIfEditMode(); } }
        public string InputVendorName { get => _inputVendorName; set => SetProperty(ref _inputVendorName, value); }

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set => SetProperty(ref _isEditMode, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private string _searchQuery;
        public string SearchQuery { get => _searchQuery; set => SetProperty(ref _searchQuery, value); }

        private VendorModel _selectedVendor;
        public VendorModel SelectedVendor
        {
            get => _selectedVendor;
            set
            {
                if (SetProperty(ref _selectedVendor, value) && value != null)
                {
                    InputVendorCode = value.VendorCd;
                    InputVendorName = value.Sort;
                }
            }
        }

        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;
        private int _totalItems = 0;

        public int CurrentPage { get => _currentPage; set => SetProperty(ref _currentPage, value); }
        public int PageSize { get => _pageSize; set => SetProperty(ref _pageSize, value); }
        public int TotalPages { get => _totalPages; set => SetProperty(ref _totalPages, value); }
        public int TotalItems { get => _totalItems; set => SetProperty(ref _totalItems, value); }

        public ICommand LoadCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }

        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand LastPageCommand { get; }

        public ICommand FixEmployeePromoCommand { get; }

        public PimsManagerViewModel(IPimsRepository repository)
        {
            _repository = repository;

            LoadCommand = new AsyncRelayCommand(() => { CurrentPage = 1; SearchQuery = string.Empty; return LoadVendorsAsync(); });
            SearchCommand = new AsyncRelayCommand(() => { CurrentPage = 1; return LoadVendorsAsync(); });
            SaveCommand = new AsyncRelayCommand(SaveVendorAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteVendorAsync);
            ClearCommand = new RelayCommand(ClearForm);

            NextPageCommand = new AsyncRelayCommand(async () => { if (CurrentPage < TotalPages) { CurrentPage++; await LoadVendorsAsync(); } });
            PrevPageCommand = new AsyncRelayCommand(async () => { if (CurrentPage > 1) { CurrentPage--; await LoadVendorsAsync(); } });
            FirstPageCommand = new AsyncRelayCommand(async () => { CurrentPage = 1; await LoadVendorsAsync(); });
            LastPageCommand = new AsyncRelayCommand(async () => { CurrentPage = TotalPages; await LoadVendorsAsync(); });

            FixEmployeePromoCommand = new AsyncRelayCommand(ExecuteFixEmployeePromoAsync);
        }

        private void ApplyCredentials()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            _repository.SetCredentials(settings.PimsSqlServer, settings.PimsSqlDatabase, settings.PimsSqlUsername, settings.PimsSqlPassword);
        }

        private void CheckIfEditMode()
        {
            foreach (var v in VendorsList)
            {
                if (v.VendorCd == InputVendorCode)
                {
                    IsEditMode = true; return;
                }
            }
            IsEditMode = false;
        }

        private async Task ExecuteFixEmployeePromoAsync()
        {
            IsBusy = true;
            ApplyCredentials();
            StatusMessage = "Executing FreeItemsDB Sale Promo script...";

            var result = await _repository.ResetEmployeeSalePromoAsync();

            if (result.Success)
                StatusMessage = "SUCCESS: Employee Sale Promo (8888888888) has been reset in FreeItemsDB.";
            else
                StatusMessage = result.ErrorMessage;

            IsBusy = false;
        }

        private async Task LoadVendorsAsync()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            if (string.IsNullOrWhiteSpace(settings.PimsSqlServer) || string.IsNullOrWhiteSpace(settings.PimsSqlDatabase) || string.IsNullOrWhiteSpace(settings.PimsSqlUsername))
            {
                StatusMessage = "Validation: PIMS Server, Database, and Username must be configured in Global Settings.";
                return;
            }

            IsBusy = true;
            ApplyCredentials();
            StatusMessage = $"Connecting to {settings.PimsSqlServer} and loading Page {CurrentPage}...";
            VendorsList.Clear();

            var result = await _repository.GetVendorsPagedAsync(CurrentPage, PageSize, SearchQuery);

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                StatusMessage = result.ErrorMessage;
                TotalItems = 0;
                TotalPages = 1;
                IsBusy = false;
                return;
            }

            TotalItems = result.TotalCount;
            TotalPages = TotalItems == 0 ? 1 : (int)Math.Ceiling((double)TotalItems / PageSize);

            foreach (var item in result.Vendors) VendorsList.Add(item);

            if (TotalItems == 0) StatusMessage = "No vendors found matching your criteria.";
            else StatusMessage = $"Showing {VendorsList.Count} items (Total: {TotalItems} vendors).";

            ClearForm();
            IsBusy = false;
        }

        private async Task SaveVendorAsync()
        {
            if (string.IsNullOrWhiteSpace(InputVendorCode) || string.IsNullOrWhiteSpace(InputVendorName))
            {
                StatusMessage = "Validation Error: Vendor Code and Name cannot be blank.";
                return;
            }

            IsBusy = true;
            ApplyCredentials();
            StatusMessage = IsEditMode ? "Updating vendor..." : "Adding new vendor...";

            var model = new VendorModel
            {
                VendorCd = InputVendorCode.Trim(),
                Vendor = $"{InputVendorCode.Trim()} -{InputVendorName.Trim()}",
                Sort = InputVendorName.Trim()
            };

            var result = IsEditMode
                ? await _repository.UpdateVendorAsync(model)
                : await _repository.InsertVendorAsync(model);

            if (result.Success)
            {
                StatusMessage = IsEditMode ? "Vendor updated successfully!" : "Vendor added successfully!";
                await LoadVendorsAsync();
            }
            else
            {
                StatusMessage = result.ErrorMessage;
                IsBusy = false;
            }
        }

        private async Task DeleteVendorAsync()
        {
            if (string.IsNullOrWhiteSpace(InputVendorCode)) return;

            IsBusy = true;
            ApplyCredentials();
            StatusMessage = "Deleting vendor...";

            var result = await _repository.DeleteVendorAsync(InputVendorCode.Trim());

            if (result.Success)
            {
                StatusMessage = "Vendor Deleted successfully!";
                await LoadVendorsAsync();
            }
            else
            {
                StatusMessage = result.ErrorMessage;
                IsBusy = false;
            }
        }

        private void ClearForm()
        {
            InputVendorCode = string.Empty;
            InputVendorName = string.Empty;
            SelectedVendor = null;
            IsEditMode = false;
        }
    }
}