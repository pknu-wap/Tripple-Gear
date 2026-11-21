using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

public partial class Trigger
{
    public enum MissionState { Ready, Running, PlayingClear, Completed, Cancelled, PlayingIntro }

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

    [SerializeField, KoreanLabel("시작 타임라인"), Tooltip("재생이 끝나면 미션 목표와 적 출현이 시작됩니다. Play On Awake를 꺼 주세요.")]
    private PlayableDirector missionIntroTimeline;
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
    private ArisaHorizontalMovement missionPlayer;
    private ArisaHealth missionPlayerHealth;
    private int currentObjectiveIndex;
    private int completedObjectives;
    private bool missionOwnsCamera;
    private bool reportedInvalidMission;
    private PlayableDirector playingMissionTimeline;
    private PlayableDirector playingMissionIntro;
    private DirectorWrapMode missionIntroPreviousWrapMode;
    private BoatVoyage activeMissionBoat;
    private bool waitingForMissionStart;

    internal bool IsMissionForBoat(BoatVoyage boat)
    {
        if (!HasMissionObjectives) return false;
        foreach (MissionObjective objective in missionObjectives)
        {
            if (objective != null && objective.BattleBoat == boat) return true;
        }
        return false;
    }

    public void ResetMissionForRetry()
    {
        CancelMission();
        StopEnemySpawns(true);
        foreach (MissionObjective objective in missionObjectives)
        {
            if (objective != null && objective.Owner == null)
            {
                objective.ResetProgress();
            }
        }

        currentObjectiveIndex = 0;
        completedObjectives = 0;
        CurrentMissionState = MissionState.Ready;
    }

    private bool StartMission(ArisaHorizontalMovement player)
    {
        if (CurrentMissionState != MissionState.Ready || State == WaveState.Running)
        {
            return false;
        }

        if (!ValidateEnemySpawns())
        {
            ReportInvalidMission("적 출현 그룹에 몬스터 프리팹과 씬의 출현 지점을 연결해 주세요.");
            return false;
        }

        if (missionIntroTimeline != null && (!missionIntroTimeline.isActiveAndEnabled
            || missionIntroTimeline.playableAsset == null || missionIntroTimeline.state == PlayState.Playing))
        {
            ReportInvalidMission("시작 타임라인에 활성 Playable Director와 타임라인을 연결하고 Play On Awake를 꺼 주세요.");
            return false;
        }

        ArisaHealth playerHealth = player != null ? player.GetComponent<ArisaHealth>() : null;
        if (playerHealth != null && playerHealth.IsDead)
        {
            return false;
        }

        var objectives = new HashSet<MissionObjective>();
        MissionObjective boatObjective = null;
        foreach (MissionObjective objective in missionObjectives)
        {
            if (objective == null || !objective.isActiveAndEnabled || objective.Owner != null
                || objective.IsCompleted || !objectives.Add(objective))
            {
                ReportInvalidMission("목표 목록에 활성화된 미사용 목표를 각각 한 번씩 연결해 주세요.");
                return false;
            }
            if (!objective.HasValidTargets())
            {
                ReportInvalidMission("지정 몬스터 목표에는 씬에 배치된 서로 다른 몬스터를 연결해 주세요.");
                return false;
            }
            if (objective.BattleBoat != null)
            {
                if (boatObjective != null)
                {
                    ReportInvalidMission("미션의 목표 목록에서 항해 전투 보트는 한 목표에만 연결해 주세요.");
                    return false;
                }
                boatObjective = objective;
            }
        }

        BoatVoyage boat = boatObjective != null ? boatObjective.BattleBoat : null;
        if (missionLockCamera && boat == null)
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
        }

        if (boat != null && !boat.PrepareBattle(this, player, boatObjective.DockBeforeBattle))
        {
            return false;
        }

        activeMissionBoat = boat;
        if (missionLockCamera && activeMissionBoat == null)
        {
            LockCamera();
            missionOwnsCamera = true;
        }

        missionPlayer = player;
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

        if (activeMissionBoat != null && !activeMissionBoat.IsBattleReady(this))
        {
            waitingForMissionStart = true;
            CurrentMissionState = MissionState.PlayingIntro;
        }
        else
        {
            StartMissionIntro();
        }
        return true;
    }

    private void StartMissionIntro()
    {
        waitingForMissionStart = false;
        if (missionIntroTimeline == null)
        {
            BeginMissionGameplay();
            return;
        }

        CurrentMissionState = MissionState.PlayingIntro;
        playingMissionIntro = missionIntroTimeline;
        missionIntroPreviousWrapMode = playingMissionIntro.extrapolationMode;
        // Keep the last sample until we observe completion and release the graph.
        playingMissionIntro.extrapolationMode = DirectorWrapMode.Hold;
        playingMissionIntro.stopped += OnMissionIntroStopped;
        playingMissionIntro.Play();
    }

    private void BeginMissionGameplay()
    {
        CurrentMissionState = MissionState.Running;
        StartEnemySpawns(missionPlayer, true);
        missionPlayer = null;

        activeMissionBoat?.BeginBattle();
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
    }

    private void UpdateMissionIntro()
    {
        if (waitingForMissionStart)
        {
            if (activeMissionBoat == null) CancelMission();
            else if (activeMissionBoat.IsBattleReady(this)) StartMissionIntro();
            return;
        }

        if (playingMissionIntro == null || !playingMissionIntro.isActiveAndEnabled
            || (missionOwnsCamera && (lockedCamera == null || lockedManager == null)))
        {
            CancelMission();
            return;
        }

        if (playingMissionIntro.time + 0.00001d < playingMissionIntro.duration)
        {
            return;
        }

        ReleaseMissionIntro(stopPlayback: true);
        if (CurrentMissionState != MissionState.PlayingIntro)
        {
            return;
        }

        if (missionOwnsCamera && lockedCamera != null)
        {
            lockedPosition = lockedCamera.transform.position;
        }
        BeginMissionGameplay();
    }

    private void OnMissionIntroStopped(PlayableDirector director)
    {
        ReleaseMissionIntro(stopPlayback: false);
        CancelMission();
    }

    private void ReleaseMissionIntro(bool stopPlayback)
    {
        PlayableDirector director = playingMissionIntro;
        playingMissionIntro = null;
        if (director != null)
        {
            director.stopped -= OnMissionIntroStopped;
            if (stopPlayback && director.playableGraph.IsValid()) director.Stop();
            director.extrapolationMode = missionIntroPreviousWrapMode;
        }
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
        if (!continueSpawningAfterMissionClear)
        {
            StopEnemySpawns(clearMissionSpawnedEnemies);
        }
        else if (clearMissionSpawnedEnemies)
        {
            foreach (RunningSpawnGroup group in runningSpawns)
            {
                ReleaseSpawnedEnemies(group, true);
            }
        }
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
        activeMissionBoat?.FinishBattle();
        ReleaseMission();
        missionCompleted.Invoke();
    }

    public void CancelMission()
    {
        if (CurrentMissionState != MissionState.PlayingIntro && CurrentMissionState != MissionState.Running
            && CurrentMissionState != MissionState.PlayingClear)
        {
            return;
        }

        CurrentMissionState = MissionState.Cancelled;
        waitingForMissionStart = false;
        ReleaseMissionIntro(stopPlayback: true);
        StopEnemySpawns(true);
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
        activeMissionBoat = null;
        missionPlayer = null;
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

    private void UpdateMission()
    {
        if (CurrentMissionState == MissionState.PlayingIntro)
        {
            UpdateMissionIntro();
            return;
        }
        if (CurrentMissionState != MissionState.Running)
        {
            if (continueSpawningAfterMissionClear
                && (CurrentMissionState == MissionState.PlayingClear || CurrentMissionState == MissionState.Completed))
            {
                UpdateEnemySpawns();
            }
            return;
        }
        if (!missionOwnsCamera)
        {
            UpdateEnemySpawns();
            return;
        }
        if (lockedCamera == null || lockedManager == null)
        {
            CancelMission();
            return;
        }

        lockedCamera.transform.position = lockedPosition;
        UpdateCameraBoundaries();
        UpdateEnemySpawns();
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
