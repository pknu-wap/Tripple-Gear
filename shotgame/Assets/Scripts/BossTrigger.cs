using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

[DefaultExecutionOrder(1000)]
public sealed class BossTrigger : TriggerBase
{
    private enum EncounterState { Ready, Intro, Battle, Finished }

    [Header("보스 등장")]
    [SerializeField, KoreanLabel("보스")] private MonsterHealth boss;
    [SerializeField, KoreanLabel("등장 연출 타임라인")] private PlayableDirector introTimeline;
    [SerializeField, KoreanLabel("처치 후 텔레포트")] private TeleportTrigger defeatTeleport;
    [SerializeField, KoreanLabel("처치 후 총 선택")] private GameManager gunSelectionManager;
    [SerializeField, KoreanLabel("전투 시작 이벤트")] private UnityEvent battleStarted = new UnityEvent();
    [SerializeField, KoreanLabel("보스 처치 이벤트")] private UnityEvent bossDefeated = new UnityEvent();

    [Header("전투 카메라와 음악")]
    [SerializeField, KoreanLabel("전투 중 카메라 고정")] private bool lockCameraDuringBattle = true;
    [SerializeField, KoreanLabel("등장 연출부터 카메라 고정")] private bool lockCameraDuringIntro;
    [SerializeField, KoreanLabel("카메라 매니저")] private CameraManager cameraManager;
    [SerializeField, KoreanLabel("보스 음악")] private BackgroundMusicFader bossMusic;
    [SerializeField, KoreanLabel("음악 페이드 시간"), Min(0f)] private float musicFadeDuration = 0.5f;

    private readonly EncounterCameraLock cameraLock = new EncounterCameraLock();
    private EncounterState state;
    private ArisaHealth playerHealth;
    private ArisaSkillController playerSkills;
    private PlayableDirector playingIntro;
    private DirectorWrapMode previousWrapMode;
    private bool reportedInvalidSetup;

    public void StartEncounter(ArisaHorizontalMovement player)
    {
        if (player != null) TryActivate(Manager, player);
    }

    internal void ResetEncounter()
    {
        CancelEncounter();
        if (boss != null) boss.gameObject.SetActive(false);
        state = EncounterState.Ready;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (state == EncounterState.Ready)
        {
            TryActivate(other, Manager);
        }
    }

    internal override bool Activate(TriggerManager manager, ArisaHorizontalMovement player)
    {
        if (state != EncounterState.Ready || player == null)
        {
            return false;
        }

        if (boss == null || boss.gameObject.scene != gameObject.scene || boss.gameObject.activeSelf
            || transform.IsChildOf(boss.transform)
            || (boss.transform.parent != null && !boss.transform.parent.gameObject.activeInHierarchy)
            || (lockCameraDuringBattle && !EncounterCameraLock.CanLock(cameraManager))
            || (introTimeline != null && (!introTimeline.isActiveAndEnabled || introTimeline.playableAsset == null)))
        {
            if (!reportedInvalidSetup)
            {
                Debug.LogWarning("BossTrigger의 보스, 카메라 및 등장 연출 연결을 확인해 주세요. 보스는 비활성 상태로 배치해야 합니다.", this);
                reportedInvalidSetup = true;
            }
            return false;
        }

        playerHealth = player.GetComponentInParent<ArisaHealth>();
        if ((playerHealth != null && playerHealth.IsDead)
            || (lockCameraDuringBattle && !cameraManager.isActiveAndEnabled))
        {
            return false;
        }

        playerSkills = player.GetComponentInParent<ArisaSkillController>();
        if (introTimeline == null)
        {
            BeginBattle();
        }
        else
        {
            state = EncounterState.Intro;
            if (lockCameraDuringBattle && lockCameraDuringIntro) cameraLock.Lock(cameraManager);
            playingIntro = introTimeline;
            previousWrapMode = playingIntro.extrapolationMode;
            playingIntro.extrapolationMode = DirectorWrapMode.Hold;
            playingIntro.stopped += OnIntroStopped;
            playingIntro.Play();
        }
        return true;
    }

    private void LateUpdate()
    {
        if ((state == EncounterState.Intro || state == EncounterState.Battle)
            && playerHealth != null && playerHealth.IsDead)
        {
            CancelEncounter();
            return;
        }

        if (state == EncounterState.Intro)
        {
            UpdateIntro();
        }
        else if (state == EncounterState.Battle)
        {
            if (boss == null || !boss.isActiveAndEnabled || (lockCameraDuringBattle && !cameraLock.IsLocked))
            {
                CancelEncounter();
            }
            else if (lockCameraDuringBattle)
            {
                cameraLock.Update();
            }
        }
    }

    private void UpdateIntro()
    {
        bool holdsCamera = lockCameraDuringBattle && lockCameraDuringIntro;
        if (playingIntro == null || !playingIntro.isActiveAndEnabled || (holdsCamera && !cameraLock.IsLocked))
        {
            CancelEncounter();
            return;
        }
        if (holdsCamera) cameraLock.Update();
        if (playingIntro.time + 0.00001d < playingIntro.duration)
        {
            return;
        }

        ReleaseIntro();
        if (state != EncounterState.Intro)
        {
            return;
        }
        if (boss == null || (lockCameraDuringBattle && !holdsCamera
            && (!EncounterCameraLock.CanLock(cameraManager) || !cameraManager.isActiveAndEnabled)))
        {
            CancelEncounter();
            return;
        }
        BeginBattle();
    }

    private void BeginBattle()
    {
        state = EncounterState.Battle;
        if (lockCameraDuringBattle && !cameraLock.IsLocked)
        {
            cameraLock.Lock(cameraManager);
        }
        boss.Defeated.AddListener(CompleteEncounter);
        boss.gameObject.SetActive(true);
        if (state == EncounterState.Battle)
        {
            if (bossMusic != null) bossMusic.FadeIn(musicFadeDuration);
            battleStarted.Invoke();
        }
    }

    private void OnIntroStopped(PlayableDirector director)
    {
        ReleaseIntro(stopPlayback: false);
        CancelEncounter();
    }

    private void ReleaseIntro(bool stopPlayback = true)
    {
        PlayableDirector director = playingIntro;
        playingIntro = null;
        if (director != null)
        {
            director.stopped -= OnIntroStopped;
            if (stopPlayback && director.playableGraph.IsValid())
            {
                director.Stop();
            }
            director.extrapolationMode = previousWrapMode;
        }
    }

    private void CompleteEncounter()
    {
        if (state != EncounterState.Battle)
        {
            return;
        }

        state = EncounterState.Finished;
        boss.Defeated.RemoveListener(CompleteEncounter);
        cameraLock.Release();
        if (playerSkills != null) playerSkills.ClearSkills();
        bossDefeated.Invoke();
        if (defeatTeleport == null || (playerHealth != null && playerHealth.IsDead)) return;

        if (gunSelectionManager == null || !gunSelectionManager.TryShowGunSelection(defeatTeleport))
        {
            defeatTeleport.TeleportPlayer();
        }
    }

    public void CancelEncounter()
    {
        if (state != EncounterState.Intro && state != EncounterState.Battle)
        {
            return;
        }

        bool wasInBattle = state == EncounterState.Battle;
        // An interrupted intro can be replayed. A started boss fight is single-use.
        state = wasInBattle ? EncounterState.Finished : EncounterState.Ready;
        ReleaseIntro();
        cameraLock.Release();
        if (wasInBattle && boss != null)
        {
            boss.Defeated.RemoveListener(CompleteEncounter);
            boss.gameObject.SetActive(false);
        }
    }

    private void OnDisable() => CancelEncounter();
}
