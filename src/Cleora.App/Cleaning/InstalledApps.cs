using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Cleora.App.Logging;
using Cleora.Core.Apps;
using Microsoft.Win32;

namespace Cleora.App.Cleaning
{
    /// <summary>The apps "Apps & features" lists: the Uninstall keys of both registry views and of the user.</summary>
    internal static class InstalledApps
    {
        private const string Uninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

        public static IReadOnlyList<InstalledApp> List()
        {
            var apps = new List<InstalledApp>();
            Read(RegistryHive.LocalMachine, RegistryView.Registry64, apps);
            Read(RegistryHive.LocalMachine, RegistryView.Registry32, apps);
            Read(RegistryHive.CurrentUser, RegistryView.Default, apps);
            return UninstallEntry.Distinct(apps);
        }

        /// <summary>Starts the app's own uninstaller (Windows asks for rights when it needs them).</summary>
        public static bool Start(InstalledApp app)
        {
            var (file, arguments) = CommandLine.Split(app.UninstallCommand);
            try
            {
                Process.Start(new ProcessStartInfo(Environment.ExpandEnvironmentVariables(file), arguments) { UseShellExecute = true })?.Dispose();
                return true;
            }
            catch (Exception error) when (error is Win32Exception || error is InvalidOperationException || error is System.IO.FileNotFoundException)
            {
                Log.Warn($"uninstall of {app.Name} failed to start: {error.Message}");
                return false;
            }
        }

        private static void Read(RegistryHive hive, RegistryView view, List<InstalledApp> into)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var list = root.OpenSubKey(Uninstall))
                {
                    if (list == null)
                    {
                        return;
                    }

                    foreach (var name in list.GetSubKeyNames())
                    {
                        using (var key = list.OpenSubKey(name))
                        {
                            if (key == null)
                            {
                                continue;
                            }

                            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            foreach (var valueName in key.GetValueNames())
                            {
                                values[valueName] = key.GetValue(valueName);
                            }

                            var app = UninstallEntry.Parse(values, $@"{hive}\{Uninstall}\{name}");
                            if (app != null)
                            {
                                into.Add(app);
                            }
                        }
                    }
                }
            }
            catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is System.IO.IOException)
            {
                Log.Warn($"reading {hive} {view} uninstall list: {error.Message}");
            }
        }
    }
}
