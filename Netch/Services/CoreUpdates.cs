using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Netch.Utils;

namespace Netch.Services
{
    internal static class CoreUpdates
    {
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };
        private static readonly Regex VersionPattern = new(@"\b\d+\.\d+\.\d+\b");

        public static async Task<(string Detail, bool UpdateAvailable)> CheckAsync(string repository, string executable)
        {
            var path = Path.Combine(Global.NetchDir, "bin", executable);
            var installed = File.Exists(path) ? await ReadVersionAsync(path) : null;
            var releaseUrl = $"https://github.com/{repository}/releases/latest";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
                request.Headers.UserAgent.ParseAdd("Netch-Core-Updates/1.0");
                using var response = await Client.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                var tag = release.RootElement.GetProperty("tag_name").GetString() ?? "";
                var latest = ParseVersion(tag);
                var updateAvailable = installed != null && latest != null && installed < latest;
                var status = installed == null ? "Missing or unreadable version" :
                    latest == null ? "Unable to compare versions" : updateAvailable ? "Update available" : "Up to date";
                return (Describe(executable, installed, tag, status, releaseUrl), updateAvailable);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or System.Collections.Generic.KeyNotFoundException or InvalidOperationException)
            {
                return (Describe(executable, installed, null, "Update check failed", releaseUrl), false);
            }
        }

        private static string Describe(string executable, Version? installed, string? latest, string status, string url)
        {
            var unknown = i18N.Translate("Unknown");
            return i18N.TranslateFormat("{0} | Installed: {1} | Latest: {2} | {3}",
                executable, installed?.ToString() ?? unknown, latest ?? unknown, status) + "\n" + url;
        }

        private static Version? ParseVersion(string value)
        {
            var match = VersionPattern.Match(value);
            return Version.TryParse(match.Value, out var parsed) ? parsed : null;
        }

        private static async Task<Version?> ReadVersionAsync(string path)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(path, "version")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                }
            };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                process.Start();
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                var lines = await Task.WhenAll(output, errors).ConfigureAwait(false);
                return process.ExitCode == 0 ? ParseVersion(string.Join("\n", lines)) : null;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                return null;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }
    }
}
