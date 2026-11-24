using System;
using System.Collections.Generic;
using UnityEngine;

public partial class Trigger
{
    [Serializable]
    private sealed class WaveEntrance
    {
        [SerializeField] internal GameObject monster;
        [SerializeField] internal Transform start;
        [SerializeField] internal MonsterEntranceSettings entrance;
    }

    [Serializable]
    private sealed class SpawnRoute
    {
        [SerializeField, InspectorName("출현 위치")] internal Transform start;
        [SerializeField, InspectorName("도착 위치")] internal Transform arrival;
        [SerializeField, InspectorName("등장 설정")] internal MonsterEntranceSettings entrance;
    }

    [Serializable]
    private sealed class EnemySpawnGroup
    {
        [SerializeField, InspectorName("몬스터 프리팹")] internal MonsterHealth prefab;
        [SerializeField, InspectorName("기본 화면 밖 출현 지점")] internal Transform[] points;
        [SerializeField, InspectorName("출현 경로"), Tooltip("경로를 설정하면 기본 화면 밖 출현 지점 대신 순서대로 사용합니다. 0번 경로는 출현 위치만 사용합니다.")]
        internal SpawnRoute[] routes;
        [SerializeField, InspectorName("첫 출현 대기 (초)"), Min(0f)] internal float initialDelay = 1f;
        [SerializeField, InspectorName("출현 간격 (초)"), Min(0.1f)] internal float interval = 2f;
        [SerializeField, InspectorName("동시 생존 최대 수"), Min(1)] internal int maxAlive = 3;
        [SerializeField, InspectorName("총 출현 수 (Wave 전용)"), Min(1)] internal int total = 5;

        internal bool HasRoutes => routes != null && routes.Length > 0;
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

    [SerializeField, KoreanLabel("적 출현 그룹")] private EnemySpawnGroup[] enemySpawnGroups;
    [SerializeField] private WaveEntrance[] waveEntrances;
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
            if (group == null || group.prefab == null)
            {
                return false;
            }
            if (group.HasRoutes)
            {
                foreach (SpawnRoute route in group.routes)
                {
                    if (route == null || !IsScenePoint(route.start)
                        || (route.entrance.type != MonsterEntranceType.Normal && !IsScenePoint(route.arrival)))
                    {
                        return false;
                    }
                }
                continue;
            }
            if (group.points == null || group.points.Length == 0 || !Array.TrueForAll(group.points, IsScenePoint))
            {
                return false;
            }
        }
        return true;
    }

    private bool IsScenePoint(Transform point) => point != null && point.gameObject.scene == gameObject.scene;

    private WaveEntrance GetWaveEntrance(GameObject monster) =>
        waveEntrances == null ? null : Array.Find(waveEntrances, entry => entry != null && entry.monster == monster);

    private bool ValidateWaveEntrance(GameObject monster)
    {
        WaveEntrance entry = GetWaveEntrance(monster);
        return entry == null || entry.entrance.type == MonsterEntranceType.Normal ||
            (monster.TryGetComponent<MonsterHealth>(out _) && (entry.start == null || IsScenePoint(entry.start)));
    }

    private void ActivateWaveMonster(GameObject monster)
    {
        WaveEntrance entry = GetWaveEntrance(monster);
        Vector3 destination = monster.transform.position;
        monster.SetActive(true);
        if (entry == null || entry.entrance.type == MonsterEntranceType.Normal ||
            !monster.TryGetComponent(out MonsterHealth health))
        {
            return;
        }

        Vector3 start = entry.start != null ? entry.start.position : GetDefaultWaveEntranceStart(destination, entry.entrance.type);
        MonsterEntranceMotion.Play(health, start, destination, entry.entrance);
    }

    private Vector3 GetDefaultWaveEntranceStart(Vector3 destination, MonsterEntranceType type)
    {
        if (type == MonsterEntranceType.SkyDrop)
        {
            Camera view = cameraLock.Camera != null ? cameraLock.Camera : Camera.main;
            destination.y += 6f;
            if (view != null && view.orthographic)
            {
                destination.y = Mathf.Max(destination.y, view.transform.position.y + view.orthographicSize + 1.5f);
            }
            return destination;
        }
        return destination + new Vector3(-2.5f, -1.9f);
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
        Camera view = cameraLock.Camera != null ? cameraLock.Camera : Camera.main;
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

            group.Alive.Add(SpawnEnemy(group, view));
            group.Spawned++;
            group.NextSpawn = now + Mathf.Max(0.1f, settings.interval);
        }
        return hasPendingEnemies;
    }

    private MonsterHealth SpawnEnemy(RunningSpawnGroup group, Camera view)
    {
        EnemySpawnGroup settings = group.Settings;
        int pointIndex = group.PointIndex;
        SpawnRoute route = settings.HasRoutes ? settings.routes[pointIndex] : null;
        group.PointIndex = (pointIndex + 1) % (settings.HasRoutes ? settings.routes.Length : settings.points.Length);
        bool animated = route != null && route.entrance.type != MonsterEntranceType.Normal;
        Vector3 start = route != null ? route.start.position : settings.points[pointIndex].position;
        Vector3 destination = animated ? route.arrival.position : GetOffscreenPosition(start, view);

        // Initialize chase height and patrol position at the actual combat location.
        MonsterHealth monster = Instantiate(settings.prefab, destination, settings.prefab.transform.rotation);
        if (monster.TryGetComponent(out NormalMonsterChase chase))
        {
            chase.BeginEncounterApproach(spawnTarget, view);
        }
        monster.gameObject.SetActive(true);
        if (animated) MonsterEntranceMotion.Play(monster, start, destination, route.entrance);
        return monster;
    }

    private static Vector3 GetOffscreenPosition(Vector3 position, Camera view)
    {
        if (view != null && view.orthographic)
        {
            float halfWidth = view.orthographicSize * view.aspect;
            float center = view.transform.position.x;
            position.x = position.x < center
                ? Mathf.Min(position.x, center - halfWidth - 1f)
                : Mathf.Max(position.x, center + halfWidth + 1f);
        }
        return position;
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
