using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameFlowController : MonoBehaviour
{
    [Header("게임 시작")]
    [SerializeField, KoreanLabel("인트로 타임라인")] private PlayableDirector introTimeline;
    [SerializeField, KoreanLabel("컷신 중 입력을 막을 플레이어")] private Transform gameplayPlayer;
    [SerializeField, KoreanLabel("시작 신호를 보낼 씬 (선택)"), Tooltip("비워 두면 어느 씬에서 보낸 전환 신호든 받습니다.")]
    private string cutsceneSignalSourceSceneName;
    [SerializeField, KoreanLabel("게임 시작 버튼")] private Button startButton;
    [SerializeField, KoreanLabel("재생하면 버튼 숨기기")] private bool hideStartButtonOnPlay = true;

    [Header("타임라인 종료")]
    [SerializeField, KoreanLabel("마지막 애니메이션 클립에서 완료")] private bool completeWhenLastAnimationClipEnds = true;
    [SerializeField, KoreanLabel("끝까지 재생됐을 때만 완료")] private bool onlyCompleteWhenReachedEnd = true;
    [SerializeField, KoreanLabel("완료하면 타임라인 정지")] private bool stopTimelineOnComplete = true;
    [SerializeField, KoreanLabel("완료 프레임 고정")] private bool freezeAnimatorsOnComplete = true;
    [SerializeField, KoreanLabel("추가 고정 애니메이터")] private Animator[] extraAnimatorsToFreeze;
    [SerializeField, KoreanLabel("완료 디버그 메시지")] private string completedLogMessage = "타임라인 종료: 게임 시작 로직 호출";
    [SerializeField, KoreanLabel("완료 후 호출")] private UnityEvent onTimelineCompleted;

    private const double TimelineEndTolerance = 0.001d;

    private readonly List<Animator> animatorsToFreeze = new List<Animator>();
    private readonly List<Behaviour> suspendedGameplayInput = new List<Behaviour>();

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

    private void OnEnable()
    {
        FindReferences();
        SubscribeTimeline();

        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
        }
    }

    private void Start()
    {
        string currentSceneName = gameObject.scene.name;
        if (SceneTransitionSignalReceiver.TryConsumeSceneTransition(cutsceneSignalSourceSceneName, currentSceneName))
        {
            StartGame();
        }
    }

    private void OnDisable()
    {
        UnsubscribeTimeline();
        ResumeGameplayInput();

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }
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

    public void StartGame()
    {
        if (!isActiveAndEnabled || isTimelinePlaying || isTimelineCompleted)
        {
            return;
        }

        if (introTimeline == null)
        {
            isTimelineCompleted = true;
            SetStartButtonVisible(false);
            onTimelineCompleted?.Invoke();
            return;
        }

        if (introTimeline.playableAsset == null)
        {
            Debug.LogWarning("인트로 타임라인에 재생할 에셋이 연결되지 않았습니다.", this);
            return;
        }

        CacheAnimatorsToFreeze();
        SetAnimatorsEnabled(true);
        completionTime = GetCompletionTime();
        isTimelinePlaying = true;
        SetStartButtonVisible(false);
        SuspendGameplayInput();
        GameplaySfx.Play(GameplaySfx.Cue.Encounter, .42f);

        introTimeline.time = 0d;
        introTimeline.Evaluate();
        introTimeline.Play();
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
            ResumeGameplayInput();
            SetStartButtonVisible(true);
            return;
        }

        CompleteTimeline();
    }

    internal void SkipIntroForRetry()
    {
        CacheAnimatorsToFreeze();
        SetStartButtonVisible(false);
        CompleteTimeline(notifyCompletion: false);
        if (gameplayPlayer != null) gameplayPlayer.gameObject.SetActive(true);
    }

    private void CompleteTimeline(bool notifyCompletion = true)
    {
        ForceTimelineToCompletionFrame();

        if (freezeAnimatorsOnComplete)
        {
            SetAnimatorsEnabled(false);
        }

        isTimelinePlaying = false;
        isTimelineCompleted = true;

        if (introTimeline != null && (notifyCompletion
            ? stopTimelineOnComplete && introTimeline.state == PlayState.Playing
            : introTimeline.playableGraph.IsValid()))
        {
            introTimeline.Stop();
        }

        ResumeGameplayInput();
        if (!notifyCompletion) return;
        DebugTimelineCompleted();
        onTimelineCompleted?.Invoke();
    }

    private void SuspendGameplayInput()
    {
        if (gameplayPlayer == null)
        {
            return;
        }

        SuspendInput(gameplayPlayer.GetComponent<ArisaHorizontalMovement>());
        SuspendInput(gameplayPlayer.GetComponent<ArisaMouseAim>());
        SuspendInput(gameplayPlayer.GetComponent<SubmachineGunFire>());
        SuspendInput(gameplayPlayer.GetComponent<ArisaSkillController>());
    }

    private void SuspendInput(Behaviour input)
    {
        if (input == null || !input.enabled)
        {
            return;
        }

        input.enabled = false;
        suspendedGameplayInput.Add(input);
    }

    private void ResumeGameplayInput()
    {
        foreach (Behaviour input in suspendedGameplayInput)
        {
            if (input != null)
            {
                input.enabled = true;
            }
        }

        suspendedGameplayInput.Clear();
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

    private void SetStartButtonVisible(bool visible)
    {
        if (startButton == null)
        {
            return;
        }

        startButton.interactable = visible;

        if (hideStartButtonOnPlay)
        {
            startButton.gameObject.SetActive(visible);
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

        if (startButton == null)
        {
            startButton = GetComponentInChildren<Button>(true);
        }
    }
}
