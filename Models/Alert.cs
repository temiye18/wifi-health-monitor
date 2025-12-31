using System;

namespace WiFiHealthMonitor.Models
{
    /// <summary>
    /// Represents an alert or recommendation for the user
    /// </summary>
    public class Alert
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public AlertType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public bool IsAcknowledged { get; set; }

        public Alert() { }

        public Alert(string title, string message, AlertType type = AlertType.Information, AlertSeverity severity = AlertSeverity.Info)
        {
            Timestamp = DateTime.Now;
            Title = title;
            Message = message;
            Type = type;
            Severity = severity;
            IsAcknowledged = false;
        }
    }

    public enum AlertType
    {
        Information,
        Warning,
        Error,
        Recommendation,
        SpeedIssue,
        SignalIssue,
        SecurityIssue,
        NewDevice
    }

    public enum AlertSeverity
    {
        Info,
        Low,
        Medium,
        High,
        Critical
    }
}
