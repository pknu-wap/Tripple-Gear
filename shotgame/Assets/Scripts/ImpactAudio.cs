using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class ImpactAudio : MonoBehaviour
{
    private void Start()
    {
        AudioSource source = GetComponent<AudioSource>();
        if (source.clip != null) source.PlayOneShot(source.clip, GameAudioSettings.EffectsGain);
    }
}
