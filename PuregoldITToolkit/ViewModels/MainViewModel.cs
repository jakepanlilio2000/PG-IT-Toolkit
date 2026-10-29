using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.EJConsolidator.ViewModels;
using PuregoldITToolkit.Tools.FormsTool.ViewModels;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PuregoldITToolkit.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IToolRegistryService _toolRegistry;

        public DashboardViewModel DashboardVm { get; private set; }
        public EJConsolidatorViewModel EjVm { get; private set; }
        public InfViewModel InfVm { get; private set; }
        public SettingsViewModel SettingsVm { get; private set; }
        public SystemLogsViewModel LogsVm { get; private set; }
        public InfoHelpViewModel InfoHelpVm { get; private set; }

        public ObservableCollection<NavigationLink> NavigationLinks { get; } = new ObservableCollection<NavigationLink>();
        public string ActiveWorkflowTitle => NavigationLinks.FirstOrDefault(n => n.Key == ActiveWorkflowKey)?.Label ?? ActiveWorkflowKey;

        public ObservableCollection<ITool> UtilityTools { get; } = new ObservableCollection<ITool>();

        private object _currentContent;
        public object CurrentContent
        {
            get => _currentContent;
            set => SetProperty(ref _currentContent, value);
        }

        private string _activeWorkflowKey = "Dashboard";
        public string ActiveWorkflowKey
        {
            get => _activeWorkflowKey;
            set => SetProperty(ref _activeWorkflowKey, value);
        }

        private bool _isConsoleExpanded;
        public bool IsConsoleExpanded
        {
            get => _isConsoleExpanded;
            set => SetProperty(ref _isConsoleExpanded, value);
        }

        public LoggingService Logger => LoggingService.Instance;

        public ICommand NavigateWorkflowCommand { get; }
        public ICommand SelectUtilityToolCommand { get; }
        public ICommand ToggleConsoleCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand ClearLogsCommand { get; }

        public MainViewModel(
            IToolRegistryService toolRegistry,
            EJConsolidatorViewModel ejVm,
            InfViewModel infVm,
            SettingsViewModel settingsVm)
        {
            _toolRegistry = toolRegistry;
            EjVm = ejVm;
            InfVm = infVm;
            SettingsVm = settingsVm;
            LogsVm = new SystemLogsViewModel();
            InfoHelpVm = new InfoHelpViewModel();
            DashboardVm = new DashboardViewModel(NavigateTo);

            // Populate utilities that are not primary workflows
            var allTools = _toolRegistry.GetAllTools();
            foreach (var tool in allTools)
            {
                // EJConsolidator and Settings are primary workflows; other tools are in utilities
                if (tool.ToolName != "EJ Consolidator" && tool.ToolName != "Settings")
                {
                    UtilityTools.Add(tool);
                }
            }

            NavigateWorkflowCommand = new RelayCommand<string>(NavigateTo);
            SelectUtilityToolCommand = new RelayCommand<ITool>(SelectUtilityTool);
            ToggleConsoleCommand = new RelayCommand(() => IsConsoleExpanded = !IsConsoleExpanded);
            OpenLogFolderCommand = new RelayCommand(() => Logger.OpenLogFolder());
            ClearLogsCommand = new RelayCommand(() => Logger.Clear());

            NavigationLinks.Add(new NavigationLink("Dashboard", "Overview"));
            if (allTools.Any(t => t.ToolName == "EJ Consolidator")) NavigationLinks.Add(new NavigationLink("EJConsolidator", "EJ Consolidator"));
            if (allTools.Any(t => t.ToolName == "Puregold Forms"))
            {
                NavigationLinks.Add(new NavigationLink("Puregold Forms", "Forms"));
            }
            if (allTools.Any(t => t.ToolName == "Puregold Toolkits")) NavigationLinks.Add(new NavigationLink("Puregold Toolkits", "PurePOS"));
            NavigationLinks.Add(new NavigationLink("Settings", "Settings"));
            NavigationLinks.Add(new NavigationLink("SystemLogs", "Activity"));
            NavigationLinks.Add(new NavigationLink("InfoHelp", "Help"));

            // Start on Dashboard
            NavigateTo("Dashboard");
        }

        public void NavigateTo(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            if (key == "EJConsolidator" && !_toolRegistry.GetAllTools().Any(t => t.ToolName == "EJ Consolidator")) return;
            if (key == "INFGenerator" && !UtilityTools.Any(t => t.ToolName == "Puregold Forms")) return;
            ActiveWorkflowKey = key;
            foreach (var link in NavigationLinks) link.IsSelected = link.Key == key;
            OnPropertyChanged(nameof(ActiveWorkflowTitle));

            switch (key)
            {
                case "Dashboard":
                    DashboardVm.LoadSettingsData();
                    CurrentContent = DashboardVm;
                    break;
                case "EJConsolidator":
                    CurrentContent = EjVm;
                    break;
                case "INFGenerator":
                    NavigateTo("Puregold Forms");
                    if (CurrentContent is FormsMainViewModel forms) forms.IsInfSelected = true;
                    break;
                case "Settings":
                    CurrentContent = SettingsVm;
                    break;
                case "SystemLogs":
                    CurrentContent = LogsVm;
                    break;
                case "InfoHelp":
                    CurrentContent = InfoHelpVm;
                    break;
                default:
                    // Look for tool by name
                    var found = UtilityTools.FirstOrDefault(t => t.ToolName.Equals(key, System.StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        CurrentContent = found.ToolViewModel;
                    }
                    break;
            }
        }

        public void SelectUtilityTool(ITool tool)
        {
            if (tool != null)
            {
                NavigateTo(tool.ToolName);
            }
        }
    }

    public class NavigationLink : ViewModelBase
    {
        public string Key { get; }
        public string Label { get; }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
        public NavigationLink(string key, string label) { Key = key; Label = label; }
    }
}
