using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth), typeof(SpriteRenderer), typeof(BoxCollider2D))]
public sealed partial class GongOilBoss : MonoBehaviour
{
    private enum Pattern
    {
        SmokeCharge, ScytheThrow, ElectricCross, Summon, SideMissiles,
        EdgeSlashes, FloorLightning, SmokeMissiles
    }

    [System.Serializable]
    private struct SwingFrame
    {
        public Sprite sprite;
        public Vector2 bladeBase;
        public Vector2 bladeTip;
    }

    [System.Serializable]
    private struct HandFrame
    {
        public Sprite sprite;
        public Vector2 hand;
    }

    [System.Serializable]
    private struct SummonMonster
    {
        public MonsterHealth prefab;
        public MonsterSO healthSource;
    }

    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueData introduction;
    [SerializeField] private ChapterMusic chapterMusic;
    [SerializeField] private ArisaHealth player;
    [SerializeField] private BoxCollider2D arenaFloor;
    [SerializeField] private SpriteRenderer arenaBackground;
    [SerializeField] private Vector2 electricTeleportPosition;
    [SerializeField] private UIBarController bossBar;
    [SerializeField] private FinalSkyBattle finalSkyBattle;
    [SerializeField] private SwingFrame[] chargeFrames;
    [SerializeField] private HandFrame[] throwFrames;
    [SerializeField] private Sprite[] electricFrames;
    [SerializeField] private HandFrame[] summonFrames;
    [SerializeField] private Sprite scytheSprite;
    [SerializeField] private Sprite smokeSprite;
    [SerializeField] private Sprite missileSprite;
    [SerializeField] private SummonMonster[] summonMonsters;
    [SerializeField] private MonsterSO summonDrops;

    private const float ElectricWindupDuration = 3f;
    private const float ElectricWindupTurn = 1.5f;
    private const float ElectricDuration = 8f;
    private const float ElectricRotationSpeed = 20f;
    private const int TeleportCount = 5;
    private const float SummonStopDuration = 3f;
    private const int MissileWaves = 6;
    private const float MissileWaveInterval = 0.8f;
    private const float MissileWarningDuration = 0.35f;
    private const float MissileFlightDuration = 3.15f;
    private const float MissileRecoveryDuration = 0.35f;
    private const float MissileSmokeDuration = 2f;
    private const float MissileLaneSmokeDuration = 2.5f;
    private const float MissileMinHeight = 0.75f;
    private const float MissileMaxHeight = 3.1f;
    private const float SlashVolleyDuration = 6f;
    private const int AttackDamage = 1;

    private static readonly Color SmokeColor = new Color(0.04f, 0.035f, 0.045f);
    private static readonly Color PortalColor = new Color(0.85f, 0.42f, 0.22f);
    private static readonly Color ElectricColor = new Color(0.25f, 0.82f, 1f);
    private static readonly Color BoostColor = new Color(0.32f, 1f, 0.44f);
    private static readonly Color RiftColor = new Color(0.63f, 0.25f, 1f);
    private static readonly Color SlashColor = new Color(1f, 0.45f, 0.18f);
    private MonsterHealth health;
    private SpriteRenderer body;
    private Sprite idleSprite;
    private BoxCollider2D hitbox;
    private Vector2 standingHitboxOffset;
    private Vector2 standingHitboxSize;
    private Collider2D playerBody;
    private Material lineMaterial;
    private Material missileLaneSmokeMaterial;
    private Transform effects;
    private Vector3 startingPosition;
    private Rect arena;
    private bool running;
    private bool defeated;
    private float nextHitTime;

    private Vector2 PlayerCenter => playerBody != null ? playerBody.bounds.center : player.transform.position;
    private Vector2 BossCenter => (Vector2)transform.position + Vector2.up * 2f;
    private Vector2 AtFloor(float x) => new Vector2(x, arena.yMin);

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
        body = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
        standingHitboxOffset = hitbox.offset;
        standingHitboxSize = hitbox.size;
        idleSprite = body.sprite;
        startingPosition = transform.position;
        hitbox.enabled = false;
        health.Defeated.AddListener(OnDefeated);
    }

    // Called after the boarding teleport fade, including the F1 test route.
    public void BeginEncounter()
    {
        if (!isActiveAndEnabled || running || defeated || player.IsDead) return;

        Bounds floor = arenaFloor.bounds;
        arena = Rect.MinMaxRect(floor.min.x, floor.max.y, floor.max.x, arenaBackground.bounds.max.y);
        playerBody = player.GetComponent<Collider2D>();
        nextHitTime = 0f;
        running = true;
        player.Defeated.AddListener(OnPlayerDefeated);

        effects = new GameObject("공오일 전투 오브젝트").transform;
        effects.SetParent(transform.parent, false);
        lineMaterial ??= new Material(Shader.Find("Sprites/Default"));
        StartCoroutine(Encounter());
    }

    private IEnumerator Encounter()
    {
        // Wait out any dialogue already active before opening our own.
        while (DialogueManager.ActiveDialogueCount > 0) yield return null;
        dialogueManager.PlayDialogue(introduction);
        while (dialogueManager.IsPlaying(introduction)) yield return null;

        // The key that advances the last line also controls the player.
        yield return null;
        if (chapterMusic != null) chapterMusic.PlayBoss(3);
        if (bossBar != null) bossBar.ShowEncounter(health);
        var remaining = new List<Pattern>();
        Pattern? previous = null;
        int patternCount = 0;
        while (running)
        {
            Pattern selected = PickPattern(remaining, previous, ref patternCount);
            yield return PlayPattern(selected);
            previous = selected;
            yield return Rest();
        }
    }

    private Pattern PickPattern(List<Pattern> remaining, Pattern? previous, ref int patternCount)
    {
        Pattern last = (long)health.CurrentHp * 2 <= health.MaxHp ? Pattern.SmokeMissiles : Pattern.SideMissiles;
        int available = (int)last + 1;
        int excluded = 0;
        if (available != patternCount || remaining.Count == 0)
        {
            remaining.Clear();
            for (int i = 0; i < available; i++) remaining.Add((Pattern)i);
            if (previous.HasValue && remaining.Remove(previous.Value))
            {
                remaining.Add(previous.Value);
                excluded = 1;
            }
            patternCount = available;
        }

        int index = Random.Range(0, remaining.Count - excluded);
        Pattern pattern = remaining[index];
        remaining.RemoveAt(index);
        return pattern;
    }

    private IEnumerator PlayPattern(Pattern pattern) => pattern switch
    {
        Pattern.SmokeCharge => SmokeCharge(),
        Pattern.ScytheThrow => ThrowScythe(),
        Pattern.ElectricCross => RotateElectricCross(),
        Pattern.Summon => SummonMonsters(),
        Pattern.SideMissiles => MissileBarrage(fromSmokeZone: false),
        Pattern.EdgeSlashes => EdgeSlashes(),
        Pattern.FloorLightning => FloorLightning(),
        Pattern.SmokeMissiles => MissileBarrage(fromSmokeZone: true),
        _ => throw new System.ArgumentOutOfRangeException(nameof(pattern), pattern, null)
    };

    private void Update()
    {
        if (running && !GameplayPause.IsPaused
            && (PlayerCenter.x < arena.xMin - 2f || PlayerCenter.x > arena.xMax + 2f
                || PlayerCenter.y < arena.yMin - 3f || PlayerCenter.y > arena.yMax + 3f))
        {
            StopEncounter();
        }
    }

    private IEnumerator Rest()
    {
        yield return Warp(AtFloor(Mathf.Clamp(PlayerCenter.x + 6f, arena.xMin + 2f, arena.xMax - 2f)));
        yield return new WaitForSeconds(0.9f);
    }

    private IEnumerator SmokeCharge()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Dash, .55f);
        float direction = Random.value < 0.5f ? 1f : -1f;
        float entry = direction > 0f ? arena.xMin : arena.xMax;
        float exit = direction > 0f ? arena.xMax : arena.xMin;
        yield return Warp(AtFloor(entry + direction * 2f), visible: false);

        // A front of smoke crosses the room before the boss appears.
        Transform smoke = new GameObject("검은 연기").transform;
        smoke.SetParent(effects, false);
        const int cloudCount = 48;
        var clouds = new SpriteRenderer[cloudCount];
        var cloudDrift = new Vector2[cloudCount];
        for (int i = 0; i < cloudCount; i++)
        {
            clouds[i] = MakeSprite("연기", smokeSprite, 13, smoke);
            clouds[i].transform.localScale = Vector3.one * Random.Range(3.8f, 7f) / smokeSprite.bounds.size.x;
            cloudDrift[i] = new Vector2(Random.Range(0f, 12f), Random.Range(0.3f, 5.2f));
        }
        yield return Animate(2.15f, progress =>
        {
            float front = Mathf.Lerp(entry - direction * 4f, exit + direction * 13f, progress);
            for (int i = 0; i < cloudCount; i++)
            {
                SpriteRenderer cloud = clouds[i];
                cloud.transform.position = new Vector2(front - direction * cloudDrift[i].x,
                    arena.yMin + cloudDrift[i].y + progress * 1.2f);
                Color tint = SmokeColor;
                tint.a = 0.65f * Mathf.SmoothStep(0f, 1f, progress * 12f)
                    * (1f - Mathf.InverseLerp(0.83f, 1f, progress));
                cloud.color = tint;
            }
        });
        Destroy(smoke.gameObject);

        Vector2 start = AtFloor(entry + direction * 2f);
        Vector2 end = AtFloor(exit - direction * 2f);
        yield return Warp(start, faceRight: direction > 0f);
        SpriteRenderer exhaust = MakeSprite("초록 부스트 불빛", smokeSprite, 11, effects);
        LineRenderer[] speedStreaks = {
            MakeLine("돌진 속도선", 11, 0.07f, 2),
            MakeLine("돌진 속도선", 11, 0.12f, 2),
            MakeLine("돌진 속도선", 11, 0.06f, 2)
        };
        const float swingTime = 1.22f;
        const float chargeTime = 3.45f;
        float nextEcho = 0f;
        Vector2 previousBladeTip = Vector2.zero;
        bool wasSwinging = false;
        for (float time = 0f; time < chargeTime; time += Time.deltaTime)
        {
            float phase = Mathf.Repeat(time, swingTime) / swingTime;
            float progress = time / chargeTime;
            float lift = Mathf.SmoothStep(0f, 1f, progress * 12f)
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, progress)));
            transform.position = Vector2.Lerp(start, end, progress) + Vector2.up * (0.62f * lift);
            float boost = lift * (0.84f + 0.16f * Mathf.Sin(time * 46f));
            exhaust.transform.position = (Vector2)transform.position + new Vector2(-2.2f * direction, 1.2f);
            exhaust.transform.localScale = new Vector3(5.4f + boost * 2.2f, 1.65f, 1f) / smokeSprite.bounds.size.x;
            exhaust.color = WithAlpha(BoostColor, boost * 0.58f);
            SwingFrame frame = chargeFrames[FrameIndex(phase, chargeFrames.Length)];
            body.sprite = frame.sprite;
            hitbox.offset = new Vector2(0f, 1.45f);
            hitbox.size = new Vector2(2.85f, 2.7f);
            Vector2 bladeBase = PosePoint(frame.bladeBase);
            Vector2 bladeTip = PosePoint(frame.bladeTip);
            bool swinging = (phase >= 0.26f && phase <= 0.49f) || (phase >= 0.59f && phase <= 0.85f);
            if (swinging)
            {
                HitSegment(bladeBase, bladeTip, 0.48f);
                if (wasSwinging) HitSegment(previousBladeTip, bladeTip, 0.48f);
            }
            previousBladeTip = bladeTip;
            wasSwinging = swinging;
            for (int i = 0; i < speedStreaks.Length; i++)
            {
                LineRenderer streak = speedStreaks[i];
                Vector2 tail = (Vector2)transform.position + new Vector2(-1.1f * direction, 0.55f + 0.82f * i);
                streak.SetPosition(0, tail - Vector2.right * (4.2f + i * 0.82f + boost) * direction);
                streak.SetPosition(1, tail);
                streak.startColor = Color.clear;
                streak.endColor = WithAlpha(BoostColor, (swinging ? 0.82f : 0.62f) * boost);
            }
            if (time >= nextEcho)
            {
                nextEcho = time + 0.075f;
                StartCoroutine(FadeEcho(body, transform.position, 0.34f, 11, WithAlpha(BoostColor, 0.65f * lift), -direction));
            }
            yield return null;
        }

        transform.position = end;
        Destroy(exhaust.gameObject);
        foreach (LineRenderer streak in speedStreaks) Destroy(streak.gameObject);
        ResetPose();
    }

    private Vector2 PosePoint(Vector2 leftFacingOffset)
    {
        if (body.flipX) leftFacingOffset.x = -leftFacingOffset.x;
        return (Vector2)transform.position + leftFacingOffset;
    }

    private static int FrameIndex(float progress, int count) =>
        Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(progress) * count), count - 1);

    private void ResetPose()
    {
        body.sprite = idleSprite;
        body.color = Color.white;
        hitbox.offset = standingHitboxOffset;
        hitbox.size = standingHitboxSize;
    }

    private IEnumerator FadeEcho(SpriteRenderer source, Vector3 position, float duration, int sortingOrder, Color tint,
        float drift)
    {
        SpriteRenderer echo = MakeSprite("공오일 잔상", source.sprite, sortingOrder, effects);
        echo.flipX = source.flipX;
        echo.transform.rotation = source.transform.rotation;
        echo.transform.localScale = source.transform.localScale;
        yield return Animate(duration, progress =>
        {
            echo.transform.position = position + Vector3.right * (progress * drift);
            echo.color = WithAlpha(tint, tint.a * (1f - progress) * (1f - progress));
        });
        Destroy(echo.gameObject);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private IEnumerator ThrowScythe()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Throw, .55f);
        Vector2 playerPosition = player.transform.position;
        float orbitY = PlayerCenter.y;
        const float minDistance = 5f;
        const float maxDistance = 10f;
        const float wallClearance = 1.6f;
        float leftSpace = playerPosition.x - (arena.xMin + wallClearance);
        float rightSpace = arena.xMax - wallClearance - playerPosition.x;
        bool fromLeft = leftSpace >= minDistance && (rightSpace < minDistance || Random.value < 0.5f);
        float distance = Random.Range(minDistance, Mathf.Min(maxDistance, fromLeft ? leftSpace : rightSpace));
        Vector2 feet = playerPosition + Vector2.right * (fromLeft ? -distance : distance);
        yield return Warp(feet, faceRight: fromLeft);
        float signedRadius = (feet.x - playerPosition.x) * 0.5f;
        float radius = Mathf.Abs(signedRadius);
        Vector2 orbitCenter = new Vector2((feet.x + playerPosition.x) * 0.5f, orbitY);
        Vector2 orbitStart = new Vector2(feet.x, orbitY);

        SpriteRenderer scythe = MakeSprite("공오일의 투척 낫", scytheSprite, body.sortingOrder - 1, effects);
        scythe.transform.localScale = Vector3.one * 0.8f;
        scythe.flipX = body.flipX;
        const int followThroughFrames = 3;
        int releaseIndex = throwFrames.Length - followThroughFrames - 1;
        yield return Animate(0.36f, progress =>
        {
            float windup = Mathf.SmoothStep(0f, 1f, progress);
            HandFrame frame = throwFrames[FrameIndex(progress, releaseIndex)];
            body.sprite = frame.sprite;
            scythe.transform.SetPositionAndRotation(PosePoint(frame.hand),
                Quaternion.Euler(0f, 0f, Mathf.Lerp(5f, 77f, windup) * (body.flipX ? -1f : 1f)));
        });

        float turn = body.flipX ? -1f : 1f;
        HandFrame releaseFrame = throwFrames[releaseIndex];
        body.sprite = releaseFrame.sprite;
        Vector2 releaseHand = PosePoint(releaseFrame.hand);
        scythe.sortingOrder = 15;
        yield return Animate(0.10f, progress =>
        {
            scythe.transform.position = Vector2.Lerp(releaseHand, orbitStart, progress);
        });
        StartCoroutine(FadeEcho(scythe, orbitStart, 0.18f, 13, new Color(1f, 0.62f, 0.31f, 0.65f), 0f));
        TrailRenderer trail = scythe.gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = lineMaterial;
        trail.sortingOrder = 14;
        const float orbitDuration = 1.55f;
        trail.time = orbitDuration;
        trail.minVertexDistance = 0.07f;
        trail.startWidth = 0.56f;
        trail.endWidth = 0f;
        trail.startColor = new Color(1f, 0.64f, 0.27f, 0.95f);
        trail.endColor = new Color(0.78f, 0.26f, 0.9f, 0f);

        Vector2 previous = orbitStart;
        bool hit = false;
        yield return Animate(orbitDuration, progress =>
        {
            float followThrough = Mathf.InverseLerp(0.025f, 0.26f, progress);
            int frame = followThrough >= 1f
                ? 0
                : releaseIndex + FrameIndex(followThrough, followThroughFrames + 1);
            body.sprite = throwFrames[frame].sprite;
            float angle = progress * (2f * Mathf.PI);
            Vector2 position = orbitCenter + new Vector2(signedRadius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
            scythe.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, progress * 1440f * turn));
            if (!hit) hit = HitSegment(previous, position, 0.84f);
            previous = position;
        });
        scythe.enabled = false;
        trail.emitting = false;
        Destroy(scythe.gameObject, 0.22f);
        ResetPose();
    }

    private IEnumerator RotateElectricCross()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Electric, .55f);
        Vector2 feet = transform.parent.TransformPoint(electricTeleportPosition);
        yield return Warp(feet);
        Vector2 center = BossCenter;
        body.sprite = electricFrames[0];
        var sparks = new LineRenderer[4];
        var cores = new LineRenderer[4];
        for (int i = 0; i < cores.Length; i++)
        {
            sparks[i] = MakeLine("전깃줄 외곽", 14, 0.3f, 19);
            cores[i] = MakeLine("전깃줄 중심", 15, 0.09f, 19);
        }

        float direction = Random.value < 0.5f ? 1f : -1f;
        float attackStartAngle = ElectricWindupTurn * direction;
        yield return Animate(ElectricWindupDuration, progress =>
        {
            float turn = Mathf.InverseLerp(0.85f, 1f, progress);
            float warningAngle = Mathf.SmoothStep(0f, attackStartAngle, turn);
            float rise = Mathf.InverseLerp(0.24f, 0.98f, progress);
            body.sprite = electricFrames[FrameIndex(rise, electricFrames.Length - 2)];
            RenderElectricCross(center, warningAngle, sparks, cores, Mathf.Lerp(0.08f, 0.41f, progress), damaging: false);
        });
        SpriteRenderer corona = MakeSprite("전기 방출 중심광", smokeSprite, 15, effects);
        LineRenderer ring = MakeLine("전기 방출 충격파", 15, 0.13f, 33);
        yield return Animate(ElectricDuration, progress =>
        {
            float time = progress * ElectricDuration;
            float rotation = attackStartAngle + time * ElectricRotationSpeed * direction;
            int recoilFrame = Mathf.Repeat(time * 3.1f, 1f) < 0.32f ? 0 : 1;
            body.sprite = electricFrames[electricFrames.Length - 2 + recoilFrame];
            RenderElectricSource(center, rotation, time, 1f, corona, ring);
            RenderElectricCross(center, rotation, sparks, cores, 1f, damaging: true);
        });

        Destroy(corona.gameObject);
        Destroy(ring.gameObject);
        for (int i = 0; i < cores.Length; i++)
        {
            Destroy(sparks[i].gameObject);
            Destroy(cores[i].gameObject);
        }
        ResetPose();
    }

    private void RenderElectricSource(Vector2 center, float angle, float time, float strength, SpriteRenderer corona,
        LineRenderer ring)
    {
        float pulse = Mathf.Repeat(time * 3.1f, 1f);
        float jitter = Mathf.Sin(time * 43f);
        transform.position = center + new Vector2(jitter * 0.055f, -2f + Mathf.Sin(time * 5f) * 0.15f);
        body.color = Color.Lerp(Color.white, new Color(0.69f, 0.91f, 1f), strength * (1f - pulse) * 0.75f);
        corona.transform.position = center;
        corona.transform.localScale = Vector3.one * ((1.6f + strength * (1f - pulse)) / smokeSprite.bounds.size.x);
        corona.color = WithAlpha(ElectricColor, strength * (0.66f + jitter * 0.12f));
        DrawRing(ring, center, 0.55f + pulse * 1.7f, angle, WithAlpha(ElectricColor, strength * (1f - pulse)));
    }

    private void RenderElectricCross(Vector2 center, float angle, LineRenderer[] sparks, LineRenderer[] cores,
        float opacity, bool damaging)
    {
        // The emitter can be below the walkable floor.
        float bottom = center.y >= arena.yMin
            ? arena.yMin
            : Mathf.Min(arenaBackground.bounds.min.y, center.y - 2f);
        for (int i = 0; i < cores.Length; i++)
        {
            float radians = (angle + 90f * i) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 edge = new Vector2(direction.x > 0f ? arena.xMax : arena.xMin,
                direction.y > 0f ? arena.yMax : bottom);
            float length = Mathf.Min(Mathf.Abs(direction.x) < 0.0001f ? float.PositiveInfinity
                    : (edge.x - center.x) / direction.x,
                Mathf.Abs(direction.y) < 0.0001f ? float.PositiveInfinity : (edge.y - center.y) / direction.y);

            Color cyan = ElectricColor;
            cyan.a = opacity;
            sparks[i].startColor = sparks[i].endColor = cyan;
            sparks[i].widthMultiplier = damaging ? 0.36f + Mathf.Sin(Time.time * 50f + i) * 0.095f : 0.18f;
            cores[i].startColor = cores[i].endColor = new Color(0.85f, 0.99f, 1f, opacity);
            Vector2 normal = Vector2.Perpendicular(direction);
            for (int j = 0; j < cores[i].positionCount; j++)
            {
                float fraction = j / (float)(cores[i].positionCount - 1);
                float wobble = damaging && j != 0 ? Mathf.Sin(j * 3.46f + i + Time.time * 58f) * 0.12f : 0f;
                Vector2 point = center + direction * (length * fraction) + normal * wobble;
                sparks[i].SetPosition(j, point);
                cores[i].SetPosition(j, point);
            }
            if (damaging) HitSegment(center, center + direction * length, 0.27f);
        }
    }

    private IEnumerator SummonMonsters()
    {
        bool fromLeft = Random.value < 0.5f;
        float left = arena.xMin + 3.2f;
        float right = arena.xMax - 3.2f;
        for (int stop = 0; stop < TeleportCount; stop++)
        {
            float fraction = stop / (float)(TeleportCount - 1);
            float x = Mathf.Lerp(fromLeft ? left : right, fromLeft ? right : left, fraction);
            yield return Warp(AtFloor(x));
            yield return OpenSummonRift(x);
            ResetPose();
        }
    }

    private IEnumerator OpenSummonRift(float x)
    {
        GameplaySfx.Play(GameplaySfx.Cue.Energy, .45f);
        SpriteRenderer abyss = MakeSprite("블랙홀 중심", smokeSprite, 14, effects);
        SpriteRenderer halo = MakeSprite("블랙홀 후광", smokeSprite, 13, effects);
        LineRenderer rim = MakeLine("블랙홀 가장자리", 15, 0.12f, 41);
        LineRenderer spiral = MakeLine("블랙홀 소용돌이", 16, 0.10f, 34);
        int count = Random.Range(1, 5);
        int released = 0;
        float burstTime = -1f;
        yield return Animate(SummonStopDuration, progress =>
        {
            float lift = Mathf.Clamp01(progress / 0.24f) * (1f - Mathf.InverseLerp(0.82f, 1f, progress));
            float pose = lift * (summonFrames.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(pose), summonFrames.Length - 2);
            HandFrame from = summonFrames[index];
            HandFrame to = summonFrames[index + 1];
            float blend = pose - index;
            body.sprite = blend < 0.5f ? from.sprite : to.sprite;
            Vector2 center = PosePoint(Vector2.Lerp(from.hand, to.hand, blend) + Vector2.up * 0.55f);
            abyss.transform.position = halo.transform.position = center;
            while (released < count && progress >= Mathf.Lerp(0.28f, 0.73f, released / (float)Mathf.Max(1, count - 1)))
            {
                SpawnMonster(center, x, released, count);
                released++;
                burstTime = progress;
            }
            float expand = Mathf.SmoothStep(0f, 1f, progress / 0.22f)
                * (1f - Mathf.SmoothStep(0.8f, 1f, progress));
            float flare = 1f - Mathf.Clamp01((progress - burstTime) * 23f);
            float radius = expand * (1.24f + 0.17f * Mathf.Sin(progress * 28f) + 0.28f * flare);
            abyss.transform.localScale = Vector3.one * (2.7f * radius / smokeSprite.bounds.size.x);
            halo.transform.localScale = abyss.transform.localScale * 1.58f;
            abyss.color = new Color(0.012f, 0.006f, 0.023f, expand);
            halo.color = WithAlpha(RiftColor, expand * (0.65f + flare * 0.34f));
            DrawRing(rim, center, radius, -progress * 1120f, WithAlpha(RiftColor, expand));
            spiral.startColor = WithAlpha(new Color(0.95f, 0.69f, 1f), expand * 0.95f);
            spiral.endColor = Color.clear;
            for (int i = 0; i < spiral.positionCount; i++)
            {
                float along = i / (float)(spiral.positionCount - 1);
                float angle = (progress * 2.7f + along * 1.3f) * 2f * Mathf.PI;
                spiral.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                    * radius * Mathf.Lerp(1.31f, 0.12f, along));
            }
        });
        Destroy(abyss.gameObject);
        Destroy(halo.gameObject);
        Destroy(rim.gameObject);
        Destroy(spiral.gameObject);
    }

    private static void DrawRing(LineRenderer ring, Vector2 center, float radius, float rotation, Color color)
    {
        ring.startColor = ring.endColor = color;
        float angleOffset = rotation * Mathf.Deg2Rad;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * (2f * Mathf.PI / (ring.positionCount - 1));
            float ripple = 1f + Mathf.Sin(angle * 6f) * 0.075f;
            ring.SetPosition(i, center + new Vector2(Mathf.Cos(angle + angleOffset), Mathf.Sin(angle + angleOffset))
                * radius * ripple);
        }
    }

    private void SpawnMonster(Vector2 origin, float x, int index, int count)
    {
        SummonMonster summon = summonMonsters[Random.Range(0, summonMonsters.Length)];
        float spread = index - (count - 1) * 0.5f;
        float spawnX = Mathf.Clamp(x + (spread * 3.15f) + Random.Range(-1.2f, 1.2f),
            arena.xMin + 1.2f, arena.xMax - 1.2f);
        MonsterHealth monster = Instantiate(summon.prefab, origin, Quaternion.identity, effects);
        if (summon.healthSource != null) monster.InitializeHpOverride(summon.healthSource.MaxHp);
        monster.DropTableOverride = summonDrops;
        Vector2 landing = AtFloor(spawnX);
        if (monster.TryGetComponent<FlyingMonsterAttack>(out _))
        {
            landing.y += 3.2f;
        }
        else if (monster.TryGetComponent(out BoxCollider2D monsterBody))
        {
            Bounds bounds = monsterBody.bounds;
            Vector2 anchor = monster.transform.position;
            // Land with the collider's feet above the deck, even for scaled or offset prefabs.
            landing.y += anchor.y - bounds.min.y + MonsterGroundMotion.SkinWidth;
            float minX = arena.xMin + anchor.x - bounds.min.x + MonsterGroundMotion.SkinWidth;
            float maxX = arena.xMax - (bounds.max.x - anchor.x) - MonsterGroundMotion.SkinWidth;
            landing.x = Mathf.Clamp(landing.x, minX, maxX);
        }
        MonsterEntranceMotion.Play(monster, origin, landing, new MonsterEntranceSettings
        {
            type = MonsterEntranceType.GroundArc,
            duration = Random.Range(0.5f, 0.75f),
            arcHeight = Random.Range(1.15f, 2.05f)
        });
    }

    private IEnumerator EdgeSlashes()
    {
        float direction = Random.value < 0.5f ? 1f : -1f;
        yield return Warp(SlashEntry(direction), faceRight: direction > 0f);
        float endTime = Time.time + SlashVolleyDuration;
        yield return SwingSlashVolley(direction, SlashVolleyDuration * 0.5f);
        direction = -direction;
        yield return Warp(SlashEntry(direction), faceRight: direction > 0f);
        yield return SwingSlashVolley(direction, Mathf.Max(0f, endTime - Time.time));
        ResetPose();
    }

    private Vector2 SlashEntry(float direction) => AtFloor(direction > 0f ? arena.xMin + 2f : arena.xMax - 2f);

    private IEnumerator SwingSlashVolley(float direction, float duration)
    {
        const float swingDuration = 0.55f;
        const float slashTravel = 1.5f;
        const float minSlashLifetime = 0.38f;
        // Leave the far quarter available, including when the next volley starts from the other side.
        float limitX = Mathf.Lerp(arena.xMin, arena.xMax, direction > 0f ? 0.75f : 0.25f);
        float nextRelease = swingDuration * 0.36f;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float phase = Mathf.Repeat(time, swingDuration) / swingDuration;
            SwingFrame frame = chargeFrames[FrameIndex(phase, chargeFrames.Length)];
            body.sprite = frame.sprite;
            float lifetime = Mathf.Min(slashTravel, duration - time);
            if (time >= nextRelease && lifetime >= minSlashLifetime)
            {
                Vector2 start = PosePoint(frame.bladeTip);
                Vector2 end = new Vector2(limitX, arena.yMin + Random.Range(1.25f, 3.85f));
                start.y = Mathf.Max(arena.yMin + 1f, start.y);
                StartCoroutine(FlySlash(start, end, direction, lifetime, lifetime / slashTravel));
                nextRelease += swingDuration;
            }
            yield return null;
        }
    }

    private IEnumerator FlySlash(Vector2 start, Vector2 end, float direction, float lifetime, float travelFraction)
    {
        GameplaySfx.Play(GameplaySfx.Cue.Throw, .39f);
        LineRenderer glow = MakeLine("공오일 참격 외곽", 14, 0.48f, 13);
        LineRenderer edge = MakeLine("공오일 참격", 15, 0.17f, 13);
        AnimationCurve taper = AnimationCurve.EaseInOut(0f, 0.05f, 0.5f, 1f);
        taper.AddKey(1f, 0.05f);
        glow.widthCurve = edge.widthCurve = taper;
        Vector2 previous = start;
        bool hit = false;
        yield return Animate(lifetime, progress =>
        {
            Vector2 position = Vector2.Lerp(start, end, progress * travelFraction);
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.76f, 1f, progress));
            glow.startColor = glow.endColor = WithAlpha(SlashColor, opacity * 0.7f);
            edge.startColor = edge.endColor = WithAlpha(new Color(1f, 0.88f, 0.63f), opacity);
            for (int i = 0; i < edge.positionCount; i++)
            {
                float along = i / (float)(edge.positionCount - 1);
                Vector2 point = position + new Vector2(-1.5f * direction * Mathf.Abs(2f * along - 1f),
                    (along - 0.5f) * 2.7f);
                glow.SetPosition(i, point);
                edge.SetPosition(i, point);
            }
            if (!hit && direction * (PlayerCenter.x - end.x) < 0f)
                hit = HitSegment(previous, position, 1.17f);
            previous = position;
        });
        Destroy(glow.gameObject);
        Destroy(edge.gameObject);
    }

    private IEnumerator FloorLightning()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Electric, .52f);
        float targetX = Mathf.Clamp(player.transform.position.x, arena.xMin, arena.xMax);
        yield return Warp(AtFloor(targetX), faceRight: targetX > transform.position.x);
        int downSwingFrames = chargeFrames.Length / 2;
        yield return Animate(0.42f, progress =>
        {
            body.sprite = chargeFrames[FrameIndex(progress, downSwingFrames)].sprite;
        });

        var surfaces = new List<Bounds> { arenaFloor.bounds };
        foreach (PlatformEffector2D platform in transform.parent.GetComponentsInChildren<PlatformEffector2D>())
        {
            if (platform.TryGetComponent(out BoxCollider2D collider) && collider.enabled)
                surfaces.Add(collider.bounds);
        }

        var charge = new LineRenderer[surfaces.Count];
        for (int i = 0; i < surfaces.Count; i++)
        {
            Bounds bounds = surfaces[i];
            charge[i] = MakeLine("푸른 발판 예고", 16, i == 0 ? 0.72f : 0.5f, 2);
            charge[i].SetPosition(0, new Vector2(bounds.min.x, bounds.max.y - 0.08f));
            charge[i].SetPosition(1, new Vector2(bounds.max.x, bounds.max.y - 0.08f));
        }
        yield return Animate(1.5f, progress =>
        {
            float opacity = Mathf.Pow(Mathf.Abs(Mathf.Sin(progress * 6f * Mathf.PI)), 1.6f) * 0.92f;
            foreach (LineRenderer strip in charge)
                strip.startColor = strip.endColor = WithAlpha(ElectricColor, opacity);
        });
        foreach (LineRenderer strip in charge) Destroy(strip.gameObject);
        yield return new WaitForSeconds(0.5f);

        var bolts = new LineRenderer[surfaces.Count];
        for (int i = 0; i < surfaces.Count; i++)
            bolts[i] = MakeLine("발판 낙뢰", 16, 0.23f, Mathf.CeilToInt(surfaces[i].size.x * 2f) + 1);

        yield return Animate(0.55f, progress =>
        {
            bool damaging = progress < 0.72f;
            for (int i = 0; i < surfaces.Count; i++)
            {
                Bounds bounds = surfaces[i];
                LineRenderer bolt = bolts[i];
                Vector2 left = new Vector2(bounds.min.x, bounds.max.y);
                Vector2 right = new Vector2(bounds.max.x, bounds.max.y);
                bolt.startColor = WithAlpha(new Color(0.85f, 0.97f, 1f), 1f - progress);
                bolt.endColor = WithAlpha(ElectricColor, 1f - progress);
                for (int j = 0; j < bolt.positionCount; j++)
                {
                    Vector2 point = Vector2.Lerp(left, right, j / (float)(bolt.positionCount - 1));
                    point.y += 0.2f + Mathf.Sin(j * 4.6f + Time.time * 65f + i) * 0.32f;
                    bolt.SetPosition(j, point);
                }
                if (damaging) HitSegment(left, right, 0.38f);
            }
        });
        foreach (LineRenderer bolt in bolts) Destroy(bolt.gameObject);
        ResetPose();
    }

    private IEnumerator MissileBarrage(bool fromSmokeZone)
    {
        yield return Hide();
        int zone = fromSmokeZone ? Random.Range(-1, 2) : 0;
        float sourceX = zone < 0 ? arena.xMin : zone > 0 ? arena.xMax : arena.center.x;
        float flightDuration = fromSmokeZone && zone == 0 ? MissileFlightDuration * 0.5f : MissileFlightDuration;
        if (!fromSmokeZone)
        {
            yield return MissileLaneSmoke();
        }

        for (int wave = 0; wave < MissileWaves; wave++)
        {
            float leftHeight = Random.Range(MissileMinHeight, MissileMaxHeight);
            float rightHeight = Random.Range(MissileMinHeight, MissileMaxHeight);
            if (fromSmokeZone)
            {
                StartCoroutine(MissileSmoke(new Vector2(sourceX, arena.yMin + 1.9f), 1.65f));
                StartCoroutine(FlyMissile(zone > 0 ? -1f : 1f, leftHeight, sourceX, flightDuration, fromSmokeZone: true));
                StartCoroutine(FlyMissile(zone < 0 ? 1f : -1f, rightHeight, sourceX, flightDuration, fromSmokeZone: true));
            }
            else
            {
                StartCoroutine(FlyMissile(1f, leftHeight, arena.xMin, flightDuration, fromSmokeZone: false));
                StartCoroutine(FlyMissile(-1f, rightHeight, arena.xMax, flightDuration, fromSmokeZone: false));
            }
            yield return new WaitForSeconds(MissileWaveInterval);
        }
        float warningDuration = fromSmokeZone ? MissileWarningDuration : 0f;
        yield return new WaitForSeconds(warningDuration + flightDuration
            + MissileRecoveryDuration - MissileWaveInterval);
    }

    private IEnumerator MissileLaneSmoke()
    {
        if (missileLaneSmokeMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("GongOilMissileSmoke");
            if (shader != null && shader.isSupported) missileLaneSmokeMaterial = new Material(shader);
        }

        const float sideExtension = 4f;
        float width = arena.width + sideExtension * 2f;
        float bottom = arenaFloor.bounds.min.y - 0.9f;
        float top = arena.yMin + MissileMaxHeight + 1.95f;
        float height = top - bottom;
        SpriteRenderer smoke = MakeSprite("미사일 경로 검은 연기", smokeSprite, 13, effects);
        smoke.transform.position = new Vector2(arena.center.x, (bottom + top) * 0.5f);
        smoke.transform.localScale = new Vector3(width / smokeSprite.bounds.size.x,
            height / smokeSprite.bounds.size.y, 1f);
        bool useSmokeShader = missileLaneSmokeMaterial != null;
        if (useSmokeShader)
        {
            missileLaneSmokeMaterial.SetFloat("_BandAspect", width / height);
            smoke.sharedMaterial = missileLaneSmokeMaterial;
        }

        Vector3 center = smoke.transform.position;
        yield return Animate(MissileLaneSmokeDuration, progress =>
        {
            float time = progress * MissileLaneSmokeDuration;
            float opacity = Mathf.SmoothStep(0f, 1f, time / 0.16f)
                * Mathf.SmoothStep(0f, 1f, (MissileLaneSmokeDuration - time) / 0.28f);
            if (useSmokeShader)
            {
                missileLaneSmokeMaterial.SetFloat("_SmokeTime", time);
                missileLaneSmokeMaterial.SetFloat("_SmokeOpacity", opacity);
            }
            else
            {
                smoke.transform.position = center + Vector3.up * progress * 0.65f;
                smoke.color = WithAlpha(SmokeColor, opacity);
            }
        });
        smoke.gameObject.SetActive(false);
        Destroy(smoke.gameObject);
    }

    private IEnumerator MissileSmoke(Vector2 origin, float size)
    {
        origin = MissileSmokeOrigin(origin);
        const int puffCount = 12;
        var smoke = new SpriteRenderer[puffCount];
        var offsets = new Vector2[puffCount];
        for (int i = 0; i < puffCount; i++)
        {
            smoke[i] = MakeSprite("미사일 검은 연기", smokeSprite, 13, effects);
            offsets[i] = new Vector2(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 2.4f)) * size;
        }
        float scale = size * 3f / smokeSprite.bounds.size.x;
        yield return Animate(MissileSmokeDuration, progress =>
        {
            for (int i = 0; i < puffCount; i++)
            {
                float layer = i / (float)(puffCount - 1);
                float puffProgress = Mathf.InverseLerp(layer * 0.16f, 1f, progress);
                float expansion = Mathf.SmoothStep(0f, 1f, puffProgress);
                float opacity = Mathf.SmoothStep(0f, 1f, puffProgress * 10f)
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 1f, puffProgress)));
                smoke[i].transform.position = origin + offsets[i] * (0.25f + expansion * 0.82f)
                    + Vector2.up * puffProgress * (3.5f + layer * 1.6f);
                smoke[i].transform.localScale = Vector3.one * (scale * (0.65f + expansion));
                smoke[i].color = WithAlpha(Color.Lerp(SmokeColor, new Color(0.17f, 0.15f, 0.15f), layer * 0.72f),
                    opacity * 0.92f);
            }
        });
        foreach (SpriteRenderer puff in smoke) Destroy(puff.gameObject);
    }

    private IEnumerator FlyMissile(float direction, float height, float sourceX, float flightDuration, bool fromSmokeZone)
    {
        Vector2 start = new Vector2(sourceX, arena.yMin + height);
        Vector2 end = new Vector2(direction > 0f ? arena.xMax : arena.xMin, start.y);
        if (fromSmokeZone)
        {
            GameplaySfx.Play(GameplaySfx.Cue.Warning, 0.4f);
            LineRenderer warning = MakeLine("미사일 진입 경고", 14, 0.13f, 2);
            warning.startColor = warning.endColor = new Color(1f, 0.37f, 0.16f, 0.72f);
            warning.SetPosition(0, start);
            warning.SetPosition(1, start + Vector2.right * direction * 3.8f);
            yield return new WaitForSeconds(MissileWarningDuration);
            Destroy(warning.gameObject);
        }

        Camera view = fromSmokeZone ? Camera.main : null;
        // A launch at the far wall can be outside the camera that follows Arisa.
        bool needsEntrySmoke = view != null && !IsInMissileView(view, MissileSmokeOrigin(start));
        GameplaySfx.Play(GameplaySfx.Cue.Missile, 0.37f);
        SpriteRenderer missile = MakeSprite("파괴 불가 미사일", missileSprite, 14, effects);
        missile.transform.localScale = Vector3.one * (1.65f / missileSprite.bounds.size.x);
        missile.flipX = direction < 0f;
        Vector2 previous = start;
        bool hit = false;
        yield return Animate(flightDuration, progress =>
        {
            Vector2 position = Vector2.Lerp(start, end, progress);
            missile.transform.position = position;
            if (needsEntrySmoke && IsInMissileView(view, position))
            {
                needsEntrySmoke = false;
                StartCoroutine(MissileSmoke(position, 0.8f));
            }
            if (!hit) hit = HitSegment(previous, position, 0.47f);
            previous = position;
        });
        Destroy(missile.gameObject);
    }

    private Vector2 MissileSmokeOrigin(Vector2 source)
    {
        source.x = Mathf.Clamp(source.x, arena.xMin + 1.2f, arena.xMax - 1.2f);
        return source;
    }

    private static bool IsInMissileView(Camera view, Vector2 position)
    {
        Vector3 viewport = view.WorldToViewportPoint(position);
        return viewport.z > 0f && viewport.x > 0.06f && viewport.x < 0.94f
            && viewport.y > 0.02f && viewport.y < 0.98f;
    }

    private IEnumerator Warp(Vector2 feet, bool visible = true, bool? faceRight = null)
    {
        yield return Hide();
        transform.position = feet;
        if (!visible) yield break;

        body.flipX = faceRight ?? PlayerCenter.x > feet.x;
        body.sprite = idleSprite;
        yield return AnimateTeleport(0.22f, arriving: true);
        body.enabled = true;
        hitbox.enabled = true;
    }

    private IEnumerator Hide()
    {
        if (body.enabled)
        {
            hitbox.enabled = false;
            body.enabled = false;
            yield return AnimateTeleport(0.2f, arriving: false);
            yield break;
        }
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator AnimateTeleport(float duration, bool arriving)
    {
        GameplaySfx.Play(GameplaySfx.Cue.Teleport, .31f);
        Vector3 feet = transform.position;
        Vector2 center = BossCenter;
        SpriteRenderer silhouette = MakeSprite("순간이동 실루엣", body.sprite, body.sortingOrder, effects);
        SpriteRenderer flash = MakeSprite("순간이동 섬광", smokeSprite, 16, effects);
        LineRenderer ring = MakeLine("순간이동 파동", 16, 0.09f, 25);
        silhouette.flipX = body.flipX;
        flash.transform.position = center;
        float flashScale = 3.7f / smokeSprite.bounds.size.x;
        yield return Animate(duration, progress =>
        {
            float visibility = Mathf.SmoothStep(0f, 1f, arriving ? progress : 1f - progress);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            Vector3 scale = new Vector3(Mathf.Lerp(0.07f, 1f, visibility),
                Mathf.Lerp(1.32f, 1f, visibility), 1f);
            silhouette.transform.localScale = Vector3.Scale(transform.localScale, scale);
            silhouette.transform.position = feet - Vector3.up * ((scale.y - 1f) * 1.8f);
            silhouette.color = WithAlpha(Color.Lerp(PortalColor, Color.white, visibility), visibility);
            flash.transform.localScale = new Vector3(0.3f + pulse, 0.9f + pulse * 0.54f, 1f) * flashScale;
            flash.color = WithAlpha(PortalColor, pulse * 0.78f);
            DrawRing(ring, center, Mathf.Lerp(0.24f, 1.9f, arriving ? progress : 1f - progress), 0f,
                WithAlpha(PortalColor, pulse * 0.8f));
        });
        Destroy(silhouette.gameObject);
        Destroy(flash.gameObject);
        Destroy(ring.gameObject);
    }

    private bool HitSegment(Vector2 from, Vector2 to, float radius)
    {
        if (GameplayPause.IsPaused || player.IsDead || Time.time < nextHitTime) return false;
        Vector2 center = PlayerCenter;
        Vector2 segment = to - from;
        float position = segment.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(Vector2.Dot(center - from, segment) / segment.sqrMagnitude) : 0f;
        Vector2 contact = from + segment * position;
        Vector2 normal = (center - contact).normalized;
        Vector2 extents = playerBody != null ? playerBody.bounds.extents : new Vector2(0.36f, 0.65f);
        // Approximate Arisa's body with an ellipse so a crouch changes the vertical reach.
        float reach = new Vector2(extents.x * normal.x, extents.y * normal.y).magnitude;
        if ((center - contact).sqrMagnitude > (radius + reach) * (radius + reach)) return false;

        nextHitTime = Time.time + 0.65f;
        player.TakeDamage(AttackDamage, contact);
        return true;
    }

    private SpriteRenderer MakeSprite(string objectName, Sprite sprite, int sortingOrder, Transform parent)
    {
        var effect = new GameObject(objectName);
        effect.transform.SetParent(parent, false);
        var renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = body.sharedMaterial;
        renderer.sortingLayerID = body.sortingLayerID;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private LineRenderer MakeLine(string objectName, int sortingOrder, float width, int positionCount)
    {
        var effect = new GameObject(objectName);
        effect.transform.SetParent(effects, false);
        var line = effect.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.sortingLayerID = body.sortingLayerID;
        line.sortingOrder = sortingOrder;
        line.positionCount = positionCount;
        line.widthMultiplier = width;
        line.useWorldSpace = true;
        return line;
    }

    private static IEnumerator Animate(float duration, System.Action<float> update)
    {
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            update(time / duration);
            yield return null;
        }
        update(1f);
    }

    private void OnDefeated()
    {
        Vector3 defeatPosition = body.bounds.center;
        defeated = true;
        StopEncounter();
        if (dialogueManager != null) dialogueManager.StopDialogue(introduction);
        if (finalSkyBattle != null && player != null && !player.IsDead)
        {
            GameplayInput.Block(this);
            defeatSequence = StartCoroutine(PlayDefeatSequence(defeatPosition));
        }
    }

    internal bool CanStartSkyBattleForTest =>
        finalSkyBattle != null && finalSkyBattle.isActiveAndEnabled && player != null && !player.IsDead;

    internal void StartSkyBattleForTest()
    {
        if (!CanStartSkyBattleForTest) return;
        CancelDefeatSequence();
        StopEncounter();
        if (dialogueManager != null) dialogueManager.StopDialogue(introduction);
        finalSkyBattle.BeginFromTestMenu(player, bossBar, missileSprite);
    }

    private void OnPlayerDefeated() => StopEncounter();

    private void StopEncounter()
    {
        if (!running) return;

        running = false;
        player.Defeated.RemoveListener(OnPlayerDefeated);
        StopAllCoroutines();
        if (bossBar != null) bossBar.HideEncounter(health);
        if (effects != null)
        {
            effects.gameObject.SetActive(false);
            Destroy(effects.gameObject);
            effects = null;
        }
        hitbox.enabled = false;
        transform.position = startingPosition;
        ResetPose();
        body.flipX = false;
        body.enabled = !defeated;
        if (!defeated) health.ResetForMission();
    }

    private void OnDisable()
    {
        CancelDefeatSequence();
        StopEncounter();
    }

    private void OnDestroy()
    {
        if (health != null) health.Defeated.RemoveListener(OnDefeated);
        if (lineMaterial != null) Destroy(lineMaterial);
        if (missileLaneSmokeMaterial != null) Destroy(missileLaneSmokeMaterial);
    }
}
