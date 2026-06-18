using PuregoldITToolkit.Core.Base;
using System;

namespace PuregoldITToolkit.Tools.FormsTool.Models
{
    public class TsrfModel : ViewModelBase
    {
        private string _company = "PUREGOLD PRICE CLUB INC.";
        private string _others = "722- SAN FERNANDO JR";
        private string _requestingDept;
        private DateTime? _dateFiled = DateTime.Now;
        private string _tsrfNumber;

        private string _assetDescription;
        private string _problemDetails;
        private string _actionTaken;
        private string _remarks;

        private string _requesterName;
        private string _requesterPosition;

        // --- NEW: Performed By ---
        private string _performedByName;
        private string _performedByPosition;

        // --- UPDATED: Signatories & Positions ---
        private string _notedByDeptHead1;
        private string _notedByDeptHead1Pos;
        private string _notedByDeptHead2;
        private string _notedByDeptHead2Pos;

        private string _notedByMis1;
        private string _notedByMis1Pos;
        private string _notedByMis2;
        private string _notedByMis2Pos;

        public string Company { get => _company; set => SetProperty(ref _company, value?.ToUpper()); }
        public string Others { get => _others; set => SetProperty(ref _others, value?.ToUpper()); }
        public string RequestingDept { get => _requestingDept; set => SetProperty(ref _requestingDept, value?.ToUpper()); }
        public DateTime? DateFiled { get => _dateFiled; set { if (SetProperty(ref _dateFiled, value)) OnPropertyChanged(); } }
        public string TsrfNumber { get => _tsrfNumber; set => SetProperty(ref _tsrfNumber, value?.ToUpper()); }

        public string AssetDescription { get => _assetDescription; set => SetProperty(ref _assetDescription, value); }
        public string ProblemDetails { get => _problemDetails; set => SetProperty(ref _problemDetails, value); }
        public string ActionTaken { get => _actionTaken; set => SetProperty(ref _actionTaken, value); }
        public string Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }

        public string RequesterName { get => _requesterName; set => SetProperty(ref _requesterName, value?.ToUpper()); }
        public string RequesterPosition { get => _requesterPosition; set => SetProperty(ref _requesterPosition, value?.ToUpper()); }

        public string PerformedByName { get => _performedByName; set => SetProperty(ref _performedByName, value?.ToUpper()); }
        public string PerformedByPosition { get => _performedByPosition; set => SetProperty(ref _performedByPosition, value?.ToUpper()); }

        public string NotedByDeptHead1 { get => _notedByDeptHead1; set => SetProperty(ref _notedByDeptHead1, value?.ToUpper()); }
        public string NotedByDeptHead1Pos { get => _notedByDeptHead1Pos; set => SetProperty(ref _notedByDeptHead1Pos, value?.ToUpper()); }
        public string NotedByDeptHead2 { get => _notedByDeptHead2; set => SetProperty(ref _notedByDeptHead2, value?.ToUpper()); }
        public string NotedByDeptHead2Pos { get => _notedByDeptHead2Pos; set => SetProperty(ref _notedByDeptHead2Pos, value?.ToUpper()); }

        public string NotedByMis1 { get => _notedByMis1; set => SetProperty(ref _notedByMis1, value?.ToUpper()); }
        public string NotedByMis1Pos { get => _notedByMis1Pos; set => SetProperty(ref _notedByMis1Pos, value?.ToUpper()); }
        public string NotedByMis2 { get => _notedByMis2; set => SetProperty(ref _notedByMis2, value?.ToUpper()); }
        public string NotedByMis2Pos { get => _notedByMis2Pos; set => SetProperty(ref _notedByMis2Pos, value?.ToUpper()); }
    }
}