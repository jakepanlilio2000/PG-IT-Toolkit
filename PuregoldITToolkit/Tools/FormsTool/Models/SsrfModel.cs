using PuregoldITToolkit.Core.Base;
using System;

namespace PuregoldITToolkit.Tools.FormsTool.Models
{
    public class SsrfModel : ViewModelBase
    {
        private string _location = "";
        private string _department = "";
        private DateTime? _dateFiled = DateTime.Now;
        private string _baseSsrfNumber = "";
        private int _printQuantity = 10;

        public string Location { get => _location; set => SetProperty(ref _location, value?.ToUpper()); }
        public string Department { get => _department; set => SetProperty(ref _department, value?.ToUpper()); }
        public DateTime? DateFiled { get => _dateFiled; set => SetProperty(ref _dateFiled, value); }
        public string BaseSsrfNumber { get => _baseSsrfNumber; set => SetProperty(ref _baseSsrfNumber, value?.ToUpper()); }

        // Settings
        public int PrintQuantity { get => _printQuantity; set => SetProperty(ref _printQuantity, value); }
    }
}