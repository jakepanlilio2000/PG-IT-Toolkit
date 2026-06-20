using System;
using System.Collections.Generic;

namespace PuregoldITToolkit.Tools.EJConsolidator.Models
{
    public class EJFilterOptions
    {
        // Modes
        public bool IsModeTrxFinder { get; set; }
        public string TargetTrxNumber { get; set; }

        public string StoreCode { get; set; }
        public List<DateTime> TargetDates { get; set; } = new List<DateTime>();
        public List<string> PosLanes { get; set; } = new List<string>();
        public bool MergeAllIntoOneFile { get; set; }

        // Settings
        public string LiveServerIp { get; set; }
        public int TotalPosCount { get; set; }

        // Filters
        public string SpecificCashier { get; set; }
        public string SpecificBagger { get; set; }
        public bool SecondReceiptOnly { get; set; }
        public bool GcashBarcodeOnly { get; set; }
        public bool HacsOnlineOnly { get; set; }
        public bool XReadZReadOnly { get; set; }
        public string FilterCardLast4 { get; set; }
        public string FilterMemberName { get; set; }
        public string FilterExactAmount { get; set; }
    }

}