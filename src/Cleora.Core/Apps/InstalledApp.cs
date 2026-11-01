using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Cleora.Core.Apps
{
    /// <summary>An app from the Windows "Uninstall" registry list, as "Apps & features" shows it.</summary>
    public sealed record InstalledApp(
        string Name,
        string Publisher,
        string Version,
        long? Bytes,
        DateTime? Installed,
        string UninstallCommand,
        string RegistryKey);

    public static class UninstallEntry
    {
        /// <summary>
        /// One registry key's values to an app, or null for entries Windows hides too: updates, system components,
        /// parts of another app, and entries without a name or an uninstall command.
        /// </summary>
        public static InstalledApp? Parse(IReadOnlyDictionary<string, object?> values, string registryKey)
        {
            string Text(string name) => values.TryGetValue(name, out var v) && v != null ? v.ToString()!.Trim() : string.Empty;
            long? Number(string name)
            {
                if (!values.TryGetValue(name, out var v) || v == null)
                {
                    return null;
                }

                return v is int i ? (long)unchecked((uint)i) : long.TryParse(v.ToString(), out var n) ? n : (long?)null;
            }

            var name = Text("DisplayName");
            var command = Text("UninstallString");
            if (name.Length == 0 || command.Length == 0 || Number("SystemComponent") == 1
                || Text("ParentKeyName").Length > 0 || Text("ReleaseType").Length > 0)
            {
                return null;
            }

            var sizeKb = Number("EstimatedSize");
            DateTime? installed = DateTime.TryParseExact(Text("InstallDate"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : (DateTime?)null;
            return new InstalledApp(name, Text("Publisher"), Text("DisplayVersion"), sizeKb > 0 ? sizeKb * 1024 : null, installed, command, registryKey);
        }

        /// <summary>The same app listed in both the 64-bit and 32-bit views (or per user and per machine) once.</summary>
        public static IReadOnlyList<InstalledApp> Distinct(IEnumerable<InstalledApp> apps) =>
            apps.GroupBy(a => a.Name + "\u0001" + a.Version, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(a => a.Bytes ?? 0).First())
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
    }

    /// <summary>Splits an uninstall command line into the program and its arguments.</summary>
    public static class CommandLine
    {
        public static (string File, string Arguments) Split(string command)
        {
            var text = command.Trim();
            if (text.StartsWith("\"", StringComparison.Ordinal))
            {
                var close = text.IndexOf('"', 1);
                if (close > 0)
                {
                    return (text.Substring(1, close - 1), text.Substring(close + 1).Trim());
                }
            }

            // Unquoted paths with spaces ("C:\Program Files\X\uninst.exe /S"): the program ends at ".exe".
            var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe > 0)
            {
                var end = exe + 4;
                return (text.Substring(0, end), text.Substring(end).Trim());
            }

            var space = text.IndexOf(' ');
            return space < 0 ? (text, string.Empty) : (text.Substring(0, space), text.Substring(space + 1).Trim());
        }
    }
}
