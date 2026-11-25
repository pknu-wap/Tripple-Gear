using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class BoatVoyage : MonoBehaviour
{
    [SerializeField, KoreanLabel("탑승 캐릭터")] private ArisaHorizontalMovement player;
    [SerializeField, KoreanLabel("항해 카메라")] private CameraManager cameraManager;
    [SerializeField, KoreanLabel("보트 갑판")] private BoxCollider2D deck;
    [SerializeField, KoreanLabel("보트 그림")] private SpriteRenderer hull;
    [SerializeField, KoreanLabel("바다 배경")] private SpriteRenderer[] seaTiles;
    [SerializeField, KoreanLabel("수면 스크롤 셰이더")] private Shader seaScrollShader;
    [SerializeField, KoreanLabel("항해 끝에 시작할 미션")] private Trigger arrivalMission;
    [SerializeField, KoreanLabel("항해 속도"), Min(0.1f)] private float cruiseSpeed = 6f;
    [SerializeField, KoreanLabel("출항 가속도"), Min(0.1f)] private float acceleration = 5f;

    private Bounds seaBounds;
    private ArisaHealth playerHealth;
    private bool departureRequested;
    private enum VoyagePhase { Docked, Sailing, Docking, Battle, Arrived }

    private static readonly int Scroll = Shader.PropertyToID("_Scroll");
    private static readonly int SeaArea = Shader.PropertyToID("_SeaArea");
    private VoyagePhase phase;
    private float speed;
    private Vector3 startPosition;
    private BoatWake wake;
    private Material scrollingSea;
    private Material[] stillSeaMaterials;
    private float cameraStartX;
    private float visualSpeed;
    private float visualTravel;
    private bool seaIsScrolling;
    private Trigger activeBattleMission;
    private bool dockedBattle;
    private bool suspendedForBossEntrance;

    internal event Action Departed;

    private void Awake()
    {
        startPosition = transform.localPosition;
        wake = GetComponentInChildren<BoatWake>();
    }

    private void OnDisable() => StopVoyage();

    private void OnDestroy()
    {
        if (scrollingSea != null) Destroy(scrollingSea);
    }

    public void RestartForTest() => BeginDeparture(fromMission: false);

    internal void PreviewBossEntrance(float deckCenterX, Action playEntrance)
    {
        if (!TryPrepareDeparture()) return;
        StartCoroutine(PreviewBossEntranceRoutine(deckCenterX, playEntrance));
    }

    private IEnumerator PreviewBossEntranceRoutine(float deckCenterX, Action playEntrance)
    {
        bool boarded = false;
        yield return TeleportDirectionalFade.GetOrCreate().Play(() => boarded = BoardForVoyage(deckCenterX), 2f, 2f);
        if (boarded) playEntrance();
    }

    public void Depart()
    {
        if (!departureRequested)
        {
            BeginDeparture(fromMission: true);
        }
    }

    private void BeginDeparture(bool fromMission)
    {
        if (!TryPrepareDeparture()) return;

        departureRequested |= fromMission;
        StartCoroutine(TeleportDirectionalFade.GetOrCreate().Play(() => BoardForVoyage(), 2f, 2f));
    }

    private bool TryPrepareDeparture()
    {
        if (!isActiveAndEnabled || player == null || deck == null
            || cameraManager == null || hull == null || !TryGetSeaBounds(out seaBounds))
        {
            return false;
        }

        playerHealth = player.GetComponent<ArisaHealth>();
        return true;
    }

    private bool BoardForVoyage(float? deckCenterX = null)
    {
        if (player == null || (playerHealth != null && playerHealth.IsDead))
        {
            return false;
        }

        suspendedForBossEntrance = false;
        hull.enabled = true;
        deck.enabled = true;
        wake?.Clear();
        foreach (Trigger mission in FindObjectsByType<Trigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mission.gameObject.scene == gameObject.scene && mission.IsMissionForBoat(this))
            {
                mission.ResetMissionForRetry();
            }
        }
        activeBattleMission = null;
        ResetSeaScroll();
        transform.localPosition = startPosition;
        speed = 0f;
        Physics2D.SyncTransforms();
        if (deckCenterX.HasValue)
        {
            transform.position += Vector3.right * (deckCenterX.Value - deck.bounds.center.x);
            Physics2D.SyncTransforms();
        }
        player.BoardBoat(deck);
        cameraManager.SetOrthographicSize(Mathf.Min(cameraManager.OrthographicSize, seaBounds.extents.y));
        cameraManager.SetCameraArea(seaBounds);
        cameraManager.SetFollowMode(CameraFollowMode.RunningMap);
        cameraManager.SetFollowTarget(transform, true);
        cameraStartX = cameraManager.ManagedCamera.transform.position.x;
        phase = deckCenterX.HasValue ? VoyagePhase.Docked : VoyagePhase.Sailing;
        if (phase == VoyagePhase.Sailing) wake?.Play();
        Departed?.Invoke();
        return true;
    }

    private void FixedUpdate()
    {
        if (suspendedForBossEntrance || phase == VoyagePhase.Docked)
        {
            return;
        }

        if (player == null || !player.IsInBoatSection || (playerHealth != null && playerHealth.IsDead)
            || (activeBattleMission != null && activeBattleMission.CurrentMissionState == Trigger.MissionState.Cancelled))
        {
            StopVoyage();
            return;
        }

        if (player.transform.position.y < seaBounds.min.y - 2f && playerHealth != null)
        {
            playerHealth.TakeFatalDamage();
        }

        Camera currentCamera = cameraManager.ManagedCamera;
        float halfViewWidth = currentCamera != null ? currentCamera.orthographicSize * currentCamera.aspect : 0f;
        float bowOffset = hull.bounds.max.x - transform.position.x;
        float remaining = seaBounds.max.x - Mathf.Max(halfViewWidth, bowOffset) - transform.position.x;
        float deltaTime = Time.fixedDeltaTime;
        bool stoppingForBattle = phase == VoyagePhase.Docking || phase == VoyagePhase.Battle;
        speed = Mathf.MoveTowards(speed, stoppingForBattle ? 0f : cruiseSpeed, acceleration * deltaTime);
        float step = Mathf.Clamp(remaining, 0f, speed * deltaTime);
        bool reachedEnd = phase == VoyagePhase.Sailing && remaining <= step;

        if (!stoppingForBattle && remaining <= step)
        {
            phase = VoyagePhase.Arrived;
            speed = 0f;
        }
        else if (phase == VoyagePhase.Docking && remaining <= step)
        {
            speed = 0f;
        }

        float boatSpeed = step / deltaTime;
        float wakeSpeed = phase == VoyagePhase.Arrived ? boatSpeed : Mathf.Max(boatSpeed, visualSpeed);
        wake?.SetSpeedFraction(wakeSpeed / Mathf.Max(0.1f, cruiseSpeed), wakeSpeed - boatSpeed);
        player.MoveWithBoat(deck, step);

        if (step > 0f)
        {
            transform.position += Vector3.right * step;
            Physics2D.SyncTransforms();
        }

        if (reachedEnd && arrivalMission != null)
        {
            arrivalMission.TryStartBoatArrivalMission(this, player);
        }
    }

    private void LateUpdate()
    {
        if (suspendedForBossEntrance || phase == VoyagePhase.Docked || seaScrollShader == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        bool stopped = phase == VoyagePhase.Arrived || phase == VoyagePhase.Docking
            || (phase == VoyagePhase.Battle && dockedBattle);
        float targetSpeed = stopped ? 0f : cruiseSpeed;
        float nextSpeed = Mathf.MoveTowards(visualSpeed, targetSpeed, acceleration * deltaTime);
        visualTravel += (visualSpeed + nextSpeed) * 0.5f * deltaTime;
        visualSpeed = nextSpeed;

        // CameraManager updates first. Remove its actual step from the texture motion.
        float cameraTravel = cameraManager.ManagedCamera.transform.position.x - cameraStartX;
        UpdateSeaScroll(cameraTravel);
    }

    internal Vector3 PlaceGroundEnemyOnDeck(MonsterHealth monster)
    {
        Vector3 position = monster.transform.position;
        if (deck == null || !deck.isActiveAndEnabled || monster.TryGetComponent<FlyingMonsterAttack>(out _)
            || !monster.TryGetComponent(out BoxCollider2D body) || !body.isActiveAndEnabled)
        {
            return position;
        }

        Physics2D.SyncTransforms();
        Bounds deckBounds = deck.bounds;
        Bounds bodyBounds = body.bounds;
        position.x += ClampToDeckWidth(deckBounds, bodyBounds, 0f);
        position.y += Mathf.Max(0f, deckBounds.max.y + MonsterGroundMotion.SkinWidth - bodyBounds.min.y);

        if (body.attachedRigidbody != null) body.attachedRigidbody.position = position;
        monster.transform.position = position;
        Physics2D.SyncTransforms();
        return position;
    }

    internal static float ClampToDeckWidth(Bounds deckBounds, Bounds bodyBounds, float step)
    {
        float minStep = deckBounds.min.x - bodyBounds.min.x;
        float maxStep = deckBounds.max.x - bodyBounds.max.x;
        return minStep > maxStep ? deckBounds.center.x - bodyBounds.center.x
            : Mathf.Clamp(step, minStep, maxStep);
    }

    internal bool PrepareBattle(Trigger mission, ArisaHorizontalMovement subject, bool dock)
    {
        if (!isActiveAndEnabled || (phase != VoyagePhase.Sailing && phase != VoyagePhase.Arrived) || activeBattleMission != null
            || subject == null || subject != player || !subject.IsInBoatSection)
        {
            return false;
        }

        activeBattleMission = mission;
        dockedBattle = dock;
        phase = dock && phase != VoyagePhase.Arrived ? VoyagePhase.Docking : VoyagePhase.Battle;
        if (phase == VoyagePhase.Battle) speed = 0f;
        return true;
    }

    internal bool IsBattleReady(Trigger mission)
    {
        return activeBattleMission == mission && (phase == VoyagePhase.Battle
            || (speed <= 0f && visualSpeed <= 0f && (wake == null || !wake.HasLiveParticles)));
    }

    internal void BeginBattle()
    {
        if (phase == VoyagePhase.Docking && activeBattleMission != null
            && activeBattleMission.CurrentMissionState == Trigger.MissionState.Running)
        {
            phase = VoyagePhase.Battle;
        }
    }

    internal void FinishBattle()
    {
        if (phase == VoyagePhase.Battle && activeBattleMission != null
            && activeBattleMission.CurrentMissionState == Trigger.MissionState.Completed)
        {
            activeBattleMission = null;
            phase = VoyagePhase.Sailing;
        }
    }

    internal void SuspendForBossEntrance()
    {
        suspendedForBossEntrance = true;
        wake?.Clear();
        hull.enabled = false;
        deck.enabled = false;
    }

    internal void ResumeAfterBossEntrance(bool wrecked)
    {
        suspendedForBossEntrance = false;
        if (wrecked)
        {
            StopVoyage();
            return;
        }
        hull.enabled = true;
        deck.enabled = true;
        if (phase != VoyagePhase.Docked) wake?.Play();
    }

    private void StopVoyage()
    {
        if (phase == VoyagePhase.Docked) return;
        phase = VoyagePhase.Docked;
        wake?.Clear();
        activeBattleMission?.CancelMission();
        activeBattleMission = null;
        ResetSeaScroll();
    }

    private void UpdateSeaScroll(float cameraTravel)
    {
        if (scrollingSea == null)
        {
            scrollingSea = new Material(seaScrollShader);
            stillSeaMaterials = new Material[seaTiles.Length];
        }
        if (!seaIsScrolling)
        {
            Bounds referenceTile = seaTiles[0].bounds;
            scrollingSea.SetVector(SeaArea, new Vector4(referenceTile.min.x, referenceTile.min.y,
                1f / referenceTile.size.x, 1f / referenceTile.size.y));
            for (int i = 0; i < seaTiles.Length; i++)
            {
                SpriteRenderer tile = seaTiles[i];
                stillSeaMaterials[i] = tile.sharedMaterial;
                tile.sharedMaterial = scrollingSea;
            }
            seaIsScrolling = true;
        }
        scrollingSea.SetVector(Scroll, new Vector4(visualTravel, cameraTravel, 0f, 0f));
    }

    private void ResetSeaScroll()
    {
        visualSpeed = 0f;
        visualTravel = 0f;
        if (seaIsScrolling)
        {
            for (int i = 0; i < seaTiles.Length; i++)
            {
                if (seaTiles[i] != null) seaTiles[i].sharedMaterial = stillSeaMaterials[i];
            }
        }
        seaIsScrolling = false;
    }

    private bool TryGetSeaBounds(out Bounds bounds)
    {
        bounds = default;
        if (seaTiles == null || seaTiles.Length == 0 || seaTiles[0] == null)
        {
            return false;
        }

        Vector3 min = seaTiles[0].bounds.min;
        Vector3 max = seaTiles[0].bounds.max;
        foreach (SpriteRenderer tile in seaTiles)
        {
            if (tile == null)
            {
                return false;
            }

            Bounds tileBounds = tile.bounds;
            min.x = Mathf.Min(min.x, tileBounds.min.x);
            max.x = Mathf.Max(max.x, tileBounds.max.x);
            min.y = Mathf.Max(min.y, tileBounds.min.y);
            max.y = Mathf.Min(max.y, tileBounds.max.y);
        }

        bounds.SetMinMax(min, max);
        return max.x > min.x && max.y > min.y;
    }
}
