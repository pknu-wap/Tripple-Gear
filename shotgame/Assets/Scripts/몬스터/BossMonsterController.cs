using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossAttackPattern { MissileRain, HorizontalMissiles, SweepingLaser, Summon }

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth))]
public sealed class BossMonsterController : MonoBehaviour
{
    [Header("연결")]
    [SerializeField, KoreanLabel("패턴 데이터")] private BossAttackSO attackData;
    [SerializeField, KoreanLabel("플레이어")] private ArisaHealth player;
    [SerializeField, KoreanLabel("레이저 발사점")] private Transform laserOrigin;
    [SerializeField, KoreanLabel("선 머티리얼")] private Material lineMaterial;
    [SerializeField, KoreanLabel("자동 전투 시작")] private bool autoStart = true;

    [Header("보스 발밑 기준 전투 영역")]
    [SerializeField, KoreanLabel("영역 최소 좌표")] private Vector2 arenaMin = new Vector2(-17f, 0f);
    [SerializeField, KoreanLabel("영역 최대 좌표")] private Vector2 arenaMax = new Vector2(-2f, 7f);

    private readonly List<MonsterHealth> summonedMonsters = new List<MonsterHealth>();
    private readonly RaycastHit2D[] laserHits = new RaycastHit2D[32];
    private MonsterHealth health;
    private GameObject combatObjects;
    private bool battleRunning;
    private bool encounterEnded;
    private int lastPattern = -1;

    public BossAttackSO AttackData => attackData;
    public bool IsAttacking { get; private set; }
    public BossAttackPattern CurrentPattern { get; private set; }
    public string Status { get; private set; } = "대기";
    public int LastMissileCount { get; private set; }
    public int LivingSummonCount
    {
        get
        {
            summonedMonsters.RemoveAll(monster => monster == null || monster.IsDead);
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
        yield return new WaitForSeconds(attackData.InitialDelay);
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
            Status = "미사일 낙하 예고";
            LineRenderer warning = CreateLine("낙하 예고", new Vector2(x, area.yMin + 0.05f),
                new Vector2(x, area.yMax), new Color(1f, 0.2f, 0.1f, 0.45f), 0.06f);
            yield return new WaitForSeconds(attackData.WarningTime);
            Destroy(warning.gameObject);
            Status = "미사일 낙하";
            SpawnMissile(new Vector2(x, area.yMax + 0.7f), Vector2.down, attackData.RainSpeed);
            yield return new WaitForSeconds(attackData.MissileInterval);
        }
    }

    private IEnumerator HorizontalMissiles()
    {
        Rect area = ArenaBounds;
        LastMissileCount = Random.Range(attackData.HorizontalMinCount, attackData.HorizontalMaxCount + 1);
        for (int i = 0; i < LastMissileCount; i++)
        {
            float y = Random.Range(area.yMin + 0.55f, area.yMax - 0.6f);
            Status = "가로 미사일 예고";
            LineRenderer warning = CreateLine("가로 발사 예고", new Vector2(area.xMax + 0.8f, y),
                new Vector2(area.xMax - 1.2f, y), new Color(1f, 0.5f, 0.1f, 0.65f), 0.07f);
            yield return new WaitForSeconds(attackData.WarningTime);
            Destroy(warning.gameObject);
            Status = "가로 미사일";
            SpawnMissile(new Vector2(area.xMax + 0.8f, y), Vector2.left, attackData.HorizontalSpeed);
            yield return new WaitForSeconds(attackData.MissileInterval);
        }
    }

    private void SpawnMissile(Vector2 position, Vector2 direction, float speed)
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
        missile.AddComponent<EnemyProjectile2D>().Launch(direction, speed, attackData.MissileDamage,
            attackData.GroundLayers, 0.14f, 14f, attackData.ExplosionPrefab,
            attackData.ExplosionRadius, attackData.ExplosionLifetime, attackData.ExplosionScale);
    }

    private IEnumerator SweepingLaser()
    {
        Rect area = ArenaBounds;
        Vector2 origin = laserOrigin.position;
        Vector2 start = new Vector2(area.xMin, origin.y);
        Vector2 end = new Vector2(area.xMin, area.yMax);
        Status = "레이저 예고";
        LineRenderer startWarning = CreateLine("레이저 시작선", origin, start, new Color(1f, 0.1f, 0.1f, 0.7f), 0.06f);
        LineRenderer endWarning = CreateLine("레이저 종료선", origin, end, new Color(0.3f, 1f, 0.3f, 0.45f), 0.04f);
        yield return new WaitForSeconds(attackData.WarningTime);
        Destroy(startWarning.gameObject);
        Destroy(endWarning.gameObject);

        GameObject laser = Instantiate(attackData.LaserPrefab, origin, Quaternion.identity, combatObjects.transform);
        laser.name = "보스 무지개 레이저";
        SpriteRenderer sprite = laser.GetComponentInChildren<SpriteRenderer>();
        // 지정 프리팹의 광선 모양 위에 무지개 그라데이션을 겹친다.
        LineRenderer rainbow = CreateLine("무지개 그라데이션", origin, start, Color.white, attackData.LaserWidth);
        rainbow.colorGradient = CreateRainbowGradient();
        rainbow.positionCount = 32;
        rainbow.sortingOrder = 32;
        Status = "레이저 발사";
        float elapsed = 0f;
        float nextHitAt = 0f;
        float startAngle = Mathf.Atan2(start.y - origin.y, start.x - origin.x) * Mathf.Rad2Deg;
        float endAngle = Mathf.Atan2(end.y - origin.y, end.x - origin.x) * Mathf.Rad2Deg;
        while (elapsed < attackData.LaserDuration)
        {
            float t = Mathf.Clamp01(elapsed / attackData.LaserDuration);
            float angle = Mathf.LerpAngle(startAngle, endAngle, t);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float length = (origin.x - area.xMin) / Mathf.Max(0.01f, -direction.x);
            Vector2 tip = origin + direction * length;
            for (int vertex = 0; vertex < rainbow.positionCount; vertex++)
                rainbow.SetPosition(vertex, Vector2.Lerp(origin, tip, vertex / (float)(rainbow.positionCount - 1)));
            if (sprite != null && sprite.sprite != null)
            {
                Vector2 size = sprite.sprite.bounds.size;
                laser.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                laser.transform.localScale = new Vector3(length / Mathf.Max(0.01f, size.x),
                    attackData.LaserWidth * 1.25f / Mathf.Max(0.01f, size.y), 1f);
                laser.transform.position = (Vector3)(origin + direction * length * 0.5f);
                sprite.color = Color.HSVToRGB(t, 0.8f, 1f);
                sprite.sortingOrder = 31;
            }
            if (Time.time >= nextHitAt && DamageAlongLaser(origin, direction, length))
                nextHitAt = Time.time + attackData.LaserHitInterval;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(laser);
        Destroy(rainbow.gameObject);
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
        if (!CanSummon) yield break;
        Rect area = ArenaBounds;
        Status = "원거리 몹 소환 예고";
        List<Vector2> positions = new List<Vector2>();
        List<GameObject> warnings = new List<GameObject>();
        for (int i = 0; i < attackData.SummonCount; i++)
        {
            Vector2 position = new Vector2(Mathf.Lerp(area.xMin + 1f, area.xMax - 1f,
                (i + 0.5f) / attackData.SummonCount), area.yMax - 0.8f);
            positions.Add(position);
            warnings.Add(CreateLine("소환 예고", position + Vector2.left * 0.5f,
                position + Vector2.right * 0.5f, Color.cyan, 0.15f).gameObject);
        }
        yield return new WaitForSeconds(attackData.WarningTime);
        foreach (GameObject warning in warnings) Destroy(warning);
        foreach (Vector2 position in positions)
        {
            GameObject monster = Instantiate(attackData.RangedMonsterPrefab, position, Quaternion.identity, combatObjects.transform);
            MonsterHealth monsterHealth = monster.GetComponent<MonsterHealth>();
            if (monsterHealth != null) summonedMonsters.Add(monsterHealth);
            RangedMonsterAttack ranged = monster.GetComponent<RangedMonsterAttack>();
            if (ranged != null) ranged.Initialize(player, combatObjects.transform);
        }
        Status = "원거리 몹 소환";
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

    private static Gradient CreateRainbowGradient()
    {
        Gradient gradient = new Gradient();
        GradientColorKey[] colors = new GradientColorKey[7];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = new GradientColorKey(Color.HSVToRGB(i / 7f, 0.9f, 1f), i / 6f);
        gradient.SetKeys(colors, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    private void HandleDefeated()
    {
        EndEncounter("보스 처치");
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>()) renderer.color = new Color(0.35f, 0.35f, 0.35f);
    }

    private void EndEncounter(string status)
    {
        encounterEnded = true;
        battleRunning = false;
        IsAttacking = false;
        Status = status;
        StopAllCoroutines();
        if (combatObjects != null)
        {
            combatObjects.SetActive(false);
            Destroy(combatObjects);
        }
        summonedMonsters.Clear();
    }

    private void OnDisable()
    {
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
