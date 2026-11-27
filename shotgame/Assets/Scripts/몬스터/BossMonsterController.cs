using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossAttackPattern { MissileRain, HorizontalMissiles, SweepingLaser, Summon }

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth))]
public sealed class BossMonsterController : MonoBehaviour
{
    private const float DefeatExplosionDuration = 4f;

    [Header("연결")]
    [SerializeField, KoreanLabel("패턴 데이터")] private BossAttackSO attackData;
    [SerializeField, KoreanLabel("플레이어")] private ArisaHealth player;
    [SerializeField, KoreanLabel("레이저 발사점")] private Transform laserOrigin;
    [SerializeField, KoreanLabel("공격 모션")] private BossAttackVisual attackVisual;
    [SerializeField, KoreanLabel("선 머티리얼")] private Material lineMaterial;
    [SerializeField, KoreanLabel("자동 전투 시작")] private bool autoStart = true;
    [SerializeField, KoreanLabel("처치 후 텔레포트")] private TeleportTrigger defeatTeleport;

    [Header("처치 연출")]
    [SerializeField, KoreanLabel("처치 폭발 효과")] private GameObject defeatExplosion;
    [SerializeField, KoreanLabel("부서진 보스 스프라이트")] private Sprite wreckSprite;

    [Header("보스 발밑 기준 전투 영역")]
    [SerializeField, KoreanLabel("영역 최소 좌표")] private Vector2 arenaMin = new Vector2(-17f, 0f);
    [SerializeField, KoreanLabel("영역 최대 좌표")] private Vector2 arenaMax = new Vector2(-2f, 7f);

    private readonly List<GameObject> summonedMonsters = new List<GameObject>();
    private readonly RaycastHit2D[] laserHits = new RaycastHit2D[32];
    private MonsterHealth health;
    private GameObject combatObjects;
    private bool battleRunning;
    private bool encounterEnded;
    private bool defeatSequenceStarted;
    private int lastPattern = -1;

    public BossAttackSO AttackData => attackData;
    public bool IsAttacking { get; private set; }
    internal bool IsPlayingDefeat { get; private set; }
    public BossAttackPattern CurrentPattern { get; private set; }
    public string Status { get; private set; } = "대기";
    public int LastMissileCount { get; private set; }
    public int LivingSummonCount
    {
        get
        {
            summonedMonsters.RemoveAll(monster => monster == null
                || (monster.TryGetComponent<MonsterHealth>(out var monsterHealth) && monsterHealth.IsDead));
            return summonedMonsters.Count;
        }
    }
    public bool CanSummon => attackData != null && attackData.RangedMonsterPrefab != null && LivingSummonCount == 0;
    public Rect ArenaBounds
    {
        get
        {
            Vector2 min = Vector2.Min(arenaMin, arenaMax) + (Vector2)transform.position;
            Vector2 max = Vector2.Max(arenaMin, arenaMax) + (Vector2)transform.position;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
        health.Defeated.AddListener(HandleDefeated);
    }

    private void Start()
    {
        if (autoStart) StartBattle();
    }

    private void Update()
    {
        if (!encounterEnded && player != null && player.IsDead && (battleRunning || combatObjects != null))
            EndEncounter("플레이어 쓰러짐");
    }

    public void StartBattle()
    {
        if (battleRunning || encounterEnded || health.IsDead || attackData == null) return;
        battleRunning = true;
        StartCoroutine(BattleLoop());
    }

    // 테스트 씬이나 연출 이벤트에서도 개별 패턴을 실행할 수 있다.
    public bool TryStartPattern(BossAttackPattern pattern)
    {
        if (!isActiveAndEnabled || encounterEnded || health.IsDead || attackData == null || IsAttacking) return false;
        if (player == null) player = FindFirstObjectByType<ArisaHealth>();
        if (player == null || player.IsDead || !IsPatternAvailable(pattern)) return false;
        StartCoroutine(ExecutePattern(pattern));
        return true;
    }

    private IEnumerator BattleLoop()
    {
        Status = "전투 준비";
        while (!encounterEnded && !health.IsDead)
        {
            if (player == null) player = FindFirstObjectByType<ArisaHealth>();
            if (player == null || IsAttacking)
            {
                yield return new WaitForSeconds(0.2f);
                continue;
            }
            if (player.IsDead) { EndEncounter("플레이어 쓰러짐"); yield break; }
            List<BossAttackPattern> available = new List<BossAttackPattern>(4);
            for (int i = 0; i < 4; i++)
                if (i != lastPattern && IsPatternAvailable((BossAttackPattern)i)) available.Add((BossAttackPattern)i);
            if (available.Count == 0)
            {
                for (int i = 0; i < 4; i++)
                    if (IsPatternAvailable((BossAttackPattern)i)) available.Add((BossAttackPattern)i);
            }
            if (available.Count == 0) { Status = "패턴 프리팹을 연결해 주세요"; yield break; }
            BossAttackPattern pattern = available[Random.Range(0, available.Count)];
            yield return ExecutePattern(pattern);
            Status = "재정비";
            yield return new WaitForSeconds(attackData.RecoveryTime);
        }
    }

    private bool IsPatternAvailable(BossAttackPattern pattern)
    {
        switch (pattern)
        {
            case BossAttackPattern.MissileRain:
            case BossAttackPattern.HorizontalMissiles: return attackData.MissilePrefab != null;
            case BossAttackPattern.SweepingLaser: return attackData.LaserPrefab != null && laserOrigin != null;
            case BossAttackPattern.Summon: return CanSummon;
            default: return false;
        }
    }

    private IEnumerator ExecutePattern(BossAttackPattern pattern)
    {
        EnsureCombatRoot();
        IsAttacking = true;
        CurrentPattern = pattern;
        lastPattern = (int)pattern;
        switch (pattern)
        {
            case BossAttackPattern.MissileRain: yield return MissileRain(); break;
            case BossAttackPattern.HorizontalMissiles: yield return HorizontalMissiles(); break;
            case BossAttackPattern.SweepingLaser: yield return SweepingLaser(); break;
            case BossAttackPattern.Summon: yield return Summon(); break;
        }
        IsAttacking = false;
        Status = "재정비";
    }

    private IEnumerator MissileRain()
    {
        Rect area = ArenaBounds;
        LastMissileCount = Random.Range(attackData.RainMinCount, attackData.RainMaxCount + 1);
        for (int i = 0; i < LastMissileCount; i++)
        {
            float x = Random.Range(area.xMin + 0.6f, area.xMax - 0.6f);
            Status = "낙하 미사일 예고";
            yield return AnimateOpening(BossAttackPattern.MissileRain, true, attackData.WarningTime);
            Status = "미사일 낙하";
            Vector2 dropPoint = new Vector2(x, area.yMax + 0.7f);
            if (attackVisual != null) StartCoroutine(LaunchRisingMissile(dropPoint));
            else SpawnMissile(dropPoint, Vector2.down, attackData.RainSpeed);
            yield return AnimateOpening(BossAttackPattern.MissileRain, false, attackData.MissileInterval);
            attackVisual?.ResetPose();
        }
    }

    private IEnumerator HorizontalMissiles()
    {
        Rect area = ArenaBounds;
        LastMissileCount = Random.Range(attackData.HorizontalMinCount, attackData.HorizontalMaxCount + 1);
        int[] lanes = new int[LastMissileCount];
        for (int i = 0; i < lanes.Length; i++) lanes[i] = i;
        float low = Mathf.Min(area.yMin + 0.55f, area.center.y);
        float high = Mathf.Max(area.yMax - 0.6f, area.center.y);
        for (int i = 0; i < LastMissileCount; i++)
        {
            int choice = Random.Range(i, lanes.Length);
            (lanes[i], lanes[choice]) = (lanes[choice], lanes[i]);
            float height = Mathf.Lerp(low, high, (lanes[i] + Random.Range(0.2f, 0.8f)) / lanes.Length);
            Status = "가로 미사일 예고";
            yield return AnimateOpening(BossAttackPattern.HorizontalMissiles, true, attackData.WarningTime);
            Status = "가로 미사일";
            if (attackVisual != null) StartCoroutine(LaunchChestMissile(area.xMax, height));
            else SpawnMissile(new Vector2(area.xMax + 0.8f, height), Vector2.left, attackData.HorizontalSpeed);
            yield return AnimateOpening(BossAttackPattern.HorizontalMissiles, false, attackData.MissileInterval);
            attackVisual?.ResetPose();
        }
    }

    private IEnumerator LaunchChestMissile(float arenaRight, float height)
    {
        Vector2 start = attackVisual.ChestMuzzle;
        Vector2 end = new Vector2(Mathf.Min(start.x - 0.65f, arenaRight + 0.8f), height);
        GameObject missile = CreateMissile(start);
        missile.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        float duration = Mathf.Clamp(Vector2.Distance(start, end) / 12f, 0.17f, 0.45f);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (missile == null) yield break;
            float progress = elapsed / duration;
            Vector2 position = new Vector2(Mathf.Lerp(start.x, end.x, progress),
                Mathf.SmoothStep(start.y, end.y, progress));
            float verticalTangent = (end.y - start.y) * (6f * progress * (1f - progress));
            float angle = Mathf.Atan2(verticalTangent, end.x - start.x) * Mathf.Rad2Deg;
            missile.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            yield return null;
        }

        if (missile == null) yield break;
        missile.transform.position = end;
        ArmMissile(missile, Vector2.left, attackData.HorizontalSpeed);
    }

    private IEnumerator AnimateOpening(BossAttackPattern pattern, bool opening, float duration)
    {
        if (attackVisual == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float motionDuration = Mathf.Min(duration, 0.32f);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / motionDuration);
            attackVisual.ShowPose(pattern, opening ? progress : 1f - progress);
            yield return null;
        }
        attackVisual.ShowPose(pattern, opening ? 1f : 0f);
    }

    private IEnumerator LaunchRisingMissile(Vector2 dropPoint)
    {
        Vector2 start = attackVisual.RainMuzzle;
        GameObject missile = CreateMissile(start);
        missile.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        Camera cameraToUse = Camera.main;
        float top = cameraToUse != null && cameraToUse.orthographic
            ? cameraToUse.transform.position.y + cameraToUse.orthographicSize + attackData.MissileWidth * 0.5f
            : dropPoint.y;
        Vector2 end = new Vector2(start.x, Mathf.Max(start.y + 1.25f, top));
        const float riseDuration = 0.38f;
        for (float elapsed = 0f; elapsed < riseDuration; elapsed += Time.deltaTime)
        {
            if (missile == null) yield break;
            missile.transform.position = Vector2.Lerp(start, end, elapsed / riseDuration);
            yield return null;
        }

        if (missile != null)
        {
            missile.transform.position = dropPoint;
            ArmMissile(missile, Vector2.down, attackData.RainSpeed);
        }
    }

    private void SpawnMissile(Vector2 position, Vector2 direction, float speed)
    {
        ArmMissile(CreateMissile(position), direction, speed);
    }

    private GameObject CreateMissile(Vector2 position)
    {
        GameObject missile = Instantiate(attackData.MissilePrefab, position, Quaternion.identity, combatObjects.transform);
        missile.name = "보스 미사일";
        SpriteRenderer renderer = missile.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null)
        {
            float currentWidth = renderer.bounds.size.x;
            missile.transform.localScale *= attackData.MissileWidth / Mathf.Max(0.01f, currentWidth);
            renderer.sortingOrder = 25;
        }
        return missile;
    }

    private void ArmMissile(GameObject missile, Vector2 direction, float speed)
    {
        GameplaySfx.Play(GameplaySfx.Cue.Missile, .46f);
        if (!missile.TryGetComponent(out EnemyProjectile2D projectile))
            projectile = missile.AddComponent<EnemyProjectile2D>();
        LayerMask missileGroundLayers = attackData.GroundLayers.value & ~LayerMask.GetMask("Stair");
        projectile.Launch(direction, speed, attackData.MissileDamage,
            missileGroundLayers, 0.14f, 14f, attackData.ExplosionPrefab,
            attackData.ExplosionRadius, attackData.ExplosionLifetime, attackData.ExplosionScale);
    }

    private IEnumerator SweepingLaser()
    {
        Rect area = ArenaBounds;
        Vector2 origin = laserOrigin.position;
        Vector2 start = new Vector2(area.xMin, origin.y);
        Vector2 end = new Vector2(area.xMin, area.yMax);
        Status = "레이저 예고";
        LineRenderer startWarning = CreateLine("레이저 수평 예고선", origin, start,
            new Color(1f, 0.26f, 0.11f, 0.85f), attackData.LaserWidth * 0.35f);
        LineRenderer endWarning = CreateLine("레이저 회전 끝 예고선", origin, end,
            new Color(1f, 0.68f, 0.24f, 0.68f), attackData.LaserWidth * 0.25f);
        yield return AnimateLaserCharge(0f, 1f, attackData.WarningTime);
        startWarning.enabled = false;
        endWarning.enabled = false;
        Destroy(startWarning.gameObject);
        Destroy(endWarning.gameObject);

        GameObject laser = Instantiate(attackData.LaserPrefab, origin, Quaternion.identity, combatObjects.transform);
        laser.name = "보스 레이저";
        SpriteRenderer sprite = laser.GetComponentInChildren<SpriteRenderer>();
        LineRenderer core = CreateLine("레이저 중심선", origin, start, new Color(1f, 0.83f, 0.48f),
            attackData.LaserWidth * 0.45f);
        core.sortingOrder = 32;
        Status = "레이저 발사";
        GameplaySfx.Play(GameplaySfx.Cue.Laser, 0.60f);
        float elapsed = 0f;
        float nextHitAt = 0f;
        float startAngle = Mathf.Atan2(start.y - origin.y, start.x - origin.x) * Mathf.Rad2Deg;
        float endAngle = Mathf.Atan2(end.y - origin.y, end.x - origin.x) * Mathf.Rad2Deg;
        while (elapsed < attackData.LaserDuration)
        {
            float t = Mathf.Clamp01(elapsed / attackData.LaserDuration);
            float angle = Mathf.LerpAngle(startAngle, endAngle, Mathf.SmoothStep(0f, 1f, t));
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float length = (origin.x - area.xMin) / Mathf.Max(0.01f, -direction.x);
            Vector2 tip = origin + direction * length;
            core.SetPosition(1, tip);
            if (sprite != null && sprite.sprite != null)
            {
                Vector2 size = sprite.sprite.bounds.size;
                laser.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                laser.transform.localScale = new Vector3(length / Mathf.Max(0.01f, size.x),
                    attackData.LaserWidth * 1.25f / Mathf.Max(0.01f, size.y), 1f);
                laser.transform.position = (Vector3)(origin + direction * length * 0.5f);
                sprite.color = new Color(1f, 0.25f, 0.08f);
                sprite.sortingOrder = 31;
            }
            if (Time.time >= nextHitAt && DamageAlongLaser(origin, direction, length))
                nextHitAt = Time.time + attackData.LaserHitInterval;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(laser);
        Destroy(core.gameObject);
        yield return AnimateLaserCharge(1f, 0f, 0.35f);
    }

    private IEnumerator AnimateLaserCharge(float start, float end, float duration)
    {
        if (end > start) GameplaySfx.Play(GameplaySfx.Cue.Warning, 0.45f);
        if (attackVisual == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            attackVisual.SetLaserCharge(Mathf.SmoothStep(start, end, elapsed / duration));
            yield return null;
        }
        attackVisual.SetLaserCharge(end);
    }

    private bool DamageAlongLaser(Vector2 origin, Vector2 direction, float length)
    {
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        int count = Physics2D.CircleCast(origin, attackData.LaserWidth * 0.5f, direction, filter, laserHits, length);
        for (int i = 0; i < count; i++)
        {
            ArisaHealth target = laserHits[i].collider.GetComponentInParent<ArisaHealth>();
            if (target == null || target.IsDead) continue;
            target.TakeDamage(attackData.LaserDamage);
            return true;
        }
        return false;
    }

    private IEnumerator Summon()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Energy, .49f);
        if (!CanSummon) yield break;
        Rect area = ArenaBounds;
        Status = "원거리 몹 소환 예고";
        yield return AnimateOpening(BossAttackPattern.Summon, true, attackData.WarningTime);
        Status = "원거리 몹 소환";
        for (int i = 0; i < attackData.SummonCount; i++)
        {
            Vector2 position = new Vector2(Mathf.Lerp(area.xMin + 1f, area.xMax - 1f,
                (i + 0.5f) / attackData.SummonCount), area.yMax - 0.8f);
            GameObject monster = Instantiate(attackData.RangedMonsterPrefab, position, Quaternion.identity, combatObjects.transform);
            summonedMonsters.Add(monster);
            if (attackVisual != null)
            {
                StartCoroutine(LaunchSummon(monster, position));
                yield return new WaitForSeconds(0.28f);
            }
        }

        if (attackVisual != null)
        {
            yield return AnimateOpening(BossAttackPattern.Summon, false, 0.38f);
            attackVisual.ResetPose();
        }
    }

    private IEnumerator LaunchSummon(GameObject monster, Vector2 destination)
    {
        NormalMonsterChase chase = monster.GetComponent<NormalMonsterChase>();
        FlyingMonsterAttack attack = monster.GetComponent<FlyingMonsterAttack>();
        Rigidbody2D body = monster.GetComponent<Rigidbody2D>();
        SpriteRenderer renderer = monster.GetComponentInChildren<SpriteRenderer>();
        int sortingLayer = renderer != null ? renderer.sortingLayerID : 0;
        int sortingOrder = renderer != null ? renderer.sortingOrder : 0;
        attackVisual.ShowInFrontOfBody(renderer);
        bool resumeChase = chase != null && chase.enabled;
        bool resumeAttack = attack != null && attack.enabled;
        bool resumePhysics = body != null && body.simulated;
        if (resumeChase) chase.enabled = false;
        if (resumeAttack) attack.enabled = false;
        if (resumePhysics) body.simulated = false;
        Vector2 start = attackVisual.HangarExit;
        monster.transform.position = start;

        float duration = Mathf.Clamp(Vector2.Distance(start, destination) / 15f, 0.32f, 1f);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (monster == null) yield break;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            monster.transform.position = Vector2.Lerp(start, destination, t);
            yield return null;
        }

        if (monster == null) yield break;
        monster.transform.position = destination;
        if (renderer != null)
        {
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = sortingOrder;
        }
        if (resumePhysics) body.simulated = true;
        if (resumeChase) chase.enabled = true;
        if (resumeAttack) attack.enabled = true;
    }

    private void EnsureCombatRoot()
    {
        if (combatObjects == null) combatObjects = new GameObject(name + " 전투 오브젝트");
    }

    private LineRenderer CreateLine(string objectName, Vector2 from, Vector2 to, Color color, float width)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(combatObjects.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.numCapVertices = 4;
        line.sortingOrder = 30;
        return line;
    }

    private void HandleDefeated() => BeginDefeatSequence();

    internal void BeginDefeatSequence()
    {
        if (defeatSequenceStarted || !isActiveAndEnabled) return;
        defeatSequenceStarted = true;
        EndEncounter("보스 처치");
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        IsPlayingDefeat = true;
        StartCoroutine(PlayDefeatSequence());
    }

    private IEnumerator PlayDefeatSequence()
    {
        SpriteRenderer body = attackVisual != null ? attackVisual.Body : null;
        GameObject lastBurst = null;
        float nextBurst = 0f;
        for (float elapsed = 0f; elapsed < DefeatExplosionDuration; elapsed += Time.deltaTime)
        {
            if (elapsed >= nextBurst && defeatExplosion != null && body != null)
            {
                Bounds bounds = body.bounds;
                Vector3 position = bounds.center + new Vector3(
                    Random.Range(-0.55f, 0.55f) * bounds.size.x,
                    Random.Range(-0.46f, 0.46f) * bounds.size.y);
                GameObject burst = Instantiate(defeatExplosion, position, Quaternion.identity, transform);
                burst.name = "크로코 처치 폭발";
                burst.transform.localScale *= Random.Range(0.70f, 1.25f)
                    * Mathf.Lerp(1f, 1.35f, elapsed / DefeatExplosionDuration);
                attackVisual.ShowInFrontOfBody(burst.GetComponentInChildren<SpriteRenderer>());
                Destroy(burst, 1.2f);
                lastBurst = burst;
                nextBurst = elapsed + Random.Range(0.14f, 0.25f);
            }
            yield return null;
        }

        if (attackVisual != null) attackVisual.ShowWreck(wreckSprite);
        while (lastBurst != null) yield return null;
        IsPlayingDefeat = false;
        if (defeatTeleport != null) defeatTeleport.TeleportPlayer();
    }

    private void EndEncounter(string status)
    {
        encounterEnded = true;
        battleRunning = false;
        IsAttacking = false;
        Status = status;
        StopAllCoroutines();
        attackVisual?.ResetPose();
        if (combatObjects != null)
        {
            combatObjects.SetActive(false);
            Destroy(combatObjects);
        }
        summonedMonsters.Clear();
    }

    private void OnDisable()
    {
        IsPlayingDefeat = false;
        EndEncounter(health != null && health.IsDead ? "보스 처치" : "전투 종료");
    }

    private void OnDestroy()
    {
        if (health != null) health.Defeated.RemoveListener(HandleDefeated);
    }

    private void OnDrawGizmosSelected()
    {
        Rect area = ArenaBounds;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
