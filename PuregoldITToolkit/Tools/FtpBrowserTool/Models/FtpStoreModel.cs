namespace PuregoldITToolkit.Tools.FtpBrowserTool.Models
{
    public class FtpStoreModel
    {
        public string StoreCode { get; set; }
        public string StoreName { get; set; }
        public string OriginalFolderName { get; set; }
        public string DisplayText => $"{StoreCode} - {StoreName}";
    }
}