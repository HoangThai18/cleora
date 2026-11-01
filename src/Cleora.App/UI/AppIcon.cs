using System.Drawing;
using System.IO;
using System.Reflection;

namespace Cleora.App.UI
{
    /// <summary>The app icon, embedded in the exe (also set as the Win32 icon resource for Explorer and the taskbar).</summary>
    internal static class AppIcon
    {
        private static Icon? _large;
        private static Icon? _small;
        private static Bitmap? _bitmap;

        public static Icon Large => _large ??= new Icon(Open("cleora.ico"));

        /// <summary>The tray-sized frame of the same .ico, so Windows doesn't blur the 256 px one down.</summary>
        public static Icon Small => _small ??= new Icon(Open("cleora.ico"), System.Windows.Forms.SystemInformation.SmallIconSize);

        public static Bitmap Bitmap => _bitmap ??= new Bitmap(Open("cleora-256.png"));

        private static Stream Open(string name) =>
            Assembly.GetExecutingAssembly().GetManifestResourceStream("Cleora.App.Assets." + name)
            ?? throw new FileNotFoundException("embedded resource missing", name);
    }
}
