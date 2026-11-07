using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Index-based progression. Loading scenes is the lobby's responsibility.
[DisallowMultipleComponent]
public sealed class InGameSequenceController : MonoBehaviour
{
    public enum SequenceState { Ready, Intro, Playing, Completed, Cancelled }

    [SerializeField, KoreanLabel("씬 시작 시 자동 진행 (테스트용)")] private bool playOnStart = false;
    [SerializeField, KoreanLabel("인트로 (선택)")] private TimelineCutscenePlayer intro;
    [SerializeField, KoreanLabel("진행 순서")] private ChapterController[] chapters = new ChapterController[0];
    [SerializeField, KoreanLabel("플레이 시작")] private UnityEvent onGameplayStarted = new UnityEvent();
    [SerializeField, KoreanLabel("전체 챕터 완료")] private UnityEvent onSequenceCompleted = new UnityEvent();

    public SequenceState State { get; private set; }
    public int CurrentChapterIndex { get; private set; } = -1;
    public UnityEvent GameplayStarted => onGameplayStarted;
    public UnityEvent SequenceCompleted => onSequenceCompleted;
    private ChapterController currentChapter;

    private void Start()
    {
        if (playOnStart) BeginSequence();
    }

    public void BeginSequence()
    {
        if (!isActiveAndEnabled || State != SequenceState.Ready || !ValidateChapters()) return;
        if (intro == null)
        {
            BeginGameplay();
            return;
        }

        State = SequenceState.Intro;
        intro.Completed += HandleIntroCompleted;
        intro.Cancelled += HandleIntroCancelled;
        if (!intro.TryPlay()) HandleIntroCancelled();
    }

    private bool ValidateChapters()
    {
        var seen = new HashSet<ChapterController>();
        if (chapters == null) chapters = new ChapterController[0];
        foreach (ChapterController chapter in chapters)
        {
            if (chapter == null || !seen.Add(chapter) || chapter.HasStarted
                || !chapter.isActiveAndEnabled || chapter.gameObject.scene != gameObject.scene)
            {
                Debug.LogError("챕터 목록에는 같은 씬의 활성화된 챕터를 중복 없이 등록해 주세요.", this);
                return false;
            }
        }
        return true;
    }

    private void HandleIntroCompleted()
    {
        if (State != SequenceState.Intro) return;
        UnsubscribeIntro();
        BeginGameplay();
    }

    private void HandleIntroCancelled()
    {
        if (State != SequenceState.Intro) return;
        UnsubscribeIntro();
        State = SequenceState.Ready;
    }

    private void BeginGameplay()
    {
        State = SequenceState.Playing;
        onGameplayStarted.Invoke();
        // A sample without chapters remains playable; it is not a cleared game.
        if (State == SequenceState.Playing && chapters.Length > 0) AdvanceChapter();
    }

    private void AdvanceChapter()
    {
        DetachChapter();
        CurrentChapterIndex++;
        if (CurrentChapterIndex >= chapters.Length)
        {
            State = SequenceState.Completed;
            onSequenceCompleted.Invoke();
            return;
        }

        currentChapter = chapters[CurrentChapterIndex];
        if (currentChapter != null) currentChapter.Completed += HandleChapterCompleted;
        if (currentChapter == null || !currentChapter.BeginChapter())
        {
            Debug.LogError("다음 챕터를 시작할 수 없습니다. 챕터의 활성화 상태를 확인해 주세요.", this);
            CancelSequence();
        }
    }

    private void HandleChapterCompleted(ChapterController chapter)
    {
        if (State == SequenceState.Playing && chapter == currentChapter) AdvanceChapter();
    }

    public void CancelSequence()
    {
        if (State == SequenceState.Completed || State == SequenceState.Cancelled) return;
        State = SequenceState.Cancelled;
        UnsubscribeIntro();
        if (intro != null) intro.Cancel();
        ChapterController previous = currentChapter;
        DetachChapter();
        if (previous != null) previous.CancelChapter();
    }

    private void DetachChapter()
    {
        if (currentChapter != null) currentChapter.Completed -= HandleChapterCompleted;
        currentChapter = null;
    }

    private void UnsubscribeIntro()
    {
        if (intro == null) return;
        intro.Completed -= HandleIntroCompleted;
        intro.Cancelled -= HandleIntroCancelled;
    }

    private void OnDisable()
    {
        CancelSequence();
    }
}
