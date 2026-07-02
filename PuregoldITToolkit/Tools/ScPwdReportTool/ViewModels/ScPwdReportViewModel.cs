using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.ScPwdReportTool.Interfaces;
using PuregoldITToolkit.Tools.ScPwdReportTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.ScPwdReportTool.ViewModels
{
    public class ScPwdReportViewModel : ViewModelBase
    {
        private readonly IScPwdReportService _service;
        private readonly string _cacheFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScPwdReportCache.json");

        private string _storeCode;
        private string _storeName;
        private string _liveServerIp;
        private string _posLanes;
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today;

        private bool _generateScReport = true;
        private bool _generatePwdReport = true;
        private int _reportChunkDays = 3;
        private string _reportUserId;

        private string _statusMessage = "Ready.";
        private string _statusColor = "#7F8FA6";
        private int _progressValue;
        private bool _isBusy;

        public string StoreCode { get => _storeCode; set => SetProperty(ref _storeCode, value); }
        public string StoreName { get => _storeName; set => SetProperty(ref _storeName, value); }
        public string LiveServerIp { get => _liveServerIp; set => SetProperty(ref _liveServerIp, value); }
        public string PosLanes { get => _posLanes; set => SetProperty(ref _posLanes, value); }
        public DateTime StartDate { get => _startDate; set { SetProperty(ref _startDate, value); if (EndDate < value) EndDate = value; } }
        public DateTime EndDate { get => _endDate; set { SetProperty(ref _endDate, value); if (StartDate > value) StartDate = value; } }

        public bool GenerateScReport { get => _generateScReport; set => SetProperty(ref _generateScReport, value); }
        public bool GeneratePwdReport { get => _generatePwdReport; set => SetProperty(ref _generatePwdReport, value); }
        public int ReportChunkDays { get => _reportChunkDays; set => SetProperty(ref _reportChunkDays, value); }
        public string ReportUserId { get => _reportUserId; set => SetProperty(ref _reportUserId, value); }

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }
        public int ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand ProcessCommand { get; }

        public ScPwdReportViewModel(IScPwdReportService service)
        {
            _service = service;
            ProcessCommand = new AsyncRelayCommand(ExecuteProcessAsync);
            LoadCache();
        }

        private async Task ExecuteProcessAsync()
        {
            if (!GenerateScReport && !GeneratePwdReport) { StatusMessage = "Please select at least one report type."; StatusColor = "#C0392B"; return; }

            IsBusy = true;
            ProgressValue = 0;
            StatusColor = "#3498DB";
            SaveCache();

            var dates = new List<DateTime>();
            for (DateTime dt = StartDate.Date; dt <= EndDate.Date; dt = dt.AddDays(1)) dates.Add(dt);

            var lanes = new List<string>();
            if (!string.IsNullOrWhiteSpace(PosLanes))
            {
                var splitLanes = PosLanes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var l in splitLanes)
                {
                    if (int.TryParse(l.Trim(), out int num)) lanes.Add(num.ToString());
                }
            }

            var options = new ScPwdReportOptions
            {
                StoreCode = StoreCode?.Trim(),
                StoreName = StoreName?.Trim(),
                LiveServerIp = LiveServerIp?.Trim(),
                TargetDates = dates,
                PosLanes = lanes,
                GenerateScReport = GenerateScReport,
                GeneratePwdReport = GeneratePwdReport,
                ReportChunkDays = ReportChunkDays <= 0 ? 3 : ReportChunkDays,
                ReportUserId = ReportUserId?.Trim()
            };

            try
            {
                var textProgress = new Progress<string>(msg => StatusMessage = msg);
                var pctProgress = new Progress<int>(pct => ProgressValue = pct);

                int recordsFound = await _service.GenerateReportsAsync(options, textProgress, pctProgress);
                StatusMessage = $"Success! Found {recordsFound} receipts. Saved to Desktop/SC_PWD_Reports.";
                StatusColor = "#27AE60";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed: {ex.Message}";
                StatusColor = "#C0392B";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LoadCache()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            StoreCode = settings.DefaultStoreCode;
            LiveServerIp = settings.DefaultLiveServerIp;
            StoreName = "PUREGOLD JR - SAN FERNANDO";
            ReportUserId = "87113";

            if (File.Exists(_cacheFile))
            {
                try
                {
                    var json = File.ReadAllText(_cacheFile);
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                    if (data.TryGetValue("StoreCode", out string sc)) StoreCode = sc;
                    if (data.TryGetValue("StoreName", out string sn)) StoreName = sn;
                    if (data.TryGetValue("LiveServerIp", out string ip)) LiveServerIp = ip;
                    if (data.TryGetValue("PosLanes", out string pl)) PosLanes = pl;
                    if (data.TryGetValue("UserId", out string ui)) ReportUserId = ui;
                    if (data.TryGetValue("ChunkDays", out string cd) && int.TryParse(cd, out int chunk)) ReportChunkDays = chunk;
                    if (data.TryGetValue("GenSc", out string scb) && bool.TryParse(scb, out bool gSc)) GenerateScReport = gSc;
                    if (data.TryGetValue("GenPwd", out string pwb) && bool.TryParse(pwb, out bool gPw)) GeneratePwdReport = gPw;
                }
                catch { }
            }
        }

        private void SaveCache()
        {
            try
            {
                var data = new Dictionary<string, string>
                {
                    ["StoreCode"] = StoreCode,
                    ["StoreName"] = StoreName,
                    ["LiveServerIp"] = LiveServerIp,
                    ["PosLanes"] = PosLanes,
                    ["UserId"] = ReportUserId,
                    ["ChunkDays"] = ReportChunkDays.ToString(),
                    ["GenSc"] = GenerateScReport.ToString(),
                    ["GenPwd"] = GeneratePwdReport.ToString()
                };
                File.WriteAllText(_cacheFile, JsonSerializer.Serialize(data));
            }
            catch { }
        }
    }
}