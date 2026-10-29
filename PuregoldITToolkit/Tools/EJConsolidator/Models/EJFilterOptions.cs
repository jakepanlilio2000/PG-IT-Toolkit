using System;
using System.Collections.Generic;

namespace PuregoldITToolkit.Tools.EJConsolidator.Models
{
    public enum EJExecutionMode
    {
        Local,
        Ftp,
        FtpOthers
    }

    public class EJReceiptResult
    {
        public string SourceFile { get; set; }
        public string TransactionNumber { get; set; }
        public string Date { get; set; }
        public string ReceiptText { get; set; }
    }

    public class EJStoreInfo
    {
        public string StoreCode { get; set; }
        public string StoreName { get; set; }
        public string FolderName { get; set; }
        public string DisplayName => $"{StoreCode} · {StoreName}";
    }

    public class EJProgressReport
    {
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public int Percentage => TotalFiles > 0 ? (int)((double)ProcessedFiles / TotalFiles * 100) : 0;
        public string StatusMessage { get; set; } = string.Empty;
        public int MissingDates { get; set; }
        public int MatchedReceipts { get; set; }
        public EJReceiptResult Receipt { get; set; }
        public string DetectedStore { get; set; }
        public string OutputDirectory { get; set; }
    }

    public class EJFilterOptions
    {
        // Execution Mode
        public EJExecutionMode ExecutionMode { get; set; } = EJExecutionMode.Local;
        public bool ProcessAllStores { get; set; } = false;

        // Path & Directory Settings
        public string LocalStoreDirectory { get; set; }
        public string OutputDirectory { get; set; }

        // FTP Settings (for Remote / All Stores mode)
        public string FtpHost { get; set; }
        public int FtpPort { get; set; } = 21;
        public int FtpTimeoutSeconds { get; set; } = 30;
        public bool FtpPassiveMode { get; set; } = true;

        // Operation Modes
        public bool IsModeTrxFinder { get; set; }
        public string TargetTrxNumber { get; set; }
        public string StoreCode { get; set; }
        public List<DateTime> TargetDates { get; set; } = new List<DateTime>();
        public List<string> PosLanes { get; set; } = new List<string>();
        public bool MergeAllIntoOneFile { get; set; }

        // Live Server SFTP Settings (Conso)
        public string LiveServerIp { get; set; }

        internal int PublishedPreviewCount { get; set; }
        public bool UseCachedData { get; set; }
        public string SearchText { get; set; }
        public string ExcludeText { get; set; }
        public string PaymentMethod { get; set; }
        public string ReceiptType { get; set; }
        public decimal? MinimumAmount { get; set; }
        public decimal? MaximumAmount { get; set; }
        public TimeSpan? TimeFrom { get; set; }
        public TimeSpan? TimeTo { get; set; }

        // Content Filters
        public string SpecificCashier { get; set; }
        public string SpecificBagger { get; set; }
        public bool SecondReceiptOnly { get; set; }
        public bool GcashBarcodeOnly { get; set; }
        public bool HacsOnlineOnly { get; set; }
        public bool XReadZReadOnly { get; set; }
        public bool SeniorCitizenOnly { get; set; }
        public bool PwdOnly { get; set; }

        public string FilterCardLast4 { get; set; }
        public string FilterMemberName { get; set; }
        public string FilterExactAmount { get; set; }
        public string FilterProductOrSku { get; set; }
        public string FilterScPwdId { get; set; }
    }
}
