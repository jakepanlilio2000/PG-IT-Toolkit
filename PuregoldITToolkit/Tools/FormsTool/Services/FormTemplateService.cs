using PuregoldITToolkit.Tools.FormsTool.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PuregoldITToolkit.Tools.FormsTool.Services
{
    public class FormTemplateService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string DefaultTemplatePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "form_template.json");

        public bool SaveTemplate(FormTemplatePreset preset, string filePath = null)
        {
            try
            {
                string targetPath = string.IsNullOrWhiteSpace(filePath) ? DefaultTemplatePath : filePath;
                string dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(preset, JsonOptions);
                File.WriteAllText(targetPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save form template: {ex.Message}");
                return false;
            }
        }

        public FormTemplatePreset LoadTemplate(string filePath = null)
        {
            try
            {
                string targetPath = string.IsNullOrWhiteSpace(filePath) ? DefaultTemplatePath : filePath;
                if (!File.Exists(targetPath))
                {
                    return CreateDefaultPreset();
                }

                string json = File.ReadAllText(targetPath);
                var preset = JsonSerializer.Deserialize<FormTemplatePreset>(json, JsonOptions);
                return preset ?? CreateDefaultPreset();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load form template: {ex.Message}");
                return CreateDefaultPreset();
            }
        }

        public FormTemplatePreset CreateDefaultPreset()
        {
            return new FormTemplatePreset
            {
                TemplateName = "Official Business (OB) Form",
                BackgroundTemplate = "OBFormTemplate.png",
                Elements = new List<FormElementModel>
                {
                    new FormElementModel { Id = "PreparedBy", Name = "Prepared By", Text = "Juan Dela Cruz", X = 55, Y = 52, FontFamily = "Arial", FontSize = 16, IsBold = false },
                    new FormElementModel { Id = "Date", Name = "Date", Text = DateTime.Now.ToString("MMMM dd, yyyy"), X = 450, Y = 54, FontFamily = "Arial", FontSize = 16, IsBold = false },
                    new FormElementModel { Id = "Position", Name = "Position", Text = "IT Store Support Specialist", X = 75, Y = 80, FontFamily = "Arial", FontSize = 15, IsBold = false },
                    new FormElementModel { Id = "Department", Name = "Department / Store", Text = "Store 722 - San Fernando", X = 462, Y = 80, FontFamily = "Arial", FontSize = 15, IsBold = false },
                    new FormElementModel { Id = "DocNumber", Name = "Document Number", Text = "OB-2026-722-001", X = 210, Y = 118, FontFamily = "Arial", FontSize = 15, IsBold = true },
                    new FormElementModel { Id = "ApprovedBy", Name = "Approved By", Text = "Store Operations Manager", X = 120, Y = 460, FontFamily = "Arial", FontSize = 14, IsBold = true }
                },
                Signature = new SignatureElementModel
                {
                    X = 120,
                    Y = 410,
                    Width = 180,
                    Height = 65,
                    Scale = 1.0,
                    SamplingMode = "Best",
                    EnableThreshold = true,
                    ThresholdValue = 220,
                    ContrastBoost = 30
                }
            };
        }

        public BitmapSource ProcessSignature(string imagePath, bool enableThreshold, int thresholdValue, int contrastBoost)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return null;

            try
            {
                var uri = new Uri(imagePath, UriKind.Absolute);
                BitmapImage source = new BitmapImage();
                source.BeginInit();
                source.UriSource = uri;
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.EndInit();
                source.Freeze();

                FormatConvertedBitmap converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = source;
                converted.DestinationFormat = PixelFormats.Bgra32;
                converted.EndInit();
                converted.Freeze();

                int width = converted.PixelWidth;
                int height = converted.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                converted.CopyPixels(pixels, stride, 0);

                if (enableThreshold)
                {
                    double boostFactor = 1.0 + (contrastBoost / 100.0);

                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        byte b = pixels[i];
                        byte g = pixels[i + 1];
                        byte r = pixels[i + 2];
                        byte a = pixels[i + 3];

                        if (a == 0) continue;

                        // Perceived luminance
                        double luminance = (0.299 * r) + (0.587 * g) + (0.114 * b);

                        if (luminance >= thresholdValue)
                        {
                            // Faint white/gray paper background becomes completely transparent
                            pixels[i + 3] = 0;
                        }
                        else
                        {
                            // Smooth gradient edge transition near threshold
                            if (thresholdValue - luminance < 20)
                            {
                                double edgeAlpha = ((thresholdValue - luminance) / 20.0) * a;
                                pixels[i + 3] = (byte)Math.Max(0, Math.Min(255, edgeAlpha));
                            }

                            // Darken strokes / boost ink contrast
                            if (contrastBoost > 0)
                            {
                                pixels[i] = (byte)Math.Max(0, b - (contrastBoost * 1.5));     // Blue
                                pixels[i + 1] = (byte)Math.Max(0, g - (contrastBoost * 1.5)); // Green
                                pixels[i + 2] = (byte)Math.Max(0, r - (contrastBoost * 1.5)); // Red
                            }
                        }
                    }
                }

                WriteableBitmap resultBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                resultBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
                resultBitmap.Freeze();
                return resultBitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing signature image: {ex.Message}");
                return null;
            }
        }

        public BitmapImage LoadEmbeddedTemplate(string templateName)
        {
            string resourceName = $"PuregoldITToolkit.Assets.{templateName}";
            var assembly = Assembly.GetExecutingAssembly();

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Embedded Resource '{resourceName}' not found in Assets folder.");

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        public RenderTargetBitmap RenderTemplateToBitmap(FormTemplatePreset preset, BitmapSource processedSignature)
        {
            BitmapImage bgTemplate = LoadEmbeddedTemplate(preset.BackgroundTemplate);
            int width = bgTemplate.PixelWidth;
            int height = bgTemplate.PixelHeight;

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                // Draw form template background
                dc.DrawImage(bgTemplate, new Rect(0, 0, width, height));

                // Draw text elements
                Brush textBrush = Brushes.Black;
                foreach (var el in preset.Elements)
                {
                    if (string.IsNullOrEmpty(el.Text)) continue;

                    FontStyle fontStyle = el.IsItalic ? FontStyles.Italic : FontStyles.Normal;
                    FontWeight fontWeight = el.IsBold ? FontWeights.Bold : FontWeights.Normal;
                    Typeface tf = new Typeface(new FontFamily(el.FontFamily ?? "Arial"), fontStyle, fontWeight, FontStretches.Normal);

#pragma warning disable CS0618
                    FormattedText ft = new FormattedText(
                        el.Text,
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        tf,
                        el.FontSize > 0 ? el.FontSize : 14,
                        textBrush);
#pragma warning restore CS0618

                    dc.DrawText(ft, new Point(el.X, el.Y));
                }

                // Draw signature overlay
                if (processedSignature != null && preset.Signature != null)
                {
                    double sigWidth = preset.Signature.Width * preset.Signature.Scale;
                    double sigHeight = preset.Signature.Height * preset.Signature.Scale;
                    Rect sigRect = new Rect(preset.Signature.X, preset.Signature.Y, sigWidth, sigHeight);

                    dc.DrawImage(processedSignature, sigRect);
                }
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        public bool ExportToPng(RenderTargetBitmap rtb, string outputPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (FileStream fs = new FileStream(outputPath, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                Process.Start(new ProcessStartInfo { FileName = outputPath, UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to export PNG: {ex.Message}");
                return false;
            }
        }
    }
}

