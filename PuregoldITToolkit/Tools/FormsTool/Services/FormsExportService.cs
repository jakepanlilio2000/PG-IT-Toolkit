using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PuregoldITToolkit.Tools.FormsTool.Interfaces;
using PuregoldITToolkit.Tools.FormsTool.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bold = DocumentFormat.OpenXml.Wordprocessing.Bold;
// OpenXML Aliases
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace PuregoldITToolkit.Tools.FormsTool.Services
{
    public class FormsExportService : IFormsExportService
    {
        public async Task<bool> ExportInfToEmailAsync(IEnumerable<InfEntryModel> entries, string infType, string storeCode, string storeName, string toAddresses, string ccAddresses, string signatureHtml, IEnumerable<string> screenshotPaths)
        {
            if (entries == null || !entries.Any()) return false;

            return await Task.Run(() =>
            {
                try
                {
                    bool isType4 = infType.Contains("APAR");

                    // Strips the "1. ", "2. ", etc., from the INF string
                    string cleanInfType = Regex.Replace(infType, @"^\d+\.\s*", "");

                    string subject = $"Item Not Found: {storeCode}-{storeName} Purepos";
                    string boundary = "----=_Part_" + Guid.NewGuid().ToString("N");

                    StringBuilder emlContent = new StringBuilder();

                    // Mail Headers
                    emlContent.AppendLine($"To: {toAddresses}");
                    if (!string.IsNullOrWhiteSpace(ccAddresses)) emlContent.AppendLine($"Cc: {ccAddresses}");
                    emlContent.AppendLine("X-Unsent: 1");
                    emlContent.AppendLine($"Subject: {subject}");
                    emlContent.AppendLine("MIME-Version: 1.0");
                    emlContent.AppendLine($"Content-Type: multipart/related; boundary=\"{boundary}\"");
                    emlContent.AppendLine();

                    // HTML Body Construction
                    emlContent.AppendLine($"--{boundary}");
                    emlContent.AppendLine("Content-Type: text/html; charset=utf-8");
                    emlContent.AppendLine();

                    StringBuilder htmlBody = new StringBuilder();
                    htmlBody.AppendLine("<html><body>");

                    htmlBody.AppendLine("<p>Masayang araw po,<br><br>");
                    htmlBody.AppendLine($"Makikisuyo po sana INF Concern for {storeCode} - {storeName}<br><br>");
                    htmlBody.AppendLine($"Concern (UNMATCH / INF):&nbsp; {cleanInfType}</p><br>");

                    // Plain Table (No Styles)
                    htmlBody.AppendLine("<table border=\"1\">");
                    htmlBody.AppendLine("<tr>");

                    if (isType4)
                        htmlBody.AppendLine("<th>Store</th><th>Reg SKU</th><th>Gen SKU (APAR)</th><th>UPC</th><th>PurePos Price</th><th>MMS Price</th><th>Description</th><th>IsPromo?</th>");
                    else
                        htmlBody.AppendLine("<th>Store</th><th>SKU</th><th>UPC</th><th>PurePos Price</th><th>MMS Price</th><th>Description</th><th>IsPromo?</th>");

                    htmlBody.AppendLine("</tr>");

                    foreach (var entry in entries)
                    {
                        htmlBody.AppendLine("<tr>");
                        htmlBody.AppendLine($"<td>{entry.StoreCode}</td>");
                        htmlBody.AppendLine($"<td>{entry.Sku}</td>");
                        if (isType4) htmlBody.AppendLine($"<td>{entry.GeneratedSku}</td>");
                        htmlBody.AppendLine($"<td>{entry.Upc}</td>");
                        htmlBody.AppendLine($"<td>{entry.PurePosPrice}</td>");
                        htmlBody.AppendLine($"<td>{entry.MmsPrice}</td>");
                        htmlBody.AppendLine($"<td>{entry.Description}</td>");
                        htmlBody.AppendLine($"<td>{entry.IsPromo}</td>");
                        htmlBody.AppendLine("</tr>");
                    }

                    htmlBody.AppendLine("</table>");
                    htmlBody.AppendLine("<br/><br/>");

                    // Process Screenshots
                    List<Tuple<string, string, string>> attachments = new List<Tuple<string, string, string>>();
                    if (screenshotPaths != null && screenshotPaths.Any())
                    {
                        foreach (var path in screenshotPaths)
                        {
                            if (File.Exists(path))
                            {
                                string cid = "img_" + Guid.NewGuid().ToString("N") + "@puregold.com";
                                string fileName = Path.GetFileName(path);
                                attachments.Add(Tuple.Create(path, cid, fileName));

                                // Inject HTML tag with fixed width
                                htmlBody.AppendLine($"<img src=\"cid:{cid}\" width=\"500\" /><br/><br/>");
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(signatureHtml))
                    {
                        htmlBody.AppendLine(signatureHtml);
                    }

                    htmlBody.AppendLine("</body></html>");
                    emlContent.AppendLine(htmlBody.ToString());
                    emlContent.AppendLine();

                    // Embed Images as Base64 encoded parts
                    foreach (var att in attachments)
                    {
                        byte[] fileBytes = File.ReadAllBytes(att.Item1);
                        string base64 = Convert.ToBase64String(fileBytes, Base64FormattingOptions.InsertLineBreaks);
                        string mimeType = att.Item3.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";

                        emlContent.AppendLine($"--{boundary}");
                        emlContent.AppendLine($"Content-Type: {mimeType}; name=\"{att.Item3}\"");
                        emlContent.AppendLine("Content-Transfer-Encoding: base64");
                        emlContent.AppendLine($"Content-ID: <{att.Item2}>");
                        emlContent.AppendLine($"Content-Disposition: inline; filename=\"{att.Item3}\"");
                        emlContent.AppendLine();
                        emlContent.AppendLine(base64);
                    }

                    emlContent.AppendLine($"--{boundary}--");

                    string tempPath = Path.Combine(Path.GetTempPath(), $"INF_Draft_{storeCode}_{DateTime.Now:yyyyMMdd_HHmmss}.eml");
                    File.WriteAllText(tempPath, emlContent.ToString());

                    Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"INF Email Draft Error: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<System.Windows.Media.ImageSource> GenerateObPreviewAsync(ObModel data, string templatePath)
        {
            if (!File.Exists(templatePath)) return null;
            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => DrawObImage(data, templatePath));
        }

        public async Task<bool> ExportObToImageAsync(ObModel data, string templatePath, string outputPath)
        {
            try
            {
                if (!File.Exists(templatePath)) return false;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var rtb = DrawObImage(data, templatePath);

                    System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

                    using (FileStream fs = new FileStream(outputPath, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }
                });

                Process.Start(outputPath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image Export Error: {ex.Message}");
                return false;
            }
        }

        // --- SHARED OB DRAWING ENGINE USING YOUR COORDINATES ---
        private System.Windows.Media.Imaging.RenderTargetBitmap DrawObImage(ObModel data, string templatePath)
        {
            System.Windows.Media.Imaging.BitmapImage bitmapTemplate = new System.Windows.Media.Imaging.BitmapImage();
            bitmapTemplate.BeginInit();
            bitmapTemplate.UriSource = new Uri(templatePath, UriKind.Absolute);
            bitmapTemplate.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmapTemplate.EndInit();

            System.Windows.Media.DrawingVisual visual = new System.Windows.Media.DrawingVisual();
            using (System.Windows.Media.DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(bitmapTemplate, new System.Windows.Rect(0, 0, bitmapTemplate.PixelWidth, bitmapTemplate.PixelHeight));

                System.Windows.Media.Typeface regularFont = new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal);
                System.Windows.Media.Typeface boldFont = new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal);
                System.Windows.Media.Brush textBrush = System.Windows.Media.Brushes.Black;

                double fontSize = 20;

                void DrawText(string text, double x, double y, System.Windows.Media.Typeface tf, double size)
                {
                    if (string.IsNullOrEmpty(text)) return;
#pragma warning disable CS0618
                    System.Windows.Media.FormattedText ft = new System.Windows.Media.FormattedText(
                        text,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        tf,
                        size,
                        textBrush);
#pragma warning restore CS0618

                    dc.DrawText(ft, new System.Windows.Point(x, y));
                }

                // Your Adjusted coordinates for 757x521 image
                DrawText(data.EmployeeName, 55, 52, regularFont, fontSize);

                if (data.DateFiled.HasValue)
                    DrawText(data.DateFiled.Value.ToString("MMMM dd, yyyy"), 450, 54, regularFont, fontSize);

                DrawText(data.Position, 75, 80, regularFont, fontSize);
                DrawText(data.Department, 462, 80, regularFont, fontSize);
                DrawText(data.ObSchedule, 210, 118, regularFont, fontSize);

                // --- SIGNATURE RENDER LOGIC ---
                if (!string.IsNullOrEmpty(data.SignatureImagePath) && File.Exists(data.SignatureImagePath))
                {
                    System.Windows.Media.Imaging.BitmapImage sigBitmap = new System.Windows.Media.Imaging.BitmapImage();
                    sigBitmap.BeginInit();
                    sigBitmap.UriSource = new Uri(data.SignatureImagePath, UriKind.Absolute);
                    sigBitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    sigBitmap.EndInit();

                    double targetHeight = 60;
                    double targetWidth = (sigBitmap.PixelWidth / (double)sigBitmap.PixelHeight) * targetHeight;
                    double xPos = 180 - (targetWidth / 2);

                    dc.DrawImage(sigBitmap, new System.Windows.Rect(xPos, 440, targetWidth, targetHeight));
                }
                else if (!string.IsNullOrEmpty(data.EmployeeSignature))
                {
                    DrawText(data.EmployeeSignature, 120, 460, boldFont, 16);
                }
            }

            System.Windows.Media.Imaging.RenderTargetBitmap rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                bitmapTemplate.PixelWidth,
                bitmapTemplate.PixelHeight,
                96, 96,
                System.Windows.Media.PixelFormats.Pbgra32);

            rtb.Render(visual);
            rtb.Freeze(); 
            return rtb;
        }


        public async Task<System.Windows.Media.ImageSource> GenerateTsrfPreviewAsync(TsrfModel data, string templatePath)
        {
            if (!File.Exists(templatePath)) return null;

            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                return DrawTsrfImage(data, templatePath);
            });
        }

        // -------------------------------------------------------------
        // SSRF BATCH PRINTING LOGIC
        // -------------------------------------------------------------
        public async Task<bool> PrintSsrfBatchAsync(SsrfModel data, string templatePath)
        {
            if (!File.Exists(templatePath)) return false;

            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    System.Windows.Controls.PrintDialog printDialog = new System.Windows.Controls.PrintDialog();

                    if (printDialog.ShowDialog() != true) return false;

                    System.Windows.Documents.FixedDocument document = new System.Windows.Documents.FixedDocument();
                    document.DocumentPaginator.PageSize = new System.Windows.Size(816, 1056);

                    string prefix = data.BaseSsrfNumber;
                    int currentNumber = 0;
                    int padWidth = 0;
                    var match = System.Text.RegularExpressions.Regex.Match(data.BaseSsrfNumber ?? "", @"(\d+)$");
                    if (match.Success)
                    {
                        prefix = data.BaseSsrfNumber.Substring(0, match.Index);
                        currentNumber = int.Parse(match.Value);
                        padWidth = match.Value.Length;
                    }

                    for (int i = 0; i < data.PrintQuantity; i++)
                    {
                        string ssrfString = padWidth > 0 ? $"{prefix}{(currentNumber + i).ToString(new string('0', padWidth))}" : prefix;
                        var rtb = DrawSsrfPage(data, ssrfString, templatePath);

                        System.Windows.Controls.Image img = new System.Windows.Controls.Image { Source = rtb, Width = 816, Height = 1056 };
                        System.Windows.Documents.FixedPage page = new System.Windows.Documents.FixedPage { Width = 816, Height = 1056 };
                        page.Children.Add(img);

                        System.Windows.Documents.PageContent pageContent = new System.Windows.Documents.PageContent();
                        ((System.Windows.Markup.IAddChild)pageContent).AddChild(page);
                        document.Pages.Add(pageContent);
                    }

                    printDialog.PrintDocument(document.DocumentPaginator, $"SSRF Batch Print ({data.PrintQuantity} copies)");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SSRF Print Error: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<System.Windows.Media.ImageSource> GenerateSsrfPreviewAsync(SsrfModel data, string templatePath)
        {
            if (!File.Exists(templatePath)) return null;

            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                return DrawSsrfPage(data, data.BaseSsrfNumber ?? "", templatePath);
            });
        }

        private System.Windows.Media.Imaging.RenderTargetBitmap DrawSsrfPage(SsrfModel data, string ssrfNo, string templatePath)
        {
            System.Windows.Media.Imaging.BitmapImage bitmapTemplate = new System.Windows.Media.Imaging.BitmapImage();
            bitmapTemplate.BeginInit();
            bitmapTemplate.UriSource = new Uri(templatePath, UriKind.Absolute);
            bitmapTemplate.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmapTemplate.EndInit();

            System.Windows.Media.DrawingVisual visual = new System.Windows.Media.DrawingVisual();
            using (System.Windows.Media.DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(bitmapTemplate, new System.Windows.Rect(0, 0, bitmapTemplate.PixelWidth, bitmapTemplate.PixelHeight));

                System.Windows.Media.Typeface regularFont = new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal);
                System.Windows.Media.Brush textBrush = System.Windows.Media.Brushes.Black;
                double fontSize = 22;

                void DrawText(string text, double x, double y)
                {
                    if (string.IsNullOrEmpty(text)) return;
#pragma warning disable CS0618
                    System.Windows.Media.FormattedText ft = new System.Windows.Media.FormattedText(text, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, regularFont, fontSize, textBrush);
#pragma warning restore CS0618
                    dc.DrawText(ft, new System.Windows.Point(x, y));
                }

                DrawText(data.Location, 170, 105);
                DrawText(data.Department, 200, 135);

                DrawText(ssrfNo, 940, 120);
                if (data.DateFiled.HasValue) DrawText(data.DateFiled.Value.ToString("MM/dd/yyyy"), 940, 150);
            }

            System.Windows.Media.Imaging.RenderTargetBitmap rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(bitmapTemplate.PixelWidth, bitmapTemplate.PixelHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }
        public async Task<bool> ExportTsrfToImageAsync(TsrfModel data, string templatePath, string outputPath)
        {
            try
            {
                if (!File.Exists(templatePath)) return false;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var rtb = DrawTsrfImage(data, templatePath);

                    System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

                    using (FileStream fs = new FileStream(outputPath, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }
                });

                Process.Start(outputPath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TSRF Export Error: {ex.Message}");
                return false;
            }
        }

        // --- SHARED DRAWING ENGINE WITH FIXED COORDINATES ---
        private System.Windows.Media.Imaging.RenderTargetBitmap DrawTsrfImage(TsrfModel data, string templatePath)
        {
            System.Windows.Media.Imaging.BitmapImage bitmapTemplate = new System.Windows.Media.Imaging.BitmapImage();
            bitmapTemplate.BeginInit();
            bitmapTemplate.UriSource = new Uri(templatePath, UriKind.Absolute);
            bitmapTemplate.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmapTemplate.EndInit();

            System.Windows.Media.DrawingVisual visual = new System.Windows.Media.DrawingVisual();
            using (System.Windows.Media.DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(bitmapTemplate, new System.Windows.Rect(0, 0, bitmapTemplate.PixelWidth, bitmapTemplate.PixelHeight));

                // Using Times New Roman to perfectly match the TSRF style
                System.Windows.Media.Typeface regularFont = new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Times New Roman"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal);
                System.Windows.Media.Typeface boldFont = new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Times New Roman"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal);
                System.Windows.Media.Brush textBrush = System.Windows.Media.Brushes.Black;

                // Auto-Centering Helper
                void DrawText(string text, double x, double y, double size, System.Windows.Media.Typeface tf, bool center = false)
                {
                    if (string.IsNullOrEmpty(text)) return;
#pragma warning disable CS0618
                    System.Windows.Media.FormattedText ft = new System.Windows.Media.FormattedText(text, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, tf, size, textBrush);
#pragma warning restore CS0618

                    double finalX = center ? x - (ft.Width / 2) : x;
                    dc.DrawText(ft, new System.Windows.Point(finalX, y));
                }

                // Multi-line Wrapper Helper
                void DrawWrappedText(string text, double x, double y, double maxWidth, double size)
                {
                    if (string.IsNullOrEmpty(text)) return;
#pragma warning disable CS0618
                    System.Windows.Media.FormattedText ft = new System.Windows.Media.FormattedText(text, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, regularFont, size, textBrush)
                    { MaxTextWidth = maxWidth, LineHeight = size + 4 };
#pragma warning restore CS0618
                    dc.DrawText(ft, new System.Windows.Point(x, y));
                }

                double fSize = 16; // ~10pt Font
                double fSmall = 10.5; // ~8pt Font for positions

                // Fixed Headers based on Target Image
                DrawText(data.Company, 385, 42, fSize, boldFont);
                DrawText(data.Others, 520, 116, fSize, boldFont);
                DrawText(data.RequestingDept, 120, 152, fSize, boldFont);

                if (data.DateFiled.HasValue)
                    DrawText(data.DateFiled.Value.ToString("MMMM dd, yyyy"), 400, 152, fSize, boldFont, center: true);

                DrawText(data.TsrfNumber, 650, 152, fSize, boldFont, center: true);

                // Fixed Multiline Boxes (Added X offsets to match template)
                DrawWrappedText(data.AssetDescription, 20, 210, 230, fSize);
                DrawWrappedText(data.ProblemDetails, 290, 210, 230, fSize);
                DrawWrappedText(data.Remarks, 530, 210, 230, fSize);
                DrawWrappedText(data.ActionTaken, 130, 325, 600, fSize);

                // Fixed Signatures (X is the Center Point)
                double sigY_Name = 375;
                double sigY_Pos = 390;

                // Requester
                DrawText(data.RequesterName, 150, sigY_Name, fSize, boldFont, center: true);
                DrawText(data.RequesterPosition, 150, sigY_Pos, fSmall, regularFont, center: true);

                // Performed By
                DrawText(data.PerformedByName, 580, sigY_Name, fSize, boldFont, center: true);
                DrawText(data.PerformedByPosition, 580, sigY_Pos, fSmall, regularFont, center: true);

                // Noted By (Bottom Row)
                double notedY_Name = 421;
                double notedY_Pos = 435;

                DrawText(data.NotedByDeptHead1, 90, notedY_Name, fSize, boldFont, center: true);
                DrawText(data.NotedByDeptHead1Pos, 90, notedY_Pos, fSmall, regularFont, center: true);

                DrawText(data.NotedByDeptHead2, 240, notedY_Name, fSize, boldFont, center: true);
                DrawText(data.NotedByDeptHead2Pos, 240, notedY_Pos, fSmall, regularFont, center: true);

                DrawText(data.NotedByMis1, 450, notedY_Name, fSize, boldFont, center: true);
                DrawText(data.NotedByMis1Pos, 450, notedY_Pos, fSmall, regularFont, center: true);

                DrawText(data.NotedByMis2, 620, notedY_Name, fSize, boldFont, center: true);
                DrawText(data.NotedByMis2Pos, 620, notedY_Pos, fSmall, regularFont, center: true);
            }

            System.Windows.Media.Imaging.RenderTargetBitmap rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                bitmapTemplate.PixelWidth, bitmapTemplate.PixelHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);

            rtb.Render(visual);
            rtb.Freeze(); // Freezes memory so it can be passed safely to UI
            return rtb;
        }
    }
}