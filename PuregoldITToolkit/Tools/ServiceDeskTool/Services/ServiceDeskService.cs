using ClosedXML.Excel;
using PuregoldITToolkit.Tools.ServiceDeskTool.Interfaces;
using PuregoldITToolkit.Tools.ServiceDeskTool.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.ServiceDeskTool.Services
{
    public class ServiceDeskService : IServiceDeskService
    {
        public async Task<bool> ExportOtReportAsync(IEnumerable<OtEntryModel> entries, DateTime cutoffStart, DateTime cutoffEnd, string signatureHtml)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string cutoffPeriod = $"{cutoffStart:MMMM d} TO {cutoffEnd:MMMM d}, {cutoffEnd.Year}".ToUpper();
                    string subject = $"Overtime Application Approval";

                    string toAddresses = "jymendoza@puregold.com.ph";
                    string ccAddresses = "allITzone11@puregold.com.ph";

                    // 1. Generate Excel File
                    string excelFileName = $"Overtime Monitoring.xlsx";
                    string excelPath = Path.Combine(Path.GetTempPath(), excelFileName);

                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("OT Monitoring");

                        ws.Cell(1, 1).Value = "CUTOFF PERIOD:";
                        ws.Cell(1, 1).Style.Font.Bold = true;
                        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        ws.Cell(1, 2).Value = cutoffPeriod;
                        ws.Cell(1, 2).Style.Font.Bold = true;
                        ws.Cell(1, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        int headerRow = 3;
                        string[] headers = { "EMPLOYEE", "STORE", "DATE OF OVERTIME", "TOTAL HOUR(S)", "PURPOSE", "STORE OPERATION PRE-APPROVED BY" };

                        for (int i = 0; i < headers.Length; i++)
                        {
                            var cell = ws.Cell(headerRow, i + 1);
                            cell.Value = headers[i];
                            cell.Style.Font.Bold = true;
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        int currentRow = headerRow + 1;
                        foreach (var entry in entries)
                        {
                            ws.Cell(currentRow, 1).Value = entry.Employee;
                            ws.Cell(currentRow, 2).Value = entry.Store;
                            ws.Cell(currentRow, 3).Value = entry.DateOfOvertime.ToString("M/d/yyyy");
                            ws.Cell(currentRow, 4).Value = entry.Minutes == 0 ? $"{entry.Hours} HOURS" : $"{entry.Hours} HOURS {entry.Minutes} MINS";
                            ws.Cell(currentRow, 5).Value = entry.Purpose;
                            ws.Cell(currentRow, 6).Value = entry.PreApprovedBy;

                            for (int col = 1; col <= 6; col++)
                                ws.Cell(currentRow, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                            currentRow++;
                        }

                        ws.Columns().AdjustToContents();
                        workbook.SaveAs(excelPath);
                    }

                    // 2. Construct the .eml Draft with Multipart/Mixed for Attachments
                    string boundary = "----=_Part_" + Guid.NewGuid().ToString("N");
                    StringBuilder emlContent = new StringBuilder();

                    emlContent.AppendLine($"To: {toAddresses}");
                    emlContent.AppendLine($"Cc: {ccAddresses}");
                    emlContent.AppendLine("X-Unsent: 1");
                    emlContent.AppendLine($"Subject: {subject}");
                    emlContent.AppendLine("MIME-Version: 1.0");
                    emlContent.AppendLine($"Content-Type: multipart/mixed; boundary=\"{boundary}\"");
                    emlContent.AppendLine();

                    // HTML Body
                    emlContent.AppendLine($"--{boundary}");
                    emlContent.AppendLine("Content-Type: text/html; charset=utf-8");
                    emlContent.AppendLine();
                    StringBuilder htmlBody = new StringBuilder();
                    htmlBody.AppendLine("<html><body><div style=\"font-family: Calibri, Arial, sans-serif; font-size: 14px; color: #000;\">");
                    htmlBody.AppendLine("<p>Masayang araw,</p>");
                    htmlBody.AppendLine($"<p>OT for cut-off {cutoffPeriod}</p>");

                    if (!string.IsNullOrWhiteSpace(signatureHtml))
                    {
                        htmlBody.AppendLine($"<br/><br/>{signatureHtml}");
                    }

                    htmlBody.AppendLine("</div></body></html>");
                    emlContent.AppendLine(htmlBody.ToString());
                    emlContent.AppendLine();

                    // Excel Attachment Base64 Injection
                    byte[] fileBytes = File.ReadAllBytes(excelPath);
                    string base64File = Convert.ToBase64String(fileBytes, Base64FormattingOptions.InsertLineBreaks);

                    emlContent.AppendLine($"--{boundary}");
                    emlContent.AppendLine($"Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet; name=\"{excelFileName}\"");
                    emlContent.AppendLine("Content-Transfer-Encoding: base64");
                    emlContent.AppendLine($"Content-Disposition: attachment; filename=\"{excelFileName}\"");
                    emlContent.AppendLine();
                    emlContent.AppendLine(base64File);
                    emlContent.AppendLine($"--{boundary}--");

                    // Save and open in Thunderbird
                    string tempPath = Path.Combine(Path.GetTempPath(), $"OT_Report_Draft_{DateTime.Now:yyyyMMdd_HHmmss}.eml");
                    File.WriteAllText(tempPath, emlContent.ToString());

                    Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OT Email Draft Error: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> DraftOutageEmailAsync(OutageEmailModel data, string signatureHtml)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string toAddresses = data.Isp == "GLOBE"
                        ? "gowifihelpdesk@globe.com.ph, globedsgservicedesk@globe.com.ph"
                        : "mnstech@convergeict.com, Mnssupport@convergeict.com, enterprisesupport@convergeict.com";

                    string ccAddresses = $"{data.AllItZoneEmail}, {data.ZoneHeadName} <{data.ZoneHeadEmail}>";
                    string subject = $"NETWORK DOWN ({data.StoreCode} - {data.StoreName}) ({data.ModemSerial})";

                    string boundary = "----=_Part_" + Guid.NewGuid().ToString("N");
                    string contentId = "modem_img_" + Guid.NewGuid().ToString("N") + "@puregold.com";

                    StringBuilder emlContent = new StringBuilder();

                    emlContent.AppendLine($"To: {toAddresses}");
                    emlContent.AppendLine($"Cc: {ccAddresses}");
                    emlContent.AppendLine("X-Unsent: 1");
                    emlContent.AppendLine($"Subject: {subject}");
                    emlContent.AppendLine("MIME-Version: 1.0");
                    emlContent.AppendLine($"Content-Type: multipart/related; boundary=\"{boundary}\"");
                    emlContent.AppendLine();

                    emlContent.AppendLine($"--{boundary}");
                    emlContent.AppendLine("Content-Type: text/html; charset=utf-8");
                    emlContent.AppendLine();

                    string htmlBody = data.GeneratedBody.Replace("\r\n", "<br/>").Replace("\n", "<br/>");

                    if (!string.IsNullOrEmpty(data.AttachmentPath) && File.Exists(data.AttachmentPath))
                    {
                        htmlBody = htmlBody.Replace("[IMAGE ATTACHED]", $"<br/><img src=\"cid:{contentId}\" style=\"max-width: 600px; height: auto; border: 1px solid #ccc;\" /><br/>");
                    }

                    if (!string.IsNullOrWhiteSpace(signatureHtml))
                    {
                        htmlBody += $"<br/><br/>{signatureHtml}";
                    }

                    emlContent.AppendLine($"<html><body><div style=\"font-family: Calibri, Arial, sans-serif; font-size: 14px; color: #000;\">{htmlBody}</div></body></html>");
                    emlContent.AppendLine();

                    if (!string.IsNullOrEmpty(data.AttachmentPath) && File.Exists(data.AttachmentPath))
                    {
                        string fileName = Path.GetFileName(data.AttachmentPath);
                        byte[] fileBytes = File.ReadAllBytes(data.AttachmentPath);
                        string base64File = Convert.ToBase64String(fileBytes, Base64FormattingOptions.InsertLineBreaks);

                        string mimeType = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";

                        emlContent.AppendLine($"--{boundary}");
                        emlContent.AppendLine($"Content-Type: {mimeType}; name=\"{fileName}\"");
                        emlContent.AppendLine("Content-Transfer-Encoding: base64");
                        emlContent.AppendLine($"Content-ID: <{contentId}>");
                        emlContent.AppendLine($"Content-Disposition: inline; filename=\"{fileName}\"");
                        emlContent.AppendLine();
                        emlContent.AppendLine(base64File);
                    }

                    emlContent.AppendLine($"--{boundary}--");

                    string tempPath = Path.Combine(Path.GetTempPath(), $"NetworkOutage_{data.StoreCode}_{DateTime.Now:HHmmss}.eml");
                    File.WriteAllText(tempPath, emlContent.ToString());

                    Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Email Error: {ex.Message}");
                    return false;
                }
            });
        }
    }
}