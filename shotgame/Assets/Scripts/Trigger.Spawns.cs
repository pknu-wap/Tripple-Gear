using System;
using System.Collections.Generic;
using UnityEngine;

public partial class Trigger
{
    [Serializable]
    private sealed class EnemySpawnGroup
    {
        [SerializeField, InspectorName("몬스터 프리팹")] internal MonsterHealth prefab;
        [SerializeField, InspectorName("화면 밖 출현 지점")] internal Transform[] points;
        [SerializeField, InspectorName("첫 출현 대기 (초)"), Min(0f)] internal float initialDelay = 1f;
        [SerializeField, InspectorName("출현 간격 (초)"), Min(0.1f)] internal float interval = 2f;
        [SerializeField, InspectorName("동시 생존 최대 수"), Min(1)] internal int maxAlive = 3;
        [SerializeField, InspectorName("총 출현 수 (Wave 전용)"), Min(1)] internal int total = 5;
    }

    private sealed class RunningSpawnGroup
    {
        internal readonly EnemySpawnGroup Settings;
        internal readonly List<MonsterHealth> Alive = new List<MonsterHealth>();
        internal int Spawned;
        internal int PointIndex;
        internal double NextSpawn;

        internal RunningSpawnGroup(EnemySpawnGroup settings)
        {
            Settings = settings;
            NextSpawn = Time.timeAsDouble + Mathf.Max(0f, settings.initialDelay);
        }
    }

    [SerializeField, KoreanLabel("화면 밖 적 출현 그룹")] private EnemySpawnGroup[] enemySpawnGroups;
    [SerializeField, KoreanLabel("목표 달성 시 출현한 적 정리")] private bool clearMissionSpawnedEnemies = true;
    [SerializeField, KoreanLabel("클리어 후에도 적 출현")] private bool continueSpawningAfterMissionClear;

    private readonly List<RunningSpawnGroup> runningSpawns = new List<RunningSpawnGroup>();
    private Transform spawnTarget;
    private bool spawnForever;

    private bool HasEnemySpawns => enemySpawnGroups != null && enemySpawnGroups.Length > 0;

    private bool ValidateEnemySpawns()
    {
        if (!HasEnemySpawns)
        {
            return true;
        }
        foreach (EnemySpawnGroup group in enemySpawnGroups)
        {
            if (group == null || group.prefab == null || group.points == null || group.points.Length == 0)
            {
                return false;
            }
            foreach (Transform point in group.points)
            {
                if (point == null || point.gameObject.scene != gameObject.scene)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void StartEnemySpawns(ArisaHorizontalMovement player, bool repeat)
    {
        if (!HasEnemySpawns)
        {
            return;
        }
        spawnTarget = player != null ? player.transform : null;
        spawnForever = repeat;
        foreach (EnemySpawnGroup group in enemySpawnGroups)
        {
            runningSpawns.Add(new RunningSpawnGroup(group));
        }
    }

    private bool UpdateEnemySpawns()
    {
        bool hasPendingEnemies = false;
        double now = Time.timeAsDouble;
        Camera view = lockedCamera != null ? lockedCamera : Camera.main;
        foreach (RunningSpawnGroup group in runningSpawns)
        {
            for (int i = group.Alive.Count - 1; i >= 0; i--)
            {
                MonsterHealth monster = group.Alive[i];
                if (monster == null || monster.IsDead || !monster.gameObject.activeInHierarchy)
                {
                    if (monster != null) Destroy(monster.gameObject);
                    group.Alive.RemoveAt(i);
                }
            }

            EnemySpawnGroup settings = group.Settings;
            bool canSpawnMore = spawnForever || group.Spawned < Mathf.Max(1, settings.total);
            hasPendingEnemies |= canSpawnMore || group.Alive.Count > 0;
            if (!canSpawnMore || now < group.NextSpawn || group.Alive.Count >= Mathf.Max(1, settings.maxAlive))
            {
                continue;
            }

            Transform point = settings.points[group.PointIndex];
            group.PointIndex = (group.PointIndex + 1) % settings.points.Length;
            Vector3 position = point.position;
            if (view != null && view.orthographic)
            {
                float halfWidth = view.orthographicSize * view.aspect;
                float center = view.transform.position.x;
                position.x = position.x < center
                    ? Mathf.Min(position.x, center - halfWidth - 1f)
                    : Mathf.Max(position.x, center + halfWidth + 1f);
            }

            MonsterHealth monsterInstance = Instantiate(settings.prefab, position, settings.prefab.transform.rotation);
            if (monsterInstance.TryGetComponent(out NormalMonsterChase chase))
            {
                chase.BeginEncounterApproach(spawnTarget, view);
            }
            monsterInstance.gameObject.SetActive(true);
            group.Alive.Add(monsterInstance);
            group.Spawned++;
            group.NextSpawn = now + Mathf.Max(0.1f, settings.interval);
        }
        return hasPendingEnemies;
    }

    private void StopEnemySpawns(bool removeSurvivors)
    {
        foreach (RunningSpawnGroup group in runningSpawns)
        {
            ReleaseSpawnedEnemies(group, removeSurvivors);
        }
        runningSpawns.Clear();
        spawnTarget = null;
    }

    private void ReleaseSpawnedEnemies(RunningSpawnGroup group, bool removeSurvivors)
    {
        foreach (MonsterHealth monster in group.Alive)
        {
            if (monster == null)
            {
                continue;
            }
            if (removeSurvivors || monster.IsDead || !monster.gameObject.activeInHierarchy)
            {
                monster.gameObject.SetActive(false);
                Destroy(monster.gameObject);
            }
            else if (monster.TryGetComponent(out NormalMonsterChase chase))
            {
                chase.EndEncounterApproach();
            }
        }
        group.Alive.Clear();
    }
}
