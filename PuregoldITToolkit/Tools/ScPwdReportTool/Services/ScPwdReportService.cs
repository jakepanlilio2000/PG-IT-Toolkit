using PuregoldITToolkit.Tools.ScPwdReportTool.Interfaces;
using PuregoldITToolkit.Tools.ScPwdReportTool.Models;
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

namespace PuregoldITToolkit.Tools.ScPwdReportTool.Services
{
    public class ScPwdReportService : IScPwdReportService
    {
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

        private decimal ParseDecimal(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            input = input.Replace(",", "").Trim();
            decimal.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result);
            return result;
        }

        public async Task<int> GenerateReportsAsync(ScPwdReportOptions options, IProgress<string> textProgress, IProgress<int> pctProgress)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputDir = Path.Combine(desktopPath, "SC_PWD_Reports");
            Directory.CreateDirectory(outputDir);

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

                    textProgress?.Report($"Scanning Date: {dateStr}...");

                    if (!Directory.Exists(dateCacheDir) || !Directory.GetFiles(dateCacheDir, "*.txt", SearchOption.AllDirectories).Any())
                    {
                        Directory.CreateDirectory(dateCacheDir);
                        bool success = DownloadFromPrimaryFtp(options, dateStr, dateCacheDir, textProgress);
                        if (!success) DownloadFromFallbackSftp(options, dateStr, dateCacheDir, textProgress);
                    }

                    if (Directory.Exists(dateCacheDir))
                    {
                        textProgress?.Report($"  Parsing receipts for {dateStr}...");
                        ParseReportFiles(dateCacheDir, options, scReports, pwdReports);
                    }

                    currentDateIndex++;
                    pctProgress?.Report((int)((double)currentDateIndex / totalDates * 100));
                }

                if (options.GenerateScReport && !scReports.Any())
                    throw new Exception("No Senior Citizen (SC) data was found in the extracted EJ files for this date range.");

                if (options.GeneratePwdReport && !pwdReports.Any())
                    throw new Exception("No Person with Disability (PWD) data was found in the extracted EJ files for this date range.");

                if (options.GenerateScReport)
                {
                    textProgress?.Report("Generating SC Excel Reports...");
                    ExportExcelReports(scReports.ToList(), "senior", options, outputDir);
                }

                if (options.GeneratePwdReport)
                {
                    textProgress?.Report("Generating PWD Excel Reports...");
                    ExportExcelReports(pwdReports.ToList(), "pwd", options, outputDir);
                }
            });

            return scReports.Count + pwdReports.Count;
        }

        private void ParseReportFiles(string sourceDir, ScPwdReportOptions options, System.Collections.Concurrent.ConcurrentBag<ReportRow> scReports, System.Collections.Concurrent.ConcurrentBag<ReportRow> pwdReports)
        {
            var txtFiles = Directory.GetFiles(sourceDir, "*.txt", SearchOption.AllDirectories);
            if (txtFiles.Length == 0) return;

            // FIX: Merge all TXT files into a single string BEFORE parsing
            var sb = new StringBuilder();
            foreach (var file in txtFiles)
            {
                sb.AppendLine(File.ReadAllText(file, Encoding.UTF8));
            }
            string mergedContent = sb.ToString();

            var blocks = Regex.Split(mergedContent, @"(?=Puregold Price Club|SECOND RECEIPT)", RegexOptions.IgnoreCase);

            Parallel.ForEach(blocks, block =>
            {
                if (string.IsNullOrWhiteSpace(block)) return;
                if (block.IndexOf("SECOND RECEIPT", StringComparison.OrdinalIgnoreCase) >= 0) return;

                // More resilient detection for SC/PWD blocks
                bool isSc = Regex.IsMatch(block, @"(?i)OSCA\s*(?:ID\s*)?No:");
                bool isPwd = Regex.IsMatch(block, @"(?i)PWD\s*(?:ID\s*)?No:");

                if ((isSc && options.GenerateScReport) || (isPwd && options.GeneratePwdReport))
                {
                    var row = ParseReportRow(block, isSc);
                    if (row != null)
                    {
                        if (isSc) scReports.Add(row);
                        else pwdReports.Add(row);
                    }
                }
            });
        }

        private ReportRow ParseReportRow(string block, bool isSc)
        {
            try
            {
                var row = new ReportRow();

                // More flexible date matching
                string dateRaw = Regex.Match(block, @"(?i)(?:Trans\.?\s*Date|Date):\s*([0-9]{1,4}[-/][0-9]{1,2}[-/][0-9]{1,4})").Groups[1].Value;
                if (DateTime.TryParse(dateRaw, out DateTime dt)) row.Date = dt; else return null;

                row.Invoice = Regex.Match(block, @"INVOICE#\s*([\d\-]+)").Groups[1].Value.Trim();
                var invParts = row.Invoice.Split('-');
                row.Terminal = invParts.Length >= 2 ? invParts[1] : "000";

                row.SerialNumber = Regex.Match(block, @"S/N:([A-Z0-9]+)").Groups[1].Value.Trim();
                row.MinNumber = Regex.Match(block, @"MIN:(\d+)").Groups[1].Value.Trim();
                row.Cashier = Regex.Match(block, @"Cashier:\s*([A-Z\s]+)").Groups[1].Value.Trim();

                row.Sales = ParseDecimal(Regex.Match(block, @"Subtotal\s+([\d,\.]+)").Groups[1].Value);
                row.Vat = ParseDecimal(Regex.Match(block, @"VAT\s*\(12%\)\s+([\d,\.]+)").Groups[1].Value);
                row.Exempt = ParseDecimal(Regex.Match(block, @"VAT Exempt Sale\s*\(E\)\s+([\d,\.]+)").Groups[1].Value);

                var netMatch = Regex.Match(block, @"Total\s+(?:Amt\s+Due\s+)?Php\s+([\d,\.]+)");
                if (!netMatch.Success) netMatch = Regex.Match(block, @"Total\s+Php\s+([\d,\.]+)");
                row.NetSales = ParseDecimal(netMatch.Groups[1].Value);

                string discPattern = isSc ? @"SC Discount Applied:\s*([\d,\.]+)" : @"PWD Discount Applied:\s*([\d,\.]+)";
                row.Discount5 = ParseDecimal(Regex.Match(block, discPattern).Groups[1].Value);

                // Improved Name matching (handles "Member Name:" as well)
                var nameMatch = Regex.Match(block, @"(?i)(?:Member\s+)?Name:\s*([^\n\r]+)");
                row.Name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "";

                // Improved TIN matching
                //var tinMatch = Regex.Match(block, @"(?i)TIN:\s*([0-9\-]+)");
                row.Tin = "";

                // Resilient ID matching
                string idPattern = isSc ? @"(?i)OSCA\s*(?:ID\s*)?No:\s*([^\n\r]+)" : @"(?i)PWD\s*(?:ID\s*)?No:\s*([^\n\r]+)";
                row.IdNo = Regex.Match(block, idPattern).Groups[1].Value.Trim();

                var addressMatch = Regex.Match(block, @"Puregold Price Club, Inc\.?(.*?)(?:VAT REG TIN)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                row.Address = addressMatch.Success ? Regex.Replace(addressMatch.Groups[1].Value, @"\s+", " ").Trim() : "SAVER'S BLDG. MAC ARTHUR HIGHWAY DOLORES CITY OF SAN FERNANDO";

                // FIX: Specifically look for VAT REG TIN instead of blindly grabbing the first number/hyphen sequence
                var vatRegTinMatch = Regex.Match(block, @"(?i)VAT REG TIN[:\s]*([0-9\-]+)");
                row.VatRegTin = vatRegTinMatch.Success ? vatRegTinMatch.Groups[1].Value.Trim() : "";

                return row;
            }
            catch { return null; }
        }

        private void ExportExcelReports(List<ReportRow> allRows, string typeName, ScPwdReportOptions options, string outputDir)
        {
            var groupedByPos = allRows.GroupBy(r => r.Terminal).OrderBy(g => g.Key);
            var requestedDates = options.TargetDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
            var dateChunks = ChunkBy(requestedDates, options.ReportChunkDays);

            foreach (var posGroup in groupedByPos)
            {
                string term = posGroup.Key;
                var termRows = posGroup.ToList();
                var metadataRow = termRows.First();

                foreach (var chunk in dateChunks)
                {
                    DateTime minDate = chunk.Min();
                    DateTime maxDate = chunk.Max();
                    var chunkRows = termRows.Where(r => r.Date.Date >= minDate && r.Date.Date <= maxDate).OrderBy(r => r.Date).ToList();

                    string dateRangeStr = $"{minDate:MMdd}-{maxDate:MMdd}";
                    string fileName = $"{options.StoreCode}_{term}_{dateRangeStr}_{typeName}_report.xls";
                    string filePath = Path.Combine(outputDir, fileName);

                    GenerateHtmlExcelFile(chunkRows, metadataRow, typeName, options, filePath);
                }
            }
        }

        private void GenerateHtmlExcelFile(List<ReportRow> rows, ReportRow metadataRow, string typeName, ScPwdReportOptions options, string outputPath)
        {
            var html = new StringBuilder();

            html.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns=\"http://www.w3.org/TR/REC-html40\">");
            html.AppendLine("<head><meta charset=\"utf-8\">");
            html.AppendLine("<!--[if gte mso 9]><xml><x:ExcelWorkbook><x:ExcelWorksheets><x:ExcelWorksheet><x:Name>Report</x:Name><x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions></x:ExcelWorksheet></x:ExcelWorksheets></x:ExcelWorkbook></xml><![endif]-->");
            html.AppendLine("<style>body, table, td, th { font-family: Calibri; font-size: 11pt; } table { border-collapse: collapse; } .b { font-weight: bold; } .c { text-align: center; vertical-align: middle; } .r { text-align: right; }</style>");
            html.AppendLine("</head><body>");

            string storeNameDisplay = string.IsNullOrWhiteSpace(options.StoreName) ? "PUREGOLD JR - SAN FERNANDO" : options.StoreName;
            string terminalDisplay = int.TryParse(metadataRow.Terminal, out int termNum) ? termNum.ToString() : metadataRow.Terminal;
            string userIdDisplay = string.IsNullOrWhiteSpace(options.ReportUserId) ? "87113" : options.ReportUserId;

            html.AppendLine("<table cellpadding=\"0\" cellspacing=\"0\">");
            html.AppendLine("<colgroup><col width=\"64\"/><col width=\"152\"/><col width=\"152\"/><col width=\"152\"/><col width=\"152\"/><col width=\"152\"/><col width=\"152\"/><col width=\"152\"/><col width=\"152\"/><col width=\"64\"/><col width=\"152\"/></colgroup>");

            html.AppendLine("<tr><td colspan=\"11\" class=\"b\">PUREGOLD PRICE CLUB INC.</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">STORE CODE : {options.StoreCode} {storeNameDisplay}</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">{metadataRow.Address}</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">{metadataRow.VatRegTin}</td></tr>");
            html.AppendLine("<tr><td colspan=\"11\" class=\"b\">PUREGOLD pos V.1.0.0</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">SERIAL# {metadataRow.SerialNumber}</td></tr>");
            html.AppendLine($"<tr><td colspan=\"11\" class=\"b\">MIN# {metadataRow.MinNumber}</td></tr>");
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
            html.AppendLine("</tr><tr>");
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

            html.AppendLine("</table></body></html>");
            File.WriteAllText(outputPath, html.ToString(), Encoding.UTF8);
        }

        private List<List<T>> ChunkBy<T>(List<T> source, int chunkSize)
        {
            if (chunkSize <= 0) chunkSize = 1;
            if (!source.Any()) return new List<List<T>>();
            return source.Select((x, i) => new { Index = i, Value = x })
                         .GroupBy(x => x.Index / chunkSize)
                         .Select(x => x.Select(v => v.Value).ToList()).ToList();
        }

        private bool DownloadFromPrimaryFtp(ScPwdReportOptions options, string dateStr, string tempDir, IProgress<string> progress)
        {
            string ftpHost = "192.168.200.177";
            using (Session session = new Session())
            {
                for (int i = 1; i <= 12; i++)
                {
                    try
                    {
                        session.Open(new SessionOptions { Protocol = Protocol.Ftp, HostName = ftpHost, UserName = $"ftp{i}@puregold", Password = "pw@1234" });
                        var files = session.ListDirectory("/PUREPOS_EJRECEIPT/").Files;
                        string targetFolder = files.FirstOrDefault(f => f.Name.StartsWith($"({options.StoreCode})"))?.Name;
                        if (string.IsNullOrEmpty(targetFolder)) return false;

                        string remoteZip = $"/PUREPOS_EJRECEIPT/{targetFolder}/{dateStr}_EJReceiptJournal.zip";
                        if (session.FileExists(remoteZip))
                        {
                            progress?.Report($"  Found Primary ZIP: {dateStr}_EJReceiptJournal.zip");
                            string localZip = Path.Combine(tempDir, "main.zip");
                            session.GetFiles(remoteZip, localZip).Check();

                            // FIX: Extract to an Inner directory first to prevent collisions
                            string innerDir = Path.Combine(tempDir, "Inner");
                            Directory.CreateDirectory(innerDir);
                            ExtractZipSafe(localZip, innerDir, progress);

                            bool processedAny = false;
                            var innerZips = Directory.GetFiles(innerDir, "*.zip", SearchOption.AllDirectories);
                            foreach (var zip in innerZips)
                            {
                                ExtractZipSafe(zip, tempDir, progress);
                                processedAny = true;
                            }

                            var txtFiles = Directory.GetFiles(innerDir, "*.txt", SearchOption.AllDirectories);
                            foreach (var txt in txtFiles)
                            {
                                File.Copy(txt, Path.Combine(tempDir, Path.GetFileName(txt)), true);
                                processedAny = true;
                            }
                            return processedAny;
                        }
                    }
                    catch (Exception ex) { progress?.Report($"  Primary FTP Error: {ex.Message}"); }
                }
            }
            return false;
        }

        private bool DownloadFromFallbackSftp(ScPwdReportOptions options, string dateStr, string tempDir, IProgress<string> progress)
        {
            try
            {
                var globalSettings = SettingsViewModel.GetCurrentSettings();
                using (Session session = new Session())
                {
                    session.Open(new SessionOptions { Protocol = Protocol.Sftp, HostName = options.LiveServerIp, UserName = globalSettings.ConsoUser, Password = globalSettings.ConsoPassword, SshHostKeyPolicy = SshHostKeyPolicy.AcceptNew });

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
                            ExtractZipSafe(zip, tempDir, progress);
                            processedAny = true;
                        }

                        var txtFiles = Directory.GetFiles(innerDir, "*.txt", SearchOption.AllDirectories);
                        foreach (var txt in txtFiles)
                        {
                            File.Copy(txt, Path.Combine(tempDir, Path.GetFileName(txt)), true);
                            processedAny = true;
                        }
                        if (processedAny) return true;
                    }

                    string targetFolder = $"/opt/purepos/ejreceipt/{dateStr}/";
                    if (session.FileExists(targetFolder))
                    {
                        progress?.Report($"  Found Date Folder: {targetFolder}");
                        var files = session.ListDirectory(targetFolder).Files.Where(f => !f.IsDirectory && (f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));

                        bool processedAny = false;
                        foreach (var file in files)
                        {
                            progress?.Report($"  Downloading POS File: {file.Name}");
                            string localFilePath = Path.Combine(tempDir, file.Name);
                            session.GetFiles(targetFolder + file.Name, localFilePath).Check();

                            if (file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                ExtractZipSafe(localFilePath, tempDir, progress);
                            }
                            processedAny = true;
                        }
                        return processedAny;
                    }
                    progress?.Report($"  No folders or files found for {dateStr} in Fallback Server.");
                }
            }
            catch (Exception ex) { progress?.Report($"  [Failed] Fallback Connection Error: {ex.Message}"); }
            return false;
        }

        // FIX: Use entry.FullName to preserve directory structure and prevent file collisions
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