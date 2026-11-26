using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BeachLaserSurvival : MonoBehaviour
{
    [SerializeField] private MissionObjective objective;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private ArisaHealth player;
    [SerializeField] private Collider2D arenaFloor;
    [SerializeField] private Canvas overlay;
    [SerializeField] private Image shade;
    [SerializeField] private RectTransform ship;
    [SerializeField] private RectTransform warning;
    [SerializeField] private RectTransform beam;
    [SerializeField, Min(1)] private int damage = 25;
    [SerializeField, Range(0f, 0.5f)] private float darkness = 0.20f;

    public const float EntranceSeconds = 0.55f;
    private const float ExitSeconds = 0.45f;
    private const float ShotInterval = 1f;
    private const float LineupSeconds = 0.25f;
    private const float WarningSeconds = 0.66f;
    private const float BeamEnd = 0.90f;
    private const float BeamWidth = 0.057f;
    private const float ShipWidth = 1.44f;
    private const float ShipBottom = 0.595f;
    private const float ShipTop = 1.16f;
    private const int LaneCount = 8;
    private const int SparkCount = 8;
    private const float DischargeFlash = 0.08f;
    private const float ImpactShakeDuration = 0.28f;
    // Centers of the three red lenses on BeachLaserAirship.png, in sprite UVs.
    private static readonly Vector2[] Emitters =
    {
        new Vector2(692f / 2172f, 1f - 433f / 724f),
        new Vector2(1074f / 2172f, 1f - 488f / 724f),
        new Vector2(1439f / 2172f, 1f - 424f / 724f),
    };

    private Camera view;
    private CapsuleCollider2D playerCollider;
    private RectTransform charge;
    private Texture2D glowTexture;
    private Sprite glowSprite;
    private Image chargeHalo;
    private Image chargeCore;
    private readonly Image[] chargeSparks = new Image[SparkCount];
    private BeachLaserImpactDust impactDust;
    private bool active;
    private float departure;
    private float reveal;
    private int lastShot;
    private int lane;
    private bool hitThisShot;
    private bool impactThisShot;
    private float shakeStartedAt = float.NegativeInfinity;
    private Vector2 shakeOffset;
    private Camera shakenCamera;
    private float shipOffset;
    private float shipBank;
    private float lineupStart;
    private float lineupTarget;
    private Vector2 emitter;

    public void Begin()
    {
        TryGetComponent(out Trigger trigger);
        ResolveReferences(trigger);
        view = cameraManager != null ? cameraManager.ManagedCamera : null;
        if (view == null) view = Camera.main;
        if (view == null || objective == null || player == null || !player.TryGetComponent(out playerCollider)
            || arenaFloor == null || overlay == null || shade == null || ship == null || warning == null || beam == null)
        {
            Debug.LogWarning("해변 레이저 미션에 카메라, 플레이어 또는 연출 참조가 빠져 있어 시작할 수 없습니다.", this);
            if (trigger != null) trigger.CancelMission();
            return;
        }

        if (charge == null) CreateChargeVisuals();
        impactDust ??= new BeachLaserImpactDust((RectTransform)overlay.transform, beam, glowSprite);
        impactDust.Clear();
        shakeStartedAt = float.NegativeInfinity;
        shakeOffset = Vector2.zero;
        active = true;
        departure = 0f;
        lastShot = -1;
        lane = -1;
        shipOffset = shipBank = 0f;
        warning.gameObject.SetActive(false);
        beam.gameObject.SetActive(false);
        charge.gameObject.SetActive(false);
        Present(0f);
        overlay.gameObject.SetActive(true);
    }

    private void ResolveReferences(Trigger trigger)
    {
        if (objective == null) TryGetComponent(out objective);
        if (player == null && trigger != null) player = trigger.MissionPlayerHealth;
        if (arenaFloor == null && transform.parent != null)
        {
            Transform floor = transform.parent.Find("챕터3 바닥")
                ?? transform.parent.Find("해변·도시 바닥");
            if (floor != null) arenaFloor = floor.GetComponent<Collider2D>();
        }

        if (overlay == null) overlay = GetComponentInChildren<Canvas>(true);
        if (overlay == null) return;

        Transform visuals = overlay.transform;
        if (shade == null)
        {
            Transform shadeObject = visuals.Find("해변 그림자");
            if (shadeObject != null) shade = shadeObject.GetComponent<Image>();
        }
        if (ship == null) ship = visuals.Find("비행체 하부") as RectTransform;
        if (warning == null) warning = visuals.Find("발사 위치 경고") as RectTransform;
        if (beam == null) beam = visuals.Find("레이저 광선") as RectTransform;
    }

    public void End()
    {
        if (!active) return;
        active = false;
        warning.gameObject.SetActive(false);
        beam.gameObject.SetActive(false);
        charge.gameObject.SetActive(false);
        departure = ExitSeconds * reveal;
        if (departure <= 0f) Hide();
    }

    private void LateUpdate()
    {
        if (!active)
        {
            if (departure <= 0f) return;
            departure = Mathf.Max(0f, departure - Time.deltaTime);
            if (departure == 0f) Hide();
            else
            {
                UpdateShake();
                Present(departure / ExitSeconds);
                impactDust.Update(view, shakeOffset);
            }
            return;
        }

        if (view == null || player == null || player.IsDead)
        {
            Hide();
            return;
        }

        float elapsed = objective.ElapsedSeconds;
        float combatTime = elapsed - EntranceSeconds;
        if (combatTime < 0f)
        {
            Present(Mathf.Clamp01(elapsed / EntranceSeconds));
            return;
        }

        int shot = Mathf.FloorToInt(combatTime / ShotInterval);
        if (lastShot != shot)
        {
            lastShot = shot;
            int pick = Random.Range(0, lane < 0 ? LaneCount : LaneCount - 1);
            lane = lane >= 0 && pick >= lane ? pick + 1 : pick;
            hitThisShot = false;
            impactThisShot = false;
            AlignEmitter(LaneCenter());
        }

        float phase = combatTime - shot * ShotInterval;
        float lineup = Mathf.Clamp01(phase / LineupSeconds);
        shipOffset = Mathf.SmoothStep(lineupStart, lineupTarget, lineup);
        shipBank = lineup < 1f ? -Mathf.Sign(lineupTarget - lineupStart) * 2.3f * Mathf.Sin(lineup * Mathf.PI) : 0f;
        bool firing = phase >= WarningSeconds && phase < BeamEnd;
        warning.gameObject.SetActive(phase < WarningSeconds);
        beam.gameObject.SetActive(firing);

        float center = LaneCenter();
        float emitterY = Mathf.Lerp(ShipBottom, ShipTop, emitter.y);
        float left = center - BeamWidth * 0.5f;
        float right = center + BeamWidth * 0.5f;
        float floorY = arenaFloor.bounds.max.y;
        float floorViewport = view.WorldToViewportPoint(new Vector3(0f, floorY, transform.position.z)).y;
        if (firing && !impactThisShot)
        {
            impactThisShot = true;
            shakeStartedAt = Time.time;
            float floorDepth = view.WorldToViewportPoint(arenaFloor.bounds.center).z;
            Vector3 impact = view.ViewportToWorldPoint(new Vector3(center, floorViewport, floorDepth));
            impact.y = floorY;
            impactDust.Play(impact);
        }

        UpdateShake();
        Present(1f);
        UpdateCharge(new Vector2(center, emitterY) + shakeOffset, phase);
        SetBand(warning, left, right, floorViewport, floorViewport + 0.026f);
        SetBand(beam, left, right, floorViewport, emitterY);
        impactDust.Update(view, shakeOffset);
        if (!firing || hitThisShot || playerCollider == null || !playerCollider.enabled) return;

        float depth = view.WorldToViewportPoint(player.transform.position).z;
        float worldLeft = view.ViewportToWorldPoint(new Vector3(left, floorViewport, depth)).x;
        float worldRight = view.ViewportToWorldPoint(new Vector3(right, floorViewport, depth)).x;
        float worldTop = view.ViewportToWorldPoint(new Vector3(center, emitterY, depth)).y;
        Bounds playerBounds = playerCollider.bounds;
        if (playerBounds.min.x >= worldRight || playerBounds.max.x <= worldLeft
            || playerBounds.max.y <= floorY || playerBounds.min.y >= worldTop) return;

        hitThisShot = true;
        player.TakeDamage(damage);
    }

    private float LaneCenter() => Mathf.Lerp(0.12f, 0.88f, lane / (float)(LaneCount - 1));

    private void AlignEmitter(float target)
    {
        lineupStart = shipOffset;
        float bestOffset = float.PositiveInfinity;
        foreach (Vector2 lens in Emitters)
        {
            float offset = target - (0.5f + (lens.x - 0.5f) * ShipWidth);
            if (Mathf.Abs(offset) >= Mathf.Abs(bestOffset)) continue;
            bestOffset = offset;
            emitter = lens;
        }
        lineupTarget = bestOffset;
    }

    private void CreateChargeVisuals()
    {
        const int size = 32;
        glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "레이저 충전 광원" };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 uv = new Vector2((x + .5f) / size, (y + .5f) / size);
            float fade = 1f - Mathf.Clamp01(2f * Vector2.Distance(uv, Vector2.one * .5f));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * fade * Mathf.Sqrt(fade)));
        }
        glowTexture.SetPixels32(pixels);
        glowTexture.Apply(false, true);
        glowSprite = Sprite.Create(glowTexture, new Rect(0, 0, size, size), Vector2.one * .5f);
        charge = new GameObject("레이저 충전", typeof(RectTransform)).GetComponent<RectTransform>();
        charge.SetParent(overlay.transform, false);
        charge.sizeDelta = Vector2.zero;
        chargeHalo = CreateGlow("충전 광륜");
        for (int i = 0; i < SparkCount; i++) chargeSparks[i] = CreateGlow("모이는 불꽃");
        chargeCore = CreateGlow("충전 중심");
    }

    private Image CreateGlow(string objectName)
    {
        var glow = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        glow.rectTransform.SetParent(charge, false);
        glow.raycastTarget = false;
        glow.sprite = glowSprite;
        return glow;
    }

    private void UpdateCharge(Vector2 origin, float phase)
    {
        float timeSinceFire = phase - WarningSeconds;
        bool gathering = timeSinceFire < 0f;
        charge.gameObject.SetActive(phase >= LineupSeconds && timeSinceFire < DischargeFlash);
        if (!charge.gameObject.activeSelf) return;

        charge.anchorMin = charge.anchorMax = origin;
        charge.anchoredPosition = Vector2.zero;
        float screenHeight = ((RectTransform)overlay.transform).rect.height;
        float progress = Mathf.InverseLerp(LineupSeconds, WarningSeconds, phase);
        float flash = gathering ? 1f : 1f - timeSinceFire / DischargeFlash;
        float size = Mathf.Lerp(.028f, .14f, progress) * screenHeight;
        chargeHalo.rectTransform.sizeDelta = Vector2.one * size;
        chargeHalo.color = new Color(1f, .19f, .055f, (.24f + .72f * progress) * flash);
        chargeCore.rectTransform.sizeDelta = Vector2.one * (size * .43f);
        chargeCore.color = new Color(1f, .83f, .60f, Mathf.Lerp(.28f, 1f, progress) * flash);
        float spread = Mathf.Lerp(.12f, .012f, progress) * screenHeight;
        for (int i = 0; i < SparkCount; i++)
        {
            Image spark = chargeSparks[i];
            spark.gameObject.SetActive(gathering);
            if (!gathering) continue;
            float angle = i * (2f * Mathf.PI / SparkCount) + progress * 1.6f;
            spark.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                * (spread * (i % 2 == 0 ? 1f : .70f));
            spark.rectTransform.sizeDelta = Vector2.one * (screenHeight * .026f);
            spark.color = new Color(1f, .57f, .23f, Mathf.Lerp(.65f, 1f, progress));
        }
    }

    private void Present(float progress)
    {
        reveal = progress;
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        shade.color = new Color(0.045f, 0.066f, 0.10f, darkness * eased);
        float offset = (1f - eased) * 0.36f;
        float center = .5f + shipOffset;
        SetBand(ship, center - ShipWidth * .5f, center + ShipWidth * .5f, ShipBottom + offset, ShipTop + offset);
        ship.localRotation = Quaternion.Euler(0f, 0f, shipBank * eased);
    }

    private void SetBand(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = new Vector2(left, bottom) + shakeOffset;
        rect.anchorMax = new Vector2(right, top) + shakeOffset;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void UpdateShake()
    {
        float elapsed = Time.time - shakeStartedAt;
        float remaining = 1f - Mathf.Clamp01(elapsed / ImpactShakeDuration);
        if (remaining <= 0f)
        {
            shakeOffset = Vector2.zero;
            return;
        }

        float angle = elapsed * 2f * Mathf.PI;
        shakeOffset = new Vector2(0.010f * Mathf.Sin(angle * 19f + 0.6f),
            0.022f * Mathf.Cos(angle * 23f)) * (remaining * remaining);
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraShake;
        RenderPipelineManager.endCameraRendering += EndCameraShake;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraShake;
        RenderPipelineManager.endCameraRendering -= EndCameraShake;
        Hide();
    }

    private void BeginCameraShake(ScriptableRenderContext context, Camera camera)
    {
        if (camera != view || shakeOffset == Vector2.zero) return;

        // Shift the rendered view without moving the camera that the mission and its boundaries use.
        Vector2 clipOffset = shakeOffset * 2f;
        camera.projectionMatrix = Matrix4x4.Translate(new Vector3(clipOffset.x, clipOffset.y, 0f))
            * camera.projectionMatrix;
        shakenCamera = camera;
    }

    private void EndCameraShake(ScriptableRenderContext context, Camera camera)
    {
        if (camera == shakenCamera) RestoreCamera();
    }

    private void RestoreCamera()
    {
        if (shakenCamera != null) shakenCamera.ResetProjectionMatrix();
        shakenCamera = null;
    }

    private void OnDestroy()
    {
        impactDust?.Dispose();
        if (glowSprite != null) Destroy(glowSprite);
        if (glowTexture != null) Destroy(glowTexture);
    }

    private void Hide()
    {
        active = false;
        departure = 0f;
        shakeStartedAt = float.NegativeInfinity;
        shakeOffset = Vector2.zero;
        RestoreCamera();
        impactDust?.Clear();
        if (overlay != null) overlay.gameObject.SetActive(false);
    }
}
