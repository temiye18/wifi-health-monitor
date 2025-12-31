using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Services
{
    /// <summary>
    /// Engine for analyzing network metrics and generating intelligent alerts and recommendations
    /// </summary>
    public class AlertEngine
    {
        private readonly WiFiMonitorService _wifiMonitor;

        public AlertEngine(WiFiMonitorService wifiMonitor)
        {
            _wifiMonitor = wifiMonitor;
        }

        /// <summary>
        /// Analyzes current metrics and historical data to generate alerts
        /// </summary>
        public async Task<List<Alert>> AnalyzeMetricsAsync(NetworkMetrics current, List<NetworkMetrics> history)
        {
            var alerts = new List<Alert>();

            // Check signal strength
            alerts.AddRange(CheckSignalStrength(current));

            // Check for better band availability
            var bandAlert = await CheckBandRecommendationAsync(current);
            if (bandAlert != null)
                alerts.Add(bandAlert);

            // Check speed degradation
            if (history.Any())
                alerts.AddRange(CheckSpeedDegradation(current, history));

            // Check channel congestion
            alerts.AddRange(CheckChannelCongestion(current));

            // Check security
            alerts.AddRange(CheckSecurity(current));

            return alerts;
        }

        /// <summary>
        /// Checks signal strength and generates alerts if weak
        /// </summary>
        private List<Alert> CheckSignalStrength(NetworkMetrics metrics)
        {
            var alerts = new List<Alert>();

            if (metrics.SignalPercent < 40)
            {
                alerts.Add(new Alert(
                    "Weak Signal",
                    $"Your WiFi signal is weak ({metrics.SignalPercent}%). Consider moving closer to the router or reducing obstacles between your device and the router.",
                    AlertType.SignalIssue,
                    AlertSeverity.High
                ));
            }
            else if (metrics.SignalPercent < 60)
            {
                alerts.Add(new Alert(
                    "Fair Signal",
                    $"Your WiFi signal is fair ({metrics.SignalPercent}%). You may experience occasional slowdowns. Consider moving closer to the router for better performance.",
                    AlertType.SignalIssue,
                    AlertSeverity.Medium
                ));
            }

            return alerts;
        }

        /// <summary>
        /// Checks if a better band (5GHz) is available
        /// </summary>
        private async Task<Alert?> CheckBandRecommendationAsync(NetworkMetrics metrics)
        {
            if (metrics.Band == "2.4 GHz")
            {
                var is5GHzAvailable = await _wifiMonitor.Is5GHzAvailableAsync(metrics.Ssid);
                if (is5GHzAvailable)
                {
                    return new Alert(
                        "Switch to 5GHz",
                        $"A 5GHz network is available for {metrics.Ssid}. Switching to 5GHz can provide 3-5x faster speeds with less interference.",
                        AlertType.Recommendation,
                        AlertSeverity.Info
                    );
                }
            }

            return null;
        }

        /// <summary>
        /// Checks for speed degradation compared to historical averages
        /// </summary>
        private List<Alert> CheckSpeedDegradation(NetworkMetrics current, List<NetworkMetrics> history)
        {
            var alerts = new List<Alert>();

            if (history.Count < 10)
                return alerts;

            var recentHistory = history.Take(20).ToList();
            var avgReceiveSpeed = recentHistory.Average(m => m.ReceiveSpeedMbps);
            var avgTransmitSpeed = recentHistory.Average(m => m.TransmitSpeedMbps);

            // Check if current speed is significantly lower than average
            if (current.ReceiveSpeedMbps < avgReceiveSpeed * 0.5)
            {
                alerts.Add(new Alert(
                    "Slow Download Speed",
                    $"Your download speed ({current.ReceiveSpeedMbps:F1} Mbps) is significantly lower than average ({avgReceiveSpeed:F1} Mbps). This could be due to network congestion or ISP issues.",
                    AlertType.SpeedIssue,
                    AlertSeverity.Medium
                ));
            }

            if (current.TransmitSpeedMbps < avgTransmitSpeed * 0.5)
            {
                alerts.Add(new Alert(
                    "Slow Upload Speed",
                    $"Your upload speed ({current.TransmitSpeedMbps:F1} Mbps) is significantly lower than average ({avgTransmitSpeed:F1} Mbps). This could be due to network congestion or ISP issues.",
                    AlertType.SpeedIssue,
                    AlertSeverity.Medium
                ));
            }

            return alerts;
        }

        /// <summary>
        /// Checks for channel congestion
        /// </summary>
        private List<Alert> CheckChannelCongestion(NetworkMetrics metrics)
        {
            var alerts = new List<Alert>();

            if (metrics.ChannelUtilization > 70)
            {
                alerts.Add(new Alert(
                    "Channel Congestion",
                    $"Your WiFi channel is heavily congested ({metrics.ChannelUtilization}% utilization). Consider changing to a less congested channel in your router settings.",
                    AlertType.Recommendation,
                    AlertSeverity.Medium
                ));
            }

            return alerts;
        }

        /// <summary>
        /// Checks security settings
        /// </summary>
        private List<Alert> CheckSecurity(NetworkMetrics metrics)
        {
            var alerts = new List<Alert>();

            if (!metrics.Authentication.Contains("WPA2") && !metrics.Authentication.Contains("WPA3"))
            {
                alerts.Add(new Alert(
                    "Weak Security",
                    $"Your network is using {metrics.Authentication} authentication. Consider upgrading to WPA2 or WPA3 for better security.",
                    AlertType.SecurityIssue,
                    AlertSeverity.High
                ));
            }
            else if (!metrics.Authentication.Contains("WPA3") && metrics.Authentication.Contains("WPA2"))
            {
                alerts.Add(new Alert(
                    "Security Recommendation",
                    "Your network uses WPA2. If your router supports it, consider upgrading to WPA3 for enhanced security.",
                    AlertType.SecurityIssue,
                    AlertSeverity.Low
                ));
            }

            return alerts;
        }

        /// <summary>
        /// Generates a summary of network health
        /// </summary>
        public string GenerateHealthSummary(NetworkMetrics metrics)
        {
            var score = metrics.HealthScore;
            var status = metrics.HealthStatus;

            var summary = $"Network Health: {status} ({score}/100)\n\n";

            summary += $"Signal: {metrics.SignalPercent}% ({metrics.Rssi} dBm)\n";
            summary += $"Speed: ↓ {metrics.ReceiveSpeedMbps:F1} Mbps / ↑ {metrics.TransmitSpeedMbps:F1} Mbps\n";
            summary += $"Band: {metrics.Band} (Channel {metrics.Channel})\n";
            summary += $"Security: {metrics.Authentication}\n";

            if (metrics.ChannelUtilization > 0)
            {
                summary += $"Channel Usage: {metrics.ChannelUtilization}%\n";
            }

            return summary;
        }
    }
}
