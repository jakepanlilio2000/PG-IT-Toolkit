using PuregoldITToolkit.Tools.ScPwdReportTool.Models;
using System;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.ScPwdReportTool.Interfaces
{
    public interface IScPwdReportService
    {
        Task<int> GenerateReportsAsync(ScPwdReportOptions options, IProgress<string> textProgress, IProgress<int> pctProgress);
    }
}