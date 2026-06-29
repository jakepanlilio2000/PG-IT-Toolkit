using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.EJConsolidator;
using PuregoldITToolkit.Tools.EJConsolidator.Services;
using PuregoldITToolkit.Tools.EJConsolidator.ViewModels;
using PuregoldITToolkit.Tools.FormsTool;
using PuregoldITToolkit.Tools.FormsTool.Services;
using PuregoldITToolkit.Tools.FormsTool.ViewModels;
using PuregoldITToolkit.Tools.PimsManagerTool; // Updated namespace
using PuregoldITToolkit.Tools.PimsManagerTool.Services;
using PuregoldITToolkit.Tools.PimsManagerTool.ViewModels;
using PuregoldITToolkit.Tools.PureposTool; // Adjusted to singular to match standard
using PuregoldITToolkit.Tools.PureposTool.Services;
using PuregoldITToolkit.Tools.PureposTool.ViewModels;
using PuregoldITToolkit.Tools.PureposTools;
using PuregoldITToolkit.Tools.ServiceDeskTool;
using PuregoldITToolkit.Tools.ServiceDeskTool.Services;
using PuregoldITToolkit.Tools.ServiceDeskTool.ViewModels;
using PuregoldITToolkit.Tools.SettingsTool;
using PuregoldITToolkit.Tools.SettingsTool.Services;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using PuregoldITToolkit.Tools.SodChecker;
using PuregoldITToolkit.Tools.SodChecker.Services;
using PuregoldITToolkit.Tools.SodChecker.ViewModels;
using PuregoldITToolkit.ViewModels;
using PuregoldITToolkit.Views;
using System.Windows;

namespace PuregoldITToolkit
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Initialize Core Services
            var registryService = new ToolRegistryService();

            // ==========================================
            // 2. INITIALIZE TOOLS & DEPENDENCIES
            // ==========================================

            // --- EJ Consolidator ---
            var receiptFilterService = new ReceiptFilterService();
            var ejConsolidatorService = new EJConsolidatorService(receiptFilterService);
            var ejViewModel = new EJConsolidatorViewModel(ejConsolidatorService);
            var ejTool = new EJConsolidatorTool(ejViewModel);

            // --- SOD Checker ---
            var sodService = new SodCheckerService();
            var sodViewModel = new SodCheckerViewModel(sodService);
            var sodTool = new SodCheckerTool(sodViewModel);

            // --- Forms Tool ---
            var formsExportService = new FormsExportService();
            var infVm = new InfViewModel(formsExportService);
            var obVm = new ObViewModel(formsExportService);
            var ssrfVm = new SsrfViewModel(formsExportService);
            var tsrfVm = new TsrfViewModel(formsExportService);
            var formsMainVm = new FormsMainViewModel(infVm, obVm, ssrfVm, tsrfVm);
            var formsTool = new FormsTool(formsMainVm);

            // --- PIMS Manager Tool (Updated naming) ---
            var pimsRepo = new PimsRepository();
            var pimsVm = new PimsManagerViewModel(pimsRepo);
            var pimsTool = new PimsManagerTool(pimsVm);

            // --- PurePOS Tool ---
            var pureposService = new PureposService();
            var pureposVm = new PureposViewModel(pureposService);
            var pureposTool = new PureposTool(pureposVm);

            // --- Service Desk Tool ---
            var sdService = new ServiceDeskService();
            var sdVm = new ServiceDeskViewModel(sdService);
            var sdTool = new ServiceDeskTool(sdVm);

            // --- Settings Tool ---
            var settingsService = new SettingsService();
            var settingsVm = new SettingsViewModel(settingsService);
            var settingsTool = new SettingsTool(settingsVm);

            // ==========================================
            // 3. REGISTER TOOLS TO THE SIDEBAR
            // ==========================================
            registryService.RegisterTool(ejTool);
            registryService.RegisterTool(sodTool);
            registryService.RegisterTool(formsTool);
            registryService.RegisterTool(pimsTool); // Registered the updated tool
            registryService.RegisterTool(pureposTool);
            registryService.RegisterTool(sdTool);
            registryService.RegisterTool(settingsTool);

            // ==========================================
            // 4. INITIALIZE SHELL AND SHOW
            // ==========================================
            var mainViewModel = new MainViewModel(registryService);
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}