using UnityEngine;

internal static class GameAudioSettings
{
    private const float EffectsBaseGain = 1.5f;
    private const string EffectsKey = "TrippleGear.EffectsVolume";
    private const string MusicKey = "TrippleGear.MusicVolume";
    internal static event System.Action<float> MusicChanged;

    internal static float Effects
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, 1f));
        set => PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value));
    }

    internal static float EffectsGain => Effects * EffectsBaseGain;

    internal static float Music
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
        set
        {
            float level = Mathf.Clamp01(value);
            if (Mathf.Approximately(Music, level)) return;
            PlayerPrefs.SetFloat(MusicKey, level);
            MusicChanged?.Invoke(level);
        }
    }
}
