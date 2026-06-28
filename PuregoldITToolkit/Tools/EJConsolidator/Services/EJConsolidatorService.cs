using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
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
                        totalProcessedReceipts += MergeAndFilterFiles(dateCacheDir, sodPath, masterFile, trxFile, dateStr, options, textProgress);
                    }

                    currentDateIndex++;
                    pctProgress?.Report((int)((double)currentDateIndex / totalDates * 100));
                }
            });

            return totalProcessedReceipts;
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

                        string enhancedBlock = block.Trim() + Environment.NewLine +
                                               $"[EXTRACTED DATA] Card: {cardText} | Member: {memberText} | Amount: Php {amountText}" +
                                               Environment.NewLine;

                        filteredReceipts.Add(enhancedBlock);
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
                    catch (Exception ex)
                    {
                        lastException = ex;
                    }
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