using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.FormsTool.Interfaces;
using PuregoldITToolkit.Tools.FormsTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                    UpdatePreview();
                }
            }
        }

        private string _storeName;
        public string StoreName { get => _storeName; set { if (SetProperty(ref _storeName, value)) UpdatePreview(); } }

        // Distribution list properties
        private string _toAddresses = "John Lee S. Caronan IT-HO <jscaronan@puregold.com.ph>, Jose Marie O. Gonzales IT-HO <jogonzales@puregold.com.ph>, Marc M. Del Rosario IT-HO <mmdelrosario@puregold.com.ph>, Felix Canada IT-PUREMART <felix.canada@puregold.intra>";
        public string ToAddresses
        {
            get => _toAddresses;
            set { if (SetProperty(ref _toAddresses, PuregoldITToolkit.Tools.SettingsTool.Models.SettingsModel.RemoveRetiredContact(value))) UpdatePreview(); }
        }

        private string _ccAddresses = "Winston C. Dahan IT-HO <wcdahan@puregold.com.ph>, Mon Tongco Jr IT-HO <mtongcojr@puregold.com.ph>";
        public string CcAddresses
        {
            get => _ccAddresses;
            set { if (SetProperty(ref _ccAddresses, PuregoldITToolkit.Tools.SettingsTool.Models.SettingsModel.RemoveRetiredContact(value))) UpdatePreview(); }
        }

        private string _additionalCc;
        public string AdditionalCc { get => _additionalCc; set { if (SetProperty(ref _additionalCc, PuregoldITToolkit.Tools.SettingsTool.Models.SettingsModel.RemoveRetiredContact(value))) UpdatePreview(); } }

        public string FullCc => string.IsNullOrWhiteSpace(AdditionalCc)
            ? CcAddresses
            : (string.IsNullOrWhiteSpace(CcAddresses) ? AdditionalCc : $"{CcAddresses}, {AdditionalCc}");

        public bool IsType4Apar => SelectedInfType != null && SelectedInfType.Contains("APAR");
        public bool IsPromoLocked => SelectedInfType != null && SelectedInfType.Contains("(Promo)");

        public InfEntryModel CurrentEntry { get; } = new InfEntryModel();
        public ObservableCollection<InfEntryModel> InfTable { get; } = new ObservableCollection<InfEntryModel>();
        public ObservableCollection<string> ScreenshotPaths { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> CustomColumns { get; } = new ObservableCollection<string>();
        private string _newColumnName;
        public string NewColumnName { get => _newColumnName; set => SetProperty(ref _newColumnName, value); }
        public ICommand AddColumnCommand { get; }
        public ICommand RemoveColumnCommand { get; }

        // Preview Properties
        public string InfPreviewTo => ToAddresses;
        public string InfPreviewCc => FullCc;
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
            AddColumnCommand = new RelayCommand(AddColumn);
            RemoveColumnCommand = new RelayCommand<string>(RemoveColumn);
            SelectedInfType = InfTypes[0];

            AddToTableCommand = new RelayCommand(AddEntry);
            RemoveItemCommand = new RelayCommand<InfEntryModel>(RemoveEntry);
            AddScreenshotCommand = new RelayCommand(AddScreenshot);
            RemoveScreenshotCommand = new RelayCommand<string>(RemoveScreenshot);

            ExportCommand = new AsyncRelayCommand(ExportDataAsync, () => InfTable.Count > 0);

            // Populate defaults from centralized Settings
            var globalSettings = SettingsViewModel.GetCurrentSettings();
            if (!string.IsNullOrWhiteSpace(globalSettings.InfDefaultToEmails))
                ToAddresses = globalSettings.InfDefaultToEmails;
            if (!string.IsNullOrWhiteSpace(globalSettings.InfDefaultCcEmails))
                CcAddresses = globalSettings.InfDefaultCcEmails;
            if (!string.IsNullOrWhiteSpace(globalSettings.StoreName))
                StoreName = globalSettings.StoreName;
            if (!string.IsNullOrWhiteSpace(globalSettings.StoreCode))
                CurrentEntry.StoreCode = globalSettings.StoreCode;

            CurrentEntry.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InfEntryModel.StoreCode)) UpdatePreview();
            };

            InfTable.CollectionChanged += (s, e) =>
            {
                if (e.OldItems != null) foreach (InfEntryModel entry in e.OldItems) TrackEntry(entry, false);
                if (e.NewItems != null) foreach (InfEntryModel entry in e.NewItems) TrackEntry(entry, true);
                UpdatePreview();
                ((AsyncRelayCommand)ExportCommand).NotifyCanExecuteChanged();
            };

            ScreenshotPaths.CollectionChanged += (s, e) => UpdatePreview();
            UpdatePreview();
        }

        private void TrackEntry(InfEntryModel entry, bool subscribe)
        {
            if (subscribe) { entry.PropertyChanged += EntryChanged; entry.CustomFields.CollectionChanged += CustomFieldsChanged; }
            else { entry.PropertyChanged -= EntryChanged; entry.CustomFields.CollectionChanged -= CustomFieldsChanged; }
            foreach (var field in entry.CustomFields)
                if (subscribe) field.PropertyChanged += EntryChanged; else field.PropertyChanged -= EntryChanged;
        }
        private void EntryChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) { UpdatePreview(); }
        private void CustomFieldsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) foreach (InfCustomField field in e.OldItems) field.PropertyChanged -= EntryChanged;
            if (e.NewItems != null) foreach (InfCustomField field in e.NewItems) field.PropertyChanged += EntryChanged;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            string storeCode = InfTable.FirstOrDefault()?.StoreCode ?? CurrentEntry.StoreCode ?? "[StoreCode]";
            string storeName = string.IsNullOrWhiteSpace(StoreName) ? "[StoreName]" : StoreName.Trim();

            string cleanType = Regex.Replace(SelectedInfType ?? "", @"^\d+\.\s*", "");

            InfPreviewSubject = $"Item Not Found: {storeCode}-{storeName} Purepos";
            var body = new System.Text.StringBuilder();
            body.AppendLine($"Masayang araw po,\n\nMakikisuyo po sana INF Concern for {storeCode} - {storeName}");
            body.AppendLine($"\nConcern: {cleanType}\n");
            foreach (var entry in InfTable)
            {
                body.AppendLine($"SKU: {entry.Sku} | UPC: {entry.Upc} | {entry.Description}");
                body.AppendLine($"PurePOS: {entry.PurePosPrice} | MMS: {entry.MmsPrice} | Promo: {entry.IsPromo}");
                foreach (var field in entry.CustomFields) body.AppendLine($"{field.Name}: {field.Value}");
                body.AppendLine();
            }
            body.AppendLine($"{InfTable.Count} item(s) · {ScreenshotPaths.Count} supporting image(s)");
            InfPreviewBody = body.ToString();

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

            var entry = new InfEntryModel
            {
                StoreCode = CurrentEntry.StoreCode,
                Sku = CurrentEntry.Sku,
                GeneratedSku = CurrentEntry.GeneratedSku,
                Upc = CurrentEntry.Upc,
                PurePosPrice = CurrentEntry.PurePosPrice,
                MmsPrice = CurrentEntry.MmsPrice,
                Description = CurrentEntry.Description,
                IsPromo = CurrentEntry.IsPromo
            };
            foreach (var field in CurrentEntry.CustomFields)
                entry.CustomFields.Add(new InfCustomField { Name = field.Name, Value = field.Value });
            InfTable.Add(entry);

            CurrentEntry.Clear();
            EvaluatePromoLock();
            StatusMessage = $"Added item to list ({InfTable.Count} item(s) total).";
        }

        private void AddColumn()
        {
            string name = NewColumnName?.Trim();
            string[] standard = { "Store", "Store Code", "SKU", "Reg. SKU", "Generated SKU", "UPC", "PurePOS Price", "MMS Price", "Description", "Is Promo?", "Action" };
            if (string.IsNullOrWhiteSpace(name) || name.Length > 60 || name.Any(char.IsControl) ||
                CustomColumns.Concat(standard).Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = "Use a unique column name (1–60 characters).";
                return;
            }
            CurrentEntry.CustomFields.Add(new InfCustomField { Name = name });
            foreach (var entry in InfTable) entry.CustomFields.Add(new InfCustomField { Name = name });
            CustomColumns.Add(name);
            NewColumnName = string.Empty;
            StatusMessage = $"Added column: {name}. It will be included in the email.";
            UpdatePreview();
        }

        private void RemoveColumn(string name)
        {
            int index = CustomColumns.IndexOf(name);
            if (index < 0) return;
            CurrentEntry.CustomFields.RemoveAt(index);
            foreach (var entry in InfTable) entry.CustomFields.RemoveAt(index);
            CustomColumns.RemoveAt(index);
            StatusMessage = $"Removed column: {name}.";
            UpdatePreview();
        }

        private void RemoveEntry(InfEntryModel item)
        {
            if (item != null)
            {
                InfTable.Remove(item);
                StatusMessage = $"Removed item. ({InfTable.Count} remaining).";
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

            string storeCode = InfTable.FirstOrDefault()?.StoreCode ?? CurrentEntry.StoreCode ?? "UNKNOWN";
            string storeName = string.IsNullOrWhiteSpace(StoreName) ? "UNKNOWN" : StoreName.Trim();

            bool success = await _exportService.ExportInfToEmailAsync(InfTable, SelectedInfType, storeCode, storeName, InfPreviewTo, InfPreviewCc, sig, ScreenshotPaths);

            if (success)
            {
                StatusMessage = "Success! Email draft generated and opened in default email client.";
                LoggingService.Instance.LogSuccess($"Drafted INF email for Store {storeCode} ({InfTable.Count} items)");
            }
            else
            {
                StatusMessage = "Error drafting INF email.";
                LoggingService.Instance.LogError($"Failed to draft INF email for Store {storeCode}");
            }
        }

    }
}
