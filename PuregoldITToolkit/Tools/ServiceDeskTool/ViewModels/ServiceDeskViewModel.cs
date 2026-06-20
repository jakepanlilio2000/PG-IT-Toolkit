using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.ServiceDeskTool.Interfaces;
using PuregoldITToolkit.Tools.ServiceDeskTool.Models;
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
            set => SetProperty(ref _cutoffStart, value);
        }

        public DateTime CutoffEnd
        {
            get => _cutoffEnd;
            set => SetProperty(ref _cutoffEnd, value);
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

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand AddOtRowCommand { get; }
        public ICommand RemoveOtRowCommand { get; }
        public ICommand ExportOtCommand { get; }
        public ICommand DraftEmailCommand { get; }
        public ICommand BrowseAttachmentCommand { get; }

        // Profile Management Commands
        public ICommand SaveProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
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
            ClearOutageFormCommand = new RelayCommand(ClearOutageForm);

            LoadStoreDatabase();
            AddRow();
        }

        private void LoadStoreDatabase()
        {
            StoreProfiles.Clear();

            // Try loading from saved JSON first
            if (File.Exists(_profilesFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_profilesFilePath);
                    var savedProfiles = JsonSerializer.Deserialize<List<StoreProfileModel>>(json);
                    foreach (var p in savedProfiles) StoreProfiles.Add(p);
                    return; // Exit if successful
                }
                catch { } // Fall through to default load if JSON is corrupted
            }

            // --- Default Load (Runs on very first launch) ---
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "144", StoreName = "APALIT", Isp = "CONVERGE", ModemSerial = "48575443F01C46A8", CidSid = "0030300229646", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "201", StoreName = "SUBIC", Isp = "CONVERGE", ModemSerial = "48575443CB977A8", CidSid = "0030300243147", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "202", StoreName = "CLARK", Isp = "CONVERGE", ModemSerial = "4857544314F7C0A9", CidSid = "0030300243151", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "232", StoreName = "SF2", Isp = "CONVERGE", ModemSerial = "48575443F03808A8", CidSid = "0030300230469", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "244", StoreName = "GUAGUA", Isp = "CONVERGE", ModemSerial = "48575443F03966A8", CidSid = "0030300229672", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "294", StoreName = "STO TOMAS", Isp = "CONVERGE", ModemSerial = "4857544354D9C7AA", CidSid = "0030300233905", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "313", StoreName = "MASANTOL", Isp = "CONVERGE", ModemSerial = "48575443566A3DA7", CidSid = "0030300229768", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "360", StoreName = "MEXICO", Isp = "CONVERGE", ModemSerial = "48575443ECBA5BA8", CidSid = "0030300229608", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "374", StoreName = "SINDALAN", Isp = "GLOBE", ModemSerial = "N/A", CidSid = "ACCT#918328770 SID#453064236", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "383", StoreName = "STA RITA", Isp = "GLOBE", ModemSerial = "N/A", CidSid = "ACCT#918331240 SID#453007597", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "418", StoreName = "BULAON", Isp = "GLOBE", ModemSerial = "N/A", CidSid = "ACCT#918328873 SID#453014135", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "438", StoreName = "MACABEBE", Isp = "CONVERGE", ModemSerial = "485754433C33FCA9", CidSid = "0030300233899", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "449", StoreName = "CALIBUTBUT", Isp = "CONVERGE", ModemSerial = "48575443F1F0CAA8", CidSid = "0030300242815", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "642", StoreName = "SAN SIMON", Isp = "CONVERGE", ModemSerial = "N/A", CidSid = "ACCT# 0030300236601", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "687", StoreName = "BACOLOR", Isp = "GLOBE", ModemSerial = "N/A", CidSid = "ACCT#1043251316 SID#452813627", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "697", StoreName = "SAN AGUSTIN", Isp = "CONVERGE", ModemSerial = "4857544355A2D0AF", CidSid = "30300266316", Address = "", ContactPerson = "" });
            StoreProfiles.Add(new StoreProfileModel { StoreCode = "722", StoreName = "SAN FERNANDO", Isp = "CONVERGE", ModemSerial = "N/A", CidSid = "0030300234119", Address = "", ContactPerson = "" });

            SaveProfilesToJson();
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
                // Update Existing
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
                // Add New
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
                StatusMessage = $"Profile for {OutageData.StoreCode} added!";

                // Set as currently selected
                SelectedProfile = newProfile;
            }
            SaveProfilesToJson();
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
            else
            {
                StatusMessage = "Profile not found to delete.";
            }
        }

        private void ClearOutageForm()
        {
            SelectedProfile = null;
            OutageData.StoreCode = string.Empty;
            OutageData.StoreName = string.Empty;
            OutageData.Isp = "GLOBE";
            OutageData.ModemSerial = string.Empty;
            OutageData.CidSid = string.Empty;
            OutageData.Address = string.Empty;
            OutageData.ContactPerson = string.Empty;
            OutageData.AttachmentPath = string.Empty;
            OutageData.IssueDesc = string.Empty;
            StatusMessage = "Form cleared.";
        }

        // --- Keep existing BrowseAttachment, AddRow, RemoveRow, ExportOtReportAsync, and DraftEmailAsync here ---
        private void BrowseAttachment()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp", Title = "Select Modem Picture" };
            if (dlg.ShowDialog() == true) OutageData.AttachmentPath = dlg.FileName;
        }

        private void AddRow()
        {
            OtEntries.Add(new OtEntryModel());
        }



        private void RemoveRow(OtEntryModel entry)
        {
            if (entry != null && OtEntries.Contains(entry))
            {
                OtEntries.Remove(entry);
            }
        }

        private async Task ExportOtReportAsync()
        {
            if (OtEntries.Count == 0) return;

            IsBusy = true;
            StatusMessage = "Generating Formatted OT Report...";

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string folderPath = Path.Combine(desktopPath, "Puregold_Forms_Output");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileName = $"OT_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string fullPath = Path.Combine(folderPath, fileName);

            bool success = await _service.ExportOtReportAsync(OtEntries.ToList(), fullPath, CutoffStart, CutoffEnd);

            StatusMessage = success ? "Success! Report Generated." : "Failed.";
            IsBusy = false;
        }

        private async Task DraftEmailAsync()
        {
            IsBusy = true;
            StatusMessage = "Drafting email template with attachment...";
            bool success = await _service.DraftOutageEmailAsync(OutageData);
            StatusMessage = success ? "Success! Draft opened in Thunderbird." : "Failed to create email draft.";
            IsBusy = false;
        }
    }
}