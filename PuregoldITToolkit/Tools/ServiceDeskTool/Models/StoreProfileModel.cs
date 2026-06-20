using PuregoldITToolkit.Core.Base;

namespace PuregoldITToolkit.Tools.ServiceDeskTool.Models
{
    public class StoreProfileModel : ViewModelBase
    {
        private string _storeCode;
        private string _storeName;
        private string _modemSerial;
        private string _cidSid;
        private string _isp;
        private string _address;
        private string _contactPerson;

        public string StoreCode { get => _storeCode; set { SetProperty(ref _storeCode, value); OnPropertyChanged(nameof(DisplayName)); } }
        public string StoreName { get => _storeName; set { SetProperty(ref _storeName, value); OnPropertyChanged(nameof(DisplayName)); } }
        public string ModemSerial { get => _modemSerial; set { SetProperty(ref _modemSerial, value); OnPropertyChanged(nameof(DisplayName)); } }
        public string Isp { get => _isp; set { SetProperty(ref _isp, value); OnPropertyChanged(nameof(DisplayName)); } }

        public string CidSid { get => _cidSid; set => SetProperty(ref _cidSid, value); }
        public string Address { get => _address; set => SetProperty(ref _address, value); }
        public string ContactPerson { get => _contactPerson; set => SetProperty(ref _contactPerson, value); }

        public string DisplayName => $"Store ({StoreCode} - {StoreName}) - {ModemSerial} - {Isp}";
    }
}