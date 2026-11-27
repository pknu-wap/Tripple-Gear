using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class MonsterHealth : MonoBehaviour
{
    private enum DamageSource { Other, GunShot, AdditionalGunDamage, Skill }

    [System.Serializable]
    private sealed class HealthChangedEvent : UnityEvent<int, int>
    {
    }

    [SerializeField, KoreanLabel("몬스터 데이터")] private MonsterSO monsterData;
    [SerializeField, KoreanLabel("보스 처치 점수")] private bool awardsBossDefeatScore;
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
        ApplyDamage(amount, DamageSource.Other);
    }

    internal void TakeGunDamage(float amount, bool awardHit = true)
    {
        ApplyDamage(Mathf.CeilToInt(amount), awardHit ? DamageSource.GunShot : DamageSource.AdditionalGunDamage);
    }

    internal void TakeSkillDamage(float amount) => ApplyDamage(Mathf.CeilToInt(amount), DamageSource.Skill);

    private void ApplyDamage(int amount, DamageSource source)
    {
        if (amount <= 0 || IsDead || (RequiredDamageObjective != null && !RequiredDamageObjective.IsRunning))
        {
            return;
        }

        SetHp(currentHp - amount, source);
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

    private void SetHp(int hp, DamageSource source = DamageSource.Other)
    {
        int previousHp = currentHp;
        currentHp = Mathf.Clamp(hp, 0, MaxHp);
        if (currentHp == previousHp)
        {
            return;
        }

        bool fromGun = source == DamageSource.GunShot || source == DamageSource.AdditionalGunDamage;
        if (fromGun)
        {
            MonsterHitFlash.Play(gameObject, monsterData.UseWallHitFlash);
        }

        int points = source == DamageSource.GunShot ? 10 : 0;
        if (IsDead)
        {
            bool isClassicBoss = TryGetComponent<BossMonsterController>(out _) || TryGetComponent<AtailSerpent>(out _);
            bool continuesIntoSky = TryGetComponent<SummitAirshipBoss>(out _) || TryGetComponent<GongOilBoss>(out _);
            if (source != DamageSource.Other && !continuesIntoSky)
                points += isClassicBoss || awardsBossDefeatScore ? 50 : 40;

            if (!isClassicBoss && !continuesIntoSky)
            {
                TryGetComponent(out MonsterDeathEffect deathEffect);
                GameObject burst = deathEffect != null ? deathEffect.Prefab : null;
                if (burst == null && explosive != null)
                {
                    burst = explosive.ExplosionEffectPrefab;
                }
                MonsterDeathBurst.Play(gameObject, burst, fromGun, monsterData.UseWallHitFlash);
            }
        }

        if (points > 0) GameManager.Instance?.AddScore(points);

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
