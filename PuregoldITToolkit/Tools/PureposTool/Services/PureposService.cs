using PuregoldITToolkit.Tools.PureposTool.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.Models;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.PureposTool.Services
{
    public class PureposService : IPureposService
    {
        private readonly string _cacheFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EjUploadCache.json");

        // --- EMBEDDED FTP CREDENTIALS ---
        private readonly string[] _ftpUsers = new[]
        {
            "puregold/ftp1", "puregold/ftp1a", "puregold/ftp1b", "puregold/ftp1c",
            "puregold/ftp2", "puregold/ftp3", "puregold/ftp4", "puregold/ftp5",
            "puregold/ftp6", "puregold/ftp7", "puregold/ftp8", "puregold/ftp9",
            "puregold/ftp10", "puregold/ftp11"
        };
        private readonly string _ftpPassword = "pw@1234";

        // --- AUTO PERMISSION LOGIC ---
        public async Task RunAutoPermissionAsync(AutoPermissionModel config, Action<string> logCallback)
        {
            await Task.Run(() =>
            {
                string ipPrefix = config.LiveServerIp.Substring(0, config.LiveServerIp.LastIndexOf('.') + 1);

                for (int i = 1; i <= config.TotalLanes; i++)
                {
                    string posIp = $"{ipPrefix}{50 + i}";
                    logCallback($"\nConnecting to POS Lane {i} ({posIp})...");
                    ExecuteSshCommand(posIp, "cashier", "cashier", "sh /opt/purepos/scripts/permission.sh", logCallback);
                }

                logCallback($"\nConnecting to CONSO ({config.LiveServerIp})...");
                ExecuteSshCommand(config.LiveServerIp, config.ConsoUser, config.ConsoPassword, "sh /opt/purepos/scripts/permission.sh", logCallback);

                logCallback("\n✅ AUTO PERMISSION COMPLETED SUCCESSFULLY.");
            });
        }

        private void ExecuteSshCommand(string host, string username, string password, string command, Action<string> logCallback)
        {
            try
            {
                using (var client = new SshClient(host, username, password))
                {
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                    client.Connect();
                    var cmd = client.CreateCommand(command);
                    string result = cmd.Execute();
                    logCallback($"[SUCCESS] {host}: Permission granted. {result.Trim()}");
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                logCallback($"[ERROR] {host}: {ex.Message}");
            }
        }

        // --- MANUAL EJ SAVE LOGIC ---
        public async Task RunManualEjSaveAsync(EjSaveModel config, Action<string> logCallback)
        {
            await Task.Run(() =>
            {
                try
                {
                    logCallback("Scanning FTP Accounts for Store Directory...");

                    // 1. Hunt for the correct FTP User and Folder
                    string targetFtpDir = GetFtpStoreDirectory(config.FtpServer, config.StoreCode, out NetworkCredential validCreds, logCallback);

                    if (string.IsNullOrEmpty(targetFtpDir) || validCreds == null)
                    {
                        logCallback($"\n[ERROR] Could not find folder containing '({config.StoreCode})' across any FTP accounts.");
                        return;
                    }

                    logCallback($"\nFound target FTP directory: {targetFtpDir}");
                    logCallback($"Authenticated using FTP account: {validCreds.UserName}");

                    HashSet<string> cache = LoadCache();

                    // 2. Get existing files from FTP using the valid credentials
                    logCallback("Scanning FTP for existing EJ Zips...");
                    HashSet<string> existingFtpFiles = GetFtpFiles(targetFtpDir, validCreds);

                    List<DateTime> datesToProcess = new List<DateTime>();
                    for (DateTime date = config.DateFrom.Date; date <= config.DateTo.Date; date = date.AddDays(1))
                    {
                        string dateStr = date.ToString("MMddyyyy");
                        string expectedZip = $"{dateStr}_EJReceiptJournal.zip";

                        if (cache.Contains(dateStr))
                        {
                            logCallback($"Skipping {dateStr} (Already in local Cache).");
                            continue;
                        }

                        if (existingFtpFiles.Contains(expectedZip))
                        {
                            logCallback($"Skipping {dateStr} (Found on FTP). Marking to cache.");
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

                    // 3. Process Missing Dates via SFTP
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                    using (var sftp = new SftpClient(config.LiveServerIp, config.ConsoUser, config.ConsoPassword))
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

                            // Download
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

                            // Zip
                            logCallback($"Zipping files...");
                            if (File.Exists(localZipFile)) File.Delete(localZipFile);
                            ZipFile.CreateFromDirectory(localTempDir, localZipFile);

                            // Upload
                            logCallback($"Uploading {dateStr}_EJReceiptJournal.zip to FTP...");
                            UploadToFtp($"{targetFtpDir}/{Path.GetFileName(localZipFile)}", localZipFile, validCreds);

                            // Cleanup & Cache
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

        // --- ADVANCED FTP HELPER METHODS ---

        private string GetFtpStoreDirectory(string ftpIp, string storeCode, out NetworkCredential workingCreds, Action<string> logCallback)
        {
            workingCreds = null;
            string ftpUrl = $"ftp://{ftpIp}/PUREPOS_EJRECEIPT/";

            foreach (var user in _ftpUsers)
            {
                try
                {
                    var creds = new NetworkCredential(user, _ftpPassword);
                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                    request.Method = WebRequestMethods.Ftp.ListDirectory;
                    request.Credentials = creds;
                    request.Timeout = 3000; // Fast timeout to skip invalid accounts quickly

                    using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Contains($"({storeCode})"))
                            {
                                workingCreds = creds;
                                return $"{ftpUrl}{line}";
                            }
                        }
                    }
                }
                catch (WebException)
                {
                    // Access denied or folder missing for this specific user account. 
                    // Silently catch and move to the next user in the array.
                }
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
                    while ((line = reader.ReadLine()) != null) files.Add(Path.GetFileName(line));
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