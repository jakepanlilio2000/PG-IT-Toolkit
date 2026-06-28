using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.IO;
using System.Text.Json;

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

            // Load saved entries when the tool opens
            LoadCache();
        }

        public void SaveCache()
        {
            try
            {
                var cacheFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EodInputCache.json");
                var data = new { StoreName, StoreManager, StoreOfficer };
                File.WriteAllText(cacheFile, JsonSerializer.Serialize(data));
            }
            catch { /* Silently fail if there's a permission issue writing the file */ }
        }

        private void LoadCache()
        {
            var cacheFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EodInputCache.json");
            if (File.Exists(cacheFile))
            {
                try
                {
                    var json = File.ReadAllText(cacheFile);
                    var data = JsonSerializer.Deserialize<JsonElement>(json);

                    if (data.TryGetProperty("StoreName", out var sn)) StoreName = sn.GetString();
                    if (data.TryGetProperty("StoreManager", out var sm)) StoreManager = sm.GetString();
                    if (data.TryGetProperty("StoreOfficer", out var so)) StoreOfficer = so.GetString();
                }
                catch { /* Fallback to default values if JSON is corrupted */ }
            }
        }
    }
}