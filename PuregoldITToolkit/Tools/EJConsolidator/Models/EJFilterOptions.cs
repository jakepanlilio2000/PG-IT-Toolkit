using System;
using System.Collections.Generic;

namespace PuregoldITToolkit.Tools.EJConsolidator.Models
{
    public class EJFilterOptions
    {
        public string StoreCode { get; set; }
        public string StoreName { get; set; } // NEW: User input for Store Name/Description
        public string LiveServerIp { get; set; }
        public List<DateTime> TargetDates { get; set; } = new List<DateTime>();
        public List<string> PosLanes { get; set; }

        public bool IsModeConsolidator { get; set; }
        public bool IsModeTrxFinder { get; set; }
        public string TargetTrxNumber { get; set; }

        public bool SecondReceiptOnly { get; set; }
        public bool GcashBarcodeOnly { get; set; }
        public bool HacsOnlineOnly { get; set; }
        public bool XReadZReadOnly { get; set; }
        public bool MergeAllIntoOneFile { get; set; }

        public string SpecificCashier { get; set; }
        public string SpecificBagger { get; set; }
        public string FilterExactAmount { get; set; }
        public string FilterMemberName { get; set; }
        public string FilterCardLast4 { get; set; }
        public string FilterProductOrSku { get; set; }

        public bool GenerateScReport { get; set; }
        public bool GeneratePwdReport { get; set; }
        public int ReportChunkDays { get; set; } = 3;

        public string ReportUserId { get; set; } 
    }
}