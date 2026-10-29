using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WinSCP;

namespace PuregoldITToolkit.Tools.EJConsolidator.Services
{
    public class EJConsolidatorService : IEJConsolidatorService
    {
        internal static readonly SemaphoreSlim CacheAccess = new SemaphoreSlim(1, 1);
        private readonly IReceiptFilterService _filterService;

        public EJConsolidatorService(IReceiptFilterService filterService)
        {
            _filterService = filterService;
        }

        // Backward compatibility overload
        public async Task<int> ProcessConsolidationAsync(EJFilterOptions options, IProgress<string> textProgress, IProgress<int> pctProgress)
        {
            var progress = new Progress<EJProgressReport>(report =>
            {
                textProgress?.Report(report.StatusMessage);
                pctProgress?.Report(report.Percentage);
            });

            return await ProcessConsolidationAsync(options, progress, CancellationToken.None);
        }

        public async Task<int> ProcessConsolidationAsync(EJFilterOptions options, IProgress<EJProgressReport> progress, CancellationToken cancellationToken)
        {
            await CacheAccess.WaitAsync(cancellationToken);
            try { return await ProcessCoreAsync(options, progress, cancellationToken); }
            finally { CacheAccess.Release(); }
        }

        private async Task<int> ProcessCoreAsync(EJFilterOptions options, IProgress<EJProgressReport> progress, CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.TargetDates == null || options.TargetDates.Count == 0) throw new ArgumentException("Select at least one date.");
            if (!options.ProcessAllStores && !Regex.IsMatch(options.StoreCode ?? "", @"^\d{1,6}$")) throw new ArgumentException("Store code must contain 1–6 digits.");
            if (options.IsModeTrxFinder && !Regex.IsMatch(options.TargetTrxNumber ?? "", @"^\d+(?:-\d+)*$")) throw new ArgumentException("Enter a numeric transaction or store-lane-invoice number.");
            if (options.ProcessAllStores && options.ExecutionMode != EJExecutionMode.FtpOthers) throw new ArgumentException("All-store processing requires FTP Others.");
            options.TargetDates = options.TargetDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
            if (options.ExecutionMode != EJExecutionMode.Local && (string.IsNullOrWhiteSpace(options.FtpHost) || options.FtpPort < 1 || options.FtpPort > 65535))
                throw new ArgumentException("Specify a valid FTP host and port.");
            cancellationToken.ThrowIfCancellationRequested();
            if (options.ExecutionMode == EJExecutionMode.Local && string.IsNullOrWhiteSpace(options.LiveServerIp)) throw new ArgumentException("Enter the live/CONSO server address.");
            options.PublishedPreviewCount = 0;
            int totalProcessedReceipts = 0;
            var missingDates = new List<string>();
            string outputDir = string.IsNullOrWhiteSpace(options.OutputDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SOD_Output")
                : options.OutputDirectory;

            outputDir = Path.Combine(Path.GetFullPath(outputDir), $"EJ_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}");
            progress?.Report(new EJProgressReport { OutputDirectory = outputDir, StatusMessage = "Preparing output folder..." });
            try
            {
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Failed to create output directory: {outputDir}", ex);
                throw new InvalidOperationException($"Cannot access output directory '{outputDir}': {ex.Message}");
            }

            string storeLabel = options.ProcessAllStores ? "ALL_STORES" : options.StoreCode;
            string masterFile = Path.Combine(outputDir, $"Master_Consolidated_{storeLabel}_{DateTime.Now:yyyyMMdd}.txt");
            string trxFile = Path.Combine(outputDir, $"{options.TargetTrxNumber}.txt");

            if (options.IsModeTrxFinder && File.Exists(trxFile))
            {
                SafeDeleteFile(trxFile);
            }
            if (!options.IsModeTrxFinder && options.MergeAllIntoOneFile && File.Exists(masterFile))
            {
                SafeDeleteFile(masterFile);
            }

            string sourceIdentity = $"{options.ExecutionMode}|{options.FtpHost}:{options.FtpPort}|{options.LocalStoreDirectory}|{options.LiveServerIp}|{storeLabel}";
            string sourceKey;
            using (var hash = System.Security.Cryptography.SHA256.Create())
                sourceKey = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(sourceIdentity))).Replace("-", "").Substring(0, 24);
            string cacheRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EJ_Cache", sourceKey);
            if (!Directory.Exists(cacheRoot)) Directory.CreateDirectory(cacheRoot);

            await Task.Run(() =>
            {
                int totalSteps = Math.Max(1, options.TargetDates.Count);
                int currentStep = 0;

                LoggingService.Instance.LogInfo($"Starting EJ Consolidation. Mode: {options.ExecutionMode}. Dates: {options.TargetDates.Count}. Store: {storeLabel}");

                foreach (var targetDate in options.TargetDates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string dateStr = targetDate.ToString("MMddyyyy");
                    string dateCacheDir = Path.Combine(cacheRoot, dateStr);

                    progress?.Report(new EJProgressReport
                    {
                        ProcessedFiles = currentStep,
                        TotalFiles = totalSteps,
                        StatusMessage = $"Processing Date: {dateStr} ({options.ExecutionMode} Mode)..."
                    });

                    // Check if already in cache and contains valid files
                    bool hasValidCachedFiles = options.UseCachedData && targetDate.Date < DateTime.Today && File.Exists(Path.Combine(dateCacheDir, ".complete")) && Directory.Exists(dateCacheDir) &&
                        Directory.GetFiles(dateCacheDir, "*.*", SearchOption.AllDirectories)
                                 .Any(f => (f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ej", StringComparison.OrdinalIgnoreCase)) && new FileInfo(f).Length > 0);

                    bool retrieved = hasValidCachedFiles;
                    if (hasValidCachedFiles)
                    {
                        LoggingService.Instance.LogInfo($"[Cache] Using existing offline cache for {dateStr}");
                    }
                    else
                    {
                        SafeRecreateDirectory(dateCacheDir);

                        if (options.ExecutionMode == EJExecutionMode.Local)
                        {
                            // ==========================================
                            // 1. LOCAL (Own Store) Execution Mode
                            // ==========================================
                            retrieved = ExtractLocalStoreData(options, targetDate, dateCacheDir, progress, cancellationToken);
                        }
                        else
                        {
                            // ==========================================
                            // 2. FTP (All Stores / Remote) Execution Mode
                            // ==========================================
                            retrieved = ExtractFtpStoreData(options, targetDate, dateCacheDir, progress, cancellationToken);
                        }

                        if (retrieved) File.WriteAllText(Path.Combine(dateCacheDir, ".complete"), DateTime.UtcNow.ToString("O"));
                        if (!retrieved)
                        {
                            LoggingService.Instance.LogWarning($"[Skipped] No valid EJ data retrieved for date {dateStr}");
                        }
                    }

                    if (!retrieved) missingDates.Add(dateStr);

                    // Consolidate & Filter
                    cancellationToken.ThrowIfCancellationRequested();

                    if (retrieved && Directory.Exists(dateCacheDir))
                    {
                        var candidateFiles = Directory.GetFiles(dateCacheDir, "*.*", SearchOption.AllDirectories)
                            .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ej", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (candidateFiles.Any())
                        {
                            string dailyFile = Path.Combine(outputDir, $"Merged_{storeLabel}_{dateStr}.txt");
                            if (!options.IsModeTrxFinder && !options.MergeAllIntoOneFile && File.Exists(dailyFile))
                            {
                                SafeDeleteFile(dailyFile);
                            }

                            int matched = MergeAndFilterFilesSafe(candidateFiles, outputDir, masterFile, trxFile, dateStr, storeLabel, options, progress, cancellationToken);
                            totalProcessedReceipts += matched;
                        }
                    }

                    currentStep++;
                    progress?.Report(new EJProgressReport
                    {
                        ProcessedFiles = currentStep,
                        TotalFiles = totalSteps,
                        StatusMessage = $"Completed {dateStr}. Cumulative receipts: {totalProcessedReceipts}.",
                        MatchedReceipts = totalProcessedReceipts
                    });
                }
            }, cancellationToken);

            File.WriteAllText(Path.Combine(outputDir, "RunSummary.txt"),
                $"EJ consolidation · {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\nSource: {options.ExecutionMode}\r\nStore: {storeLabel}\r\nRequested dates: {string.Join(", ", options.TargetDates.Select(d => d.ToString("yyyy-MM-dd")))}\r\nMatching receipts: {totalProcessedReceipts}\r\nDates without data: {(missingDates.Count == 0 ? "None" : string.Join(", ", missingDates))}\r\n", Encoding.UTF8);
            progress?.Report(new EJProgressReport { ProcessedFiles = options.TargetDates.Count, TotalFiles = options.TargetDates.Count,
                MissingDates = missingDates.Count, MatchedReceipts = totalProcessedReceipts, OutputDirectory = outputDir,
                StatusMessage = $"Finished · {totalProcessedReceipts} receipts · {missingDates.Count} dates without data." });
            LoggingService.Instance.LogSuccess($"EJ Consolidation finished. Total matching blocks: {totalProcessedReceipts}");
            return totalProcessedReceipts;
        }

        // ==========================================
        // LOCAL EXECUTION MODE
        // ==========================================
        protected virtual bool ExtractLocalStoreData(EJFilterOptions options, DateTime targetDate, string destinationDir, IProgress<EJProgressReport> progress, CancellationToken ct)
        {
            string date = targetDate.ToString("MMddyyyy");
            return DownloadFromLiveServerDoneZipSafe(options, date, destinationDir, progress, ct)
                || DownloadFromLiveServerFolderSafe(options, date, destinationDir, progress, ct);
        }

        protected bool ExtractDirectoryData(EJFilterOptions options, DateTime targetDate, string destinationDir, IProgress<EJProgressReport> progress, CancellationToken ct)
        {
            string dateStr = targetDate.ToString("MMddyyyy");
            bool anyCopied = false;

            var settings = SettingsViewModel.GetCurrentSettings();
            string root = !string.IsNullOrWhiteSpace(options.LocalStoreDirectory) ? options.LocalStoreDirectory : settings.EjInputDirectory;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                throw new DirectoryNotFoundException("The selected local EJ directory does not exist.");
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            string[] dateNames = { dateStr, targetDate.ToString("yyyyMMdd"), targetDate.ToString("yyyy-MM-dd") };
            foreach (var file in EnumerateLocalFiles(root).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                if (file.StartsWith(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EJ_Cache")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                string relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
                bool matchesDate = dateNames.Any(d => Path.GetFileName(root) == d ||
                    Regex.IsMatch(relative, @"(?<!\d)" + d + @"(?!\d)"));
                if (!matchesDate) continue;
                if (ProcessLocalFile(file, destinationDir, relative)) anyCopied = true;
            }

            return anyCopied;
        }

        private bool ProcessLocalFile(string filePath, string destinationDir, string relativePath)
        {
            try
            {
                var fi = new FileInfo(filePath);
                if (!fi.Exists || fi.Length == 0) return false;

                string ext = fi.Extension.ToLowerInvariant();
                string destFile = Path.Combine(destinationDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                if (ext == ".zip")
                {
                    string extractionDir = destFile + ".contents";
                    Directory.CreateDirectory(extractionDir);
                    return ExtractZipSafe(filePath, extractionDir);
                }
                if (ext == ".txt" || ext == ".ej")
                {
                    SafeCopyFile(filePath, destFile);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarning($"Error processing local file '{filePath}': {ex.Message}");
            }
            return false;
        }

        // ==========================================
        // FTP / SFTP EXECUTION MODE
        // ==========================================
        private bool ExtractFtpStoreData(EJFilterOptions options, DateTime targetDate, string destinationDir, IProgress<EJProgressReport> progress, CancellationToken ct)
        {
            string dateStr = targetDate.ToString("MMddyyyy");
            var settings = SettingsViewModel.GetCurrentSettings();

            string ftpHost = !string.IsNullOrWhiteSpace(options.FtpHost) ? options.FtpHost : settings.FtpHost;
            int ftpPort = options.FtpPort > 0 ? options.FtpPort : (settings.FtpPort > 0 ? settings.FtpPort : 21);
            int timeoutSec = options.FtpTimeoutSeconds > 0 ? options.FtpTimeoutSeconds : settings.FtpTimeoutSeconds;

            return DownloadFromPrimaryFtpSafe(options, dateStr, destinationDir, ftpHost, ftpPort, timeoutSec, options.FtpPassiveMode, progress, ct);
        }

        protected virtual bool DownloadFromPrimaryFtpSafe(
            EJFilterOptions options,
            string dateStr,
            string destinationDir,
            string host,
            int port,
            int timeoutSeconds,
            bool passive,
            IProgress<EJProgressReport> progress,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using (var session = AutomaticFtpLogin.Open(host, port, timeoutSeconds, passive, ct,
                    accepts: connected => connected.FileExists("/PUREPOS_EJRECEIPT/") &&
                        (options.ProcessAllStores || connected.ListDirectory("/PUREPOS_EJRECEIPT/").Files.Any(f => f.IsDirectory && StoreFolderMatches(f.Name, options.StoreCode)))))
                {
                    string remoteBasePath = "/PUREPOS_EJRECEIPT/";

                    if (!session.FileExists(remoteBasePath))
                    {
                        LoggingService.Instance.LogWarning($"Directory {remoteBasePath} does not exist on FTP {host}");
                        return false;
                    }

                    var storeFolders = new List<string>();

                    if (options.ProcessAllStores)
                    {
                        // Iterate through all store folders
                        var remoteFiles = session.ListDirectory(remoteBasePath).Files;
                        storeFolders = remoteFiles.Where(f => f.IsDirectory && !f.Name.StartsWith(".")).Select(f => f.Name).ToList();
                    }
                    else
                    {
                        // Match specific store folder e.g. (722)
                        var remoteFiles = session.ListDirectory(remoteBasePath).Files;
                        var target = remoteFiles.FirstOrDefault(f => f.IsDirectory && StoreFolderMatches(f.Name, options.StoreCode));
                        if (target != null) storeFolders.Add(target.Name);
                    }

                    if (!storeFolders.Any())
                    {
                        LoggingService.Instance.LogWarning($"No store directories found on FTP for Store '{options.StoreCode}'");
                        return false;
                    }

                    bool anyDownloaded = false;

                    foreach (var storeFolder in storeFolders)
                    {
                        ct.ThrowIfCancellationRequested();
                        progress?.Report(new EJProgressReport { DetectedStore = storeFolder, StatusMessage = $"Detected store: {storeFolder}. Retrieving {dateStr}..." });
                        string remoteZip = $"{remoteBasePath}{storeFolder}/{dateStr}_EJReceiptJournal.zip";

                        if (session.FileExists(remoteZip))
                        {
                            string localZip = Path.Combine(destinationDir, $"{storeFolder}_{dateStr}.zip");
                            session.GetFiles(RemotePath.EscapeFileMask(remoteZip), localZip).Check();

                            string innerDir = Path.Combine(destinationDir, Regex.Replace(storeFolder, @"[<>:""/\\|?*]", "_"));
                            Directory.CreateDirectory(innerDir);
                            ExtractZipSafe(localZip, innerDir, progress);

                            anyDownloaded |= Directory.EnumerateFiles(innerDir, "*", SearchOption.AllDirectories).Any(IsJournalFile);
                        }
                    }

                    return anyDownloaded;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarning($"FTP Primary download warning ({host}): {ex.Message}");
                return false;
            }
        }

        protected virtual bool DownloadFromLiveServerDoneZipSafe(EJFilterOptions options, string dateStr, string destinationDir, IProgress<EJProgressReport> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var settings = SettingsViewModel.GetCurrentSettings();
                var sftpOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = options.LiveServerIp,
                    UserName = settings.ConsoUser,
                    Password = settings.ConsoPassword,
                    SshHostKeyPolicy = SshHostKeyPolicy.GiveUpSecurityAndAcceptAny,
                    Timeout = TimeSpan.FromSeconds(20)
                };

                using (var session = new Session())
                {
                    session.Open(sftpOptions);
                    string doneZipPath = $"/opt/purepos/ejreceipt/done/{dateStr}_EJReceiptJournal.zip";

                    if (session.FileExists(doneZipPath))
                    {
                        string localZip = Path.Combine(destinationDir, "done.zip");
                        session.GetFiles(doneZipPath, localZip).Check();

                        string innerDir = Path.Combine(destinationDir, "DoneInner");
                        Directory.CreateDirectory(innerDir);
                        ExtractZipSafe(localZip, innerDir, progress);

                        return Directory.EnumerateFiles(innerDir, "*", SearchOption.AllDirectories).Any(IsJournalFile);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarning($"Live server done zip warning: {ex.Message}");
            }
            return false;
        }

        protected virtual bool DownloadFromLiveServerFolderSafe(EJFilterOptions options, string dateStr, string destinationDir, IProgress<EJProgressReport> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var settings = SettingsViewModel.GetCurrentSettings();
                var sftpOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = options.LiveServerIp,
                    UserName = settings.ConsoUser,
                    Password = settings.ConsoPassword,
                    SshHostKeyPolicy = SshHostKeyPolicy.GiveUpSecurityAndAcceptAny,
                    Timeout = TimeSpan.FromSeconds(20)
                };

                using (var session = new Session())
                {
                    session.Open(sftpOptions);
                    string remoteFolder = $"/opt/purepos/ejreceipt/{dateStr}/";

                    if (session.FileExists(remoteFolder))
                    {
                        var files = session.ListDirectory(remoteFolder).Files
                            .Where(f => !f.IsDirectory && (f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || IsJournalFile(f.Name)))
                            .ToList();

                        bool any = false;
                        foreach (var f in files)
                        {
                            ct.ThrowIfCancellationRequested();
                            string localPath = Path.Combine(destinationDir, f.Name);
                            session.GetFiles(remoteFolder + f.Name, localPath).Check();

                            if (f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                ExtractZipSafe(localPath, destinationDir, progress);
                            }
                            any = true;
                        }
                        return any;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarning($"Live server folder SFTP warning: {ex.Message}");
            }
            return false;
        }

        // ==========================================
        // SAFE MERGING & FILTERING (Handling locked files, corrupt lines, zero-byte logs)
        // ==========================================
        private int MergeAndFilterFilesSafe(
            List<string> candidateFiles,
            string sodPath,
            string masterFile,
            string trxFile,
            string dateStr,
            string storeLabel,
            EJFilterOptions options,
            IProgress<EJProgressReport> progress,
            CancellationToken ct)
        {
            if (candidateFiles == null || candidateFiles.Count == 0) return 0;

            var filteredReceipts = new ConcurrentBag<Tuple<string, int, string>>();

            Parallel.ForEach(candidateFiles, file =>
            {
                if (ct.IsCancellationRequested) return;

                // 1. Audit: Zero-Byte check
                var fi = new FileInfo(file);
                if (!fi.Exists || fi.Length == 0)
                {
                    LoggingService.Instance.LogWarning($"[Audit] Skipping zero-byte log file: {fi.Name}");
                    return;
                }

                // 2. Audit: Locked File Handling
                string fileContent = null;
                try
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fs, Encoding.UTF8, true))
                    {
                        fileContent = reader.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"[Locked File] Cannot read {fi.Name}", ex);
                    return;
                }

                if (string.IsNullOrWhiteSpace(fileContent)) return;

                // 4. Audit: Splitting receipts & Corrupt block isolation
                string[] blocks = null;
                try
                {
                    blocks = SplitReceipts(fileContent);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogWarning($"[Regex Split Error] {fi.Name}: {ex.Message}");
                    blocks = new[] { fileContent };
                }


                int blockIndex = 0;
                var laneMatch = Regex.Match(fi.Name, @"(?:_|POS|Lane)(\d{1,3})(?:\.(?:txt|ej)|[_-])", RegexOptions.IgnoreCase);
                if (!laneMatch.Success) laneMatch = Regex.Match(Path.GetDirectoryName(file), @"(?:POS|Lane)(\d{1,3})(?:[\\/]|$)", RegexOptions.IgnoreCase);
                string sourceLane = laneMatch.Success ? laneMatch.Groups[1].Value : null;
                foreach (var block in blocks)
                {
                    blockIndex++;
                    if (string.IsNullOrWhiteSpace(block)) continue;

                    try
                    {
                        if (_filterService.IsMatch(block, options, sourceLane))
                        {
                            filteredReceipts.Add(Tuple.Create(file, blockIndex, block));
                        }
                    }
                    catch (Exception ex)
                    {
                        // Localized transaction-level exception handling prevents crashing batch
                        LoggingService.Instance.LogWarning($"[Transaction Error] File '{fi.Name}' corrupt receipt block skipped: {ex.Message}");
                    }
                }
            });

            ct.ThrowIfCancellationRequested();

            var finalReceiptList = filteredReceipts.OrderBy(r => r.Item1, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Item2).ToList();

            if (finalReceiptList.Any())
            {
                string targetFile = options.IsModeTrxFinder
                    ? trxFile
                    : (options.MergeAllIntoOneFile ? masterFile : Path.Combine(sodPath, $"Merged_{storeLabel}_{dateStr}.txt"));

                try
                {
                    using (var stream = new FileStream(targetFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        foreach (var receipt in finalReceiptList)
                        {
                            writer.WriteLine($"[Source: {receipt.Item1.Substring(receipt.Item1.IndexOf(dateStr, StringComparison.Ordinal) + dateStr.Length).TrimStart(Path.DirectorySeparatorChar)}]");
                            using (var reader = new StringReader(receipt.Item3))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    writer.WriteLine(line);
                                }
                            }
                            writer.WriteLine();
                            writer.WriteLine(new string('-', 50));
                            writer.WriteLine();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Failed writing consolidated output to {targetFile}", ex);
                    throw;
                }
            }

            foreach (var receipt in finalReceiptList.Take(Math.Max(0, 200 - options.PublishedPreviewCount)))
            {
                var id = Regex.Match(receipt.Item3, @"(?:Trans(?:action)?\s*(?:#|No\.?|Number)?|INVOICE\s*#?|Sales\s+Invoice|Tr)\s*[:#]?\s*(\d[\d-]*)", RegexOptions.IgnoreCase);
                progress?.Report(new EJProgressReport { Receipt = new EJReceiptResult { SourceFile = receipt.Item1.Substring(receipt.Item1.IndexOf(dateStr, StringComparison.Ordinal) + dateStr.Length).TrimStart(Path.DirectorySeparatorChar),
                    Date = dateStr, TransactionNumber = id.Success ? id.Groups[1].Value : "—", ReceiptText = receipt.Item3 }, StatusMessage = "Loading result previews..." });
                options.PublishedPreviewCount++;
            }
            return finalReceiptList.Count;
        }

        // ==========================================
        // SAFE ZIP EXTRACTION
        // ==========================================
        private bool ExtractZipSafe(string zipPath, string extractPath, IProgress<EJProgressReport> progress = null)
        {
            if (!File.Exists(zipPath)) return false;

            var fi = new FileInfo(zipPath);
            if (fi.Length == 0)
            {
                LoggingService.Instance.LogWarning($"[Zip Audit] Skipping 0-byte archive: {Path.GetFileName(zipPath)}");
                return false;
            }

            bool extracted = false;
            string safeRoot = Path.GetFullPath(extractPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Name) || !IsJournalFile(entry.Name)) continue;

                        try
                        {
                            string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                            // Protect against Zip Slip directory traversal
                            if (destinationPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                            {
                                string targetDir = Path.GetDirectoryName(destinationPath);
                                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                                    Directory.CreateDirectory(targetDir);

                                entry.ExtractToFile(destinationPath, true);
                                extracted |= IsJournalFile(destinationPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.Instance.LogWarning($"[Zip Entry Error] Failed extracting '{entry.Name}' from '{Path.GetFileName(zipPath)}': {ex.Message}");
                        }
                    }
                }
            }
            catch (InvalidDataException idEx)
            {
                LoggingService.Instance.LogWarning($"[Corrupt Zip] {Path.GetFileName(zipPath)} is corrupted or incomplete: {idEx.Message}");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarning($"[Zip Error] {Path.GetFileName(zipPath)}: {ex.Message}");
            }
            return extracted;
        }

        private static void SafeCopyFile(string source, string dest)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            using (var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.Read)) src.CopyTo(dst);
        }
        private static void SafeDeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
        private static void SafeRecreateDirectory(string path)
        {
            string target = Path.GetFullPath(path);
            string cacheBase = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EJ_Cache")) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(cacheBase, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid cache path.");
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);
        }
        private static IEnumerable<string> EnumerateLocalFiles(string root)
        {
            foreach (var file in Directory.EnumerateFiles(root))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0 && (IsJournalFile(file) || file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))) yield return file;
            foreach (var directory in Directory.EnumerateDirectories(root))
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                    foreach (var file in EnumerateLocalFiles(directory)) yield return file;
        }

        public static string[] SplitReceipts(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return new string[0];
            string normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
            var headers = Regex.Matches(normalized, @"(?im)^[ \t]*(?:PUREGOLD PRICE CLUB|PUREGOLD JUNIOR|PUREMART)\b");
            if (headers.Count == 0) return new[] { normalized };
            var starts = new List<int>();
            foreach (Match header in headers)
            {
                int start = header.Index;
                int preceding = Math.Max(0, start - 100);
                var second = Regex.Match(normalized.Substring(preceding, start - preceding), @"(?im)^[ \t]*SECOND RECEIPT[ \t]*\n(?:[ \t]*\n)*$");
                if (second.Success) start = preceding + second.Index;
                starts.Add(start);
            }
            var blocks = new List<string>();
            for (int i = 0; i < starts.Count; i++)
                blocks.Add(normalized.Substring(starts[i], (i + 1 < starts.Count ? starts[i + 1] : normalized.Length) - starts[i]));
            return blocks.ToArray();
        }

        private static bool IsJournalFile(string path) => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ej", StringComparison.OrdinalIgnoreCase);

        public static bool StoreFolderMatches(string folder, string code)
        {
            var match = Regex.Match(folder ?? "", @"^(?:\((\d+)\)|(\d+)(?:\s*[-_ ]|$))");
            string actual = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return match.Success && actual.TrimStart('0') == (code ?? "").TrimStart('0');
        }

        public Task<List<EJStoreInfo>> GetStoresAsync(EJFilterOptions options, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var session = AutomaticFtpLogin.Open(options.FtpHost, options.FtpPort, options.FtpTimeoutSeconds,
                    options.FtpPassiveMode, cancellationToken, accepts: connected => connected.FileExists("/PUREPOS_EJRECEIPT/")))
                {
                    var stores = new List<EJStoreInfo>();
                    foreach (var folder in session.ListDirectory("/PUREPOS_EJRECEIPT/").Files.Where(f => f.IsDirectory && !f.Name.StartsWith(".")))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var match = Regex.Match(folder.Name, @"^\((\d+)\)\s*(.*)$|^(\d+)[-_ ]+(.*)$");
                        if (match.Success) stores.Add(new EJStoreInfo { StoreCode = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value,
                            StoreName = match.Groups[1].Success ? match.Groups[2].Value : match.Groups[4].Value, FolderName = folder.Name });
                    }
                    return stores.OrderBy(s => s.StoreCode).ToList();
                }
            }, cancellationToken);
        }
    }
}
