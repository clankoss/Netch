using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netch.Services;

namespace UnitTest
{
    [TestClass]
    public class AppUpdateTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ValidatesStagedAppPathWithOrWithoutTrailingSeparator(bool trailingSeparator)
        {
            var install = Path.Combine(Path.GetTempPath(), "Netch Install");
            var stage = Path.Combine(install, ".netch-update-0123456789abcdef0123456789abcdef");
            var payload = Path.Combine(stage, "Netch");
            if (trailingSeparator)
                payload += Path.DirectorySeparatorChar;

            Assert.AreEqual(stage, Updater.GetValidatedStage(payload).FullName);
        }

        [TestMethod]
        public void RejectsApplicationOutsideUpdateStage()
        {
            var payload = Path.Combine(Path.GetTempPath(), "Unrelated Folder", "Netch") + Path.DirectorySeparatorChar;
            Assert.ThrowsException<InvalidOperationException>(() => Updater.GetValidatedStage(payload));
        }

        [TestMethod]
        public void UpdatePreservesConfigurationAndCustomModes()
        {
            WithPackage((payload, install, backup) =>
            {
                Updater.ApplyPackage(payload, install, backup, () => { });
                Assert.AreEqual("new app", File.ReadAllText(Path.Combine(install, "Netch.exe")));
                Assert.AreEqual("settings", File.ReadAllText(Path.Combine(install, "data", "settings.json")));
                Assert.AreEqual("custom mode", File.ReadAllText(Path.Combine(install, "mode", "Custom", "game.txt")));
            });
        }

        [TestMethod]
        public void FailedRestartRollsBackReplacement()
        {
            WithPackage((payload, install, backup) =>
            {
                Assert.ThrowsException<IOException>(() => Updater.ApplyPackage(payload, install, backup,
                    () => throw new IOException("Restart failed")));
                Assert.AreEqual("old app", File.ReadAllText(Path.Combine(install, "Netch.exe")));
                Assert.IsFalse(File.Exists(Path.Combine(install, "bonus.dll")));
            });
        }

        private static void WithPackage(Action<string, string, string> check)
        {
            var root = Directory.CreateTempSubdirectory("netch-update-test-");
            try
            {
                var install = Path.Combine(root.FullName, "install");
                var payload = Path.Combine(root.FullName, "package");
                Directory.CreateDirectory(Path.Combine(install, "data"));
                Directory.CreateDirectory(Path.Combine(install, "mode", "Custom"));
                Directory.CreateDirectory(payload);
                File.WriteAllText(Path.Combine(install, "data", "settings.json"), "settings");
                File.WriteAllText(Path.Combine(install, "mode", "Custom", "game.txt"), "custom mode");
                File.WriteAllText(Path.Combine(install, "Netch.exe"), "old app");
                File.WriteAllText(Path.Combine(payload, "Netch.exe"), "new app");
                File.WriteAllText(Path.Combine(payload, "bonus.dll"), "new dependency");
                check(payload, install, Path.Combine(root.FullName, "backup"));
            }
            finally { root.Delete(recursive: true); }
        }
    }
}
