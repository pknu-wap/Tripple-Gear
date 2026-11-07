using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisallowMultipleComponent]
public sealed class TimelineCutscenePlayer : MonoBehaviour
{
    [Header("컷신 재생")]
    [SerializeField, KoreanLabel("인트로 타임라인")] private PlayableDirector introTimeline;

    [Header("타임라인 종료")]
    [SerializeField, KoreanLabel("마지막 애니메이션 클립에서 완료")] private bool completeWhenLastAnimationClipEnds = true;
    [SerializeField, KoreanLabel("끝까지 재생됐을 때만 완료")] private bool onlyCompleteWhenReachedEnd = true;
    [SerializeField, KoreanLabel("완료하면 타임라인 정지")] private bool stopTimelineOnComplete = true;
    [SerializeField, KoreanLabel("완료 프레임 고정")] private bool freezeAnimatorsOnComplete = true;
    [SerializeField, KoreanLabel("추가 고정 애니메이터")] private Animator[] extraAnimatorsToFreeze;
    [SerializeField, KoreanLabel("완료 디버그 메시지")] private string completedLogMessage = "컷신 재생 완료";
    [SerializeField, KoreanLabel("완료 후 호출")] private UnityEvent onTimelineCompleted;

    private const double TimelineEndTolerance = 0.001d;

    private readonly List<Animator> animatorsToFreeze = new List<Animator>();

    private double completionTime = -1d;
    private bool isTimelinePlaying;
    private bool isTimelineCompleted;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
        PrepareTimeline();
    }

    private void OnValidate()
    {
        DisableTimelinePlayOnAwake();
    }

    public event System.Action Completed;
    public event System.Action Cancelled;
    public bool IsPlaying => isTimelinePlaying;
    public bool IsCompleted => isTimelineCompleted;

    private void OnEnable() => SubscribeTimeline();

    private void OnDisable()
    {
        UnsubscribeTimeline();
        Cancel();
    }

    public void Cancel()
    {
        if (!isTimelinePlaying) return;
        isTimelinePlaying = false;
        if (introTimeline != null) introTimeline.Stop();
        Cancelled?.Invoke();
    }

    private void Update()
    {
        if (!isTimelinePlaying || isTimelineCompleted || introTimeline == null)
        {
            return;
        }

        if (HasTimelineReachedEnd())
        {
            CompleteTimeline();
        }
    }

    // Void entry point for Inspector UnityEvents (standalone cutscenes).
    public void Play() => TryPlay();

    public bool TryPlay()
    {
        if (!isActiveAndEnabled || isTimelinePlaying) return false;
        FindReferences();
        if (introTimeline == null || introTimeline.playableAsset == null)
        {
            Debug.LogError("컷신 타임라인과 재생 에셋을 연결해 주세요.", this);
            return false;
        }
        SubscribeTimeline();
        // Hold the end time so Update can detect completion even on a long frame.
        introTimeline.extrapolationMode = DirectorWrapMode.Hold;
        CacheAnimatorsToFreeze();
        SetAnimatorsEnabled(true);
        completionTime = GetCompletionTime();
        isTimelinePlaying = true;
        isTimelineCompleted = false;

        introTimeline.time = 0d;
        introTimeline.Evaluate();
        introTimeline.Play();
        return true;
    }

    public void DebugTimelineCompleted()
    {
        Debug.Log(completedLogMessage, this);
    }

    private void HandleTimelineStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector != introTimeline || !isTimelinePlaying || isTimelineCompleted)
        {
            return;
        }

        if (onlyCompleteWhenReachedEnd && !HasTimelineReachedEnd())
        {
            isTimelinePlaying = false;
            Cancelled?.Invoke();
            return;
        }

        CompleteTimeline();
    }

    private void CompleteTimeline()
    {
        if (!isTimelinePlaying || isTimelineCompleted) return;
        isTimelinePlaying = false;
        isTimelineCompleted = true;
        ForceTimelineToCompletionFrame();

        if (freezeAnimatorsOnComplete)
        {
            SetAnimatorsEnabled(false);
        }

        if (stopTimelineOnComplete && introTimeline != null && introTimeline.state == PlayState.Playing)
        {
            introTimeline.Stop();
        }

        DebugTimelineCompleted();
        onTimelineCompleted?.Invoke();
        Completed?.Invoke();
    }

    private void PrepareTimeline()
    {
        DisableTimelinePlayOnAwake();

        if (!Application.isPlaying)
        {
            return;
        }

        if (introTimeline == null)
        {
            return;
        }

        if (introTimeline.state == PlayState.Playing)
        {
            introTimeline.Stop();
        }

        introTimeline.time = 0d;
        introTimeline.Evaluate();
    }

    private void DisableTimelinePlayOnAwake()
    {
        if (introTimeline != null)
        {
            introTimeline.playOnAwake = false;
        }
    }

    private bool HasTimelineReachedEnd()
    {
        double endTime = completionTime > 0d ? completionTime : GetCompletionTime();
        if (introTimeline == null || endTime <= 0d)
        {
            return true;
        }

        return introTimeline.time >= endTime - TimelineEndTolerance;
    }

    private double GetCompletionTime()
    {
        if (introTimeline == null)
        {
            return 0d;
        }

        if (completeWhenLastAnimationClipEnds && introTimeline.playableAsset is TimelineAsset timelineAsset)
        {
            double lastAnimationClipEnd = GetLastAnimationClipEnd(timelineAsset);
            if (lastAnimationClipEnd > 0d)
            {
                return lastAnimationClipEnd;
            }
        }

        return double.IsInfinity(introTimeline.duration) ? 0d : introTimeline.duration;
    }

    private static double GetLastAnimationClipEnd(TimelineAsset timelineAsset)
    {
        double lastAnimationClipEnd = 0d;

        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (!(track is AnimationTrack))
            {
                continue;
            }

            foreach (TimelineClip clip in track.GetClips())
            {
                lastAnimationClipEnd = System.Math.Max(lastAnimationClipEnd, clip.end);
            }
        }

        return lastAnimationClipEnd;
    }

    private void ForceTimelineToCompletionFrame()
    {
        if (introTimeline == null)
        {
            return;
        }

        double endTime = completionTime > 0d ? completionTime : GetCompletionTime();
        if (endTime > 0d)
        {
            introTimeline.time = endTime;
            introTimeline.Evaluate();
        }
    }

    private void CacheAnimatorsToFreeze()
    {
        animatorsToFreeze.Clear();

        if (introTimeline != null && introTimeline.playableAsset != null)
        {
            foreach (PlayableBinding output in introTimeline.playableAsset.outputs)
            {
                if (!(output.sourceObject is AnimationTrack))
                {
                    continue;
                }

                AddAnimatorToFreeze(introTimeline.GetGenericBinding(output.sourceObject));
            }
        }

        if (extraAnimatorsToFreeze == null)
        {
            return;
        }

        foreach (Animator animator in extraAnimatorsToFreeze)
        {
            AddAnimatorToFreeze(animator);
        }
    }

    private void AddAnimatorToFreeze(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (target is Animator animator)
        {
            AddAnimatorToFreeze(animator);
            return;
        }

        if (target is GameObject targetObject)
        {
            AddAnimatorToFreeze(targetObject.GetComponent<Animator>());
            return;
        }

        if (target is Component component)
        {
            AddAnimatorToFreeze(component.GetComponent<Animator>());
        }
    }

    private void AddAnimatorToFreeze(Animator animator)
    {
        if (animator != null && !animatorsToFreeze.Contains(animator))
        {
            animatorsToFreeze.Add(animator);
        }
    }

    private void SetAnimatorsEnabled(bool enabled)
    {
        foreach (Animator animator in animatorsToFreeze)
        {
            if (animator != null)
            {
                animator.enabled = enabled;
            }
        }
    }

    private void SubscribeTimeline()
    {
        if (introTimeline != null)
        {
            introTimeline.stopped -= HandleTimelineStopped;
            introTimeline.stopped += HandleTimelineStopped;
        }
    }

    private void UnsubscribeTimeline()
    {
        if (introTimeline != null)
        {
            introTimeline.stopped -= HandleTimelineStopped;
        }
    }

    private void FindReferences()
    {
        if (introTimeline == null)
        {
            introTimeline = GetComponentInChildren<PlayableDirector>();
        }

    }
}
