using PuregoldITToolkit.Tools.PureposTool.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.Models;
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
        private readonly string _ftpPassword = "pw@1234";

        public async Task RunAutoPermissionAsync(AutoPermissionModel config, Action<string> logCallback)
        {
            var globalSettings = PuregoldITToolkit.Tools.SettingsTool.ViewModels.SettingsViewModel.GetCurrentSettings();
            string ipPrefix = config.LiveServerIp.Substring(0, config.LiveServerIp.LastIndexOf('.') + 1);
            var tasks = new List<Task>();

            logCallback("\nFiring permission commands in parallel...");

            for (int i = 1; i <= config.TotalLanes; i++)
            {
                int lane = i;
                string posIp = $"{ipPrefix}{50 + lane}";
                tasks.Add(Task.Run(() =>
                {
                    ExecuteSshCommandSync(posIp, "cashier", "cashier", "sh /opt/purepos/scripts/permission.sh", logCallback);
                }));
            }

            tasks.Add(Task.Run(() =>
            {
                ExecuteSshCommandSync(config.LiveServerIp, globalSettings.ConsoUser, globalSettings.ConsoPassword, "sh /opt/purepos/scripts/permission.sh", logCallback);
            }));

            await Task.WhenAll(tasks);
            logCallback("\n✅ AUTO PERMISSION COMPLETED SUCCESSFULLY.");
        }

        private void ExecuteSshCommandSync(string host, string username, string password, string command, Action<string> logCallback)
        {
            try
            {
                using (var client = new SshClient(host, username, password))
                {
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                    client.Connect();
                    var cmd = client.CreateCommand(command);
                    string result = cmd.Execute();
                    logCallback($"[SUCCESS] {host}: {result.Trim()}");
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                logCallback($"[ERROR] {host}: {ex.Message}");
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
                        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                        client.Connect();
                        var cmd = client.CreateCommand(command);
                        string result = cmd.Execute();
                        client.Disconnect();
                        return $"[SUCCESS {host}]:\n{result.Trim()}";
                    }
                }
                catch (Exception ex)
                {
                    return $"[ERROR {host}]: {ex.Message}";
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

                    DateTime targetDate = config.UseYesterday ? DateTime.Now.AddDays(-1) : DateTime.Now;
                    logCallback($"[i] DATE MODE: Using {(config.UseYesterday ? "yesterday's" : "today's")} date.");

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

                    System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage();
                    mail.From = new System.Net.Mail.MailAddress(globalSettings.SmtpUser, globalSettings.SenderName);

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
                              Store Offices: {config.StoreOfficer}</p>
                            <br/>
                            {globalSettings.SignatureHtml}
                          </body>
                        </html>";

                    mail.Body = htmlContent;
                    mail.Attachments.Add(new System.Net.Mail.Attachment(zipFilepath));

                    logCallback($"[*] Sending email via SMTP ({globalSettings.SmtpServer})...");
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
                        mboxContent.Append($"From - {DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}\n");
                        mboxContent.AppendLine($"From: {mail.From}");
                        mboxContent.AppendLine($"To: {mail.To}");

                        if (mail.CC.Count > 0)
                        {
                            var ccList = mail.CC.Select(c => c.Address).ToArray();
                            mboxContent.AppendLine($"Cc: {string.Join(", ", ccList)}");
                        }

                        mboxContent.AppendLine($"Subject: {mail.Subject}");
                        mboxContent.AppendLine($"Date: {DateTime.Now:R}");
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
                        if (cache.Contains(dateStr) || existingFtpFiles.Contains($"{dateStr}_EJReceiptJournal.zip"))
                        {
                            cache.Add(dateStr);
                            continue;
                        }
                        datesToProcess.Add(date);
                    }

                    if (datesToProcess.Count == 0)
                    {
                        SaveCache(cache);
                        logCallback("\nALL EJ FILES ARE UP TO DATE. No missing dates found.");
                        return;
                    }

                    logCallback("\nMANUAL EJ SAVE COMPLETED.");
                }
                catch (Exception ex)
                {
                    logCallback($"\n[FATAL ERROR] {ex.Message}");
                }
            });
        }

        private string GetFtpStoreDirectory(string ftpIp, string storeCode, out NetworkCredential workingCreds, Action<string> logCallback) { workingCreds = null; return null; } // Placeholder
        private HashSet<string> GetFtpFiles(string ftpDirUrl, NetworkCredential creds) { return new HashSet<string>(); }
        private void UploadToFtp(string targetUrl, string localFilePath, NetworkCredential creds) { }
        private HashSet<string> LoadCache() { return new HashSet<string>(); }
        private void SaveCache(HashSet<string> cache) { }
    }
}