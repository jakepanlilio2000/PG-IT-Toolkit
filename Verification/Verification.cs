using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using PuregoldITToolkit.Tools.EJConsolidator.Services;
using PuregoldITToolkit.Tools.EJConsolidator.ViewModels;
using PuregoldITToolkit.Tools.FormsTool.Models;
using PuregoldITToolkit.Tools.FormsTool.Services;
using PuregoldITToolkit.Tools.FormsTool.ViewModels;
using PuregoldITToolkit.Tools.PureposTool.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.Models;
using PuregoldITToolkit.Tools.PureposTool.ViewModels;
using PuregoldITToolkit.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class Verification
{
    private static int _checks;
    private static readonly string Root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", Guid.NewGuid().ToString("N"));
    private static string TestImagePath;
    private const string Receipt = "Puregold Price Club, Inc.\nINVOICE#: 722-001-000123\n10/20/2026 14:32:10\nCashier: MARIA\nBagger: JUAN\nSKU 999999 MILK\nSubtotal 80.00\nTotal Amt Due: PHP 100.00\nCash 200.00\nMember Card Number: **** **** 7654\nMember Name: ANA REYES\nOSCA ID No: SC-101\n";
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--write-defaults")
        {
            File.WriteAllText(args[1], PuregoldITToolkit.Tools.SettingsTool.Services.SettingsService.CreateDefaultConfiguration(), Encoding.UTF8);
            return 0;
        }
        Directory.CreateDirectory(Root);
        var app = new App();
        app.DispatcherUnhandledException += (sender, e) => { Console.Error.WriteLine(e.Exception); e.Handled = true; Environment.ExitCode = 1; Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); };
        app.InitializeComponent();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                TestFilters();
                TestSplitting();
                TestForms();
                await TestConsolidation();
                await TestSourceRouting();
                await TestAutomaticLogin();
                await TestCleanup();
                await TestViewModels();
                await TestViews();
                Console.WriteLine($"PASS: {_checks} checks. Rendered screens: {Path.Combine(Root, "screens")}");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
        }));
        Dispatcher.Run();
        return Environment.ExitCode;
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAILED: " + label);
        _checks++;
    }
    private static void TestFilters()
    {
        var service = new ReceiptFilterService();
        Check(service.IsMatch(Receipt, new EJFilterOptions()), "unfiltered receipt");
        Check(!service.IsMatch("", new EJFilterOptions()), "empty block rejected");
        Check(service.IsMatch(Receipt, new EJFilterOptions { IsModeTrxFinder = true, TargetTrxNumber = "123" }), "invoice sequence with leading zeros");
        Check(!service.IsMatch(Receipt, new EJFilterOptions { IsModeTrxFinder = true, TargetTrxNumber = "999999" }), "SKU cannot match transaction identifier");
        Check(!service.IsMatch(Receipt, new EJFilterOptions { IsModeTrxFinder = true, TargetTrxNumber = "23" }), "transaction substring rejected");
        Check(service.IsMatch(Receipt, new EJFilterOptions { SpecificCashier = "maria", SpecificBagger = "juan", FilterMemberName = "ana" }), "field searches");
        Check(!service.IsMatch(Receipt, new EJFilterOptions { SpecificCashier = "JUAN" }), "bagger cannot match cashier field");
        Check(service.IsMatch(Receipt, new EJFilterOptions { FilterExactAmount = "100.00", MinimumAmount = 90, MaximumAmount = 110 }), "total amount exact and range");
        Check(!service.IsMatch(Receipt, new EJFilterOptions { FilterExactAmount = "200.00" }), "cash tender cannot match total");
        Check(!service.IsMatch(Receipt, new EJFilterOptions { FilterExactAmount = "80.00" }), "subtotal cannot match total");
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        Check(service.IsMatch(Receipt, new EJFilterOptions { FilterExactAmount = "100.00" }), "amount parsing independent of OS culture");
        CultureInfo.CurrentCulture = culture;
        Check(service.IsMatch(Receipt, new EJFilterOptions { FilterCardLast4 = "7654" }), "masked card suffix");
        Check(!service.IsMatch(Receipt.Replace("7654", "76549"), new EJFilterOptions { FilterCardLast4 = "7654" }), "card digits must be a suffix");
        Check(service.IsMatch(Receipt, new EJFilterOptions { SeniorCitizenOnly = true, PwdOnly = true }), "SC and PWD union");
        Check(!service.IsMatch(Receipt, new EJFilterOptions { PwdOnly = true }), "PWD filter excludes SC-only receipt");
        Check(service.IsMatch(Receipt, new EJFilterOptions { SearchText = "milk", ExcludeText = "bread", PaymentMethod = "Cash" }), "contains, excludes and tender");
        Check(!service.IsMatch(Receipt, new EJFilterOptions { ExcludeText = "milk" }), "excluded text");
        Check(service.IsMatch(Receipt, new EJFilterOptions { TimeFrom = TimeSpan.FromHours(14), TimeTo = TimeSpan.FromHours(15) }), "time window");
        Check(!service.IsMatch(Receipt, new EJFilterOptions { TimeFrom = TimeSpan.FromHours(16) }), "outside time window");
        Check(service.IsMatch(Receipt, new EJFilterOptions { PosLanes = new List<string> { "1" } }), "invoice lane");
        Check(!service.IsMatch(Receipt, new EJFilterOptions { PosLanes = new List<string> { "2" } }, "2"), "receipt lane overrides filename lane");
        Check(service.IsMatch("Plain journal\nTotal 100.00", new EJFilterOptions { PosLanes = new List<string> { "2" } }, "002"), "filename lane fallback");
        Check(service.IsMatch("VOID TRANSACTION\n" + Receipt, new EJFilterOptions { ReceiptType = "Void" }), "void classification");
        var compact = Receipt.Replace("722-001-000123", "7220010000000123");
        Check(service.IsMatch(compact, new EJFilterOptions { IsModeTrxFinder = true, TargetTrxNumber = "123" }), "short number finds compact store-lane-receipt identifier");
        Check(!service.IsMatch(compact, new EJFilterOptions { IsModeTrxFinder = true, TargetTrxNumber = "23" }), "short number must match the receipt sequence, not a partial suffix");
        Check(service.IsMatch(compact, new EJFilterOptions { IsModeTrxFinder = true, TargetTrxNumber = "123", FilterExactAmount = "999.00", ExcludeText = "MILK", PwdOnly = true }), "finder ignores other receipt filters");
        Check(service.IsMatch("Receipt No: 7220010000000123\n" + Receipt, new EJFilterOptions { IsModeTrxFinder = true, TargetTrxNumber = "123" }), "receipt number label supported");
        Check(!service.IsMatch("VOID TRANSACTION\n" + Receipt, new EJFilterOptions { ReceiptType = "Sale" }), "sale excludes void");
    }
    private static void TestSplitting()
    {
        var blocks = EJConsolidatorService.SplitReceipts(Receipt + "\nSECOND RECEIPT\n" + Receipt.Replace("000123", "000124"));
        Check(blocks.Length == 2, "second-receipt marker does not create an extra block");
        Check(!blocks[0].Contains("SECOND RECEIPT") && blocks[1].Contains("SECOND RECEIPT"), "second marker stays with its receipt");
        Check(EJConsolidatorService.SplitReceipts(Receipt.Replace("\n", "\r\n") + Receipt).Length == 2, "mixed line endings");
        Check(EJConsolidatorService.StoreFolderMatches("(0722) SAN FERNANDO", "722"), "store folder leading zeros");
        Check(!EJConsolidatorService.StoreFolderMatches("(1722) OTHER STORE", "722"), "store code is exact");
    }
    private static void TestForms()
    {
        var service = new FormsExportService();
        var defaults = new PuregoldITToolkit.Tools.SettingsTool.Models.SettingsModel();
        defaults.InfDefaultToEmails = "All IT-POS SYSTEM SUPPORT <allitpossystemsup@puregold.com.ph>, keep@example.com";
        Check(defaults.InfDefaultToEmails == "keep@example.com", "old settings cannot restore the retired mailing-list address");
        var vm = new InfViewModel(service);
        vm.NewColumnName = "Remarks <extra>";
        vm.AddColumnCommand.Execute(null);
        vm.CurrentEntry.StoreCode = "722"; vm.CurrentEntry.Sku = "00001";
        vm.CurrentEntry.CustomFields[0].Value = "Shelf <50> & \"promo\"";
        vm.AddToTableCommand.Execute(null);
        Check(vm.InfTable[0].CustomFields[0].Value == "Shelf <50> & \"promo\"", "INF custom value copied into row");
        Check(string.IsNullOrEmpty(vm.CurrentEntry.CustomFields[0].Value), "INF custom input clears for next item");
        vm.NewColumnName = "remarks <EXTRA>"; vm.AddColumnCommand.Execute(null);
        Check(vm.CustomColumns.Count == 1, "duplicate column names rejected");
        vm.NewColumnName = "Reference"; vm.AddColumnCommand.Execute(null);
        Check(vm.InfTable[0].CustomFields.Count == 2, "new column added to existing rows");
        string email = service.GenerateInfEmailContent(vm.InfTable, vm.SelectedInfType, "722", "<STORE>", "user@example.com\r\nBcc: injected", "", "", null);
        string encodedBody = email.Split(new[] { "Content-Transfer-Encoding: base64" + Environment.NewLine + Environment.NewLine }, StringSplitOptions.None)[1].Split(new[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).TakeWhile(l => !l.StartsWith("----")).Aggregate("", (a, b) => a + b);
        string html = Encoding.UTF8.GetString(Convert.FromBase64String(encodedBody));
        Check(html.Contains("<th>Remarks &lt;extra&gt;</th>") && html.Contains("Shelf &lt;50&gt; &amp; &quot;promo&quot;"), "custom headers and values in HTML with encoding");
        Check(!email.Contains("\nBcc:"), "mail header newline sanitization");
        vm.RemoveColumnCommand.Execute("Remarks <extra>");
        Check(vm.InfTable[0].CustomFields.Count == 1 && vm.CustomColumns[0] == "Reference", "remove custom column across rows");
        var model = new ObModel { EmployeeName = "TEST", Position = "IT", Department = "TEST" };
        var preview = service.RenderObImage(model, "OBFormTemplate.png");
        model.ConfigurePage(preview.PixelWidth, preview.PixelHeight);
        model.ConfigureImage(600, 200);
        model.ImageX = 90000; model.ImageY = -20;
        Check(model.ImageX + model.ImageWidth <= model.PageWidth && model.ImageY == 0, "OB placement clamped to page");
        model.ImageWidth = 150;
        Check(Math.Abs(model.ImageHeight - 50) < .01, "OB aspect ratio preserved");
        var export = service.RenderObImage(model, "OBFormTemplate.png", true);
        Check(export.PixelWidth == (int)Math.Ceiling(preview.PixelWidth * 300.0 / 96) && export.DpiX == 300, "OB export at 300 DPI");
        Check(export.IsFrozen, "OB rendered bitmap transferable between threads");
        byte[] pixels = new byte[600 * 200 * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 255; pixels[i + 3] = 255; }
        var attached = BitmapSource.Create(600, 200, 96, 96, PixelFormats.Bgra32, null, pixels, 600 * 4);
        TestImagePath = Path.Combine(Root, "attachment.png");
        using (var stream = File.Create(TestImagePath)) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(attached)); encoder.Save(stream); }
        model.SignatureImagePath = TestImagePath; model.ImageX = 20; model.ImageY = 180;
        var positioned = service.RenderObImage(model, "OBFormTemplate.png", true);
        byte[] sample = new byte[4];
        positioned.CopyPixels(new Int32Rect((int)(30 * 300.0 / 96), (int)(190 * 300.0 / 96), 1, 1), sample, 4, 0);
        Check(sample[0] == 255 && sample[1] == 0 && sample[2] == 0 && sample[3] == 255, "original attached image rendered at the selected location in export");
    }
    private static async Task TestConsolidation()
    {
        string input = Path.Combine(Root, "input"), output = Path.Combine(Root, "output");
        string date = "10202026";
        Directory.CreateDirectory(Path.Combine(input, date));
        File.WriteAllText(Path.Combine(input, date, "journal_001.txt"), Receipt);
        using (var zip = ZipFile.Open(Path.Combine(input, date + "_EJ.zip"), ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("another/journal_002.txt").Open())) writer.Write(Receipt.Replace("722-001", "722-002").Replace("000123", "000124"));
            using (var writer = new StreamWriter(zip.CreateEntry("../escape.txt").Open())) writer.Write("should not be extracted");
            using (var writer = new StreamWriter(zip.CreateEntry("../" + date + "_EJ.zip.contents-escape/escaped.txt").Open())) writer.Write("should not be extracted");
        }
        var service = new FixtureConsolidator();
        var options = new EJFilterOptions { StoreCode = "722", LiveServerIp = "127.0.0.1", LocalStoreDirectory = input, OutputDirectory = output, TargetDates = new List<DateTime> { new DateTime(2026, 10, 20) } };
        var reports = new CaptureProgress();
        int count = await service.ProcessConsolidationAsync(options, reports, CancellationToken.None);
        Check(count == 2, "nested zip and loose journals counted once");
        Check(reports.Reports.Count(r => r.Receipt != null) == 2, "receipt previews reported");
        string firstOutput = reports.Reports.First(r => r.OutputDirectory != null).OutputDirectory;
        Check(Directory.GetFiles(firstOutput, "Merged_*.txt").Length == 1, "one daily result file");
        Check(File.Exists(Path.Combine(firstOutput, "RunSummary.txt")), "run summary exported");
        string contents = File.ReadAllText(Directory.GetFiles(firstOutput, "Merged_*.txt")[0]);
        Check(contents.Contains("000123") && contents.Contains("000124") && contents.Contains("[Source:"), "output includes receipts and source provenance");
        Check(!Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EJ_Cache"), "escape.txt", SearchOption.AllDirectories).Any(), "zip traversal rejected");
        Check(!Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EJ_Cache"), "escaped.txt", SearchOption.AllDirectories).Any(), "zip sibling-prefix traversal rejected");
        File.AppendAllText(Path.Combine(input, date, "journal_001.txt"), Receipt.Replace("000123", "000125"));
        count = await service.ProcessConsolidationAsync(options, reports, CancellationToken.None);
        Check(count == 3, "fresh runs see new journal data");
        Check(File.Exists(Directory.GetFiles(firstOutput, "Merged_*.txt")[0]), "earlier outputs retained");
        options.PosLanes.Add("2");
        count = await service.ProcessConsolidationAsync(options, reports, CancellationToken.None);
        Check(count == 1, "lane filter through full extraction pipeline");
        options.PosLanes.Clear(); options.IsModeTrxFinder = true; options.TargetTrxNumber = "124";
        count = await service.ProcessConsolidationAsync(options, reports, CancellationToken.None);
        Check(count == 1, "transaction sequence search through pipeline");
        options.TargetTrxNumber = "../bad";
        bool invalid = false;
        try { await service.ProcessConsolidationAsync(options, reports, CancellationToken.None); } catch (ArgumentException) { invalid = true; }
        Check(invalid, "unsafe transaction filename rejected");
        options.TargetTrxNumber = "124";
        string secondInput = Path.Combine(Root, "second-input", date); Directory.CreateDirectory(secondInput);
        File.WriteAllText(Path.Combine(secondInput, "single_001.txt"), Receipt.Replace("000123", "000124"));
        options.LocalStoreDirectory = Path.GetDirectoryName(secondInput); options.UseCachedData = true;
        count = await service.ProcessConsolidationAsync(options, reports, CancellationToken.None);
        Check(count == 1, "cache identity isolates different local source directories");
        options.TargetDates.Add(options.TargetDates[0]);
        count = await service.ProcessConsolidationAsync(options, reports, CancellationToken.None);
        Check(count == 1, "duplicate dates do not duplicate receipts");
        bool cancelled = false;
        try { await service.ProcessConsolidationAsync(options, reports, new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "pre-cancelled operation stops cleanly");
    }
    private static async Task TestSourceRouting()
    {
        foreach (var mode in new[] { EJExecutionMode.Local, EJExecutionMode.Ftp, EJExecutionMode.FtpOthers })
        {
            var source = new RouteConsolidator();
            var options = new EJFilterOptions { ExecutionMode = mode, StoreCode = "722", LiveServerIp = "127.0.0.1", FtpHost = "127.0.0.2",
                TargetDates = new List<DateTime> { new DateTime(2026, 10, 20) }, OutputDirectory = Path.Combine(Root, "route-" + mode) };
            int count = await source.ProcessConsolidationAsync(options, new CaptureProgress(), CancellationToken.None);
            Check(count == 1, "source routing returned a receipt: " + mode);
            Check(mode == EJExecutionMode.Local ? source.LiveCalls == 1 && source.FtpCalls == 0 : source.LiveCalls == 0 && source.FtpCalls == 1,
                "Local uses live/CONSO and FTP choices use FTP: " + mode);
        }
    }
    private static async Task TestAutomaticLogin()
    {
        Check(PuregoldITToolkit.Services.AutomaticFtpLogin.Usernames.Contains("ftp1@puregold") && PuregoldITToolkit.Services.AutomaticFtpLogin.Usernames.Contains("puregold/ftp1a"), "automatic username array includes existing account formats");
        using (var server = new LoginTestServer())
        {
            using (var session = await Task.Run(() => PuregoldITToolkit.Services.AutomaticFtpLogin.Open("127.0.0.1", server.Port, 3)))
                Check(session.Opened, "FTP opens without manual credentials");
            Check(server.Users.Take(2).SequenceEqual(new[] { "ftp1@puregold", "ftp2@puregold" }), "FTP retries the next username after a rejected login");
        }
    }
    private static async Task TestCleanup()
    {
        string data = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"); Directory.CreateDirectory(data);
        string stores = Path.Combine(data, "stores.csv"); File.WriteAllText(stores, "722,TEST,AUTO,PUREPOS");
        await new PuregoldITToolkit.Tools.PureposTool.Services.ToolkitDiagnosticsService().CleanCacheDirectoriesAsync(null);
        Check(File.ReadAllText(stores) == "722,TEST,AUTO,PUREPOS", "temporary cleanup preserves saved store data");
    }
    private static async Task TestViewModels()
    {
        var vm = new EJConsolidatorViewModel(new EJConsolidatorService(new ReceiptFilterService()));
        string own = vm.StoreCode;
        vm.StoreCode = "12345";
        Check(vm.IsModeFtpOthers && !vm.IsModeLocal && !vm.IsModeFtp, "other store automatically selects FTP Others");
        vm.IsModeLocal = true;
        Check(vm.StoreCode == own && vm.IsModeLocal, "local mode returns to own store");
        var discovery = new EJConsolidatorViewModel(new FakeEjDiscovery()) { StoreCode = "9999" };
        await Task.Delay(650);
        Check(discovery.DetectedStore.Contains("Discovered store"), "entering another store code detects its FTP store name automatically");
        vm.SearchText = "x"; vm.FilterExactAmount = "10"; vm.PosLanes = "1-3";
        vm.ResetFiltersCommand.Execute(null);
        Check(vm.SearchText == null && vm.FilterExactAmount == null && vm.PosLanes == null, "filter reset clears all fields");
        vm.PosLanes = "bad";
        await ((IAsyncRelayCommand)vm.ProcessCommand).ExecuteAsync(null);
        Check(!vm.IsBusy && vm.StatusMessage.Contains("lane"), "invalid lane rejected before extraction");
        var pos = new PureposViewModel(new ThrowingPureposService());
        pos.CliTargetIp = "127.0.0.1";
        await ((IAsyncRelayCommand)pos.RunSingleCliCommand).ExecuteAsync(null);
        Check(!pos.IsBusy && pos.CliOutput.Contains("simulated failure"), "PurePOS error handled and busy state restored");
        Check(pos.RunEjSaveCommand.CanExecute(null), "other PurePOS operations re-enabled after failure");
        var pendingService = new PendingPureposService();
        var pending = new PureposViewModel(pendingService) { CliTargetIp = "127.0.0.1" };
        var pendingTask = ((IAsyncRelayCommand)pending.RunSingleCliCommand).ExecuteAsync(null);
        Check(pending.IsBusy && !pending.RunEjSaveCommand.CanExecute(null), "parallel PurePOS operations disabled while SSH is running");
        pendingService.Completion.SetResult("done"); await pendingTask;
        Check(!pending.IsBusy && pending.RunEjSaveCommand.CanExecute(null), "successful operation restores availability");
        pos.DiagnosticTotalLanes = 500;
        await ((IAsyncRelayCommand)pos.RunNetworkPingSuiteCommand).ExecuteAsync(null);
        Check(!pos.IsBusy && pos.CliOutput.Contains("204"), "invalid lane count rejected before network operations");
    }
    private static async Task TestViews()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        PuregoldITToolkit.Services.LoggingService.Instance.Clear();
        var listener = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var formsService = new FormsExportService();
        var inf = new InfViewModel(formsService);
        inf.NewColumnName = "Remarks"; inf.AddColumnCommand.Execute(null);
        inf.CurrentEntry.StoreCode = "722"; inf.CurrentEntry.Sku = "123"; inf.AddToTableCommand.Execute(null);
        var ob = new ObViewModel(formsService);
        ob.FormData.SignatureImagePath = TestImagePath; ob.FormData.ImageX = 20; ob.FormData.ImageY = 180;
        await Task.Delay(250);
        var shell = new Shell();
        var window = new PuregoldITToolkit.Views.MainWindow { DataContext = shell, WindowState = WindowState.Normal, Width = 1400, Height = 900, WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        var screens = new Dictionary<string, object>
        {
            { "inf", inf },
            { "ej", new EJConsolidatorViewModel(new EJConsolidatorService(new ReceiptFilterService())) },
            { "purepos", new PureposViewModel(new ThrowingPureposService()) },
            { "ftp-browser", new PuregoldITToolkit.Tools.FtpBrowserTool.ViewModels.FtpBrowserViewModel(new FakeFtpBrowserService()) },
            { "settings", new PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel(new PuregoldITToolkit.Tools.SettingsTool.Services.SettingsService()) },
            { "logs", new SystemLogsViewModel() },
            { "help", new InfoHelpViewModel() },
            { "service-desk", new PuregoldITToolkit.Tools.ServiceDeskTool.ViewModels.ServiceDeskViewModel(new PuregoldITToolkit.Tools.ServiceDeskTool.Services.ServiceDeskService()) },
            { "pims", new PuregoldITToolkit.Tools.PimsManagerTool.ViewModels.PimsManagerViewModel(new PuregoldITToolkit.Tools.PimsManagerTool.Services.PimsRepository()) },
            { "sod", new PuregoldITToolkit.Tools.SodChecker.ViewModels.SodCheckerViewModel(new PuregoldITToolkit.Tools.SodChecker.Services.SodCheckerService()) },
            { "sc-pwd", new PuregoldITToolkit.Tools.ScPwdReportTool.ViewModels.ScPwdReportViewModel(new PuregoldITToolkit.Tools.ScPwdReportTool.Services.ScPwdReportService()) },
            { "forms", new FormsMainViewModel(inf, ob, new SsrfViewModel(formsService), new TsrfViewModel(formsService)) }
        };
        string folder = Path.Combine(Root, "screens"); Directory.CreateDirectory(folder);
        foreach (var item in screens)
        {
            shell.CurrentContent = item.Value;
            window.DataContext = null; window.DataContext = shell;
            window.Measure(new Size(1400, 900)); window.Arrange(new Rect(0, 0, 1400, 900)); window.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var render = new RenderTargetBitmap((int)((FrameworkElement)window.Content).ActualWidth, (int)((FrameworkElement)window.Content).ActualHeight, 96, 96, PixelFormats.Pbgra32);
            render.Render((Visual)window.Content);
            using (var stream = File.Create(Path.Combine(folder, item.Key + ".png")))
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(render)); encoder.Save(stream);
            }
            Check(window.ActualWidth > 0, item.Key + " screen loads and lays out");
            Check(!FindAll<TextBlock>(window).Any(t => t.Text != null && (t.Text.Contains("PROD RELEASE") || t.Text.Contains("allitpossystemsup@puregold.com.ph"))), item.Key + " has no retired support address or release badge");
            if (item.Value is PureposViewModel)
            {
                var view = FindChild<PuregoldITToolkit.Tools.PureposTool.Views.PureposView>((DependencyObject)window.Content);
                foreach (var page in new[] { new[] { "RbAutoPerm", "PermissionData.LiveServerIp" }, new[] { "RbManualEj", "EjData.LiveServerIp" }, new[] { "RbCli", "CliTargetIp" }, new[] { "RbEod", "EodData.StoreName" }, new[] { "RbBirCrmSync", "AutoSyncData.LiveServerIp" } })
                {
                    ((RadioButton)view.FindName(page[0])).IsChecked = true;
                    window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Check(FindAll<TextBox>(view).Any(t => t.IsVisible && System.Windows.Data.BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path?.Path == page[1]), "PurePOS page exposes its configuration: " + page[0]);
                    var pageRender = new RenderTargetBitmap((int)((FrameworkElement)window.Content).ActualWidth, (int)((FrameworkElement)window.Content).ActualHeight, 96, 96, PixelFormats.Pbgra32); pageRender.Render((Visual)window.Content);
                    using (var stream = File.Create(Path.Combine(folder, page[0] + ".png"))) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(pageRender)); encoder.Save(stream); }
                }
            }
            if (item.Value is FormsMainViewModel forms)
            {
                forms.IsObSelected = true;
                await Task.Delay(150); window.UpdateLayout();
                var obView = FindChild<PuregoldITToolkit.Tools.FormsTool.Views.ObView>((DependencyObject)window.Content);
                var surface = (Border)obView.FindName("PreviewSurface");
                double before = ob.FormData.ImageX;
                surface.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(surface), Environment.TickCount, System.Windows.Input.Key.Right) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent });
                Check(ob.FormData.ImageX == before + 1, "OB arrow-key placement event moves attachment by one pixel");
                var obRender = new RenderTargetBitmap((int)((FrameworkElement)window.Content).ActualWidth, (int)((FrameworkElement)window.Content).ActualHeight, 96, 96, PixelFormats.Pbgra32); obRender.Render((Visual)window.Content);
                using (var stream = File.Create(Path.Combine(folder, "ob.png"))) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(obRender)); encoder.Save(stream); }
            }
        }
        var toolsMenu = FindAll<MenuItem>(window).FirstOrDefault(m => (m.Header as string) == "All tools");
        if (toolsMenu != null) { toolsMenu.IsSubmenuOpen = true; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); toolsMenu.IsSubmenuOpen = false; }
        PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        Check(listener.Errors.Count == 0, "WPF binding errors: " + string.Join(" | ", listener.Errors));
        window.Close();
    }
    private static T FindChild<T>(DependencyObject root) where T : DependencyObject => FindAll<T>(root).FirstOrDefault();
    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in FindAll<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Errors { get; } = new List<string>();
        public override void Write(string message) { if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message); }
        public override void WriteLine(string message) { Write(message); }
    }
    private sealed class CaptureProgress : IProgress<EJProgressReport>
    {
        public List<EJProgressReport> Reports { get; } = new List<EJProgressReport>();
        public void Report(EJProgressReport report) { Reports.Add(report); }
    }
    public sealed class Shell
    {
        public object CurrentContent { get; set; }
        public string ActiveWorkflowTitle => "Verification";
        public object DashboardVm { get; } = new { StoreName = "Test store", StoreCode = "722", CurrentTime = "October 27, 2026" };
        public object[] UtilityTools { get; } = { new { ToolName = "Test utility" } };
        public NavigationLink[] NavigationLinks { get; } = { new NavigationLink("Dashboard", "Overview"), new NavigationLink("EJConsolidator", "EJ Consolidator"), new NavigationLink("Puregold Forms", "Forms"), new NavigationLink("Puregold Toolkits", "PurePOS"), new NavigationLink("Settings", "Settings"), new NavigationLink("SystemLogs", "Activity"), new NavigationLink("InfoHelp", "Help") };
        public object NavigateWorkflowCommand => null;
        public object SelectUtilityToolCommand => null;
        public object OpenLogFolderCommand => null;
        public object ClearLogsCommand => null;
        public bool IsConsoleExpanded { get; set; }
        public object Logger => PuregoldITToolkit.Services.LoggingService.Instance;
    }
    private sealed class PendingPureposService : ThrowingPureposService
    {
        public TaskCompletionSource<string> Completion { get; } = new TaskCompletionSource<string>();
        public override Task<string> RunSshCommandAsync(string host, string username, string password, string command) => Completion.Task;
    }
    private class ThrowingPureposService : IPureposService
    {
        public virtual Task<string> RunSshCommandAsync(string host, string username, string password, string command) => throw new InvalidOperationException("simulated failure");
        public Task RunAutoPermissionAsync(AutoPermissionModel model, Action<string> log) => throw new InvalidOperationException("simulated failure");
        public Task RunManualEjSaveAsync(EjSaveModel model, Action<string> log) => throw new InvalidOperationException("simulated failure");
        public Task RunEodGeneratorAsync(EodModel model, Action<string> log) => throw new InvalidOperationException("simulated failure");
        public Task RunBirEisSyncAsync(AutoSyncModel model, Action<string> log) => throw new InvalidOperationException("simulated failure");
        public Task RunCrmPointsSyncAsync(AutoSyncModel model, Action<string> log) => throw new InvalidOperationException("simulated failure");
        public Task RunPromoAvailmentSyncAsync(AutoSyncModel model, Action<string> log) => throw new InvalidOperationException("simulated failure");
    }
    private sealed class FixtureConsolidator : EJConsolidatorService
    {
        public FixtureConsolidator() : base(new ReceiptFilterService()) { }
        protected override bool ExtractLocalStoreData(EJFilterOptions options, DateTime date, string destination, IProgress<EJProgressReport> progress, CancellationToken ct) => ExtractDirectoryData(options, date, destination, progress, ct);
    }
    private sealed class RouteConsolidator : EJConsolidatorService
    {
        public int LiveCalls, FtpCalls;
        public RouteConsolidator() : base(new ReceiptFilterService()) { }
        protected override bool DownloadFromLiveServerDoneZipSafe(EJFilterOptions options, string date, string destination, IProgress<EJProgressReport> progress, CancellationToken ct)
        { LiveCalls++; File.WriteAllText(Path.Combine(destination, "live_001.txt"), Receipt); return true; }
        protected override bool DownloadFromPrimaryFtpSafe(EJFilterOptions options, string date, string destination, string host, int port, int timeout, bool passive, IProgress<EJProgressReport> progress, CancellationToken ct)
        { FtpCalls++; File.WriteAllText(Path.Combine(destination, "ftp_001.txt"), Receipt); return true; }
    }
    private sealed class LoginTestServer : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener _listener;
        private readonly Task _worker;
        public List<string> Users { get; } = new List<string>();
        public int Port => ((System.Net.IPEndPoint)_listener.LocalEndpoint).Port;
        public LoginTestServer()
        {
            _listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); _listener.Start();
            _worker = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        try
                        {
                        using (var connection = await _listener.AcceptTcpClientAsync())
                        using (var stream = connection.GetStream())
                        using (var reader = new StreamReader(stream, Encoding.ASCII))
                        using (var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" })
                        {
                            writer.WriteLine("220 Test FTP"); string line, user = "";
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                string command = line.Split(' ')[0].ToUpperInvariant();
                                File.AppendAllText(Path.Combine(Root, "ftp-commands.txt"), command + Environment.NewLine);
                                switch (command)
                                {
                                    case "USER": user = line.Substring(5); Users.Add(user); writer.WriteLine("331 Password required"); break;
                                    case "PASS": writer.WriteLine(user == "ftp2@puregold" ? "230 Logged in" : "530 Login incorrect"); break;
                                    case "FEAT": writer.WriteLine("211 No features"); break;
                                    case "SYST": writer.WriteLine("215 UNIX Type: L8"); break;
                                    case "PWD": writer.WriteLine("257 \"/\" is current directory"); break;
                                    case "CWD": writer.WriteLine("250 Directory changed"); break;
                                    case "QUIT": writer.WriteLine("221 Goodbye"); break;
                                    default: writer.WriteLine("200 OK"); break;
                                }
                                if (command == "QUIT" || (command == "PASS" && user != "ftp2@puregold")) break;
                            }
                        }
                        } catch (IOException) { }
                    }
                }
                catch (Exception) { }
            });
        }
        public void Dispose() { _listener.Stop(); }
    }
    private sealed class FakeEjDiscovery : PuregoldITToolkit.Tools.EJConsolidator.Interfaces.IEJConsolidatorService
    {
        public Task<List<EJStoreInfo>> GetStoresAsync(EJFilterOptions options, CancellationToken ct) => Task.FromResult(new List<EJStoreInfo> { new EJStoreInfo { StoreCode = "9999", StoreName = "Discovered store", FolderName = "(9999) Discovered store" } });
        public Task<int> ProcessConsolidationAsync(EJFilterOptions options, IProgress<string> text, IProgress<int> percentage) => throw new NotSupportedException();
        public Task<int> ProcessConsolidationAsync(EJFilterOptions options, IProgress<EJProgressReport> progress, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class FakeFtpBrowserService : PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces.IFtpBrowserService
    {
        public Task<List<PuregoldITToolkit.Tools.FtpBrowserTool.Models.FtpStoreModel>> GetStoresAsync() => Task.FromResult(new List<PuregoldITToolkit.Tools.FtpBrowserTool.Models.FtpStoreModel>());
        public Task<List<PuregoldITToolkit.Tools.FtpBrowserTool.Models.FtpFileModel>> GetFilesAsync(string host, string path) => Task.FromResult(new List<PuregoldITToolkit.Tools.FtpBrowserTool.Models.FtpFileModel>());
        public Task<List<PuregoldITToolkit.Tools.FtpBrowserTool.Models.FtpFileModel>> DeepSearchAsync(string host, string path, string pattern, IProgress<PuregoldITToolkit.Tools.FtpBrowserTool.Models.DeepSearchProgress> progress, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> DownloadFileAsync(string host, string remote, string local, IProgress<PuregoldITToolkit.Tools.FtpBrowserTool.Models.TransferProgress> progress = null) => throw new NotSupportedException();
        public Task DownloadFilesAsync(string host, string[] paths, string local, IProgress<PuregoldITToolkit.Tools.FtpBrowserTool.Models.TransferProgress> progress, CancellationToken ct) => throw new NotSupportedException();
        public Task DownloadFolderAsync(string host, string path, string local, IProgress<PuregoldITToolkit.Tools.FtpBrowserTool.Models.TransferProgress> progress, CancellationToken ct) => throw new NotSupportedException();
        public Task UploadFilesAsync(string host, string path, string[] local, IProgress<PuregoldITToolkit.Tools.FtpBrowserTool.Models.TransferProgress> progress, CancellationToken ct) => throw new NotSupportedException();
        public Task RenameFileAsync(string host, string oldPath, string newPath) => throw new NotSupportedException();
        public Task DeleteFilesAsync(string host, string[] paths) => throw new NotSupportedException();
        public Task<string> ReadRemoteTextFileAsync(string host, string path) => throw new NotSupportedException();
        public Task<PuregoldITToolkit.Tools.FtpBrowserTool.Models.FilePropertiesModel> GetFilePropertiesAsync(string host, string path) => throw new NotSupportedException();
    }

}

