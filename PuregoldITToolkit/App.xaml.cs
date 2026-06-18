using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.EJConsolidator;
using PuregoldITToolkit.Tools.EJConsolidator.Services;
using PuregoldITToolkit.Tools.EJConsolidator.ViewModels;
using PuregoldITToolkit.Tools.FormsTool;
using PuregoldITToolkit.Tools.FormsTool.Services;
using PuregoldITToolkit.Tools.FormsTool.ViewModels;
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

            // 2. Initialize EJ Consolidator Dependencies
            var receiptFilterService = new ReceiptFilterService();
            var ejConsolidatorService = new EJConsolidatorService(receiptFilterService);

            // 3. Create the Tool ViewModel & Tool Wrapper
            var ejViewModel = new EJConsolidatorViewModel(ejConsolidatorService);
            var ejTool = new EJConsolidatorTool(ejViewModel);

            // 4. Register the Tool into the Menu
            registryService.RegisterTool(ejTool);

            var sodService = new SodCheckerService();
            var sodViewModel = new SodCheckerViewModel(sodService);
            var sodTool = new SodCheckerTool(sodViewModel);

            // 1. Initialize Services for Forms
            var formsExportService = new FormsExportService();

            // 2. Initialize Sub-ViewModels
            var infVm = new InfViewModel(formsExportService);
            var obVm = new ObViewModel(formsExportService);
            var ssrfVm = new SsrfViewModel(formsExportService);
            var tsrfVm = new TsrfViewModel(formsExportService);

            // 3. Initialize Main Form ViewModel and Tool Wrapper
            var formsMainVm = new FormsMainViewModel(infVm, obVm, ssrfVm, tsrfVm);
            var formsTool = new FormsTool(formsMainVm);

            // 4. Register in Shell
            registryService.RegisterTool(formsTool);
            registryService.RegisterTool(sodTool);
            // 5. Initialize Shell and Show
            var mainViewModel = new MainViewModel(registryService);
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}