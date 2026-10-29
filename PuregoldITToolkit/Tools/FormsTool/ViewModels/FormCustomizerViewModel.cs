using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FormsTool.Models;
using PuregoldITToolkit.Tools.FormsTool.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PuregoldITToolkit.Tools.FormsTool.ViewModels
{
    public class FormCustomizerViewModel : ViewModelBase
    {
        private readonly FormTemplateService _templateService;

        public ObservableCollection<FormElementModel> Elements { get; } = new ObservableCollection<FormElementModel>();
        public SignatureElementModel Signature { get; } = new SignatureElementModel();

        public ObservableCollection<string> AvailableFonts { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableTemplates { get; } = new ObservableCollection<string>
        {
            "OBFormTemplate.png",
            "SSRF_Template.jpg",
            "TSRF.png"
        };

        public ObservableCollection<string> AvailableSamplingModes { get; } = new ObservableCollection<string>
        {
            "Best",
            "Smooth"
        };

        private string _selectedTemplate = "OBFormTemplate.png";
        public string SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value))
                {
                    LoadTemplateBackground();
                }
            }
        }

        private ImageSource _templateBackgroundImage;
        public ImageSource TemplateBackgroundImage
        {
            get => _templateBackgroundImage;
            set => SetProperty(ref _templateBackgroundImage, value);
        }

        private BitmapSource _processedSignatureSource;
        public BitmapSource ProcessedSignatureSource
        {
            get => _processedSignatureSource;
            set => SetProperty(ref _processedSignatureSource, value);
        }

        private FormElementModel _selectedElement;
        public FormElementModel SelectedElement
        {
            get => _selectedElement;
            set
            {
                if (_selectedElement != null) _selectedElement.IsSelected = false;
                if (SetProperty(ref _selectedElement, value))
                {
                    if (_selectedElement != null)
                    {
                        _selectedElement.IsSelected = true;
                        IsSignatureSelected = false;
                    }
                    OnPropertyChanged(nameof(HasSelectedElement));
                }
            }
        }

        private bool _isSignatureSelected;
        public bool IsSignatureSelected
        {
            get => _isSignatureSelected;
            set
            {
                if (SetProperty(ref _isSignatureSelected, value) && value)
                {
                    if (SelectedElement != null) SelectedElement = null;
                }
                Signature.IsSelected = value;
            }
        }

        public bool HasSelectedElement => SelectedElement != null;

        private string _statusMessage = "Ready. Drag elements or customize properties.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Selected Font Style Helper
        public bool IsSelectedFontRegular
        {
            get => SelectedElement != null && !SelectedElement.IsBold && !SelectedElement.IsItalic;
            set
            {
                if (value && SelectedElement != null)
                {
                    SelectedElement.IsBold = false;
                    SelectedElement.IsItalic = false;
                    OnPropertyChanged(nameof(IsSelectedFontRegular));
                    OnPropertyChanged(nameof(IsSelectedFontBold));
                    OnPropertyChanged(nameof(IsSelectedFontItalic));
                }
            }
        }

        public bool IsSelectedFontBold
        {
            get => SelectedElement != null && SelectedElement.IsBold;
            set
            {
                if (SelectedElement != null)
                {
                    SelectedElement.IsBold = value;
                    OnPropertyChanged(nameof(IsSelectedFontRegular));
                    OnPropertyChanged(nameof(IsSelectedFontBold));
                }
            }
        }

        public bool IsSelectedFontItalic
        {
            get => SelectedElement != null && SelectedElement.IsItalic;
            set
            {
                if (SelectedElement != null)
                {
                    SelectedElement.IsItalic = value;
                    OnPropertyChanged(nameof(IsSelectedFontRegular));
                    OnPropertyChanged(nameof(IsSelectedFontItalic));
                }
            }
        }

        public ICommand SelectElementCommand { get; }
        public ICommand SelectSignatureCommand { get; }
        public ICommand UploadSignatureCommand { get; }
        public ICommand ProcessSignatureCommand { get; }
        public ICommand SaveTemplateCommand { get; }
        public ICommand LoadTemplateCommand { get; }
        public ICommand ResetDefaultsCommand { get; }
        public ICommand ExportImageCommand { get; }

        public FormCustomizerViewModel(FormTemplateService templateService)
        {
            _templateService = templateService ?? new FormTemplateService();

            // Populate system fonts
            try
            {
                foreach (var font in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
                {
                    AvailableFonts.Add(font.Source);
                }
            }
            catch
            {
                AvailableFonts.Add("Arial");
                AvailableFonts.Add("Segoe UI");
                AvailableFonts.Add("Consolas");
                AvailableFonts.Add("Times New Roman");
                AvailableFonts.Add("Calibri");
            }

            SelectElementCommand = new RelayCommand<FormElementModel>(el => SelectedElement = el);
            SelectSignatureCommand = new RelayCommand(() => IsSignatureSelected = true);
            UploadSignatureCommand = new RelayCommand(UploadSignature);
            ProcessSignatureCommand = new RelayCommand(RefreshProcessedSignature);
            SaveTemplateCommand = new RelayCommand(SaveTemplatePreset);
            LoadTemplateCommand = new RelayCommand(LoadTemplatePreset);
            ResetDefaultsCommand = new RelayCommand(ResetToDefaults);
            ExportImageCommand = new AsyncRelayCommand(ExportFormAsync);

            // Load initial default or saved template
            LoadTemplatePreset();
            LoadTemplateBackground();
        }

        private void LoadTemplateBackground()
        {
            try
            {
                TemplateBackgroundImage = _templateService.LoadEmbeddedTemplate(SelectedTemplate);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Warning loading template: {ex.Message}";
            }
        }

        public void RefreshProcessedSignature()
        {
            if (string.IsNullOrEmpty(Signature.ImagePath) || !File.Exists(Signature.ImagePath))
            {
                ProcessedSignatureSource = null;
                return;
            }

            ProcessedSignatureSource = _templateService.ProcessSignature(
                Signature.ImagePath,
                Signature.EnableThreshold,
                Signature.ThresholdValue,
                Signature.ContrastBoost);

            StatusMessage = "Signature image updated.";
        }

        private void UploadSignature()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Transparent Signature (PNG / JPG / BMP)",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                Signature.ImagePath = dlg.FileName;
                RefreshProcessedSignature();
                IsSignatureSelected = true;
                StatusMessage = $"Signature loaded: {Path.GetFileName(dlg.FileName)}";
            }
        }

        public void SaveTemplatePreset()
        {
            var preset = new FormTemplatePreset
            {
                TemplateName = "Customized Form Template",
                BackgroundTemplate = SelectedTemplate,
                Elements = Elements.ToList(),
                Signature = Signature
            };

            bool success = _templateService.SaveTemplate(preset);
            if (success)
            {
                StatusMessage = $"Preset saved successfully to {Path.GetFileName(FormTemplateService.DefaultTemplatePath)}";
            }
            else
            {
                StatusMessage = "Error saving template preset.";
            }
        }

        public void LoadTemplatePreset()
        {
            var preset = _templateService.LoadTemplate();
            ApplyPreset(preset);
            StatusMessage = "Template preset loaded.";
        }

        public void ResetToDefaults()
        {
            var preset = _templateService.CreateDefaultPreset();
            ApplyPreset(preset);
            StatusMessage = "Form layout reset to defaults.";
        }

        private void ApplyPreset(FormTemplatePreset preset)
        {
            if (preset == null) return;

            Elements.Clear();
            foreach (var el in preset.Elements)
            {
                Elements.Add(el);
            }

            if (preset.Signature != null)
            {
                Signature.X = preset.Signature.X;
                Signature.Y = preset.Signature.Y;
                Signature.Width = preset.Signature.Width;
                Signature.Height = preset.Signature.Height;
                Signature.Scale = preset.Signature.Scale;
                Signature.SamplingMode = preset.Signature.SamplingMode == "Bilinear" || preset.Signature.SamplingMode == "Smooth" ? "Smooth" : "Best";
                Signature.EnableThreshold = preset.Signature.EnableThreshold;
                Signature.ThresholdValue = preset.Signature.ThresholdValue;
                Signature.ContrastBoost = preset.Signature.ContrastBoost;
                Signature.ImagePath = preset.Signature.ImagePath;
            }

            if (!string.IsNullOrEmpty(preset.BackgroundTemplate) && AvailableTemplates.Contains(preset.BackgroundTemplate))
            {
                SelectedTemplate = preset.BackgroundTemplate;
            }

            RefreshProcessedSignature();
            SelectedElement = Elements.FirstOrDefault();
        }

        private async Task ExportFormAsync()
        {
            try
            {
                StatusMessage = "Rendering form layout...";
                var preset = new FormTemplatePreset
                {
                    TemplateName = "Customized Form",
                    BackgroundTemplate = SelectedTemplate,
                    Elements = Elements.ToList(),
                    Signature = Signature
                };

                await Task.Run(() =>
                {
                    var rtb = _templateService.RenderTemplateToBitmap(preset, ProcessedSignatureSource);
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string outDir = Path.Combine(desktop, "Puregold_Forms_Output");
                    string fileName = $"Form_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string fullPath = Path.Combine(outDir, fileName);

                    _templateService.ExportToPng(rtb, fullPath);
                });

                StatusMessage = "Export complete! Image opened successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export error: {ex.Message}";
            }
        }

        // Live coordinate update helpers from Canvas dragging
        public void UpdateElementPosition(FormElementModel element, double newX, double newY)
        {
            if (element != null)
            {
                element.X = Math.Max(0, Math.Round(newX, 1));
                element.Y = Math.Max(0, Math.Round(newY, 1));
            }
        }

        public void UpdateSignaturePosition(double newX, double newY)
        {
            Signature.X = Math.Max(0, Math.Round(newX, 1));
            Signature.Y = Math.Max(0, Math.Round(newY, 1));
        }
    }
}

