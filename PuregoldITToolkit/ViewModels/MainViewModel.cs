using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using System.Collections.ObjectModel;

namespace PuregoldITToolkit.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IToolRegistryService _toolRegistry;
        private ITool _selectedTool;

        public ObservableCollection<ITool> AvailableTools { get; }

        public ITool SelectedTool
        {
            get => _selectedTool;
            set => SetProperty(ref _selectedTool, value);
        }

        public MainViewModel(IToolRegistryService toolRegistry)
        {
            _toolRegistry = toolRegistry;
            AvailableTools = new ObservableCollection<ITool>(_toolRegistry.GetAllTools());

            if (AvailableTools.Count > 0)
            {
                SelectedTool = AvailableTools[0]; 
            }
        }
    }
}