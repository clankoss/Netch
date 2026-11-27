using System;
using System.IO;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.TaskScheduler;
using Netch.Utils;

namespace UnitTest
{
    [TestClass]
    public class StartupRegistrationTests
    {
        [TestMethod]
        public void StartupDefinitionIsScopedToInteractiveUser()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User!.Value;
            using var service = new TaskService();
            using var definition = service.NewTask();
            var executable = Path.Combine(Path.GetTempPath(), "Netch Test Install", "Netch.exe");

            StartupRegistration.Configure(definition, sid, executable);

            AssertCurrentUser(definition.Principal.UserId, identity);
            Assert.AreEqual(TaskLogonType.InteractiveToken, definition.Principal.LogonType);
            Assert.AreEqual(TaskRunLevel.Highest, definition.Principal.RunLevel);
            AssertCurrentUser(((LogonTrigger)definition.Triggers[0]).UserId, identity);
            var action = (ExecAction)definition.Actions[0];
            Assert.AreEqual(executable, action.Path);
            Assert.AreEqual(Path.GetDirectoryName(executable), action.WorkingDirectory);
            Assert.AreEqual("--startup", action.Arguments);
            Assert.AreEqual(TimeSpan.Zero, definition.Settings.ExecutionTimeLimit);
            Assert.IsFalse(definition.Settings.DisallowStartIfOnBatteries);
        }
        private static void AssertCurrentUser(string userId, WindowsIdentity identity)
        {
            Assert.IsTrue(string.Equals(userId, identity.User!.Value, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(userId, identity.Name, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(userId, Environment.UserName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
