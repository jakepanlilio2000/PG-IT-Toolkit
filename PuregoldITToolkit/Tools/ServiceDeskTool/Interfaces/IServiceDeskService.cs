using PuregoldITToolkit.Tools.ServiceDeskTool.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.ServiceDeskTool.Interfaces
{
    public interface IServiceDeskService
    {
        Task<bool> ExportOtReportAsync(IEnumerable<OtEntryModel> entries, string outputPath, DateTime cutoffStart, DateTime cutoffEnd);
        Task<bool> DraftOutageEmailAsync(OutageEmailModel data);
    }
}