using Cleora.Core.Scanning;
using Cleora.Core.Settings;

namespace Cleora.Core.Tests
{
    public class SettingsAndSizesTests
    {
        [Theory]
        [InlineData(0, "0 B")]
        [InlineData(2048, "2 KB")]
        [InlineData(5L * 1024 * 1024 + 300_000, "5,3 MB")]
        [InlineData(3L * 1024 * 1024 * 1024 / 2, "1,5 GB")]
        public void Format_UsesADecimalComma(long bytes, string expected)
        {
            Assert.Equal(expected, Sizes.Format(bytes));
        }

        [Fact]
        public void Settings_RoundTrip_AndBadValuesFallBack()
        {
            using var s = new Scratch();
            var path = Path.Combine(s.Root, "settings.json");
            var settings = new AppSettings { Theme = ThemeMode.Dark, KeepRule = KeepRule.Oldest, DuplicateMinMb = 10, FreedTotal = 12345 };
            settings.Choices["shader-cache"] = true;
            settings.Choices["temp"] = false;

            SettingsStore.Save(path, settings);
            var loaded = SettingsStore.Load(path);

            Assert.Equal(ThemeMode.Dark, loaded.Theme);
            Assert.Equal(KeepRule.Oldest, loaded.KeepRule);
            Assert.Equal(10, loaded.DuplicateMinMb);
            Assert.Equal(12345, loaded.FreedTotal);
            Assert.True(loaded.IsChosen("shader-cache", false));
            Assert.False(loaded.IsChosen("temp", true));
            Assert.True(loaded.IsChosen("chrome", true));

            File.WriteAllText(path, "{\"theme\":\"Neon\",\"duplicateMinMb\":7}");
            var odd = SettingsStore.Load(path);
            Assert.Equal(ThemeMode.System, odd.Theme);
            Assert.Equal(1, odd.DuplicateMinMb);
            Assert.NotNull(odd.Choices);
        }
    }
}
