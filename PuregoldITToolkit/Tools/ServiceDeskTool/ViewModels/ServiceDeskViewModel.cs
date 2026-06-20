using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.ServiceDeskTool.Interfaces;
using PuregoldITToolkit.Tools.ServiceDeskTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels; // Imports the global settings
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.ServiceDeskTool.ViewModels
{
    public class ServiceDeskViewModel : ViewModelBase
    {
        private readonly IServiceDeskService _service;
        private readonly string _profilesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StoreProfilesDatabase.json");

        private DateTime _cutoffStart = new DateTime(2026, 5, 20);
        private DateTime _cutoffEnd = new DateTime(2026, 6, 4);

        public DateTime CutoffStart
        {
            get => _cutoffStart;
            set { if (SetProperty(ref _cutoffStart, value)) UpdatePreviews(); }
        }

        public DateTime CutoffEnd
        {
            get => _cutoffEnd;
            set { if (SetProperty(ref _cutoffEnd, value)) UpdatePreviews(); }
        }

        public ObservableCollection<OtEntryModel> OtEntries { get; } = new ObservableCollection<OtEntryModel>();
        public OutageEmailModel OutageData { get; } = new OutageEmailModel();
        public ObservableCollection<StoreProfileModel> StoreProfiles { get; } = new ObservableCollection<StoreProfileModel>();

        private StoreProfileModel _selectedProfile;
        public StoreProfileModel SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value) && value != null)
                {
                    OutageData.StoreCode = value.StoreCode;
                    OutageData.StoreName = value.StoreName;
                    OutageData.Isp = value.Isp;
                    OutageData.ModemSerial = value.ModemSerial;
                    OutageData.CidSid = value.CidSid;
                    if (!string.IsNullOrEmpty(value.Address)) OutageData.Address = value.Address;
                    if (!string.IsNullOrEmpty(value.ContactPerson)) OutageData.ContactPerson = value.ContactPerson;
                }
            }
        }

        // OT Previews
        public string OtPreviewTo => "jymendoza@puregold.com.ph";
        public string OtPreviewCc => "allITzone11@puregold.com.ph";
        public string OtPreviewSubject => $"Overtime Application Approval";

        private string _otPreviewBody;
        public string OtPreviewBody { get => _otPreviewBody; set => SetProperty(ref _otPreviewBody, value); }

        // Outage Previews
        public string OutagePreviewTo => OutageData.Isp == "GLOBE" ? "gowifihelpdesk@globe.com.ph, globedsgservicedesk@globe.com.ph" : "mnstech@convergeict.com, Mnssupport@convergeict.com, enterprisesupport@convergeict.com";
        public string OutagePreviewCc => $"{OutageData.AllItZoneEmail}, {OutageData.ZoneHeadName} <{OutageData.ZoneHeadEmail}>";
        public string OutagePreviewSubject => $"NETWORK DOWN ({OutageData.StoreCode} - {OutageData.StoreName}) ({OutageData.ModemSerial})";

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // Commands
        public ICommand AddOtRowCommand { get; }
        public ICommand RemoveOtRowCommand { get; }
        public ICommand ExportOtCommand { get; }
        public ICommand DraftEmailCommand { get; }
        public ICommand BrowseAttachmentCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand AddNewProfileCommand { get; }
        public ICommand ClearOutageFormCommand { get; }

        public ServiceDeskViewModel(IServiceDeskService service)
        {
            _service = service;

            AddOtRowCommand = new RelayCommand(AddRow);
            RemoveOtRowCommand = new RelayCommand<OtEntryModel>(RemoveRow);
            ExportOtCommand = new AsyncRelayCommand(ExportOtReportAsync);
            DraftEmailCommand = new AsyncRelayCommand(DraftEmailAsync);
            BrowseAttachmentCommand = new RelayCommand(BrowseAttachment);

            SaveProfileCommand = new RelayCommand(SaveStoreProfile);
            DeleteProfileCommand = new RelayCommand(DeleteStoreProfile);
            AddNewProfileCommand = new RelayCommand(AddNewProfile);
            ClearOutageFormCommand = new RelayCommand(ClearOutageForm);

            OtEntries.CollectionChanged += (s, e) => UpdatePreviews();
            OutageData.PropertyChanged += (s, e) => {
                OnPropertyChanged(nameof(OutagePreviewTo));
                OnPropertyChanged(nameof(OutagePreviewCc));
                OnPropertyChanged(nameof(OutagePreviewSubject));
            };

            LoadStoreDatabase();
            AddRow();
        }

        // *** FIX: Uses the new Global Settings JSON instead of the old TXT file ***
        private string GetGlobalSignature()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            return settings.SignatureHtml ?? string.Empty;
        }

        private void UpdatePreviews()
        {
            OnPropertyChanged(nameof(OtPreviewSubject));
            OtPreviewBody = $"Masayang araw,\n\nOT for cut-off {CutoffStart:MMMM d} TO {CutoffEnd:MMMM d}, {CutoffEnd.Year}\n\n[Excel Auto-Generated & Attached]";
        }

        private void LoadStoreDatabase()
        {
            StoreProfiles.Clear();
            if (File.Exists(_profilesFilePath))
            {
                try
                {
                    var savedProfiles = JsonSerializer.Deserialize<List<StoreProfileModel>>(File.ReadAllText(_profilesFilePath));
                    foreach (var p in savedProfiles) StoreProfiles.Add(p);
                    return;
                }
                catch { }
            }
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "144", StoreName = "APALIT", Isp = "CONVERGE" });
        }

        private void SaveProfilesToJson()
        {
            try { File.WriteAllText(_profilesFilePath, JsonSerializer.Serialize(StoreProfiles)); } catch { }
        }

        private void SaveStoreProfile()
        {
            if (string.IsNullOrWhiteSpace(OutageData.StoreCode))
            {
                StatusMessage = "Store Code is required to save a profile.";
                return;
            }

            var existing = StoreProfiles.FirstOrDefault(p => p.StoreCode == OutageData.StoreCode);
            if (existing != null)
            {
                existing.StoreName = OutageData.StoreName;
                existing.Isp = OutageData.Isp;
                existing.ModemSerial = OutageData.ModemSerial;
                existing.CidSid = OutageData.CidSid;
                existing.Address = OutageData.Address;
                existing.ContactPerson = OutageData.ContactPerson;
                StatusMessage = $"Profile for {OutageData.StoreCode} updated and saved!";
            }
            else
            {
                var newProfile = new StoreProfileModel
                {
                    StoreCode = OutageData.StoreCode,
                    StoreName = OutageData.StoreName,
                    Isp = OutageData.Isp,
                    ModemSerial = OutageData.ModemSerial,
                    CidSid = OutageData.CidSid,
                    Address = OutageData.Address,
                    ContactPerson = OutageData.ContactPerson
                };
                StoreProfiles.Add(newProfile);
                SelectedProfile = newProfile;
                StatusMessage = $"Profile for {OutageData.StoreCode} added!";
            }
            SaveProfilesToJson();
        }

        private void AddNewProfile()
        {
            ClearOutageForm();
            StatusMessage = "Ready for new profile input. Fill details and click SAVE.";
        }

        private void DeleteStoreProfile()
        {
            var existing = StoreProfiles.FirstOrDefault(p => p.StoreCode == OutageData.StoreCode);
            if (existing != null)
            {
                StoreProfiles.Remove(existing);
                SaveProfilesToJson();
                StatusMessage = $"Profile for {OutageData.StoreCode} deleted!";
                ClearOutageForm();
            }
        }

        private void ClearOutageForm()
        {
            SelectedProfile = null;
            OutageData.StoreCode = string.Empty; OutageData.StoreName = string.Empty; OutageData.Isp = "GLOBE";
            OutageData.ModemSerial = string.Empty; OutageData.CidSid = string.Empty; OutageData.Address = string.Empty;
            OutageData.ContactPerson = string.Empty; OutageData.AttachmentPath = string.Empty; OutageData.IssueDesc = string.Empty;
        }

        private void BrowseAttachment()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp", Title = "Select Modem Picture" };
            if (dlg.ShowDialog() == true) OutageData.AttachmentPath = dlg.FileName;
        }

        private void AddRow() { OtEntries.Add(new OtEntryModel()); UpdatePreviews(); }
        private void RemoveRow(OtEntryModel entry) { if (entry != null && OtEntries.Contains(entry)) { OtEntries.Remove(entry); UpdatePreviews(); } }

        private async Task ExportOtReportAsync()
        {
            if (OtEntries.Count == 0) return;
            IsBusy = true;
            StatusMessage = "Generating Excel & Drafting OT email template...";

            bool success = await _service.ExportOtReportAsync(OtEntries.ToList(), CutoffStart, CutoffEnd, GetGlobalSignature());

            StatusMessage = success ? "Success! OT Draft opened in Thunderbird." : "Failed to create email draft.";
            IsBusy = false;
        }

        private async Task DraftEmailAsync()
        {
            IsBusy = true;
            StatusMessage = "Drafting email template with attachment...";

            bool success = await _service.DraftOutageEmailAsync(OutageData, GetGlobalSignature());

            StatusMessage = success ? "Success! Outage Draft opened in Thunderbird." : "Failed to create email draft.";
            IsBusy = false;
        }
    }
}