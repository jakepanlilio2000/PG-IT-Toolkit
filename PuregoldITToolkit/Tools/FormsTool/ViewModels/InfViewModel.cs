using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FormsTool.Interfaces;
using PuregoldITToolkit.Tools.FormsTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.FormsTool.ViewModels
{
    public class InfViewModel : ViewModelBase
    {
        private readonly IFormsExportService _exportService;

        public ObservableCollection<string> InfTypes { get; } = new ObservableCollection<string>
        {
            "1. PurePOS no price/Not Found (Promo)",
            "2. PurePOS no price/Not Found (Regular Item)",
            "3. Unmatch SKU/UPC",
            "4. Generated SKU/Barcode (APAR)"
        };

        private string _selectedInfType;
        public string SelectedInfType
        {
            get => _selectedInfType;
            set
            {
                if (SetProperty(ref _selectedInfType, value))
                {
                    OnPropertyChanged(nameof(IsType4Apar));
                    OnPropertyChanged(nameof(IsPromoLocked));
                    EvaluatePromoLock();
                }
            }
        }

        // HARDCODED ROUTING LISTS (Add all your recipients here)
        private string _emailTo = "user1@puregold.com.ph, user2@puregold.com.ph, user3@puregold.com.ph";
        public string EmailTo { get => _emailTo; set => SetProperty(ref _emailTo, value); }

        private string _emailCc = "cc1@puregold.com.ph, cc2@puregold.com.ph";
        public string EmailCc { get => _emailCc; set => SetProperty(ref _emailCc, value); }


        public bool IsType4Apar => SelectedInfType != null && SelectedInfType.Contains("APAR");
        public bool IsPromoLocked => SelectedInfType != null && SelectedInfType.Contains("(Promo)");

        public InfEntryModel CurrentEntry { get; } = new InfEntryModel();
        public ObservableCollection<InfEntryModel> InfTable { get; } = new ObservableCollection<InfEntryModel>();

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public ICommand AddToTableCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand ExportCommand { get; }

        public InfViewModel(IFormsExportService exportService)
        {
            _exportService = exportService;
            SelectedInfType = InfTypes[0];

            AddToTableCommand = new RelayCommand(AddEntry);
            RemoveItemCommand = new RelayCommand<InfEntryModel>(RemoveEntry);
            ExportCommand = new AsyncRelayCommand(ExportDataAsync, () => InfTable.Count > 0);
        }

        private void EvaluatePromoLock()
        {
            if (IsPromoLocked) CurrentEntry.IsPromo = "YES";
            else if (SelectedInfType != null && SelectedInfType.Contains("Regular")) CurrentEntry.IsPromo = "NO";
            else CurrentEntry.IsPromo = string.Empty;
        }

        private void AddEntry()
        {
            if (string.IsNullOrWhiteSpace(CurrentEntry.StoreCode) || string.IsNullOrWhiteSpace(CurrentEntry.Sku))
            {
                StatusMessage = "Error: Store Code and SKU are required.";
                return;
            }

            if (IsType4Apar && string.IsNullOrWhiteSpace(CurrentEntry.GeneratedSku))
            {
                StatusMessage = "Error: Generated SKU is required for APAR items.";
                return;
            }

            InfTable.Add(new InfEntryModel
            {
                StoreCode = CurrentEntry.StoreCode,
                Sku = CurrentEntry.Sku,
                GeneratedSku = CurrentEntry.GeneratedSku,
                Upc = CurrentEntry.Upc,
                PurePosPrice = CurrentEntry.PurePosPrice,
                MmsPrice = CurrentEntry.MmsPrice,
                Description = CurrentEntry.Description,
                IsPromo = CurrentEntry.IsPromo
            });

            CurrentEntry.Clear();
            EvaluatePromoLock();
            StatusMessage = "Item added to list.";
            ((AsyncRelayCommand)ExportCommand).NotifyCanExecuteChanged();
        }

        private void RemoveEntry(InfEntryModel item)
        {
            if (item != null)
            {
                InfTable.Remove(item);
                ((AsyncRelayCommand)ExportCommand).NotifyCanExecuteChanged();
            }
        }

        private async Task ExportDataAsync()
        {
            StatusMessage = "Drafting INF Email Template...";

            string sig = "";
            if (File.Exists(SettingsViewModel.SignatureFilePath))
                sig = File.ReadAllText(SettingsViewModel.SignatureFilePath);

            string storeCode = InfTable.FirstOrDefault()?.StoreCode ?? "UNKNOWN";

            bool success = await _exportService.ExportInfToEmailAsync(InfTable, SelectedInfType, storeCode, EmailTo, EmailCc, sig);

            StatusMessage = success ? "Success! Thunderbird draft opened." : "Error drafting email.";
        }
    }
}