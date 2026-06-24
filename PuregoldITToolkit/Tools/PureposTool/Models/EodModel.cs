using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;

namespace PuregoldITToolkit.Tools.PureposTool.Models
{
    public class EodModel : ViewModelBase
    {
        private string _liveServerIp;
        private string _storeCode;
        private string _storeName = "Biringan";
        private string _storeManager = "Mr. Jose P. Rizal";
        private string _storeOfficer = "Mr. Juan Dela Cruz & Mr. Julian Felippe";
        private bool _useYesterday = false;
        private bool _testModeEmail = false;
        private bool _enableLivePreview = false;

        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public string StoreCode { get => _storeCode; set => SetProperty(ref _storeCode, value); }
        public string StoreName { get => _storeName; set => SetProperty(ref _storeName, value); }
        public string StoreManager { get => _storeManager; set => SetProperty(ref _storeManager, value); }
        public string StoreOfficer { get => _storeOfficer; set => SetProperty(ref _storeOfficer, value); }
        public bool UseYesterday { get => _useYesterday; set => SetProperty(ref _useYesterday, value); }
        public bool TestModeEmail { get => _testModeEmail; set => SetProperty(ref _testModeEmail, value); }
        public bool EnableLivePreview { get => _enableLivePreview; set => SetProperty(ref _enableLivePreview, value); }

        public EodModel()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            LiveServerIp = settings.DefaultLiveServerIp;
            StoreCode = settings.DefaultStoreCode;
        }
    }
}