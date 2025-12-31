using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WiFiHealthMonitor.Data;
using WiFiHealthMonitor.Models;
using Timer = System.Timers.Timer;

namespace WiFiHealthMonitor.Services
{
    /// <summary>
    /// Main service that orchestrates all monitoring activities
    /// </summary>
    public class MonitoringService
    {
        private readonly WiFiMonitorService _wifiMonitor;
        private readonly SpeedTestService _speedTest;
        private readonly DatabaseService _database;
        private readonly AlertEngine _alertEngine;
        private readonly DeviceTrackingService _deviceTracking;
        private readonly ChannelOptimizerService _channelOptimizer;
        private readonly AnalyticsService _analytics;
        private readonly HybridPredictiveService _predictive;

        private Timer? _monitoringTimer;
        private Timer? _speedTestTimer;
        private Timer? _deviceScanTimer;
        private Timer? _predictionTimer;
        private bool _isRunning;

        public event EventHandler<NetworkMetrics>? MetricsUpdated;
        public event EventHandler<Alert>? AlertGenerated;
        public event EventHandler<SpeedTestResult>? SpeedTestCompleted;
        public event EventHandler<List<NetworkDevice>>? DevicesUpdated;
        public event EventHandler<Prediction>? PredictionGenerated;
        public event EventHandler<ChannelRecommendation>? ChannelRecommendationAvailable;

        public NetworkMetrics? CurrentMetrics { get; private set; }
        public bool IsRunning => _isRunning;

        public MonitoringService()
        {
            _wifiMonitor = new WiFiMonitorService();
            _speedTest = new SpeedTestService();
            _database = new DatabaseService();
            _alertEngine = new AlertEngine(_wifiMonitor);
            _deviceTracking = new DeviceTrackingService();
            _channelOptimizer = new ChannelOptimizerService(_wifiMonitor);
            _analytics = new AnalyticsService(_database);
            _predictive = new HybridPredictiveService(_database);
        }

        /// <summary>
        /// Initializes the monitoring service
        /// </summary>
        public async Task InitializeAsync()
        {
            await _database.InitializeDatabaseAsync();
            await _speedTest.EnsureSpeedTestInstalledAsync();
        }

        /// <summary>
        /// Starts monitoring WiFi metrics
        /// </summary>
        public void StartMonitoring(int intervalSeconds = 30)
        {
            if (_isRunning)
                return;

            _isRunning = true;

            // Start WiFi metrics monitoring
            _monitoringTimer = new Timer(intervalSeconds * 1000);
            _monitoringTimer.Elapsed += async (sender, e) => await CollectAndAnalyzeMetricsAsync();
            _monitoringTimer.AutoReset = true;
            _monitoringTimer.Start();

            // Start speed test (every 1 hour)
            _speedTestTimer = new Timer(3600 * 1000);
            _speedTestTimer.Elapsed += async (sender, e) => await RunSpeedTestAsync();
            _speedTestTimer.AutoReset = true;
            _speedTestTimer.Start();

            // Start device scanning (every 2 minutes)
            _deviceScanTimer = new Timer(120 * 1000);
            _deviceScanTimer.Elapsed += async (sender, e) => await ScanNetworkDevicesAsync();
            _deviceScanTimer.AutoReset = true;
            _deviceScanTimer.Start();

            // Start predictive analysis (every 5 minutes)
            _predictionTimer = new Timer(300 * 1000);
            _predictionTimer.Elapsed += async (sender, e) => await RunPredictiveAnalysisAsync();
            _predictionTimer.AutoReset = true;
            _predictionTimer.Start();

            // Collect initial metrics immediately
            Task.Run(async () =>
            {
                await CollectAndAnalyzeMetricsAsync();
                await ScanNetworkDevicesAsync();
            });
        }

        /// <summary>
        /// Stops monitoring
        /// </summary>
        public void StopMonitoring()
        {
            _isRunning = false;
            _monitoringTimer?.Stop();
            _speedTestTimer?.Stop();
            _deviceScanTimer?.Stop();
            _predictionTimer?.Stop();
            _monitoringTimer?.Dispose();
            _speedTestTimer?.Dispose();
            _deviceScanTimer?.Dispose();
            _predictionTimer?.Dispose();
        }

        /// <summary>
        /// Collects current metrics and performs analysis
        /// </summary>
        private async Task CollectAndAnalyzeMetricsAsync()
        {
            try
            {
                var metrics = await _wifiMonitor.CollectMetricsAsync();
                if (metrics == null)
                    return;

                CurrentMetrics = metrics;

                // Save to database
                await _database.SaveMetricsAsync(metrics);

                // Raise event for UI update
                MetricsUpdated?.Invoke(this, metrics);

                // Analyze and generate alerts
                var history = await _database.GetRecentMetricsAsync(20);
                var alerts = await _alertEngine.AnalyzeMetricsAsync(metrics, history);

                foreach (var alert in alerts)
                {
                    await _database.SaveAlertAsync(alert);
                    AlertGenerated?.Invoke(this, alert);
                }

                // Check for channel optimization opportunity (every 10 minutes = 20 metrics at 30sec intervals)
                if (history.Count >= 20)
                {
                    var recommendation = await _channelOptimizer.GetChannelRecommendationAsync();
                    if (recommendation != null && recommendation.ImprovementPercent > 15)
                    {
                        ChannelRecommendationAvailable?.Invoke(this, recommendation);

                        var alert = new Alert(
                            "Channel Optimization Available",
                            $"Switching to channel {recommendation.RecommendedChannel} could improve performance by {recommendation.ImprovementPercent}%. {recommendation.Reason}",
                            AlertType.Recommendation,
                            AlertSeverity.Info
                        );
                        await _database.SaveAlertAsync(alert);
                        AlertGenerated?.Invoke(this, alert);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error collecting metrics: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs a speed test
        /// </summary>
        public async Task RunSpeedTestAsync()
        {
            try
            {
                Console.WriteLine("[MonitoringService] Starting speed test...");
                var result = await _speedTest.RunSpeedTestAsync();
                if (result != null)
                {
                    Console.WriteLine($"[MonitoringService] Speed test completed: {result.DownloadMbps:F2} Mbps down, {result.UploadMbps:F2} Mbps up");
                    await _database.SaveSpeedTestAsync(result);
                    Console.WriteLine("[MonitoringService] Speed test saved to database");
                    SpeedTestCompleted?.Invoke(this, result);
                }
                else
                {
                    Console.WriteLine("[MonitoringService] Speed test returned null result");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error running speed test: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets unacknowledged alerts
        /// </summary>
        public async Task<System.Collections.Generic.List<Alert>> GetUnacknowledgedAlertsAsync()
        {
            return await _database.GetUnacknowledgedAlertsAsync();
        }

        /// <summary>
        /// Gets recent metrics history
        /// </summary>
        public async Task<System.Collections.Generic.List<NetworkMetrics>> GetRecentMetricsAsync(int count = 100)
        {
            return await _database.GetRecentMetricsAsync(count);
        }

        /// <summary>
        /// Scans for devices on the network
        /// </summary>
        private async Task ScanNetworkDevicesAsync()
        {
            try
            {
                var devices = await _deviceTracking.ScanDevicesAsync();

                // Check for new devices
                var newDevices = _deviceTracking.GetNewDevices(TimeSpan.FromMinutes(5));
                if (newDevices.Any())
                {
                    foreach (var device in newDevices)
                    {
                        var alert = new Alert(
                            "New Device Detected",
                            $"A new device '{device.DisplayName}' ({device.IpAddress}) has connected to your network. " +
                            $"Vendor: {device.Vendor}. If you don't recognize this device, check your router security.",
                            AlertType.NewDevice,
                            AlertSeverity.Medium
                        );
                        await _database.SaveAlertAsync(alert);
                        AlertGenerated?.Invoke(this, alert);
                    }
                }

                DevicesUpdated?.Invoke(this, devices);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning devices: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs predictive analysis
        /// </summary>
        private async Task RunPredictiveAnalysisAsync()
        {
            try
            {
                var predictions = await _predictive.PredictIssuesAsync();

                foreach (var prediction in predictions)
                {
                    PredictionGenerated?.Invoke(this, prediction);

                    // Generate alert for high-confidence predictions
                    if (prediction.Confidence >= 60)
                    {
                        var alert = new Alert(
                            prediction.Title,
                            $"{prediction.Message} (Confidence: {prediction.Confidence}%)",
                            AlertType.Warning,
                            prediction.Severity
                        );
                        await _database.SaveAlertAsync(alert);
                        AlertGenerated?.Invoke(this, alert);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error running predictions: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all tracked network devices
        /// </summary>
        public System.Collections.Generic.List<NetworkDevice> GetNetworkDevices()
        {
            return _deviceTracking.GetAllDevices();
        }

        /// <summary>
        /// Gets best download times analysis
        /// </summary>
        public async Task<BestTimeAnalysis?> GetBestDownloadTimesAsync()
        {
            return await _analytics.GetBestDownloadTimesAsync();
        }

        /// <summary>
        /// Gets ISP performance analysis
        /// </summary>
        public async Task<IspAnalysis?> GetIspAnalysisAsync()
        {
            return await _analytics.AnalyzeIspPerformanceAsync();
        }

        /// <summary>
        /// Gets network stability metrics
        /// </summary>
        public async Task<NetworkStability> GetNetworkStabilityAsync(TimeSpan period)
        {
            return await _analytics.GetNetworkStabilityAsync(period);
        }

        /// <summary>
        /// Gets channel optimization recommendation
        /// </summary>
        public async Task<ChannelRecommendation?> GetChannelRecommendationAsync()
        {
            return await _channelOptimizer.GetChannelRecommendationAsync();
        }

        /// <summary>
        /// Marks a device as trusted
        /// </summary>
        public void SetDeviceTrust(string macAddress, bool trusted)
        {
            _deviceTracking.SetDeviceTrust(macAddress, trusted);
        }

        /// <summary>
        /// Gets prediction engine status (Statistical vs ML.NET)
        /// </summary>
        public async Task<PredictionEngineStatus> GetPredictionEngineStatusAsync()
        {
            return await _predictive.GetEngineStatusAsync();
        }
    }
}
