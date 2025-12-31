using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiFiHealthMonitor.Data;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Services
{
    /// <summary>
    /// Hybrid predictive service that intelligently switches between statistical and ML.NET approaches
    /// - Uses statistical methods for first week (insufficient data for ML)
    /// - Automatically upgrades to ML.NET after collecting enough data
    /// - Provides best accuracy at each stage of data collection
    /// </summary>
    public class HybridPredictiveService
    {
        private readonly DatabaseService _database;
        private readonly PredictiveAnalyticsService _statistical;
        private readonly MLNetPredictiveService _mlNet;
        private const int ML_MINIMUM_DATA_POINTS = 500; // Minimum for ML.NET to work effectively

        public HybridPredictiveService(DatabaseService database)
        {
            _database = database;
            _statistical = new PredictiveAnalyticsService(database);
            _mlNet = new MLNetPredictiveService(database);
        }

        /// <summary>
        /// Generates predictions using the most appropriate method based on available data
        /// </summary>
        public async Task<List<Prediction>> PredictIssuesAsync()
        {
            // Get data count
            var metrics = await _database.GetRecentMetricsAsync(1000);
            var dataPoints = metrics.Count;

            // Determine which engine to use
            PredictionEngine engine;
            if (dataPoints < ML_MINIMUM_DATA_POINTS)
            {
                // Use statistical for early stages
                engine = PredictionEngine.Statistical;
                Console.WriteLine($"[Hybrid] Using Statistical engine ({dataPoints}/{ML_MINIMUM_DATA_POINTS} data points)");
                return await _statistical.PredictIssuesAsync();
            }
            else
            {
                // Use ML.NET for better accuracy
                engine = PredictionEngine.MLNet;
                Console.WriteLine($"[Hybrid] Using ML.NET engine ({dataPoints} data points available)");

                var predictions = await _mlNet.PredictIssuesAsync();

                // Add engine indicator to messages
                foreach (var prediction in predictions)
                {
                    if (!prediction.Message.Contains("ML.NET") && !prediction.Title.Contains("ML.NET"))
                    {
                        prediction.Title += " (ML.NET)";
                    }
                }

                return predictions;
            }
        }

        /// <summary>
        /// Gets the current prediction engine status
        /// </summary>
        public async Task<PredictionEngineStatus> GetEngineStatusAsync()
        {
            var metrics = await _database.GetRecentMetricsAsync(1000);
            var dataPoints = metrics.Count;
            var isUsingMLNet = dataPoints >= ML_MINIMUM_DATA_POINTS;

            var status = new PredictionEngineStatus
            {
                CurrentEngine = isUsingMLNet ? PredictionEngine.MLNet : PredictionEngine.Statistical,
                DataPointsCollected = dataPoints,
                DataPointsNeededForML = Math.Max(0, ML_MINIMUM_DATA_POINTS - dataPoints),
                ExpectedAccuracy = isUsingMLNet ? "60-85%" : "30-75%",
                WillUpgradeToML = !isUsingMLNet,
                EstimatedTimeToUpgrade = EstimateTimeToUpgrade(dataPoints)
            };

            // Get ML.NET training status if available
            if (isUsingMLNet)
            {
                var (isTrained, lastTraining, hoursUntilRetrain) = _mlNet.GetModelStatus();
                status.MLModelTrained = isTrained;
                status.LastModelTraining = lastTraining;
                status.HoursUntilRetrain = hoursUntilRetrain;
            }

            return status;
        }

        private string EstimateTimeToUpgrade(int currentDataPoints)
        {
            if (currentDataPoints >= ML_MINIMUM_DATA_POINTS)
                return "Already using ML.NET";

            var pointsNeeded = ML_MINIMUM_DATA_POINTS - currentDataPoints;

            // Assuming 30-second intervals = 120 points/hour = 2880 points/day
            var hoursNeeded = pointsNeeded / 120.0;

            if (hoursNeeded < 1)
                return $"{(int)(hoursNeeded * 60)} minutes";
            else if (hoursNeeded < 24)
                return $"{hoursNeeded:F1} hours";
            else
                return $"{hoursNeeded / 24:F1} days";
        }
    }

    /// <summary>
    /// Indicates which prediction engine is currently active
    /// </summary>
    public enum PredictionEngine
    {
        Statistical,
        MLNet
    }

    /// <summary>
    /// Status information about the prediction engine
    /// </summary>
    public class PredictionEngineStatus
    {
        public PredictionEngine CurrentEngine { get; set; }
        public int DataPointsCollected { get; set; }
        public int DataPointsNeededForML { get; set; }
        public string ExpectedAccuracy { get; set; } = string.Empty;
        public bool WillUpgradeToML { get; set; }
        public string EstimatedTimeToUpgrade { get; set; } = string.Empty;

        // ML.NET specific
        public bool MLModelTrained { get; set; }
        public DateTime LastModelTraining { get; set; }
        public int HoursUntilRetrain { get; set; }

        public override string ToString()
        {
            if (CurrentEngine == PredictionEngine.MLNet)
            {
                return $"Using ML.NET engine with {ExpectedAccuracy} accuracy. " +
                       $"Model trained {(DateTime.Now - LastModelTraining).TotalHours:F1} hours ago, " +
                       $"retraining in {HoursUntilRetrain} hours.";
            }
            else
            {
                return $"Using Statistical engine with {ExpectedAccuracy} accuracy. " +
                       $"{DataPointsCollected}/{DataPointsCollected + DataPointsNeededForML} data points collected. " +
                       $"Will upgrade to ML.NET in {EstimatedTimeToUpgrade}.";
            }
        }
    }
}
