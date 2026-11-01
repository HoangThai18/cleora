using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Cleora.App.Cleaning
{
    internal static class RunningApps
    {
        /// <summary>Which of <paramref name="names"/> (process names without .exe) are running now.</summary>
        public static bool AnyRunning(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                var processes = Process.GetProcessesByName(name);
                var running = processes.Length > 0;
                foreach (var process in processes)
                {
                    process.Dispose();
                }

                if (running)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
