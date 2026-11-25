using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class AtailEntrance : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private BoatVoyage boat;
    [SerializeField] private ArisaHorizontalMovement player;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private TeleportTrigger arenaArrival;
    [SerializeField] private Transform launchOrigin;
    [SerializeField] private Transform launchPlayerAnchor;
    [SerializeField] private Transform launchSnapshot;
    [SerializeField] private Transform launchFlightRig;
    [SerializeField] private Transform arenaOrigin;
    [SerializeField] private SpriteRenderer arenaHitPose;
    [SerializeField] private SpriteRenderer arenaIdlePose;
    [SerializeField] private Transform atailAnchor;
    [SerializeField] private Transform arenaFraming;
    [SerializeField] private GameObject impactPlatform;
    [SerializeField, Min(0.1f)] private float arenaCameraSize = 7f;
    [SerializeField] private UnityEvent battleReady = new UnityEvent();
    [SerializeField, InspectorName("F1 보스 진입 미션 위치")] private Transform testLaunchPoint;

    internal bool CanPlayFromTestMenu => isActiveAndEnabled && boat != null && boat.isActiveAndEnabled
        && testLaunchPoint != null;

    internal void PlayFromTestMenu() => boat.PreviewBossEntrance(testLaunchPoint.position.x, PlayEntrance);

    private struct RendererState
    {
        public SpriteRenderer renderer;
        public bool enabled;
    }

    private Behaviour[] suspendedControls;
    private RendererState[] suspendedRenderers;
    private GameObject snapshotContent;
    private Rigidbody2D playerBody;
    private bool playerWasSimulated;
    private DirectorWrapMode previousWrapMode;
    private Vector3 previousCameraPosition;
    private float previousCameraSize;
    private bool running;
    private bool completed;

    private void OnEnable()
    {
        if (boat != null) boat.Departed += ResetForDeparture;
    }

    private void ResetForDeparture()
    {
        completed = false;
        if (impactPlatform != null) impactPlatform.SetActive(true);
    }

    public void PlayEntrance()
    {
        if (running || completed || !isActiveAndEnabled) return;
        if (director == null || director.playableAsset == null || !director.isActiveAndEnabled
            || boat == null || player == null || !player.IsInBoatSection || arenaArrival == null
            || cameraManager == null || cameraManager.ManagedCamera == null || !cameraManager.isActiveAndEnabled
            || launchOrigin == null || launchPlayerAnchor == null || launchSnapshot == null
            || launchFlightRig == null || arenaOrigin == null || arenaHitPose == null || arenaIdlePose == null
            || atailAnchor == null || arenaFraming == null || impactPlatform == null || !impactPlatform.activeSelf)
        {
            Debug.LogWarning("아테일 연출의 타임라인, 항해 및 보스방 참조를 확인해 주세요.", this);
            arenaArrival?.TeleportPlayer();
            return;
        }

        running = true;
        var camera = cameraManager.ManagedCamera;
        previousCameraPosition = camera.transform.position;
        previousCameraSize = camera.orthographicSize;
        previousWrapMode = director.extrapolationMode;
        launchOrigin.position = boat.transform.position;
        launchPlayerAnchor.SetPositionAndRotation(player.transform.position, player.transform.rotation);
        var aim = player.GetComponent<ArisaMouseAim>();
        bool facingLeft = aim != null && aim.IsFacingLeft;
        Vector3 flightScale = launchFlightRig.localScale;
        flightScale.x = (facingLeft ? -1f : 1f) * Mathf.Abs(flightScale.x);
        launchFlightRig.localScale = flightScale;
        arenaHitPose.flipX = facingLeft;
        SpriteRenderer idleSource = player.IdleSpriteRenderer;
        if (idleSource != null)
        {
            Vector3 idleScale = arenaIdlePose.transform.localScale;
            idleScale.x = Mathf.Abs(idleScale.x) * Mathf.Sign(idleSource.transform.lossyScale.x);
            arenaIdlePose.transform.localScale = idleScale;
            arenaIdlePose.flipX = idleSource.flipX;
        }
        atailAnchor.position = new Vector3(launchPlayerAnchor.position.x + 2.35f,
            launchOrigin.position.y, launchOrigin.position.z);

        cameraManager.enabled = false;
        CapturePlayerPose();
        SuspendPlayer();
        boat.SuspendForBossEntrance();
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.stopped += CancelOnDirectorStop;
        director.time = 0d;
        director.Play();
        director.Evaluate();
    }

    // Timeline signal at the cut from the launch shot to the arena.
    public void ShowArena()
    {
        if (!running) return;
        atailAnchor.position = arenaOrigin.position;
        var camera = cameraManager.ManagedCamera;
        camera.orthographicSize = arenaCameraSize;
        camera.transform.position = new Vector3(arenaFraming.position.x, arenaFraming.position.y,
            previousCameraPosition.z);
    }

    private void LateUpdate()
    {
        if (!running) return;
        if (!director.isActiveAndEnabled || !director.playableGraph.IsValid())
        {
            CancelEntrance();
        }
        else if (director.time + 0.00001d >= director.duration)
        {
            completed = true;
            ReleaseDirector(stopPlayback: true);
            impactPlatform.SetActive(false);
            boat.ResumeAfterBossEntrance(wrecked: true);
            cameraManager.SetOrthographicSize(arenaCameraSize);
            RestorePlayerPhysics();
            arenaArrival.PlacePlayerAtDestination();
            cameraManager.enabled = true;
            RestorePlayerPresentation();
            if (player.TryGetComponent(out ArisaHealth health)) health.ClearHitPresentation();
            player.ShowIdlePresentation();
            battleReady.Invoke();
        }
    }

    public void CancelEntrance() => CancelEntrance(stopPlayback: true);

    private void CancelOnDirectorStop(PlayableDirector source) => CancelEntrance(stopPlayback: false);

    private void CancelEntrance(bool stopPlayback)
    {
        if (!running) return;
        ReleaseDirector(stopPlayback);
        if (impactPlatform != null) impactPlatform.SetActive(true);
        if (boat != null) boat.ResumeAfterBossEntrance(wrecked: false);
        if (cameraManager != null)
        {
            var camera = cameraManager.ManagedCamera;
            if (camera != null)
            {
                camera.transform.position = previousCameraPosition;
                camera.orthographicSize = previousCameraSize;
            }
            cameraManager.enabled = true;
        }
        RestorePlayerPhysics();
        RestorePlayerPresentation();
    }

    private void ReleaseDirector(bool stopPlayback)
    {
        running = false;
        if (snapshotContent != null)
        {
            snapshotContent.SetActive(false);
            Destroy(snapshotContent);
            snapshotContent = null;
        }
        if (director == null) return;
        director.stopped -= CancelOnDirectorStop;
        if (stopPlayback && director.playableGraph.IsValid()) director.Stop();
        director.extrapolationMode = previousWrapMode;
    }

    private void CapturePlayerPose()
    {
        snapshotContent = new GameObject("미션 완료 순간의 아리사");
        snapshotContent.transform.SetParent(launchSnapshot, false);
        Transform sourceRoot = player.transform;
        Quaternion inverseRotation = Quaternion.Inverse(sourceRoot.rotation);

        foreach (SpriteRenderer source in player.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!source.enabled || source.sprite == null) continue;

            var copy = new GameObject(source.name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            copy.gameObject.layer = source.gameObject.layer;
            copy.transform.SetParent(snapshotContent.transform, false);
            copy.transform.localPosition = inverseRotation * (source.transform.position - sourceRoot.position);
            copy.transform.localRotation = inverseRotation * source.transform.rotation;
            copy.transform.localScale = source.transform.lossyScale;
            copy.sprite = source.sprite;
            copy.sharedMaterial = source.sharedMaterial;
            copy.color = source.color;
            copy.sortingLayerID = source.sortingLayerID;
            copy.sortingOrder = source.sortingOrder;
            copy.flipX = source.flipX;
            copy.flipY = source.flipY;
            copy.maskInteraction = source.maskInteraction;
            copy.spriteSortPoint = source.spriteSortPoint;
        }
    }

    private void SuspendPlayer()
    {
        suspendedControls = new Behaviour[] { player, player.GetComponent<ArisaMouseAim>(),
            player.GetComponent<SubmachineGunFire>(), player.GetComponent<ArisaSkillController>() };
        for (int i = 0; i < suspendedControls.Length; i++)
        {
            Behaviour control = suspendedControls[i];
            if (control != null && control.enabled) control.enabled = false;
            else suspendedControls[i] = null;
        }
        var renderers = player.GetComponentsInChildren<SpriteRenderer>(true);
        suspendedRenderers = new RendererState[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            suspendedRenderers[i] = new RendererState { renderer = renderers[i], enabled = renderers[i].enabled };
            renderers[i].enabled = false;
        }
        playerBody = player.GetComponent<Rigidbody2D>();
        playerWasSimulated = playerBody.simulated;
        playerBody.simulated = false;
    }

    private void RestorePlayerPhysics()
    {
        if (playerBody != null)
        {
            playerBody.simulated = playerWasSimulated;
            Physics2D.SyncTransforms();
        }
    }

    private void RestorePlayerPresentation()
    {
        foreach (var previous in suspendedRenderers)
        {
            if (previous.renderer != null) previous.renderer.enabled = previous.enabled;
        }
        foreach (Behaviour control in suspendedControls)
        {
            if (control != null) control.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (boat != null) boat.Departed -= ResetForDeparture;
        CancelEntrance();
    }
}
