using PuregoldITToolkit.Tools.ScPwdReportTool.Interfaces;
using PuregoldITToolkit.Tools.ScPwdReportTool.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.ScPwdReportTool.Services
{
    public class ScPwdReportService : IScPwdReportService
    {
        public async Task GenerateReportsViaApiAsync(ScPwdReportOptions options, IProgress<string> textProgress, IProgress<int> pctProgress)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputDir = Path.Combine(desktopPath, "SC_PWD_Reports");
            Directory.CreateDirectory(outputDir);

            List<string> terminals;
            if (options.MergeAllPos)
            {
                terminals = new List<string> { "0" };
            }
            else
            {
                if (options.PosLanes != null && options.PosLanes.Any())
                {
                    terminals = options.PosLanes
                        .Select(l => int.TryParse(l, out int num) ? num.ToString() : l)
                        .Distinct().OrderBy(t => t).ToList();
                }
                else
                {
                    terminals = new List<string> { "1", "2", "3", "4", "5", "6" };
                }
            }

            var requestedDates = options.TargetDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
            var dateChunks = ChunkBy(requestedDates, options.ReportChunkDays);

            int totalOperations = terminals.Count * dateChunks.Count;
            int currentOperation = 0;

            var cookieContainer = new CookieContainer();
            using (var handler = new HttpClientHandler { CookieContainer = cookieContainer })
            using (var client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromMinutes(10);

                foreach (var term in terminals)
                {
                    string displayTerm = (term == "0" && options.MergeAllPos) ? "ALL" : term.PadLeft(3, '0');
                    string payloadTerm = term;

                    foreach (var chunk in dateChunks)
                    {
                        DateTime minDate = chunk.Min();
                        DateTime maxDate = chunk.Max();

                        string apiDateRangeStr = $"{minDate:yyyy-MM-dd} to {maxDate:yyyy-MM-dd}";
                        string fileDateRangeStr = $"{minDate:yyyyMMdd}-{maxDate:yyyyMMdd}";

                        if (options.GenerateScReport)
                        {
                            textProgress?.Report($"[SC] Terminal {displayTerm}: Generating data for {apiDateRangeStr}...");
                            bool success = await TriggerReportApi(client, options.LiveServerIp, "get_senior_reports", apiDateRangeStr, payloadTerm);

                            if (success)
                            {
                                textProgress?.Report($"[SC] Terminal {displayTerm}: Downloading Excel...");
                                string downloadUrl = $"http://{options.LiveServerIp}/purepos_conso/prints/senior_sales_excel/{displayTerm}";

                                string fileName = $"{options.StoreCode}_{displayTerm}_{fileDateRangeStr}_senior_report.xls";
                                string filePath = Path.Combine(outputDir, fileName);

                                await DownloadExcelFile(client, downloadUrl, filePath);
                            }
                            else
                            {
                                textProgress?.Report($"[SC ERROR] Terminal {displayTerm}: Failed to generate {apiDateRangeStr}.");
                            }
                        }

                        if (options.GeneratePwdReport)
                        {
                            textProgress?.Report($"[PWD] Terminal {displayTerm}: Generating data for {apiDateRangeStr}...");
                            bool success = await TriggerReportApi(client, options.LiveServerIp, "get_pwd_reports", apiDateRangeStr, payloadTerm);

                            if (success)
                            {
                                textProgress?.Report($"[PWD] Terminal {displayTerm}: Downloading Excel...");
                                string downloadUrl = $"http://{options.LiveServerIp}/purepos_conso/prints/pwd_sales_excel/{displayTerm}";

                                string fileName = $"{options.StoreCode}_{displayTerm}_{fileDateRangeStr}_pwd_report.xls";
                                string filePath = Path.Combine(outputDir, fileName);

                                await DownloadExcelFile(client, downloadUrl, filePath);
                            }
                            else
                            {
                                textProgress?.Report($"[PWD ERROR] Terminal {displayTerm}: Failed to generate {apiDateRangeStr}.");
                            }
                        }

                        currentOperation++;
                        pctProgress?.Report((int)((double)currentOperation / totalOperations * 100));
                    }
                }
            }
        }

        private async Task<bool> TriggerReportApi(HttpClient client, string ip, string endpoint, string dateRange, string terminalId)
        {
            try
            {
                string url = $"http://{ip}/purepos_conso/prints/{endpoint}";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("date_range", dateRange),
                    new KeyValuePair<string, string>("terminal_id", terminalId)
                });

                var response = await client.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task DownloadExcelFile(HttpClient client, string url, string destinationPath)
        {
            try
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Download failed from {url}: {ex.Message}");
            }
        }

        private List<List<T>> ChunkBy<T>(List<T> source, int chunkSize)
        {
            if (!source.Any()) return new List<List<T>>();
            if (chunkSize <= 0) chunkSize = source.Count;

            return source.Select((x, i) => new { Index = i, Value = x })
                         .GroupBy(x => x.Index / chunkSize)
                         .Select(x => x.Select(v => v.Value).ToList()).ToList();
        }
    }
}
