using System.Collections.Generic;
using UnityEngine;

internal static class GameplaySfx
{
    internal enum Cue
    {
        Pickup, Heal, Dash, Shield, Throw, Electric, Explosion, Energy, Laser, Warning,
        Missile, MetalHit, PlayerHit, Reload, Wind, Splash, Teleport, Gate, Clear, Failure, Encounter, Flash
    }

    private static readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
    private static readonly Dictionary<Cue, float> lastPlayed = new Dictionary<Cue, float>();
    private static AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        clips.Clear();
        lastPlayed.Clear();
        source = null;
    }

    internal static void Play(Cue cue, float volume = 0.55f)
    {
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(cue, out float previous) && now - previous < 0.075f) return;
        if (!clips.TryGetValue(cue, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>("GameplaySfx/" + cue);
            clips[cue] = clip;
        }
        if (clip == null) return;

        if (source == null)
        {
            source = new GameObject("게임 효과음").AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        lastPlayed[cue] = now;
        source.PlayOneShot(clip, volume * GameAudioSettings.EffectsGain);
    }
}
