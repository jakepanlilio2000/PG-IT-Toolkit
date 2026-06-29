using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.PimsManagerTool.ViewModels;

namespace PuregoldITToolkit.Tools.PimsManagerTool
{
    public class PimsManagerTool : ITool
    {
        public string ToolName => "PIMS Manager";
        public string Description => "Insert, Update, and Manage PGBIS Vendors.";
        public string IconResourceKey => "DatabaseIcon";

        public ViewModelBase ToolViewModel { get; }

        public PimsManagerTool(PimsManagerViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}