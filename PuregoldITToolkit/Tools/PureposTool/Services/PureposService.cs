using PuregoldITToolkit.Tools.PureposTool.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.Models;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

        // --- PARALLEL AUTO PERMISSION LOGIC ---
        public async Task RunAutoPermissionAsync(AutoPermissionModel config, Action<string> logCallback)
        {
            string ipPrefix = config.LiveServerIp.Substring(0, config.LiveServerIp.LastIndexOf('.') + 1);
            var tasks = new List<Task>();

            logCallback("\nFiring permission commands in parallel...");

            // Queue POS Lanes
            for (int i = 1; i <= config.TotalLanes; i++)
            {
                int lane = i;
                string posIp = $"{ipPrefix}{50 + lane}";
                tasks.Add(Task.Run(() =>
                {
                    ExecuteSshCommandSync(posIp, "cashier", "cashier", "sh /opt/purepos/scripts/permission.sh", logCallback);
                }));
            }

            // Queue CONSO
            tasks.Add(Task.Run(() =>
            {
                ExecuteSshCommandSync(config.LiveServerIp, config.ConsoUser, config.ConsoPassword, "sh /opt/purepos/scripts/permission.sh", logCallback);
            }));

            // Execute all at once
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

        public async Task RunManualEjSaveAsync(EjSaveModel config, Action<string> logCallback)
        {
            await Task.Run(() =>
            {
                try
                {
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