using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.PimsVendorTool.ViewModels;

namespace PuregoldITToolkit.Tools.PimsVendorTool
{
    public class PimsVendorTool : ITool
    {
        public string ToolName => "PIMS Vendor Manager";
        public string Description => "Insert, Update, and Manage PGBIS Vendors.";
        public string IconResourceKey => "DatabaseIcon";

        public ViewModelBase ToolViewModel { get; }

        public PimsVendorTool(VendorManagerViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}