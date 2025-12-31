using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiFiHealthMonitor.Data;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Services
{
    /// <summary>
    /// Service for historical data analysis and insights
    /// </summary>
    public class AnalyticsService
    {
        private readonly DatabaseService _database;

        public AnalyticsService(DatabaseService database)
        {
            _database = database;
        }

        /// <summary>
        /// Analyzes historical data to find best times for large downloads
        /// </summary>
        public async Task<BestTimeAnalysis?> GetBestDownloadTimesAsync()
        {
            var metrics = await _database.GetRecentMetricsAsync(1000);

            if (metrics.Count < 100)
                return null; // Need more data for meaningful analysis

            // Group by hour of day and filter for hours with sufficient data
            var hourlyStats = metrics
                .GroupBy(m => m.Timestamp.Hour)
                .Select(g => new HourlyStats
                {
                    Hour = g.Key,
                    AverageSpeed = g.Average(m => m.ReceiveSpeedMbps),
                    AverageSignal = g.Average(m => m.SignalPercent),
                    SampleCount = g.Count(),
                    MinSpeed = g.Min(m => m.ReceiveSpeedMbps),
                    MaxSpeed = g.Max(m => m.ReceiveSpeedMbps)
                })
                .Where(h => h.SampleCount >= 10) // Need at least 10 samples per hour for reliability
                .OrderByDescending(h => h.AverageSpeed)
                .ToList();

            // Need data from at least 3 different hours to make meaningful comparisons
            if (hourlyStats.Count < 3)
                return null;

            var bestHours = hourlyStats.Take(3).OrderBy(h => h.Hour).ToList();
            var worstHours = hourlyStats.OrderBy(h => h.AverageSpeed).Take(3).OrderBy(h => h.Hour).ToList();

            return new BestTimeAnalysis
            {
                BestHours = bestHours,
                WorstHours = worstHours,
                OverallAverage = metrics.Average(m => m.ReceiveSpeedMbps),
                DataPoints = metrics.Count,
                Recommendation = GenerateTimeRecommendation(bestHours, worstHours)
            };
        }

        /// <summary>
        /// Analyzes ISP performance patterns
        /// </summary>
        public async Task<IspAnalysis?> AnalyzeIspPerformanceAsync()
        {
            var speedTests = await _database.GetRecentSpeedTestsAsync(100);
            Console.WriteLine($"[Analytics] AnalyzeIspPerformanceAsync: Found {speedTests.Count} speed tests");

            if (speedTests.Count < 2)
            {
                Console.WriteLine("[Analytics] AnalyzeIspPerformanceAsync: Not enough data (need 2+), returning null");
                return null;
            }

            Console.WriteLine("[Analytics] AnalyzeIspPerformanceAsync: Generating ISP analysis...");

            // Analyze by time of day
            var hourlyPerformance = speedTests
                .GroupBy(s => s.Timestamp.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    AvgSpeed = g.Average(s => s.DownloadMbps),
                    Count = g.Count()
                })
                .Where(h => h.Count >= 3) // Need sufficient samples
                .ToList();

            var overallAvg = speedTests.Average(s => s.DownloadMbps);
            var bestHour = hourlyPerformance.OrderByDescending(h => h.AvgSpeed).FirstOrDefault();
            var worstHour = hourlyPerformance.OrderBy(h => h.AvgSpeed).FirstOrDefault();

            // Check for consistent slowdowns (potential throttling)
            // Only calculate degradation if we have enough data (more than 10 tests)
            double degradationPercent = 0;
            if (speedTests.Count > 10)
            {
                var recentTests = speedTests.OrderByDescending(s => s.Timestamp).Take(10).ToList();
                var recentAvg = recentTests.Average(s => s.DownloadMbps);
                var historicalTests = speedTests.OrderByDescending(s => s.Timestamp).Skip(10).ToList();
                if (historicalTests.Any())
                {
                    var historicalAvg = historicalTests.Average(s => s.DownloadMbps);
                    degradationPercent = historicalAvg > 0 ? ((historicalAvg - recentAvg) / historicalAvg) * 100 : 0;
                }
            }

            return new IspAnalysis
            {
                TotalTests = speedTests.Count,
                AverageDownloadSpeed = overallAvg,
                AverageUploadSpeed = speedTests.Average(s => s.UploadMbps),
                AverageLatency = speedTests.Average(s => s.LatencyMs),
                BestHour = bestHour?.Hour ?? 0,
                WorstHour = worstHour?.Hour ?? 0,
                SpeedDegradation = degradationPercent,
                PossibleThrottling = degradationPercent > 20,
                Recommendation = GenerateIspRecommendation(degradationPercent, bestHour, worstHour, hourlyPerformance.Count)
            };
        }

        /// <summary>
        /// Gets network stability metrics
        /// </summary>
        public async Task<NetworkStability> GetNetworkStabilityAsync(TimeSpan period)
        {
            var metrics = await _database.GetMetricsSinceAsync(DateTime.Now - period);

            if (metrics.Count < 10)
                return new NetworkStability { InsufficientData = true };

            // Calculate signal stability (variance)
            var avgSignal = metrics.Average(m => m.SignalPercent);
            var signalVariance = metrics.Average(m => Math.Pow(m.SignalPercent - avgSignal, 2));
            var signalStdDev = Math.Sqrt(signalVariance);

            // Calculate speed stability
            var avgSpeed = metrics.Average(m => m.ReceiveSpeedMbps);
            var speedVariance = metrics.Average(m => Math.Pow(m.ReceiveSpeedMbps - avgSpeed, 2));
            var speedStdDev = Math.Sqrt(speedVariance);

            // Count disconnections or severe degradations
            var significantDrops = 0;
            for (int i = 1; i < metrics.Count; i++)
            {
                var signalDrop = metrics[i - 1].SignalPercent - metrics[i].SignalPercent;
                if (signalDrop > 30) // 30% signal drop
                    significantDrops++;
            }

            return new NetworkStability
            {
                Period = period,
                AverageSignal = avgSignal,
                SignalStability = Math.Max(0, 100 - signalStdDev * 2), // 0-100 score
                AverageSpeed = avgSpeed,
                SpeedStability = Math.Max(0, 100 - (speedStdDev / avgSpeed) * 100),
                SignificantDrops = significantDrops,
                StabilityScore = CalculateStabilityScore(signalStdDev, speedStdDev, significantDrops),
                IsStable = signalStdDev < 15 && significantDrops < 3
            };
        }

        private double CalculateStabilityScore(double signalStdDev, double speedStdDev, int drops)
        {
            var signalScore = Math.Max(0, 50 - signalStdDev);
            var dropScore = Math.Max(0, 30 - (drops * 5));
            var speedScore = Math.Max(0, 20 - speedStdDev / 10);

            return Math.Min(100, signalScore + dropScore + speedScore);
        }

        private string GenerateTimeRecommendation(List<HourlyStats> best, List<HourlyStats> worst)
        {
            var bestTime = best.First();
            var worstTime = worst.First();

            var improvement = ((bestTime.AverageSpeed - worstTime.AverageSpeed) / worstTime.AverageSpeed) * 100;

            // If best and worst are the same time, or improvement is negligible
            if (bestTime.Hour == worstTime.Hour || improvement < 10)
            {
                return $"Network speeds are fairly consistent throughout the day (±{improvement:F0}% variation). " +
                       $"Average speed: {bestTime.AverageSpeed:F1} Mbps. Downloads can be scheduled at any time.";
            }

            return $"For optimal download speeds, schedule large transfers between {FormatHour(bestTime.Hour)}-{FormatHour(bestTime.Hour + 1)} " +
                   $"(averaging {bestTime.AverageSpeed:F1} Mbps). " +
                   $"Avoid {FormatHour(worstTime.Hour)}-{FormatHour(worstTime.Hour + 1)} when speeds drop to {worstTime.AverageSpeed:F1} Mbps " +
                   $"({improvement:F0}% slower).";
        }

        private string GenerateIspRecommendation(double degradation, dynamic? bestHour, dynamic? worstHour, int uniqueHoursCount)
        {
            // Check for throttling first (highest priority)
            if (degradation > 30)
            {
                return "WARNING: Significant speed degradation detected (>30%). Possible ISP throttling or service issues. " +
                       "Consider running speed tests during different times and contacting your ISP.";
            }

            if (degradation > 20)
            {
                return "NOTICE: Moderate speed degradation observed. Monitor your connection and consider documenting speeds for ISP support.";
            }

            // Only provide time-based recommendations if we have data from multiple hours
            if (bestHour != null && worstHour != null && uniqueHoursCount >= 3)
            {
                // Check if best and worst are different hours
                if (bestHour.Hour != worstHour.Hour)
                {
                    // Calculate the speed difference
                    var speedDiff = ((bestHour.AvgSpeed - worstHour.AvgSpeed) / worstHour.AvgSpeed) * 100;

                    // Only mention time patterns if there's a meaningful difference (>15%)
                    if (speedDiff > 15)
                    {
                        return $"Your internet performs best around {FormatHour(bestHour.Hour)} ({bestHour.AvgSpeed:F1} Mbps) " +
                               $"and slowest around {FormatHour(worstHour.Hour)} ({worstHour.AvgSpeed:F1} Mbps). " +
                               $"This {speedDiff:F0}% variation is typical network congestion.";
                    }
                }
            }

            // Default: stable performance
            return "Your ISP performance is stable across different times of day. No significant throttling or congestion patterns detected.";
        }

        private string FormatHour(int hour)
        {
            hour = hour % 24;
            if (hour == 0) return "12 AM";
            if (hour < 12) return $"{hour} AM";
            if (hour == 12) return "12 PM";
            return $"{hour - 12} PM";
        }
    }

    public class HourlyStats
    {
        public int Hour { get; set; }
        public double AverageSpeed { get; set; }
        public double AverageSignal { get; set; }
        public int SampleCount { get; set; }
        public double MinSpeed { get; set; }
        public double MaxSpeed { get; set; }
    }

    public class BestTimeAnalysis
    {
        public List<HourlyStats> BestHours { get; set; } = new();
        public List<HourlyStats> WorstHours { get; set; } = new();
        public double OverallAverage { get; set; }
        public int DataPoints { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class IspAnalysis
    {
        public int TotalTests { get; set; }
        public double AverageDownloadSpeed { get; set; }
        public double AverageUploadSpeed { get; set; }
        public double AverageLatency { get; set; }
        public int BestHour { get; set; }
        public int WorstHour { get; set; }
        public double SpeedDegradation { get; set; }
        public bool PossibleThrottling { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class NetworkStability
    {
        public TimeSpan Period { get; set; }
        public double AverageSignal { get; set; }
        public double SignalStability { get; set; }
        public double AverageSpeed { get; set; }
        public double SpeedStability { get; set; }
        public int SignificantDrops { get; set; }
        public double StabilityScore { get; set; }
        public bool IsStable { get; set; }
        public bool InsufficientData { get; set; }
    }
}
