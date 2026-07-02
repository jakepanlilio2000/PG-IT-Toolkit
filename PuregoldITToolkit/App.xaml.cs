using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.EJConsolidator;
using PuregoldITToolkit.Tools.EJConsolidator.Services;
using PuregoldITToolkit.Tools.EJConsolidator.ViewModels;
using PuregoldITToolkit.Tools.FormsTool;
using PuregoldITToolkit.Tools.FormsTool.Services;
using PuregoldITToolkit.Tools.FormsTool.ViewModels;
using PuregoldITToolkit.Tools.PimsManagerTool;
using PuregoldITToolkit.Tools.PimsManagerTool.Services;
using PuregoldITToolkit.Tools.PimsManagerTool.ViewModels;
using PuregoldITToolkit.Tools.PureposTool.Services;
using PuregoldITToolkit.Tools.PureposTool.ViewModels;
using PuregoldITToolkit.Tools.PureposTools;
using PuregoldITToolkit.Tools.ScPwdReportTool;
using PuregoldITToolkit.Tools.ScPwdReportTool.Services;
using PuregoldITToolkit.Tools.ScPwdReportTool.ViewModels;
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
using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace PuregoldITToolkit
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Initialize the License Manager FIRST
            LicenseManager.Initialize();

            // 2. Initialize Core Services
            var registryService = new ToolRegistryService();

            // ==========================================
            // 3. SELECTIVE TOOL LOADING
            // ==========================================

            // --- 1. EJ Consolidator ---
            if (LicenseManager.IsEnabled("EJConsolidator"))
            {
                var receiptFilterService = new ReceiptFilterService();
                var ejConsolidatorService = new EJConsolidatorService(receiptFilterService);
                var ejViewModel = new EJConsolidatorViewModel(ejConsolidatorService);

                registryService.RegisterTool(new EJConsolidatorTool(ejViewModel));
            }

            // --- SC/PWD Reports ---
            if (LicenseManager.IsEnabled("ScPwdReports"))
            {
                var scPwdReportService = new ScPwdReportService();
                var scPwdViewModel = new ScPwdReportViewModel(scPwdReportService);
                registryService.RegisterTool(new ScPwdReportTool(scPwdViewModel));
            }

            // --- 2. SOD Checker ---
            if (LicenseManager.IsEnabled("SODChecker"))
            {
                var sodService = new SodCheckerService();
                var sodViewModel = new SodCheckerViewModel(sodService);
                registryService.RegisterTool(new SodCheckerTool(sodViewModel));
            }

            // --- 3. Puregold Forms ---
            if (LicenseManager.IsEnabled("PuregoldForms"))
            {
                var formsExportService = new FormsExportService();
                var infVm = new InfViewModel(formsExportService);
                var obVm = new ObViewModel(formsExportService);
                var ssrfVm = new SsrfViewModel(formsExportService);
                var tsrfVm = new TsrfViewModel(formsExportService);
                var formsMainVm = new FormsMainViewModel(infVm, obVm, ssrfVm, tsrfVm);
                registryService.RegisterTool(new FormsTool(formsMainVm));
            }

            // --- 4. PIMS Manager ---
            if (LicenseManager.IsEnabled("PIMSManager"))
            {
                var pimsRepo = new PimsRepository();
                var pimsVm = new PimsManagerViewModel(pimsRepo);
                registryService.RegisterTool(new PimsManagerTool(pimsVm));
            }

            // --- 5. Service Desk Utilities ---
            if (LicenseManager.IsEnabled("ServiceDeskUtilities"))
            {
                var sdService = new ServiceDeskService();
                var sdVm = new ServiceDeskViewModel(sdService);
                registryService.RegisterTool(new ServiceDeskTool(sdVm));
            }

            // --- Optional: Purepos & Settings (Add if you still need them) ---
            if (LicenseManager.IsEnabled("PureposTool"))
            {
                var pureposService = new PureposService();
                var pureposVm = new PureposViewModel(pureposService);
                registryService.RegisterTool(new PureposTool(pureposVm));
            }

                var settingsService = new SettingsService();
                var settingsVm = new SettingsViewModel(settingsService);
                registryService.RegisterTool(new SettingsTool(settingsVm));

            // ==========================================
            // 4. STARTUP MAIN WINDOW
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