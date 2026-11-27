using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChapterMusic : MonoBehaviour
{
    [SerializeField] private AudioClip chapter2;
    [SerializeField] private AudioClip chapter2Boss;
    [SerializeField] private AudioClip chapter3;
    [SerializeField] private AudioClip chapter3Boss;
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
    [SerializeField] private BackgroundMusicFader chapter1Music;
    [SerializeField] private BackgroundMusicFader chapter1BossMusic;

    private AudioSource source;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
    }

    private void OnEnable()
    {
        ApplyMusicLevel(GameAudioSettings.Music);
        GameAudioSettings.MusicChanged += ApplyMusicLevel;
    }

    private void OnDisable() => GameAudioSettings.MusicChanged -= ApplyMusicLevel;

    private void ApplyMusicLevel(float level) => source.volume = volume * level;

    public void PlayChapter(int chapter) => Play(chapter switch { 2 => chapter2, 3 => chapter3, _ => null });

    public void PlayBoss(int chapter) => Play(chapter switch { 2 => chapter2Boss, 3 => chapter3Boss, _ => null });

    internal void Stop()
    {
        if (source == null) return;
        source.Stop();
        source.clip = null;
    }

    private void Play(AudioClip track)
    {
        if (track == null || !isActiveAndEnabled || (source.clip == track && source.isPlaying)) return;

        if (chapter1Music != null) chapter1Music.FadeOut(0f);
        if (chapter1BossMusic != null) chapter1BossMusic.FadeOut(0f);
        source.Stop();
        source.clip = track;
        source.Play();
    }
}
