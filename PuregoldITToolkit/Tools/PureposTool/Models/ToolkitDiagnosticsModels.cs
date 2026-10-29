using PuregoldITToolkit.Core.Base;

namespace PuregoldITToolkit.Tools.PureposTool.Models
{
    public class PingTargetResult : ViewModelBase
    {
        private string _targetName;
        private string _ipAddress;
        private string _status = "Pending";
        private long _latencyMs;
        private string _statusColor = "#94A3B8";

        public string TargetName
        {
            get => _targetName;
            set => SetProperty(ref _targetName, value);
        }

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public long LatencyMs
        {
            get => _latencyMs;
            set => SetProperty(ref _latencyMs, value);
        }

        public string StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }
    }

    public class PortCheckResult : ViewModelBase
    {
        private int _port;
        private string _serviceName;
        private string _status = "Pending";
        private long _latencyMs;
        private string _statusColor = "#94A3B8";

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public string ServiceName
        {
            get => _serviceName;
            set => SetProperty(ref _serviceName, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public long LatencyMs
        {
            get => _latencyMs;
            set => SetProperty(ref _latencyMs, value);
        }

        public string StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }
    }

    public class NetworkAdapterDetail : ViewModelBase
    {
        private string _name;
        private string _description;
        private string _ipAddress = "N/A";
        private string _subnetMask = "N/A";
        private string _gateway = "N/A";
        private string _dnsServers = "N/A";
        private string _macAddress = "N/A";
        private string _speed = "N/A";
        private string _operationalStatus = "Down";

        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public string Description { get => _description; set => SetProperty(ref _description, value); }
        public string IpAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }
        public string SubnetMask { get => _subnetMask; set => SetProperty(ref _subnetMask, value); }
        public string Gateway { get => _gateway; set => SetProperty(ref _gateway, value); }
        public string DnsServers { get => _dnsServers; set => SetProperty(ref _dnsServers, value); }
        public string MacAddress { get => _macAddress; set => SetProperty(ref _macAddress, value); }
        public string Speed { get => _speed; set => SetProperty(ref _speed, value); }
        public string OperationalStatus { get => _operationalStatus; set => SetProperty(ref _operationalStatus, value); }
    }

    public class CacheDirectoryInfo : ViewModelBase
    {
        private string _directoryName;
        private string _path;
        private int _fileCount;
        private long _totalBytes;
        private string _formattedSize = "0 KB";

        public string DirectoryName { get => _directoryName; set => SetProperty(ref _directoryName, value); }
        public string Path { get => _path; set => SetProperty(ref _path, value); }
        public int FileCount { get => _fileCount; set => SetProperty(ref _fileCount, value); }
        public long TotalBytes { get => _totalBytes; set => SetProperty(ref _totalBytes, value); }
        public string FormattedSize { get => _formattedSize; set => SetProperty(ref _formattedSize, value); }
    }
}

