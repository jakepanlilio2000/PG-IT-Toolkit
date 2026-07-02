using System;
using System.Collections.Generic;

namespace PuregoldITToolkit.Tools.ScPwdReportTool.Models
{
    public class ScPwdReportOptions
    {
        public string StoreCode { get; set; }
        public string StoreName { get; set; }
        public string LiveServerIp { get; set; }
        public List<DateTime> TargetDates { get; set; } = new List<DateTime>();
        public List<string> PosLanes { get; set; } = new List<string>();

        public bool GenerateScReport { get; set; }
        public bool GeneratePwdReport { get; set; }
        public int ReportChunkDays { get; set; }
        public string ReportUserId { get; set; }
    }
}