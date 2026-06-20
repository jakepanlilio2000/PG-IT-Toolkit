using PuregoldITToolkit.Core.Base;

namespace PuregoldITToolkit.Tools.ServiceDeskTool.Models
{
    public class OutageEmailModel : ViewModelBase
    {
        private string _isp = "GLOBE";
        private string _storeCode;
        private string _storeName;
        private string _modemSerial;
        private string _cidSid = "N/A";
        private string _issueStatus = "DOWN";
        private string _issueDesc;
        private string _address;
        private string _contactPerson;
        private string _availability = "ANYTIME";
        private string _accessPass = "PRESENT VALID ID ONLY";

        private string _allItZoneEmail = "allITzone11@puregold.com.ph";
        private string _zoneHeadEmail = "jymendoza@puregold.com.ph";
        private string _zoneHeadName = "Jeffrey Y. Mendoza IT-ZONE 11 HEAD";
        private string _attachmentPath;

        // Triggers UI Preview update whenever a value changes
        public string Isp { get => _isp; set { if (SetProperty(ref _isp, value)) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string StoreCode { get => _storeCode; set { if (SetProperty(ref _storeCode, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string StoreName { get => _storeName; set { if (SetProperty(ref _storeName, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string ModemSerial { get => _modemSerial; set { if (SetProperty(ref _modemSerial, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string CidSid { get => _cidSid; set { if (SetProperty(ref _cidSid, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string IssueStatus { get => _issueStatus; set { if (SetProperty(ref _issueStatus, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string IssueDesc { get => _issueDesc; set { if (SetProperty(ref _issueDesc, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string Address { get => _address; set { if (SetProperty(ref _address, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string ContactPerson { get => _contactPerson; set { if (SetProperty(ref _contactPerson, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string Availability { get => _availability; set { if (SetProperty(ref _availability, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string AccessPass { get => _accessPass; set { if (SetProperty(ref _accessPass, value?.ToUpper())) OnPropertyChanged(nameof(GeneratedBody)); } }

        public string AllItZoneEmail { get => _allItZoneEmail; set => SetProperty(ref _allItZoneEmail, value); }
        public string ZoneHeadEmail { get => _zoneHeadEmail; set => SetProperty(ref _zoneHeadEmail, value); }
        public string ZoneHeadName { get => _zoneHeadName; set => SetProperty(ref _zoneHeadName, value); }

        public string AttachmentPath { get => _attachmentPath; set { if (SetProperty(ref _attachmentPath, value)) OnPropertyChanged(nameof(GeneratedBody)); } }
        public string GeneratedBody
        {
            get
            {
                string imgPlaceholder = string.IsNullOrEmpty(AttachmentPath) ? "[NO IMAGE ATTACHED YET]" : "[IMAGE ATTACHED]";

                return $@"Hi Service Desk,

Good day,

Please check the circuit of {StoreName} ({StoreCode}).

{Isp} BB MODEM SERIAL: {ModemSerial}
CID/SID / ACCT#: {CidSid}
ISSUE: {IssueStatus}
ISSUE DESCRIPTION: 
{IssueDesc}

Please do check the {Isp} Modem.

EXACT ADDRESS OF SITE: {Address}
CONTACT PERSON/NUMBER: {ContactPerson}
TIME OF AVAILABILITY / OPERATING HOURS: {Availability}
ACCESS PASS / WORK PERMIT NEEDED?: {AccessPass}

{imgPlaceholder}";
            }
        }
    }
}