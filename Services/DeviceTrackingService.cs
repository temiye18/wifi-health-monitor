using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Services
{
    /// <summary>
    /// Service for tracking devices on the network
    /// </summary>
    public class DeviceTrackingService
    {
        private readonly List<NetworkDevice> _knownDevices = new();
        private readonly object _lockObject = new();

        /// <summary>
        /// Scans the network for connected devices using ARP table
        /// </summary>
        public async Task<List<NetworkDevice>> ScanDevicesAsync()
        {
            try
            {
                var devices = new List<NetworkDevice>();

                // Get ARP table
                var arpOutput = await RunCommandAsync("arp -a");
                if (string.IsNullOrEmpty(arpOutput))
                    return devices;

                // Parse ARP table
                var lines = arpOutput.Split('\n');
                foreach (var line in lines)
                {
                    // Match pattern: IP Address      Physical Address      Type
                    var match = Regex.Match(line.Trim(), @"(\d+\.\d+\.\d+\.\d+)\s+([\da-f-]+)\s+(\w+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var ipAddress = match.Groups[1].Value;
                        var macAddress = match.Groups[2].Value.ToUpper();
                        var type = match.Groups[3].Value;

                        // Skip invalid MACs
                        if (macAddress == "FF-FF-FF-FF-FF-FF" || macAddress.Length < 12)
                            continue;

                        var device = new NetworkDevice
                        {
                            IpAddress = ipAddress,
                            MacAddress = macAddress,
                            LastSeen = DateTime.Now,
                            FirstSeen = DateTime.Now
                        };

                        // Try to resolve hostname
                        device.HostName = await ResolveHostNameAsync(ipAddress);

                        // Identify vendor from MAC
                        device.Vendor = IdentifyVendor(macAddress);

                        // Guess device type
                        device.Type = GuessDeviceType(device.HostName, device.Vendor);

                        devices.Add(device);
                    }
                }

                // Update known devices
                UpdateKnownDevices(devices);

                return devices;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning devices: {ex.Message}");
                return new List<NetworkDevice>();
            }
        }

        /// <summary>
        /// Gets list of new devices (first seen recently)
        /// </summary>
        public List<NetworkDevice> GetNewDevices(TimeSpan threshold)
        {
            lock (_lockObject)
            {
                return _knownDevices
                    .Where(d => d.IsNew && d.TimeSinceFirstSeen < threshold)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all known devices
        /// </summary>
        public List<NetworkDevice> GetAllDevices()
        {
            lock (_lockObject)
            {
                return _knownDevices.ToList();
            }
        }

        /// <summary>
        /// Marks a device as trusted
        /// </summary>
        public void SetDeviceTrust(string macAddress, bool trusted)
        {
            lock (_lockObject)
            {
                var device = _knownDevices.FirstOrDefault(d => d.MacAddress == macAddress);
                if (device != null)
                {
                    device.IsTrusted = trusted;
                    device.IsNew = false;
                }
            }
        }

        private void UpdateKnownDevices(List<NetworkDevice> scannedDevices)
        {
            lock (_lockObject)
            {
                foreach (var scanned in scannedDevices)
                {
                    var known = _knownDevices.FirstOrDefault(d => d.MacAddress == scanned.MacAddress);
                    if (known != null)
                    {
                        // Update existing device
                        known.LastSeen = scanned.LastSeen;
                        known.IpAddress = scanned.IpAddress;
                        known.ConnectionCount++;

                        // Update hostname if we got a better one
                        if (!string.IsNullOrEmpty(scanned.HostName) && string.IsNullOrEmpty(known.HostName))
                        {
                            known.HostName = scanned.HostName;
                        }
                    }
                    else
                    {
                        // New device
                        scanned.IsNew = true;
                        scanned.IsTrusted = false;
                        scanned.ConnectionCount = 1;
                        _knownDevices.Add(scanned);
                    }
                }
            }
        }

        private async Task<string> ResolveHostNameAsync(string ipAddress)
        {
            try
            {
                var output = await RunCommandAsync($"nslookup {ipAddress}");
                var match = Regex.Match(output, @"Name:\s+(.+)");
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch { }

            return string.Empty;
        }

        private string IdentifyVendor(string macAddress)
        {
            // OUI (Organizationally Unique Identifier) lookup
            // First 6 characters identify manufacturer
            var oui = macAddress.Replace("-", "").Replace(":", "").Substring(0, 6);

            // Common vendor OUIs (partial list)
            var vendors = new Dictionary<string, string>
            {
                { "000000", "Xerox" },
                { "00D0C9", "Intel" },
                { "001B63", "Apple" },
                { "D85D4C", "Apple" },
                { "F0F6C1", "Apple" },
                { "3CE072", "Apple" },
                { "20C9D0", "Samsung" },
                { "E439E5", "Samsung" },
                { "C869CD", "Samsung" },
                { "DC71EB", "TP-Link" },
                { "F4EC38", "TP-Link" },
                { "50C7BF", "TP-Link" },
                { "B065BD", "Netgear" },
                { "C03F0E", "Netgear" },
                { "E091F5", "Netgear" },
                { "B0B98A", "D-Link" },
                { "C0A00A", "D-Link" },
                { "54E6FC", "Asus" },
                { "7054D5", "Google" },
                { "DC2C6E", "Google" },
                { "B8273C", "Sonos" },
                { "00124B", "Amazon" },
                { "68542D", "Amazon" },
                { "B47C9C", "Amazon Echo" },
                { "D8BB2C", "Microsoft" },
                { "18DB04", "Microsoft" }
            };

            return vendors.TryGetValue(oui, out var vendor) ? vendor : "Unknown";
        }

        private DeviceType GuessDeviceType(string hostname, string vendor)
        {
            var combined = $"{hostname} {vendor}".ToLower();

            if (combined.Contains("router") || combined.Contains("gateway"))
                return DeviceType.Router;
            if (combined.Contains("apple") || combined.Contains("iphone") || combined.Contains("ipad"))
                return DeviceType.Phone;
            if (combined.Contains("samsung") && (combined.Contains("phone") || combined.Contains("galaxy")))
                return DeviceType.Phone;
            if (combined.Contains("tv") || combined.Contains("roku") || combined.Contains("chromecast"))
                return DeviceType.SmartTV;
            if (combined.Contains("echo") || combined.Contains("sonos") || combined.Contains("nest"))
                return DeviceType.IoT;
            if (combined.Contains("desktop") || combined.Contains("laptop") || combined.Contains("pc"))
                return DeviceType.Computer;
            if (combined.Contains("tablet") || combined.Contains("ipad"))
                return DeviceType.Tablet;

            return DeviceType.Unknown;
        }

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
            catch
            {
                return string.Empty;
            }
        }
    }
}
