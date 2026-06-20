using PuregoldITToolkit.Tools.FormsTool.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PuregoldITToolkit.Tools.FormsTool.Interfaces
{
    public interface IFormsExportService
    {
        Task<bool> ExportInfToEmailAsync(IEnumerable<InfEntryModel> entries, string infType, string storeCode, string storeName, string toAddresses, string ccAddresses, string signatureHtml, IEnumerable<string> screenshotPaths);

        Task<bool> ExportObToImageAsync(ObModel data, string templatePath, string outputPath);
        Task<ImageSource> GenerateObPreviewAsync(ObModel data, string templatePath);

        Task<bool> ExportTsrfToImageAsync(TsrfModel data, string templatePath, string outputPath);
        Task<ImageSource> GenerateTsrfPreviewAsync(TsrfModel data, string templatePath);

        Task<bool> PrintSsrfBatchAsync(SsrfModel data, string templatePath);
        Task<ImageSource> GenerateSsrfPreviewAsync(SsrfModel data, string templatePath);
    }
}