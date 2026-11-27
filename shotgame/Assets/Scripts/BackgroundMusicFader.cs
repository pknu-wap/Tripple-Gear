using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class BackgroundMusicFader : MonoBehaviour
{
    private AudioSource musicSource;
    private Coroutine fadeRoutine;
    private float normalVolume;
    private float currentVolume;

    private void Awake()
    {
        EnsureSource();
    }

    private void OnEnable()
    {
        ApplyMusicLevel(GameAudioSettings.Music);
        GameAudioSettings.MusicChanged += ApplyMusicLevel;
    }

    private void OnDisable() => GameAudioSettings.MusicChanged -= ApplyMusicLevel;

    public void FadeOut(float duration)
    {
        EnsureSource();
        FadeTo(0f, duration);
    }

    public void FadeIn(float duration)
    {
        EnsureSource();
        FadeTo(normalVolume, duration);
    }

    private void FadeTo(float targetVolume, float duration)
    {
        if (musicSource == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (targetVolume > 0f && !gameObject.activeSelf)
        {
            SetVolume(0f);
            gameObject.SetActive(true);
        }

        if (targetVolume > 0f && !musicSource.isPlaying && isActiveAndEnabled)
        {
            SetVolume(0f);
            musicSource.Play();
        }

        if (!isActiveAndEnabled || duration <= 0f || Mathf.Approximately(currentVolume, targetVolume))
        {
            SetVolume(targetVolume);
            if (targetVolume <= 0f)
            {
                musicSource.Stop();
            }
            return;
        }

        fadeRoutine = StartCoroutine(AnimateVolume(targetVolume, duration));
    }

    private void EnsureSource()
    {
        if (musicSource != null)
        {
            return;
        }

        musicSource = GetComponent<AudioSource>();
        normalVolume = musicSource.volume;
        SetVolume(normalVolume);
    }

    private void SetVolume(float volume)
    {
        currentVolume = volume;
        ApplyMusicLevel(GameAudioSettings.Music);
    }

    private void ApplyMusicLevel(float level) => musicSource.volume = currentVolume * level;

    private IEnumerator AnimateVolume(float targetVolume, float duration)
    {
        float startVolume = currentVolume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetVolume(Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetVolume(targetVolume);
        if (targetVolume <= 0f)
        {
            musicSource.Stop();
        }
        fadeRoutine = null;
    }
}
