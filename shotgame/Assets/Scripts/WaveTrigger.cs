using System.Collections;
using UnityEngine;

public enum MusicTransitionTiming
{
    [InspectorName("차례대로")]
    Sequential,
    [InspectorName("동시에")]
    Crossfade,
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class WaveTrigger : MonoBehaviour
{
    [SerializeField, KoreanLabel("웨이브 매니저")] private WaveManager waveManager;
    [SerializeField, KoreanLabel("플레이어 감지 레이어")] private LayerMask playerLayers = ~0;
    [SerializeField, KoreanLabel("지정 플레이어 (선택)")] private ArisaHorizontalMovement requiredPlayer;

    [Header("배경음악")]
    [SerializeField, KoreanLabel("페이드아웃할 음악")] private BackgroundMusicFader musicToFadeOut;
    [SerializeField, KoreanLabel("페이드인할 음악")] private BackgroundMusicFader musicToFadeIn;
    [SerializeField, KoreanLabel("음악 페이드 시간"), Min(0f)] private float musicFadeDuration = 0.5f;
    [SerializeField, KoreanLabel("곡 전환 방식")] private MusicTransitionTiming musicTransitionTiming;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        waveManager = GetComponentInParent<WaveManager>();
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnValidate()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartWave(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartWave(other);
    }

    private void TryStartWave(Collider2D other)
    {
        if (!isActiveAndEnabled || waveManager == null || waveManager.HasStarted
            || (playerLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        ArisaHorizontalMovement player = other.GetComponentInParent<ArisaHorizontalMovement>();
        if (player == null || (requiredPlayer != null && player != requiredPlayer))
        {
            return;
        }
        if (!waveManager.TryStartWave())
        {
            return;
        }

        if (musicToFadeOut != null && musicToFadeOut != musicToFadeIn)
        {
            musicToFadeOut.FadeOut(musicFadeDuration);
        }
        if (musicToFadeIn != null && musicToFadeIn != musicToFadeOut)
        {
            if (musicToFadeOut != null && musicTransitionTiming == MusicTransitionTiming.Sequential
                && musicFadeDuration > 0f)
            {
                StartCoroutine(FadeInAfterOut());
            }
            else
            {
                musicToFadeIn.FadeIn(musicFadeDuration);
            }
        }
    }

    private IEnumerator FadeInAfterOut()
    {
        yield return new WaitForSecondsRealtime(musicFadeDuration);
        if (musicToFadeIn != null)
        {
            musicToFadeIn.FadeIn(musicFadeDuration);
        }
    }
}
