using PuregoldITToolkit.Core.Base;
using System;

namespace PuregoldITToolkit.Tools.PureposTool.Models
{
    public class AutoPermissionModel : ViewModelBase
    {
        private string _liveServerIp = "192.92.92.50";
        private int _totalLanes = 6;
        private string _consoUser = "pgsanfernando722";
        private string _consoPassword = "pgsanfernando722";

        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public int TotalLanes { get => _totalLanes; set => SetProperty(ref _totalLanes, value); }
        public string ConsoUser { get => _consoUser; set => SetProperty(ref _consoUser, value); }
        public string ConsoPassword { get => _consoPassword; set => SetProperty(ref _consoPassword, value); }
    }

    public class EjSaveModel : ViewModelBase
    {
        private string _liveServerIp = "192.92.92.50";
        private string _storeCode = "722";
        private string _ftpServer = "192.168.200.177";
        private string _consoUser = "pgsanfernando722";
        private string _consoPassword = "pgsanfernando722";
        private DateTime _dateFrom = DateTime.Now.AddMonths(-1);
        private DateTime _dateTo = DateTime.Now.AddDays(-1);

        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public string StoreCode { get => _storeCode; set => SetProperty(ref _storeCode, value); }
        public string FtpServer { get => _ftpServer; set => SetProperty(ref _ftpServer, value); }
        public string ConsoUser { get => _consoUser; set => SetProperty(ref _consoUser, value); }
        public string ConsoPassword { get => _consoPassword; set => SetProperty(ref _consoPassword, value); }
        public DateTime DateFrom { get => _dateFrom; set => SetProperty(ref _dateFrom, value); }
        public DateTime DateTo { get => _dateTo; set => SetProperty(ref _dateTo, value); }
    }
}