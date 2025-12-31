using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Services
{
    /// <summary>
    /// Service for analyzing WiFi channels and recommending optimal settings
    /// </summary>
    public class ChannelOptimizerService
    {
        private readonly WiFiMonitorService _wifiMonitor;

        public ChannelOptimizerService(WiFiMonitorService wifiMonitor)
        {
            _wifiMonitor = wifiMonitor;
        }

        /// <summary>
        /// Analyzes all WiFi channels and recommends the best one
        /// </summary>
        public async Task<ChannelRecommendation?> GetChannelRecommendationAsync()
        {
            try
            {
                // Scan all networks
                var networksOutput = await _wifiMonitor.ScanNetworksAsync();
                if (string.IsNullOrEmpty(networksOutput))
                    return null;

                // Get current metrics
                var currentMetrics = await _wifiMonitor.CollectMetricsAsync();
                if (currentMetrics == null)
                    return null;

                // Analyze channels
                var channels = AnalyzeChannels(networksOutput, currentMetrics.Band);

                if (!channels.Any())
                    return null;

                // Find best channel
                var bestChannel = channels
                    .Where(c => c.Band == currentMetrics.Band)
                    .OrderBy(c => c.CongestionScore)
                    .FirstOrDefault();

                var currentChannel = channels.FirstOrDefault(c => c.ChannelNumber == currentMetrics.Channel);

                if (bestChannel == null || currentChannel == null)
                    return null;

                // Calculate improvement
                var improvement = currentChannel.CongestionScore - bestChannel.CongestionScore;
                var improvementPercent = currentChannel.CongestionScore > 0
                    ? (improvement * 100) / currentChannel.CongestionScore
                    : 0;

                var recommendation = new ChannelRecommendation
                {
                    CurrentChannel = currentMetrics.Channel,
                    RecommendedChannel = bestChannel.ChannelNumber,
                    CurrentCongestion = currentChannel.CongestionScore,
                    RecommendedCongestion = bestChannel.CongestionScore,
                    ImprovementPercent = improvementPercent,
                    AllChannels = channels,
                    Reason = GenerateReason(currentChannel, bestChannel, improvement)
                };

                return recommendation;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error analyzing channels: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Analyzes channel usage from network scan
        /// </summary>
        private List<ChannelInfo> AnalyzeChannels(string scanOutput, string currentBand)
        {
            var channels = new Dictionary<int, ChannelInfo>();
            var lines = scanOutput.Split('\n');

            string? currentSsid = null;
            string? currentBandParsed = null;
            int? currentChannel = null;
            int? currentSignal = null;

            foreach (var line in lines)
            {
                // SSID line
                var ssidMatch = Regex.Match(line, @"SSID \d+ : (.+)");
                if (ssidMatch.Success)
                {
                    currentSsid = ssidMatch.Groups[1].Value.Trim();
                    continue;
                }

                // Band line
                var bandMatch = Regex.Match(line, @"Band\s+:\s+([\d.]+\s*GHz)");
                if (bandMatch.Success)
                {
                    currentBandParsed = bandMatch.Groups[1].Value.Trim();
                    continue;
                }

                // Channel line
                var channelMatch = Regex.Match(line, @"Channel\s+:\s+(\d+)");
                if (channelMatch.Success)
                {
                    currentChannel = int.Parse(channelMatch.Groups[1].Value);
                    continue;
                }

                // Signal line
                var signalMatch = Regex.Match(line, @"Signal\s+:\s+(\d+)%");
                if (signalMatch.Success)
                {
                    currentSignal = int.Parse(signalMatch.Groups[1].Value);

                    // We have all info for this network, record it
                    if (currentChannel.HasValue && !string.IsNullOrEmpty(currentBandParsed) && !string.IsNullOrEmpty(currentSsid))
                    {
                        if (!channels.ContainsKey(currentChannel.Value))
                        {
                            channels[currentChannel.Value] = new ChannelInfo
                            {
                                ChannelNumber = currentChannel.Value,
                                Band = currentBandParsed,
                                Networks = new List<string>()
                            };
                        }

                        var channelInfo = channels[currentChannel.Value];
                        channelInfo.NetworkCount++;
                        channelInfo.Networks.Add(currentSsid);

                        if (currentSignal.HasValue)
                        {
                            channelInfo.AverageSignal = (channelInfo.AverageSignal * (channelInfo.NetworkCount - 1) + currentSignal.Value) / channelInfo.NetworkCount;
                        }
                    }

                    // Reset for next network
                    currentSsid = null;
                    currentChannel = null;
                    currentSignal = null;
                }
            }

            // Calculate congestion scores
            foreach (var channel in channels.Values)
            {
                // Base score on network count
                var baseScore = Math.Min(channel.NetworkCount * 20, 100);

                // Adjust for signal strength (stronger signals = more interference)
                var signalFactor = (int)(channel.AverageSignal / 2);

                // Consider channel overlap (2.4GHz channels overlap)
                var overlapPenalty = 0;
                if (channel.Band == "2.4 GHz")
                {
                    // Channels 1, 6, 11 don't overlap
                    // Other channels have significant overlap
                    if (channel.ChannelNumber != 1 && channel.ChannelNumber != 6 && channel.ChannelNumber != 11)
                    {
                        overlapPenalty = 20;
                    }
                }

                channel.CongestionScore = Math.Min(baseScore + signalFactor + overlapPenalty, 100);
            }

            // Fill in missing channels for current band
            if (currentBand == "2.4 GHz")
            {
                for (int i = 1; i <= 11; i++)
                {
                    if (!channels.ContainsKey(i))
                    {
                        channels[i] = new ChannelInfo
                        {
                            ChannelNumber = i,
                            Band = "2.4 GHz",
                            NetworkCount = 0,
                            CongestionScore = 0
                        };
                    }
                }
            }
            else if (currentBand == "5 GHz")
            {
                // Common 5GHz channels
                var common5GHzChannels = new[] { 36, 40, 44, 48, 149, 153, 157, 161, 165 };
                foreach (var ch in common5GHzChannels)
                {
                    if (!channels.ContainsKey(ch))
                    {
                        channels[ch] = new ChannelInfo
                        {
                            ChannelNumber = ch,
                            Band = "5 GHz",
                            NetworkCount = 0,
                            CongestionScore = 0
                        };
                    }
                }
            }

            return channels.Values.OrderBy(c => c.ChannelNumber).ToList();
        }

        private string GenerateReason(ChannelInfo current, ChannelInfo recommended, int improvement)
        {
            if (improvement <= 10)
            {
                return $"Your current channel {current.ChannelNumber} is already optimal. No change needed.";
            }

            var reasons = new List<string>();

            if (current.NetworkCount > recommended.NetworkCount)
            {
                reasons.Add($"Channel {recommended.ChannelNumber} has {current.NetworkCount - recommended.NetworkCount} fewer competing networks");
            }

            if (current.CongestionScore >= 75)
            {
                reasons.Add("Current channel is heavily congested");
            }

            if (recommended.CongestionScore < 25)
            {
                reasons.Add($"Channel {recommended.ChannelNumber} has minimal interference");
            }

            if (current.ChannelNumber != 1 && current.ChannelNumber != 6 && current.ChannelNumber != 11 && current.Band == "2.4 GHz")
            {
                if (recommended.ChannelNumber == 1 || recommended.ChannelNumber == 6 || recommended.ChannelNumber == 11)
                {
                    reasons.Add($"Channel {recommended.ChannelNumber} avoids overlap with neighboring channels");
                }
            }

            if (reasons.Any())
            {
                return $"Switching to channel {recommended.ChannelNumber} will improve performance by ~{improvement}%. " + string.Join(". ", reasons) + ".";
            }

            return $"Channel {recommended.ChannelNumber} is less congested than channel {current.ChannelNumber}.";
        }

        /// <summary>
        /// Gets channel information for a specific channel
        /// </summary>
        public async Task<ChannelInfo?> GetChannelInfoAsync(int channelNumber)
        {
            var recommendation = await GetChannelRecommendationAsync();
            return recommendation?.AllChannels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
        }
    }
}
