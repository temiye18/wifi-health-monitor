using System.Collections.Generic;

namespace WiFiHealthMonitor.Models
{
    /// <summary>
    /// Represents information about a WiFi channel
    /// </summary>
    public class ChannelInfo
    {
        public int ChannelNumber { get; set; }
        public string Band { get; set; } = string.Empty;
        public int NetworkCount { get; set; }
        public List<string> Networks { get; set; } = new();
        public double AverageSignal { get; set; }
        public int CongestionScore { get; set; } // 0-100, higher = more congested

        public string CongestionLevel
        {
            get
            {
                if (CongestionScore >= 75) return "High";
                if (CongestionScore >= 50) return "Medium";
                if (CongestionScore >= 25) return "Low";
                return "None";
            }
        }

        public bool IsRecommended => CongestionScore < 25;
    }

    /// <summary>
    /// Recommendation for optimal channel
    /// </summary>
    public class ChannelRecommendation
    {
        public int CurrentChannel { get; set; }
        public int RecommendedChannel { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int CurrentCongestion { get; set; }
        public int RecommendedCongestion { get; set; }
        public int ImprovementPercent { get; set; }
        public List<ChannelInfo> AllChannels { get; set; } = new();
    }
}
