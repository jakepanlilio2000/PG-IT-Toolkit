using PuregoldITToolkit.Core.Base;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PuregoldITToolkit.Tools.FormsTool.Models
{
    public class FormElementModel : ViewModelBase
    {
        private string _id;
        private string _name;
        private string _text;
        private double _x;
        private double _y;
        private string _fontFamily = "Segoe UI";
        private double _fontSize = 14;
        private bool _isBold;
        private bool _isItalic;
        private bool _isSelected;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public string FontFamily
        {
            get => _fontFamily;
            set => SetProperty(ref _fontFamily, value);
        }

        public double FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public bool IsBold
        {
            get => _isBold;
            set => SetProperty(ref _isBold, value);
        }

        public bool IsItalic
        {
            get => _isItalic;
            set => SetProperty(ref _isItalic, value);
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public class SignatureElementModel : ViewModelBase
    {
        private string _imagePath;
        private double _x = 100;
        private double _y = 350;
        private double _width = 180;
        private double _height = 70;
        private double _scale = 1.0;
        private string _samplingMode = "Best";
        private bool _enableThreshold = true;
        private int _thresholdValue = 220; // 0 to 255: pixels brighter than this become transparent
        private int _contrastBoost = 25; // 0 to 100: darkens ink strokes
        private bool _isSelected;

        public string ImagePath
        {
            get => _imagePath;
            set => SetProperty(ref _imagePath, value);
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        public string SamplingMode
        {
            get => _samplingMode;
            set => SetProperty(ref _samplingMode, value);
        }

        public bool EnableThreshold
        {
            get => _enableThreshold;
            set => SetProperty(ref _enableThreshold, value);
        }

        public int ThresholdValue
        {
            get => _thresholdValue;
            set => SetProperty(ref _thresholdValue, value);
        }

        public int ContrastBoost
        {
            get => _contrastBoost;
            set => SetProperty(ref _contrastBoost, value);
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public class FormTemplatePreset
    {
        public string TemplateName { get; set; } = "Default Form Template";
        public string BackgroundTemplate { get; set; } = "OBFormTemplate.png";
        public List<FormElementModel> Elements { get; set; } = new List<FormElementModel>();
        public SignatureElementModel Signature { get; set; } = new SignatureElementModel();
    }
}

