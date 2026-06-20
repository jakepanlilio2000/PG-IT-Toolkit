using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.PureposTool.Models
{
    public class EjSaveModel : ViewModelBase
    {
        private string _liveServerIp;
        private string _storeCode;
        private string _ftpServer;
        private DateTime _dateFrom = DateTime.Now.AddMonths(-1);
        private DateTime _dateTo = DateTime.Now.AddDays(-1);

        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public string StoreCode { get => _storeCode; set => SetProperty(ref _storeCode, value); }
        public string FtpServer { get => _ftpServer; set => SetProperty(ref _ftpServer, value); }
        public DateTime DateFrom { get => _dateFrom; set => SetProperty(ref _dateFrom, value); }
        public DateTime DateTo { get => _dateTo; set => SetProperty(ref _dateTo, value); }

        public EjSaveModel()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            LiveServerIp = settings.DefaultLiveServerIp;
            StoreCode = settings.DefaultStoreCode;
            FtpServer = settings.DefaultFtpServer;
        }
    }
}
