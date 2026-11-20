using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class MissionObjective : MonoBehaviour
{
    public enum GoalType { [InspectorName("수량")] Count, [InspectorName("시간")] Duration }

    [SerializeField, KoreanLabel("목표 종류")] private GoalType goalType;
    [SerializeField, KoreanLabel("목표 수량"), Min(1)] private int requiredCount = 1;
    [SerializeField, KoreanLabel("버틸 시간 (초)"), Min(0.01f)] private float requiredSeconds = 10f;
    [SerializeField, KoreanLabel("목표 시작 이벤트")] private UnityEvent onStarted = new UnityEvent();
    [SerializeField, KoreanLabel("목표 종료 이벤트")] private UnityEvent onEnded = new UnityEvent();
    [SerializeField, KoreanLabel("진행도 변경 이벤트 (0~1)")] private UnityEvent<float> onProgressChanged = new UnityEvent<float>();

    public bool IsRunning { get; private set; }
    public bool IsCompleted { get; private set; }
    public int Count { get; private set; }
    public int RequiredCount => Mathf.Max(1, requiredCount);
    public float ElapsedSeconds { get; private set; }
    public int RemainingCount => IsRunning && goalType == GoalType.Count ? RequiredCount - Count : 0;
    public float Progress => goalType == GoalType.Count
        ? (float)Count / RequiredCount
        : ElapsedSeconds / Mathf.Max(0.01f, requiredSeconds);

    internal event Action<MissionObjective> Completed;
    internal Trigger Owner { get; set; }

    internal void Begin()
    {
        IsRunning = true;
        onStarted.Invoke();
    }

    internal void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        onEnded.Invoke();
    }

    public void RecordSuccess() => AddProgress(1);

    public void AddProgress(int amount)
    {
        int added = Mathf.Min(amount, RemainingCount);
        if (added > 0)
        {
            Count += added;
            PublishProgress();
        }
    }

    private void Update()
    {
        if (IsRunning && goalType == GoalType.Duration && Time.deltaTime > 0f)
        {
            ElapsedSeconds = Mathf.Min(ElapsedSeconds + Time.deltaTime, Mathf.Max(0.01f, requiredSeconds));
            PublishProgress();
        }
    }

    private void PublishProgress()
    {
        bool reachedTarget = goalType == GoalType.Count
            ? Count >= RequiredCount
            : ElapsedSeconds >= Mathf.Max(0.01f, requiredSeconds);
        if (!reachedTarget)
        {
            onProgressChanged.Invoke(Progress);
            return;
        }

        IsCompleted = true;
        Stop();
        onProgressChanged.Invoke(Progress);
        Completed?.Invoke(this);
    }
}
