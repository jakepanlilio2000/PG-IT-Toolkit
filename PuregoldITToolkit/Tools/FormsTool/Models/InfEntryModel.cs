using PuregoldITToolkit.Core.Base;

using System.Collections.ObjectModel;

namespace PuregoldITToolkit.Tools.FormsTool.Models
{
    public class InfEntryModel : ViewModelBase
    {
        public ObservableCollection<InfCustomField> CustomFields { get; } = new ObservableCollection<InfCustomField>();
        private string _storeCode;
        private string _sku;
        private string _generatedSku;
        private string _upc;
        private string _purePosPrice;
        private string _mmsPrice;
        private string _description;
        private string _isPromo;

        public string StoreCode { get => _storeCode; set => SetProperty(ref _storeCode, value); }
        public string Sku { get => _sku; set => SetProperty(ref _sku, value); }
        public string GeneratedSku { get => _generatedSku; set => SetProperty(ref _generatedSku, value); }
        public string Upc { get => _upc; set => SetProperty(ref _upc, value); }
        public string PurePosPrice { get => _purePosPrice; set => SetProperty(ref _purePosPrice, value); }
        public string MmsPrice { get => _mmsPrice; set => SetProperty(ref _mmsPrice, value); }
        public string Description { get => _description; set => SetProperty(ref _description, value); }
        public string IsPromo { get => _isPromo; set => SetProperty(ref _isPromo, value); }

        public void Clear()
        {
            // Keep StoreCode intact for faster data entry on multiple items
            Sku = string.Empty;
            GeneratedSku = string.Empty;
            Upc = string.Empty;
            PurePosPrice = string.Empty;
            MmsPrice = string.Empty;
            Description = string.Empty;
            foreach (var field in CustomFields) field.Value = string.Empty;
            // IsPromo is managed by the ViewModel based on the Type
        }
    }

    public class InfCustomField : ViewModelBase
    {
        public string Name { get; set; }
        private string _value;
        public string Value { get => _value; set => SetProperty(ref _value, value); }
    }
}
