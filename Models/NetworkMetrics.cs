using System;

namespace WiFiHealthMonitor.Models
{
    /// <summary>
    /// Represents network metrics collected at a specific point in time
    /// </summary>
    public class NetworkMetrics
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Ssid { get; set; } = string.Empty;
        public string Bssid { get; set; } = string.Empty;
        public int SignalPercent { get; set; }
        public int Rssi { get; set; }
        public string Band { get; set; } = string.Empty;
        public int Channel { get; set; }
        public double ReceiveSpeedMbps { get; set; }
        public double TransmitSpeedMbps { get; set; }
        public int ConnectedDevices { get; set; }
        public int ChannelUtilization { get; set; }
        public string RadioType { get; set; } = string.Empty;
        public string Authentication { get; set; } = string.Empty;

        /// <summary>
        /// Calculate overall health score (0-100)
        /// </summary>
        public int HealthScore
        {
            get
            {
                int score = 0;

                // Signal quality (40 points)
                if (SignalPercent >= 80) score += 40;
                else if (SignalPercent >= 60) score += 30;
                else if (SignalPercent >= 40) score += 20;
                else score += 10;

                // Speed (30 points)
                double avgSpeed = (ReceiveSpeedMbps + TransmitSpeedMbps) / 2;
                if (avgSpeed >= 100) score += 30;
                else if (avgSpeed >= 50) score += 20;
                else if (avgSpeed >= 25) score += 15;
                else score += 5;

                // Channel utilization (20 points)
                if (ChannelUtilization < 20) score += 20;
                else if (ChannelUtilization < 40) score += 15;
                else if (ChannelUtilization < 60) score += 10;
                else score += 5;

                // Security (10 points)
                if (Authentication.Contains("WPA3")) score += 10;
                else if (Authentication.Contains("WPA2")) score += 8;
                else if (Authentication.Contains("WPA")) score += 5;

                return Math.Min(score, 100);
            }
        }

        public string HealthStatus
        {
            get
            {
                int score = HealthScore;
                if (score >= 80) return "Excellent";
                if (score >= 60) return "Good";
                if (score >= 40) return "Fair";
                return "Poor";
            }
        }
    }
}
