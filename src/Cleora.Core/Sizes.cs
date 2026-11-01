using System;
using System.Globalization;

namespace Cleora.Core
{
    /// <summary>Byte counts as people read them ("1,2 GB"), always with a decimal comma like the rest of the app.</summary>
    public static class Sizes
    {
        private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

        public static string Format(long bytes)
        {
            if (bytes >= 1L << 30)
            {
                return (bytes / (double)(1L << 30)).ToString("0.0", Vietnamese) + " GB";
            }

            if (bytes >= 1L << 20)
            {
                return (bytes / (double)(1L << 20)).ToString("0.0", Vietnamese) + " MB";
            }

            if (bytes >= 1L << 10)
            {
                return (bytes / 1024.0).ToString("0", Vietnamese) + " KB";
            }

            return Math.Max(0, bytes) + " B";
        }
    }
}
