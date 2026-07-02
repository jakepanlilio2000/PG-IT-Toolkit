using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.ViewModels;

namespace PuregoldITToolkit.Tools.EJConsolidator
{
    public class EJConsolidatorTool : ITool
    {
        public string ToolName => "EJ Consolidator";
        public string Description => "Extract, filter, and merge POS electronic journals.";
        public string IconResourceKey => "ReceiptIcon"; 

        public ViewModelBase ToolViewModel { get; }

        public EJConsolidatorTool(EJConsolidatorViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}