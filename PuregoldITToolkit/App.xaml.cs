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
using System.Net;
using System.IO;
using System.Text.Json;
using System.Windows;
using PuregoldITToolkit.Tools.FtpBrowserTool;
using PuregoldITToolkit.Tools.FtpBrowserTool.Services;
using PuregoldITToolkit.Tools.FtpBrowserTool.ViewModels;

namespace PuregoldITToolkit
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Explicitly enable TLS 1.2, TLS 1.1, and TLS for legacy OS (Windows 7 SP1) network/FTP compatibility
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            // 1. Initialize the License Manager FIRST
            LicenseManager.Initialize();

            // 2. Initialize Centralized Settings & Logging
            var settingsService = new SettingsService();
            var settings = SettingsService.LoadSettingsInternal();
            LoggingService.Instance.ApplyRetentionPolicy(settings.LogRetentionDays);
            LoggingService.Instance.LogInfo($"Puregold IT Toolkit started. Environment: .NET Framework {Environment.Version}, OS: {Environment.OSVersion}, Platform: x86");

            // 3. Initialize Core Services
            var registryService = new ToolRegistryService();

            // ==========================================
            // 3. SELECTIVE TOOL LOADING & VIEWMODEL CREATION
            // ==========================================

            // --- 1. EJ Consolidator ---
            var receiptFilterService = new ReceiptFilterService();
            var ejConsolidatorService = new EJConsolidatorService(receiptFilterService);
            var ejViewModel = new EJConsolidatorViewModel(ejConsolidatorService);
            if (LicenseManager.IsEnabled("EJConsolidator"))
            {
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

            // --- 3. Puregold Forms & INF Generator ---
            var formsExportService = new FormsExportService();
            var formTemplateService = new FormTemplateService();
            var infVm = new InfViewModel(formsExportService);
            var obVm = new ObViewModel(formsExportService);
            var ssrfVm = new SsrfViewModel(formsExportService);
            var tsrfVm = new TsrfViewModel(formsExportService);
            var formCustomizerVm = new FormCustomizerViewModel(formTemplateService);
            var formsMainVm = new FormsMainViewModel(infVm, obVm, ssrfVm, tsrfVm, formCustomizerVm);
            if (LicenseManager.IsEnabled("PuregoldForms"))
            {
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

            // --- Optional: Purepos & FTP Browser ---
            if (LicenseManager.IsEnabled("PureposTool"))
            {
                var pureposService = new PureposService();
                var diagService = new ToolkitDiagnosticsService();
                var pureposVm = new PureposViewModel(pureposService, diagService);
                registryService.RegisterTool(new PureposTool(pureposVm));
            }

            if (LicenseManager.IsEnabled("FtpBrowserTool"))
            {
                var ftpService = new FtpBrowserService();
                var ftpVm = new FtpBrowserViewModel(ftpService);
                var ftpBrowserTool = new FtpBrowserTool(ftpVm);
                registryService.RegisterTool(ftpBrowserTool);
            }

            // --- Universal Settings ---
            var settingsVm = new SettingsViewModel(settingsService);
            registryService.RegisterTool(new SettingsTool(settingsVm));

            // ==========================================
            // 4. STARTUP MAIN WINDOW
            // ==========================================
            var mainViewModel = new MainViewModel(registryService, ejViewModel, infVm, settingsVm);
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}
