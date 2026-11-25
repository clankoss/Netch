using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Netch.Controllers
{
    public static class UpdateChecker
    {
        public const string Owner = "clankoss";
        public const string Repo = "Netch";
        public const string Name = "Netch";
        public const string ReleasesUrl = "https://github.com/" + Owner + "/" + Repo + "/releases";

        public static readonly string Version = typeof(UpdateChecker).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        internal record Release(string Version, Uri Download, string Sha256);

        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

        internal static async Task<Release?> CheckAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
            request.Headers.UserAgent.ParseAdd("Netch/" + Version);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var release = json.RootElement;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            var number = tag.StartsWith('v') ? tag[1..] : tag;
            if (!System.Version.TryParse(number, out var latest) || latest.Build < 0 ||
                latest.Revision >= 0 || latest <= System.Version.Parse(Version))
                return null;

            var file = $"Netch-{latest}-win-x64.zip";
            var download = new Uri($"{ReleasesUrl}/download/{tag}/{file}");
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != file ||
                    asset.GetProperty("browser_download_url").GetString() != download.AbsoluteUri ||
                    !asset.TryGetProperty("digest", out var digest))
                    continue;

                var sha256 = digest.GetString();
                if (sha256?.StartsWith("sha256:", StringComparison.Ordinal) == true &&
                    sha256.Length == 71 && sha256[7..].All(Uri.IsHexDigit))
                    return new Release(latest.ToString(), download, sha256[7..]);
            }

            throw new InvalidOperationException("The release is missing a verified Windows x64 package.");
        }
    }
}
