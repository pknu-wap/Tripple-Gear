using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth), typeof(SpriteRenderer))]
public sealed class SummitAirshipBoss : MonoBehaviour
{
    private enum Pattern { LaserSweep, EnergyShot, Summon, WindLaser, DebrisRain }

    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private BoxCollider2D arenaFloor;
    [SerializeField, KoreanLabel("기계실 이동")] private TeleportTrigger boardingTeleport;
    [SerializeField] private ArisaHealth player;
    [SerializeField] private MonsterHealth[] summonPrefabs;
    [SerializeField] private Material effectMaterial;
    [SerializeField] private GameObject burstPrefab;
    [SerializeField] private Shader energyImpactShader;
    [SerializeField] private Sprite[] debrisSprites;
    [SerializeField] private Image bossBarFill;
    [SerializeField] private Color phaseTwoBarColor = new Color(0.72f, 0.085f, 0.08f);
    [SerializeField] private Color criticalBarColor = new Color(1f, 0.46f, 0.07f);
    [SerializeField, Min(1)] private int laserDamage = 1;
    [SerializeField, Min(1)] private int energyDamage = 2;

    private const int LaserLanes = 12;
    private const int SafeLanes = 2;
    private const int PhaseTwoPercent = 70;
    private const int CriticalPercent = 50;
    private const int OpeningPatternCount = 3;
    private const float WindDuration = 6f;
    private const float HatchOpeningSeconds = 0.72f;
    private const float BoardingPullSeconds = 1.7f;
    private const int HatchTextureWidth = 96;
    private const int HatchTextureHeight = 36;
    private const int SummonLimit = 6;
    private const float LaserWarningTime = 1f;
    private const float LaserInterval = 0.82f;
    private const float LaserPulseDuration = 0.36f;
    private const float EnergyOrbDiameter = 6.7f;
    private const float EnergyImpactRadius = 4.25f;
    private const float LaserWidthRatio = 0.83f;
    private const float GroundPenetrationSeconds = 1.5f;
    private static readonly Color WarningColor = new Color(1f, 0.21f, 0.10f);
    private static readonly Color EnergyColor = new Color(0.65f, 0.89f, 0.80f);
    private static readonly Color SafeColor = new Color(0.31f, 1f, 0.65f);
    // Underside of BeachLaserAirship.png at the center of each sweep lane, measured from the top.
    private static readonly int[] LaserMountRows = { 280, 363, 381, 445, 479, 508, 496, 453, 417, 356, 324, 244 };
    // Lens centers in the existing beach mission sprite.
    private static readonly Vector2[] Lenses =
    {
        new Vector2(692f / 2172f, 1f - 433f / 724f),
        new Vector2(1074f / 2172f, 1f - 488f / 724f),
        new Vector2(1439f / 2172f, 1f - 424f / 724f),
    };

    private readonly List<MonsterHealth> summons = new List<MonsterHealth>();
    private readonly Pattern[] rotation = new Pattern[5];
    private MonsterHealth health;
    private SpriteRenderer hull;
    private Collider2D[] hitboxes;
    private Collider2D playerBody;
    private ArisaHorizontalMovement playerMovement;
    private bool phaseTwo;
    private bool critical;
    private Color normalBarColor;
    private Camera view;
    private Transform effects;
    private Texture2D effectTexture;
    private Sprite softLight;
    private Sprite solidLight;
    private Sprite hatchOpeningSprite;
    private Sprite hatchLeftSprite;
    private Sprite hatchRightSprite;
    private Rigidbody2D boardingBody;
    private SubmachineGunFire boardingGun;
    private bool boarding;
    private bool boardingHitPresentation;
    private bool restoreBodySimulation;
    private bool restoreMovement;
    private bool previousGunInputSuppression;
    private Material energyMaterial;
    private Sprite rockFragment;
    private Texture2D attackTexture;
    private readonly Sprite[] orbFrames = new Sprite[4];
    private readonly Sprite[] laserFrames = new Sprite[4];
    private readonly SpriteRenderer[] laserMounts = new SpriteRenderer[LaserLanes];
    private Sprite laserMountSprite;
    private Vector3 restPosition;
    private Vector3 flightOrigin;
    private Bounds arena;

    private float FloorY => arena.max.y;
    private Vector2 PlayerCenter => playerBody.bounds.center;

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
        hull = GetComponent<SpriteRenderer>();
        hitboxes = GetComponents<Collider2D>();
        playerBody = player.GetComponent<CapsuleCollider2D>();
        playerMovement = player.GetComponent<ArisaHorizontalMovement>();
        if (bossBarFill != null) normalBarColor = bossBarFill.color;
        CreateLightSprites();
        hatchLeftSprite = CreateHatchCover(969);
        hatchRightSprite = CreateHatchCover(1074);
        CreateAttackSprites();
        for (int i = 0; i < laserMounts.Length; i++)
        {
            var mount = new GameObject($"하부 레이저 발사구 {i + 1:00}").AddComponent<SpriteRenderer>();
            mount.transform.SetParent(transform, false);
            mount.sprite = laserMountSprite;
            mount.sharedMaterial = effectMaterial;
            mount.sortingOrder = hull.sortingOrder + 2;
            laserMounts[i] = mount;
        }
    }

    public void BeginBattle()
    {
        health.ResetForMission();
        phaseTwo = false;
        critical = false;
        if (bossBarFill != null) bossBarFill.color = normalBarColor;
        health.HpChanged.AddListener(CheckPhase);
        arena = arenaFloor.bounds;
        view = cameraManager.ManagedCamera;
        energyMaterial ??= new Material(energyImpactShader) { mainTexture = attackTexture };
        energyMaterial.SetFloat("_GroundY", FloorY);
        effects = new GameObject("산 정상 보스 전투 효과").transform;
        effects.SetParent(transform.parent, false);
        PositionHull();
        restPosition = transform.position;
        flightOrigin = restPosition + Vector3.up * (view.orthographicSize * 0.75f);
        transform.position = flightOrigin;
        SetHitboxes(false);
        StartCoroutine(Battle());
    }

    private IEnumerator Battle()
    {
        yield return Animate(1.05f, progress =>
            transform.position = Vector3.Lerp(flightOrigin, restPosition, Mathf.SmoothStep(0f, 1f, progress)));
        SetHitboxes(true);
        yield return new WaitForSeconds(0.6f);
        while (!health.IsDead && !player.IsDead)
        {
            if (critical)
            {
                var (first, second) = PickPatternPair();
                yield return PlayPatternPair(first, second);
                yield return new WaitForSeconds(0.9f);
                continue;
            }

            // Show each attack once per cycle so random ordering never starves a pattern.
            int patternCount = phaseTwo ? rotation.Length : OpeningPatternCount;
            for (int i = 0; i < patternCount; i++) rotation[i] = (Pattern)i;
            for (int i = patternCount - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                (rotation[i], rotation[other]) = (rotation[other], rotation[i]);
            }
            for (int i = 0; i < patternCount; i++)
            {
                if (health.IsDead || player.IsDead || critical ||
                    (phaseTwo && patternCount == OpeningPatternCount)) break;
                Pattern pattern = rotation[i];
                yield return PlayPattern(pattern);
                if (pattern != Pattern.EnergyShot) yield return new WaitForSeconds(0.9f);
            }
        }
    }

    private (Pattern first, Pattern second) PickPatternPair()
    {
        bool canSummon = AvailableSummonCount() > 0;
        int count = 0;
        for (int i = 0; i < rotation.Length; i++)
        {
            Pattern pattern = (Pattern)i;
            if (pattern != Pattern.Summon || canSummon) rotation[count++] = pattern;
        }

        int first = Random.Range(0, count);
        int second = Random.Range(0, count - 1);
        if (second >= first) second++;
        return (rotation[first], rotation[second]);
    }

    private IEnumerator PlayPatternPair(Pattern first, Pattern second)
    {
        bool firstFinished = false;
        IEnumerator PlayFirst()
        {
            yield return PlayPattern(first);
            firstFinished = true;
        }

        Coroutine firstAttack = StartCoroutine(PlayFirst());
        yield return PlayPattern(second);
        if (!firstFinished) yield return firstAttack;
    }

    private IEnumerator PlayPattern(Pattern pattern) => pattern switch
    {
        Pattern.LaserSweep => LaserSweep(),
        Pattern.EnergyShot => EnergyShot(),
        Pattern.Summon => Summon(),
        Pattern.WindLaser => WindLaser(),
        Pattern.DebrisRain => DebrisRain(),
        _ => throw new System.ArgumentOutOfRangeException(nameof(pattern))
    };

    private void CheckPhase(int currentHp, int maxHp)
    {
        if (currentHp <= 0) return;
        if (!phaseTwo && currentHp * 100 <= maxHp * PhaseTwoPercent)
        {
            phaseTwo = true;
            if (bossBarFill != null) bossBarFill.color = phaseTwoBarColor;
            StartCoroutine(FlashLaserMounts());
        }
        if (!critical && currentHp * 100 <= maxHp * CriticalPercent)
        {
            critical = true;
            if (bossBarFill != null) bossBarFill.color = criticalBarColor;
            StartCoroutine(PlayHullDamage());
        }
    }

    private IEnumerator FlashLaserMounts()
    {
        var flashes = new SpriteRenderer[laserMounts.Length];
        for (int i = 0; i < flashes.Length; i++)
            flashes[i] = NewLight("2페이즈 발사구 점멸", softLight);
        float startedAt = Time.time;
        while (!health.IsDead && !player.IsDead)
        {
            for (int i = 0; i < flashes.Length; i++)
            {
                float pulse = WarningPulse(Time.time - startedAt + i * 0.035f, 4.3f);
                Place(flashes[i], Nozzle(laserMounts[i]), Vector2.one * Mathf.Lerp(0.85f, 2.45f, pulse),
                    WithAlpha(new Color(1f, 0.55f, 0.18f), pulse));
            }
            yield return null;
        }
        foreach (SpriteRenderer flash in flashes) Destroy(flash.gameObject);
    }

    private IEnumerator PlayHullDamage()
    {
        while (!health.IsDead && !player.IsDead)
        {
            Vector2 position = Nozzle(laserMounts[Random.Range(0, laserMounts.Length)]);
            position += new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(0.1f, 0.6f));
            PlayBurst(position, Random.Range(0.42f, 0.65f), "비행선 기체 파손 폭발");
            SpriteRenderer shard = NewLight("비행선 기체 파손 조각", debrisSprites[Random.Range(0, debrisSprites.Length)]);
            StartCoroutine(ThrowRock(shard, position));
            yield return new WaitForSeconds(Random.Range(0.16f, 0.27f));
        }
    }

    private void PositionHull()
    {
        float size = view.orthographicSize;
        Vector2 spriteSize = hull.sprite.rect.size / hull.sprite.pixelsPerUnit;
        Vector3 hullScale = transform.localScale;
        hullScale.x = arena.size.x * 1.035f / spriteSize.x;
        hullScale.y = size * 1.56f / spriteSize.y;
        transform.localScale = hullScale;
        transform.position = new Vector3(arena.center.x, FloorY + size * 1.52f, transform.position.z);
        Sprite sprite = hull.sprite;
        for (int i = 0; i < laserMounts.Length; i++)
        {
            float x = Mathf.Lerp(arena.min.x, arena.max.x, (i + 0.5f) / LaserLanes);
            float localY = (sprite.rect.height * (1f - LaserMountRows[i] / 724f) - sprite.pivot.y) / sprite.pixelsPerUnit;
            SpriteRenderer mount = laserMounts[i];
            mount.transform.localPosition = new Vector3((x - transform.position.x) / hullScale.x, localY, 0f);
            mount.transform.localScale = new Vector3(1.32f / hullScale.x, 0.95f / hullScale.y, 1f);
        }
    }

    private IEnumerator LaserSweep()
    {
        bool leftToRight = Random.value < 0.5f;
        const int attackLanes = LaserLanes - SafeLanes;
        float laneWidth = arena.size.x / LaserLanes;
        float safeWidth = laneWidth * SafeLanes;
        float safeX = leftToRight ? arena.max.x - safeWidth * 0.5f : arena.min.x + safeWidth * 0.5f;
        SpriteRenderer shelter = NewLight("레이저 회피 구역", solidLight);
        Place(shelter, new Vector2(safeX, FloorY + 0.13f), new Vector2(safeWidth, 0.26f), SafeColor);
        StartCoroutine(BlinkShelter(shelter));
        yield return new WaitForSeconds(0.8f);
        for (int i = 0; i < attackLanes; i++)
        {
            int lane = leftToRight ? i : LaserLanes - 1 - i;
            StartCoroutine(FireLane(laserMounts[lane], laneWidth));
            yield return new WaitForSeconds(LaserInterval);
        }
        yield return new WaitForSeconds(LaserWarningTime + LaserPulseDuration - LaserInterval);
        Destroy(shelter.gameObject);
    }

    private static IEnumerator BlinkShelter(SpriteRenderer shelter)
    {
        float start = Time.time;
        while (shelter != null)
        {
            shelter.color = WithAlpha(SafeColor, Mathf.Lerp(0.24f, 1f, WarningPulse(Time.time - start, 2.5f)));
            yield return null;
        }
    }

    private IEnumerator FireLane(SpriteRenderer mount, float width)
    {
        return FireBeam(Nozzle(mount), width * LaserWidthRatio, LaserWarningTime, LaserPulseDuration, laserDamage);
    }

    private static Vector2 Nozzle(SpriteRenderer mount) =>
        (Vector2)mount.transform.position + Vector2.down * 0.21f;

    private IEnumerator FireBeam(Vector2 nozzle, float beamWidth, float warningSeconds, float pulseDuration, int damage)
    {
        SpriteRenderer glow = NewLight("레이저 경고", solidLight);
        SpriteRenderer emitter = NewLight("발사구 점멸", softLight);
        float top = nozzle.y;
        Vector2 center = new Vector2(nozzle.x, (FloorY + top) * 0.5f);
        float height = top - FloorY;
        glow.sortingOrder = hull.sortingOrder + 1;
        if (warningSeconds > 0f)
        {
            GameplaySfx.Play(GameplaySfx.Cue.Warning, 0.4f);
            yield return Animate(warningSeconds, progress =>
            {
                float pulse = WarningPulse(progress * warningSeconds, 4f);
                Place(glow, center, new Vector2(beamWidth, height), WithAlpha(WarningColor, Mathf.Lerp(0.07f, 0.45f, pulse)));
                Place(emitter, nozzle, Vector2.one * 1.45f, WithAlpha(WarningColor, Mathf.Lerp(0.18f, 1f, pulse)));
            });
        }
        GameplaySfx.Play(GameplaySfx.Cue.Laser, 0.57f);
        glow.name = "순차 레이저";
        Bounds beam = new Bounds(center, new Vector2(beamWidth, height));
        bool hit = false;
        yield return Animate(pulseDuration, progress =>
        {
            glow.sprite = laserFrames[AttackFrame()];
            Place(glow, center, new Vector2(beam.size.x / glow.sprite.bounds.size.x, height),
                WithAlpha(Color.white, 1f - 0.32f * progress));
            Place(emitter, nozzle, Vector2.one * 1.7f, WithAlpha(new Color(1f, 0.78f, 0.44f), 1f - progress));
            if (!hit && playerBody.bounds.Intersects(beam))
            {
                hit = true;
                player.TakeDamage(damage, center);
            }
        });
        Destroy(glow.gameObject);
        Destroy(emitter.gameObject);
    }

    private IEnumerator WindLaser()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Wind, .55f);
        Vector2 nozzle = Lens(1);
        float center = nozzle.x;
        const float width = 8.5f;
        SpriteRenderer danger = NewLight("중앙 레이저 발사 예고", solidLight);
        var streaks = new SpriteRenderer[24];
        for (int i = 0; i < streaks.Length; i++)
            streaks[i] = NewLight("중앙 흡입 바람", solidLight);

        yield return Animate(WindDuration, progress =>
        {
            playerMovement.SetWindPull(center, Mathf.Lerp(2.5f, 5.5f, progress));
            float pulse = WarningPulse(progress * WindDuration, 3.5f);
            Place(danger, new Vector2(center, FloorY + 0.15f), new Vector2(width, 0.3f), WithAlpha(WarningColor, pulse));
            DrawWindStreaks(streaks, nozzle, progress * WindDuration);
        });
        playerMovement.SetWindPull(0f, 0f);
        foreach (SpriteRenderer streak in streaks) Destroy(streak.gameObject);
        Destroy(danger.gameObject);
        yield return FireBeam(nozzle, width, 0f, 0.82f, energyDamage);
    }

    private void DrawWindStreaks(SpriteRenderer[] streaks, Vector2 nozzle, float elapsed)
    {
        for (int i = 0; i < streaks.Length; i++)
        {
            float flow = Mathf.Repeat(i * 0.173f + elapsed * 0.93f, 1f);
            float side = i % 2 == 0 ? -1f : 1f;
            float origin = (i % 4 < 2 ? arena.extents.x : arena.extents.x * 0.58f);
            float x = nozzle.x + side * origin * (1f - flow);
            float y = Mathf.Lerp(FloorY + 0.45f + i % 6 * 0.54f, nozzle.y - 0.7f, flow * flow);
            Place(streaks[i], new Vector2(x, y), new Vector2(Mathf.Lerp(2.4f, 0.4f, flow), 0.045f),
                new Color(0.76f, 0.90f, 0.9f, Mathf.Sin(flow * Mathf.PI) * 0.64f));
            streaks[i].transform.rotation = Quaternion.Euler(0, 0, -side * flow * 62f);
        }
    }

    private IEnumerator DebrisRain()
    {
        const int dropCount = 9;
        float left = arena.min.x + 0.85f;
        float laneWidth = (arena.size.x - 1.7f) / dropCount;
        var positions = new float[dropCount];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = left + laneWidth * (i + Random.Range(0.05f, 0.95f));

        // Vary the order while guaranteeing a drop in every part of the arena.
        for (int i = positions.Length - 1; i >= 0; i--)
        {
            int pick = Random.Range(0, i + 1);
            float x = positions[pick];
            positions[pick] = positions[i];
            StartCoroutine(DropDebris(x, debrisSprites[Random.Range(0, debrisSprites.Length)]));
            yield return new WaitForSeconds(0.23f);
        }
        yield return new WaitForSeconds(1.4f);
    }

    private IEnumerator DropDebris(float x, Sprite sprite)
    {
        Vector2 ground = new Vector2(x, FloorY + 0.13f);
        SpriteRenderer marker = NewLight("로봇 파편 낙하 예고", solidLight);
        yield return Animate(0.55f, progress =>
            Place(marker, ground, new Vector2(1.65f, 0.14f), WithAlpha(WarningColor, WarningPulse(progress, 2f))));
        SpriteRenderer fragment = NewLight("떨어지는 로봇 파편", sprite);
        float scale = Random.Range(0.85f, 1.55f) / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float spin = Random.Range(-220f, 220f);
        float startY = Mathf.Max(Lens(1).y + 2f, view.transform.position.y + view.orthographicSize);
        bool hit = false;
        yield return Animate(0.86f, progress =>
        {
            Vector2 position = new Vector2(x, Mathf.Lerp(startY, ground.y, progress * progress));
            Place(fragment, position, Vector2.one * scale, Color.white);
            fragment.transform.rotation = Quaternion.Euler(0f, 0f, spin * progress);
            if (!hit && (playerBody.ClosestPoint(position) - position).sqrMagnitude < 0.49f)
            {
                hit = true;
                player.TakeDamage(laserDamage, position);
            }
        });
        Destroy(marker.gameObject);
        Destroy(fragment.gameObject);
        PlayBurst(ground, 0.55f);
    }

    private IEnumerator EnergyShot()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Energy, .55f);
        var charges = new SpriteRenderer[Lenses.Length];
        var sparks = new SpriteRenderer[Lenses.Length * 4];
        for (int i = 0; i < charges.Length; i++) charges[i] = NewLight("발사구 에너지 충전", orbFrames[0]);
        for (int i = 0; i < sparks.Length; i++) sparks[i] = NewLight("모이는 에너지", solidLight);
        SpriteRenderer orb = NewLight("대형 에너지볼", orbFrames[0]);
        orb.sharedMaterial = energyMaterial;
        SpriteRenderer warning = NewLight("에너지볼 착탄 경고", solidLight);
        float radius = EnergyOrbDiameter * 0.5f;
        Vector2 merge = Lens(1) + Vector2.down * 1.65f;
        merge.y = Mathf.Max(merge.y, FloorY + radius + 1f);
        orb.enabled = warning.enabled = false;

        yield return Animate(2.2f, progress =>
        {
            for (int i = 0; i < charges.Length; i++)
            {
                Vector2 lens = Lens(i);
                DrawOrb(charges[i], lens, Mathf.Lerp(0.6f, 3.45f, progress));
                for (int j = 0; j < 4; j++)
                {
                    float phase = Mathf.Repeat(progress * 2.3f + j * 0.25f, 1f);
                    float angle = (j + i) * 1.57f + progress * 2f;
                    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (1f - phase) * 3.1f;
                    Place(sparks[i * 4 + j], lens + offset, new Vector2(0.24f, 0.18f), WithAlpha(EnergyColor, phase));
                }
            }
        });
        foreach (SpriteRenderer spark in sparks) Destroy(spark.gameObject);
        orb.enabled = true;
        yield return Animate(0.95f, progress =>
        {
            for (int i = 0; i < charges.Length; i++)
                DrawOrb(charges[i], Vector2.Lerp(Lens(i), merge, progress), Mathf.Lerp(3.45f, 1f, progress));
            DrawOrb(orb, merge, Mathf.Lerp(1f, EnergyOrbDiameter, progress));
        });
        foreach (SpriteRenderer charge in charges) Destroy(charge.gameObject);

        Vector2 target = new Vector2(Mathf.Clamp(PlayerCenter.x, arena.min.x + 1.8f, arena.max.x - 1.8f), FloorY + 0.35f);
        warning.enabled = true;
        yield return Animate(0.85f, progress =>
        {
            DrawOrb(orb, merge, EnergyOrbDiameter + Mathf.Sin(progress * 20f) * 0.27f);
            Place(warning, new Vector2(target.x, FloorY + 0.16f), new Vector2(EnergyImpactRadius * 2f, 0.29f),
                WithAlpha(WarningColor, Mathf.Lerp(0.35f, 1f, progress)));
        });
        bool hit = false;
        Vector2 contact = new Vector2(target.x, FloorY + radius);
        yield return Animate(1.45f, progress =>
        {
            Vector2 position = Vector2.Lerp(merge, contact, progress * (1.2f - 0.2f * progress));
            DrawOrb(orb, position, EnergyOrbDiameter);
            float hitRadius = EnergyOrbDiameter * 0.4f;
            if (!hit && (playerBody.ClosestPoint(position) - position).sqrMagnitude < hitRadius * hitRadius)
            {
                hit = true;
                player.TakeDamage(energyDamage, position);
            }
        });
        Destroy(warning.gameObject);
        if (!hit && (playerBody.ClosestPoint(target) - target).sqrMagnitude <= EnergyImpactRadius * EnergyImpactRadius)
            player.TakeDamage(energyDamage, target);
        PlayBurst(target + Vector2.up * 0.65f, 1.15f);
        StartCoroutine(BreakGround(target));
        Vector2 direction = (contact - merge).normalized;
        float distance = (EnergyOrbDiameter + 0.1f) / -direction.y;
        StartCoroutine(BuryOrb(orb, contact, contact + direction * distance));
    }

    private IEnumerator BuryOrb(SpriteRenderer orb, Vector2 contact, Vector2 buried)
    {
        yield return Animate(GroundPenetrationSeconds, progress =>
        {
            float depth = progress * progress * (2f - progress);
            float opacity = Mathf.SmoothStep(1f, 0f, progress);
            DrawOrb(orb, Vector2.Lerp(contact, buried, depth), EnergyOrbDiameter, opacity);
        });
        Destroy(orb.gameObject);
    }

    private IEnumerator BreakGround(Vector2 target)
    {
        GameplaySfx.Play(GameplaySfx.Cue.Explosion, .59f);
        var impact = new GameObject("에너지볼 지면 파괴").transform;
        impact.SetParent(effects, false);
        SpriteRenderer pit = NewLight("착탄 자국", softLight);
        pit.transform.SetParent(impact);
        Place(pit, new Vector2(target.x, FloorY - 0.14f), new Vector2(10f, 1.55f), new Color(0.12f, 0.09f, 0.065f, 0.94f));
        var cracks = new List<SpriteRenderer>();
        for (int i = 0; i < 7; i++)
        {
            float direction = i % 2 == 0 ? -1f : 1f;
            Vector2 start = new Vector2(target.x + (i - 3) * 0.68f, FloorY - 0.12f);
            for (int j = 0; j < 3; j++)
            {
                Vector2 end = start + new Vector2(direction * Random.Range(0.3f, 0.9f), -Random.Range(0.15f, 0.37f));
                SpriteRenderer crack = NewLight("갈라진 바닥", solidLight);
                crack.transform.SetParent(impact);
                Vector2 segment = end - start;
                Place(crack, (start + end) * 0.5f, new Vector2(segment.magnitude, 0.09f - j * 0.02f),
                    new Color(0.15f, 0.10f, 0.075f, 0.96f));
                crack.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
                cracks.Add(crack);
                start = end;
            }
        }
        for (int burst = 0; burst < 3; burst++)
        {
            for (int i = 0; i < 5; i++)
            {
                SpriteRenderer chip = NewLight("튀는 바위 조각", rockFragment);
                chip.sortingOrder = 33;
                chip.transform.SetParent(impact);
                StartCoroutine(ThrowRock(chip, new Vector2(target.x + Random.Range(-2.6f, 2.6f), FloorY + 0.05f)));
            }
            yield return new WaitForSeconds(0.62f);
        }

        yield return new WaitForSeconds(1.55f);
        yield return Animate(0.55f, progress =>
        {
            pit.color = WithAlpha(pit.color, 0.94f * (1f - progress));
            foreach (SpriteRenderer crack in cracks) crack.color = WithAlpha(crack.color, 1f - progress);
        });
        Destroy(impact.gameObject);
    }

    private IEnumerator ThrowRock(SpriteRenderer chip, Vector2 origin)
    {
        float size = Random.Range(0.28f, 0.90f);
        Vector2 spriteSize = chip.sprite.bounds.size;
        Vector2 scale = Vector2.one * (size / Mathf.Max(spriteSize.x, spriteSize.y));
        Vector2 velocity = new Vector2(Random.Range(-7f, 7f), Random.Range(2.8f, 6.9f));
        float spin = Random.Range(-460f, 460f);
        float lifetime = velocity.y / 7f + 0.18f;
        yield return Animate(lifetime, phase =>
        {
            float time = lifetime * phase;
            Vector2 position = origin + velocity * time + Vector2.down * 7f * time * time;
            float fade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1f, phase));
            Place(chip, position, new Vector2(scale.x, scale.y * 0.75f), WithAlpha(Color.white, fade));
            chip.transform.rotation = Quaternion.Euler(0f, 0f, spin * time);
        });
        Destroy(chip.gameObject);
    }

    private int AvailableSummonCount()
    {
        summons.RemoveAll(monster => monster == null || monster.IsDead || !monster.gameObject.activeInHierarchy);
        return SummonLimit - summons.Count;
    }

    private IEnumerator Summon()
    {
        int count = Mathf.Min(3, AvailableSummonCount());
        if (count <= 0) yield break;
        Vector2 bay = Lens(1);
        SpriteRenderer light = NewLight("중앙 소환구", softLight);
        yield return Animate(0.55f, progress =>
            Place(light, bay, Vector2.one * Mathf.Lerp(1f, 5f, progress), new Color(0.58f, 1f, 0.69f)));
        for (int i = 0; i < count; i++)
        {
            MonsterHealth prefab = summonPrefabs[Random.Range(0, summonPrefabs.Length)];
            MonsterHealth monster = Instantiate(prefab, bay, Quaternion.identity, effects);
            summons.Add(monster);
            float offset = (i - (count - 1) * 0.5f) * 3f + Random.Range(-0.8f, 0.8f);
            StartCoroutine(DeploySummon(monster, bay, offset));
            yield return new WaitForSeconds(0.26f);
        }
        Destroy(light.gameObject);
        yield return new WaitForSeconds(0.65f);
    }

    private IEnumerator DeploySummon(MonsterHealth monster, Vector2 bay, float offset)
    {
        var chase = monster.GetComponent<NormalMonsterChase>();
        var flyingAttack = monster.GetComponent<FlyingMonsterAttack>();
        var body = monster.GetComponent<Rigidbody2D>();
        chase.enabled = false;
        if (flyingAttack != null) flyingAttack.enabled = false;
        Collider2D[] colliders = monster.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D collider in colliders) collider.enabled = false;
        body.simulated = false;
        float extent = monster.GetComponent<SpriteRenderer>().bounds.extents.y;
        Vector2 destination = new Vector2(bay.x + offset, FloorY + extent + (flyingAttack != null ? 3.1f : 0.18f));
        yield return Animate(0.65f, progress =>
            monster.transform.position = Vector2.Lerp(bay, destination, progress * progress));
        foreach (Collider2D collider in colliders) collider.enabled = true;
        body.simulated = true;
        chase.BeginEncounterApproach(player.transform, view);
        chase.enabled = true;
        if (flyingAttack != null)
        {
            flyingAttack.SetProjectileParent(effects);
            flyingAttack.enabled = true;
        }
    }

    internal bool IsPlayingDefeat { get; private set; }

    internal void FinishBattle()
    {
        if (IsPlayingDefeat) return;
        ClearCombat();
        IsPlayingDefeat = true;
        if (boardingTeleport == null || !boardingTeleport.isActiveAndEnabled || player.IsDead
            || playerMovement == null || !player.TryGetComponent(out boardingBody))
        {
            StartCoroutine(Withdraw());
            return;
        }

        StartCoroutine(BoardingSequence());
    }

    internal void CancelBoardingSequence()
    {
        if (boarding) gameObject.SetActive(false);
    }

    private IEnumerator BoardingSequence()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Gate, .59f);
        Vector2 centerOffset = PlayerCenter - (Vector2)player.transform.position;
        Vector3 floorPosition = player.transform.position;
        Vector2 hatch = Lens(1);
        LockBoardingControl();

        effects = new GameObject("비행선 승선 연출").transform;
        effects.SetParent(transform.parent, false);
        SpriteRenderer opening = NewLight("중앙 해치 개구부", hatchOpeningSprite);
        SpriteRenderer left = NewLight("중앙 해치 왼쪽 덮개", hatchLeftSprite);
        SpriteRenderer right = NewLight("중앙 해치 오른쪽 덮개", hatchRightSprite);
        opening.sortingOrder = hull.sortingOrder + 3;
        left.sortingOrder = right.sortingOrder = hull.sortingOrder + 4;

        Vector2 sourceSize = hull.sprite.rect.size / hull.sprite.pixelsPerUnit;
        Vector2 hatchSize = transform.TransformVector(new Vector2(
            sourceSize.x * 210f / 2172f, sourceSize.y * 54f / 724f));
        Vector2 coverSize = new Vector2(hatchSize.x * 0.5f, hatchSize.y);
        Place(opening, hatch, SizeMultiplier(opening.sprite, hatchSize), Color.white);
        yield return Animate(HatchOpeningSeconds, progress =>
        {
            float open = Mathf.SmoothStep(0f, 1f, progress);
            float displacement = hatchSize.x * Mathf.Lerp(0.25f, 0.51f, open);
            Vector2 raised = hatch + Vector2.up * (hatchSize.y * 0.4f * open);
            Place(left, raised + Vector2.left * displacement, SizeMultiplier(left.sprite, coverSize), Color.white);
            Place(right, raised + Vector2.right * displacement, SizeMultiplier(right.sprite, coverSize), Color.white);
            left.transform.rotation = Quaternion.Euler(0f, 0f, -18f * open);
            right.transform.rotation = Quaternion.Euler(0f, 0f, 18f * open);
        });

        var streaks = new SpriteRenderer[28];
        for (int i = 0; i < streaks.Length; i++)
            streaks[i] = NewLight("해치로 빨려 들어가는 바람", solidLight);

        Vector3 hatchPosition = new Vector3(hatch.x - centerOffset.x, hatch.y - centerOffset.y + 0.3f, floorPosition.z);
        Vector3 liftPosition = new Vector3(hatchPosition.x,
            Mathf.Min(hatchPosition.y - 1f, floorPosition.y + 0.65f), floorPosition.z);
        boardingHitPresentation = true;
        GameplaySfx.Play(GameplaySfx.Cue.Wind, .56f);
        player.PlayHitPresentation(float.PositiveInfinity);
        yield return Animate(BoardingPullSeconds, progress =>
        {
            DrawWindStreaks(streaks, hatch, progress * BoardingPullSeconds * 2.1f);
            Vector3 position = progress < 0.48f
                ? Vector3.Lerp(floorPosition, liftPosition, Mathf.SmoothStep(0f, 1f, progress / 0.48f))
                : Vector3.Lerp(liftPosition, hatchPosition, Mathf.SmoothStep(0f, 1f, (progress - 0.48f) / 0.52f));
            player.transform.position = position;
        });

        Coroutine transfer = boardingTeleport.StartPlayerTeleport(ClearBoardingHitPresentation);
        if (transfer != null) yield return transfer;
        gameObject.SetActive(false);
    }

    private void LockBoardingControl()
    {
        player.TryGetComponent(out boardingGun);
        restoreBodySimulation = boardingBody.simulated;
        restoreMovement = playerMovement.enabled;
        previousGunInputSuppression = boardingGun != null && boardingGun.InputSuppressed;
        boarding = true;
        player.Defeated.AddListener(CancelBoardingSequence);
        if (boardingGun != null) boardingGun.InputSuppressed = true;
        playerMovement.enabled = false;
        playerMovement.ShowIdlePresentation();
        boardingBody.simulated = false;
    }

    private void ReleaseBoardingControl()
    {
        if (!boarding) return;
        boarding = false;
        ClearBoardingHitPresentation();
        if (player != null) player.Defeated.RemoveListener(CancelBoardingSequence);
        if (boardingBody != null) boardingBody.simulated = restoreBodySimulation;
        if (playerMovement != null) playerMovement.enabled = restoreMovement;
        if (boardingGun != null) boardingGun.InputSuppressed = previousGunInputSuppression;
    }

    private void ClearBoardingHitPresentation()
    {
        if (!boardingHitPresentation) return;
        boardingHitPresentation = false;
        if (player == null || player.IsDead) return;
        player.ClearHitPresentation();
        if (playerMovement != null) playerMovement.ShowIdlePresentation();
    }

    private static Vector2 SizeMultiplier(Sprite sprite, Vector2 size) =>
        new Vector2(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y);

    private IEnumerator Withdraw()
    {
        Vector3 start = transform.position;
        yield return Animate(1.2f, progress =>
        {
            transform.position = Vector3.Lerp(start, flightOrigin, progress * progress);
        });
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        IsPlayingDefeat = false;
        ClearCombat();
    }

    private void ClearCombat()
    {
        StopAllCoroutines();
        ReleaseBoardingControl();
        if (health != null) health.HpChanged.RemoveListener(CheckPhase);
        if (playerMovement != null) playerMovement.SetWindPull(0f, 0f);
        SetHitboxes(false);
        if (effects != null)
        {
            effects.gameObject.SetActive(false);
            Destroy(effects.gameObject);
            effects = null;
        }
        summons.Clear();
    }

    private void OnDestroy()
    {
        if (softLight != null) Destroy(softLight);
        if (solidLight != null) Destroy(solidLight);
        if (hatchOpeningSprite != null) Destroy(hatchOpeningSprite);
        if (hatchLeftSprite != null) Destroy(hatchLeftSprite);
        if (hatchRightSprite != null) Destroy(hatchRightSprite);
        if (rockFragment != null) Destroy(rockFragment);
        if (energyMaterial != null) Destroy(energyMaterial);
        if (effectTexture != null) Destroy(effectTexture);
        foreach (Sprite frame in orbFrames) if (frame != null) Destroy(frame);
        foreach (Sprite frame in laserFrames) if (frame != null) Destroy(frame);
        if (laserMountSprite != null) Destroy(laserMountSprite);
        if (attackTexture != null) Destroy(attackTexture);
    }

    private void SetHitboxes(bool value)
    {
        foreach (Collider2D hitbox in hitboxes) hitbox.enabled = value;
    }

    private Vector2 Lens(int index)
    {
        Sprite sprite = hull.sprite;
        Vector2 local = (Vector2.Scale(sprite.rect.size, Lenses[index]) - sprite.pivot) / sprite.pixelsPerUnit;
        return transform.TransformPoint(local);
    }

    private Sprite CreateHatchCover(int leftPixel)
    {
        Sprite source = hull.sprite;
        Rect rect = source.rect;
        // Split the original central emitter around its seam so the painted housing becomes the two covers.
        Rect cover = new Rect(rect.x + rect.width * leftPixel / 2172f,
            rect.y + rect.height * (1f - 515f / 724f), rect.width * 105f / 2172f, rect.height * 54f / 724f);
        return Sprite.Create(source.texture, cover, Vector2.one * 0.5f, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    private void CreateLightSprites()
    {
        const int size = 64;
        const int hatchStart = size + 24;
        const int textureWidth = hatchStart + HatchTextureWidth;
        effectTexture = new Texture2D(textureWidth, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, name = "정상 보스 에너지" };
        var pixels = new Color32[textureWidth * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < textureWidth; x++)
        {
            float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f) * 2f / size;
            float alpha = x >= size + 1 && x <= size + 3 ? 1f : Mathf.Pow(Mathf.Clamp01(1f - radius), 0.7f);
            pixels[y * textureWidth + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            bool inside = x + y >= 6 && y * 2 - x <= 20 && x * 2 + y <= 33 && x - y <= 9;
            Color32 stone = y > x * 0.4f + 5f ? new Color32(153, 144, 118, 255) : new Color32(88, 81, 69, 255);
            if (!inside) stone.a = 0;
            pixels[y * textureWidth + size + 5 + x] = stone;
        }
        for (int y = 0; y < HatchTextureHeight; y++)
        for (int x = 0; x < HatchTextureWidth; x++)
        {
            Vector2 point = new Vector2((x + 0.5f) * 2f / HatchTextureWidth - 1f,
                (y + 0.5f) * 2f / HatchTextureHeight - 1f);
            float radius = point.magnitude;
            float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.91f, radius));
            Color recess = Color.Lerp(new Color(0.014f, 0.019f, 0.024f), new Color(0.29f, 0.22f, 0.14f), rim);
            pixels[y * textureWidth + hatchStart + x] = WithAlpha(recess, 1f - Mathf.InverseLerp(0.94f, 1f, radius));
        }
        effectTexture.SetPixels32(pixels);
        effectTexture.Apply(false, true);
        softLight = Sprite.Create(effectTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
        solidLight = Sprite.Create(effectTexture, new Rect(size + 2, 1, 1, 1), Vector2.one * 0.5f, 1, 0, SpriteMeshType.FullRect);
        rockFragment = Sprite.Create(effectTexture, new Rect(size + 5, 0, 16, 16), Vector2.one * 0.5f, 16, 0, SpriteMeshType.FullRect);
        hatchOpeningSprite = Sprite.Create(effectTexture, new Rect(hatchStart, 0, HatchTextureWidth, HatchTextureHeight),
            Vector2.one * 0.5f, HatchTextureWidth, 0, SpriteMeshType.FullRect);
    }

    private SpriteRenderer NewLight(string objectName, Sprite sprite)
    {
        var light = new GameObject(objectName).AddComponent<SpriteRenderer>();
        light.transform.SetParent(effects, false);
        light.sprite = sprite;
        light.sharedMaterial = effectMaterial;
        light.sortingOrder = 30;
        return light;
    }

    private void CreateAttackSprites()
    {
        const int orbSize = 160;
        const int beamWidth = 96;
        const int beamHeight = 256;
        const int mountSize = 40;
        const int atlasWidth = orbSize + beamWidth + mountSize;
        attackTexture = new Texture2D(atlasWidth, beamHeight * orbFrames.Length, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "산 정상 보스 레이저와 에너지볼",
        };
        var pixels = new Color32[attackTexture.width * attackTexture.height];
        for (int frame = 0; frame < orbFrames.Length; frame++)
        {
            int row = frame * beamHeight;
            for (int y = 0; y < orbSize; y++)
            for (int x = 0; x < orbSize; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f) * (2f / orbSize) - Vector2.one;
                pixels[(row + y) * atlasWidth + x] = OrbPixel(point, frame);
            }
            for (int y = 0; y < beamHeight; y++)
            for (int x = 0; x < beamWidth; x++)
                pixels[(row + y) * atlasWidth + orbSize + x] = LaserPixel(x, y, beamWidth, beamHeight, frame);
        }
        for (int y = 0; y < mountSize; y++)
        for (int x = 0; x < mountSize; x++)
            pixels[y * atlasWidth + orbSize + beamWidth + x] = LaserMountPixel(x, y);
        attackTexture.SetPixels32(pixels);
        attackTexture.Apply(false, true);
        laserMountSprite = Sprite.Create(attackTexture, new Rect(orbSize + beamWidth, 0, mountSize, mountSize),
            Vector2.one * 0.5f, mountSize, 0, SpriteMeshType.FullRect);
        for (int i = 0; i < orbFrames.Length; i++)
        {
            orbFrames[i] = Sprite.Create(attackTexture, new Rect(0, i * beamHeight, orbSize, orbSize),
                Vector2.one * 0.5f, orbSize, 0, SpriteMeshType.FullRect);
            laserFrames[i] = Sprite.Create(attackTexture, new Rect(orbSize, i * beamHeight, beamWidth, beamHeight),
                Vector2.one * 0.5f, beamHeight, 0, SpriteMeshType.FullRect);
        }
    }

    private static Color32 LaserMountPixel(int x, int y)
    {
        float center = Mathf.Abs(x - 19.5f);
        float outline = y < 10 ? 12f + y * 0.33f : y > 34 ? 16f : 18f;
        if (center > outline || y < 4 || y > 37) return new Color32(0, 0, 0, 0);
        if (center > outline - 1.4f || y < 6) return new Color32(23, 24, 27, 255);
        float lens = new Vector2((x - 19.5f) / 11f, (y - 11f) / 6.1f).sqrMagnitude;
        if (lens < 0.65f) return new Color32(166, 58, 27, 255);
        if (lens < 1f) return new Color32(35, 29, 28, 255);
        if (y is 18 or 19 or 33) return new Color32(131, 108, 76, 255);
        int wear = (x * 19 + y * 31 + x * y) % 22;
        if (center > 11f) return new Color32((byte)(69 + wear), (byte)(62 + wear), (byte)(50 + wear), 255);
        return new Color32((byte)(47 + wear), (byte)(51 + wear), (byte)(57 + wear), 255);
    }

    private static Color32 OrbPixel(Vector2 point, int frame)
    {
        float radius = point.magnitude;
        float theta = Mathf.Atan2(point.y, point.x);
        float phase = frame * Mathf.PI * 0.5f;
        float edge = 0.86f + 0.025f * Mathf.Sin(theta * 5f + phase) + 0.018f * Mathf.Sin(theta * 13f - phase);
        float halo = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 0.04f, 0.99f, radius));
        float shell = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 0.045f, edge + 0.01f, radius));
        float core = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.77f, (point - new Vector2(-0.10f, 0.13f)).magnitude));
        float cloud = Mathf.PerlinNoise(point.x * 4.7f + 10f, point.y * 3.3f + 4f) * 0.64f
            + Mathf.PerlinNoise(point.x * 12f + 5f, point.y * 12f + 8f) * 0.36f;
        float rim = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(radius - edge + 0.075f) / 0.10f);
        Color color = Color.Lerp(new Color(0.12f, 0.29f, 0.31f), new Color(0.48f, 0.73f, 0.64f),
            Mathf.Clamp01(core * 0.65f + cloud * 0.42f + rim * 0.25f));
        color = Color.Lerp(color, new Color(1f, 0.95f, 0.76f), core * core * 0.97f);

        // Branches follow an irregular radius so the light reads as plasma inside the shell.
        float arcRadius = 0.66f + 0.065f * Mathf.Sin(theta * 3f + phase)
            + 0.034f * Mathf.Sin(theta * 17f - phase) + 0.015f * Mathf.Sin(theta * 37f + phase);
        float arc = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(radius - arcRadius) / 0.043f);
        float branch = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(radius - arcRadius + 0.12f * Mathf.Sin(theta * 6f)) / 0.022f);
        float litSegments = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.4f, 0.85f, Mathf.Sin(theta * 4f - phase)));
        color = Color.Lerp(color, new Color(0.87f, 0.98f, 0.84f), Mathf.Max(arc * 0.85f, branch * 0.52f) * litSegments);
        return WithAlpha(color, Mathf.Max(shell, halo * 0.38f));
    }

    private static Color32 LaserPixel(int x, int y, int width, int height, int frame)
    {
        float u = (x + 0.5f) * 2f / width - 1f;
        float v = y / (height - 1f);
        float phase = frame * Mathf.PI * 0.5f;
        float taper = Mathf.SmoothStep(1f, 0.29f, Mathf.InverseLerp(0.87f, 1f, v));
        float drift = Mathf.Sin(v * 32f + phase) * 0.024f;
        float crossBeam = u / taper - drift;
        float distance = Mathf.Abs(crossBeam);
        float envelope = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.995f, distance));
        float turbulence = Mathf.PerlinNoise(u * 8f + 5f, v * 13f + frame * 0.37f);
        float inner = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.64f, distance));
        Color color = Color.Lerp(new Color(0.54f, 0.14f, 0.08f), new Color(1f, 0.54f, 0.25f), envelope * 0.78f);
        color = Color.Lerp(color, new Color(1f, 0.97f, 0.81f), inner);

        float filament = 0.42f + 0.075f * Mathf.Sin(v * 54f - phase) + 0.035f * Mathf.Sin(v * 123f + phase);
        float thread = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(distance - filament) / 0.065f);
        float broken = Mathf.InverseLerp(0.35f, 0.68f, turbulence);
        color = Color.Lerp(color, new Color(1f, 0.79f, 0.46f), thread * broken * 0.88f);
        return WithAlpha(color, envelope * Mathf.Lerp(0.76f + turbulence * 0.22f, 1f, inner));
    }

    private static void Place(SpriteRenderer light, Vector2 position, Vector2 size, Color color)
    {
        light.transform.position = position;
        light.transform.localScale = new Vector3(size.x, size.y, 1f);
        light.color = color;
    }

    private static int AttackFrame() => Mathf.FloorToInt(Time.time * 13f) % 4;

    private static float WarningPulse(float seconds, float frequency) =>
        0.5f + 0.5f * Mathf.Cos(seconds * frequency * Mathf.PI * 2f);

    private void DrawOrb(SpriteRenderer orb, Vector2 position, float diameter, float opacity = 1f)
    {
        orb.sprite = orbFrames[AttackFrame()];
        Place(orb, position, Vector2.one * diameter, WithAlpha(Color.white, opacity));
    }

    private void PlayBurst(Vector2 position, float scale, string effectName = null)
    {
        GameObject burst = Instantiate(burstPrefab, position, Quaternion.identity, effects);
        if (effectName != null) burst.name = effectName;
        burst.transform.localScale *= scale;
        Destroy(burst, 1.5f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static IEnumerator Animate(float seconds, System.Action<float> draw)
    {
        for (float time = 0f; time < seconds; time += Time.deltaTime)
        {
            draw(time / seconds);
            yield return null;
        }
        draw(1f);
    }
}
