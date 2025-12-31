using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Data
{
    /// <summary>
    /// Service for managing SQLite database operations
    /// </summary>
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public DatabaseService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WiFiHealthMonitor"
            );

            Directory.CreateDirectory(appDataPath);
            _dbPath = Path.Combine(appDataPath, "wifihealth.db");
            _connectionString = $"Data Source={_dbPath}";
        }

        /// <summary>
        /// Initializes the database and creates tables if they don't exist
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Create NetworkMetrics table
            var createMetricsTable = @"
                CREATE TABLE IF NOT EXISTS NetworkMetrics (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Ssid TEXT,
                    Bssid TEXT,
                    SignalPercent INTEGER,
                    Rssi INTEGER,
                    Band TEXT,
                    Channel INTEGER,
                    ReceiveSpeedMbps REAL,
                    TransmitSpeedMbps REAL,
                    ConnectedDevices INTEGER,
                    ChannelUtilization INTEGER,
                    RadioType TEXT,
                    Authentication TEXT
                )";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = createMetricsTable;
                await command.ExecuteNonQueryAsync();
            }

            // Create SpeedTests table
            var createSpeedTestsTable = @"
                CREATE TABLE IF NOT EXISTS SpeedTests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    DownloadMbps REAL,
                    UploadMbps REAL,
                    LatencyMs REAL,
                    Jitter REAL,
                    Isp TEXT,
                    ServerName TEXT,
                    ServerLocation TEXT
                )";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = createSpeedTestsTable;
                await command.ExecuteNonQueryAsync();
            }

            // Create Alerts table
            var createAlertsTable = @"
                CREATE TABLE IF NOT EXISTS Alerts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Type INTEGER,
                    Title TEXT,
                    Message TEXT,
                    Severity INTEGER,
                    IsAcknowledged INTEGER
                )";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = createAlertsTable;
                await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Saves network metrics to the database
        /// </summary>
        public async Task SaveMetricsAsync(NetworkMetrics metrics)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var insertSql = @"
                INSERT INTO NetworkMetrics (
                    Timestamp, Ssid, Bssid, SignalPercent, Rssi, Band, Channel,
                    ReceiveSpeedMbps, TransmitSpeedMbps, ConnectedDevices, ChannelUtilization,
                    RadioType, Authentication
                ) VALUES (
                    @Timestamp, @Ssid, @Bssid, @SignalPercent, @Rssi, @Band, @Channel,
                    @ReceiveSpeedMbps, @TransmitSpeedMbps, @ConnectedDevices, @ChannelUtilization,
                    @RadioType, @Authentication
                )";

            using var command = connection.CreateCommand();
            command.CommandText = insertSql;
            command.Parameters.AddWithValue("@Timestamp", metrics.Timestamp.ToString("o"));
            command.Parameters.AddWithValue("@Ssid", metrics.Ssid);
            command.Parameters.AddWithValue("@Bssid", metrics.Bssid);
            command.Parameters.AddWithValue("@SignalPercent", metrics.SignalPercent);
            command.Parameters.AddWithValue("@Rssi", metrics.Rssi);
            command.Parameters.AddWithValue("@Band", metrics.Band);
            command.Parameters.AddWithValue("@Channel", metrics.Channel);
            command.Parameters.AddWithValue("@ReceiveSpeedMbps", metrics.ReceiveSpeedMbps);
            command.Parameters.AddWithValue("@TransmitSpeedMbps", metrics.TransmitSpeedMbps);
            command.Parameters.AddWithValue("@ConnectedDevices", metrics.ConnectedDevices);
            command.Parameters.AddWithValue("@ChannelUtilization", metrics.ChannelUtilization);
            command.Parameters.AddWithValue("@RadioType", metrics.RadioType);
            command.Parameters.AddWithValue("@Authentication", metrics.Authentication);

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Saves speed test results to the database
        /// </summary>
        public async Task SaveSpeedTestAsync(SpeedTestResult result)
        {
            Console.WriteLine($"[Database] SaveSpeedTestAsync: Saving speed test - Download: {result.DownloadMbps:F2} Mbps, Upload: {result.UploadMbps:F2} Mbps");
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var insertSql = @"
                INSERT INTO SpeedTests (
                    Timestamp, DownloadMbps, UploadMbps, LatencyMs, Jitter, Isp, ServerName, ServerLocation
                ) VALUES (
                    @Timestamp, @DownloadMbps, @UploadMbps, @LatencyMs, @Jitter, @Isp, @ServerName, @ServerLocation
                )";

            using var command = connection.CreateCommand();
            command.CommandText = insertSql;
            command.Parameters.AddWithValue("@Timestamp", result.Timestamp.ToString("o"));
            command.Parameters.AddWithValue("@DownloadMbps", result.DownloadMbps);
            command.Parameters.AddWithValue("@UploadMbps", result.UploadMbps);
            command.Parameters.AddWithValue("@LatencyMs", result.LatencyMs);
            command.Parameters.AddWithValue("@Jitter", result.Ping?.Jitter ?? 0);
            command.Parameters.AddWithValue("@Isp", result.Isp);
            command.Parameters.AddWithValue("@ServerName", result.Server?.Name ?? "");
            command.Parameters.AddWithValue("@ServerLocation", result.Server?.Location ?? "");

            await command.ExecuteNonQueryAsync();
            Console.WriteLine("[Database] SaveSpeedTestAsync: Speed test saved successfully");
        }

        /// <summary>
        /// Saves an alert to the database
        /// </summary>
        public async Task SaveAlertAsync(Alert alert)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var insertSql = @"
                INSERT INTO Alerts (
                    Timestamp, Type, Title, Message, Severity, IsAcknowledged
                ) VALUES (
                    @Timestamp, @Type, @Title, @Message, @Severity, @IsAcknowledged
                )";

            using var command = connection.CreateCommand();
            command.CommandText = insertSql;
            command.Parameters.AddWithValue("@Timestamp", alert.Timestamp.ToString("o"));
            command.Parameters.AddWithValue("@Type", (int)alert.Type);
            command.Parameters.AddWithValue("@Title", alert.Title);
            command.Parameters.AddWithValue("@Message", alert.Message);
            command.Parameters.AddWithValue("@Severity", (int)alert.Severity);
            command.Parameters.AddWithValue("@IsAcknowledged", alert.IsAcknowledged ? 1 : 0);

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Gets recent network metrics
        /// </summary>
        public async Task<List<NetworkMetrics>> GetRecentMetricsAsync(int count = 100)
        {
            var metrics = new List<NetworkMetrics>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var query = $"SELECT * FROM NetworkMetrics ORDER BY Timestamp DESC LIMIT {count}";

            using var command = connection.CreateCommand();
            command.CommandText = query;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                metrics.Add(new NetworkMetrics
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    Ssid = reader.GetString(2),
                    Bssid = reader.GetString(3),
                    SignalPercent = reader.GetInt32(4),
                    Rssi = reader.GetInt32(5),
                    Band = reader.GetString(6),
                    Channel = reader.GetInt32(7),
                    ReceiveSpeedMbps = reader.GetDouble(8),
                    TransmitSpeedMbps = reader.GetDouble(9),
                    ConnectedDevices = reader.GetInt32(10),
                    ChannelUtilization = reader.GetInt32(11),
                    RadioType = reader.GetString(12),
                    Authentication = reader.GetString(13)
                });
            }

            return metrics;
        }

        /// <summary>
        /// Gets recent unacknowledged alerts
        /// </summary>
        public async Task<List<Alert>> GetUnacknowledgedAlertsAsync()
        {
            var alerts = new List<Alert>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT * FROM Alerts WHERE IsAcknowledged = 0 ORDER BY Timestamp DESC";

            using var command = connection.CreateCommand();
            command.CommandText = query;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                alerts.Add(new Alert
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    Type = (AlertType)reader.GetInt32(2),
                    Title = reader.GetString(3),
                    Message = reader.GetString(4),
                    Severity = (AlertSeverity)reader.GetInt32(5),
                    IsAcknowledged = reader.GetInt32(6) == 1
                });
            }

            return alerts;
        }

        /// <summary>
        /// Gets the average speed over a time period
        /// </summary>
        public async Task<(double download, double upload)> GetAverageSpeedAsync(TimeSpan period)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var since = DateTime.Now.Subtract(period).ToString("o");
            var query = @"
                SELECT AVG(DownloadMbps), AVG(UploadMbps)
                FROM SpeedTests
                WHERE Timestamp >= @Since";

            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.Parameters.AddWithValue("@Since", since);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var download = reader.IsDBNull(0) ? 0 : reader.GetDouble(0);
                var upload = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                return (download, upload);
            }

            return (0, 0);
        }

        /// <summary>
        /// Gets metrics since a specific date
        /// </summary>
        public async Task<List<NetworkMetrics>> GetMetricsSinceAsync(DateTime since)
        {
            var metrics = new List<NetworkMetrics>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT * FROM NetworkMetrics WHERE Timestamp >= @Since ORDER BY Timestamp ASC";

            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.Parameters.AddWithValue("@Since", since.ToString("o"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                metrics.Add(new NetworkMetrics
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    Ssid = reader.GetString(2),
                    Bssid = reader.GetString(3),
                    SignalPercent = reader.GetInt32(4),
                    Rssi = reader.GetInt32(5),
                    Band = reader.GetString(6),
                    Channel = reader.GetInt32(7),
                    ReceiveSpeedMbps = reader.GetDouble(8),
                    TransmitSpeedMbps = reader.GetDouble(9),
                    ConnectedDevices = reader.GetInt32(10),
                    ChannelUtilization = reader.GetInt32(11),
                    RadioType = reader.GetString(12),
                    Authentication = reader.GetString(13)
                });
            }

            return metrics;
        }

        /// <summary>
        /// Gets recent speed test results
        /// </summary>
        public async Task<List<SpeedTestResult>> GetRecentSpeedTestsAsync(int count = 100)
        {
            Console.WriteLine($"[Database] GetRecentSpeedTestsAsync: Querying database for up to {count} speed tests");
            var results = new List<SpeedTestResult>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var query = $"SELECT * FROM SpeedTests ORDER BY Timestamp DESC LIMIT {count}";

            using var command = connection.CreateCommand();
            command.CommandText = query;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SpeedTestResult
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    Download = new DownloadInfo { Bandwidth = (long)(reader.GetDouble(2) * 125000) },
                    Upload = new UploadInfo { Bandwidth = (long)(reader.GetDouble(3) * 125000) },
                    Ping = new PingInfo { Latency = reader.GetDouble(4), Jitter = reader.GetDouble(5) },
                    Isp = reader.GetString(6),
                    Server = new ServerInfo
                    {
                        Name = reader.GetString(7),
                        Location = reader.GetString(8)
                    }
                });
            }

            Console.WriteLine($"[Database] GetRecentSpeedTestsAsync: Found {results.Count} speed tests in database");
            return results;
        }
    }
}
