using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Controls;

public partial class SpeedTestControl : UserControl
{
    private bool _isTestRunning = false;
    private const double MaxDownloadSpeed = 1000.0; // Mbps for gauge scale
    private const double MaxUploadSpeed = 500.0;
    private const double MaxPing = 200.0; // ms

    public event EventHandler? TestStartRequested;
    public event EventHandler? TestCancelled;
    public event EventHandler? Closed;

    public SpeedTestControl()
    {
        InitializeComponent();

        CloseButton.Click += (s, e) =>
        {
            Hide();
            Closed?.Invoke(this, EventArgs.Empty);
        };

        StartTestButton.Click += (s, e) =>
        {
            if (!_isTestRunning)
            {
                StartTest();
            }
            else
            {
                CancelTest();
            }
        };

        Reset();
    }

    public void Show()
    {
        SpeedTestOverlay.Visibility = Visibility.Visible;
        // Don't reset here - we want to preserve previous results
        // Reset will happen when user clicks START TEST
    }

    public void Hide()
    {
        SpeedTestOverlay.Visibility = Visibility.Collapsed;
        if (_isTestRunning)
        {
            CancelTest();
        }
    }

    private void StartTest()
    {
        // Clear previous results before starting new test
        Reset();

        _isTestRunning = true;
        StartTestButton.Content = "STOP TEST";
        TestStatusText.Text = "Testing your connection speed...";

        // Enable glow effects
        AnimateGlow(DownloadGlow, true);
        AnimateGlow(UploadGlow, true);
        AnimateGlow(PingGlow, true);

        TestStartRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CancelTest()
    {
        _isTestRunning = false;
        StartTestButton.Content = "START TEST";
        TestStatusText.Text = "Test cancelled";

        // Disable glow effects
        AnimateGlow(DownloadGlow, false);
        AnimateGlow(UploadGlow, false);
        AnimateGlow(PingGlow, false);

        TestCancelled?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateProgress(double downloadMbps, double uploadMbps, double pingMs, string phase)
    {
        Dispatcher.Invoke(() =>
        {
            // Update status
            TestStatusText.Text = phase switch
            {
                "download" => "Measuring download speed...",
                "upload" => "Measuring upload speed...",
                "ping" => "Measuring latency...",
                _ => "Testing your connection speed..."
            };

            // Animate download gauge
            AnimateValue(DownloadSpeedValue, downloadMbps);
            UpdateArc(DownloadArcSegment, DownloadArcFigure, downloadMbps, MaxDownloadSpeed);

            // Animate upload gauge
            AnimateValue(UploadSpeedValue, uploadMbps);
            UpdateArc(UploadArcSegment, UploadArcFigure, uploadMbps, MaxUploadSpeed);

            // Animate ping gauge
            AnimateValue(PingValue, pingMs);
            UpdateArc(PingArcSegment, PingArcFigure, pingMs, MaxPing);

            // Pulse active gauge
            switch (phase)
            {
                case "download":
                    AnimateGlow(DownloadGlow, true);
                    AnimateGlow(UploadGlow, false);
                    AnimateGlow(PingGlow, false);
                    break;
                case "upload":
                    AnimateGlow(DownloadGlow, false);
                    AnimateGlow(UploadGlow, true);
                    AnimateGlow(PingGlow, false);
                    break;
                case "ping":
                    AnimateGlow(DownloadGlow, false);
                    AnimateGlow(UploadGlow, false);
                    AnimateGlow(PingGlow, true);
                    break;
            }
        });
    }

    public void ShowResults(SpeedTestResult result)
    {
        Dispatcher.Invoke(() =>
        {
            _isTestRunning = false;
            StartTestButton.Content = "TEST AGAIN";
            TestStatusText.Text = "Test completed successfully!";

            // Show final values with animation
            AnimateValue(DownloadSpeedValue, result.DownloadMbps);
            UpdateArc(DownloadArcSegment, DownloadArcFigure, result.DownloadMbps, MaxDownloadSpeed);

            AnimateValue(UploadSpeedValue, result.UploadMbps);
            UpdateArc(UploadArcSegment, UploadArcFigure, result.UploadMbps, MaxUploadSpeed);

            AnimateValue(PingValue, result.LatencyMs);
            UpdateArc(PingArcSegment, PingArcFigure, result.LatencyMs, MaxPing);

            // Update ISP info
            IspNameText.Text = result.Isp;
            IpAddressText.Text = result.Server != null ?
                $"Server: {result.Server.Location}, {result.Server.Country}" :
                "Speed test completed";

            // Disable all glows
            AnimateGlow(DownloadGlow, false);
            AnimateGlow(UploadGlow, false);
            AnimateGlow(PingGlow, false);

            // Flash all gauges briefly to celebrate completion
            FlashCompletion();
        });
    }

    public void ShowError(string message)
    {
        Dispatcher.Invoke(() =>
        {
            _isTestRunning = false;
            StartTestButton.Content = "TRY AGAIN";
            TestStatusText.Text = $"Error: {message}";

            AnimateGlow(DownloadGlow, false);
            AnimateGlow(UploadGlow, false);
            AnimateGlow(PingGlow, false);
        });
    }

    private void Reset()
    {
        _isTestRunning = false;
        StartTestButton.Content = "START TEST";
        TestStatusText.Text = "Ready to test your connection";

        DownloadSpeedValue.Text = "0";
        UploadSpeedValue.Text = "0";
        PingValue.Text = "0";

        IspNameText.Text = "--";
        IpAddressText.Text = "Detecting network...";

        // Reset arcs
        UpdateArc(DownloadArcSegment, DownloadArcFigure, 0, MaxDownloadSpeed);
        UpdateArc(UploadArcSegment, UploadArcFigure, 0, MaxUploadSpeed);
        UpdateArc(PingArcSegment, PingArcFigure, 0, MaxPing);

        // Disable glows
        DownloadGlow.Opacity = 0;
        UploadGlow.Opacity = 0;
        PingGlow.Opacity = 0;
    }

    private void AnimateValue(TextBlock textBlock, double targetValue)
    {
        // Simply update the text - smooth enough for real-time updates
        textBlock.Text = targetValue.ToString("F1");
    }

    private void UpdateArc(ArcSegment arc, PathFigure figure, double value, double maxValue)
    {
        var percentage = Math.Min(value / maxValue, 1.0);
        var angle = percentage * 360;
        var radians = (angle - 90) * Math.PI / 180;

        var centerX = 90;
        var centerY = 90;
        var radius = 84;

        var endX = centerX + radius * Math.Cos(radians);
        var endY = centerY + radius * Math.Sin(radians);

        var isLargeArc = angle > 180;

        var animation = new PointAnimation
        {
            To = new Point(endX, endY),
            Duration = TimeSpan.FromMilliseconds(800),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        arc.BeginAnimation(ArcSegment.PointProperty, animation);
        arc.IsLargeArc = isLargeArc;
    }

    private void AnimateGlow(UIElement element, bool enable)
    {
        var animation = new DoubleAnimation
        {
            To = enable ? 0.6 : 0,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        if (enable)
        {
            animation.AutoReverse = true;
            animation.RepeatBehavior = RepeatBehavior.Forever;
        }
        else
        {
            animation.RepeatBehavior = new RepeatBehavior(1);
        }

        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private void FlashCompletion()
    {
        var flashAnimation = new DoubleAnimation
        {
            From = 0,
            To = 0.8,
            Duration = TimeSpan.FromMilliseconds(200),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2)
        };

        DownloadGlow.BeginAnimation(UIElement.OpacityProperty, flashAnimation);

        var storyboard = new Storyboard();
        storyboard.BeginTime = TimeSpan.FromMilliseconds(100);
        storyboard.Children.Add(flashAnimation);
        UploadGlow.BeginAnimation(UIElement.OpacityProperty, flashAnimation);

        var storyboard2 = new Storyboard();
        storyboard2.BeginTime = TimeSpan.FromMilliseconds(200);
        storyboard2.Children.Add(flashAnimation);
        PingGlow.BeginAnimation(UIElement.OpacityProperty, flashAnimation);
    }
}
