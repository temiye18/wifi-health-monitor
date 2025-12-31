using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WiFiHealthMonitor.Models;
using WiFiHealthMonitor.Services;

namespace WiFiHealthMonitor;

/// <summary>
/// Interaction logic for enhanced MainWindow with full feature support
/// </summary>
public partial class MainWindow : Window
{
    private readonly MonitoringService _monitoringService;
    private ObservableCollection<Alert> _alerts;
    private ObservableCollection<NetworkDevice> _devices;
    private ObservableCollection<Prediction> _predictions;
    private SpeedTestResult? _lastSpeedTestResult; // In-memory storage for current session

    public MainWindow()
    {
        InitializeComponent();

        _monitoringService = new MonitoringService();
        _alerts = new ObservableCollection<Alert>();
        _devices = new ObservableCollection<NetworkDevice>();
        _predictions = new ObservableCollection<Prediction>();

        AlertsListBox.ItemsSource = _alerts;
        DevicesListBox.ItemsSource = _devices;
        PredictionsListBox.ItemsSource = _predictions;

        // Wire up speed test control events
        SpeedTestOverlayControl.TestStartRequested += async (s, e) => await RunSpeedTestAsync();
        SpeedTestOverlayControl.TestCancelled += (s, e) => _monitoringService.StopMonitoring();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ProgressBar.Visibility = Visibility.Visible;

            // Initialize services
            await _monitoringService.InitializeAsync();

            // Subscribe to all events
            _monitoringService.MetricsUpdated += OnMetricsUpdated;
            _monitoringService.AlertGenerated += OnAlertGenerated;
            _monitoringService.SpeedTestCompleted += OnSpeedTestCompleted;
            _monitoringService.DevicesUpdated += OnDevicesUpdated;
            _monitoringService.PredictionGenerated += OnPredictionGenerated;
            _monitoringService.ChannelRecommendationAvailable += OnChannelRecommendation;

            // Start monitoring
            _monitoringService.StartMonitoring(30);

            // Load analytics
            await LoadAnalyticsAsync();

            // Initialize predictions UI
            UpdatePredictionsUI();

            // StatusText.Text = "Monitoring active - All systems operational";
            ProgressBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            // StatusText.Text = "Error - Click Refresh to retry";
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _monitoringService.StopMonitoring();
    }

    private void OnMetricsUpdated(object? sender, NetworkMetrics metrics)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateDashboard(metrics);
        });
    }

    private void OnAlertGenerated(object? sender, Alert alert)
    {
        Dispatcher.Invoke(() =>
        {
            _alerts.Insert(0, alert);
            if (_alerts.Count > 15)
            {
                _alerts.RemoveAt(_alerts.Count - 1);
            }
            NoAlertsText.Visibility = _alerts.Any() ? Visibility.Collapsed : Visibility.Visible;
        });
    }

    private void OnSpeedTestCompleted(object? sender, SpeedTestResult result)
    {
        Dispatcher.Invoke(() =>
        {
            // Store result in memory for current session
            _lastSpeedTestResult = result;

            // Update Internet Speed tile on dashboard
            UpdateInternetSpeedTile(result);

            // Show results in the beautiful speed test UI
            SpeedTestOverlayControl.ShowResults(result);

            RunSpeedTestButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;

            // Reload analytics
            _ = LoadAnalyticsAsync();
        });
    }

    private void UpdateInternetSpeedTile(SpeedTestResult result)
    {
        InternetSpeedText.Text = $"{result.DownloadMbps:F1} Mbps";
        InternetSpeedSubtitle.Text = $"Tested {DateTime.Now:HH:mm}";
    }

    private void OnDevicesUpdated(object? sender, System.Collections.Generic.List<NetworkDevice> devices)
    {
        Dispatcher.Invoke(() =>
        {
            _devices.Clear();
            foreach (var device in devices.OrderBy(d => d.IpAddress))
            {
                _devices.Add(device);
            }

            // Update device count in header tile
            DeviceCountText.Text = $"Devices: {devices.Count}";

            // Update devices page UI
            DeviceCountBadge.Text = devices.Count.ToString();
            LastScanText.Text = $"Last scan: {DateTime.Now:HH:mm:ss}";

            // Show/hide empty state
            if (devices.Count == 0)
            {
                NoDevicesPanel.Visibility = Visibility.Visible;
                DevicesListBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoDevicesPanel.Visibility = Visibility.Collapsed;
                DevicesListBox.Visibility = Visibility.Visible;
            }
        });
    }

    private void OnPredictionGenerated(object? sender, Prediction prediction)
    {
        Dispatcher.Invoke(() =>
        {
            _predictions.Insert(0, prediction);
            if (_predictions.Count > 10)
            {
                _predictions.RemoveAt(_predictions.Count - 1);
            }

            // Show/hide appropriate UI based on predictions
            UpdatePredictionsUI();
        });
    }

    private void UpdatePredictionsUI()
    {
        if (_predictions.Any())
        {
            // Show predictions list, hide empty state
            ActivePredictionsCard.Visibility = Visibility.Visible;
            PredictionsEmptyState.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Show empty state with status info, hide predictions list
            ActivePredictionsCard.Visibility = Visibility.Collapsed;
            PredictionsEmptyState.Visibility = Visibility.Visible;

            // Load prediction engine status
            _ = LoadPredictionStatusAsync();
        }
    }

    private async System.Threading.Tasks.Task LoadPredictionStatusAsync()
    {
        try
        {
            var status = await _monitoringService.GetPredictionEngineStatusAsync();
            if (status != null)
            {
                // Update engine type
                PredictionEngineText.Text = status.CurrentEngine == Services.PredictionEngine.MLNet
                    ? "ML.NET Machine Learning"
                    : "Statistical Analysis";

                // Update data collection progress
                var dataCollected = status.DataPointsCollected;
                var dataNeeded = status.DataPointsCollected + status.DataPointsNeededForML;

                // For initial predictions, need 100 points minimum
                var minForPredictions = 100;
                var progressValue = Math.Min(dataCollected, minForPredictions);

                DataPointsText.Text = $"{dataCollected} / {minForPredictions} data points";

                // Animate progress bar
                var maxWidth = 300.0; // Approximate max width
                var targetWidth = (progressValue / (double)minForPredictions) * maxWidth;
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(1000),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };
                DataProgressBar.BeginAnimation(System.Windows.Controls.Border.WidthProperty, animation);

                // Update accuracy
                AccuracyText.Text = status.ExpectedAccuracy;

                // Update time to predictions
                if (dataCollected >= 100)
                {
                    TimeToPredicationsText.Text = "Ready - Checking every 5 minutes";
                    TimeToPredicationsText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")!);
                }
                else
                {
                    var pointsNeeded = 100 - dataCollected;
                    var minutesNeeded = (pointsNeeded * 0.5); // 30 second intervals = 0.5 min per point

                    if (minutesNeeded < 60)
                        TimeToPredicationsText.Text = $"~{(int)minutesNeeded} minutes";
                    else
                        TimeToPredicationsText.Text = $"~{minutesNeeded / 60:F1} hours";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading prediction status: {ex.Message}");
        }
    }

    private void OnChannelRecommendation(object? sender, ChannelRecommendation recommendation)
    {
        Dispatcher.Invoke(() =>
        {
            // ChannelRecommendationText.Text =
            //     $"Current Channel: {recommendation.CurrentChannel} (Congestion: {recommendation.CurrentCongestion}%)\n\n" +
            //     $"Recommended: Channel {recommendation.RecommendedChannel} (Congestion: {recommendation.RecommendedCongestion}%)\n\n" +
            //     $"Improvement: {recommendation.ImprovementPercent}%\n\n" +
            //     $"{recommendation.Reason}";
        });
    }

    private void UpdateDashboard(NetworkMetrics metrics)
    {
        // Update main stats
        SignalText.Text = $"{metrics.SignalPercent}%";
        SignalStatusText.Text = $"({metrics.Rssi} dBm)";

        DownloadSpeedText.Text = $"{metrics.ReceiveSpeedMbps:F1} Mbps";
        // UploadSpeedText.Text = $"{metrics.TransmitSpeedMbps:F1} Mbps";

        HealthScoreText.Text = $"{metrics.HealthScore}/100";
        HealthStatusText.Text = metrics.HealthStatus;

        // Update current network name on Devices page
        CurrentNetworkName.Text = !string.IsNullOrEmpty(metrics.Ssid) ? metrics.Ssid : "Unknown Network";

        // Color code health score
        if (metrics.HealthScore >= 80)
            HealthScoreText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")!);
        else if (metrics.HealthScore >= 60)
            HealthScoreText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")!);
        else
            HealthScoreText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")!);

        // Update details
        SsidText.Text = metrics.Ssid;
        BandText.Text = $"{metrics.Band} (Channel {metrics.Channel})";
        ChannelText.Text = metrics.Channel.ToString();
        RadioTypeText.Text = metrics.RadioType;
        AuthText.Text = metrics.Authentication;
        RssiText.Text = $"{metrics.Rssi} dBm";

        LastUpdateText.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";

        // Update stability asynchronously
        _ = UpdateStabilityAsync();
    }

    private async System.Threading.Tasks.Task UpdateStabilityAsync()
    {
        try
        {
            // Stability data is now displayed in Analytics page
            // This method can be removed or kept as placeholder for future use
            await System.Threading.Tasks.Task.CompletedTask;
        }
        catch { }
    }

    private async System.Threading.Tasks.Task LoadAnalyticsAsync()
    {
        try
        {
            // Load ISP analysis
            Console.WriteLine("[MainWindow] LoadAnalyticsAsync: Starting...");
            var ispAnalysis = await _monitoringService.GetIspAnalysisAsync();
            Console.WriteLine($"[MainWindow] LoadAnalyticsAsync: ISP Analysis = {(ispAnalysis == null ? "NULL" : "HAS DATA")}");
            if (ispAnalysis != null)
            {
                Console.WriteLine($"[MainWindow] LoadAnalyticsAsync: Total Tests = {ispAnalysis.TotalTests}");
                // Populate hero metrics
                AnalyticsAvgSpeed.Text = $"{ispAnalysis.AverageDownloadSpeed:F1} Mbps";
                AnalyticsTotalTests.Text = ispAnalysis.TotalTests.ToString();

                // Populate ISP performance metrics
                IspAvgDownload.Text = $"{ispAnalysis.AverageDownloadSpeed:F2} Mbps";
                IspAvgUpload.Text = $"{ispAnalysis.AverageUploadSpeed:F2} Mbps";
                IspAvgLatency.Text = $"{ispAnalysis.AverageLatency:F1} ms";

                // Speed degradation with color-coded badge
                IspDegradation.Text = $"{ispAnalysis.SpeedDegradation:F1}%";
                if (ispAnalysis.SpeedDegradation < 10)
                {
                    IspDegradationBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")!); // Green
                    ((System.Windows.Controls.TextBlock)IspDegradationBadge.Child).Text = "EXCELLENT";
                }
                else if (ispAnalysis.SpeedDegradation < 20)
                {
                    IspDegradationBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")!); // Accent Green
                    ((System.Windows.Controls.TextBlock)IspDegradationBadge.Child).Text = "GOOD";
                }
                else if (ispAnalysis.SpeedDegradation < 30)
                {
                    IspDegradationBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")!); // Warning
                    ((System.Windows.Controls.TextBlock)IspDegradationBadge.Child).Text = "FAIR";
                }
                else
                {
                    IspDegradationBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")!); // Error
                    ((System.Windows.Controls.TextBlock)IspDegradationBadge.Child).Text = "POOR";
                }

                // Throttling status
                IspThrottling.Text = ispAnalysis.PossibleThrottling ? "⚠ YES" : "✓ NO";
                IspThrottling.Foreground = ispAnalysis.PossibleThrottling ?
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")!) :
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")!);

                // Recommendation
                IspRecommendation.Text = ispAnalysis.Recommendation;
            }
            else
            {
                // Show empty state message
                AnalyticsAvgSpeed.Text = "--";
                AnalyticsTotalTests.Text = "0";
                IspAvgDownload.Text = "No data";
                IspAvgUpload.Text = "No data";
                IspAvgLatency.Text = "No data";
                IspDegradation.Text = "--";
                IspDegradationBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")!);
                ((System.Windows.Controls.TextBlock)IspDegradationBadge.Child).Text = "N/A";
                IspThrottling.Text = "No data";
                IspRecommendation.Text = "Run at least 2 speed tests from the Overview page to see ISP performance analysis. Click the 'Run Speed Test' button to get started.";
            }

            // Load best download times
            var bestTimes = await _monitoringService.GetBestDownloadTimesAsync();
            if (bestTimes != null)
            {
                var bestHoursText = string.Join(", ", bestTimes.BestHours.Select(h => $"{h.Hour}:00"));
                var worstHoursText = string.Join(", ", bestTimes.WorstHours.Select(h => $"{h.Hour}:00"));

                BestHoursText.Text = !string.IsNullOrEmpty(bestHoursText) ? bestHoursText : "No data yet";
                WorstHoursText.Text = !string.IsNullOrEmpty(worstHoursText) ? worstHoursText : "No data yet";
                TimesRecommendation.Text = bestTimes.Recommendation;
            }
            else
            {
                BestHoursText.Text = "No data available";
                WorstHoursText.Text = "No data available";
                TimesRecommendation.Text = "Run multiple speed tests throughout the day to identify the best times for downloads.";
            }

            // Load network stability
            var stability = await _monitoringService.GetNetworkStabilityAsync(TimeSpan.FromHours(24));
            if (stability != null && !stability.InsufficientData)
            {
                // Populate hero stability score
                AnalyticsStabilityScore.Text = $"{(int)stability.StabilityScore}/100";

                // Populate stability metrics
                StabilityAvgSignal.Text = $"{stability.AverageSignal:F1}%";
                StabilityAvgSpeed.Text = $"{stability.AverageSpeed:F1} Mbps";
                StabilityDrops.Text = stability.SignificantDrops.ToString();

                // Update stability progress bars with animation
                SignalStabilityValue.Text = $"{stability.SignalStability:F0}";
                SpeedStabilityValue.Text = $"{stability.SpeedStability:F0}";

                AnimateProgressBar(SignalStabilityBar, stability.SignalStability);
                AnimateProgressBar(SpeedStabilityBar, stability.SpeedStability);

                // Update status banner
                if (stability.IsStable)
                {
                    StabilityStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")!);
                    StabilityStatusIcon.Text = "✓";
                    StabilityStatusText.Text = "Network is stable and performing well";
                }
                else
                {
                    StabilityStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")!);
                    StabilityStatusIcon.Text = "⚠";
                    StabilityStatusText.Text = "Network shows signs of instability";
                }
            }
            else
            {
                // Show empty state for stability
                AnalyticsStabilityScore.Text = "--";
                StabilityAvgSignal.Text = "Collecting...";
                StabilityAvgSpeed.Text = "Collecting...";
                StabilityDrops.Text = "--";
                SignalStabilityValue.Text = "--";
                SpeedStabilityValue.Text = "--";

                StabilityStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")!);
                StabilityStatusIcon.Text = "⏳";
                StabilityStatusText.Text = "Collecting data for 24-hour stability analysis...";
            }

            // Load channel recommendation
            var channelRec = await _monitoringService.GetChannelRecommendationAsync();
            if (channelRec != null)
            {
                OnChannelRecommendation(this, channelRec);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] Error loading analytics: {ex.Message}");
            Console.WriteLine($"[MainWindow] Stack trace: {ex.StackTrace}");
            // Still set empty states even if there's an error
            try
            {
                AnalyticsAvgSpeed.Text = "--";
                AnalyticsTotalTests.Text = "0";
                AnalyticsStabilityScore.Text = "--";
                IspRecommendation.Text = "Run at least 2 speed tests from the Overview page to see ISP performance analysis. Click the 'Run Speed Test' button to get started.";
                TimesRecommendation.Text = "Run multiple speed tests throughout the day to identify the best times for downloads.";
            }
            catch { }
        }
    }

    private void AnimateProgressBar(System.Windows.Controls.Border progressBar, double targetValue)
    {
        // Calculate target width as percentage (0-100)
        var percentage = Math.Min(Math.Max(targetValue, 0), 100);
        var targetWidth = (progressBar.Parent as System.Windows.Controls.Border)!.ActualWidth * (percentage / 100);

        var animation = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(1000),
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };

        progressBar.BeginAnimation(System.Windows.Controls.Border.WidthProperty, animation);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // StatusText.Text = "Refreshing...";
            ProgressBar.Visibility = Visibility.Visible;

            var metrics = await _monitoringService.CollectMetricsAsync();
            if (metrics != null)
            {
                UpdateDashboard(metrics);
            }

            await LoadAnalyticsAsync();

            // StatusText.Text = "Monitoring active";
            ProgressBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error refreshing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            // StatusText.Text = "Error";
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void RunSpeedTestButton_Click(object sender, RoutedEventArgs e)
    {
        // Show the beautiful speed test overlay
        SpeedTestOverlayControl.Show();

        // If we have a previous result from this session, show it
        if (_lastSpeedTestResult != null)
        {
            SpeedTestOverlayControl.ShowResults(_lastSpeedTestResult);
        }
    }

    private async System.Threading.Tasks.Task RunSpeedTestAsync()
    {
        try
        {
            RunSpeedTestButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;

            // Simulate progress updates (in real implementation, MonitoringService would report progress)
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            var phase = "ping";
            var progress = 0.0;

            timer.Tick += (s, e) =>
            {
                progress += 5;
                if (progress > 100) progress = 20; // Keep animating

                if (progress < 30)
                    phase = "ping";
                else if (progress < 70)
                    phase = "download";
                else
                    phase = "upload";

                // Update the UI with simulated progress
                SpeedTestOverlayControl.UpdateProgress(
                    downloadMbps: phase == "download" ? progress * 2 : 0,
                    uploadMbps: phase == "upload" ? progress : 0,
                    pingMs: phase == "ping" ? 100 - progress : 0,
                    phase: phase
                );
            };

            timer.Start();

            // Run the actual test
            await _monitoringService.RunSpeedTestAsync();

            timer.Stop();
        }
        catch (Exception ex)
        {
            SpeedTestOverlayControl.ShowError(ex.Message);
            RunSpeedTestButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void ScanDevices_Click(object sender, RoutedEventArgs e)
    {
        ProgressBar.Visibility = Visibility.Visible;

        // Trigger immediate scan (will fire DevicesUpdated event)
        await System.Threading.Tasks.Task.Run(() =>
        {
            _monitoringService.GetNetworkDevices();
        });

        ProgressBar.Visibility = Visibility.Collapsed;
    }

    #region Modern UI Navigation

    private void Navigation_Changed(object sender, RoutedEventArgs e)
    {
        // Guard against calls during initialization
        if (OverviewPage == null) return;

        // Hide all pages
        OverviewPage.Visibility = Visibility.Collapsed;
        AnalyticsPage.Visibility = Visibility.Collapsed;
        DevicesPage.Visibility = Visibility.Collapsed;
        PredictionsPage.Visibility = Visibility.Collapsed;

        // Show selected page and update title
        if (NavOverview.IsChecked == true)
        {
            OverviewPage.Visibility = Visibility.Visible;
            PageTitle.Text = "Overview";
            PageSubtitle.Text = "Real-time network monitoring and health insights";
        }
        else if (NavAnalytics.IsChecked == true)
        {
            AnalyticsPage.Visibility = Visibility.Visible;
            PageTitle.Text = "Analytics";
            PageSubtitle.Text = "Historical analysis and performance insights";
            // Refresh analytics data when navigating to page
            _ = LoadAnalyticsAsync();
        }
        else if (NavDevices.IsChecked == true)
        {
            DevicesPage.Visibility = Visibility.Visible;
            PageTitle.Text = "Devices";
            PageSubtitle.Text = "Connected devices on your network";
        }
        else if (NavPredictions.IsChecked == true)
        {
            PredictionsPage.Visibility = Visibility.Visible;
            PageTitle.Text = "Predictions";
            PageSubtitle.Text = "AI-powered network forecasting";
            // Refresh predictions status when navigating to page
            UpdatePredictionsUI();
        }
    }

    #endregion

    #region Window Controls

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion
}

// Extension methods for MonitoringService
public static class MonitoringServiceExtensions
{
    public static async System.Threading.Tasks.Task<NetworkMetrics?> CollectMetricsAsync(this MonitoringService service)
    {
        var wifiMonitor = new WiFiMonitorService();
        return await wifiMonitor.CollectMetricsAsync();
    }
}
