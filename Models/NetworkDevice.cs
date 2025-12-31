using System;

namespace WiFiHealthMonitor.Models
{
    /// <summary>
    /// Represents a device connected to the network
    /// </summary>
    public class NetworkDevice
    {
        public long Id { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public DeviceType Type { get; set; }
        public bool IsTrusted { get; set; }
        public bool IsNew { get; set; }
        public int ConnectionCount { get; set; }

        public string DisplayName => string.IsNullOrEmpty(HostName) ? MacAddress : HostName;

        public TimeSpan TimeSinceFirstSeen => DateTime.Now - FirstSeen;
    }

    public enum DeviceType
    {
        Unknown,
        Computer,
        Phone,
        Tablet,
        SmartTV,
        IoT,
        Router,
        AccessPoint
    }
}
