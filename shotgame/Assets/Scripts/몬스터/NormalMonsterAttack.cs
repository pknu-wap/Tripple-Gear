using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class NormalMonsterAttack : MonoBehaviour
{
    [Header("연결")]
    [SerializeField, KoreanLabel("추적 컴포넌트")] private NormalMonsterChase chase;
    [SerializeField, KoreanLabel("애니메이터")] private Animator animator;

    [Header("공격")]
    [SerializeField, KoreanLabel("공격 컨트롤러")] private RuntimeAnimatorController attackController;
    [SerializeField, KoreanLabel("첫 공격 지연"), Min(0f)] private float firstAttackDelay = 0.4f;
    [SerializeField, KoreanLabel("공격 간격"), Min(0.1f)] private float attackInterval = 2f;
    [SerializeField, KoreanLabel("공격 데미지"), Min(0)] private int attackDamage = 1;
    [SerializeField, KoreanLabel("피격 로그 메시지")] private string playerHitLogMessage = "플레이어가 일반몹 공격에 맞았습니다.";

    private ArisaHealth touchingPlayerHealth;
    private ArisaHealth targetPlayerHealth;
    private float nextAttackTime;
    private bool isAttacking;
    private bool wasOverlappingPlayer;
    private Collider2D[] ownColliders;
    private readonly List<Collider2D> touchingPlayerColliders = new List<Collider2D>();

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
        firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
        attackInterval = Mathf.Max(0.1f, attackInterval);
        attackDamage = Mathf.Max(0, attackDamage);

        if (!Application.isPlaying)
        {
            FindReferences();
        }
    }

    private void OnDisable()
    {
        ClearPlayerContact();
    }

    private void Update()
    {
        bool hadActiveContact = wasOverlappingPlayer;
        ArisaHealth overlappingPlayerHealth;
        if (!TryFindOverlappingPlayer(out overlappingPlayerHealth))
        {
            if (hadActiveContact || touchingPlayerHealth != null || isAttacking)
            {
                ClearPlayerContact();
            }

            return;
        }

        bool isNewContact = !hadActiveContact || touchingPlayerHealth != overlappingPlayerHealth;
        touchingPlayerHealth = overlappingPlayerHealth;
        targetPlayerHealth = overlappingPlayerHealth;
        wasOverlappingPlayer = true;
        UpdateChaseState(true);

        if (isNewContact)
        {
            nextAttackTime = Time.time + firstAttackDelay;
        }

        AttackTouchingPlayer();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartPlayerContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartPlayerContact(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        TryEndPlayerContact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
        {
            TryStartPlayerContact(collision.collider);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision != null)
        {
            TryStartPlayerContact(collision.collider);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision != null)
        {
            TryEndPlayerContact(collision.collider);
        }
    }

    private void AttackTouchingPlayer()
    {
        if (touchingPlayerHealth == null)
        {
            ClearPlayerContact();
            return;
        }

        UpdateChaseState(true);

        if (Time.time < nextAttackTime)
        {
            return;
        }

        EnterAttackState();

        if (attackDamage > 0)
        {
            touchingPlayerHealth.TakeDamage(attackDamage);
        }

        Debug.Log(playerHitLogMessage, touchingPlayerHealth);
        nextAttackTime = Time.time + attackInterval;
        RestartCurrentAnimation();
    }

    private void TryStartPlayerContact(Collider2D other)
    {
        ArisaHealth playerHealth = FindPlayerHealth(other);
        if (playerHealth == null)
        {
            return;
        }

        bool hadActiveContact = wasOverlappingPlayer;
        if (touchingPlayerHealth != null && touchingPlayerHealth != playerHealth)
        {
            ClearPlayerContact();
            hadActiveContact = false;
        }

        touchingPlayerHealth = playerHealth;
        targetPlayerHealth = playerHealth;
        AddPlayerCollider(other);
        UpdateChaseState(true);

        if (!hadActiveContact)
        {
            nextAttackTime = Time.time + firstAttackDelay;
        }
    }

    private void TryEndPlayerContact(Collider2D other)
    {
        ArisaHealth playerHealth = FindPlayerHealth(other);
        if (playerHealth == null || playerHealth != touchingPlayerHealth)
        {
            return;
        }

        RemovePlayerCollider(other);
    }

    private void ClearPlayerContact()
    {
        touchingPlayerColliders.Clear();
        touchingPlayerHealth = null;
        wasOverlappingPlayer = false;
        ExitAttackState();
        UpdateChaseState(false);
    }

    private void UpdateChaseState(bool shouldPause)
    {
        if (chase == null)
        {
            return;
        }

        if (touchingPlayerHealth != null)
        {
            chase.SetFollowTarget(touchingPlayerHealth.transform);
            chase.FaceTarget(touchingPlayerHealth.transform);
        }

        chase.SetMovementPaused(shouldPause);
    }

    private void EnterAttackState()
    {
        if (isAttacking)
        {
            return;
        }

        isAttacking = true;
        SetAnimationController(attackController);
        RestartCurrentAnimation();
    }

    private void ExitAttackState()
    {
        if (!isAttacking)
        {
            return;
        }

        isAttacking = false;
    }

    private void FindReferences()
    {
        if (chase == null)
        {
            chase = GetComponent<NormalMonsterChase>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponents<Collider2D>();
        }
    }

    private void AddPlayerCollider(Collider2D playerCollider)
    {
        if (!IsValidPlayerCollider(playerCollider) || touchingPlayerColliders.Contains(playerCollider))
        {
            return;
        }

        touchingPlayerColliders.Add(playerCollider);
    }

    private void RemovePlayerCollider(Collider2D playerCollider)
    {
        if (playerCollider == null)
        {
            return;
        }

        touchingPlayerColliders.Remove(playerCollider);
    }

    private bool TryFindOverlappingPlayer(out ArisaHealth playerHealth)
    {
        touchingPlayerColliders.Clear();

        if (TryCollectOverlappingPlayer(targetPlayerHealth, out playerHealth))
        {
            return true;
        }

        if (TryCollectOverlappingPlayer(touchingPlayerHealth, out playerHealth))
        {
            return true;
        }

        targetPlayerHealth = FindPlayerInScene();
        return TryCollectOverlappingPlayer(targetPlayerHealth, out playerHealth);
    }

    private bool TryCollectOverlappingPlayer(ArisaHealth candidateHealth, out ArisaHealth playerHealth)
    {
        playerHealth = null;
        if (candidateHealth == null || !candidateHealth.gameObject.activeInHierarchy)
        {
            return false;
        }

        Collider2D[] playerColliders = candidateHealth.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (IsValidPlayerCollider(playerCollider) && IsStillTouching(playerCollider))
            {
                AddPlayerCollider(playerCollider);
            }
        }

        if (touchingPlayerColliders.Count == 0)
        {
            return false;
        }

        playerHealth = candidateHealth;
        return true;
    }

    private bool HasActivePlayerContact()
    {
        return touchingPlayerHealth != null && touchingPlayerColliders.Count > 0;
    }

    private bool IsValidPlayerCollider(Collider2D playerCollider)
    {
        return playerCollider != null
            && playerCollider.enabled
            && playerCollider.gameObject.activeInHierarchy
            && (touchingPlayerHealth == null || FindPlayerHealth(playerCollider) == touchingPlayerHealth);
    }

    private bool IsStillTouching(Collider2D playerCollider)
    {
        if (playerCollider == null)
        {
            return false;
        }

        if (ownColliders == null || ownColliders.Length == 0)
        {
            FindReferences();
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D ownCollider = ownColliders[i];
            if (ownCollider == null || !ownCollider.enabled || !ownCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            ColliderDistance2D distance = ownCollider.Distance(playerCollider);
            if ((distance.isValid && distance.isOverlapped)
                || ownCollider.IsTouching(playerCollider)
                || ownCollider.bounds.Intersects(playerCollider.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private static ArisaHealth FindPlayerInScene()
    {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        return Object.FindFirstObjectByType<ArisaHealth>();
#else
        return Object.FindObjectOfType<ArisaHealth>();
#endif
    }

    private void SetAnimationController(RuntimeAnimatorController controller)
    {
        if (animator == null || controller == null || animator.runtimeAnimatorController == controller)
        {
            return;
        }

        animator.runtimeAnimatorController = controller;
    }

    private void RestartCurrentAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.Play(0, 0, 0f);
    }

    private static ArisaHealth FindPlayerHealth(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        ArisaHealth playerHealth = other.GetComponent<ArisaHealth>();
        if (playerHealth != null)
        {
            return playerHealth;
        }

        if (other.attachedRigidbody != null)
        {
            playerHealth = other.attachedRigidbody.GetComponent<ArisaHealth>();
            if (playerHealth != null)
            {
                return playerHealth;
            }
        }

        return other.GetComponentInParent<ArisaHealth>();
    }
}
