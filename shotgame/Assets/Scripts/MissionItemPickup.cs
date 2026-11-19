using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class MissionItemPickup : MonoBehaviour
{
    [SerializeField, KoreanLabel("미션 자원")] private MissionResource resource;
    [SerializeField, KoreanLabel("획득 수량"), Min(1)] private int amount = 1;
    private bool collected;

    internal void SetResource(MissionResource target) => resource = target;

    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
    private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled || collected || resource == null || other.GetComponentInParent<ArisaHorizontalMovement>() == null)
        {
            return;
        }

        collected = true;
        resource.Collect(Mathf.Max(1, amount));
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
