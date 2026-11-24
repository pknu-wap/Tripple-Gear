using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AtailBossController : MonoBehaviour
{
    [Serializable]
    private sealed class Platform
    {
        [SerializeField] internal Collider2D surface;
        [SerializeField] private GameObject[] waterEffects;
        internal Vector2 Landing { get; private set; }
        internal bool IsIntact => surface.gameObject.activeInHierarchy;

        internal void RememberPosition()
        {
            Bounds bounds = surface.bounds;
            Landing = new Vector2(bounds.center.x, bounds.max.y);
        }

        internal void SetIntact(bool value)
        {
            if (value) surface.transform.parent.gameObject.SetActive(true);
            surface.gameObject.SetActive(value);
            foreach (GameObject effect in waterEffects) effect.SetActive(value);
        }
    }

    private enum Pattern { Smash, Recover, Summon, BombRun, Fountain }
    private const int RecoveryThreshold = 8;
    private const int RecoveryTarget = 10;
    private const float PlatformWarningSeconds = 3f;
    private const int FlyingSummonCount = 3;
    private const int MissileBurstCount = 3;
    private const float FlightHeight = 8.5f;
    private const float AscentSeconds = 0.9f;
    private const float DescentSeconds = 0.58f;
    private const float AttackFlightWidth = 15f;
    private const float AttackEntryClearance = 10f;
    private const float MissileSpreadDegrees = 18f;
    private const int BombCount = 7;
    private const int FountainBursts = 3;
    private const int MissilesPerBurst = 5;
    private const float MissileRewardChance = 0.30f;

    [Header("전투 연결")]
    [SerializeField] private BossTrigger battleTrigger;
    [SerializeField] private MonsterHealth health;
    [SerializeField] private AtailSerpent serpent;
    [SerializeField] private ArisaHealth player;
    [SerializeField] private BoatVoyage voyage;
    [SerializeField] private TeleportTrigger defeatExit;
    [SerializeField, KoreanLabel("침몰 연출 후 총 선택")] private GameManager gunSelectionManager;
    [SerializeField] private Platform[] platforms;
    [SerializeField] private Transform waterline;

    [Header("패턴 설정")]
    [SerializeField, Min(PlatformWarningSeconds)] private float smashFlightSeconds = 3.2f;
    [SerializeField, Min(0.5f)] private float recoveryFlightSeconds = 4.0f;
    [SerializeField, Min(0.5f)] private float summonFlightSeconds = 3.6f;
    [SerializeField, Min(0.1f)] private float warningSeconds = 0.7f;
    [SerializeField, Min(0f)] private float betweenPatterns = 1.1f;
    [SerializeField, Min(0.1f)] private float missileSpeed = 8f;
    [SerializeField, Min(0)] private int missileDamage = 1;
    [SerializeField, Min(0)] private int smashDamage = 1;
    [SerializeField] private EnemyProjectile2D missilePrefab;
    [SerializeField] private MonsterHealth flyingPrefab;
    [SerializeField] private ParticleSystem splashEffect;
    [SerializeField] private Shader platformGlowShader;
    [SerializeField] private AnimationClip platformBreakAnimation;
    [SerializeField, Min(1f)] private float bombFlightSeconds = 5f;
    [SerializeField] private EnemyProjectile2D bombPrefab;
    [FormerlySerializedAs("fountainRewards")]
    [SerializeField] private SkillPickup[] missileRewards;
    [SerializeField] private SpriteRenderer fountainWave;
    [SerializeField] private Image bossBarFill;
    [SerializeField] private Color phaseTwoBarColor = new Color(0.57f, 0.045f, 0.085f);
    [SerializeField] private GameObject phaseTwoExplosion;

    private readonly List<MonsterHealth> summons = new List<MonsterHealth>();
    private readonly RaycastHit2D[] platformHits = new RaycastHit2D[14];
    private Collider2D playerCollider;
    private ContactFilter2D platformFilter;
    private GameObject spawnedObjects;
    private bool running;
    private bool playingDefeat;
    private bool phaseTwoStarted;
    private bool phaseTwoAttackPending;
    private Material platformGlowMaterial;
    private Color normalBarColor;

    private float WaterY => waterline.position.y;
    private Vector2 PlayerCenter => playerCollider != null ? (Vector2)playerCollider.bounds.center : player.transform.position;

    private void Awake()
    {
        playerCollider = player.GetComponent<CapsuleCollider2D>();
        platformFilter.SetLayerMask(LayerMask.GetMask("Stair"));
        foreach (Platform platform in platforms) platform.RememberPosition();
        platformGlowMaterial = new Material(platformGlowShader);
        if (bossBarFill != null) normalBarColor = bossBarFill.color;
    }

    private void OnDestroy() => Destroy(platformGlowMaterial);

    private void OnEnable()
    {
        if (voyage != null) voyage.Departed += ResetForDeparture;
    }

    private void OnDisable()
    {
        if (voyage != null) voyage.Departed -= ResetForDeparture;
        EndBattle();
    }

    public void BeginBattle()
    {
        if (running || player.IsDead || !health.isActiveAndEnabled) return;
        health.ResetForMission();
        serpent.SetAttackHitboxes(true);
        phaseTwoStarted = false;
        phaseTwoAttackPending = false;
        if (bossBarFill != null) bossBarFill.color = normalBarColor;
        serpent.SetWaterline(WaterY);
        spawnedObjects = new GameObject("ATAIL 전투 효과와 소환몹");
        spawnedObjects.transform.SetParent(transform, false);
        running = true;
        health.HpChanged.AddListener(UpdatePhase);
        StartCoroutine(AttackLoop());
    }

    public void EndBattle()
    {
        if (!running && !playingDefeat) return;
        StopBattleActivity();
        playingDefeat = false;
        if (serpent.isActiveAndEnabled) serpent.Hide();
    }

    private void StopBattleActivity()
    {
        running = false;
        health.HpChanged.RemoveListener(UpdatePhase);
        StopAllCoroutines();
        if (spawnedObjects != null)
        {
            spawnedObjects.SetActive(false);
            Destroy(spawnedObjects);
            spawnedObjects = null;
        }
        summons.Clear();
    }

    public void PlayDefeatSequence()
    {
        if (!running) return;
        StopBattleActivity();
        serpent.CloseHatch();
        serpent.SetAttackHitboxes(false);
        playingDefeat = true;
        spawnedObjects = new GameObject("아테일 침몰 연출");
        spawnedObjects.transform.SetParent(transform, false);
        StartCoroutine(DefeatSequence());
    }

    internal void CancelDefeatSequence()
    {
        if (playingDefeat) EndBattle();
    }

    private IEnumerator DefeatSequence()
    {
        if (serpent.IsSurfaced)
        {
            Vector2 nose = serpent.NosePosition;
            Vector2 below = new Vector2(nose.x, WaterY - serpent.Span - 8f);
            PlaySplash(new Vector2(nose.x, WaterY), 1.55f);
            yield return MoveAlong(1.1f, progress => MoveDown(nose, below, progress));
            serpent.Hide();
        }

        yield return new WaitForSeconds(0.48f);
        float left = platforms[0].Landing.x;
        float right = platforms[platforms.Length - 1].Landing.x;
        float center = Mathf.Clamp(PlayerCenter.x, left + 3f, right - 3f);
        float side = center <= (left + right) * 0.5f ? 1f : -1f;
        Vector2 direction = Vector2.right * side;
        Vector2 entry = new Vector2(Mathf.Clamp(center - side * 6f, left - 4f, right + 4f), WaterY);
        Vector2 sky = entry + Vector2.up * FlightHeight;
        yield return Breach(entry);
        yield return Ascend(entry, sky);

        StartCoroutine(PlayDefeatBursts());
        Vector2 flightEnd = sky + direction * Mathf.Min(serpent.Span * 0.66f, 27f);
        yield return MoveAlong(2.65f, progress =>
        {
            float straighten = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.88f, progress));
            serpent.MoveAlongTrail(Vector2.Lerp(sky, flightEnd, progress), direction, straighten);
        });

        // Keep the whole chain horizontal while the wreck settles beneath the waves.
        Vector2 sunk = flightEnd + Vector2.down * (FlightHeight + 7f);
        bool struckWater = false;
        yield return MoveAlong(2.2f, progress =>
        {
            Vector2 nose = Vector2.Lerp(flightEnd, sunk, progress * progress);
            serpent.PlaceStraight(nose, direction);
            if (struckWater || nose.y > WaterY + 1.2f) return;
            struckWater = true;
            for (int i = 0; i < 5; i++)
            {
                float x = nose.x - side * serpent.Span * (0.10f + i * 0.17f);
                PlaySplash(new Vector2(x, WaterY), 1.9f);
            }
        });
        serpent.Hide();
        yield return new WaitForSeconds(0.65f);
        GameObject effects = spawnedObjects;
        spawnedObjects = null;
        playingDefeat = false;
        Destroy(effects);
        if (player.IsDead || defeatExit == null) yield break;

        if (gunSelectionManager == null || !gunSelectionManager.TryShowGunSelection(defeatExit))
        {
            defeatExit.TeleportPlayer();
        }
    }

    private IEnumerator PlayDefeatBursts()
    {
        while (playingDefeat)
        {
            if (phaseTwoExplosion != null && serpent.TryGetDamageBurstPosition(out Vector2 position))
                SpawnBodyExplosion(position, "아테일 침몰 폭발", UnityEngine.Random.Range(0.95f, 1.65f));
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.06f, 0.11f));
        }
    }

    private void LateUpdate()
    {
        if (running && (!health.isActiveAndEnabled || player.IsDead)) EndBattle();
    }

    private void ResetForDeparture()
    {
        EndBattle();
        battleTrigger.ResetEncounter();
        foreach (Platform platform in platforms) platform.SetIntact(true);
    }

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(betweenPatterns);
        while (running)
        {
            Pattern pattern = ChoosePattern();
            switch (pattern)
            {
                case Pattern.Recover: yield return RecoverOrFireMissiles(); break;
                case Pattern.Summon: yield return Summon(); break;
                case Pattern.BombRun: yield return BombRun(); break;
                case Pattern.Fountain: yield return Fountain(); break;
                default: yield return Smash(); break;
            }
            yield return new WaitForSeconds(betweenPatterns);
        }
    }

    private bool NeedsPlatforms() => CountIntact() <= Mathf.Min(RecoveryThreshold, platforms.Length - 1);

    private void UpdatePhase(int currentHp, int maxHp)
    {
        if (phaseTwoStarted || currentHp <= 0 || currentHp * 2 > maxHp) return;
        phaseTwoStarted = true;
        phaseTwoAttackPending = true;
        if (bossBarFill != null) bossBarFill.color = phaseTwoBarColor;
        if (phaseTwoExplosion != null) StartCoroutine(PlayPhaseTwoExplosions());
    }

    private IEnumerator PlayPhaseTwoExplosions()
    {
        while (running)
        {
            if (!serpent.TryGetDamageBurstPosition(out Vector2 position))
            {
                yield return new WaitForSeconds(0.12f);
                continue;
            }

            SpawnBodyExplosion(position, "아테일 2페이즈 작은 폭발", UnityEngine.Random.Range(0.42f, 0.65f));
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.12f, 0.20f));
        }
    }

    private void SpawnBodyExplosion(Vector2 position, string effectName, float scale)
    {
        GameObject effect = Instantiate(phaseTwoExplosion, position, Quaternion.identity, spawnedObjects.transform);
        effect.name = effectName;
        effect.transform.localScale *= scale;
        Destroy(effect, 1.2f);
    }

    private Pattern ChoosePattern()
    {
        if (NeedsPlatforms()) return Pattern.Recover;

        if (phaseTwoAttackPending)
        {
            phaseTwoAttackPending = false;
            return UnityEngine.Random.value < 0.5f ? Pattern.BombRun : Pattern.Fountain;
        }

        summons.RemoveAll(monster => monster == null || monster.IsDead || !monster.gameObject.activeInHierarchy);
        int optionCount = phaseTwoStarted ? 5 : 3;
        bool canSummon = summons.Count == 0;
        int choice = UnityEngine.Random.Range(0, canSummon ? optionCount : optionCount - 1);
        if (!canSummon && choice >= (int)Pattern.Summon) choice++;
        return (Pattern)choice;
    }

    private int CountIntact()
    {
        int count = 0;
        foreach (Platform platform in platforms)
            if (platform.IsIntact) count++;
        return count;
    }

    private IEnumerator Smash()
    {
        Platform first = FindPlayerPlatform();
        if (first == null) yield break;
        Vector2 start = first.Landing + Vector2.up * 2.8f;

        Vector2 bottom = first.Landing - Vector2.up * 10f;
        PlaySplash(first.Landing, 0.55f);
        serpent.BeginPath(bottom, Vector2.up);
        yield return WarnAndBreak(first, progress =>
            serpent.MoveNose(Vector2.Lerp(bottom, start, progress * progress * progress)));

        Vector2 skyStart = Above(start);
        yield return Ascend(start, skyStart);

        float flightSeconds = Mathf.Max(smashFlightSeconds, PlatformWarningSeconds);
        float warnedFlightSeconds = PlatformWarningSeconds - DescentSeconds;
        float leadIn = flightSeconds - warnedFlightSeconds;
        float distance = Mathf.Clamp(PlayerCenter.x - skyStart.x, -leadIn * 3f, leadIn * 3f);
        Vector2 warningOrigin = skyStart + Vector2.right * distance;
        yield return MoveAlong(leadIn, progress => MoveAcrossSky(skyStart, warningOrigin, progress));

        Platform second = FindPlayerPlatform();
        Vector2 end = second != null ? second.Landing : new Vector2(warningOrigin.x, WaterY);
        Vector2 skyEnd = Above(end);
        if (second == null)
        {
            yield return MoveAlong(DescentSeconds, progress => MoveDown(skyEnd, end, progress));
            yield return Dive(end);
            yield break;
        }

        yield return WarnAndBreak(second, progress =>
        {
            float elapsed = PlatformWarningSeconds * progress;
            if (elapsed < warnedFlightSeconds)
                MoveAcrossSky(warningOrigin, skyEnd, elapsed / warnedFlightSeconds);
            else
                MoveDown(skyEnd, end, (elapsed - warnedFlightSeconds) / DescentSeconds);
        });
        yield return Dive(end);
    }

    private IEnumerator WarnAndBreak(Platform target, Action<float> move)
    {
        SpriteRenderer glow = CreateWarningGlow(target);
        yield return MoveAlong(PlatformWarningSeconds, progress =>
        {
            PulseWarning(glow, progress);
            move(progress);
        });
        Destroy(glow.gameObject);
        BreakPlatform(target);
    }

    private SpriteRenderer CreateWarningGlow(Platform platform)
    {
        SpriteRenderer source = platform.surface.GetComponent<SpriteRenderer>();
        SpriteRenderer glow = CopyPlatformSprite(source, "발판 파괴 경고");
        glow.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        glow.transform.localScale *= 1.025f;
        glow.sharedMaterial = platformGlowMaterial;
        return glow;
    }

    private static void PulseWarning(SpriteRenderer glow, float progress)
    {
        float pulse = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * (3f * progress + 2f * progress * progress));
        glow.color = new Color(1f, Mathf.Lerp(0.68f, 0.31f, progress), 0.07f, Mathf.Lerp(0.32f, 0.91f, pulse));
    }

    private IEnumerator RecoverOrFireMissiles()
    {
        var missing = new List<Platform>();
        if (NeedsPlatforms())
        {
            foreach (Platform platform in platforms)
                if (!platform.IsIntact) missing.Add(platform);
            float playerX = PlayerCenter.x;
            missing.Sort((left, right) => Mathf.Abs(left.Landing.x - playerX).CompareTo(Mathf.Abs(right.Landing.x - playerX)));
        }
        bool restoring = missing.Count > 0;
        int released = 0;
        int total = restoring ? Mathf.Min(RecoveryTarget, platforms.Length) - CountIntact() : MissileBurstCount;
        yield return FlyWithHatch(recoveryFlightSeconds, progress =>
        {
            while (released < total && progress >= Mathf.Lerp(0.49f, 0.74f, released / (float)Mathf.Max(1, total - 1)))
            {
                Vector2 origin = serpent.HatchExit;
                if (restoring) StartCoroutine(DropPlatform(missing[released], origin));
                else FireMissile(origin);
                released++;
            }
        });
    }

    private IEnumerator Summon()
    {
        int spawned = 0;
        yield return FlyWithHatch(summonFlightSeconds, progress =>
        {
            while (spawned < FlyingSummonCount && progress >= 0.44f + spawned * 0.13f)
            {
                Vector2 destination = PlayerCenter + new Vector2(UnityEngine.Random.Range(-4.5f, 4.5f),
                    UnityEngine.Random.Range(2.5f, 4f));
                destination.x = Mathf.Clamp(destination.x, platforms[0].Landing.x, platforms[platforms.Length - 1].Landing.x);
                StartCoroutine(LaunchFlying(serpent.HatchExit, destination));
                spawned++;
            }
        });
    }

    public void PlayerFellIntoWater()
    {
        GameManager.Instance.GameOver();
        player.Defeat();
        EndBattle();
    }

    private IEnumerator BombRun()
    {
        float left = platforms[0].Landing.x - 3f;
        float right = platforms[platforms.Length - 1].Landing.x + 3f;
        bool leftToRight = UnityEngine.Random.value < 0.5f;
        Vector2 entry = new Vector2(leftToRight ? left : right, WaterY);
        Vector2 exit = new Vector2(leftToRight ? right : left, WaterY);
        Vector2 skyStart = Above(entry);
        Vector2 skyEnd = Above(exit);
        yield return Breach(entry);
        yield return Ascend(entry, skyStart);

        int dropped = 0;
        yield return MoveAlong(bombFlightSeconds, progress =>
        {
            MoveAcrossSky(skyStart, skyEnd, progress);
            serpent.SetHatchOpening(Mathf.Min(Mathf.InverseLerp(0.16f, 0.26f, progress),
                Mathf.InverseLerp(0.97f, 0.89f, progress)));
            while (dropped < BombCount && progress >= Mathf.Lerp(0.29f, 0.88f, dropped / (float)(BombCount - 1)))
            {
                EnemyProjectile2D bomb = Instantiate(bombPrefab, serpent.HatchExit, Quaternion.identity, spawnedObjects.transform);
                bomb.LaunchArc(Vector2.down * 2f, 14f, WaterY, missileDamage, player);
                dropped++;
            }
        });
        serpent.CloseHatch();
        yield return MoveAlong(DescentSeconds, progress => MoveDown(skyEnd, exit, progress));
        yield return Dive(exit);
    }

    private IEnumerator Fountain()
    {
        serpent.Hide();
        float left = platforms[0].Landing.x + 2f;
        float right = platforms[platforms.Length - 1].Landing.x - 2f;
        float center = Mathf.Clamp(PlayerCenter.x, left, right);
        float side = center < (left + right) * 0.5f ? 1f : -1f;
        for (int burst = 0; burst < FountainBursts; burst++)
        {
            Vector2 surface = new Vector2(Mathf.Clamp(center + (burst - 1) * 5.5f * side, left, right), WaterY);
            StartCoroutine(SpreadWave(surface, -1f));
            StartCoroutine(SpreadWave(surface, 1f));
            PlaySplash(surface, 0.9f);
            yield return new WaitForSeconds(0.45f);
            PlaySplash(surface, 1.75f);
            for (int i = 0; i < MissilesPerBurst; i++)
            {
                float fan = i - (MissilesPerBurst - 1) * 0.5f;
                Vector2 origin = surface + new Vector2(fan * 0.30f, 0.35f);
                EnemyProjectile2D missile = SpawnMissile(origin);
                missile.LaunchArc(new Vector2(fan * 3.5f, 13f - Mathf.Abs(fan) * 0.65f), 10.5f,
                    WaterY, missileDamage, player);
            }
            yield return new WaitForSeconds(0.60f);
        }
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator SpreadWave(Vector2 surface, float direction)
    {
        SpriteRenderer wave = CopyPlatformSprite(fountainWave, "아테일 수면 파도");
        Vector3 size = wave.transform.localScale;
        Color color = wave.color;
        wave.flipX = direction < 0f;
        yield return MoveAlong(1.2f, progress =>
        {
            wave.transform.position = surface + new Vector2(direction * progress * 5.2f,
                Mathf.Sin(progress * Mathf.PI) * 0.55f);
            wave.transform.localScale = Vector3.Scale(size, new Vector3(1f + progress * 1.5f, 1.6f, 1f));
            color.a = 1f - progress;
            wave.color = color;
        });
        Destroy(wave.gameObject);
    }

    private void TryDropMissileReward(Vector2 position)
    {
        if (!running || UnityEngine.Random.value >= MissileRewardChance) return;
        SkillPickup item = Instantiate(missileRewards[UnityEngine.Random.Range(0, missileRewards.Length)],
            position, Quaternion.identity, spawnedObjects.transform);
        item.GetComponent<Rigidbody2D>().linearVelocity = Vector2.up * 1.5f;
        Destroy(item.gameObject, 14f);
    }

    private Platform FindPlayerPlatform()
    {
        Vector2 feet = PlayerCenter;
        if (playerCollider != null) feet.y = playerCollider.bounds.min.y;
        Vector2 rayOrigin = feet + Vector2.up * 0.2f;
        int hitCount = Physics2D.Raycast(rayOrigin, Vector2.down, platformFilter, platformHits,
            Mathf.Max(0.4f, rayOrigin.y - WaterY + 2f));
        for (int i = 0; i < hitCount; i++)
        {
            foreach (Platform platform in platforms)
                if (platform.IsIntact && platform.surface == platformHits[i].collider) return platform;
        }

        Platform result = null;
        float best = float.PositiveInfinity;
        foreach (Platform platform in platforms)
        {
            if (!platform.IsIntact) continue;
            float score = (platform.surface.ClosestPoint(feet) - feet).sqrMagnitude;
            if (score >= best) continue;
            best = score;
            result = platform;
        }
        return result;
    }

    private void GetAttackRoute(out Vector2 entry, out Vector2 exit)
    {
        float left = platforms[0].Landing.x - 2f;
        float right = platforms[platforms.Length - 1].Landing.x + 2f;
        float playerX = Mathf.Clamp(PlayerCenter.x, left, right);
        float directionAway = playerX <= (left + right) * 0.5f ? 1f : -1f;
        float entryX = Mathf.Clamp(playerX + directionAway * AttackEntryClearance, left, right);
        float exitX = Mathf.Clamp(entryX - directionAway * AttackFlightWidth, left, right);
        entry = new Vector2(entryX, WaterY);
        exit = new Vector2(exitX, WaterY);
    }

    private Vector2 Above(Vector2 surface) => new Vector2(surface.x, WaterY + FlightHeight);

    private IEnumerator Breach(Vector2 surface)
    {
        PlaySplash(surface, 0.55f);
        yield return new WaitForSeconds(warningSeconds);
        Vector2 bottom = surface - Vector2.up * 10f;
        serpent.BeginPath(bottom, Vector2.up);
        yield return MoveAlong(0.46f, progress => serpent.MoveNose(Vector2.Lerp(bottom, surface, progress)));
        PlaySplash(surface, 1.25f);
    }

    private IEnumerator Ascend(Vector2 surface, Vector2 sky)
    {
        return MoveAlong(AscentSeconds, progress =>
            serpent.MoveNose(Vector2.Lerp(surface, sky, progress * (2f - progress))));
    }

    private IEnumerator FlyWithHatch(float duration, Action<float> release)
    {
        GetAttackRoute(out Vector2 start, out Vector2 end);
        Vector2 skyStart = Above(start);
        Vector2 skyEnd = Above(end);
        yield return Breach(start);
        yield return Ascend(start, skyStart);

        yield return MoveAlong(duration, progress =>
        {
            MoveAcrossSky(skyStart, skyEnd, progress);
            serpent.SetHatchOpening(Mathf.Min(Mathf.InverseLerp(0.32f, 0.43f, progress),
                Mathf.InverseLerp(0.94f, 0.76f, progress)));
            if (progress >= 0.42f) release(progress);
        });
        serpent.CloseHatch();
        yield return MoveAlong(DescentSeconds, progress => MoveDown(skyEnd, end, progress));
        yield return Dive(end);
    }

    private void MoveAcrossSky(Vector2 start, Vector2 end, float progress)
    {
        serpent.MoveNose(Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, progress)));
    }

    private void MoveDown(Vector2 start, Vector2 end, float progress)
    {
        serpent.MoveNose(Vector2.Lerp(start, end, progress * progress));
    }

    private IEnumerator Dive(Vector2 start)
    {
        PlaySplash(start, 1.1f);
        Vector2 bottom = start - Vector2.up * (serpent.Span + 12f);
        yield return MoveAlong(1.1f, progress => MoveDown(start, bottom, progress));
        serpent.Hide();
    }

    private static IEnumerator MoveAlong(float duration, Action<float> sample)
    {
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            sample(time / duration);
            yield return null;
        }
        sample(1f);
    }

    private void BreakPlatform(Platform target)
    {
        if (!target.IsIntact) return;
        Vector2 center = target.Landing;
        SpriteRenderer source = target.surface.GetComponent<SpriteRenderer>();
        AtailPlatformBreak.Play(source, spawnedObjects.transform, platformBreakAnimation, serpent.HatchSortingOrder + 1);
        target.SetIntact(false);
        PlaySplash(center, 1.45f);
        if (playerCollider != null && Vector2.Distance(center, playerCollider.ClosestPoint(center)) < 2.05f)
            player.TakeDamage(smashDamage, center);
    }

    private IEnumerator DropPlatform(Platform platform, Vector2 origin)
    {
        SpriteRenderer source = platform.surface.GetComponent<SpriteRenderer>();
        SpriteRenderer replica = CopyPlatformSprite(source, "떨어지는 발판");
        replica.sortingOrder = Mathf.Max(replica.sortingOrder, serpent.HatchSortingOrder + 1);
        Vector3 fullScale = replica.transform.localScale;
        Vector2 end = source.transform.position;
        yield return MoveAlong(0.9f, progress =>
        {
            replica.transform.position = Vector2.Lerp(origin, end, progress * progress)
                + Vector2.up * (Mathf.Sin(Mathf.PI * progress) * 0.6f);
            replica.transform.localScale = fullScale * Mathf.Lerp(0.4f, 1f, Mathf.InverseLerp(0f, 0.55f, progress));
            replica.transform.rotation = source.transform.rotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(55f, 0f, progress));
        });
        platform.SetIntact(true);
        PlaySplash(platform.Landing, 0.65f);
        Destroy(replica.gameObject);
    }

    private SpriteRenderer CopyPlatformSprite(SpriteRenderer source, string name)
    {
        var replica = new GameObject(name).AddComponent<SpriteRenderer>();
        replica.transform.SetParent(spawnedObjects.transform, false);
        replica.transform.localScale = source.transform.lossyScale;
        replica.sprite = source.sprite;
        replica.sharedMaterial = source.sharedMaterial;
        replica.flipX = source.flipX;
        replica.flipY = source.flipY;
        replica.color = source.color;
        replica.sortingLayerID = source.sortingLayerID;
        replica.sortingOrder = source.sortingOrder + 4;
        var properties = new MaterialPropertyBlock();
        source.GetPropertyBlock(properties);
        replica.SetPropertyBlock(properties);
        return replica;
    }

    private IEnumerator LaunchFlying(Vector2 origin, Vector2 destination)
    {
        MonsterHealth monster = Instantiate(flyingPrefab, origin, Quaternion.identity, spawnedObjects.transform);
        summons.Add(monster);
        var chase = monster.GetComponent<NormalMonsterChase>();
        var attack = monster.GetComponent<FlyingMonsterAttack>();
        var body = monster.GetComponent<Rigidbody2D>();
        var sprite = monster.GetComponent<SpriteRenderer>();
        int sortingOrder = sprite.sortingOrder;
        sprite.sortingOrder = Mathf.Max(sortingOrder, serpent.HatchSortingOrder + 1);
        chase.enabled = false;
        attack.enabled = false;
        body.simulated = false;
        yield return MoveAlong(0.66f, progress => monster.transform.position = DropArc(origin, destination, progress));
        sprite.sortingOrder = sortingOrder;
        body.simulated = true;
        chase.BeginEncounterApproach(player.transform, null);
        chase.enabled = true;
        attack.SetProjectileParent(spawnedObjects.transform);
        attack.enabled = true;
    }

    private static Vector2 DropArc(Vector2 start, Vector2 end, float progress)
    {
        return Vector2.Lerp(start, end, progress) + Vector2.up * (Mathf.Sin(Mathf.PI * progress) * 2.0f);
    }

    private void FireMissile(Vector2 origin)
    {
        Vector2 aim = (PlayerCenter - origin).normalized;
        if (aim == Vector2.zero) aim = Vector2.down;
        float angle = UnityEngine.Random.Range(-MissileSpreadDegrees, MissileSpreadDegrees);
        Vector2 direction = Quaternion.Euler(0f, 0f, angle) * aim;
        EnemyProjectile2D missile = SpawnMissile(origin);
        missile.Launch(direction, missileSpeed, 8f, missileDamage, player);
    }

    private EnemyProjectile2D SpawnMissile(Vector2 origin)
    {
        EnemyProjectile2D missile = Instantiate(missilePrefab, origin, Quaternion.identity, spawnedObjects.transform);
        missile.ShotDown += TryDropMissileReward;
        return missile;
    }

    private void PlaySplash(Vector2 position, float scale)
    {
        if (splashEffect == null) return;
        ParticleSystem effect = Instantiate(splashEffect, position, Quaternion.identity, spawnedObjects.transform);
        effect.transform.localScale *= scale;
        effect.gameObject.SetActive(true);
        effect.Play();
        Destroy(effect.gameObject, 1.4f);
    }
}
