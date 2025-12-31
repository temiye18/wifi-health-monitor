using System;

namespace WiFiHealthMonitor.Models
{
    /// <summary>
    /// Represents a prediction about future network behavior
    /// </summary>
    public class Prediction
    {
        public PredictionType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Confidence { get; set; } // 0-100
        public string EstimatedTimeframe { get; set; } = string.Empty;
        public PredictionImpact PredictedImpact { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Types of predictions
    /// </summary>
    public enum PredictionType
    {
        SignalDegradation,
        SpeedDegradation,
        Congestion,
        Disconnection,
        SecurityThreat
    }

    /// <summary>
    /// Impact level of prediction
    /// </summary>
    public enum PredictionImpact
    {
        Low,
        Medium,
        High,
        Critical
    }
}
