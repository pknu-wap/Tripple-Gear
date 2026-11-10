using UnityEngine;
using UnityEngine.Events;

// Keep this controller active; use the events to enable/disable chapter content.
[DisallowMultipleComponent]
public sealed class ChapterController : MonoBehaviour
{
    [SerializeField, KoreanLabel("챕터 시작")] private UnityEvent onStarted = new UnityEvent();
    [SerializeField, KoreanLabel("챕터 완료")] private UnityEvent onCompleted = new UnityEvent();
    [SerializeField, KoreanLabel("챕터 취소")] private UnityEvent onCancelled = new UnityEvent();

    public event System.Action<ChapterController> Completed;
    public bool HasStarted { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsCancelled { get; private set; }
    public UnityEvent Started => onStarted;

    public bool BeginChapter()
    {
        if (!isActiveAndEnabled || HasStarted) return false;
        HasStarted = true;
        onStarted.Invoke();
        return true;
    }

    // Connect the final objective (e.g. the final WaveCompleted event) here.
    public void CompleteChapter()
    {
        if (!HasStarted || IsCompleted || IsCancelled) return;
        IsCompleted = true;
        onCompleted.Invoke();
        Completed?.Invoke(this);
    }

    public void CancelChapter()
    {
        if (!HasStarted || IsCompleted || IsCancelled) return;
        IsCancelled = true;
        onCancelled.Invoke();
    }
}
