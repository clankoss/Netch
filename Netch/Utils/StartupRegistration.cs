using System;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;

namespace Netch.Utils
{
    internal static class StartupRegistration
    {
        private const string LegacyTaskName = "Netch Startup";

        internal static void SetEnabled(bool enabled)
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value ?? throw new InvalidOperationException("Unable to identify the current Windows user.");
            var executable = Path.Combine(Global.NetchDir, "Netch.exe");
            var taskName = LegacyTaskName + "-" + sid;
            using var service = new TaskService();
            using var folder = service.RootFolder;

            if (enabled)
            {
                if (!File.Exists(executable))
                    throw new FileNotFoundException("Netch.exe was not found.", executable);

                using var definition = service.NewTask();
                Configure(definition, sid, executable);
                using var registered = folder.RegisterTaskDefinition(taskName, definition, TaskCreation.CreateOrUpdate,
                    sid, null, TaskLogonType.InteractiveToken);
            }
            else
            {
                folder.DeleteTask(taskName, exceptionOnNotExists: false);
            }

            using var legacy = service.GetTask("\\" + LegacyTaskName);
            if (legacy == null)
                return;

            using var previous = legacy.Definition;
            var previousUser = previous.Principal.UserId;
            if ((string.Equals(previousUser, sid, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(previousUser, identity.Name, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(previousUser, Environment.UserName, StringComparison.OrdinalIgnoreCase)) &&
                previous.Actions.Count == 1 && previous.Actions[0] is ExecAction action &&
                string.Equals(action.Path.Trim('"'), executable, StringComparison.OrdinalIgnoreCase))
                folder.DeleteTask(LegacyTaskName, exceptionOnNotExists: false);
        }

        internal static void Configure(TaskDefinition definition, string sid, string executable)
        {
            definition.RegistrationInfo.Author = "Netch";
            definition.RegistrationInfo.Description = "Start Netch when this Windows user signs in.";
            definition.Principal.UserId = sid;
            definition.Principal.LogonType = TaskLogonType.InteractiveToken;
            definition.Principal.RunLevel = TaskRunLevel.Highest;
            definition.Triggers.Add(new LogonTrigger { UserId = sid });
            definition.Actions.Add(new ExecAction(executable, Constants.Parameter.Startup, Path.GetDirectoryName(executable)!));
            definition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
        }
    }
}
