using Microsoft.ML.Data;

namespace WiFiHealthMonitor.Models
{
    /// <summary>
    /// Input data for ML.NET time-series forecasting
    /// </summary>
    public class SignalData
    {
        [LoadColumn(0)]
        public DateTime Timestamp { get; set; }

        [LoadColumn(1)]
        public float SignalPercent { get; set; }
    }

    /// <summary>
    /// Output forecast for signal strength
    /// </summary>
    public class SignalForecast
    {
        [VectorType(12)] // Forecast 12 periods ahead (6 minutes at 30-second intervals)
        public float[] ForecastedSignal { get; set; } = Array.Empty<float>();

        [VectorType(12)]
        public float[] LowerBoundSignal { get; set; } = Array.Empty<float>();

        [VectorType(12)]
        public float[] UpperBoundSignal { get; set; } = Array.Empty<float>();
    }

    /// <summary>
    /// Input data for speed forecasting
    /// </summary>
    public class SpeedData
    {
        [LoadColumn(0)]
        public DateTime Timestamp { get; set; }

        [LoadColumn(1)]
        public float SpeedMbps { get; set; }
    }

    /// <summary>
    /// Output forecast for network speed
    /// </summary>
    public class SpeedForecast
    {
        [VectorType(12)]
        public float[] ForecastedSpeed { get; set; } = Array.Empty<float>();

        [VectorType(12)]
        public float[] LowerBoundSpeed { get; set; } = Array.Empty<float>();

        [VectorType(12)]
        public float[] UpperBoundSpeed { get; set; } = Array.Empty<float>();
    }

    /// <summary>
    /// Prediction result with ML.NET confidence
    /// </summary>
    public class MLPredictionResult
    {
        public PredictionType Type { get; set; }
        public float PredictedValue { get; set; }
        public float ConfidenceLower { get; set; }
        public float ConfidenceUpper { get; set; }
        public int ConfidencePercent { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool ShouldAlert { get; set; }
    }
}
