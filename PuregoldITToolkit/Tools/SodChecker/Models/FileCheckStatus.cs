using PuregoldITToolkit.Core.Base;
using System;

namespace PuregoldITToolkit.Tools.SodChecker.Models
{
    public enum ScanState { Missing, Ok, Empty, Rerun, Skipped } 

    public class FileCheckStatus : ViewModelBase
    {
        private ScanState _state = ScanState.Missing;
        private string _fileName = string.Empty;
        private long _sizeBytes = 0;

        public ScanState State { get => _state; set => SetProperty(ref _state, value); }
        public string FileName { get => _fileName; set => SetProperty(ref _fileName, value); }
        public long SizeBytes
        {
            get => _sizeBytes;
            set { if (SetProperty(ref _sizeBytes, value)) OnPropertyChanged(nameof(FormattedSize)); }
        }

        public string FormattedSize
        {
            get
            {
                if (SizeBytes > 1048576) return Math.Round(SizeBytes / 1048576.0, 2) + " MB";
                if (SizeBytes > 1024) return Math.Round(SizeBytes / 1024.0, 2) + " KB";
                return SizeBytes > 0 ? SizeBytes + " B" : "";
            }
        }
    }
}