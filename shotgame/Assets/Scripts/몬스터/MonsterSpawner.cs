using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth), typeof(MonsterDeathEffect))]
public sealed class MonsterSpawner : MonoBehaviour
{
    private const double InitialSpawnDelay = 1d;
    private const float SummonChargeTime = 1f / 3f;
    private const float SpawnEventGrace = 0.12f;
    private static readonly int SummonState = Animator.StringToHash("Base Layer.Summon");

    [SerializeField, KoreanLabel("소환할 몬스터 프리팹")] private MonsterHealth monsterPrefab;
    [SerializeField, KoreanLabel("한 번에 소환할 마릿수"), Min(1)]
    private int monstersPerBatch = 3;
    [SerializeField, KoreanLabel("몬스터 사이 소환 간격 (초)"), Min(0f)]
    private float spawnInterval = 0.5f;
    [SerializeField, KoreanLabel("다음 소환 묶음까지 대기 (초)"), Min(0f)]
    private float delayBetweenBatches = 5f;

    private MonsterHealth health;
    private MonsterGroundMotion groundMotion;
    private Animator summonAnimator;
    private bool hasEnteredCamera;
    private bool waitingForSpawnEvent;
    private double nextSpawnTime;
    private float chargeWindow = SummonChargeTime;
    private int remainingInBatch;

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
        summonAnimator = GetComponent<Animator>();
        if (TryGetComponent(out BoxCollider2D body)) groundMotion = new MonsterGroundMotion(body);
    }

    private void OnEnable() => groundMotion?.Reset();

    private void FixedUpdate()
    {
        if (!health.IsDead && !health.IsEntering) groundMotion?.Step();
    }

    private void LateUpdate()
    {
        if (health.IsDead || health.IsEntering || monsterPrefab == null)
        {
            return;
        }

        double now = Time.timeAsDouble;
        if (!hasEnteredCamera)
        {
            if (IsInsideCameraView(Camera.main))
            {
                hasEnteredCamera = true;
                nextSpawnTime = now + InitialSpawnDelay;
            }
            return;
        }

        if (waitingForSpawnEvent)
        {
            if (now >= nextSpawnTime + SpawnEventGrace)
            {
                CompleteSpawn(now);
            }
            return;
        }

        if (chargeWindow > 0f && summonAnimator != null && summonAnimator.isActiveAndEnabled
            && summonAnimator.runtimeAnimatorController != null && summonAnimator.HasState(0, SummonState)
            && now < nextSpawnTime)
        {
            double timeUntilSpawn = nextSpawnTime - now;
            if (timeUntilSpawn <= chargeWindow)
            {
                waitingForSpawnEvent = true;
                summonAnimator.speed = SummonChargeTime / Mathf.Max(0.001f, (float)timeUntilSpawn);
                summonAnimator.Play(SummonState, 0, 0f);
            }
            return;
        }

        if (now >= nextSpawnTime)
        {
            CompleteSpawn(now);
        }
    }

    public void SpawnFromAnimation()
    {
        if (waitingForSpawnEvent && isActiveAndEnabled && !health.IsDead && !health.IsEntering && monsterPrefab != null)
        {
            CompleteSpawn(Time.timeAsDouble);
        }
    }

    private void CompleteSpawn(double now)
    {
        waitingForSpawnEvent = false;
        if (remainingInBatch == 0)
        {
            remainingInBatch = Mathf.Max(1, monstersPerBatch);
        }

        SpawnMonster();
        remainingInBatch--;
        float delay = Mathf.Max(0f, remainingInBatch > 0 ? spawnInterval : delayBetweenBatches);
        nextSpawnTime = now + delay;
        chargeWindow = Mathf.Min(SummonChargeTime, delay * (2f / 3f));
    }

    private bool IsInsideCameraView(Camera battleCamera)
    {
        if (battleCamera == null || !battleCamera.isActiveAndEnabled)
        {
            return false;
        }

        Vector3 viewport = battleCamera.WorldToViewportPoint(transform.position);
        return viewport.z >= battleCamera.nearClipPlane && viewport.z <= battleCamera.farClipPlane
            && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    private void SpawnMonster()
    {
        MonsterHealth spawnedHealth = Instantiate(monsterPrefab, transform.position, monsterPrefab.transform.rotation);
        GameObject spawnedMonster = spawnedHealth.gameObject;
        spawnedHealth.Defeated.AddListener(() => Destroy(spawnedMonster));
        spawnedMonster.SetActive(true);
    }
}
