using PuregoldITToolkit.Core.Base;

namespace PuregoldITToolkit.Tools.SodChecker.Models
{
    public class StoreConfig : ViewModelBase
    {
        private string _storeCode;
        private string _storeName;
        private string _storeType = "PUREPOS"; 

        public string StoreCode
        {
            get => _storeCode;
            set => SetProperty(ref _storeCode, value);
        }

        public string StoreName
        {
            get => _storeName;
            set => SetProperty(ref _storeName, value);
        }

        public string StoreType
        {
            get => _storeType;
            set => SetProperty(ref _storeType, value);
        }

        public void Clear()
        {
            StoreCode = string.Empty;
            StoreName = string.Empty;
            StoreType = "PUREPOS";
        }
    }
}