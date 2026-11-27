using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class MissionItemPickup : MonoBehaviour
{
    [SerializeField, KoreanLabel("미션 자원")] private MissionResource resource;
    [SerializeField, KoreanLabel("획득 수량"), Min(1)] private int amount = 1;
    private bool collected;

    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
    private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled || collected)
        {
            return;
        }

        ArisaHorizontalMovement player = other.GetComponentInParent<ArisaHorizontalMovement>();
        if (player == null)
        {
            return;
        }
        MissionResource destination = resource != null ? resource : player.GetComponent<MissionResource>();
        if (destination == null)
        {
            return;
        }

        collected = true;
        GameplaySfx.Play(GameplaySfx.Cue.Pickup, 0.4f);
        destination.Collect(Mathf.Max(1, amount));
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
