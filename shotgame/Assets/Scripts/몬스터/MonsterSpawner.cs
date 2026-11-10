using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth), typeof(MonsterDeathEffect))]
public sealed class MonsterSpawner : MonoBehaviour
{
    private const double BatchInterval = 5d;
    private const double MaxSpawnGap = 0.35d;

    [SerializeField, KoreanLabel("소환할 몬스터 프리팹")] private MonsterHealth monsterPrefab;
    [SerializeField, KoreanLabel("한 번에 소환할 수"), Min(1)] private int monstersPerBatch = 3;
    [SerializeField, KoreanLabel("소환 위치 보정")] private Vector2 spawnOffset = new Vector2(-2f, 0f);
    [SerializeField, KoreanLabel("웨이브 매니저 (선택)")] private WaveManager waveManager;
        [SerializeField, KoreanLabel("소환 횟수")] private int spawnCount = 2;


    private MonsterHealth health;
    private bool hasStartedBattle;
    private double nextBatchTime;
    private double nextSpawnTime;
    private double spawnGap;
    private int remainingInBatch;


    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
    }

    private void LateUpdate()
    {
        if (health.IsDead)
        {
            return;
        }

        double now = Time.timeAsDouble;
        if (!hasStartedBattle)
        {
            Camera battleCamera = waveManager != null ? waveManager.WaveCamera : Camera.main;
            if (!IsInsideCameraView(battleCamera) || (waveManager != null && !waveManager.TryStartWave()))
            {
                return;
            }

            hasStartedBattle = true;
            nextBatchTime = now + BatchInterval;
        }

        if (monsterPrefab == null || (waveManager != null && waveManager.State != WaveManager.WaveState.Running))
        {
            return;
        }
        if (spawnCount <= 0 && waveManager.IsWaveEnded())
        {
            waveManager.EndWave();
            return;
        }

        if (remainingInBatch == 0 && now >= nextBatchTime)
        {
            spawnCount--;
            remainingInBatch = Mathf.Max(1, monstersPerBatch);
            spawnGap = System.Math.Min(MaxSpawnGap, BatchInterval / remainingInBatch);
            nextBatchTime = now + BatchInterval;
            nextSpawnTime = now;
        }

        if (remainingInBatch > 0 && now >= nextSpawnTime)
        {
            if(spawnCount > 0)
            {
                SpawnMonster();
            remainingInBatch--;
            nextSpawnTime = now + spawnGap;
            }
            
        }
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
        Vector3 position = transform.position + (Vector3)spawnOffset;
        MonsterHealth spawnedHealth = Instantiate(monsterPrefab, position, monsterPrefab.transform.rotation);
        GameObject spawnedMonster = spawnedHealth.gameObject;
        spawnedHealth.Defeated.AddListener(() => Destroy(spawnedMonster));
        if (waveManager != null)
        {
            waveManager.TrackSpawnedMonster(spawnedMonster);
        }
        spawnedMonster.SetActive(true);
    }
}