using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class SkillPickup : MonoBehaviour
{
    [SerializeField, KoreanLabel("획득 스킬")] private SkillSO skillData;
    [SerializeField, KoreanLabel("획득 대상 레이어")] private LayerMask targetLayers = ~0;

    public SkillSO SkillData => skillData;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (skillData == null || other == null || !IsTargetLayer(other))
        {
            return;
        }

        ArisaSkillController skillController = GetSkillController(other);
        if (skillController == null || !skillController.TryAcquireSkill(skillData))
        {
            return;
        }

        Destroy(gameObject);
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider2D triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private static ArisaSkillController GetSkillController(Collider2D other)
    {
        ArisaSkillController skillController = other.GetComponentInParent<ArisaSkillController>();
        if (skillController != null)
        {
            return skillController;
        }

        return other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponentInParent<ArisaSkillController>()
            : null;
    }

    private bool IsTargetLayer(Collider2D other)
    {
        int colliderLayer = other.gameObject.layer;
        int rigidbodyLayer = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject.layer
            : colliderLayer;

        return IsInLayerMask(colliderLayer, targetLayers) || IsInLayerMask(rigidbodyLayer, targetLayers);
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
}
