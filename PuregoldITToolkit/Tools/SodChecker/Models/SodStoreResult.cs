using PuregoldITToolkit.Core.Base;
using System;

namespace PuregoldITToolkit.Tools.SodChecker.Models
{
    public class SodStoreResult : ViewModelBase
    {
        // UI Display Properties (Adapts based on the Mode)
        public string DisplayId { get; set; }   // Shows Store Code OR Date String
        public string DisplayName { get; set; } // Shows Store Name OR Day of Week

        // Background Scanning Properties
        public DateTime TargetDate { get; set; }
        public string TargetStoreCode { get; set; }

        private FileCheckStatus _ejStatus = new FileCheckStatus();
        private FileCheckStatus _crmStatus = new FileCheckStatus();
        private FileCheckStatus _promoStatus = new FileCheckStatus();
        private FileCheckStatus _birStatus = new FileCheckStatus();
        private FileCheckStatus _nonTradeStatus = new FileCheckStatus();
        private FileCheckStatus _pollogStatus = new FileCheckStatus();
        private FileCheckStatus _dosStatus = new FileCheckStatus();
        private FileCheckStatus _mobilePriceStatus = new FileCheckStatus();
        private FileCheckStatus _plusKuPriceStatus = new FileCheckStatus();
        private FileCheckStatus _regPriceStatus = new FileCheckStatus();
        private FileCheckStatus _sis98Status = new FileCheckStatus();
        private FileCheckStatus _shelftagStatus = new FileCheckStatus();
        private FileCheckStatus _kioskStatus = new FileCheckStatus();

        public FileCheckStatus EjStatus { get => _ejStatus; set => SetProperty(ref _ejStatus, value); }
        public FileCheckStatus CrmStatus { get => _crmStatus; set => SetProperty(ref _crmStatus, value); }
        public FileCheckStatus PromoStatus { get => _promoStatus; set => SetProperty(ref _promoStatus, value); }
        public FileCheckStatus BirStatus { get => _birStatus; set => SetProperty(ref _birStatus, value); }
        public FileCheckStatus NonTradeStatus { get => _nonTradeStatus; set => SetProperty(ref _nonTradeStatus, value); }
        public FileCheckStatus PollogStatus { get => _pollogStatus; set => SetProperty(ref _pollogStatus, value); }
        public FileCheckStatus DosStatus { get => _dosStatus; set => SetProperty(ref _dosStatus, value); }
        public FileCheckStatus MobilePriceStatus { get => _mobilePriceStatus; set => SetProperty(ref _mobilePriceStatus, value); }
        public FileCheckStatus PlusKuPriceStatus { get => _plusKuPriceStatus; set => SetProperty(ref _plusKuPriceStatus, value); }
        public FileCheckStatus RegPriceStatus { get => _regPriceStatus; set => SetProperty(ref _regPriceStatus, value); }
        public FileCheckStatus Sis98Status { get => _sis98Status; set => SetProperty(ref _sis98Status, value); }
        public FileCheckStatus ShelftagStatus { get => _shelftagStatus; set => SetProperty(ref _shelftagStatus, value); }
        public FileCheckStatus KioskStatus { get => _kioskStatus; set => SetProperty(ref _kioskStatus, value); }
    }
}