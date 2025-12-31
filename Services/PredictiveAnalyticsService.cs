using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiFiHealthMonitor.Data;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Services
{
    /// <summary>
    /// Service for predicting network issues before they occur
    /// </summary>
    public class PredictiveAnalyticsService
    {
        private readonly DatabaseService _database;

        public PredictiveAnalyticsService(DatabaseService database)
        {
            _database = database;
        }

        /// <summary>
        /// Predicts potential connectivity issues based on historical patterns
        /// </summary>
        public async Task<List<Prediction>> PredictIssuesAsync()
        {
            var predictions = new List<Prediction>();

            // Get recent metrics for analysis
            var metrics = await _database.GetRecentMetricsAsync(500);

            if (metrics.Count < 100)
            {
                return predictions; // Need more data
            }

            // Predict signal degradation
            var signalPrediction = PredictSignalDegradation(metrics);
            if (signalPrediction != null)
                predictions.Add(signalPrediction);

            // Predict speed issues
            var speedPrediction = PredictSpeedIssues(metrics);
            if (speedPrediction != null)
                predictions.Add(speedPrediction);

            // Predict peak congestion times
            var congestionPrediction = PredictCongestion(metrics);
            if (congestionPrediction != null)
                predictions.Add(congestionPrediction);

            return predictions;
        }

        /// <summary>
        /// Predicts signal degradation based on trends
        /// </summary>
        private Prediction? PredictSignalDegradation(List<NetworkMetrics> metrics)
        {
            // Analyze signal trend over time
            var recentMetrics = metrics.OrderByDescending(m => m.Timestamp).Take(100).Reverse().ToList();

            if (recentMetrics.Count < 50)
                return null;

            // Calculate linear regression for signal strength
            var trend = CalculateTrend(recentMetrics.Select((m, i) => (i, (double)m.SignalPercent)).ToList());

            // If trend is negative and significant
            if (trend < -0.5) // Losing more than 0.5% per reading
            {
                var currentSignal = recentMetrics.Last().SignalPercent;
                var estimatedDropInHour = trend * 120; // 120 readings in 1 hour (30 sec intervals)

                if (currentSignal + estimatedDropInHour < 50) // Will drop below 50% soon
                {
                    return new Prediction
                    {
                        Type = PredictionType.SignalDegradation,
                        Severity = AlertSeverity.Medium,
                        Title = "Signal Degradation Predicted",
                        Message = $"Signal strength is declining. Currently at {currentSignal}%, " +
                                 $"predicted to reach {(int)(currentSignal + estimatedDropInHour)}% within the next hour. " +
                                 "Consider moving closer to router or checking for interference.",
                        Confidence = CalculateConfidence(trend, recentMetrics.Count),
                        EstimatedTimeframe = "Within 1 hour",
                        PredictedImpact = PredictionImpact.Medium
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Predicts speed issues based on historical patterns
        /// </summary>
        private Prediction? PredictSpeedIssues(List<NetworkMetrics> metrics)
        {
            var currentHour = DateTime.Now.Hour;

            // Group by hour to find patterns
            var hourlyAverages = metrics
                .GroupBy(m => m.Timestamp.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    AvgSpeed = g.Average(m => m.ReceiveSpeedMbps),
                    Count = g.Count()
                })
                .Where(h => h.Count >= 5)
                .ToList();

            var nextHour = (currentHour + 1) % 24;
            var nextHourStats = hourlyAverages.FirstOrDefault(h => h.Hour == nextHour);
            var currentHourStats = hourlyAverages.FirstOrDefault(h => h.Hour == currentHour);

            if (nextHourStats != null && currentHourStats != null)
            {
                var expectedDrop = currentHourStats.AvgSpeed - nextHourStats.AvgSpeed;
                var dropPercent = (expectedDrop / currentHourStats.AvgSpeed) * 100;

                if (dropPercent > 30) // More than 30% drop expected
                {
                    return new Prediction
                    {
                        Type = PredictionType.SpeedDegradation,
                        Severity = AlertSeverity.Low,
                        Title = "Speed Slowdown Expected",
                        Message = $"Based on historical patterns, your connection speed typically drops by {dropPercent:F0}% " +
                                 $"around {FormatHour(nextHour)}. " +
                                 $"Consider scheduling large downloads for later.",
                        Confidence = CalculateConfidence(dropPercent / 10, nextHourStats.Count),
                        EstimatedTimeframe = $"Around {FormatHour(nextHour)}",
                        PredictedImpact = PredictionImpact.Low
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Predicts network congestion based on time patterns
        /// </summary>
        private Prediction? PredictCongestion(List<NetworkMetrics> metrics)
        {
            var currentHour = DateTime.Now.Hour;
            var currentDay = (int)DateTime.Now.DayOfWeek;

            // Find similar time patterns
            var similarTimes = metrics
                .Where(m => m.Timestamp.Hour == currentHour && (int)m.Timestamp.DayOfWeek == currentDay)
                .OrderByDescending(m => m.Timestamp)
                .Take(20)
                .ToList();

            if (similarTimes.Count >= 10)
            {
                var avgUtilization = similarTimes.Average(m => m.ChannelUtilization);

                if (avgUtilization > 70)
                {
                    return new Prediction
                    {
                        Type = PredictionType.Congestion,
                        Severity = AlertSeverity.Low,
                        Title = "Network Congestion Likely",
                        Message = $"This time period typically experiences high congestion (avg {avgUtilization:F0}% channel utilization). " +
                                 "Consider using 5GHz band or waiting until later for bandwidth-intensive tasks.",
                        Confidence = CalculateConfidence(avgUtilization / 10, similarTimes.Count),
                        EstimatedTimeframe = "Current time period",
                        PredictedImpact = PredictionImpact.Medium
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Calculates trend using simple linear regression
        /// </summary>
        private double CalculateTrend(List<(int x, double y)> data)
        {
            var n = data.Count;
            var sumX = data.Sum(d => d.x);
            var sumY = data.Sum(d => d.y);
            var sumXY = data.Sum(d => d.x * d.y);
            var sumX2 = data.Sum(d => d.x * d.x);

            // Calculate slope (trend)
            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);

            return slope;
        }

        /// <summary>
        /// Calculates confidence score for prediction (0-100)
        /// </summary>
        private int CalculateConfidence(double strength, int sampleSize)
        {
            // Base confidence on sample size
            var sizeConfidence = Math.Min(sampleSize / 2.0, 50);

            // Adjust for prediction strength
            var strengthConfidence = Math.Min(Math.Abs(strength) * 5, 50);

            return (int)Math.Min(sizeConfidence + strengthConfidence, 100);
        }

        private string FormatHour(int hour)
        {
            if (hour == 0) return "12 AM";
            if (hour < 12) return $"{hour} AM";
            if (hour == 12) return "12 PM";
            return $"{hour - 12} PM";
        }
    }

}
