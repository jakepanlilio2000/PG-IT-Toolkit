using PuregoldITToolkit.Tools.PureposTool.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.PureposTool.Services
{
    public class PureposService : IPureposService
    {
        private readonly string _cacheFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EjUploadCache.json");

        private readonly string[] _ftpUsers = new[]
        {
            "puregold/ftp1", "puregold/ftp1a", "puregold/ftp1b", "puregold/ftp1c",
            "puregold/ftp2", "puregold/ftp3", "puregold/ftp4", "puregold/ftp5",
            "puregold/ftp6", "puregold/ftp7", "puregold/ftp8", "puregold/ftp9",
            "puregold/ftp10", "puregold/ftp11"
        };

        public async Task RunAutoPermissionAsync(AutoPermissionModel config, Action<string> logCallback)
        {
            var globalSettings = SettingsViewModel.GetCurrentSettings();
            string ipPrefix = config.LiveServerIp.Substring(0, config.LiveServerIp.LastIndexOf('.') + 1);
            var tasks = new List<Task>();

            logCallback("\nFiring permission commands in parallel...");

            for (int i = 1; i <= config.TotalLanes; i++)
            {
                int lane = i;
                string posIp = $"{ipPrefix}{50 + lane}";
                tasks.Add(Task.Run(() =>
                {
                    ExecuteSshCommandSync(posIp, globalSettings.PosUsername, globalSettings.PosPassword, "sh /opt/purepos/scripts/permission.sh", logCallback);
                }));
            }

            tasks.Add(Task.Run(() =>
            {
                ExecuteSshCommandSync(config.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword, "sh /opt/purepos/scripts/permission.sh", logCallback);
            }));

            await Task.WhenAll(tasks);
            logCallback("\n✅ AUTO PERMISSION COMPLETED SUCCESSFULLY.");
        }

        public async Task RunBirEisSyncAsync(BirEisModel config, Action<string> logCallback)
        {
            await Task.Run(() =>
            {
                try
                {
                    var globalSettings = SettingsViewModel.GetCurrentSettings();
                    string yyyy = config.TargetDate.ToString("yyyy");
                    string yyyymmdd = config.TargetDate.ToString("yyyyMMdd");
                    string fileName = $"EIS_{config.StoreCode}_{yyyymmdd}.txt";
                    string remoteSourcePath = $"/opt/iPos/CONSO_EIS/{yyyy}/{fileName}";

                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string birPath = Path.Combine(desktopPath, "BIR_Output");
                    if (!Directory.Exists(birPath)) Directory.CreateDirectory(birPath);

                    string localFilePath = Path.Combine(birPath, fileName);

                    logCallback($"\n==================================================");
                    logCallback($"           BIR-EIS AUTO SYNC INITIATED            ");
                    logCallback($"==================================================");
                    logCallback($"[*] Target File: {fileName}");
                    logCallback($"\n[*] Connecting to CONSO SFTP ({config.LiveServerIp})...");
                    using (var sftp = new SftpClient(config.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword))
                    {
                        sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
                        sftp.Connect();

                        if (!sftp.Exists(remoteSourcePath))
                        {
                            logCallback($"[ERROR] Target file not found on CONSO: {remoteSourcePath}");
                            sftp.Disconnect();
                            return;
                        }

                        logCallback($"[+] File located. Downloading to Desktop/BIR_Output...");
                        using (var fs = new FileStream(localFilePath, FileMode.Create))
                        {
                            sftp.DownloadFile(remoteSourcePath, fs);
                        }
                        logCallback($"[+] Download successful: {localFilePath}");

                        sftp.Disconnect();
                    }
                    logCallback($"[*] Processing EIS file and generating BIR reports on Windows...");
                    ProcessEisFileForBIR(localFilePath, birPath, logCallback);
                    string ftpHost = string.IsNullOrWhiteSpace(globalSettings.DefaultFtpServer) ? "192.168.200.177" : globalSettings.DefaultFtpServer;
                    string ftpTargetUrl = $"ftp://{ftpHost}/toho/ANAHAW-BIR-EIS/{fileName}";

                    logCallback($"\n[*] Uploading to Main FTP: {ftpTargetUrl}...");
                    bool ftpSuccess = false;
                    foreach (var user in _ftpUsers)
                    {
                        try
                        {
                            var creds = new System.Net.NetworkCredential(user, globalSettings.FtpPassword);
                            UploadToFtp(ftpTargetUrl, localFilePath, creds);
                            logCallback($"[+] Upload successful using FTP account: {user}");
                            ftpSuccess = true;
                            break;
                        }
                        catch { /* Silently fail and try next credential */ }
                    }

                    if (!ftpSuccess)
                    {
                        logCallback("[ERROR] Failed to upload to FTP. Authentication or network failure.");
                    }

                    logCallback("\n✅ BIR-EIS SYNC PROCESS COMPLETED.");
                }
                catch (Exception ex)
                {
                    logCallback($"\n[FATAL ERROR] {ex.Message}");
                }
            });
        }

        private void ProcessEisFileForBIR(string eisFilePath, string outputDir, Action<string> logCallback)
        {
            try
            {
                logCallback($"[*] Reading EIS file: {eisFilePath}");
                string[] lines = File.ReadAllLines(eisFilePath);
                if (lines.Length == 0)
                {
                    logCallback("[WARNING] EIS file is empty");
                    return;
                }
                string header = lines[0];
                string[] headers = header.Split('|');
                var transactions = new List<Dictionary<string, string>>();
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    string[] values = lines[i].Split('|');
                    var row = new Dictionary<string, string>();

                    for (int j = 0; j < headers.Length && j < values.Length; j++)
                    {
                        row[headers[j].Trim()] = values[j].Trim();
                    }

                    transactions.Add(row);
                }

                logCallback($"[*] Processed {transactions.Count} transactions");
                GenerateBIRSalesReport(transactions, outputDir, logCallback);
                GenerateBIRVATReport(transactions, outputDir, logCallback);

                logCallback($"[+] BIR reports generated successfully in: {outputDir}");
            }
            catch (Exception ex)
            {
                logCallback($"[ERROR] Failed to process EIS file: {ex.Message}");
            }
        }

        private void GenerateBIRSalesReport(List<Dictionary<string, string>> transactions, string outputDir, Action<string> logCallback)
        {
            try
            {
                string fileName = $"BIR_Sales_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(outputDir, fileName);

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("BIR SALES REPORT");
                    writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine(new string('=', 80));
                    writer.WriteLine();
                    var grouped = transactions.GroupBy(t => $"{t["TrxType"]}|{t["RegNo"]}");

                    foreach (var group in grouped)
                    {
                        string[] keyParts = group.Key.Split('|');
                        string trxType = keyParts[0];
                        string regNo = keyParts[1];

                        writer.WriteLine($"Transaction Type: {trxType} | Register: {regNo}");
                        writer.WriteLine(new string('-', 60));

                        decimal totalSales = 0;
                        decimal totalVat = 0;

                        foreach (var txn in group)
                        {
                            string trxNo = txn.ContainsKey("TrxNo") ? txn["TrxNo"] : "N/A";
                            string invNo = txn.ContainsKey("InvNo") ? txn["InvNo"] : "N/A";
                            string slsAmt = txn.ContainsKey("SlsAmt") ? txn["SlsAmt"] : "0";
                            string vatAmt = txn.ContainsKey("VatAmt") ? txn["VatAmt"] : "0";

                            decimal sales = decimal.TryParse(slsAmt, out var s) ? s : 0;
                            decimal vat = decimal.TryParse(vatAmt, out var v) ? v : 0;

                            totalSales += sales;
                            totalVat += vat;

                            writer.WriteLine($"  {trxNo} | {invNo} | Sales: {sales:F2} | VAT: {vat:F2}");
                        }

                        writer.WriteLine(new string('-', 60));
                        writer.WriteLine($"  TOTALS - Sales: {totalSales:F2} | VAT: {totalVat:F2}");
                        writer.WriteLine();
                    }
                }

                logCallback($"[+] Sales report saved: {filePath}");
            }
            catch (Exception ex)
            {
                logCallback($"[ERROR] Failed to generate sales report: {ex.Message}");
            }
        }

        private void GenerateBIRVATReport(List<Dictionary<string, string>> transactions, string outputDir, Action<string> logCallback)
        {
            try
            {
                string fileName = $"BIR_VAT_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(outputDir, fileName);

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("BIR VAT REPORT");
                    writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine(new string('=', 80));
                    writer.WriteLine();

                    decimal totalVatSales = 0;
                    decimal totalVatAmount = 0;
                    decimal totalVatExempt = 0;
                    decimal totalZeroRated = 0;

                    writer.WriteLine($"{"Transaction No",-25} {"VAT Sales",15} {"VAT Amount",15} {"VAT Exempt",15} {"Zero Rated",15}");
                    writer.WriteLine(new string('-', 85));

                    foreach (var txn in transactions)
                    {
                        string trxNo = txn.ContainsKey("TrxNo") ? txn["TrxNo"] : "N/A";
                        string vatSales = txn.ContainsKey("VatSales") ? txn["VatSales"] : "0";
                        string vatAmt = txn.ContainsKey("VatAmt") ? txn["VatAmt"] : "0";
                        string vatExempt = txn.ContainsKey("VatEx") ? txn["VatEx"] : "0";
                        string zeroRated = txn.ContainsKey("ZeroSales") ? txn["ZeroSales"] : "0";

                        decimal vs = decimal.TryParse(vatSales, out var v) ? v : 0;
                        decimal va = decimal.TryParse(vatAmt, out var a) ? a : 0;
                        decimal ve = decimal.TryParse(vatExempt, out var e) ? e : 0;
                        decimal zr = decimal.TryParse(zeroRated, out var z) ? z : 0;

                        totalVatSales += vs;
                        totalVatAmount += va;
                        totalVatExempt += ve;
                        totalZeroRated += zr;

                        writer.WriteLine($"{trxNo,-25} {vs,15:F2} {va,15:F2} {ve,15:F2} {zr,15:F2}");
                    }

                    writer.WriteLine(new string('=', 85));
                    writer.WriteLine($"{"TOTALS",-25} {totalVatSales,15:F2} {totalVatAmount,15:F2} {totalVatExempt,15:F2} {totalZeroRated,15:F2}");
                }

                logCallback($"[+] VAT report saved: {filePath}");
            }
            catch (Exception ex)
            {
                logCallback($"[ERROR] Failed to generate VAT report: {ex.Message}");
            }
        }

        #region BIR EIS Generation Helpers

        private string GenerateBirEisContent(string consolidatedText, string storeCode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Store|TIN|Addr|TrxDate|TrxType|RegNo|TrxNo|InvNo|SlsAmt|VatSales|VatAmt|VatEx|ZeroSales|Rdisc|SCDisc|PWDDisc|MIN|SerialNo|PTU");
            var blocks = consolidatedText.Split(new[] { "Puregold Price Club, Inc" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                if (!block.Contains("Trans. Date:")) continue;
                if (block.Contains("SECOND RECEIPT")) continue;
                string tin = ExtractRegex(block, @"VAT REG TIN:\s*([0-9\-]+)");
                string addr = ExtractAddress(block);
                string trxDateStr = ExtractRegex(block, @"Trans\.\s*Date:\s*([\d\-]+\s+[\d:]+)");
                string trxDate = "";
                if (DateTime.TryParse(trxDateStr, out DateTime trxDateTime))
                {
                    trxDate = trxDateTime.ToString("MM/dd/yyyy HH:mm:ss");
                }
                string trxType = block.Contains("RETURN SALE") ? "RETURN" : "SALE";
                string regNoRaw = ExtractRegex(block, @"INVOICE#\s*\d{3}-(\d{3})-\d+");
                string regNo = int.TryParse(regNoRaw, out int r) ? r.ToString() : regNoRaw;
                string trxNo = ExtractRegex(block, @"Trans\s*#\s*(\d+)");
                string invNo = ExtractRegex(block, @"INVOICE#\s*([\d\-]+)");
                string slsAmt = ExtractAmount(block, @"Total Amt Due\s*(?:Php)?\s*([\d,\.]+)");
                string vatSales = ExtractAmount(block, @"VATable Sale \(V\)\s*([\d,\.]+)");
                string vatAmt = ExtractAmount(block, @"VAT \(12%\)\s*([\d,\.]+)");
                string vatEx = ExtractAmount(block, @"VAT Exempt Sale \(E\)\s*([\d,\.]+)");
                string zeroSales = ExtractAmount(block, @"Zero Rated Sale \(Z\)\s*([\d,\.]+)");
                string scDisc = ExtractAmount(block, @"SC Discount Applied:\s*([\d,\.]+)", "0");
                string pwdDisc = ExtractAmount(block, @"PWD Discount Applied:\s*([\d,\.]+)", "0");
                string min = ExtractRegex(block, @"MIN:\s*([A-Z0-9]+)");
                string serialNo = ExtractRegex(block, @"S/N:\s*([A-Z0-9]+)");
                string ptu = ExtractRegex(block, @"PTU:\s*([A-Z0-9\-]+)");
                sb.AppendLine($"{storeCode}|{tin}|{addr}|{trxDate}|{trxType}|{regNo}|{trxNo}|{invNo}|{slsAmt}|{vatSales}|{vatAmt}|{vatEx}|{zeroSales}|0|{scDisc}|{pwdDisc}|0|{min}|{serialNo}|{ptu}");
            }

            return sb.ToString();
        }

        private string ExtractAddress(string block)
        {
            var match = Regex.Match(block, @"Puregold Price Club, Inc\s*\n(.*?)\n\s*VAT REG TIN:", RegexOptions.Singleline);
            if (match.Success)
            {
                return Regex.Replace(match.Groups[1].Value.Trim(), @"\s*\n\s*", " ");
            }
            return "";
        }

        private string ExtractRegex(string block, string pattern)
        {
            var match = Regex.Match(block, pattern);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private string ExtractAmount(string block, string pattern, string defaultValue = "0")
        {
            var match = Regex.Match(block, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value.Replace(",", "");
            }
            return defaultValue;
        }

        #endregion

        private void ExecuteSshCommandSync(string host, string username, string password, string command, Action<string> logCallback)
        {
            try
            {
                using (var client = new SshClient(host, username, password))
                {
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
                    client.Connect();
                    var cmd = client.CreateCommand(command);
                    string result = cmd.Execute();
                    string error = cmd.Error;

                    if (cmd.ExitStatus == 0)
                    {
                        logCallback($"[SUCCESS] {host}:\n{result.Trim()}\n{error.Trim()}".Trim());
                    }
                    else
                    {
                        logCallback($"[ERROR] {host} (Exit {cmd.ExitStatus}):\n{error.Trim()}\n{result.Trim()}".Trim());
                    }

                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                logCallback($"[FAILED] {host}: {ex.Message}");
            }
        }

        public async Task<string> RunSshCommandAsync(string host, string username, string password, string command)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var client = new SshClient(host, username, password))
                    {
                        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
                        client.Connect();
                        var cmd = client.CreateCommand(command);
                        string result = cmd.Execute();
                        string error = cmd.Error;
                        client.Disconnect();

                        if (cmd.ExitStatus != 0)
                        {
                            return $"[ERROR {host}] Exit Code {cmd.ExitStatus}:\n{error.Trim()}\n{result.Trim()}".Trim();
                        }

                        return $"[SUCCESS {host}]:\n{result.Trim()}\n{error.Trim()}".Trim();
                    }
                }
                catch (Exception ex)
                {
                    return $"[FAILED {host}]: {ex.Message}";
                }
            });
        }

        public async Task RunEodGeneratorAsync(EodModel config, Action<string> logCallback)
        {
            await Task.Run(() =>
            {
                try
                {
                    logCallback("\n==================================================");
                    logCallback("           EOD POLLOG AUTOMATION TOOL             ");
                    logCallback("==================================================");

                    var globalSettings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();

                    DateTime manilaTime = DateTime.UtcNow.AddHours(8);
                    DateTime targetDate = config.UseYesterday ? manilaTime.AddDays(-1) : manilaTime;

                    logCallback($"[i] DATE MODE: Using {(config.UseYesterday ? "yesterday's" : "today's")} date (Manila Time).");

                    string fileDate = targetDate.ToString("yyyyMMdd");
                    string emailDate = targetDate.ToString("MM-dd-yy");

                    string dosFilename = $"{config.StoreCode}_{fileDate}.dos";
                    string zipFilename = $"{config.StoreCode}_{fileDate}.zip";

                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string dosFilepath = Path.Combine(desktopPath, dosFilename);
                    string zipFilepath = Path.Combine(desktopPath, zipFilename);

                    logCallback("[*] Cleaning up old POLLOG remnants from Desktop...");
                    int cleanedCount = 0;
                    foreach (var file in Directory.GetFiles(desktopPath))
                    {
                        string fileName = Path.GetFileName(file);
                        if (fileName.StartsWith($"{config.StoreCode}_") && (fileName.EndsWith(".dos") || fileName.EndsWith(".zip")))
                        {
                            if (fileName != dosFilename && fileName != zipFilename)
                            {
                                try { File.Delete(file); logCallback($"[-] Deleted: {fileName}"); cleanedCount++; } catch { }
                            }
                        }
                    }
                    if (cleanedCount == 0) logCallback("[-] No old remnants found to clean.");

                    logCallback($"\n[*] Connecting to {config.LiveServerIp} to download {dosFilename}...");
                    using (var sftp = new Renci.SshNet.SftpClient(config.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword))
                    {
                        sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
                        sftp.Connect();

                        string remoteFile = $"/opt/purepos/pollog/done/{dosFilename}";
                        if (sftp.Exists(remoteFile))
                        {
                            using (var fs = new FileStream(dosFilepath, FileMode.Create))
                            {
                                sftp.DownloadFile(remoteFile, fs);
                            }
                            logCallback("[+] Validation passed. File downloaded successfully.");
                        }
                        sftp.Disconnect();
                    }

                    if (!File.Exists(dosFilepath))
                    {
                        logCallback("\n[!] ERROR: POLLOG file not found on the server.");
                        logCallback("[*] Opening IT Tools portal in your web browser...");
                        Process.Start(new ProcessStartInfo { FileName = $"http://{config.LiveServerIp}/IT_TOOLS/index.php", UseShellExecute = true });
                        logCallback("\n==================================================");
                        logCallback("                 ACTION REQUIRED                  ");
                        logCallback("==================================================");
                        logCallback(" 1. A webpage has just been opened in your browser.");
                        logCallback(" 2. Please toggle the 'End Of the day' option.");
                        logCallback(" 3. Wait for the server process to finish.");
                        logCallback(" 4. Run this tool again.");
                        return;
                    }

                    logCallback("[*] Zipping file to Desktop...");
                    if (File.Exists(zipFilepath)) File.Delete(zipFilepath);
                    using (var archive = System.IO.Compression.ZipFile.Open(zipFilepath, System.IO.Compression.ZipArchiveMode.Create))
                    {
                        archive.CreateEntryFromFile(dosFilepath, dosFilename, System.IO.Compression.CompressionLevel.Optimal);
                    }

                    logCallback("[*] Preparing email...");

                    System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage
                    {
                        From = new System.Net.Mail.MailAddress(globalSettings.SmtpUser, globalSettings.SenderName)
                    };

                    if (config.TestModeEmail)
                    {
                        mail.To.Add(new System.Net.Mail.MailAddress(globalSettings.SmtpUser));
                        logCallback("[i] EMAIL MODE: TEST");
                    }
                    else
                    {
                        mail.To.Add("AllITDataControllersMMS@puregold.com.ph");
                        mail.CC.Add("jymendoza@puregold.com.ph");
                        mail.CC.Add("allITzone11@puregold.com.ph");
                    }

                    mail.Subject = $"{config.StoreCode} - {config.StoreName} - EOD File Purepos POLLOG {emailDate}";
                    mail.IsBodyHtml = true;

                    string htmlContent = $@"
                        <!DOCTYPE html>
                        <html>
                          <body>
                            <p>Masayang Araw!<br><br>
                              Please see attached POLLOG file for {config.StoreCode} - {config.StoreName}<br><br>
                              Store Manager: {config.StoreManager}<br>
                              Store Officers: {config.StoreOfficer}</p>
                            <br/>
                            {globalSettings.SignatureHtml}
                          </body>
                        </html>";

                    mail.Body = htmlContent;
                    mail.Attachments.Add(new System.Net.Mail.Attachment(zipFilepath));

                    logCallback($"[*] Sending email via SMTP ({globalSettings.SmtpServer})....");
                    using (var smtp = new System.Net.Mail.SmtpClient(globalSettings.SmtpServer, globalSettings.SmtpPort))
                    {
                        smtp.Credentials = new System.Net.NetworkCredential(globalSettings.SmtpUser, globalSettings.SmtpPass);
                        smtp.EnableSsl = true;
                        System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                        smtp.Send(mail);
                    }
                    logCallback("[+] Email successfully sent out over the network.");

                    logCallback("[*] Injecting copy into Thunderbird Local Sent folder...");
                    if (!string.IsNullOrWhiteSpace(globalSettings.ThunderbirdSentPath) && Directory.Exists(Path.GetDirectoryName(globalSettings.ThunderbirdSentPath)))
                    {
                        string boundary = "----=_Part_" + Guid.NewGuid().ToString("N");
                        StringBuilder mboxContent = new StringBuilder();

                        mboxContent.Append($"From - {manilaTime.ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}\n");
                        mboxContent.AppendLine($"From: {mail.From}");
                        mboxContent.AppendLine($"To: {mail.To}");

                        if (mail.CC.Count > 0)
                        {
                            var ccList = mail.CC.Select(c => c.Address).ToArray();
                            mboxContent.AppendLine($"Cc: {string.Join(", ", ccList)}");
                        }

                        mboxContent.AppendLine($"Subject: {mail.Subject}");
                        mboxContent.AppendLine($"Date: {manilaTime:R}");
                        mboxContent.AppendLine("MIME-Version: 1.0");
                        mboxContent.AppendLine($"Content-Type: multipart/mixed; boundary=\"{boundary}\"");
                        mboxContent.AppendLine();

                        mboxContent.AppendLine($"--{boundary}");
                        mboxContent.AppendLine("Content-Type: text/html; charset=utf-8");
                        mboxContent.AppendLine();
                        mboxContent.AppendLine(htmlContent);
                        mboxContent.AppendLine();

                        if (File.Exists(zipFilepath))
                        {
                            byte[] zipBytes = File.ReadAllBytes(zipFilepath);
                            string base64Zip = Convert.ToBase64String(zipBytes, Base64FormattingOptions.InsertLineBreaks);

                            mboxContent.AppendLine($"--{boundary}");
                            mboxContent.AppendLine($"Content-Type: application/zip; name=\"{zipFilename}\"");
                            mboxContent.AppendLine("Content-Transfer-Encoding: base64");
                            mboxContent.AppendLine($"Content-Disposition: attachment; filename=\"{zipFilename}\"");
                            mboxContent.AppendLine();
                            mboxContent.AppendLine(base64Zip);
                        }

                        mboxContent.AppendLine($"--{boundary}--");
                        mboxContent.AppendLine();

                        File.AppendAllText(globalSettings.ThunderbirdSentPath, mboxContent.ToString());
                        logCallback($"[+] Local copy written to {globalSettings.ThunderbirdSentPath}");
                    }
                    else
                    {
                        logCallback("[!] Warning: Thunderbird MBOX path is invalid or not configured. Skipped local injection.");
                    }

                    logCallback("\n[+] SUCCESS! Entire EOD POLLOG process is complete.\n");
                }
                catch (Exception ex)
                {
                    logCallback($"\n[!] Failed: {ex.Message}\n");
                }
            });
        }

        public async Task RunManualEjSaveAsync(EjSaveModel config, Action<string> logCallback)
        {
            await Task.Run(() =>
            {
                try
                {
                    var globalSettings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();

                    logCallback("Scanning FTP Accounts for Store Directory...");
                    string targetFtpDir = GetFtpStoreDirectory(config.FtpServer, config.StoreCode, out NetworkCredential validCreds, logCallback);

                    if (string.IsNullOrEmpty(targetFtpDir) || validCreds == null)
                    {
                        logCallback($"\n[ERROR] Could not find folder containing '({config.StoreCode})' across any FTP accounts.");
                        return;
                    }

                    logCallback($"\nFound target FTP directory: {targetFtpDir}\nAuthenticated using FTP account: {validCreds.UserName}");
                    HashSet<string> cache = LoadCache();

                    logCallback("Scanning FTP for existing EJ Zips...");
                    HashSet<string> existingFtpFiles = GetFtpFiles(targetFtpDir, validCreds);

                    List<DateTime> datesToProcess = new List<DateTime>();
                    for (DateTime date = config.DateFrom.Date; date <= config.DateTo.Date; date = date.AddDays(1))
                    {
                        string dateStr = date.ToString("MMddyyyy");
                        string expectedZip = $"{dateStr}_EJReceiptJournal.zip";

                        if (cache.Contains(dateStr) || existingFtpFiles.Contains(expectedZip))
                        {
                            cache.Add(dateStr);
                            continue;
                        }
                        datesToProcess.Add(date);
                    }

                    if (datesToProcess.Count == 0)
                    {
                        SaveCache(cache);
                        logCallback("\n✅ ALL EJ FILES ARE UP TO DATE. No missing dates found.");
                        return;
                    }

                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                    using (var sftp = new Renci.SshNet.SftpClient(config.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword))
                    {
                        logCallback($"\nConnecting to CONSO SFTP ({config.LiveServerIp})...");
                        sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
                        sftp.Connect();

                        foreach (var date in datesToProcess)
                        {
                            string dateStr = date.ToString("MMddyyyy");
                            string remoteEjPath = $"/opt/purepos/ejreceipt/{dateStr}";
                            string localTempDir = Path.Combine(desktopPath, $"TEMP_EJ_{dateStr}");
                            string localZipFile = Path.Combine(desktopPath, $"{dateStr}_EJReceiptJournal.zip");

                            logCallback($"\nProcessing missing date: {dateStr}");

                            if (!sftp.Exists(remoteEjPath))
                            {
                                logCallback($"[WARNING] No EJ folder found on Conso for {dateStr}. Skipping.");
                                continue;
                            }

                            Directory.CreateDirectory(localTempDir);
                            logCallback($"Downloading EJ files from Conso...");
                            var files = sftp.ListDirectory(remoteEjPath).Where(f => !f.IsDirectory);
                            foreach (var file in files)
                            {
                                using (var fileStream = File.OpenWrite(Path.Combine(localTempDir, file.Name)))
                                {
                                    sftp.DownloadFile(file.FullName, fileStream);
                                }
                            }

                            logCallback($"Zipping files...");
                            if (File.Exists(localZipFile)) File.Delete(localZipFile);
                            System.IO.Compression.ZipFile.CreateFromDirectory(localTempDir, localZipFile);

                            logCallback($"Uploading {dateStr}_EJReceiptJournal.zip to FTP...");
                            UploadToFtp($"{targetFtpDir}/{Path.GetFileName(localZipFile)}", localZipFile, validCreds);

                            Directory.Delete(localTempDir, true);
                            File.Delete(localZipFile);

                            cache.Add(dateStr);
                            SaveCache(cache);
                            logCallback($"[SUCCESS] {dateStr} uploaded and cleaned up!");
                        }

                        sftp.Disconnect();
                    }

                    logCallback("\n✅ MANUAL EJ SAVE COMPLETED.");
                }
                catch (Exception ex)
                {
                    logCallback($"\n[FATAL ERROR] {ex.Message}");
                }
            });
        }

        private string GetFtpStoreDirectory(string ftpIp, string storeCode, out NetworkCredential workingCreds, Action<string> logCallback)
        {
            workingCreds = null;
            string ftpUrl = $"ftp://{ftpIp}/PUREPOS_EJRECEIPT/";
            var globalSettings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();

            foreach (var user in _ftpUsers)
            {
                try
                {
                    var creds = new NetworkCredential(user, globalSettings.FtpPassword);
                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                    request.Method = WebRequestMethods.Ftp.ListDirectory;
                    request.Credentials = creds;
                    request.Timeout = 3000;

                    using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Contains($"({storeCode})"))
                            {
                                workingCreds = creds;
                                string folderName = Path.GetFileName(line);
                                return $"{ftpUrl}{folderName}";
                            }
                        }
                    }
                }
                catch (WebException) { }
            }
            return null;
        }

        private HashSet<string> GetFtpFiles(string ftpDirUrl, NetworkCredential creds)
        {
            var files = new HashSet<string>();
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpDirUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = creds;

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        files.Add(Path.GetFileName(line));
                    }
                }
            }
            catch { }
            return files;
        }

        private void UploadToFtp(string targetUrl, string localFilePath, NetworkCredential creds)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(targetUrl);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = creds;
            request.UseBinary = true;

            byte[] fileContents = File.ReadAllBytes(localFilePath);
            request.ContentLength = fileContents.Length;

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(fileContents, 0, fileContents.Length);
            }
        }

        private HashSet<string> LoadCache()
        {
            if (!File.Exists(_cacheFile)) return new HashSet<string>();
            try { return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(_cacheFile)); }
            catch { return new HashSet<string>(); }
        }

        private void SaveCache(HashSet<string> cache)
        {
            File.WriteAllText(_cacheFile, JsonSerializer.Serialize(cache));
        }
    }
}