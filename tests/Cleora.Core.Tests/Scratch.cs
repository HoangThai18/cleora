using Cleora.Core.Cleanup;

namespace Cleora.Core.Tests
{
    /// <summary>A throw-away folder tree shaped like a Windows machine, deleted after the test.</summary>
    public sealed class Scratch : IDisposable
    {
        public Scratch()
        {
            Root = Path.Combine(Path.GetTempPath(), "cleora-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            var drive = Path.Combine(Root, "C");
            Folders = new KnownFolders(
                drive,
                Path.Combine(drive, "Windows"),
                Path.Combine(drive, "ProgramData"),
                Path.Combine(drive, "Program Files (x86)"),
                Path.Combine(drive, "Users", "ban"),
                Path.Combine(drive, "Users", "ban", "AppData", "Local"),
                Path.Combine(drive, "Users", "ban", "AppData", "Roaming"),
                Path.Combine(drive, "Users", "ban", "AppData", "Local", "Temp"));
        }

        public string Root { get; }

        public KnownFolders Folders { get; }

        public string File(string path, int bytes = 10, DateTime? written = null)
        {
            var full = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            System.IO.File.WriteAllBytes(full, new byte[bytes]);
            System.IO.File.SetLastWriteTimeUtc(full, written ?? DateTime.UtcNow.AddDays(-30));
            return full;
        }

        public string Dir(string path)
        {
            var full = Path.Combine(Root, path);
            Directory.CreateDirectory(full);
            return full;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
