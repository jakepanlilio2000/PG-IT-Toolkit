using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FormsTool.Interfaces;
using PuregoldITToolkit.Tools.FormsTool.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
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
            SelectedInfType = InfTypes[0]; // Default selection

            AddToTableCommand = new RelayCommand(AddEntry);
            RemoveItemCommand = new RelayCommand<InfEntryModel>(RemoveEntry);
            ExportCommand = new AsyncRelayCommand(ExportDataAsync, () => InfTable.Count > 0);
        }

        private void EvaluatePromoLock()
        {
            if (IsPromoLocked)
            {
                CurrentEntry.IsPromo = "YES";
            }
            else if (SelectedInfType != null && SelectedInfType.Contains("Regular"))
            {
                CurrentEntry.IsPromo = "NO";
            }
            else
            {
                CurrentEntry.IsPromo = string.Empty;
            }
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
            EvaluatePromoLock(); // Reset promo field based on selected type
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
            StatusMessage = "Generating Word Document...";
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string folderPath = Path.Combine(desktopPath, "Puregold_Forms_Output");

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileName = $"INF_Form_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string fullPath = Path.Combine(folderPath, fileName);

            bool success = await _exportService.ExportInfToWordAsync(InfTable, SelectedInfType, fullPath);

            if (success)
                StatusMessage = "Success! Word document opened.";
            else
                StatusMessage = "Error generating Word file. Please try again.";
        }
    }
}