using PuregoldITToolkit.Core.Base;

namespace PuregoldITToolkit.Tools.SettingsTool.Models
{
    public class SettingsModel : ViewModelBase
    {
        // Email & Mailing
        private string _signatureHtml;
        private string _smtpServer = "192.168.200.153";
        private int _smtpPort = 587;
        private string _smtpUser = "jcpanlilio@puregold.intra";
        private string _smtpPass = "steep seldom limited";
        private string _senderName = "IT-SAN FERNANDO Jake Ashley C. Panlilio";
        private string _thunderbirdSentPath = @"D:\Jash Email\Mail\Local Folders\Sent";

        // PurePOS & Network Credentials
        private string _consoUser = "pgsanfernando722";
        private string _consoPassword = "pgsanfernando722";
        private string _defaultLiveServerIp = "192.92.92.50";
        private string _defaultFtpServer = "192.168.200.177";
        private string _defaultStoreCode = "722";

        // PIMS Database Credentials
        private string _pimsSqlServer = "192.168.200.50";
        private string _pimsSqlDatabase = "PGBIS";
        private string _pimsSqlUsername = "sa";
        private string _pimsSqlPassword = "sa";

        public string SignatureHtml { get => _signatureHtml; set => SetProperty(ref _signatureHtml, value); }
        public string SmtpServer { get => _smtpServer; set => SetProperty(ref _smtpServer, value); }
        public int SmtpPort { get => _smtpPort; set => SetProperty(ref _smtpPort, value); }
        public string SmtpUser { get => _smtpUser; set => SetProperty(ref _smtpUser, value); }
        public string SmtpPass { get => _smtpPass; set => SetProperty(ref _smtpPass, value); }
        public string SenderName { get => _senderName; set => SetProperty(ref _senderName, value); }
        public string ThunderbirdSentPath { get => _thunderbirdSentPath; set => SetProperty(ref _thunderbirdSentPath, value); }

        public string ConsoUser { get => _consoUser; set => SetProperty(ref _consoUser, value); }
        public string ConsoPassword { get => _consoPassword; set => SetProperty(ref _consoPassword, value); }
        public string DefaultLiveServerIp { get => _defaultLiveServerIp; set => SetProperty(ref _defaultLiveServerIp, value); }
        public string DefaultFtpServer { get => _defaultFtpServer; set => SetProperty(ref _defaultFtpServer, value); }
        public string DefaultStoreCode { get => _defaultStoreCode; set => SetProperty(ref _defaultStoreCode, value); }

        public string PimsSqlServer { get => _pimsSqlServer; set => SetProperty(ref _pimsSqlServer, value); }
        public string PimsSqlDatabase { get => _pimsSqlDatabase; set => SetProperty(ref _pimsSqlDatabase, value); }
        public string PimsSqlUsername { get => _pimsSqlUsername; set => SetProperty(ref _pimsSqlUsername, value); }
        public string PimsSqlPassword { get => _pimsSqlPassword; set => SetProperty(ref _pimsSqlPassword, value); }
    }
}