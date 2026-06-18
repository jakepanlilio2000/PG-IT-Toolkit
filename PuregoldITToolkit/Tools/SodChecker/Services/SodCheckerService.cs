using PuregoldITToolkit.Tools.SodChecker.Interfaces;
using PuregoldITToolkit.Tools.SodChecker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinSCP;
using System.Text.Json;

namespace PuregoldITToolkit.Tools.SodChecker.Services
{
    public class SodCheckerService : ISodCheckerService
    {
        public async Task<List<StoreConfig>> GetStoreListAsync()
        {
            var stores = new List<StoreConfig>();
            try
            {
                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                string csvPath = Path.Combine(dataDir, "stores.csv");

                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                if (!File.Exists(csvPath)) File.WriteAllText(csvPath, "722,SAN FERNANDO 2,AUTO,PUREPOS\n697,CALUMPIT,AUTO,PUREPOS");

                var lines = await Task.Run(() => File.ReadAllLines(csvPath));
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length >= 4)
                    {
                        stores.Add(new StoreConfig { StoreCode = parts[0].Trim(), StoreName = parts[1].Trim(), StoreType = parts[3].Trim() });
                    }
                }
            }
            catch { }
            return stores;
        }

        public async Task SaveStoresAsync(IEnumerable<StoreConfig> stores)
        {
            string csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "stores.csv");
            var lines = stores.Select(s => $"{s.StoreCode},{s.StoreName},AUTO,{s.StoreType}");
            await Task.Run(() => File.WriteAllLines(csvPath, lines));
        }

        public async Task ScanRowsAsync(IEnumerable<SodStoreResult> rows, string targetCol, IProgress<string> progress, IProgress<int> pctProgress)
        {
            await Task.Run(() =>
            {
                progress?.Report("Connecting to Main FTP (.177)...");
                Session mainSession = ConnectToMainFtp();
                bool hasMain = mainSession != null && mainSession.Opened;

                progress?.Report("Connecting to Outbound FTP (.84)...");
                Session outSession = ConnectToOutboundFtp();
                bool hasOut = outSession != null && outSession.Opened;

                var tohoMap = new Dictionary<string, string>();
                var ejMap = new Dictionary<string, string>();

                if (hasMain)
                {
                    progress?.Report("Mapping directory structures on FTP...");
                    try
                    {
                        foreach (var f in mainSession.ListDirectory("/toho/").Files)
                        {
                            var match = Regex.Match(f.Name, @"^\((\d+)\)");
                            if (match.Success) tohoMap[match.Groups[1].Value] = f.Name;
                        }
                        foreach (var f in mainSession.ListDirectory("/PUREPOS_EJRECEIPT/").Files)
                        {
                            var match = Regex.Match(f.Name, @"^\((\d+)\)");
                            if (match.Success) ejMap[match.Groups[1].Value] = f.Name;
                        }
                    }
                    catch { }
                }

                int total = rows.Count();
                int current = 0;

                foreach (var row in rows)
                {
                    current++;
                    pctProgress?.Report((int)((double)current / total * 100));
                    progress?.Report($"Scanning target: {row.DisplayId} - {row.DisplayName}...");

                    string sc = row.TargetStoreCode;
                    string Ymd = row.TargetDate.ToString("yyyyMMdd");
                    string mdy = row.TargetDate.ToString("MMddyy");
                    string mdY_Prev = row.TargetDate.AddDays(-2).ToString("MMddyyyy");

                    // Mark skipped columns if in Targeted Mode
                    if (targetCol != "ALL")
                    {
                        row.EjStatus.State = targetCol == "EJ" ? ScanState.Missing : ScanState.Skipped;
                        row.PollogStatus.State = targetCol == "Pollog" ? ScanState.Missing : ScanState.Skipped;
                        row.CrmStatus.State = targetCol == "CRM" ? ScanState.Missing : ScanState.Skipped;
                        row.PromoStatus.State = targetCol == "Promo" ? ScanState.Missing : ScanState.Skipped;
                        row.BirStatus.State = targetCol == "BIR" ? ScanState.Missing : ScanState.Skipped;
                        row.NonTradeStatus.State = targetCol == "NonTrade" ? ScanState.Missing : ScanState.Skipped;
                        row.DosStatus.State = targetCol == "DOS Log" ? ScanState.Missing : ScanState.Skipped;
                        row.MobilePriceStatus.State = targetCol == "Mob Price" ? ScanState.Missing : ScanState.Skipped;
                        row.PlusKuPriceStatus.State = targetCol == "PlusKu" ? ScanState.Missing : ScanState.Skipped;
                        row.RegPriceStatus.State = targetCol == "Reg Price" ? ScanState.Missing : ScanState.Skipped;
                        row.Sis98Status.State = targetCol == "SIS98" ? ScanState.Missing : ScanState.Skipped;
                        row.ShelftagStatus.State = targetCol == "Shelftag" ? ScanState.Missing : ScanState.Skipped;
                        row.KioskStatus.State = targetCol == "Kiosk" ? ScanState.Missing : ScanState.Skipped;
                    }

                    if (hasMain)
                    {
                        if (targetCol == "ALL" || targetCol == "CRM") row.CrmStatus = CheckFile(mainSession, $"/toho/CRM_DAILY_UPDATES_FROM_STORES/automated/{sc}_{Ymd}_crm.zip");
                        if (targetCol == "ALL" || targetCol == "Promo") row.PromoStatus = CheckFile(mainSession, $"/toho/PROMO_AVAILMENT_REPORT/automated/{sc}_{Ymd}.zip");
                        if (targetCol == "ALL" || targetCol == "BIR") row.BirStatus = CheckBirFile(mainSession, $"/toho/Anahaw-BIR-EIS/EIS_{sc}_{Ymd}.txt");

                        if (ejMap.TryGetValue(sc, out string ejFolder) && (targetCol == "ALL" || targetCol == "EJ"))
                            row.EjStatus = CheckFile(mainSession, $"/PUREPOS_EJRECEIPT/{ejFolder}/{mdY_Prev}_EJReceiptJournal.zip");

                        if (tohoMap.TryGetValue(sc, out string tohoFolder))
                        {
                            string baseToho = $"/toho/{tohoFolder}/";
                            if (targetCol == "ALL" || targetCol == "Pollog") row.PollogStatus = CheckFile(mainSession, $"{baseToho}Pollog/{sc}_{mdy}.zip");
                            if (targetCol == "ALL" || targetCol == "Mob Price") row.MobilePriceStatus = CheckFile(mainSession, $"{baseToho}Puregoldpos_Pricelist/mobile/{sc}_pricelistma_{Ymd}.zip");
                            if (targetCol == "ALL" || targetCol == "PlusKu") row.PlusKuPriceStatus = CheckFile(mainSession, $"{baseToho}Puregoldpos_Pricelist/plusku/{sc}_pricelistplusku_{Ymd}.zip");
                            if (targetCol == "ALL" || targetCol == "Reg Price") row.RegPriceStatus = CheckFile(mainSession, $"{baseToho}Puregoldpos_Pricelist/regular/{sc}_pricelistreg_{Ymd}.zip");

                            string updatePath = $"{baseToho}UPDATE_STATUS/";
                            if (targetCol == "ALL" || targetCol == "SIS98") row.Sis98Status = CheckStatusLog(mainSession, updatePath, "sis98_auto_update_", Ymd, "Start Updating Process");
                            if (targetCol == "ALL" || targetCol == "Shelftag") row.ShelftagStatus = CheckStatusLog(mainSession, updatePath, "Shelftag_auto_update_", Ymd, "TOTAL BULK INSERT COUNT");
                            if (targetCol == "ALL" || targetCol == "Kiosk") row.KioskStatus = CheckStatusLog(mainSession, updatePath, "kiosk_auto_update_", Ymd, "PROCESS COMPLETED!");
                        }
                    }

                    if (hasOut)
                    {
                        if (targetCol == "ALL" || targetCol == "NonTrade")
                        {
                            row.NonTradeStatus = CheckFile(outSession, $"/outbound_ftp/sales_log/{sc}_NonTrade_{Ymd}.dat");

                            // Check for EMPTY_ file if the main one is missing
                            if (row.NonTradeStatus.State == ScanState.Missing && CheckFile(outSession, $"/outbound_ftp/sales_log/EMPTY_{sc}_NonTrade_{Ymd}.dat").State == ScanState.Ok)
                            {
                                row.NonTradeStatus.State = ScanState.Empty;
                                // Line removed here!
                            }
                        }

                        if (targetCol == "ALL" || targetCol == "DOS Log")
                        {
                            row.DosStatus = CheckFile(outSession, $"/outbound_ftp/sales_log/{sc}/{sc}_{Ymd}.dos");
                        }
                    }
                }
                SaveRowsToCache(rows, targetCol);

                mainSession?.Dispose();
                outSession?.Dispose();
            });
        }

        // --- WINSCP HELPER METHODS ---

        private Session ConnectToMainFtp()
        {
            var session = new Session();
            var users = new List<string>();

            // Replicating f.php users logic
            for (int i = 1; i <= 12; i++) users.Add($"ftp{i}@puregold");
            users.Add("ftp1a@puregold");
            users.Add("ftp1b@puregold");
            users.Add("ftp1c@puregold");

            foreach (var user in users)
            {
                try
                {
                    session.Open(new SessionOptions
                    {
                        Protocol = Protocol.Ftp,
                        HostName = "192.168.200.177",
                        UserName = user,
                        Password = "pw@1234"
                    });
                    return session;
                }
                catch { }
            }
            return null;
        }

        private Session ConnectToOutboundFtp()
        {
            try
            {
                var session = new Session();
                session.Open(new SessionOptions
                {
                    Protocol = Protocol.Ftp,
                    HostName = "192.168.200.84",
                    UserName = "anahawftp",
                    Password = "an@haw"
                });
                return session;
            }
            catch { return null; }
        }

        private FileCheckStatus CheckFile(Session session, string fullPath)
        {
            try
            {
                if (session.FileExists(fullPath))
                {
                    var fileInfo = session.GetFileInfo(fullPath);
                    return new FileCheckStatus { State = ScanState.Ok, FileName = fileInfo.Name, SizeBytes = fileInfo.Length };
                }
            }
            catch { }
            return new FileCheckStatus { State = ScanState.Missing };
        }

        private FileCheckStatus CheckBirFile(Session session, string fullPath)
        {
            try
            {
                if (session.FileExists(fullPath))
                {
                    var fileInfo = session.GetFileInfo(fullPath);
                    string tempFile = Path.GetTempFileName();
                    try
                    {
                        session.GetFiles(fullPath, tempFile).Check();
                        int lineCount = File.ReadLines(tempFile).Count(line => !string.IsNullOrWhiteSpace(line));

                        if (lineCount <= 1)
                            return new FileCheckStatus { State = ScanState.Rerun, FileName = fileInfo.Name, SizeBytes = fileInfo.Length };

                        return new FileCheckStatus { State = ScanState.Ok, FileName = fileInfo.Name, SizeBytes = fileInfo.Length };
                    }
                    finally
                    {
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                    }
                }
            }
            catch { }
            return new FileCheckStatus { State = ScanState.Missing };
        }

        private FileCheckStatus CheckStatusLog(Session session, string path, string prefix, string dateStr, string successKeyword)
        {
            try
            {
                if (!session.FileExists(path)) return new FileCheckStatus { State = ScanState.Missing };

                var files = session.ListDirectory(path).Files
                    .Where(f => !f.IsDirectory && f.Name.StartsWith(prefix + dateStr))
                    .OrderByDescending(f => f.Name)
                    .ToList();

                if (files.Any())
                {
                    long latestSize = files.First().Length;
                    string latestName = files.First().Name;

                    foreach (var file in files)
                    {
                        string tempFile = Path.GetTempFileName();
                        try
                        {
                            session.GetFiles(path + file.Name, tempFile).Check();
                            string content = File.ReadAllText(tempFile);
                            if (content.Contains(successKeyword))
                            {
                                return new FileCheckStatus { State = ScanState.Ok, FileName = file.Name, SizeBytes = file.Length };
                            }
                        }
                        finally
                        {
                            if (File.Exists(tempFile)) File.Delete(tempFile);
                        }
                    }
                    return new FileCheckStatus { State = ScanState.Rerun, FileName = latestName, SizeBytes = latestSize };
                }
            }
            catch { }
            return new FileCheckStatus { State = ScanState.Missing };
        }

        // Cache Storage DTOs to avoid serializing WPF elements
        private class CacheColumnDto
        {
            public int State { get; set; }
            public string FileName { get; set; }
            public long SizeBytes { get; set; }
        }

        private class CacheStoreDto
        {
            public Dictionary<string, CacheColumnDto> Columns { get; set; } = new Dictionary<string, CacheColumnDto>();
        }

        private string GetCacheFilePath(DateTime date)
        {
            string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "cache");
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, $"cache_{date:yyyyMMdd}.json");
        }

        // 1. Core Cache Loader Routine
        public async Task LoadCachedDataAsync(IEnumerable<SodStoreResult> rows)
        {
            await Task.Run(() =>
            {
                // Group rows by target date to open cache files efficiently
                var rowsByDate = rows.GroupBy(r => r.TargetDate.Date);

                foreach (var dateGroup in rowsByDate)
                {
                    string cachePath = GetCacheFilePath(dateGroup.Key);
                    if (!File.Exists(cachePath)) continue;

                    try
                    {
                        string json = File.ReadAllText(cachePath);
                        var cacheMap = JsonSerializer.Deserialize<Dictionary<string, CacheStoreDto>>(json);
                        if (cacheMap == null) continue;

                        foreach (var row in dateGroup)
                        {
                            if (cacheMap.TryGetValue(row.TargetStoreCode, out var cachedStore))
                            {
                                ApplyColumnCache(row.EjStatus, "EJ", cachedStore);
                                ApplyColumnCache(row.PollogStatus, "Pollog", cachedStore);
                                ApplyColumnCache(row.CrmStatus, "CRM", cachedStore);
                                ApplyColumnCache(row.PromoStatus, "Promo", cachedStore);
                                ApplyColumnCache(row.BirStatus, "BIR", cachedStore);
                                ApplyColumnCache(row.NonTradeStatus, "NonTrade", cachedStore);
                                ApplyColumnCache(row.DosStatus, "DOS Log", cachedStore);
                                ApplyColumnCache(row.MobilePriceStatus, "Mob Price", cachedStore);
                                ApplyColumnCache(row.PlusKuPriceStatus, "PlusKu", cachedStore);
                                ApplyColumnCache(row.RegPriceStatus, "Reg Price", cachedStore);
                                ApplyColumnCache(row.Sis98Status, "SIS98", cachedStore);
                                ApplyColumnCache(row.ShelftagStatus, "Shelftag", cachedStore);
                                ApplyColumnCache(row.KioskStatus, "Kiosk", cachedStore);
                            }
                        }
                    }
                    catch { /* Handle corrupted cache silently */ }
                }
            });
        }

        private void ApplyColumnCache(FileCheckStatus status, string colKey, CacheStoreDto cachedStore)
        {
            if (cachedStore.Columns.TryGetValue(colKey, out var colCache))
            {
                status.State = (ScanState)colCache.State;
                status.FileName = colCache.FileName;
                status.SizeBytes = colCache.SizeBytes;
            }
        }

        // 2. Core Cache Saver Routine (Call this inside ScanRowsAsync after completion)
        private void SaveRowsToCache(IEnumerable<SodStoreResult> rows, string targetCol)
        {
            var rowsByDate = rows.GroupBy(r => r.TargetDate.Date);

            foreach (var dateGroup in rowsByDate)
            {
                string cachePath = GetCacheFilePath(dateGroup.Key);
                var cacheMap = new Dictionary<string, CacheStoreDto>();

                // Load existing cache to preserve non-scanned columns
                if (File.Exists(cachePath))
                {
                    try
                    {
                        string existingJson = File.ReadAllText(cachePath);
                        cacheMap = JsonSerializer.Deserialize<Dictionary<string, CacheStoreDto>>(existingJson) ?? new Dictionary<string, CacheStoreDto>();
                    }
                    catch { cacheMap = new Dictionary<string, CacheStoreDto>(); }
                }

                foreach (var row in dateGroup)
                {
                    if (!cacheMap.TryGetValue(row.TargetStoreCode, out var storeDto))
                    {
                        storeDto = new CacheStoreDto();
                        cacheMap[row.TargetStoreCode] = storeDto;
                    }

                    // Update only processed or valid columns
                    UpdateColumnCache(storeDto, "EJ", row.EjStatus, targetCol);
                    UpdateColumnCache(storeDto, "Pollog", row.PollogStatus, targetCol);
                    UpdateColumnCache(storeDto, "CRM", row.CrmStatus, targetCol);
                    UpdateColumnCache(storeDto, "Promo", row.PromoStatus, targetCol);
                    UpdateColumnCache(storeDto, "BIR", row.BirStatus, targetCol);
                    UpdateColumnCache(storeDto, "NonTrade", row.NonTradeStatus, targetCol);
                    UpdateColumnCache(storeDto, "DOS Log", row.DosStatus, targetCol);
                    UpdateColumnCache(storeDto, "Mob Price", row.MobilePriceStatus, targetCol);
                    UpdateColumnCache(storeDto, "PlusKu", row.PlusKuPriceStatus, targetCol);
                    UpdateColumnCache(storeDto, "Reg Price", row.RegPriceStatus, targetCol);
                    UpdateColumnCache(storeDto, "SIS98", row.Sis98Status, targetCol);
                    UpdateColumnCache(storeDto, "Shelftag", row.ShelftagStatus, targetCol);
                    UpdateColumnCache(storeDto, "Kiosk", row.KioskStatus, targetCol);
                }

                try
                {
                    string outputJson = JsonSerializer.Serialize(cacheMap, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(cachePath, outputJson);
                }
                catch { }
            }
        }

        private void UpdateColumnCache(CacheStoreDto storeDto, string colKey, FileCheckStatus status, string targetCol)
        {
            // Skip updating cache if the column was explicitly skipped in targeted scanning mode
            if (targetCol != "ALL" && targetCol != colKey) return;

            storeDto.Columns[colKey] = new CacheColumnDto
            {
                State = (int)status.State,
                FileName = status.FileName,
                SizeBytes = status.SizeBytes
            };
        }
    }
}