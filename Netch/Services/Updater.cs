using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Netch.Controllers;

namespace Netch.Services
{
    internal static class Updater
    {
        internal const string ApplyArgument = "--apply-update";
        internal const string CleanupArgument = "--cleanup-update";
        private const string StagePrefix = ".netch-update-";
        private static readonly string[] AllowedFolders = { "bin", "amd64", "x86", "zh-Hans", "en-US", "runtimes" };
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(5) };

        public static async Task<string> StageAsync(UpdateChecker.Release release)
        {
            var stage = Path.Combine(Global.NetchDir, StagePrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                var archive = Path.Combine(stage, "package.zip");
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                using (var response = await Client.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead, timeout.Token))
                {
                    response.EnsureSuccessStatusCode();
                    await using var stream = File.Create(archive);
                    await response.Content.CopyToAsync(stream, timeout.Token);
                }
                await using (var stream = File.OpenRead(archive))
                {
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
                    if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The package SHA-256 did not match GitHub.");
                }

                await Task.Run(() => ZipFile.ExtractToDirectory(archive, stage));
                var payload = Path.Combine(stage, "Netch");
                var executable = Path.Combine(payload, "Netch.exe");
                if (!File.Exists(executable) || !File.Exists(Path.Combine(payload, "Netch.dll")) ||
                    FileVersionInfo.GetVersionInfo(executable).ProductVersion != release.Version)
                    throw new InvalidDataException("The package does not contain the expected Netch version.");
                foreach (var file in Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories))
                    PackagePath(payload, file);

                File.Delete(archive);
                return stage;
            }
            catch
            {
                TryDeleteStage(stage);
                throw;
            }
        }

        public static void Start(string stage)
        {
            using var helper = Launch(Path.Combine(stage, "Netch", "Netch.exe"), ApplyArgument, Environment.ProcessId.ToString());
        }

        public static bool TryHandleCommand(string[] args)
        {
            if (args is [ApplyArgument, var parent] && int.TryParse(parent, out var parentId))
            {
                try { Apply(parentId); }
                catch (Exception error) { MessageBox.Show(error.Message, "Netch Update", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                return true;
            }

            if (args is [CleanupArgument, var helper, var name] && int.TryParse(helper, out var helperId) && IsStageName(name))
            {
                try
                {
                    WaitForExit(helperId);
                    TryDeleteStage(Path.Combine(Global.NetchDir, name));
                }
                catch (TimeoutException) { }
            }
            return false;
        }

        private static void Apply(int parentId)
        {
            var payload = Path.TrimEndingDirectorySeparator(Global.NetchDir);
            var stage = GetValidatedStage(payload);
            var install = stage.Parent!;

            WaitForExit(parentId);
            ApplyPackage(payload, install.FullName, Path.Combine(stage.FullName, "backup"), () =>
            {
                using var restarted = Launch(Path.Combine(install.FullName, "Netch.exe"), CleanupArgument,
                    Environment.ProcessId.ToString(), stage.Name);
            });
        }

        internal static DirectoryInfo GetValidatedStage(string payload)
        {
            var package = new DirectoryInfo(Path.TrimEndingDirectorySeparator(payload));
            var stage = package.Parent;
            if (stage?.Parent == null || !IsStageName(stage.Name) ||
                !package.Name.Equals("Netch", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid Netch update location.");

            return stage;
        }

        internal static void ApplyPackage(string payload, string install, string backup, Action restart)
        {
            var applied = new List<(string Target, string? Backup)>();
            try
            {
                foreach (var source in Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories))
                {
                    var relative = PackagePath(payload, source);
                    var target = Path.Combine(install, relative);
                    for (var directory = new DirectoryInfo(Path.GetDirectoryName(target)!);
                         directory != null && directory.FullName != install; directory = directory.Parent)
                    {
                        if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            throw new IOException($"Cannot update through a link: {directory.Name}");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    string? saved = null;
                    if (File.Exists(target))
                    {
                        saved = Path.Combine(backup, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                        File.Move(target, saved);
                    }
                    applied.Add((target, saved));
                    File.Copy(source, target);
                }

                restart();
            }
            catch
            {
                foreach (var (target, saved) in applied.AsEnumerable().Reverse())
                {
                    File.Delete(target);
                    if (saved != null)
                        File.Move(saved, target);
                }
                throw;
            }
        }

        private static string PackagePath(string payload, string file)
        {
            var relative = Path.GetRelativePath(payload, file);
            var parts = relative.Split(Path.DirectorySeparatorChar);
            if (parts.Length > 1 ? AllowedFolders.Contains(parts[0], StringComparer.OrdinalIgnoreCase) :
                relative is "Netch.exe" or "README.md" or "LICENSE" || relative.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                relative is "Netch.deps.json" or "Netch.runtimeconfig.json")
                return relative;

            throw new InvalidDataException($"Unexpected file in Netch update: {relative}");
        }

        private static bool IsStageName(string name) => name.StartsWith(StagePrefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(name[StagePrefix.Length..], "N", out _);

        private static void TryDeleteStage(string stage)
        {
            if (!IsStageName(Path.GetFileName(stage)))
                return;

            try { Directory.Delete(stage, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void WaitForExit(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (!process.WaitForExit(45_000))
                    throw new TimeoutException("Netch did not exit in time to apply the update.");
            }
            catch (ArgumentException) { }
        }

        private static Process Launch(string executable, params string[] arguments)
        {
            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)!
            };
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);
            return Process.Start(info) ?? throw new InvalidOperationException("Unable to start Netch.");
        }
    }
}
