using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class MissionInteractable : MonoBehaviour
{
    [SerializeField, KoreanLabel("수량 목표")] private MissionObjective objective;
    [SerializeField, KoreanLabel("투입 자원 (선택)")] private MissionResource resource;
    [SerializeField, KoreanLabel("조작 성공 이벤트")] private UnityEvent onInteracted = new UnityEvent();

    private readonly List<Collider2D> nearbyColliders = new List<Collider2D>();
    private BoxCollider2D interactionArea;

    internal static int LastKeyboardInteractionFrame { get; private set; } = -1;

    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;

    private void Awake()
    {
        interactionArea = GetComponent<BoxCollider2D>();
        interactionArea.isTrigger = true;
    }

    private void Update()
    {
        if (InteractPressed() && TryInteract())
        {
            LastKeyboardInteractionFrame = Time.frameCount;
        }
    }

    public void Interact() => TryInteract();

    private bool TryInteract()
    {
        if (!isActiveAndEnabled || objective == null || objective.RemainingCount <= 0 || !PlayerIsInRange())
        {
            return false;
        }

        int added = resource != null ? resource.Withdraw(objective.RemainingCount) : 1;
        if (added <= 0) return false;

        objective.AddProgress(added);
        onInteracted.Invoke();
        return true;
    }

    private bool PlayerIsInRange()
    {
        interactionArea.Overlap(new ContactFilter2D { useTriggers = true }, nearbyColliders);
        foreach (Collider2D other in nearbyColliders)
        {
            ArisaHorizontalMovement player = other.GetComponentInParent<ArisaHorizontalMovement>();
            if (player != null && player.gameObject.activeInHierarchy)
            {
                return true;
            }
        }
        return false;
    }

    private static bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current?.eKey.wasPressedThisFrame == true;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.E);
#else
        return false;
#endif
    }
}
