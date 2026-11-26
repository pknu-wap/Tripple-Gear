using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class MonsterHealth : MonoBehaviour
{
    [System.Serializable]
    private sealed class HealthChangedEvent : UnityEvent<int, int>
    {
    }

    [SerializeField, KoreanLabel("몬스터 데이터")] private MonsterSO monsterData;
    [SerializeField, KoreanLabel("HP 변경 시 호출")] private HealthChangedEvent hpChanged = new HealthChangedEvent();
    [SerializeField, KoreanLabel("HP 0 도달 시 호출")] private UnityEvent defeated = new UnityEvent();

    // 같은 SO를 사용하는 몬스터도 현재 HP는 각자 보관한다.
    private int currentHp;
    private int? maxHpOverride;
    private NormalMonsterChase chase;
    private ExplosiveMonster explosive;
    private Animator idleAnimator;
    private RuntimeAnimatorController idleController;
    private AnimatorUpdateMode previousAnimatorMode;
    private bool playingIdleDuringDialogue;

    public MonsterSO Data => monsterData;
    public int MaxHp => GameDifficulty.ScaleMonsterHp(maxHpOverride ?? (monsterData != null ? monsterData.MaxHp : 0));
    public int CurrentHp => currentHp;
    public bool IsDead => currentHp <= 0;
    internal bool IsStunned { get; private set; }
    internal bool IsEntering { get; set; }

    internal void SetStunned(bool stunned) => IsStunned = stunned;
    public UnityEvent<int, int> HpChanged => hpChanged;
    public UnityEvent Defeated => defeated;
    internal MissionObjective RequiredDamageObjective { get; set; }
    internal MonsterSO DropTableOverride { get; set; }

    private void Awake()
    {
        TryGetComponent(out chase);
        TryGetComponent(out explosive);
        if ((chase != null || explosive != null) && TryGetComponent(out idleAnimator))
        {
            idleController = idleAnimator.runtimeAnimatorController;
        }

        if (monsterData == null)
        {
            Debug.LogError("MonsterHealth에 몬스터 SO를 연결해 주세요.", this);
            return;
        }

        currentHp = MaxHp;
        hpChanged?.Invoke(currentHp, MaxHp);
    }

    internal void InitializeHpOverride(int maxHp)
    {
        maxHpOverride = maxHp;
        currentHp = MaxHp;
        hpChanged?.Invoke(currentHp, MaxHp);
    }

    private void LateUpdate()
    {
        if (idleController == null || idleAnimator == null) return;

        if (GameplayPause.IsPausedOnlyForDialogue && !IsDead && !IsEntering && !IsStunned
            && !idleAnimator.applyRootMotion && idleAnimator.runtimeAnimatorController == idleController
            && (chase == null || !chase.IsChasing) && (explosive == null || explosive.IsIdle))
        {
            if (playingIdleDuringDialogue) return;

            previousAnimatorMode = idleAnimator.updateMode;
            idleAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            playingIdleDuringDialogue = true;
        }
        else
        {
            RestoreAnimatorMode();
        }
    }

    private void OnDisable() => RestoreAnimatorMode();

    private void RestoreAnimatorMode()
    {
        if (!playingIdleDuringDialogue) return;

        if (idleAnimator != null) idleAnimator.updateMode = previousAnimatorMode;
        playingIdleDuringDialogue = false;
    }

    public void TakeDamage(int amount)
    {
        ApplyDamage(amount, false);
    }

    internal void TakeGunDamage(float amount)
    {
        ApplyDamage(Mathf.CeilToInt(amount), true);
    }

    private void ApplyDamage(int amount, bool fromGun)
    {
        if (amount <= 0 || IsDead || (RequiredDamageObjective != null && !RequiredDamageObjective.IsRunning))
        {
            return;
        }

        SetHp(currentHp - amount, fromGun);
    }

    internal void ResetForMission()
    {
        if (TryGetComponent(out RobotStun stun)) stun.End();
        currentHp = MaxHp;
        if (TryGetComponent(out MonsterHitFlash hitFlash)) hitFlash.enabled = false;
        if (TryGetComponent(out MonsterDeathEffect deathEffect)) deathEffect.ResetForMission();
        if (chase != null) chase.ResetForMission();
        hpChanged?.Invoke(currentHp, MaxHp);
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(Mathf.CeilToInt(amount));
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        SetHp(amount >= MaxHp - currentHp ? MaxHp : currentHp + amount);
    }

    private void SetHp(int hp, bool fromGun = false)
    {
        int previousHp = currentHp;
        currentHp = Mathf.Clamp(hp, 0, MaxHp);
        if (currentHp == previousHp)
        {
            return;
        }

        if (fromGun)
        {
            MonsterHitFlash.Play(gameObject, monsterData.UseWallHitFlash);
        }

        if (IsDead
            && !TryGetComponent<BossMonsterController>(out _)
            && !TryGetComponent<AtailSerpent>(out _)
            && !TryGetComponent<SummitAirshipBoss>(out _)
            && !TryGetComponent<GongOilBoss>(out _))
        {
            TryGetComponent(out MonsterDeathEffect deathEffect);
            GameObject burst = deathEffect != null ? deathEffect.Prefab : null;
            if (burst == null && explosive != null)
            {
                burst = explosive.ExplosionEffectPrefab;
            }
            MonsterDeathBurst.Play(gameObject, burst, fromGun, monsterData.UseWallHitFlash);
        }

        hpChanged?.Invoke(currentHp, MaxHp);
        if (IsDead && previousHp > 0)
        {
            DropItem();
            defeated?.Invoke();
        }
    }

    private void DropItem()
    {
        GameObject prefab = (DropTableOverride != null ? DropTableOverride : monsterData).RollDrop();
        if (prefab != null)
        {
            Instantiate(prefab, transform.position, prefab.transform.rotation);
        }
    }
}
