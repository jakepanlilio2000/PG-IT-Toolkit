using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinSCP;

namespace PuregoldITToolkit.Tools.EJConsolidator.Services
{
    public class EJConsolidatorService : IEJConsolidatorService
    {
        private readonly IReceiptFilterService _filterService;

        public EJConsolidatorService(IReceiptFilterService filterService)
        {
            _filterService = filterService;
        }

        private class ReportRow
        {
            public DateTime Date { get; set; }
            public string Name { get; set; }
            public string IdNo { get; set; }
            public string Tin { get; set; }
            public string Invoice { get; set; }
            public decimal Sales { get; set; }
            public decimal Vat { get; set; }
            public decimal Exempt { get; set; }
            public decimal Discount5 { get; set; }
            public decimal NetSales { get; set; }

            public string Terminal { get; set; }
            public string SerialNumber { get; set; }
            public string MinNumber { get; set; }
            public string Cashier { get; set; }
            public string Address { get; set; }
            public string VatRegTin { get; set; }
        }

        // Helper to safely parse decimals regardless of commas or culture settings
        private decimal ParseDecimal(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            input = input.Replace(",", "").Trim();
            decimal.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result);
            return result;
        }

        public async Task<int> ProcessConsolidationAsync(EJFilterOptions options, IProgress<string> textProgress, IProgress<int> pctProgress)
        {
            int totalProcessedReceipts = 0;
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string sodPath = Path.Combine(desktopPath, "SOD_Output");
            Directory.CreateDirectory(sodPath);

            string masterFile = Path.Combine(sodPath, $"Master_Consolidated_{options.StoreCode}_{DateTime.Now:yyyyMMdd}.txt");
            string trxFile = Path.Combine(sodPath, $"{options.TargetTrxNumber}.txt");

            if (options.IsModeTrxFinder && File.Exists(trxFile)) File.Delete(trxFile);
            if (!options.IsModeTrxFinder && options.MergeAllIntoOneFile && File.Exists(masterFile)) File.Delete(masterFile);

            string cacheRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EJ_Cache", options.StoreCode);
            Directory.CreateDirectory(cacheRoot);

            var scReports = new System.Collections.Concurrent.ConcurrentBag<ReportRow>();
            var pwdReports = new System.Collections.Concurrent.ConcurrentBag<ReportRow>();

            await Task.Run(() =>
            {
                int totalDates = options.TargetDates.Count;
                int currentDateIndex = 0;

                foreach (var targetDate in options.TargetDates)
                {
                    string dateStr = targetDate.ToString("MMddyyyy");
                    string dateCacheDir = Path.Combine(cacheRoot, dateStr);

                    textProgress?.Report($"Processing Date: {dateStr}...");

                    if (Directory.Exists(dateCacheDir) && Directory.GetFiles(dateCacheDir, "*.txt", SearchOption.AllDirectories).Any())
                    {
                        textProgress?.Report($"  [Cache] Loaded {dateStr} instantly from offline storage.");
                    }
                    else
                    {
                        if (Directory.Exists(dateCacheDir)) Directory.Delete(dateCacheDir, true);
                        Directory.CreateDirectory(dateCacheDir);

                        bool success = DownloadFromPrimaryFtp(options, dateStr, dateCacheDir, textProgress);

                        if (!success)
                        {
                            textProgress?.Report($"  [Fallback] Primary failed. Checking Live Server {options.LiveServerIp} for {dateStr}...");
                            success = DownloadFromFallbackSftp(options, dateStr, dateCacheDir, textProgress);
                        }

                        if (!success)
                        {
                            textProgress?.Report($"  [Skipped] Could not retrieve any data for {dateStr}.");
                            if (Directory.Exists(dateCacheDir)) Directory.Delete(dateCacheDir, true);
                        }
                    }

                    if (Directory.Exists(dateCacheDir))
                    {
                        string dailyFile = Path.Combine(sodPath, $"Merged_{options.StoreCode}_{dateStr}.txt");
                        if (!options.IsModeTrxFinder && !options.MergeAllIntoOneFile && File.Exists(dailyFile)) File.Delete(dailyFile);

                        textProgress?.Report($"  Merging and Filtering {dateStr}...");
                        totalProcessedReceipts += MergeAndFilterFiles(dateCacheDir, sodPath, masterFile, trxFile, dateStr, options, scReports, pwdReports, textProgress);
                    }

                    currentDateIndex++;
                    pctProgress?.Report((int)((double)currentDateIndex / totalDates * 100));
                }

                if (options.GenerateScReport && scReports.Any())
                {
                    textProgress?.Report("Generating SC Excel Reports...");
                    ExportExcelReports(scReports.ToList(), "senior", options, sodPath);
                }

                if (options.GeneratePwdReport && pwdReports.Any())
                {
                    textProgress?.Report("Generating PWD Excel Reports...");
                    ExportExcelReports(pwdReports.ToList(), "pwd", options, sodPath);
                }
            });

            return totalProcessedReceipts;
        }

        private int MergeAndFilterFiles(string sourceDir, string sodPath, string masterFile, string trxFile, string dateStr, EJFilterOptions options,
            System.Collections.Concurrent.ConcurrentBag<ReportRow> scReports, System.Collections.Concurrent.ConcurrentBag<ReportRow> pwdReports, IProgress<string> progress)
        {
            var txtFiles = Directory.GetFiles(sourceDir, "*.txt", SearchOption.AllDirectories);
            if (txtFiles.Length == 0) return 0;

            int rawReceiptCount = 0;
            var filteredReceipts = new System.Collections.Concurrent.ConcurrentBag<string>();

            Parallel.ForEach(txtFiles, file =>
            {
                string fileContent = File.ReadAllText(file, Encoding.UTF8);

                if (options.IsModeTrxFinder && !string.IsNullOrWhiteSpace(options.TargetTrxNumber))
                {
                    if (fileContent.IndexOf(options.TargetTrxNumber.Trim(), StringComparison.OrdinalIgnoreCase) < 0) return;
                }

                var blocks = Regex.Split(fileContent, @"(?=Puregold Price Club|SECOND RECEIPT)", RegexOptions.IgnoreCase);
                System.Threading.Interlocked.Add(ref rawReceiptCount, blocks.Length);

                foreach (var block in blocks)
                {
                    if (string.IsNullOrWhiteSpace(block)) continue;

                    if (_filterService.IsMatch(block, options))
                    {
                        string cleanedBlock = Regex.Replace(block, @"^\[EXTRACTED DATA\].*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase).Trim();
                        filteredReceipts.Add(cleanedBlock + Environment.NewLine);
                    }

                    // Report Parsers (Ignore 2nd Receipt)
                    if ((options.GenerateScReport || options.GeneratePwdReport) && block.IndexOf("SECOND RECEIPT", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        bool isSc = block.Contains("OSCA No:");
                        bool isPwd = block.Contains("PWD No:");

                        if ((isSc && options.GenerateScReport) || (isPwd && options.GeneratePwdReport))
                        {
                            var row = ParseReportRow(block, isSc);
                            if (row != null)
                            {
                                if (isSc) scReports.Add(row);
                                else pwdReports.Add(row);
                            }
                        }
                    }
                }
            });

            var finalReceiptList = filteredReceipts.ToList();
            progress?.Report($"    Analyzed {txtFiles.Length} TXT files -> Found {rawReceiptCount} receipts -> {finalReceiptList.Count} matched filters.");

            if (finalReceiptList.Any())
            {
                string targetFile = options.IsModeTrxFinder ? trxFile : (options.MergeAllIntoOneFile ? masterFile : Path.Combine(sodPath, $"Merged_{options.StoreCode}_{dateStr}.txt"));

                using (StreamWriter writer = new StreamWriter(targetFile, append: true, encoding: Encoding.UTF8))
                {
                    foreach (var receipt in finalReceiptList)
                    {
                        using (StringReader reader = new StringReader(receipt))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null) writer.WriteLine(line);
                        }
                        writer.WriteLine();
                        writer.WriteLine(new string('-', 50));
                        writer.WriteLine();
                    }
                }
            }

            return finalReceiptList.Count;
        }

        private ReportRow ParseReportRow(string block, bool isSc)
        {
            try
            {
                var row = new ReportRow();

                string dateRaw = Regex.Match(block, @"Trans\. Date:\s*(\d{4}-\d{2}-\d{2})").Groups[1].Value;
                if (DateTime.TryParse(dateRaw, out DateTime dt)) row.Date = dt; else return null;

                row.Invoice = Regex.Match(block, @"INVOICE#\s*([\d\-]+)").Groups[1].Value.Trim();
                var invParts = row.Invoice.Split('-');
                row.Terminal = invParts.Length >= 2 ? invParts[1] : "000";

                row.SerialNumber = Regex.Match(block, @"S/N:([A-Z0-9]+)").Groups[1].Value.Trim();
                row.MinNumber = Regex.Match(block, @"MIN:(\d+)").Groups[1].Value.Trim();
                row.Cashier = Regex.Match(block, @"Cashier:\s*([A-Z\s]+)").Groups[1].Value.Trim();

                // Use robust decimal parsing
                row.Sales = ParseDecimal(Regex.Match(block, @"Subtotal\s+([\d,\.]+)").Groups[1].Value);
                row.Vat = ParseDecimal(Regex.Match(block, @"VAT\s*\(12%\)\s+([\d,\.]+)").Groups[1].Value);
                row.Exempt = ParseDecimal(Regex.Match(block, @"VAT Exempt Sale\s*\(E\)\s+([\d,\.]+)").Groups[1].Value);

                // Net Sales: Match "Total Amt Due Php X" or "Total Php X"
                var netMatch = Regex.Match(block, @"Total\s+(?:Amt\s+Due\s+)?Php\s+([\d,\.]+)");
                if (!netMatch.Success) netMatch = Regex.Match(block, @"Total\s+Php\s+([\d,\.]+)");
                row.NetSales = ParseDecimal(netMatch.Groups[1].Value);

                string discPattern = isSc ? @"SC Discount Applied:\s*([\d,\.]+)" : @"PWD Discount Applied:\s*([\d,\.]+)";
                row.Discount5 = ParseDecimal(Regex.Match(block, discPattern).Groups[1].Value);

                row.Name = Regex.Match(block, @"Name:\s*([^\n\r]+)").Groups[1].Value.Trim();

                // FIX: Strictly match line starting with "TIN:" to avoid catching "VAT REG TIN:"
                //var tinMatch = Regex.Match(block, @"(?m)^TIN:\s*(.*)$");
                string tinRaw =  "";
                row.Tin = string.IsNullOrWhiteSpace(tinRaw) ? "" : tinRaw;

                string idPattern = isSc ? @"OSCA No:\s*([^\n\r]+)" : @"PWD No:\s*([^\n\r]+)";
                row.IdNo = Regex.Match(block, idPattern).Groups[1].Value.Trim();

                var addressMatch = Regex.Match(block, @"Puregold Price Club, Inc\.?(.*?)(?:VAT REG TIN)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                row.Address = addressMatch.Success ? Regex.Replace(addressMatch.Groups[1].Value, @"\s+", " ").Trim() : "SAVER'S BLDG. MAC ARTHUR HIGHWAY DOLORES CITY OF SAN FERNANDO";
                row.VatRegTin = Regex.Match(block, @"([\d\-]+)").Groups[1].Value.Trim();

                return row;
            }
            catch { return null; }
        }

        private void ExportExcelReports(List<ReportRow> allRows, string typeName, EJFilterOptions options, string outputDir)
        {
            var groupedByPos = allRows.GroupBy(r => r.Terminal).OrderBy(g => g.Key);

            foreach (var posGroup in groupedByPos)
            {
                var distinctDates = posGroup.Select(r => r.Date.Date).Distinct().OrderBy(d => d).ToList();
                var dateChunks = ChunkBy(distinctDates, options.ReportChunkDays);

                foreach (var chunk in dateChunks)
                {
                    DateTime minDate = chunk.Min();
                    DateTime maxDate = chunk.Max();
                    var chunkRows = posGroup.Where(r => r.Date.Date >= minDate && r.Date.Date <= maxDate).OrderBy(r => r.Date).ToList();

                    if (!chunkRows.Any()) continue;

                    string dateRangeStr = $"{minDate:MMdd}-{maxDate:MMdd}";
                    string fileName = $"{options.StoreCode}_{posGroup.Key}_{dateRangeStr}_{typeName}_report.xls";
                    string filePath = Path.Combine(outputDir, fileName);

                    GenerateHtmlExcelFile(chunkRows, typeName, options, filePath);
                }
            }
        }

        private void GenerateHtmlExcelFile(List<ReportRow> rows, string typeName, EJFilterOptions options, string outputPath)
        {
            var first = rows.First();
            var html = new StringBuilder();

            html.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns=\"http://www.w3.org/TR/REC-html40\">");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"utf-8\">");
            html.AppendLine("<!--[if gte mso 9]>");
            html.AppendLine("<xml>");
            html.AppendLine("<x:ExcelWorkbook>");
            html.AppendLine("<x:ExcelWorksheets>");
            html.AppendLine("<x:ExcelWorksheet>");
            html.AppendLine("<x:Name>Report</x:Name>");
            html.AppendLine("<x:WorksheetOptions>");
            html.AppendLine("<x:DisplayGridlines/>");
            html.AppendLine("</x:WorksheetOptions>");
            html.AppendLine("</x:ExcelWorksheet>");
            html.AppendLine("</x:ExcelWorksheets>");
            html.AppendLine("</x:ExcelWorkbook>");
            html.AppendLine("</xml>");
            html.AppendLine("<![endif]-->");

            html.AppendLine("<style>");
            html.AppendLine("body, table, td, th { font-family: Calibri; font-size: 11pt; }");
            html.AppendLine("table { border-collapse: collapse; }");
            html.AppendLine(".b { font-weight: bold; }");
            html.AppendLine(".c { text-align: center; vertical-align: middle; }");
            html.AppendLine(".r { text-align: right; }");
            html.AppendLine("</style>");

            html.AppendLine("</head>");
            html.AppendLine("<body>");

            string storeNameDisplay = string.IsNullOrWhiteSpace(options.StoreName) ? "PUREGOLD JR - SAN FERNANDO" : options.StoreName;
            string terminalDisplay = int.TryParse(first.Terminal, out int termNum) ? termNum.ToString() : first.Terminal;
            string userIdDisplay = string.IsNullOrWhiteSpace(options.ReportUserId) ? "87113" : options.ReportUserId;

            html.AppendLine("<table cellpadding=\"0\" cellspacing=\"0\">");

            html.AppendLine("<colgroup>");
            html.AppendLine("<col width=\"64\" />");  // A: Date (Width 8)
            html.AppendLine("<col width=\"152\" />"); // B: Name (Width 19)
            html.AppendLine("<col width=\"152\" />"); // C: ID (Width 19)
            html.AppendLine("<col width=\"152\" />"); // D: TIN (Width 19)
            html.AppendLine("<col width=\"152\" />"); // E: SI/OR (Width 19)
            html.AppendLine("<col width=\"152\" />"); // F: Sales (Width 19)
            html.AppendLine("<col width=\"152\" />"); // G: VAT Amount (Width 19)
            html.AppendLine("<col width=\"152\" />"); // H: VAT Exempt (Width 19)
            html.AppendLine("<col width=\"152\" />"); // I: Discount 5% (Width 19)
            html.AppendLine("<col width=\"64\" />");  // J: Discount 20% (Width 8)
            html.AppendLine("<col width=\"152\" />"); // K: Net Sales (Width 19)
            html.AppendLine("</colgroup>");

            html.AppendLine("<tr><td colspan=\"11\" class=\"b\">PUREGOLD PRICE CLUB INC.</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">STORE CODE : {options.StoreCode} {storeNameDisplay}</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">{first.Address}</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">{first.VatRegTin}</td></tr>");
            html.AppendLine("<tr><td colspan=\"11\" class=\"b\">PUREGOLD pos V.1.0.0</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">SERIAL# {first.SerialNumber}</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">MIN# {first.MinNumber}</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">Terminal# {terminalDisplay}</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">DATE GENERATED : {DateTime.Now:MM/dd/yyyy HH:mm:ss} | USER : {userIdDisplay}</td></tr>");

            html.AppendLine("<tr><td colspan=\"11\" style=\"height: 10px;\"></td></tr>");


            string titleStyle = "font-family: Calibri; font-size: 16pt; font-weight: bold; border: .5pt solid black; text-align: center; vertical-align: middle;";
            string title = typeName == "senior" ? "Senior Citizen Sales Book/Report" : "Persons with Disability Sales Book/Report";
            html.AppendLine($"<tr><td colspan=\"11\" align=\"center\" valign=\"middle\" style=\"{titleStyle}\">{title}</td></tr>");
            string baseHeaderStyle = "font-family: Calibri; font-size: 11pt; font-weight: bold; border: .5pt solid black; text-align: center; vertical-align: middle;";

            string personType = typeName == "senior" ? "Senior Citizen (SC)" : "Person with Disability (PWD)";
            string idType = typeName == "senior" ? "OSCA ID No./ SC ID No." : "PWD ID No.";
            string tinType = typeName == "senior" ? "SC TIN" : "PWD TIN";

            html.AppendLine("<tr>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #A5A5A5;\" rowspan=\"2\">Date</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #00FFFF;\" rowspan=\"2\">Name of {personType}</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #FFFF00;\" rowspan=\"2\">{idType}</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #9999FF;\" rowspan=\"2\">{tinType}</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #FFFF00;\" rowspan=\"2\">SI/OR Number</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #99FF99;\" rowspan=\"2\">Sales (inclusive of VAT)</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #FF9933;\" rowspan=\"2\">VAT Amount</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #CCCCFF;\" rowspan=\"2\">VAT Exempt Sales</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #FFFF00;\" colspan=\"2\">Discount</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #A5A5A5;\" rowspan=\"2\">Net Sales</td>");
            html.AppendLine("</tr>");

            html.AppendLine("<tr>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #FFFF00;\">5%</td>");
            html.AppendLine($"<td align=\"center\" valign=\"middle\" style=\"{baseHeaderStyle} background-color: #FFFF00;\">20%</td>");
            html.AppendLine("</tr>");

            foreach (var r in rows)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td class=\"c\">{r.Date:MM/dd/yyyy}</td>");
                html.AppendLine($"<td>{r.Name}</td>");
                html.AppendLine($"<td class=\"c\" style=\"mso-number-format:'\\@'\">{r.IdNo}</td>");
                html.AppendLine($"<td class=\"c\" style=\"mso-number-format:'\\@'\">{r.Tin}</td>");
                html.AppendLine($"<td class=\"c\" style=\"mso-number-format:'\\@'\">{r.Invoice}</td>");
                html.AppendLine($"<td class=\"r\" style=\"mso-number-format:'#\\,##0.00'\">{r.Sales:F2}</td>");
                html.AppendLine($"<td class=\"r\" style=\"mso-number-format:'#\\,##0.00'\">{r.Vat:F2}</td>");
                html.AppendLine($"<td class=\"r\" style=\"mso-number-format:'#\\,##0.00'\">{r.Exempt:F2}</td>");
                html.AppendLine($"<td class=\"r\" style=\"mso-number-format:'#\\,##0.00'\">{r.Discount5:F2}</td>");
                html.AppendLine("<td class=\"r\" style=\"mso-number-format:'#\\,##0.00'\">0.00</td>");
                html.AppendLine($"<td class=\"r\" style=\"mso-number-format:'#\\,##0.00'\">{r.NetSales:F2}</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</table>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            File.WriteAllText(outputPath, html.ToString(), Encoding.UTF8);
        }

        private List<List<T>> ChunkBy<T>(List<T> source, int chunkSize)
        {
            return source.Select((x, i) => new { Index = i, Value = x })
                         .GroupBy(x => x.Index / chunkSize)
                         .Select(x => x.Select(v => v.Value).ToList())
                         .ToList();
        }

        private bool DownloadFromPrimaryFtp(EJFilterOptions options, string dateStr, string tempDir, IProgress<string> progress)
        {
            bool connected = false;
            Exception lastException = null;
            string ftpHost = "192.168.200.177";

            using (Session session = new Session())
            {
                for (int i = 1; i <= 12; i++)
                {
                    try
                    {
                        session.Open(new SessionOptions { Protocol = Protocol.Ftp, HostName = ftpHost, UserName = $"ftp{i}@puregold", Password = "pw@1234" });
                        connected = true; break;
                    }
                    catch (Exception ex) { lastException = ex; }
                }

                if (!connected) { progress?.Report($"  [Failed] Primary FTP Connection Error: {lastException?.Message}"); return false; }

                try
                {
                    var files = session.ListDirectory("/PUREPOS_EJRECEIPT/").Files;
                    string targetFolder = files.FirstOrDefault(f => f.Name.StartsWith($"({options.StoreCode})"))?.Name;
                    if (string.IsNullOrEmpty(targetFolder)) return false;

                    string remoteZip = $"/PUREPOS_EJRECEIPT/{targetFolder}/{dateStr}_EJReceiptJournal.zip";
                    if (session.FileExists(remoteZip))
                    {
                        progress?.Report($"  Found Primary ZIP: {dateStr}_EJReceiptJournal.zip");
                        string localZip = Path.Combine(tempDir, "main.zip");
                        session.GetFiles(remoteZip, localZip).Check();

                        string innerDir = Path.Combine(tempDir, "Inner");
                        Directory.CreateDirectory(innerDir);
                        ExtractZipSafe(localZip, innerDir, progress);

                        bool processedAny = false;
                        var innerZips = Directory.GetFiles(innerDir, "*.zip", SearchOption.AllDirectories);
                        foreach (var zip in innerZips)
                        {
                            bool keep = options.PosLanes.Count == 0 || options.PosLanes.Any(lane => Regex.IsMatch(Path.GetFileName(zip), $@"(?i)_0*{lane}\.zip$"));
                            if (keep) { ExtractZipSafe(zip, tempDir, progress); processedAny = true; }
                        }

                        var txtFiles = Directory.GetFiles(innerDir, "*.txt", SearchOption.AllDirectories);
                        foreach (var txt in txtFiles)
                        {
                            bool keep = options.PosLanes.Count == 0 || options.PosLanes.Any(lane => Regex.IsMatch(Path.GetFileName(txt), $@"(?i)_0*{lane}\.txt$"));
                            if (keep) { File.Copy(txt, Path.Combine(tempDir, Path.GetFileName(txt)), true); processedAny = true; }
                        }
                        return processedAny;
                    }
                }
                catch (Exception ex) { progress?.Report($"  Primary FTP Error: {ex.Message}"); }
            }
            return false;
        }

        private bool DownloadFromFallbackSftp(EJFilterOptions options, string dateStr, string tempDir, IProgress<string> progress)
        {
            try
            {
                var globalSettings = SettingsViewModel.GetCurrentSettings();
                SessionOptions fallbackOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = options.LiveServerIp,
                    UserName = globalSettings.ConsoUser,
                    Password = globalSettings.ConsoPassword,
                    SshHostKeyPolicy = SshHostKeyPolicy.AcceptNew
                };

                using (Session session = new Session())
                {
                    session.Open(fallbackOptions);
                    string doneZipPath = $"/opt/purepos/ejreceipt/done/{dateStr}_EJReceiptJournal.zip";
                    if (session.FileExists(doneZipPath))
                    {
                        progress?.Report($"  Found Consolidated ZIP: {doneZipPath}");
                        string localZipPath = Path.Combine(tempDir, "merged_fallback.zip");
                        session.GetFiles(doneZipPath, localZipPath).Check();

                        string innerDir = Path.Combine(tempDir, "Inner");
                        Directory.CreateDirectory(innerDir);
                        ExtractZipSafe(localZipPath, innerDir, progress);

                        bool processedAny = false;
                        var innerZips = Directory.GetFiles(innerDir, "*.zip", SearchOption.AllDirectories);
                        foreach (var zip in innerZips)
                        {
                            bool keep = options.PosLanes.Count == 0 || options.PosLanes.Any(lane => Regex.IsMatch(Path.GetFileName(zip), $@"(?i)_0*{lane}\.zip$"));
                            if (keep) { ExtractZipSafe(zip, tempDir, progress); processedAny = true; }
                        }

                        var txtFiles = Directory.GetFiles(innerDir, "*.txt", SearchOption.AllDirectories);
                        foreach (var txt in txtFiles)
                        {
                            bool keep = options.PosLanes.Count == 0 || options.PosLanes.Any(lane => Regex.IsMatch(Path.GetFileName(txt), $@"(?i)_0*{lane}\.txt$"));
                            if (keep) { File.Copy(txt, Path.Combine(tempDir, Path.GetFileName(txt)), true); processedAny = true; }
                        }
                        if (processedAny) return true;
                    }

                    string targetFolder = $"/opt/purepos/ejreceipt/{dateStr}/";
                    if (session.FileExists(targetFolder))
                    {
                        var files = session.ListDirectory(targetFolder).Files.Where(f => !f.IsDirectory && (f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));
                        bool processedAny = false;

                        foreach (var file in files)
                        {
                            bool keep = options.PosLanes.Count == 0 || options.PosLanes.Any(lane => Regex.IsMatch(file.Name, $@"(?i)_0*{lane}\.(zip|txt)$"));
                            if (keep)
                            {
                                string localFilePath = Path.Combine(tempDir, file.Name);
                                session.GetFiles(targetFolder + file.Name, localFilePath).Check();
                                if (file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ExtractZipSafe(localFilePath, tempDir, progress);
                                processedAny = true;
                            }
                        }
                        return processedAny;
                    }
                }
            }
            catch (Exception ex) { progress?.Report($"  [Failed] Fallback Connection Error: {ex.Message}"); }
            return false;
        }

        private void ExtractZipSafe(string zipPath, string extractPath, IProgress<string> progress = null)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                        string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                        if (destinationPath.StartsWith(extractPath, StringComparison.Ordinal))
                        {
                            string targetDir = Path.GetDirectoryName(destinationPath);
                            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                            entry.ExtractToFile(destinationPath, true);
                        }
                    }
                }
            }
            catch (Exception ex) { progress?.Report($"  [Warning] Extractor: {ex.Message}"); }
        }
    }
}