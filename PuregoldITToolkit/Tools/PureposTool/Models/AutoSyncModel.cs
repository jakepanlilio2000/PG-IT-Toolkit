using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;

namespace PuregoldITToolkit.Tools.PureposTool.Models
{
    public class AutoSyncModel : ViewModelBase
    {
        private string _storeCode;
        private string _liveServerIp;
        private DateTime _targetDate = DateTime.Today.AddDays(-1);
        private bool _debugMode = true;

        public string StoreCode { get => _storeCode; set => SetProperty(ref _storeCode, value); }
        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public DateTime TargetDate { get => _targetDate; set => SetProperty(ref _targetDate, value); }
        public bool DebugMode { get => _debugMode; set => SetProperty(ref _debugMode, value); }

        public AutoSyncModel()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            StoreCode = settings.DefaultStoreCode;
            LiveServerIp = settings.DefaultLiveServerIp;
        }
    }
}