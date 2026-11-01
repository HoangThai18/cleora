using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Cleora.Core.Scanning;

namespace Cleora.Core.Settings
{
    /// <summary>The app's look: follow Windows' light/dark app setting, or force one.</summary>
    public enum ThemeMode
    {
        System,
        Light,
        Dark,
    }

    /// <summary>
    /// Everything the user can change. Enums are stored as names so an unknown value falls back to that field's
    /// default instead of failing the whole file.
    /// </summary>
    [DataContract]
    public sealed class AppSettings
    {
        /// <summary>Smallest file the duplicate search looks at, in MB, one of <see cref="DuplicateSizes"/>.</summary>
        public static readonly int[] DuplicateSizes = { 0, 1, 10, 100 };

        public ThemeMode Theme { get; set; } = ThemeMode.System;

        public KeepRule KeepRule { get; set; } = KeepRule.Newest;

        /// <summary>Target id → ticked, for targets the user changed from their default.</summary>
        [DataMember(Name = "choices", Order = 1)] public Dictionary<string, bool> Choices { get; set; } = new Dictionary<string, bool>();

        [DataMember(Name = "duplicateMinMb", Order = 2)] public int DuplicateMinMb { get; set; } = 1;

        /// <summary>Everything Cleora has freed on this PC, shown on the home page.</summary>
        [DataMember(Name = "freedTotal", Order = 3)] public long FreedTotal { get; set; }

        [DataMember(Name = "theme", Order = 4)]
        private string ThemeText { get => Theme.ToString(); set => Theme = Parse(value, ThemeMode.System); }

        [DataMember(Name = "keepRule", Order = 5)]
        private string KeepRuleText { get => KeepRule.ToString(); set => KeepRule = Parse(value, KeepRule.Newest); }

        public bool IsChosen(string targetId, bool byDefault) => Choices.TryGetValue(targetId, out var on) ? on : byDefault;

        public AppSettings Normalize()
        {
            Choices ??= new Dictionary<string, bool>();
            DuplicateMinMb = Array.IndexOf(DuplicateSizes, DuplicateMinMb) >= 0 ? DuplicateMinMb : 1;
            FreedTotal = Math.Max(0, FreedTotal);
            return this;
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            // DataContractJsonSerializer skips constructors and field initializers; missing keys must still get defaults.
            var defaults = new AppSettings();
            foreach (var property in typeof(AppSettings).GetProperties())
            {
                if (property.CanWrite && property.CanRead)
                {
                    property.SetValue(this, property.GetValue(defaults));
                }
            }
        }

        private static T Parse<T>(string? text, T fallback)
            where T : struct =>
            Enum.TryParse<T>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(T), parsed) ? parsed : fallback;
    }
}
