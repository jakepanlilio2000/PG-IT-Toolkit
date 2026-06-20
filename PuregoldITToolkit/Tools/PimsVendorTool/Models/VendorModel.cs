using PuregoldITToolkit.Core.Base;

namespace PuregoldITToolkit.Tools.PimsVendorTool.Models
{
    public class VendorModel : ViewModelBase
    {
        private string _vendorCd;
        private string _vendor;
        private string _sort;

        public string VendorCd { get => _vendorCd; set => SetProperty(ref _vendorCd, value?.ToUpper()); }
        public string Vendor { get => _vendor; set => SetProperty(ref _vendor, value?.ToUpper()); }
        public string Sort { get => _sort; set => SetProperty(ref _sort, value?.ToUpper()); }
    }
}