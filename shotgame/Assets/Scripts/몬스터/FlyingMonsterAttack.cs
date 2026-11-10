using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NormalMonsterChase), typeof(MonsterHealth))]
public sealed class FlyingMonsterAttack : MonoBehaviour
{
    private enum Phase
    {
        Approach,
        Firing,
        Retreat,
        WaitAfterRetreat,
    }

    [SerializeField, KoreanLabel("추적 컴포넌트")] private NormalMonsterChase chase;
    [SerializeField, KoreanLabel("체력 컴포넌트")] private MonsterHealth health;
    [SerializeField, KoreanLabel("적공격 발사체")] private FlyingMonsterProjectile projectilePrefab;
    [SerializeField, KoreanLabel("발사 거리"), Min(0.1f)] private float fireRange = 3.5f;
    [SerializeField, KoreanLabel("공격당 발사 횟수"), Min(1)] private int shotsPerAttack = 3;
    [SerializeField, KoreanLabel("연속 발사 간격"), Min(0.01f)] private float shotInterval = 0.25f;
    [SerializeField, KoreanLabel("후퇴 거리"), Min(0f)] private float retreatDistance = 1.5f;
    [SerializeField, KoreanLabel("후퇴 속도 배율"), Min(0.1f)] private float retreatSpeedMultiplier = 1.5f;
    [SerializeField, KoreanLabel("후퇴 후 대기 시간"), Min(0f)] private float retreatPause = 0.3f;
    [SerializeField, KoreanLabel("재접근 최소 거리"), Min(0f)] private float minimumReapproachDistance = 0.75f;
    [SerializeField, KoreanLabel("발사 위치 거리"), Min(0f)] private float projectileSpawnOffset = 0.8f;
    [SerializeField, KoreanLabel("발사체 속도"), Min(0f)] private float projectileSpeed = 7f;
    [SerializeField, KoreanLabel("발사체 수명"), Min(0.1f)] private float projectileLifetime = 4f;

    private ArisaHealth player;
    private Collider2D playerCollider;
    private Phase phase;
    private Vector3 retreatDestination;
    private Vector3 approachStartPosition;
    private float nextAttackTime;
    private float nextBurstShotTime;
    private float pauseEndTime;
    private int burstShotsRemaining;
    private bool started;
    private bool requiresReapproach;

    private MonsterSO Data => health != null ? health.Data : null;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        fireRange = Mathf.Max(0.1f, fireRange);
        shotsPerAttack = Mathf.Max(1, shotsPerAttack);
        shotInterval = Mathf.Max(0.01f, shotInterval);
        retreatDistance = Mathf.Max(0f, retreatDistance);
        retreatSpeedMultiplier = Mathf.Max(0.1f, retreatSpeedMultiplier);
        retreatPause = Mathf.Max(0f, retreatPause);
        minimumReapproachDistance = Mathf.Max(0f, minimumReapproachDistance);
        projectileSpawnOffset = Mathf.Max(0f, projectileSpawnOffset);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
    }

    private void OnDisable()
    {
        if (chase != null)
        {
            chase.SetMovementPaused(false);
        }

        started = false;
        phase = Phase.Approach;
        burstShotsRemaining = 0;
        requiresReapproach = false;
    }

    private void Update()
    {
        if (chase == null || Data == null || health.IsDead || !chase.IsChasing)
        {
            return;
        }

        chase.SetMovementPaused(true);
        if (!TryFindPlayer() || player.IsDead)
        {
            return;
        }

        chase.FaceTarget(player.transform);
        if (!started)
        {
            started = true;
            phase = Phase.Approach;
            approachStartPosition = transform.position;
            nextAttackTime = 0f;
        }

        switch (phase)
        {
            case Phase.Approach:
                UpdateApproach();
                break;
            case Phase.Firing:
                UpdateFiring();
                break;
            case Phase.Retreat:
                UpdateRetreat();
                break;
            case Phase.WaitAfterRetreat:
                if (Time.time >= pauseEndTime)
                {
                    phase = Phase.Approach;
                    approachStartPosition = transform.position;
                    requiresReapproach = true;
                }
                break;
        }
    }

    private void UpdateApproach()
    {
        Vector3 playerPosition = GetPlayerPosition();
        float distanceToPlayer = Mathf.Abs(playerPosition.x - transform.position.x);
        float approachDistance = Mathf.Abs(transform.position.x - approachStartPosition.x);
        bool needsMovement = distanceToPlayer > fireRange
            || (requiresReapproach && approachDistance < minimumReapproachDistance);

        if (needsMovement)
        {
            float nextX = Mathf.MoveTowards(transform.position.x, playerPosition.x,
                Data.MoveSpeed * chase.MovementSpeedMultiplier * Time.deltaTime);
            float moved = chase.MoveHorizontally(nextX - transform.position.x);
            if (chase.IsJumping || Mathf.Abs(moved) > 0.0001f || distanceToPlayer > fireRange)
            {
                return;
            }
        }

        if (Time.time < nextAttackTime || projectilePrefab == null)
        {
            return;
        }

        phase = Phase.Firing;
        burstShotsRemaining = shotsPerAttack;
        nextBurstShotTime = Time.time;
        UpdateFiring();
    }

    private void UpdateFiring()
    {
        if (Time.time < nextBurstShotTime || projectilePrefab == null)
        {
            return;
        }

        Vector3 playerPosition = GetPlayerPosition();
        FireAtPlayer(playerPosition);
        burstShotsRemaining--;
        if (burstShotsRemaining > 0)
        {
            nextBurstShotTime = Time.time + shotInterval;
            return;
        }

        nextAttackTime = Time.time + Data.AttackInterval;
        BeginRetreat(playerPosition);
    }

    private void FireAtPlayer(Vector3 playerPosition)
    {
        Vector2 direction = playerPosition - transform.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.down;
        }

        direction.Normalize();
        Vector3 spawnPosition = transform.position + (Vector3)(direction * projectileSpawnOffset);
        FlyingMonsterProjectile projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        projectile.Launch(direction, projectileSpeed, projectileLifetime, Data.AttackDamage, player);
    }

    private void BeginRetreat(Vector3 playerPosition)
    {
        Vector3 away = transform.position - playerPosition;
        away.y = 0f;
        away.z = 0f;
        if (away.sqrMagnitude < 0.0001f)
        {
            away = Vector3.right;
        }

        retreatDestination = transform.position + away.normalized * retreatDistance;
        phase = Phase.Retreat;
    }

    private void UpdateRetreat()
    {
        float nextX = Mathf.MoveTowards(transform.position.x, retreatDestination.x,
            Data.MoveSpeed * retreatSpeedMultiplier * chase.MovementSpeedMultiplier * Time.deltaTime);
        float moved = chase.MoveHorizontally(nextX - transform.position.x);
        if (Mathf.Abs(transform.position.x - retreatDestination.x) > 0.0001f
            && (Mathf.Abs(moved) > 0.0001f || chase.IsJumping))
        {
            return;
        }

        pauseEndTime = Time.time + retreatPause;
        phase = Phase.WaitAfterRetreat;
    }

    private bool TryFindPlayer()
    {
        if (player != null)
        {
            return true;
        }

#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        player = FindFirstObjectByType<ArisaHealth>();
#else
        player = FindObjectOfType<ArisaHealth>();
#endif
        playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
        return player != null;
    }

    private Vector3 GetPlayerPosition()
    {
        return playerCollider != null ? playerCollider.bounds.center : player.transform.position;
    }

    private void FindReferences()
    {
        if (chase == null)
        {
            chase = GetComponent<NormalMonsterChase>();
        }

        if (health == null)
        {
            health = GetComponent<MonsterHealth>();
        }
    }
}
