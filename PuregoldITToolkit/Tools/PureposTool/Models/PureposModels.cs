using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;

namespace PuregoldITToolkit.Tools.PureposTool.Models
{
    public class AutoPermissionModel : ViewModelBase
    {
        private string _liveServerIp;
        private int _totalLanes = 6;

        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public int TotalLanes { get => _totalLanes; set => SetProperty(ref _totalLanes, value); }

        public AutoPermissionModel()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            LiveServerIp = settings.DefaultLiveServerIp;
        }
    }
}