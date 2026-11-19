using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

public partial class Trigger
{
    public enum MissionState { Ready, Running, PlayingClear, Completed, Cancelled }

    private enum MissionCombination
    {
        [InspectorName("모두 달성")] All,
        [InspectorName("순서대로 달성")] Sequential,
        [InspectorName("하나만 달성")] Any,
    }

    public enum ClearPresentation
    {
        [InspectorName("카메라 복구")] RestoreCamera,
        Animator,
        Timeline,
    }

    [SerializeField, KoreanLabel("미션 목표 목록")] private MissionObjective[] missionObjectives;
    [SerializeField, KoreanLabel("목표 조합 방식")] private MissionCombination missionCombination;
    [SerializeField, KoreanLabel("진행 중 카메라 고정")] private bool missionLockCamera;
    [SerializeField, KoreanLabel("클리어 처리")] private ClearPresentation missionPresentation;
    [SerializeField, KoreanLabel("클리어 애니메이터")] private Animator missionAnimator;
    [SerializeField, KoreanLabel("Animator Trigger 파라미터")] private string missionAnimatorTrigger = "Clear";
    [SerializeField, KoreanLabel("클리어 타임라인")] private PlayableDirector missionTimeline;
    [SerializeField, KoreanLabel("미션 시작 이벤트")] private UnityEvent missionStarted = new UnityEvent();
    [SerializeField, KoreanLabel("미션 종료 이벤트")] private UnityEvent missionCompleted = new UnityEvent();

    public MissionState CurrentMissionState { get; private set; }

    private bool HasMissionObjectives => missionObjectives != null && missionObjectives.Length > 0;
    private ArisaHealth missionPlayerHealth;
    private int currentObjectiveIndex;
    private int completedObjectives;
    private bool missionOwnsCamera;
    private bool reportedInvalidMission;
    private PlayableDirector playingMissionTimeline;

    private bool StartMission(ArisaHorizontalMovement player)
    {
        if (CurrentMissionState != MissionState.Ready || State == WaveState.Running)
        {
            return false;
        }

        ArisaHealth playerHealth = player != null ? player.GetComponent<ArisaHealth>() : null;
        if (playerHealth != null && playerHealth.IsDead)
        {
            return false;
        }

        var objectives = new HashSet<MissionObjective>();
        foreach (MissionObjective objective in missionObjectives)
        {
            if (objective == null || !objective.isActiveAndEnabled || objective.Owner != null
                || objective.IsCompleted || !objectives.Add(objective))
            {
                ReportInvalidMission("목표 목록에 활성화된 미사용 목표를 각각 한 번씩 연결해 주세요.");
                return false;
            }
        }

        if (missionLockCamera)
        {
            if (cameraManager == null || cameraManager.ManagedCamera == null || !cameraManager.ManagedCamera.orthographic
                || LayerMask.NameToLayer(CameraBoundaryLayerName) < 0)
            {
                ReportInvalidMission("카메라를 고정하려면 직교 카메라와 WaveBoundary 레이어가 필요합니다.");
                return false;
            }
            if (!cameraManager.isActiveAndEnabled)
            {
                return false;
            }

            LockCamera();
            missionOwnsCamera = true;
        }

        CurrentMissionState = MissionState.Running;
        missionPlayerHealth = playerHealth;
        if (missionPlayerHealth != null)
        {
            missionPlayerHealth.Defeated.AddListener(CancelMission);
        }
        foreach (MissionObjective objective in missionObjectives)
        {
            objective.Owner = this;
            objective.Completed += HandleObjectiveCompleted;
        }

        missionStarted.Invoke();
        if (CurrentMissionState == MissionState.Running)
        {
            if (missionCombination == MissionCombination.Sequential)
            {
                missionObjectives[0].Begin();
            }
            else
            {
                foreach (MissionObjective objective in missionObjectives)
                {
                    if (CurrentMissionState != MissionState.Running)
                    {
                        break;
                    }
                    objective.Begin();
                }
            }
        }
        return true;
    }

    private void HandleObjectiveCompleted(MissionObjective objective)
    {
        if (CurrentMissionState != MissionState.Running)
        {
            return;
        }

        completedObjectives++;
        if (missionCombination == MissionCombination.Any || completedObjectives == missionObjectives.Length)
        {
            ClearMission();
        }
        else if (missionCombination == MissionCombination.Sequential)
        {
            missionObjectives[++currentObjectiveIndex].Begin();
        }
    }

    private void ClearMission()
    {
        CurrentMissionState = MissionState.PlayingClear;
        StopMissionObjectives();
        if (CurrentMissionState != MissionState.PlayingClear)
        {
            return;
        }

        // The presentation can move the camera while normal follow remains suspended.
        if (missionOwnsCamera)
        {
            RemoveCameraBoundaries();
        }

        switch (missionPresentation)
        {
            case ClearPresentation.Animator when HasAnimatorTrigger():
                missionAnimator.SetTrigger(missionAnimatorTrigger);
                return;
            case ClearPresentation.Timeline when missionTimeline != null && missionTimeline.isActiveAndEnabled
                && missionTimeline.playableAsset != null:
                playingMissionTimeline = missionTimeline;
                playingMissionTimeline.stopped += OnMissionTimelineStopped;
                playingMissionTimeline.Play();
                return;
            case ClearPresentation.RestoreCamera:
                break;
            default:
                Debug.LogWarning("클리어 연출을 재생할 수 없어 미션을 바로 종료합니다.", this);
                break;
        }

        FinishMissionPresentation();
    }

    // Connect the final Timeline Signal or Animation Event to this method.
    public void FinishMissionPresentation()
    {
        if (CurrentMissionState != MissionState.PlayingClear)
        {
            return;
        }

        CurrentMissionState = MissionState.Completed;
        ReleaseMission();
        missionCompleted.Invoke();
    }

    public void CancelMission()
    {
        if (CurrentMissionState != MissionState.Running && CurrentMissionState != MissionState.PlayingClear)
        {
            return;
        }

        CurrentMissionState = MissionState.Cancelled;
        PlayableDirector timeline = playingMissionTimeline;
        StopMissionObjectives();
        ReleaseMission();
        if (timeline != null && timeline.state == PlayState.Playing)
        {
            timeline.Stop();
        }
    }

    private void StopMissionObjectives()
    {
        foreach (MissionObjective objective in missionObjectives)
        {
            if (objective != null && objective.Owner == this)
            {
                objective.Completed -= HandleObjectiveCompleted;
                objective.Stop();
                objective.Owner = null;
            }
        }
    }

    private void ReleaseMission()
    {
        if (playingMissionTimeline != null)
        {
            playingMissionTimeline.stopped -= OnMissionTimelineStopped;
        }
        playingMissionTimeline = null;

        if (missionPlayerHealth != null)
        {
            missionPlayerHealth.Defeated.RemoveListener(CancelMission);
        }
        missionPlayerHealth = null;

        if (missionOwnsCamera)
        {
            missionOwnsCamera = false;
            RestoreCamera();
        }
    }

    private void OnMissionTimelineStopped(PlayableDirector director) => CancelMission();

    private void UpdateMissionCamera()
    {
        if (!missionOwnsCamera || CurrentMissionState != MissionState.Running)
        {
            return;
        }
        if (lockedCamera == null || lockedManager == null)
        {
            CancelMission();
            return;
        }

        lockedCamera.transform.position = lockedPosition;
        UpdateCameraBoundaries();
    }

    private void ReportInvalidMission(string message)
    {
        if (!reportedInvalidMission)
        {
            reportedInvalidMission = true;
            Debug.LogWarning(message, this);
        }
    }

    private bool HasAnimatorTrigger()
    {
        if (missionAnimator == null || !missionAnimator.isActiveAndEnabled || string.IsNullOrWhiteSpace(missionAnimatorTrigger))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in missionAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == missionAnimatorTrigger)
            {
                return true;
            }
        }
        return false;
    }
}
