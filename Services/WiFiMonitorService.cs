using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Services
{
    /// <summary>
    /// Service for monitoring WiFi connection and collecting network metrics
    /// </summary>
    public class WiFiMonitorService
    {
        /// <summary>
        /// Collects current network metrics from the WiFi adapter
        /// </summary>
        public async Task<NetworkMetrics?> CollectMetricsAsync()
        {
            try
            {
                var output = await RunCommandAsync("netsh wlan show interfaces");
                if (string.IsNullOrEmpty(output))
                    return null;

                return ParseWlanInterfaces(output);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error collecting metrics: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Scans for available WiFi networks
        /// </summary>
        public async Task<string> ScanNetworksAsync()
        {
            try
            {
                return await RunCommandAsync("netsh wlan show networks mode=bssid");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning networks: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Runs a Windows command and returns the output
        /// </summary>
        private async Task<string> RunCommandAsync(string command)
        {
            try
            {
                var processInfo = new ProcessStartInfo("cmd.exe", $"/c {command}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                    return string.Empty;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running command '{command}': {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Parses the output of 'netsh wlan show interfaces' command
        /// </summary>
        private NetworkMetrics ParseWlanInterfaces(string output)
        {
            var metrics = new NetworkMetrics
            {
                Timestamp = DateTime.Now
            };

            // Extract SSID
            var ssidMatch = Regex.Match(output, @"SSID\s+:\s+(.+)");
            if (ssidMatch.Success)
                metrics.Ssid = ssidMatch.Groups[1].Value.Trim();

            // Extract BSSID
            var bssidMatch = Regex.Match(output, @"BSSID\s+:\s+(.+)");
            if (bssidMatch.Success)
                metrics.Bssid = bssidMatch.Groups[1].Value.Trim();

            // Extract Signal
            var signalMatch = Regex.Match(output, @"Signal\s+:\s+(\d+)%");
            if (signalMatch.Success)
                metrics.SignalPercent = int.Parse(signalMatch.Groups[1].Value);

            // Extract RSSI
            var rssiMatch = Regex.Match(output, @"Rssi\s+:\s+(-?\d+)");
            if (rssiMatch.Success)
                metrics.Rssi = int.Parse(rssiMatch.Groups[1].Value);

            // Extract Band
            var bandMatch = Regex.Match(output, @"Band\s+:\s+(.+)");
            if (bandMatch.Success)
                metrics.Band = bandMatch.Groups[1].Value.Trim();

            // Extract Channel
            var channelMatch = Regex.Match(output, @"Channel\s+:\s+(\d+)");
            if (channelMatch.Success)
                metrics.Channel = int.Parse(channelMatch.Groups[1].Value);

            // Extract Receive rate
            var receiveMatch = Regex.Match(output, @"Receive rate \(Mbps\)\s+:\s+([\d.]+)");
            if (receiveMatch.Success)
                metrics.ReceiveSpeedMbps = double.Parse(receiveMatch.Groups[1].Value);

            // Extract Transmit rate
            var transmitMatch = Regex.Match(output, @"Transmit rate \(Mbps\)\s+:\s+([\d.]+)");
            if (transmitMatch.Success)
                metrics.TransmitSpeedMbps = double.Parse(transmitMatch.Groups[1].Value);

            // Extract Radio type
            var radioMatch = Regex.Match(output, @"Radio type\s+:\s+(.+)");
            if (radioMatch.Success)
                metrics.RadioType = radioMatch.Groups[1].Value.Trim();

            // Extract Authentication
            var authMatch = Regex.Match(output, @"Authentication\s+:\s+(.+)");
            if (authMatch.Success)
                metrics.Authentication = authMatch.Groups[1].Value.Trim();

            return metrics;
        }

        /// <summary>
        /// Checks if a 5GHz network is available for the current SSID
        /// </summary>
        public async Task<bool> Is5GHzAvailableAsync(string currentSsid)
        {
            if (string.IsNullOrEmpty(currentSsid))
                return false;

            var networksOutput = await ScanNetworksAsync();

            // Look for 5GHz variant of current network
            var ssid5GHz = $"{currentSsid}_5G";
            return networksOutput.Contains(ssid5GHz) && networksOutput.Contains("5 GHz");
        }
    }
}
