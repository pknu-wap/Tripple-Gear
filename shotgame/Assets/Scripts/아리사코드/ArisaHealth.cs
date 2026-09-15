using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ArisaHealth : MonoBehaviour
{
    private const string AntiLayerName = "anti";
    private const string HealLayerName = "heal";

    [System.Serializable]
    private sealed class HealthChangedEvent : UnityEvent<int, int>
    {
    }

    [SerializeField, KoreanLabel("최대 HP"), Min(1)] private int maxHp = 5;
    [SerializeField, KoreanLabel("현재 HP"), Min(0)] private int currentHp = 5;
    [SerializeField, KoreanLabel("시작 시 HP 최대치로 회복")] private bool startWithMaxHp = true;
    [SerializeField, KoreanLabel("anti 피격 데미지"), Min(0)] private int antiDamage = 1;
    [SerializeField, KoreanLabel("heal 회복량"), Min(0)] private int healAmount = 1;
    [SerializeField, KoreanLabel("피해 레이어")] private LayerMask antiLayers;
    [SerializeField, KoreanLabel("회복 레이어")] private LayerMask healLayers;
    [SerializeField, KoreanLabel("HP 변경 시 호출")] private HealthChangedEvent hpChanged = new HealthChangedEvent();
    [SerializeField, KoreanLabel("HP 0 도달 시 호출")] private UnityEvent defeated = new UnityEvent();

    private ArisaInvincibility invincibility;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public bool IsDead => currentHp <= 0;
    public UnityEvent<int, int> HpChanged => hpChanged;
    public UnityEvent Defeated => defeated;

    private void Reset()
    {
        FindReferences();
        AssignDefaultLayers();
        ClampHp();
    }

    private void Awake()
    {
        FindReferences();
        AssignDefaultLayers();

        if (startWithMaxHp)
        {
            currentHp = maxHp;
        }

        ClampHp();
        hpChanged?.Invoke(currentHp, maxHp);
    }

    private void OnValidate()
    {
        AssignDefaultLayers();
        ClampHp();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleLayerHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        HandleLayerHit(collision.collider);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (invincibility != null && invincibility.IsInvincible)
        {
            return;
        }

        SetHp(currentHp - amount);
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(Mathf.CeilToInt(amount));
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetHp(currentHp + amount);
    }

    public void Heal(float amount)
    {
        Heal(Mathf.CeilToInt(amount));
    }

    private void FindReferences()
    {
        if (invincibility == null)
        {
            invincibility = GetComponent<ArisaInvincibility>();
        }
    }

    private void HandleLayerHit(Collider2D hitCollider)
    {
        if (hitCollider == null)
        {
            return;
        }

        int colliderLayer = hitCollider.gameObject.layer;
        int rigidbodyLayer = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.gameObject.layer
            : colliderLayer;

        if (IsInLayerMask(colliderLayer, antiLayers) || IsInLayerMask(rigidbodyLayer, antiLayers))
        {
            TakeDamage(antiDamage);
            return;
        }

        if (IsInLayerMask(colliderLayer, healLayers) || IsInLayerMask(rigidbodyLayer, healLayers))
        {
            Heal(healAmount);
        }
    }

    private void SetHp(int hp)
    {
        int previousHp = currentHp;
        currentHp = Mathf.Clamp(hp, 0, maxHp);

        if (currentHp == previousHp)
        {
            return;
        }

        hpChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0 && previousHp > 0)
        {
            defeated?.Invoke();
        }
    }

    private void AssignDefaultLayers()
    {
        if (antiLayers.value == 0)
        {
            antiLayers = LayerMask.GetMask(AntiLayerName);
        }

        if (healLayers.value == 0)
        {
            healLayers = LayerMask.GetMask(HealLayerName);
        }
    }

    private void ClampHp()
    {
        maxHp = Mathf.Max(1, maxHp);
        antiDamage = Mathf.Max(0, antiDamage);
        healAmount = Mathf.Max(0, healAmount);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
}
