using PuregoldITToolkit.Core.Base;
using System;

namespace PuregoldITToolkit.Tools.FormsTool.Models
{
    public class ObModel : ViewModelBase
    {
        private string _employeeName;
        private string _position;
        private string _department;
        private DateTime? _dateFiled = DateTime.Now;
        private string _obSchedule;
        private string _employeeSignature;
        private string _signatureImagePath;

        // Auto-Capitalize as the user types
        public string EmployeeName { get => _employeeName; set => SetProperty(ref _employeeName, value?.ToUpper()); }
        public string Position { get => _position; set => SetProperty(ref _position, value?.ToUpper()); }

        public string Department { get => _department; set => SetProperty(ref _department, value); }
        public DateTime? DateFiled { get => _dateFiled; set => SetProperty(ref _dateFiled, value); }
        public string ObSchedule { get => _obSchedule; set => SetProperty(ref _obSchedule, value); }
        public string EmployeeSignature { get => _employeeSignature; set => SetProperty(ref _employeeSignature, value); }
        public string SignatureImagePath { get => _signatureImagePath; set => SetProperty(ref _signatureImagePath, value); }
    }
}