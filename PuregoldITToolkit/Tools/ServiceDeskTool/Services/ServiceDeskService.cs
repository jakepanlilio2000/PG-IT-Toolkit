using ClosedXML.Excel;
using PuregoldITToolkit.Tools.ServiceDeskTool.Interfaces;
using PuregoldITToolkit.Tools.ServiceDeskTool.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.ServiceDeskTool.Services
{
    public class ServiceDeskService : IServiceDeskService
    {
        public async Task<bool> ExportOtReportAsync(IEnumerable<OtEntryModel> entries, string outputPath, DateTime cutoffStart, DateTime cutoffEnd)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("OT Monitoring");

                        // 1. CUTOFF PERIOD - Label in 1A, Period in 1B
                        ws.Cell(1, 1).Value = "CUTOFF PERIOD:";
                        ws.Cell(1, 1).Style.Font.Bold = true;
                        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // Format: MAY 20 TO JUNE 4, 2026
                        string cutoffPeriod = $"{cutoffStart:MMMM d} TO {cutoffEnd:MMMM d}, {cutoffEnd.Year}".ToUpper();
                        ws.Cell(1, 2).Value = cutoffPeriod;
                        ws.Cell(1, 2).Style.Font.Bold = true;
                        ws.Cell(1, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // 5. Row 2 is empty (skip one line after cutoff)
                        // Headers start at row 3
                        int headerRow = 3;

                        // 2. Setup Headers - NO background color, just bold and centered
                        string[] headers = { "EMPLOYEE", "STORE", "DATE OF OVERTIME", "TOTAL HOUR(S)", "PURPOSE", "STORE OPERATION PRE-APPROVED BY" };

                        for (int i = 0; i < headers.Length; i++)
                        {
                            var cell = ws.Cell(headerRow, i + 1);
                            cell.Value = headers[i];
                            cell.Style.Font.Bold = true;
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        // 3. Write Data starting at row 4
                        int currentRow = headerRow + 1;

                        foreach (var entry in entries)
                        {
                            ws.Cell(currentRow, 1).Value = entry.Employee;
                            ws.Cell(currentRow, 2).Value = entry.Store;
                            ws.Cell(currentRow, 3).Value = entry.DateOfOvertime.ToString("M/d/yyyy");

                            // Format hours and minutes
                            string hoursText = entry.Minutes == 0
                                ? $"{entry.Hours} HOURS"
                                : $"{entry.Hours} HOURS {entry.Minutes} MINS";
                            ws.Cell(currentRow, 4).Value = hoursText;

                            ws.Cell(currentRow, 5).Value = entry.Purpose;
                            ws.Cell(currentRow, 6).Value = entry.PreApprovedBy;

                            // 2. Center all data cells
                            for (int col = 1; col <= 6; col++)
                            {
                                ws.Cell(currentRow, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            }

                            currentRow++;
                        }

                        // 3. Auto fit columns based on content
                        ws.Columns().AdjustToContents();

                        workbook.SaveAs(outputPath);
                    }

                    Process.Start(new ProcessStartInfo { FileName = outputPath, UseShellExecute = true });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Excel Error: {ex.Message}");
                    return false;
                }
            });
        }

        private string FormatOtHours(double totalHours)
        {
            int hours = (int)Math.Floor(totalHours);
            double minutesDecimal = totalHours - hours;
            int minutes = (int)Math.Round(minutesDecimal * 60);

            if (minutes == 0)
                return $"{hours} HOURS";
            else
                return $"{hours} HOURS {minutes} MINS";
        }

        public async Task<bool> DraftOutageEmailAsync(OutageEmailModel data)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 1. Determine Dynamic Recipients
                    string toAddresses = data.Isp == "GLOBE"
                        ? "gowifihelpdesk@globe.com.ph, globedsgservicedesk@globe.com.ph"
                        : "mnstech@convergeict.com, Mnssupport@convergeict.com, enterprisesupport@convergeict.com";

                    // Add Persistent Recipients
                    toAddresses += $", {data.AllItZoneEmail}, {data.ZoneHeadName} <{data.ZoneHeadEmail}>";

                    // 2. Auto Create Subject
                    string subject = $"NETWORK DOWN ({data.StoreCode} - {data.StoreName}) ({data.ModemSerial})";

                    // 3. Setup Multipart Related boundaries for Inline Images
                    string boundary = "----=_Part_" + Guid.NewGuid().ToString("N");
                    string contentId = "modem_img_" + Guid.NewGuid().ToString("N") + "@puregold.com";

                    StringBuilder emlContent = new StringBuilder();

                    emlContent.AppendLine($"To: {toAddresses}");
                    emlContent.AppendLine("X-Unsent: 1"); // Marks as draft
                    emlContent.AppendLine($"Subject: {subject}");
                    emlContent.AppendLine("MIME-Version: 1.0");
                    emlContent.AppendLine($"Content-Type: multipart/related; boundary=\"{boundary}\"");
                    emlContent.AppendLine();

                    // 4. Build the HTML Body
                    emlContent.AppendLine($"--{boundary}");
                    emlContent.AppendLine("Content-Type: text/html; charset=utf-8");
                    emlContent.AppendLine();

                    // Convert plain text newlines to HTML breaks
                    string htmlBody = data.GeneratedBody.Replace("\r\n", "<br/>").Replace("\n", "<br/>");

                    // Swap the placeholder with the inline image tag
                    if (!string.IsNullOrEmpty(data.AttachmentPath) && File.Exists(data.AttachmentPath))
                    {
                        htmlBody = htmlBody.Replace("[IMAGE ATTACHED]", $"<br/><img src=\"cid:{contentId}\" style=\"max-width: 600px; height: auto; border: 1px solid #ccc;\" /><br/>");
                    }

                    // Wrap in standard HTML font styling
                    emlContent.AppendLine($"<html><body><div style=\"font-family: Arial, sans-serif; font-size: 14px; color: #000;\">{htmlBody}</div></body></html>");
                    emlContent.AppendLine();

                    // 5. Inject the Base64 Image Attachment as an Inline Part
                    if (!string.IsNullOrEmpty(data.AttachmentPath) && File.Exists(data.AttachmentPath))
                    {
                        string fileName = Path.GetFileName(data.AttachmentPath);
                        byte[] fileBytes = File.ReadAllBytes(data.AttachmentPath);
                        string base64File = Convert.ToBase64String(fileBytes, Base64FormattingOptions.InsertLineBreaks);

                        // Basic MIME type mapping
                        string mimeType = "image/jpeg";
                        if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) mimeType = "image/png";

                        emlContent.AppendLine($"--{boundary}");
                        emlContent.AppendLine($"Content-Type: {mimeType}; name=\"{fileName}\"");
                        emlContent.AppendLine("Content-Transfer-Encoding: base64");
                        emlContent.AppendLine($"Content-ID: <{contentId}>");
                        emlContent.AppendLine($"Content-Disposition: inline; filename=\"{fileName}\"");
                        emlContent.AppendLine();
                        emlContent.AppendLine(base64File);
                    }

                    emlContent.AppendLine($"--{boundary}--");

                    // 6. Save to temp folder and open with Thunderbird
                    string tempPath = Path.Combine(Path.GetTempPath(), $"NetworkOutage_{data.StoreCode}_{DateTime.Now:HHmmss}.eml");
                    File.WriteAllText(tempPath, emlContent.ToString());

                    Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Email Error: {ex.Message}");
                    return false;
                }
            });
        }
    }
}