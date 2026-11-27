using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed partial class FinalSkyBattle : MonoBehaviour
{
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private ChapterMusic chapterMusic;
    [SerializeField] private FinalSkyArena layout;
    [SerializeField] private GameObject damageExplosionPrefab;

    private static readonly Color WarningRed = new Color(1f, 0.10f, 0.18f);
    private static readonly Color BeamPink = new Color(1f, 0.18f, 0.42f);
    private static readonly Color BeamCore = new Color(1f, 0.94f, 0.78f);
    private readonly EncounterCameraLock cameraLock = new EncounterCameraLock();
    private Transform hazards;
    private ArisaHealth player;
    private ArisaHorizontalMovement movement;
    private ArisaMouseAim aim;
    private Collider2D playerCollider;
    private SpriteRenderer arisa;
    private SpriteRenderer background;
    private Vector3 backgroundScale;
    private Vector2 backgroundSize;
    private Material originalCloudMaterial;
    private Material cloudScrollMaterial;
    private float cloudTravel;
    private static readonly int CloudTravelProperty = Shader.PropertyToID("_CloudTravel");
    private static readonly int CloudSpriteRectProperty = Shader.PropertyToID("_SpriteUVRect");
    private Vector3 arisaArtOffset;
    private Color arisaArtColor;
    private Vector3 arisaHome;
    private bool arisaHomeFlip;
    private Sprite square;
    private Sprite missileSprite;
    private UIBarController bossBar;
    private MonsterHealth boss;
    private Collider2D bossCollider;
    private Vector3 bossHome;
    private Vector3 returnPosition;
    private float previousCameraSize;
    private float playerFlashUntil;
    private int previousPlayerHp;
    private bool started;
    private bool damagedPhase;
    private MissionCompleteBanner completionBanner;
    private GameObject credits;

    internal bool PhaseOneCleared { get; private set; }

    private Rect Arena
    {
        get
        {
            float cameraHalfHeight = cameraLock.Camera.orthographicSize;
            float halfWidth = cameraHalfHeight * cameraLock.Camera.aspect;
            Vector3 center = cameraLock.Position;
            return new Rect(center.x - halfWidth, center.y - cameraHalfHeight, halfWidth * 2f, cameraHalfHeight * 2f);
        }
    }

    private void Awake()
    {
        if (layout != null) layout.gameObject.SetActive(false);
    }

    internal void Begin(ArisaHealth arisaHealth, UIBarController encounterBar, Sprite missile, bool useWhiteFlash = false)
    {
        if (started || !isActiveAndEnabled || arisaHealth == null || arisaHealth.IsDead) return;
        if (layout == null || !layout.IsConfigured || !EncounterCameraLock.CanLock(cameraManager)
            || missile == null || !arisaHealth.TryGetComponent(out movement))
        {
            Debug.LogError("공중 보스전에 필요한 카메라, 캐릭터, 전투 에셋을 연결해 주세요.", this);
            return;
        }

        started = true;
        PhaseOneCleared = false;
        damagedPhase = false;
        player = arisaHealth;
        player.TryGetComponent(out aim);
        player.TryGetComponent(out playerCollider);
        bossBar = encounterBar;
        missileSprite = missile;
        GameplayInput.Block(this);
        StartCoroutine(Enter(useWhiteFlash));
    }

    internal void BeginFromTestMenu(ArisaHealth arisaHealth, UIBarController encounterBar, Sprite missile)
    {
        if (cameraManager != null) cameraManager.SetCameraAreaByName("챕터3 비행선 기계실");
        Begin(arisaHealth, encounterBar, missile);
    }

    private IEnumerator Enter(bool useWhiteFlash)
    {
        if (useWhiteFlash)
        {
            yield return EnterWithWhiteFlash();
        }
        else
        {
            var fade = TeleportDirectionalFade.GetOrCreate();
            yield return fade.FadeIn(2.6f);
            if (player != null && !player.IsDead) EnterArena();
            yield return fade.FadeOut(2.4f);
        }
        GameplayInput.Unblock(this);
        if (player == null || player.IsDead || boss == null) yield break;

        Vector3 entry = OutsideArena(1.18f, bossHome.y);
        yield return Animate(0.8f, t => boss.transform.position = Vector3.Lerp(entry, bossHome, Ease(t)));
        if (chapterMusic != null) chapterMusic.PlayBoss(3);
        if (bossBar != null) bossBar.ShowEncounter(boss);
        bossCollider.enabled = true;
        yield return new WaitForSeconds(0.45f);

        while (true)
        {
            yield return LaserSweep();
            yield return new WaitForSeconds(0.60f);
            yield return WarningDash();
            yield return new WaitForSeconds(0.55f);
            yield return MissileVolley();
            yield return new WaitForSeconds(0.80f);
            if (damagedPhase)
            {
                yield return ElectricField();
                yield return new WaitForSeconds(0.60f);
                yield return EnergyBallVolley();
                yield return new WaitForSeconds(0.75f);
            }
        }
    }

    private void EnterArena()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Wind, .52f);
        returnPosition = player.transform.position;
        cameraLock.Lock(cameraManager);
        previousCameraSize = cameraLock.Camera.orthographicSize;
        Camera framing = layout.FramingCamera;
        cameraLock.Camera.orthographicSize = framing.orthographicSize;
        cameraLock.Position = framing.transform.position;
        framing.enabled = false;
        layout.gameObject.SetActive(true);
        cameraLock.Update();

        hazards = new GameObject("진행 중인 공격 효과").transform;
        hazards.SetParent(layout.AttackEffects, false);
        Texture2D white = Texture2D.whiteTexture;
        square = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
        background = layout.Clouds;
        backgroundScale = background.transform.localScale;
        backgroundSize = background.bounds.size;
        SetUpCloudDrift();
        arisa = layout.ArisaArt;
        arisaArtOffset = arisa.transform.position - layout.ArisaSpawn.position;
        arisaArtColor = arisa.color;
        arisaHome = arisa.transform.localPosition;
        arisaHomeFlip = arisa.flipX;

        player.TryGetComponent(out SubmachineGunFire gun);
        layout.ArisaArms.Bind(gun, aim);
        movement.BeginFlight(cameraLock.Camera, layout.ArisaSpawn.position, arisa, layout.ArisaArms);
        previousPlayerHp = player.CurrentHp;
        player.HpChanged.AddListener(OnPlayerHpChanged);
        player.Defeated.AddListener(OnPlayerDefeated);
        boss = layout.Boss;
        boss.ResetForMission();
        CacheHullPose();
        bossHome = boss.transform.position;
        boss.transform.position = OutsideArena(1.18f, bossHome.y);
        bossCollider = boss.GetComponent<Collider2D>();
        bossCollider.enabled = false;
        boss.HpChanged.AddListener(OnBossHpChanged);
        boss.Defeated.AddListener(OnBossDefeated);
        UpdatePresentation();
    }

    private IEnumerator LaserSweep()
    {
        bool rises = Random.value < 0.5f;
        float from = At(0f, rises ? 0.17f : 0.83f).y;
        float to = At(0f, rises ? 0.83f : 0.17f).y;
        yield return Animate(0.36f, t => MoveBossVertically(Mathf.Lerp(bossHome.y, from, Ease(t))));

        SpriteRenderer glow = AddSprite("레이저 예고와 외곽광", square, hazards, 30);
        SpriteRenderer core = AddSprite("레이저 중심광", square, hazards, 31);
        core.color = BeamCore;
        core.enabled = false;
        yield return Animate(0.66f, t =>
        {
            glow.color = WithAlpha(WarningRed, Mathf.Lerp(0.30f, 0.9f, Mathf.PingPong(t * 4f, 1f)));
            DrawBeam(glow, 0.12f);
        });

        bool hit = false;
        glow.color = WithAlpha(BeamPink, 0.82f);
        core.enabled = true;
        GameplaySfx.Play(GameplaySfx.Cue.Laser, .62f);
        yield return Animate(1.48f, t =>
        {
            MoveBossVertically(Mathf.Lerp(from, to, t));
            Rect beam = DrawBeam(glow, 0.44f);
            DrawBeam(core, 0.15f);
            TryHit(beam, ref hit);
        });
        RemoveEffect(core);
        RemoveEffect(glow);
        yield return Animate(0.42f, t => MoveBossVertically(Mathf.Lerp(to, bossHome.y, Ease(t))));
    }

    private Rect DrawBeam(SpriteRenderer beam, float height)
    {
        Vector3 muzzle = layout.LaserMuzzle.position;
        float left = Arena.xMin;
        var area = Rect.MinMaxRect(left, muzzle.y - height * 0.5f, muzzle.x, muzzle.y + height * 0.5f);
        DrawRectangle(beam, area);
        return area;
    }

    private IEnumerator WarningDash()
    {
        Vector3 exit = OutsideArena(1.22f, bossHome.y);
        yield return Animate(0.29f, t => boss.transform.position = Vector3.Lerp(bossHome, exit, Ease(t)));
        bool upper = Random.value < 0.5f;
        float lane = upper ? 0.75f : 0.25f;
        SpriteRenderer warning = AddSprite("돌진 위험 구역", square, hazards, 6);
        GameplaySfx.Play(GameplaySfx.Cue.Warning, .54f);
        SpriteRenderer border = AddSprite("돌진 구역 경계", square, hazards, 7);
        yield return Animate(0.92f, t =>
        {
            Rect arena = Arena;
            float boundary = arena.center.y;
            var danger = Rect.MinMaxRect(arena.xMin, upper ? boundary : arena.yMin,
                arena.xMax, upper ? arena.yMax : boundary);
            warning.color = WithAlpha(WarningRed, Mathf.Lerp(0.17f, 0.39f, Mathf.PingPong(t * 4f, 1f)));
            border.color = WithAlpha(WarningRed, 0.93f);
            DrawRectangle(warning, danger);
            DrawRectangle(border, new Rect(arena.xMin, boundary - 0.06f, arena.width, 0.12f));
        });
        RemoveEffect(warning);
        RemoveEffect(border);

        bool hit = false;
        Vector3 previous = At(1.22f, lane);
        Vector2 impactExtents = bossCollider.bounds.extents;
        LineRenderer boostGlow = AddJet("돌진 부스트 외곽광", 18, 0.54f);
        LineRenderer boostCore = AddJet("돌진 부스트 중심광", 19, 0.25f);
        GameplaySfx.Play(GameplaySfx.Cue.Dash, .61f);
        yield return Animate(0.39f, t =>
        {
            Vector3 next = At(Mathf.Lerp(1.22f, -0.23f, t), lane);
            boss.transform.position = next;
            PresentBoost(boostGlow, boostCore, t);
            Rect impact = Rect.MinMaxRect(next.x - impactExtents.x, next.y - impactExtents.y,
                previous.x + impactExtents.x, next.y + impactExtents.y);
            TryHit(impact, ref hit);
            previous = next;
        });
        Destroy(boostGlow.gameObject);
        Destroy(boostCore.gameObject);
        ResetHullPose();

        yield return new WaitForSeconds(0.26f);
        float returnY = At(0f, 1.24f).y;
        yield return Animate(0.55f, t => MoveBossVertically(Mathf.Lerp(returnY, bossHome.y, Ease(t))));
    }

    private IEnumerator MissileVolley()
    {
        SpriteRenderer flash = AddSprite("미사일 포드 점화", square, hazards, 27);
        flash.transform.position = (layout.UpperMissilePod.position + layout.LowerMissilePod.position) * 0.5f;
        flash.transform.localScale = new Vector3(0.20f, 0.64f, 1f);
        yield return Animate(0.48f, t => flash.color = WithAlpha(BeamCore, Mathf.PingPong(t * 4f, 1f)));
        RemoveEffect(flash);

        for (int i = 0; i < 5; i++)
        {
            bool upper = i % 2 == 0;
            Transform pod = upper ? layout.UpperMissilePod : layout.LowerMissilePod;
            StartCoroutine(LaunchMissile(pod.position, upper));
            yield return new WaitForSeconds(0.19f);
        }
        yield return new WaitForSeconds(0.55f);
    }

    private IEnumerator LaunchMissile(Vector3 mount, bool upper)
    {
        GameplaySfx.Play(GameplaySfx.Cue.Missile, .43f);
        StartCoroutine(PlayPodIgnition(mount));
        SpriteRenderer missile = AddSprite("등 뒤 미사일", missileSprite, hazards, 19);
        missile.transform.localScale = FitHeight(missileSprite, 0.48f);
        Vector2 size = missileSprite.bounds.size;
        SpriteRenderer exhaust = AddSprite("미사일 배기광", square, missile.transform, 27);
        exhaust.color = new Color(1f, 0.64f, 0.22f, 0.92f);
        exhaust.transform.localPosition = new Vector3(-size.x * 0.56f, 0f);
        exhaust.transform.localScale = new Vector3(size.x * 0.38f, size.y * 0.28f, 1f);
        Vector3 turn = mount + new Vector3(0.58f, upper ? 1.18f : -1.08f);
        Vector3 enclosed = mount - Vector3.right * 0.31f;
        Vector3 ejection = mount + Vector3.right * 0.64f;
        yield return Animate(0.27f, t =>
        {
            float remaining = 1f - t;
            missile.transform.position = remaining * remaining * enclosed + 2f * remaining * t * ejection + t * t * turn;
            Vector3 delta = Vector3.Lerp(ejection - enclosed, turn - ejection, t);
            missile.sortingOrder = t < 0.19f ? 19 : 28;
            exhaust.enabled = t >= 0.19f;
            missile.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        });
        Vector2 target = playerCollider != null ? playerCollider.bounds.center : player.transform.position;
        Vector2 direction = target - (Vector2)turn;
        missile.gameObject.AddComponent<EnemyProjectile2D>().Launch(direction, 10.2f, 1, 0, 0.22f, 4.5f);
    }

    private void TryHit(Rect area, ref bool hit)
    {
        if (hit || playerCollider == null || player.IsDead) return;
        Bounds bounds = playerCollider.bounds;
        if (!area.Overlaps(Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y))) return;
        int hp = player.CurrentHp;
        player.TakeDamage(1, boss.transform.position);
        hit = player.CurrentHp != hp;
    }

    private void OnBossDefeated()
    {
        StopAttacks();
        PhaseOneCleared = true;
        if (player != null && !player.IsDead)
        {
            GameplayInput.Block(this);
            StartCoroutine(FinishCampaign());
        }
    }

    private IEnumerator FinishCampaign()
    {
        GameManager manager = GameManager.Instance;
        int finalScore = manager != null ? manager.SaveFinalScore() : 0;
        yield return Retreat();
        if (player != null) player.HealToFull();
        completionBanner = MissionCompleteBanner.Show(gameObject.scene);
        while (completionBanner != null) yield return null;
        if (player != null && !player.IsDead)
        {
            if (manager != null) manager.SetEndingCursor(true);
            credits = EndingCredits.Show(gameObject.scene, finalScore, manager);
        }
    }
    private IEnumerator Retreat()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Wind, .45f);
        SpriteRenderer hull = boss.GetComponentInChildren<SpriteRenderer>();
        Color hullColor = hull.color;
        Vector3 start = boss.transform.position;
        yield return Animate(0.9f, t =>
        {
            boss.transform.position = Vector3.Lerp(start, At(1.23f, 0.83f), Ease(t));
            hull.color = WithAlpha(hullColor, hullColor.a * (1f - t));
        });
        boss.gameObject.SetActive(false);
    }

    private void OnPlayerDefeated() => StopAttacks();

    private void StopAttacks()
    {
        StopAllCoroutines();
        HideEntryFlash();
        GameplayInput.Unblock(this);
        ResetHullPose();
        if (boss != null)
        {
            boss.HpChanged.RemoveListener(OnBossHpChanged);
            boss.Defeated.RemoveListener(OnBossDefeated);
            bossCollider.enabled = false;
            if (bossBar != null) bossBar.HideEncounter(boss);
        }
        if (hazards != null)
        {
            hazards.gameObject.SetActive(false);
            Destroy(hazards.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (!cameraLock.IsLocked) return;
        cameraLock.Update();
        UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        Rect arena = Arena;
        float cover = Mathf.Max(1f, Mathf.Max(arena.width / backgroundSize.x, arena.height / backgroundSize.y));
        background.transform.localScale = backgroundScale * cover;
        if (cloudScrollMaterial != null && !GameplayPause.IsPaused)
        {
            cloudTravel = Mathf.Repeat(cloudTravel + Time.deltaTime * layout.CloudDriftSpeed, 2f);
            cloudScrollMaterial.SetFloat(CloudTravelProperty, cloudTravel);
        }
        if (player == null) return;

        arisa.transform.position = player.transform.position + arisaArtOffset
            + Vector3.up * (0.055f * Mathf.Sin(Time.time * 9f));
        arisa.flipX = aim != null && aim.IsFacingLeft;
        arisa.color = Time.time < playerFlashUntil || player.IsDead
            ? arisaArtColor * new Color(1f, 0.43f, 0.44f) : arisaArtColor;
        Camera camera = cameraLock.Camera;
        Vector3 aimTarget = arisa.transform.position + (arisa.flipX ? Vector3.left : Vector3.right) * 8f;
        if (movement.TryGetMouseScreenPosition(out Vector2 cursor))
        {
            float depth = arisa.transform.position.z - camera.transform.position.z;
            aimTarget = camera.ScreenToWorldPoint(new Vector3(cursor.x, cursor.y, depth));
        }
        layout.ArisaArms.Present(aimTarget, arisa.flipX, arisa.color);
    }

    private void SetUpCloudDrift()
    {
        if (layout.CloudScrollShader == null || layout.CloudDriftSpeed <= 0f) return;
        originalCloudMaterial = background.sharedMaterial;
        Sprite sprite = background.sprite;
        Rect rect = sprite.textureRect;
        Texture2D texture = sprite.texture;
        cloudScrollMaterial = new Material(layout.CloudScrollShader);
        cloudScrollMaterial.SetVector(CloudSpriteRectProperty, new Vector4(
            rect.x / texture.width, rect.y / texture.height,
            rect.width / texture.width, rect.height / texture.height));
        cloudTravel = 0f;
        background.sharedMaterial = cloudScrollMaterial;
    }

    private void OnPlayerHpChanged(int hp, int maximum)
    {
        if (hp < previousPlayerHp) playerFlashUntil = Time.time + 0.18f;
        previousPlayerHp = hp;
    }

    private void OnDisable() => EndArena();

    internal void CancelForTest() => EndArena();

    private void EndArena()
    {
        if (!started) return;
        StopAttacks();
        if (player != null)
        {
            player.HpChanged.RemoveListener(OnPlayerHpChanged);
            player.Defeated.RemoveListener(OnPlayerDefeated);
        }
        if (movement != null && cameraLock.IsLocked) movement.EndFlight(returnPosition);
        if (cameraLock.Camera != null) cameraLock.Camera.orthographicSize = previousCameraSize;
        cameraLock.Release();
        if (completionBanner != null) completionBanner.Hide();
        if (credits != null)
        {
            credits.SetActive(false);
            Destroy(credits);
            if (GameManager.Instance != null) GameManager.Instance.SetEndingCursor(false);
        }
        if (arisa != null)
        {
            arisa.transform.localPosition = arisaHome;
            arisa.flipX = arisaHomeFlip;
            arisa.color = arisaArtColor;
            layout.ArisaArms.RestorePose();
        }
        if (background != null) background.transform.localScale = backgroundScale;
        if (cloudScrollMaterial != null)
        {
            if (background != null) background.sharedMaterial = originalCloudMaterial;
            Destroy(cloudScrollMaterial);
            cloudScrollMaterial = null;
        }
        if (layout != null) layout.gameObject.SetActive(false);
        if (boss != null)
        {
            boss.transform.position = bossHome;
            RestoreHullColor();
            boss.gameObject.SetActive(true);
        }
        if (square != null) Destroy(square);
        if (energyBallSprite != null) Destroy(energyBallSprite);
        if (energyBallTexture != null) Destroy(energyBallTexture);
        if (effectsMaterial != null) Destroy(effectsMaterial);
        effectsMaterial = null;
        boss = null;
        started = false;
    }

    private Vector3 At(float x, float y)
    {
        Rect arena = Arena;
        return new Vector3(arena.xMin + x * arena.width, arena.yMin + y * arena.height, bossHome.z);
    }

    private Vector3 OutsideArena(float x, float y) => new Vector3(At(x, 0f).x, y, bossHome.z);

    private void MoveBossVertically(float y) => boss.transform.position = new Vector3(bossHome.x, y, bossHome.z);

    private static SpriteRenderer AddSprite(string objectName, Sprite sprite, Transform parent, int order)
    {
        var visual = new GameObject(objectName).AddComponent<SpriteRenderer>();
        visual.transform.SetParent(parent, false);
        visual.sprite = sprite;
        visual.sortingOrder = order;
        return visual;
    }

    private static void RemoveEffect(SpriteRenderer renderer)
    {
        renderer.enabled = false;
        Destroy(renderer.gameObject);
    }

    private static void DrawRectangle(SpriteRenderer renderer, Rect rect)
    {
        renderer.transform.position = rect.center;
        renderer.transform.localScale = new Vector3(rect.width, rect.height, 1f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Vector3 FitHeight(Sprite sprite, float height)
    {
        float scale = height / sprite.bounds.size.y;
        return new Vector3(scale, scale, 1f);
    }

    private static float Ease(float t) => Mathf.SmoothStep(0f, 1f, t);

    private static IEnumerator Animate(float duration, Action<float> update)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            update(elapsed / duration);
            yield return null;
        }
        update(1f);
    }
}
