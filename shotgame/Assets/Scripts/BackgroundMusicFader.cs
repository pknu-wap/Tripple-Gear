using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class BackgroundMusicFader : MonoBehaviour
{
    private AudioSource musicSource;
    private Coroutine fadeRoutine;
    private float normalVolume;

    private void Awake()
    {
        EnsureSource();
    }

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
            musicSource.volume = 0f;
            gameObject.SetActive(true);
        }

        if (targetVolume > 0f && !musicSource.isPlaying && isActiveAndEnabled)
        {
            musicSource.volume = 0f;
            musicSource.Play();
        }

        if (!isActiveAndEnabled || duration <= 0f || Mathf.Approximately(musicSource.volume, targetVolume))
        {
            musicSource.volume = targetVolume;
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
    }

    private IEnumerator AnimateVolume(float targetVolume, float duration)
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        musicSource.volume = targetVolume;
        if (targetVolume <= 0f)
        {
            musicSource.Stop();
        }
        fadeRoutine = null;
    }
}
