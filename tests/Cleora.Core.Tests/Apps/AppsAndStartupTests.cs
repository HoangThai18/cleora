using Cleora.Core.Apps;
using Cleora.Core.Startup;

namespace Cleora.Core.Tests.Apps
{
    public class AppsAndStartupTests
    {
        private static Dictionary<string, object?> Values(params (string Key, object? Value)[] pairs) =>
            pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void Parse_RegularApp_ReadsSizeAndDate()
        {
            var app = UninstallEntry.Parse(Values(
                ("DisplayName", "Zalo"), ("Publisher", "VNG Corp."), ("DisplayVersion", "24.10.1"),
                ("EstimatedSize", 512000), ("InstallDate", "20260115"), ("UninstallString", "\"C:\\Users\\ban\\AppData\\Local\\Programs\\Zalo\\Uninstall Zalo.exe\"")), "Zalo");

            Assert.NotNull(app);
            Assert.Equal(512000L * 1024, app!.Bytes);
            Assert.Equal(new DateTime(2026, 1, 15), app.Installed);
        }

        [Theory]
        [InlineData("SystemComponent", 1)]
        [InlineData("ParentKeyName", "OfficeClickToRun")]
        [InlineData("ReleaseType", "Security Update")]
        public void Parse_HiddenEntries_Null(string key, object value)
        {
            Assert.Null(UninstallEntry.Parse(Values(("DisplayName", "X"), ("UninstallString", "x.exe"), (key, value)), "k"));
        }

        [Fact]
        public void Parse_NoNameOrNoUninstaller_Null()
        {
            Assert.Null(UninstallEntry.Parse(Values(("UninstallString", "x.exe")), "k"));
            Assert.Null(UninstallEntry.Parse(Values(("DisplayName", "X")), "k"));
        }

        [Fact]
        public void Distinct_SameAppInTwoViews_ListedOnce()
        {
            var a = new InstalledApp("7-Zip", "Igor Pavlov", "24.08", 5_000_000, null, "u", "64");
            var b = a with { Bytes = null, RegistryKey = "32" };

            Assert.Single(UninstallEntry.Distinct(new[] { a, b }));
        }

        [Theory]
        [InlineData("\"C:\\Program Files\\App\\uninst.exe\" /S", "C:\\Program Files\\App\\uninst.exe", "/S")]
        [InlineData("C:\\Program Files\\App\\uninst.exe /S", "C:\\Program Files\\App\\uninst.exe", "/S")]
        [InlineData("MsiExec.exe /X{1234-ABCD}", "MsiExec.exe", "/X{1234-ABCD}")]
        [InlineData("C:\\tools\\remove.cmd", "C:\\tools\\remove.cmd", "")]
        public void Split_CommandLines(string command, string file, string arguments)
        {
            Assert.Equal((file, arguments), CommandLine.Split(command));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(new byte[] { 2, 0, 0, 0 }, true)]
        [InlineData(new byte[] { 6, 0, 0, 0 }, true)]
        [InlineData(new byte[] { 3, 0, 0, 0 }, false)]
        [InlineData(new byte[] { 7, 0, 0, 0 }, false)]
        public void StartupApproved_ReadsTheSwitch(byte[]? value, bool enabled)
        {
            Assert.Equal(enabled, StartupApproved.IsEnabled(value));
        }

        [Fact]
        public void StartupApproved_WritesWhatItReads()
        {
            var now = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

            var off = StartupApproved.Value(false, now);
            var on = StartupApproved.Value(true, now);

            Assert.Equal(12, off.Length);
            Assert.False(StartupApproved.IsEnabled(off));
            Assert.Equal(now.ToFileTimeUtc(), BitConverter.ToInt64(off, 4));
            Assert.True(StartupApproved.IsEnabled(on));
            Assert.Equal(StartupApproved.Key + "Run32", StartupApproved.SubKey(StartupLocation.MachineRun32));
        }
    }
}
