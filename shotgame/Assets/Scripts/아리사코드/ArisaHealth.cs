using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
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
    [SerializeField, KoreanLabel("피격 스프라이트 렌더러")] private SpriteRenderer hitSpriteRenderer;
    [SerializeField, KoreanLabel("피격 애니메이터")] private Animator hitAnimator;
    [SerializeField, KoreanLabel("피격 표시 시간"), Min(0.01f)] private float hitDisplayDuration = 1f;
    [SerializeField, KoreanLabel("피격 중 최소 불투명도"), Range(0f, 1f)] private float hitMinOpacity = 0.6f;
    [SerializeField, KoreanLabel("초당 피격 깜빡임 횟수"), Min(0f)] private float hitFlashFrequency = 4f;
    [SerializeField, KoreanLabel("피격 경직 시간"), Min(0f)] private float hitStunDuration = 0.18f;
    [SerializeField, KoreanLabel("피격 넉백 거리"), Min(0f)] private float hitKnockbackDistance = 0.35f;
    [SerializeField, KoreanLabel("피격 위로 튀는 속도"), Min(0f)] private float hitKnockbackUpSpeed = 3f;
    [SerializeField, KoreanLabel("HP 변경 시 호출")] private HealthChangedEvent hpChanged = new HealthChangedEvent();
    [SerializeField, KoreanLabel("HP 0 도달 시 호출")] private UnityEvent defeated = new UnityEvent();

    private ArisaInvincibility invincibility;
    private ArisaMouseAim aim;
    private ArisaHorizontalMovement movement;
    private BoxCollider2D bodyCollider;
    private SpriteRenderer[] normalSpriteRenderers;
    private bool[] normalRendererStates;
    private Color hitSpriteOriginalColor;
    private float hitVisualStartTime;
    private float hitVisualEndTime;
    private float hitProtectionEndTime;
    private bool hitVisualActive;

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
        if (hitSpriteRenderer != null)
        {
            hitSpriteRenderer.enabled = false;
        }
        hpChanged?.Invoke(currentHp, maxHp);
    }

    private void OnValidate()
    {
        AssignDefaultLayers();
        ClampHp();
        hitDisplayDuration = Mathf.Max(0.01f, hitDisplayDuration);
        hitMinOpacity = Mathf.Clamp01(hitMinOpacity);
        hitFlashFrequency = Mathf.Max(0f, hitFlashFrequency);
        hitStunDuration = Mathf.Max(0f, hitStunDuration);
        hitKnockbackDistance = Mathf.Max(0f, hitKnockbackDistance);
        hitKnockbackUpSpeed = Mathf.Max(0f, hitKnockbackUpSpeed);
    }

    private void LateUpdate()
    {
        if (!hitVisualActive)
        {
            return;
        }

        if (Time.time >= hitVisualEndTime)
        {
            StopHitVisual();
            return;
        }

        if (hitSpriteRenderer != null)
        {
            hitSpriteRenderer.flipX = aim != null && aim.IsFacingLeft;
            hitSpriteRenderer.enabled = true;
            float flashPhase = Mathf.PingPong(
                (Time.time - hitVisualStartTime) * hitFlashFrequency * 2f, 1f
            );
            Color flashColor = hitSpriteOriginalColor;
            flashColor.a *= Mathf.Lerp(1f, hitMinOpacity, flashPhase);
            hitSpriteRenderer.color = flashColor;
        }

        HideNormalSpriteRenderers();
    }

    private void OnDisable()
    {
        StopHitVisual();
        hitProtectionEndTime = 0f;
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
        TakeDamage(amount, null);
    }

    public void TakeDamage(int amount, Vector2? sourcePosition)
    {
        if (amount <= 0)
        {
            return;
        }

        // 스킬 컴포넌트가 Awake 이후에 무적 컴포넌트를 추가할 수도 있다.
        FindReferences();
        if (Time.time < hitProtectionEndTime
            || (invincibility != null && invincibility.IsInvincible))
        {
            return;
        }

        SetHp(currentHp - amount, sourcePosition);
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

        if (aim == null)
        {
            aim = GetComponent<ArisaMouseAim>();
        }

        if (movement == null)
        {
            movement = GetComponent<ArisaHorizontalMovement>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<BoxCollider2D>();
        }

        if (hitSpriteRenderer == null)
        {
            Transform hitSprite = transform.Find("HitSprite");
            if (hitSprite != null)
            {
                hitSpriteRenderer = hitSprite.GetComponent<SpriteRenderer>();
            }
        }

        if (hitAnimator == null && hitSpriteRenderer != null)
        {
            hitAnimator = hitSpriteRenderer.GetComponent<Animator>();
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
            TakeDamage(antiDamage, hitCollider.bounds.center);
            return;
        }

        if (IsInLayerMask(colliderLayer, healLayers) || IsInLayerMask(rigidbodyLayer, healLayers))
        {
            Heal(healAmount);
        }
    }

    private void SetHp(int hp, Vector2? sourcePosition = null)
    {
        int previousHp = currentHp;
        currentHp = Mathf.Clamp(hp, 0, maxHp);

        if (currentHp == previousHp)
        {
            return;
        }

        if (currentHp < previousHp)
        {
            hitProtectionEndTime = Time.time + hitDisplayDuration;
            PlayHitVisual();
            PlayHitReaction(sourcePosition);
        }

        hpChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0 && previousHp > 0)
        {
            defeated?.Invoke();
        }
    }

    private void PlayHitReaction(Vector2? sourcePosition)
    {
        if (movement == null || hitStunDuration <= 0f)
        {
            return;
        }

        float fallbackDirection = aim != null && aim.IsFacingLeft ? 1f : -1f;
        float playerCenterX = bodyCollider != null ? bodyCollider.bounds.center.x : transform.position.x;
        float horizontalOffset = sourcePosition.HasValue
            ? playerCenterX - sourcePosition.Value.x
            : 0f;
        float knockbackDirection = Mathf.Abs(horizontalOffset) > 0.01f
            ? Mathf.Sign(horizontalOffset)
            : fallbackDirection;
        movement.StartHitReaction(knockbackDirection, hitStunDuration, hitKnockbackDistance, hitKnockbackUpSpeed);
    }

    private void PlayHitVisual()
    {
        if (hitSpriteRenderer == null)
        {
            return;
        }

        if (hitVisualActive && Time.time >= hitVisualEndTime)
        {
            StopHitVisual();
        }

        if (!hitVisualActive)
        {
            hitSpriteOriginalColor = hitSpriteRenderer.color;
            normalSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            normalRendererStates = new bool[normalSpriteRenderers.Length];
            for (int i = 0; i < normalSpriteRenderers.Length; i++)
            {
                normalRendererStates[i] = normalSpriteRenderers[i] != null && normalSpriteRenderers[i].enabled;
            }
        }

        hitVisualActive = true;
        hitVisualStartTime = Time.time;
        hitVisualEndTime = Time.time + hitDisplayDuration;
        hitSpriteRenderer.flipX = aim != null && aim.IsFacingLeft;
        hitSpriteRenderer.enabled = true;
        if (hitAnimator != null)
        {
            hitAnimator.Play(0, 0, 0f);
        }

        HideNormalSpriteRenderers();
    }

    private void HideNormalSpriteRenderers()
    {
        if (normalSpriteRenderers == null)
        {
            return;
        }

        foreach (SpriteRenderer spriteRenderer in normalSpriteRenderers)
        {
            if (spriteRenderer != null && spriteRenderer != hitSpriteRenderer)
            {
                spriteRenderer.enabled = false;
            }
        }
    }

    private void StopHitVisual()
    {
        if (!hitVisualActive)
        {
            return;
        }

        hitVisualActive = false;
        if (hitSpriteRenderer != null)
        {
            hitSpriteRenderer.enabled = false;
            hitSpriteRenderer.color = hitSpriteOriginalColor;
        }

        if (normalSpriteRenderers != null && normalRendererStates != null)
        {
            for (int i = 0; i < normalSpriteRenderers.Length; i++)
            {
                if (normalSpriteRenderers[i] != null)
                {
                    normalSpriteRenderers[i].enabled = normalRendererStates[i];
                }
            }
        }

        normalSpriteRenderers = null;
        normalRendererStates = null;
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
