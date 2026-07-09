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
                        bool success = false;

                        if (targetDate.Date == DateTime.Today)
                        {
                            success = DownloadFromPosDirectly(options, targetDate, dateCacheDir, textProgress);
                        }

                        if (!success)
                        {
                            success = DownloadFromLiveServerFolder(options, dateStr, dateCacheDir, textProgress);
                        }

                        if (!success)
                        {
                            success = DownloadFromLiveServerDoneZip(options, dateStr, dateCacheDir, textProgress);
                        }
                        if (!success)
                        {
                            textProgress?.Report($"  [Fallback] Checking FTP Server for {dateStr}...");
                            success = DownloadFromPrimaryFtp(options, dateStr, dateCacheDir, textProgress);
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
                        totalProcessedReceipts += MergeAndFilterFiles(dateCacheDir, sodPath, masterFile, trxFile, dateStr, options, textProgress);
                    }

                    currentDateIndex++;
                    pctProgress?.Report((int)((double)currentDateIndex / totalDates * 100));
                }
            });

            return totalProcessedReceipts;
        }

        private bool DownloadFromLiveServerFolder(EJFilterOptions options, string dateStr, string tempDir, IProgress<string> progress)
        {
            try
            {
                var globalSettings = SettingsViewModel.GetCurrentSettings();
                if (string.IsNullOrWhiteSpace(globalSettings.ConsoUser) || string.IsNullOrWhiteSpace(globalSettings.ConsoPassword))
                {
                    progress?.Report($"  [Error] Console credentials not configured. Please check Settings.");
                    return false;
                }


                SessionOptions sftpOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = options.LiveServerIp,
                    UserName = globalSettings.ConsoUser,
                    Password = globalSettings.ConsoPassword,
                    SshHostKeyPolicy = SshHostKeyPolicy.GiveUpSecurityAndAcceptAny,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                using (Session session = new Session())
                {
                    session.Open(sftpOptions);
                    string remoteFolder = $"/opt/purepos/ejreceipt/{dateStr}/";

                    var files = session.ListDirectory(remoteFolder).Files;
                    var zipFiles = files.Where(f => !f.IsDirectory && f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();

                    if (zipFiles.Any())
                    {
                        progress?.Report($"  Found {zipFiles.Count} ZIP files in {remoteFolder}");
                        string innerDir = Path.Combine(tempDir, "Inner");
                        Directory.CreateDirectory(innerDir);

                        bool processedAny = false;
                        foreach (var zipFile in zipFiles)
                        {
                            string localZipPath = Path.Combine(tempDir, zipFile.Name);
                            session.GetFiles(remoteFolder + zipFile.Name, localZipPath).Check();
                            ExtractZipSafe(localZipPath, innerDir, progress);
                            processedAny = true;
                        }

                        if (processedAny)
                        {
                            var txtFiles = Directory.GetFiles(innerDir, "*.txt", SearchOption.AllDirectories);
                            foreach (var txt in txtFiles)
                            {
                                File.Copy(txt, Path.Combine(tempDir, Path.GetFileName(txt)), true);
                            }
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"  [Failed] Live Server Folder Error: {ex.Message}");
            }
            return false;
        }

        private bool DownloadFromLiveServerDoneZip(EJFilterOptions options, string dateStr, string tempDir, IProgress<string> progress)
        {
            try
            {
                var globalSettings = SettingsViewModel.GetCurrentSettings();
                if (string.IsNullOrWhiteSpace(globalSettings.ConsoUser) || string.IsNullOrWhiteSpace(globalSettings.ConsoPassword))
                {
                    progress?.Report($"  [Error] Console credentials not configured. Please check Settings.");
                    return false;
                }
                SessionOptions sftpOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = options.LiveServerIp,
                    UserName = globalSettings.ConsoUser,
                    Password = globalSettings.ConsoPassword,
                    SshHostKeyPolicy = SshHostKeyPolicy.GiveUpSecurityAndAcceptAny,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                using (Session session = new Session())
                {
                    session.Open(sftpOptions);
                    string doneZipPath = $"/opt/purepos/ejreceipt/done/{dateStr}_EJReceiptJournal.zip";

                    if (session.FileExists(doneZipPath))
                    {
                        progress?.Report($"  Found Done ZIP: {doneZipPath}");
                        string localZipPath = Path.Combine(tempDir, "done.zip");
                        session.GetFiles(doneZipPath, localZipPath).Check();

                        string innerDir = Path.Combine(tempDir, "Inner");
                        Directory.CreateDirectory(innerDir);
                        ExtractZipSafe(localZipPath, innerDir, progress);

                        var txtFiles = Directory.GetFiles(innerDir, "*.txt", SearchOption.AllDirectories);
                        if (txtFiles.Any())
                        {
                            foreach (var txt in txtFiles)
                            {
                                File.Copy(txt, Path.Combine(tempDir, Path.GetFileName(txt)), true);
                            }
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"  [Failed] Live Server Done ZIP Error: {ex.Message}");
            }
            return false;
        }


        private int MergeAndFilterFiles(string sourceDir, string sodPath, string masterFile, string trxFile, string dateStr, EJFilterOptions options, IProgress<string> progress)
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
                    if (fileContent.IndexOf(options.TargetTrxNumber.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                        return;
                }

                var blocks = Regex.Split(fileContent, @"(?=Puregold Price Club|SECOND RECEIPT)", RegexOptions.IgnoreCase);
                System.Threading.Interlocked.Add(ref rawReceiptCount, blocks.Length);

                foreach (var block in blocks)
                {
                    if (string.IsNullOrWhiteSpace(block)) continue;
                    if (_filterService.IsMatch(block, options))
                    {
                        string cardMatch = Regex.Match(block, @"(?i)Member Card Number:\s*.*?(\d{4})\b").Groups[1].Value;
                        string memberMatch = Regex.Match(block, @"(?i)Member Name:\s*([^\n\r]+)").Groups[1].Value.Trim();
                        string amountMatch = Regex.Match(block, @"(?i)Total(?: Amt Due)?\s*(?:Php)?\s*([\d,.]+)").Groups[1].Value.Trim();

                        string cardText = string.IsNullOrEmpty(cardMatch) ? "N/A" : $"****{cardMatch}";
                        string memberText = string.IsNullOrEmpty(memberMatch) ? "N/A" : memberMatch;
                        string amountText = string.IsNullOrEmpty(amountMatch) ? "0.00" : amountMatch;

                        // CRITICAL FIX: Add the matched block to the collection so it gets written to the file
                        filteredReceipts.Add(block);
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
                        session.Open(new SessionOptions
                        {
                            Protocol = Protocol.Ftp,
                            HostName = ftpHost,
                            UserName = $"ftp{i}@puregold",
                            Password = "pw@1234"
                        });
                        connected = true;
                        break;
                    }
                    catch (Exception ex) { lastException = ex; }
                }

                if (!connected)
                {
                    progress?.Report($"  [Failed] Primary FTP Connection Error: {lastException?.Message}");
                    return false;
                }

                try
                {
                    var files = session.ListDirectory("/PUREPOS_EJRECEIPT/").Files;
                    string targetFolder = files.FirstOrDefault(f => f.Name.StartsWith($"({options.StoreCode})"))?.Name;

                    if (string.IsNullOrEmpty(targetFolder))
                    {
                        progress?.Report($"  [Missing] Store folder for ({options.StoreCode}) not found on Primary FTP.");
                        return false;
                    }

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

        private bool DownloadFromPosDirectly(EJFilterOptions options, DateTime targetDate, string tempDir, IProgress<string> progress)
        {
            bool processedAny = false;
            string dateStr = targetDate.ToString("MMddyyyy");
            string ipPrefix = options.LiveServerIp.Substring(0, options.LiveServerIp.LastIndexOf('.') + 1);

            List<int> lanesToCheck = new List<int>();
            if (options.PosLanes != null && options.PosLanes.Any())
            {
                foreach (var l in options.PosLanes)
                    if (int.TryParse(l, out int laneNum)) lanesToCheck.Add(laneNum);
            }
            else
            {
                int total = options.TotalPosCount > 0 ? options.TotalPosCount : 6;
                for (int i = 1; i <= total; i++) lanesToCheck.Add(i);
            }

            progress?.Report($"  [EJ Today] Pulling live data directly from POS Terminals for {dateStr}...");
            object syncLock = new object();

            Parallel.ForEach(lanesToCheck, lane =>
            {
                string posIp = $"{ipPrefix}{50 + lane}";
                try
                {
                    using (var session = new Session())
                    {
                        session.Open(new SessionOptions
                        {
                            Protocol = Protocol.Sftp,
                            HostName = posIp,
                            UserName = "cashier",
                            Password = "cashier",
                            SshHostKeyPolicy = SshHostKeyPolicy.GiveUpSecurityAndAcceptAny,
                            Timeout = TimeSpan.FromSeconds(5) 
                        });

                        string remotePath = $"/home/cashier/Documents/{dateStr}/";
                        if (session.FileExists(remotePath))
                        {
                            var files = session.ListDirectory(remotePath).Files
                                               .Where(f => !f.IsDirectory && f.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

                            foreach (var file in files)
                            {
                                string localFilePath = Path.Combine(tempDir, file.Name);
                                session.GetFiles(remotePath + file.Name, localFilePath).Check();
                                lock (syncLock) { processedAny = true; }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"  [Warning] Failed to pull from POS {lane} ({posIp}): {ex.Message}");
                }
            });

            return processedAny;
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
                        progress?.Report($"  Found Date Folder: {targetFolder}");
                        var files = session.ListDirectory(targetFolder).Files
                            .Where(f => !f.IsDirectory && (f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));

                        bool processedAny = false;

                        foreach (var file in files)
                        {
                            bool keep = options.PosLanes.Count == 0 || options.PosLanes.Any(lane => Regex.IsMatch(file.Name, $@"(?i)_0*{lane}\.(zip|txt)$"));

                            if (keep)
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
                        }
                        return processedAny;
                    }

                    progress?.Report($"  No folders or files found for {dateStr} in Fallback Server.");
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"  [Failed] Fallback Connection Error: {ex.Message}");
            }
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