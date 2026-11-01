using System;
using System.Runtime.InteropServices;

namespace Cleora.App.Native
{
    internal static class NativeMethods
    {
        public const int DwmUseImmersiveDarkModeOld = 19;
        public const int DwmUseImmersiveDarkMode = 20;
        public const int DwmBorderColor = 34;
        public const int DwmCaptionColor = 35;
        public const int DwmTextColor = 36;

        public const int AttachParentProcess = -1;

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);
    }
}
