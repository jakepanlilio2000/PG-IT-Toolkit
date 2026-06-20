using PuregoldITToolkit.Tools.ServiceDeskTool.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.ServiceDeskTool.Interfaces
{
    public interface IServiceDeskService
    {
        Task<bool> ExportOtReportAsync(IEnumerable<OtEntryModel> entries, DateTime cutoffStart, DateTime cutoffEnd, string signatureHtml);
        Task<bool> DraftOutageEmailAsync(OutageEmailModel data, string signatureHtml);
    }
}