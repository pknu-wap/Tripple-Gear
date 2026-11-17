using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth), typeof(MonsterDeathEffect))]
public sealed class MonsterSpawner : MonoBehaviour
{
    private const double InitialSpawnDelay = 1d;

    [SerializeField, KoreanLabel("소환할 몬스터 프리팹")] private MonsterHealth monsterPrefab;
    [SerializeField, KoreanLabel("한 번에 소환할 마릿수"), Min(1)]
    private int monstersPerBatch = 3;
    [SerializeField, KoreanLabel("몬스터 사이 소환 간격 (초)"), Min(0f)]
    private float spawnInterval = 0.5f;
    [SerializeField, KoreanLabel("다음 소환 묶음까지 대기 (초)"), Min(0f)]
    private float delayBetweenBatches = 5f;

    private MonsterHealth health;
    private bool hasEnteredCamera;
    private double nextSpawnTime;
    private int remainingInBatch;

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
    }

    private void LateUpdate()
    {
        if (health.IsDead || monsterPrefab == null)
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

        if (now < nextSpawnTime)
        {
            return;
        }

        if (remainingInBatch == 0)
        {
            remainingInBatch = Mathf.Max(1, monstersPerBatch);
        }

        SpawnMonster();
        remainingInBatch--;
        float delay = remainingInBatch > 0 ? spawnInterval : delayBetweenBatches;
        nextSpawnTime = now + Mathf.Max(0f, delay);
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
