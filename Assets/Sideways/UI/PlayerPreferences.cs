using System;
using UnityEngine;

namespace Sideways.UI
{
    public readonly struct PlayerPreferences : IEquatable<PlayerPreferences>
    {
        public PlayerPreferences(bool audioEnabled, bool hapticsEnabled, bool highContrast, bool largeHud, bool reducedMotion)
        {
            AudioEnabled = audioEnabled;
            HapticsEnabled = hapticsEnabled;
            HighContrast = highContrast;
            LargeHud = largeHud;
            ReducedMotion = reducedMotion;
        }

        public bool AudioEnabled { get; }
        public bool HapticsEnabled { get; }
        public bool HighContrast { get; }
        public bool LargeHud { get; }
        public bool ReducedMotion { get; }

        public static PlayerPreferences Default => new(true, true, false, false, false);

        public bool Equals(PlayerPreferences other) =>
            AudioEnabled == other.AudioEnabled &&
            HapticsEnabled == other.HapticsEnabled &&
            HighContrast == other.HighContrast &&
            LargeHud == other.LargeHud &&
            ReducedMotion == other.ReducedMotion;

        public override bool Equals(object obj) => obj is PlayerPreferences other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(AudioEnabled, HapticsEnabled, HighContrast, LargeHud, ReducedMotion);
    }

    public sealed class PlayerPreferencesStore
    {
        private readonly string prefix;

        public PlayerPreferencesStore(string keyPrefix = "sideways.preferences")
        {
            prefix = string.IsNullOrWhiteSpace(keyPrefix) ? "sideways.preferences" : keyPrefix;
        }

        public PlayerPreferences Load()
        {
            var defaults = PlayerPreferences.Default;
            return new PlayerPreferences(
                Read("audio", defaults.AudioEnabled),
                Read("haptics", defaults.HapticsEnabled),
                Read("contrast", defaults.HighContrast),
                Read("large-hud", defaults.LargeHud),
                Read("reduced-motion", defaults.ReducedMotion));
        }

        public void Save(PlayerPreferences preferences)
        {
            Write("audio", preferences.AudioEnabled);
            Write("haptics", preferences.HapticsEnabled);
            Write("contrast", preferences.HighContrast);
            Write("large-hud", preferences.LargeHud);
            Write("reduced-motion", preferences.ReducedMotion);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(Key("audio"));
            PlayerPrefs.DeleteKey(Key("haptics"));
            PlayerPrefs.DeleteKey(Key("contrast"));
            PlayerPrefs.DeleteKey(Key("large-hud"));
            PlayerPrefs.DeleteKey(Key("reduced-motion"));
        }

        private bool Read(string suffix, bool fallback) => PlayerPrefs.GetInt(Key(suffix), fallback ? 1 : 0) != 0;

        private void Write(string suffix, bool value) => PlayerPrefs.SetInt(Key(suffix), value ? 1 : 0);

        private string Key(string suffix) => $"{prefix}.{suffix}";
    }
}
