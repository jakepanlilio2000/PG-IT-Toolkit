using PuregoldITToolkit.Core.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace PuregoldITToolkit.Tools.SettingsTool.Models
{
    public class SettingsModel : ViewModelBase
    {
        // ==========================================
        // 1. Network & FTP Settings
        // ==========================================
        private string _ftpHost = "192.168.200.177";
        private int _ftpPort = 21;
        private string _ftpUsername = "ftp1@puregold";
        private string _ftpPassword = "pw@1234";
        private int _ftpTimeoutSeconds = 30;
        private bool _ftpPassiveMode = true;

        public string FtpHost
        {
            get => _ftpHost;
            set
            {
                if (SetProperty(ref _ftpHost, value))
                {
                    _defaultFtpServer = value;
                    OnPropertyChanged(nameof(DefaultFtpServer));
                }
            }
        }

        public int FtpPort
        {
            get => _ftpPort;
            set => SetProperty(ref _ftpPort, value);
        }

        public string FtpUsername
        {
            get => _ftpUsername;
            set => SetProperty(ref _ftpUsername, value);
        }

        public string FtpPassword
        {
            get => _ftpPassword;
            set => SetProperty(ref _ftpPassword, value);
        }

        public int FtpTimeoutSeconds
        {
            get => _ftpTimeoutSeconds;
            set => SetProperty(ref _ftpTimeoutSeconds, value);
        }

        public bool FtpPassiveMode
        {
            get => _ftpPassiveMode;
            set => SetProperty(ref _ftpPassiveMode, value);
        }

        // ==========================================
        // 2. Local POS & Store Configuration
        // ==========================================
        private string _defaultStoreCode = "722";
        private string _storeName = "Puregold San Fernando";
        private string _backOfficeRootDir = @"C:\PurePOS\BackOffice";
        private int _posLanesCount = 6;
        private string _posLaneNamingPattern = "POS{0:D2}";
        private string _ejInputDirectory = @"C:\PurePOS\EJ";
        private string _ejOutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SOD_Output");

        public string DefaultStoreCode
        {
            get => _defaultStoreCode;
            set
            {
                if (SetProperty(ref _defaultStoreCode, value))
                {
                    OnPropertyChanged(nameof(StoreCode));
                }
            }
        }

        public string StoreCode
        {
            get => _defaultStoreCode;
            set => DefaultStoreCode = value;
        }

        public string StoreName
        {
            get => _storeName;
            set => SetProperty(ref _storeName, value);
        }

        public string BackOfficeRootDir
        {
            get => _backOfficeRootDir;
            set => SetProperty(ref _backOfficeRootDir, value);
        }

        public int PosLanesCount
        {
            get => _posLanesCount;
            set => SetProperty(ref _posLanesCount, value);
        }

        public string PosLaneNamingPattern
        {
            get => _posLaneNamingPattern;
            set => SetProperty(ref _posLaneNamingPattern, value);
        }

        public string EjInputDirectory
        {
            get => _ejInputDirectory;
            set => SetProperty(ref _ejInputDirectory, value);
        }

        public string EjOutputDirectory
        {
            get => _ejOutputDirectory;
            set => SetProperty(ref _ejOutputDirectory, value);
        }

        // ==========================================
        // 3. INF Generator Defaults
        // ==========================================
        private string _infDefaultToEmails = "John Lee S. Caronan IT-HO <jscaronan@puregold.com.ph>, Jose Marie O. Gonzales IT-HO <jogonzales@puregold.com.ph>, Marc M. Del Rosario IT-HO <mmdelrosario@puregold.com.ph>, Felix Canada IT-PUREMART <felix.canada@puregold.intra>";
        private string _infDefaultCcEmails = "Winston C. Dahan IT-HO <wcdahan@puregold.com.ph>, Mon Tongco Jr IT-HO <mtongcojr@puregold.com.ph>";
        private string _infRegionZone = "Zone 11";


        public string InfDefaultToEmails
        {
            get => _infDefaultToEmails;
            set => SetProperty(ref _infDefaultToEmails, RemoveRetiredContact(value));
        }

        public string InfDefaultCcEmails
        {
            get => _infDefaultCcEmails;
            set => SetProperty(ref _infDefaultCcEmails, RemoveRetiredContact(value));
        }


        public string InfRegionZone
        {
            get => _infRegionZone;
            set => SetProperty(ref _infRegionZone, value);
        }

        // ==========================================
        // 4. System & Maintenance Settings
        // ==========================================
        private int _logRetentionDays = 30;

        public int LogRetentionDays
        {
            get => _logRetentionDays;
            set => SetProperty(ref _logRetentionDays, value);
        }

        // ==========================================
        // 5. Existing Legacy & Operational Properties (Backward Compatibility)
        // ==========================================
        private string _signatureHtml = string.Empty;
        private string _smtpServer = "192.168.200.153";
        private int _smtpPort = 587;
        private string _smtpUser = "email@puregold.intra";
        private string _smtpPass = "defaultpassword";
        private string _senderName = "IT-Biringan Crispin Dela Cruz";
        private string _thunderbirdSentPath = @"D:\Mail\Local Folders\Sent";

        private string _consoUser = "pgsanfernando722";
        private string _consoPassword = "pgsanfernando722";
        private string _defaultLiveServerIp = "192.92.92.50";
        private string _defaultFtpServer = "192.168.200.177";

        private string _posUsername = "cashier";
        private string _posPassword = "cashier";
        private string _outboundFtpIp = "192.168.200.84";
        private string _outboundFtpUser = "anahawftp";
        private string _outboundFtpPassword = "an@haw";

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
        public string DefaultFtpServer
        {
            get => _defaultFtpServer;
            set
            {
                if (SetProperty(ref _defaultFtpServer, value))
                {
                    _ftpHost = value;
                    OnPropertyChanged(nameof(FtpHost));
                }
            }
        }

        public string PosUsername { get => _posUsername; set => SetProperty(ref _posUsername, value); }
        public string PosPassword { get => _posPassword; set => SetProperty(ref _posPassword, value); }
        public string OutboundFtpIp { get => _outboundFtpIp; set => SetProperty(ref _outboundFtpIp, value); }
        public string OutboundFtpUser { get => _outboundFtpUser; set => SetProperty(ref _outboundFtpUser, value); }
        public string OutboundFtpPassword { get => _outboundFtpPassword; set => SetProperty(ref _outboundFtpPassword, value); }

        public string PimsSqlServer { get => _pimsSqlServer; set => SetProperty(ref _pimsSqlServer, value); }
        public string PimsSqlDatabase { get => _pimsSqlDatabase; set => SetProperty(ref _pimsSqlDatabase, value); }
        public string PimsSqlUsername { get => _pimsSqlUsername; set => SetProperty(ref _pimsSqlUsername, value); }
        public string PimsSqlPassword { get => _pimsSqlPassword; set => SetProperty(ref _pimsSqlPassword, value); }

        // ==========================================
        // 6. Strong Input Validation
        // ==========================================
        public static string RemoveRetiredContact(string contacts)
        {
            return Regex.Replace(contacts ?? string.Empty,
                @"(?:All IT-POS SYSTEM SUPPORT\s*)?(?:<)?allitpossystemsup@puregold\.com\.ph>?\s*[,;]?\s*", "", RegexOptions.IgnoreCase).Trim().Trim(',', ';');
        }

        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            // Store code
            if (string.IsNullOrWhiteSpace(StoreCode))
                errors.Add("Store Code cannot be empty.");

            // FTP Host
            if (string.IsNullOrWhiteSpace(FtpHost))
            {
                errors.Add("FTP Server Host cannot be empty.");
            }
            else
            {
                string host = FtpHost.Trim();
                bool isIp = IPAddress.TryParse(host, out _);
                bool isHostname = Uri.CheckHostName(host) != UriHostNameType.Unknown;
                if (!isIp && !isHostname)
                    errors.Add($"FTP Server Host '{host}' is not a valid IP address or hostname.");
            }

            // FTP Port
            if (FtpPort < 1 || FtpPort > 65535)
                errors.Add("FTP Port must be between 1 and 65535.");

            // Timeout
            if (FtpTimeoutSeconds < 1)
                errors.Add("Connection Timeout must be at least 1 second.");

            // Retention days
            if (LogRetentionDays < 1)
                errors.Add("Log Retention Days must be at least 1 day.");

            // Directory Paths
            ValidatePath(EjInputDirectory, "Default EJ Input Directory", errors, false);
            ValidatePath(EjOutputDirectory, "Processed Output Directory", errors, false);
            ValidatePath(BackOfficeRootDir, "Back-Office Root Directory", errors, false);

            // Emails
            ValidateEmailList(InfDefaultToEmails, "Default To Contact Emails", errors);

            return errors.Count == 0;
        }

        private static void ValidatePath(string path, string fieldName, List<string> errors, bool mustExist)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errors.Add($"{fieldName} cannot be empty.");
                return;
            }

            try
            {
                char[] invalidChars = Path.GetInvalidPathChars();
                if (path.IndexOfAny(invalidChars) >= 0)
                {
                    errors.Add($"{fieldName} contains invalid path characters.");
                    return;
                }

                if (mustExist && !Directory.Exists(path))
                {
                    errors.Add($"{fieldName} does not exist on disk: {path}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{fieldName} path is invalid: {ex.Message}");
            }
        }

        private static void ValidateEmailList(string emails, string fieldName, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(emails)) return;

            string[] list = emails.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in list)
            {
                string trimmed = item.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Match either Display Name <email@domain> or email@domain
                var match = Regex.Match(trimmed, @"^(?:.*<)?([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})(?:>)?$");
                if (!match.Success)
                {
                    errors.Add($"{fieldName} contains malformed email entry: '{trimmed}'.");
                }
            }
        }
    }
}
