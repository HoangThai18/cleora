using System;
using System.Linq;
using System.Windows.Forms;
using Cleora.App.Logging;
using Cleora.App.Native;
using Cleora.App.Platform;
using Cleora.App.UI;
using Cleora.Core.Settings;

namespace Cleora.App
{
    internal static class Program
    {
        public static string Version => typeof(Program).Assembly.GetName().Version.ToString(3);

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var selfTest = OptionValue(args, "--selftest");
            var screenshot = OptionValue(args, "--ui-screenshot");
            if (selfTest != null || screenshot != null)
            {
                // A GUI-subsystem exe has no console; borrow the caller's so CI logs show progress.
                NativeMethods.AttachConsole(NativeMethods.AttachParentProcess);
            }

            if (selfTest != null)
            {
                return SelfTest.Run(selfTest);
            }

            if (screenshot != null)
            {
                var scale = float.Parse(OptionValue(args, "--ui-scale") ?? "1", System.Globalization.CultureInfo.InvariantCulture);
                var page = (PageKind)Enum.Parse(typeof(PageKind), OptionValue(args, "--page") ?? nameof(PageKind.Clean), ignoreCase: true);
                return UiScreenshot.Run(screenshot, scale, page, OptionValue(args, "--theme"));
            }

            return RunApp(args.Contains("--after-elevate"));
        }

        private static int RunApp(bool afterElevate)
        {
            Log.Initialize(Log.DefaultDirectory, echoToConsole: false);
            using (var instance = SingleInstance.TryAcquire(afterElevate ? TimeSpan.FromSeconds(10) : TimeSpan.Zero))
            {
                if (instance == null)
                {
                    Log.Info("Cleora is already running; asked it to show its window");
                    return 0;
                }

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (_, e) => Report(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("unhandled exception", e.ExceptionObject as Exception);

                var settings = SettingsStore.Load(AppPaths.SettingsFile);
                Log.Info($"Cleora {Version} starting{(AppPaths.IsPortable ? " (portable)" : string.Empty)}, {Environment.OSVersion}");
                Theme.Use(Theme.For(settings.Theme));
                using (var form = new MainForm())
                using (var controller = new MainController(form, settings, AppPaths.SettingsFile))
                {
                    controller.Start();
                    Application.Run(controller.Context);
                }
            }

            return 0;
        }

        private static void Report(Exception error)
        {
            Log.Error("unexpected error on the UI thread", error);
            MessageBox.Show("Cleora gặp lỗi không mong muốn nhưng vẫn chạy tiếp. Chi tiết đã ghi vào nhật ký (Cài đặt → Khi gặp lỗi).", "Cleora", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        internal static string? OptionValue(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
