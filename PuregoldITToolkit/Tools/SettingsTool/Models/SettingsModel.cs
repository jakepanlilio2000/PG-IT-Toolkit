using PuregoldITToolkit.Core.Base;

namespace PuregoldITToolkit.Tools.SettingsTool.Models
{
    public class SettingsModel : ViewModelBase
    {
        private string _signatureHtml;
        public string SignatureHtml
        {
            get => _signatureHtml;
            set => SetProperty(ref _signatureHtml, value);
        }
    }
}