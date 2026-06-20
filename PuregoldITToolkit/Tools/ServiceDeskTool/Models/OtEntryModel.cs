using PuregoldITToolkit.Core.Base;
using System;

namespace PuregoldITToolkit.Tools.ServiceDeskTool.Models
{
    public class OtEntryModel : ViewModelBase
    {
        private string _employee;
        private string _store;
        private DateTime _dateOfOvertime = DateTime.Now;
        private int _hours;
        private int _minutes;
        private string _purpose;
        private string _preApprovedBy;

        public string Employee { get => _employee; set => SetProperty(ref _employee, value?.ToUpper()); }
        public string Store { get => _store; set => SetProperty(ref _store, value?.ToUpper()); }
        public DateTime DateOfOvertime { get => _dateOfOvertime; set => SetProperty(ref _dateOfOvertime, value); }

        // New Separate Fields
        public int Hours { get => _hours; set => SetProperty(ref _hours, value); }
        public int Minutes { get => _minutes; set => SetProperty(ref _minutes, value); }

        public string Purpose { get => _purpose; set => SetProperty(ref _purpose, value?.ToUpper()); }
        public string PreApprovedBy { get => _preApprovedBy; set => SetProperty(ref _preApprovedBy, value); }
    }
}