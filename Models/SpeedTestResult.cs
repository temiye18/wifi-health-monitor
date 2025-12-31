using System;
using System.Text.Json.Serialization;

namespace WiFiHealthMonitor.Models
{
    /// <summary>
    /// Represents the result of an internet speed test
    /// </summary>
    public class SpeedTestResult
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("download")]
        public DownloadInfo? Download { get; set; }

        [JsonPropertyName("upload")]
        public UploadInfo? Upload { get; set; }

        [JsonPropertyName("ping")]
        public PingInfo? Ping { get; set; }

        [JsonPropertyName("isp")]
        public string Isp { get; set; } = string.Empty;

        [JsonPropertyName("server")]
        public ServerInfo? Server { get; set; }

        public double DownloadMbps => Download != null ? Download.Bandwidth / 125000.0 : 0;
        public double UploadMbps => Upload != null ? Upload.Bandwidth / 125000.0 : 0;
        public double LatencyMs => Ping?.Latency ?? 0;
    }

    public class DownloadInfo
    {
        [JsonPropertyName("bandwidth")]
        public long Bandwidth { get; set; }

        [JsonPropertyName("bytes")]
        public long Bytes { get; set; }

        [JsonPropertyName("elapsed")]
        public int Elapsed { get; set; }
    }

    public class UploadInfo
    {
        [JsonPropertyName("bandwidth")]
        public long Bandwidth { get; set; }

        [JsonPropertyName("bytes")]
        public long Bytes { get; set; }

        [JsonPropertyName("elapsed")]
        public int Elapsed { get; set; }
    }

    public class PingInfo
    {
        [JsonPropertyName("jitter")]
        public double Jitter { get; set; }

        [JsonPropertyName("latency")]
        public double Latency { get; set; }

        [JsonPropertyName("low")]
        public double Low { get; set; }

        [JsonPropertyName("high")]
        public double High { get; set; }
    }

    public class ServerInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;
    }
}
