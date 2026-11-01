using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cleora.App.Logging;
using Cleora.Core.Startup;
using Microsoft.Win32;

namespace Cleora.App.Cleaning
{
    /// <summary>
    /// Apps that start with Windows: the Run keys and the Startup folders, switched on and off through
    /// StartupApproved exactly like Task Manager, so nothing is deleted and Task Manager shows the same state.
    /// </summary>
    internal static class StartupApps
    {
        private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static IReadOnlyList<StartupItem> List()
        {
            var items = new List<StartupItem>();
            FromRun(RegistryHive.CurrentUser, RegistryView.Default, StartupLocation.UserRun, items);
            FromRun(RegistryHive.LocalMachine, RegistryView.Registry64, StartupLocation.MachineRun, items);
            FromRun(RegistryHive.LocalMachine, RegistryView.Registry32, StartupLocation.MachineRun32, items);
            FromFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupLocation.UserFolder, items);
            FromFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupLocation.CommonFolder, items);
            return items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static bool SetEnabled(StartupItem item, bool enabled)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(HiveOf(item.Location), RegistryView.Registry64))
                using (var key = root.CreateSubKey(StartupApproved.SubKey(item.Location)))
                {
                    key!.SetValue(item.Name, StartupApproved.Value(enabled, DateTime.UtcNow), RegistryValueKind.Binary);
                }

                Log.Info($"startup {item.Name} ({item.Location}) -> {(enabled ? "on" : "off")}");
                return true;
            }
            catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is IOException)
            {
                Log.Warn($"startup {item.Name}: {error.Message}");
                return false;
            }
        }

        private static RegistryHive HiveOf(StartupLocation location) =>
            location == StartupLocation.UserRun || location == StartupLocation.UserFolder ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;

        private static bool ReadEnabled(StartupLocation location, string name)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(HiveOf(location), RegistryView.Registry64))
                using (var key = root.OpenSubKey(StartupApproved.SubKey(location)))
                {
                    return StartupApproved.IsEnabled(key?.GetValue(name) as byte[]);
                }
            }
            catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is IOException)
            {
                return true;
            }
        }

        private static void FromRun(RegistryHive hive, RegistryView view, StartupLocation location, List<StartupItem> into)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var key = root.OpenSubKey(Run))
                {
                    if (key == null)
                    {
                        return;
                    }

                    foreach (var name in key.GetValueNames().Where(n => n.Length > 0))
                    {
                        into.Add(new StartupItem(name, key.GetValue(name)?.ToString() ?? string.Empty, location, ReadEnabled(location, name)));
                    }
                }
            }
            catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is IOException)
            {
                Log.Warn($"reading {hive} {view} Run: {error.Message}");
            }
        }

        private static void FromFolder(string folder, StartupLocation location, List<StartupItem> into)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(folder).Where(f => !string.Equals(Path.GetFileName(f), "desktop.ini", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileName(file);
                into.Add(new StartupItem(name, file, location, ReadEnabled(location, name)));
            }
        }
    }
}
