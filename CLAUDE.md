# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

### Development
```bash
cd C:\Users\xps\Documents\Projects\YEAR_2025\VIBE_CODE\WiFiHealthMonitor
dotnet run
# Watch console for diagnostic messages: [MainWindow], [Analytics], [Database], [MonitoringService], [SpeedTest]
```

### Build and Test
```bash
# Build (debug)
dotnet build

# Build (release)
dotnet build --configuration Release

# Run release executable
.\bin\Release\net8.0-windows\WiFiHealthMonitor.exe
```

### Clean and Rebuild
```bash
# Delete database to start fresh
rmdir /s %APPDATA%\WiFiHealthMonitor

# Clean build artifacts
dotnet clean

# Rebuild
dotnet build
```

### Distribution Build (Single-File Executable)
```bash
# Publish as self-contained single-file executable for distribution
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# Output: bin/Release/net8.0-windows/win-x64/publish/WiFiHealthMonitor.exe (~225 MB)

# Create distributable ZIP for website downloads
cd bin/Release/net8.0-windows/win-x64/publish
tar -a -cf WiFiHealthMonitor-v2.0.0-win-x64.zip WiFiHealthMonitor.exe cs/ de/ es/ fr/ it/ ja/ ko/ pl/ pt-BR/ ru/ tr/ zh-Hans/ zh-Hant/

# Result: Single ZIP file ready for users (no .NET required on their machine)
```

## CRITICAL: Documentation Update Rule

**MANDATORY**: Whenever you make changes to the codebase that affect functionality, architecture, or user-facing features, you MUST update the relevant documentation files:

### When to Update Documentation

Update **README.md** when:
- Adding or modifying user-facing features
- Changing how users interact with the application
- Adding new pages or UI elements
- Modifying data requirements for Analytics or Predictions
- Changing installation or usage instructions
- Adding new dependencies or system requirements
- Updating FAQ items or troubleshooting guidance

Update **ARCHITECTURE.md** when:
- Adding new services or components
- Modifying service dependencies or architecture
- Changing data flow patterns
- Adding new database tables or schema changes
- Modifying prediction engine logic or ML.NET integration
- Adding new API methods to services
- Changing critical implementation details
- Adding new extensibility points

Update **CLAUDE.md** (this file) when:
- Adding new development patterns or best practices
- Discovering new "gotchas" or critical rules
- Changing build/run commands
- Modifying the service layer architecture
- Adding new common development tasks

### Documentation Update Checklist

Before completing any task, verify:
- [ ] Are there new features users should know about? → Update README.md
- [ ] Did the architecture or data flow change? → Update ARCHITECTURE.md
- [ ] Did you discover new patterns or gotchas? → Update CLAUDE.md
- [ ] Are version numbers current in all files? (currently v2.0.0)
- [ ] Are code examples in documentation still accurate?

**Example**: If you add a new analytics feature:
1. Code the feature in `AnalyticsService.cs`
2. Update README.md "Features" section and usage instructions
3. Update ARCHITECTURE.md with new data flow and API example
4. Update CLAUDE.md if new patterns were established

**Failure to update documentation makes future maintenance significantly harder.**

## High-Level Architecture

### Multi-Page WPF Application
This is a Windows desktop app with **3-page tabbed navigation**:
1. **Overview Page** - Real-time monitoring dashboard (default)
2. **Analytics Page** - Historical data analysis (ISP, Best Times, Stability)
3. **Predictions Page** - AI-powered forecasting (Hybrid Statistical/ML.NET)

Page switching uses RadioButtons with Visibility binding - NOT separate windows or frames.

### Service Layer Architecture
The app follows a strict service-oriented pattern with a central orchestrator:

```
MainWindow (UI)
    └─> MonitoringService (Orchestrator)
         ├─> WiFiMonitorService (Windows API data collection)
         ├─> SpeedTestService (Ookla CLI integration)
         ├─> AlertEngine (Intelligence & recommendations)
         ├─> AnalyticsService (Historical analysis)
         ├─> HybridPredictiveService (AI predictions)
         │    ├─> StatisticalPredictiveService (< 500 points)
         │    └─> MLNetPredictiveService (≥ 500 points)
         ├─> DeviceTrackingService (Network device monitoring)
         ├─> ChannelOptimizerService (Channel recommendations)
         └─> DatabaseService (SQLite persistence)
```

**Critical**: All services are instantiated by `MonitoringService` constructor. UI never directly instantiates services except `MonitoringService`.

### Event-Driven UI Updates
Services raise events → MonitoringService propagates → MainWindow handles with `Dispatcher.Invoke()`:
- `MetricsUpdated` - Every 30 seconds
- `AlertGenerated` - When analysis detects issues
- `SpeedTestCompleted` - After speed test finishes
- `DevicesUpdated` - Every 2 minutes
- `PredictionGenerated` - Every 5 minutes

**All event handlers in MainWindow.xaml.cs MUST use Dispatcher.Invoke() for thread safety.**

### Data Flow Pattern
1. Timer fires in MonitoringService
2. Service collects data (WiFi metrics, speed test, etc.)
3. Data saved to SQLite database
4. Analysis runs on fresh data
5. Event raised with results
6. UI updates via Dispatcher.Invoke()

### Analytics Data Requirements
Analytics features have strict minimum data requirements to avoid showing misleading information:

- **ISP Performance**: 2+ speed tests minimum
  - Throttling detection requires comparing recent vs older tests
  - Only shows time patterns when best/worst hours differ AND >15% speed difference

- **Best Download Times**: 100+ metrics, 3+ different hours, 10+ samples per hour
  - Smart logic prevents confusing recommendations ("best at 12 AM, worst at 12 AM")
  - Only shows patterns with >10% variation

- **Network Stability**: 10+ metrics (about 5 minutes)
  - Analyzes last 24 hours of data
  - Calculates signal/speed standard deviations

**Empty states are critical**: When insufficient data, show helpful messages explaining requirements.

### Hybrid Prediction Engine
The prediction system automatically upgrades based on data availability:

```csharp
if (dataPoints < 500)
    Use StatisticalPredictiveService  // 30-75% accuracy, linear regression
else
    Use MLNetPredictiveService        // 60-85% accuracy, ML.NET SSA forecasting
```

**NEVER modify prediction services directly**. The HybridPredictiveService automatically selects and switches engines. To add new prediction types, implement in both Statistical and MLNet services.

## Critical Implementation Patterns

### Thread-Safe UI Updates
```csharp
Dispatcher.Invoke(() =>
{
    YourUIElement.Text = value;
    _observableCollection.Add(item);
});
```

### Diagnostic Logging
Use `Console.WriteLine` (NOT Debug.WriteLine) with service name prefix:
```csharp
Console.WriteLine("[ServiceName] Action description");
Console.WriteLine($"[Analytics] Found {count} speed tests");
```

Prefixes used: `[MainWindow]`, `[MonitoringService]`, `[Database]`, `[Analytics]`, `[SpeedTest]`

### Analytics Page Navigation
When user clicks "Analytics" tab, the page automatically refreshes ALL analytics sections:
```csharp
private async void LoadAnalyticsAsync()
{
    // All three sections refresh on navigation
    await LoadIspAnalysisAsync();
    await LoadBestDownloadTimesAsync();
    await LoadNetworkStabilityAsync();
}
```

### Empty State Handling
```csharp
var analysis = await _monitoringService.GetIspAnalysisAsync();
if (analysis == null)
{
    // Show helpful message about data requirements
    IspStatusText.Text = "Run at least 2 speed tests to see ISP performance analysis";
    return;
}
// Display analysis data
```

### Adding New Analytics Features
1. Add analysis method to `Services/AnalyticsService.cs`:
   ```csharp
   public async Task<YourAnalysis?> GetYourAnalysisAsync()
   {
       var data = await _database.GetYourDataAsync();
       if (data.Count < minimumRequired) return null;
       // Perform analysis
       return new YourAnalysis { ... };
   }
   ```
2. Add public wrapper in `Services/MonitoringService.cs`
3. Update `LoadAnalyticsAsync()` in `MainWindow.xaml.cs`
4. Add UI elements to Analytics page section in `MainWindow.xaml`
5. Include empty state messaging for insufficient data

## Database Schema
SQLite database at `%APPDATA%\WiFiHealthMonitor\wifihealth.db`:

- **NetworkMetrics** - WiFi metrics collected every 30 seconds (Timestamp, SignalPercent, Rssi, Band, Channel, ReceiveSpeedMbps, etc.)
- **SpeedTests** - Speed test results (Timestamp, DownloadMbps, UploadMbps, LatencyMs, Isp, Server)
- **Alerts** - Generated alerts and recommendations

All timestamps are UTC. Use `datetime(Timestamp)` in SQL queries for local time display.

## Common Development Tasks

### Adding a New Alert Rule
1. Add method to `Services/AlertEngine.cs`
2. Call from `AnalyzeMetricsAsync()`
3. Create Alert object with appropriate Type and Severity
4. Alert automatically appears in Overview page (UI subscribes to AlertGenerated event)

### Modifying Analytics Recommendations
**Key files**: `Services/AnalyticsService.cs` methods ending with "Recommendation"
- Always check if best/worst times/hours are different before comparing
- Use meaningful thresholds (>10-15%) to avoid trivial differences
- Include actual values (speeds, percentages) for context, not just recommendations

### Testing Analytics Features
```bash
# Delete database to start fresh
rmdir /s %APPDATA%\WiFiHealthMonitor

# Run app
dotnet run

# For ISP Performance: Run 2+ speed tests from Overview page
# For Best Download Times: Let run 100+ minutes across 3+ hours
# For Predictions: Let run 100+ minutes (500 points for ML.NET upgrade)
```

### Windows API Integration
WiFi data collected via `netsh wlan` commands:
```bash
netsh wlan show interfaces          # Current connection
netsh wlan show networks mode=bssid # Available networks
```

Parsed in `WiFiMonitorService.CollectMetricsAsync()`. Uses Process.Start with output redirection.

## Important Gotchas

1. **DO NOT use Debug.WriteLine** - Use Console.WriteLine for diagnostic output visible in `dotnet run`
2. **DO NOT modify prediction services individually** - HybridPredictiveService manages engine selection
3. **ALWAYS check data requirements** - Analytics returns null when insufficient data
4. **ALWAYS use Dispatcher.Invoke()** - Service events fire from background threads
5. **Page navigation is visibility-based** - Don't create new windows/pages, toggle Visibility
6. **Alert list is capped at 15** - Automatically removes oldest when full
7. **Database is in AppData** - Not in project directory, use `%APPDATA%\WiFiHealthMonitor`

## ML.NET Integration
- **Package**: Microsoft.ML 5.0.0 + Microsoft.ML.TimeSeries 5.0.0
- **Algorithm**: SSA (Singular Spectrum Analysis) for time-series forecasting
- **Training**: 2-5 seconds on 200 recent points, retrained every 24 hours
- **Prediction Horizon**: 12 periods ahead (6 minutes with 30s intervals)
- **Impact**: +20-40 MB memory, <1% CPU for predictions after training

## References
- **README.md** - User-facing documentation and features
- **ARCHITECTURE.md** - Detailed technical architecture, data flows, and extensibility
- **MainWindow.xaml.cs** - UI event handlers and page navigation logic
- **MonitoringService.cs** - Central orchestrator, all timer management
- **AnalyticsService.cs** - Historical data analysis algorithms
