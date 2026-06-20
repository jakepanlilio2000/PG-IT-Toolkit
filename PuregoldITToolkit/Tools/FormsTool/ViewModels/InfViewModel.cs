using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FormsTool.Interfaces;
using PuregoldITToolkit.Tools.FormsTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels; // To access global settings helper
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.FormsTool.ViewModels
{
    public class InfViewModel : ViewModelBase
    {
        private readonly IFormsExportService _exportService;

        private readonly string DefaultToAddresses = "John Lee S. Caronan IT-HO <jscaronan@puregold.com.ph>, Jose Marie O. Gonzales IT-HO <jogonzales@puregold.com.ph>, Marc M. Del Rosario IT-HO <mmdelrosario@puregold.com.ph>, Felix Canada IT-PUREMART <felix.canada@puregold.intra>";
        private readonly string DefaultCcAddresses = "Winston C. Dahan IT-HO <wcdahan@puregold.com.ph>, Mon Tongco Jr IT-HO <mtongcojr@puregold.com.ph>";

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
                    UpdatePreview();
                }
            }
        }

        private string _storeName;
        public string StoreName { get => _storeName; set { if (SetProperty(ref _storeName, value)) UpdatePreview(); } }

        private string _additionalCc;
        public string AdditionalCc { get => _additionalCc; set { if (SetProperty(ref _additionalCc, value)) UpdatePreview(); } }

        public bool IsType4Apar => SelectedInfType != null && SelectedInfType.Contains("APAR");
        public bool IsPromoLocked => SelectedInfType != null && SelectedInfType.Contains("(Promo)");

        public InfEntryModel CurrentEntry { get; } = new InfEntryModel();
        public ObservableCollection<InfEntryModel> InfTable { get; } = new ObservableCollection<InfEntryModel>();
        public ObservableCollection<string> ScreenshotPaths { get; } = new ObservableCollection<string>();

        // Preview Properties
        public string InfPreviewTo => DefaultToAddresses;
        public string InfPreviewCc => string.IsNullOrWhiteSpace(AdditionalCc) ? DefaultCcAddresses : $"{DefaultCcAddresses}, {AdditionalCc}";
        public string InfPreviewSubject { get; private set; }
        public string InfPreviewBody { get; private set; }

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public ICommand AddToTableCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand AddScreenshotCommand { get; }
        public ICommand RemoveScreenshotCommand { get; }
        public ICommand ExportCommand { get; }

        public InfViewModel(IFormsExportService exportService)
        {
            _exportService = exportService;
            SelectedInfType = InfTypes[0];

            AddToTableCommand = new RelayCommand(AddEntry);
            RemoveItemCommand = new RelayCommand<InfEntryModel>(RemoveEntry);

            AddScreenshotCommand = new RelayCommand(AddScreenshot);
            RemoveScreenshotCommand = new RelayCommand<string>(RemoveScreenshot);

            ExportCommand = new AsyncRelayCommand(ExportDataAsync, () => InfTable.Count > 0);

            CurrentEntry.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InfEntryModel.StoreCode)) UpdatePreview();
            };

            InfTable.CollectionChanged += (s, e) => UpdatePreview();
            ScreenshotPaths.CollectionChanged += (s, e) => UpdatePreview();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            string storeCode = InfTable.FirstOrDefault()?.StoreCode ?? CurrentEntry.StoreCode ?? "[StoreCode]";
            string storeName = string.IsNullOrWhiteSpace(StoreName) ? "[StoreName]" : StoreName.Trim();

            string cleanType = Regex.Replace(SelectedInfType ?? "", @"^\d+\.\s*", "");

            InfPreviewSubject = $"Item Not Found: {storeCode}-{storeName} Purepos";
            InfPreviewBody = $"Masayang araw po,\n\nMakikisuyo po sana INF Concern for {storeCode} - {storeName}\n\nConcern (UNMATCH / INF):  {cleanType}\n\n[Plain HTML Table Auto-Generated Here]\n\n[ {ScreenshotPaths.Count} Image(s) Attached Inline (Width 500) ]\n\n[HTML Signature attached internally]";

            OnPropertyChanged(nameof(InfPreviewTo));
            OnPropertyChanged(nameof(InfPreviewCc));
            OnPropertyChanged(nameof(InfPreviewSubject));
            OnPropertyChanged(nameof(InfPreviewBody));
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

        private void AddScreenshot()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Supporting Screenshots"
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames) ScreenshotPaths.Add(file);
            }
        }

        private void RemoveScreenshot(string path)
        {
            if (ScreenshotPaths.Contains(path)) ScreenshotPaths.Remove(path);
        }

        private async Task ExportDataAsync()
        {
            StatusMessage = "Drafting INF Email Template...";

            var globalSettings = SettingsViewModel.GetCurrentSettings();
            string sig = globalSettings.SignatureHtml ?? string.Empty;

            string storeCode = InfTable.FirstOrDefault()?.StoreCode ?? "UNKNOWN";
            string storeName = string.IsNullOrWhiteSpace(StoreName) ? "UNKNOWN" : StoreName.Trim();

            bool success = await _exportService.ExportInfToEmailAsync(InfTable, SelectedInfType, storeCode, storeName, InfPreviewTo, InfPreviewCc, sig, ScreenshotPaths);

            StatusMessage = success ? "Success! Thunderbird draft opened." : "Error drafting email.";
        }
    }
}