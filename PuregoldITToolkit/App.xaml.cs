using PuregoldITToolkit.Services;
// --- Tool Namespaces ---
using PuregoldITToolkit.Tools.EJConsolidator;
using PuregoldITToolkit.Tools.EJConsolidator.Services;
using PuregoldITToolkit.Tools.EJConsolidator.ViewModels;
using PuregoldITToolkit.Tools.FormsTool;
using PuregoldITToolkit.Tools.FormsTool.Services;
using PuregoldITToolkit.Tools.FormsTool.ViewModels;
using PuregoldITToolkit.Tools.PimsVendorTool;
using PuregoldITToolkit.Tools.PimsVendorTool.Services;
using PuregoldITToolkit.Tools.PimsVendorTool.ViewModels;
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

            // --- PIMS Vendor Tool ---
            var vendorRepo = new VendorRepository();
            var vendorVm = new VendorManagerViewModel(vendorRepo);
            var vendorTool = new PimsVendorTool(vendorVm);

            // --- Purepos Tool ---
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
            registryService.RegisterTool(vendorTool);
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