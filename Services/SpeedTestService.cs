using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WiFiHealthMonitor.Models;

namespace WiFiHealthMonitor.Services
{
    /// <summary>
    /// Service for running internet speed tests
    /// </summary>
    public class SpeedTestService
    {
        private readonly string _speedTestExePath;
        private const string SPEEDTEST_URL = "https://install.speedtest.net/app/cli/ookla-speedtest-1.2.0-win64.zip";

        public SpeedTestService()
        {
            var tempPath = Path.GetTempPath();
            _speedTestExePath = Path.Combine(tempPath, "speedtest.exe");
        }

        /// <summary>
        /// Ensures speedtest.exe is downloaded and available
        /// </summary>
        public async Task<bool> EnsureSpeedTestInstalledAsync()
        {
            if (File.Exists(_speedTestExePath))
                return true;

            try
            {
                Debug.WriteLine("Downloading Speedtest CLI...");
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                var zipPath = Path.Combine(Path.GetTempPath(), "speedtest.zip");
                var zipBytes = await httpClient.GetByteArrayAsync(SPEEDTEST_URL);
                await File.WriteAllBytesAsync(zipPath, zipBytes);

                // Extract using PowerShell
                var extractCommand = $"Expand-Archive -Path '{zipPath}' -DestinationPath '{Path.GetTempPath()}' -Force";
                await RunPowerShellCommandAsync(extractCommand);

                File.Delete(zipPath);

                return File.Exists(_speedTestExePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading speedtest: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs a speed test and returns the results
        /// </summary>
        public async Task<SpeedTestResult?> RunSpeedTestAsync()
        {
            try
            {
                if (!await EnsureSpeedTestInstalledAsync())
                {
                    Debug.WriteLine("Speedtest executable not available");
                    return null;
                }

                var processInfo = new ProcessStartInfo(_speedTestExePath, "--accept-license --format=json")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetTempPath()
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                    return null;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (string.IsNullOrEmpty(output))
                    return null;

                // Parse the first JSON object (before the license text)
                var jsonStart = output.IndexOf("{");
                var jsonEnd = output.IndexOf("\n==============", jsonStart);
                if (jsonEnd == -1) jsonEnd = output.Length;

                var jsonOutput = output.Substring(jsonStart, jsonEnd - jsonStart);

                var result = JsonSerializer.Deserialize<SpeedTestResult>(jsonOutput);
                if (result != null)
                {
                    result.Timestamp = DateTime.Now;
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running speed test: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Runs a PowerShell command
        /// </summary>
        private async Task<string> RunPowerShellCommandAsync(string command)
        {
            var processInfo = new ProcessStartInfo("powershell.exe", $"-Command \"{command}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                return string.Empty;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output;
        }
    }
}
