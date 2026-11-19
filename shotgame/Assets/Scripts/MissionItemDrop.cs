using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth))]
public sealed class MissionItemDrop : MonoBehaviour
{
    [SerializeField, KoreanLabel("드롭 아이템 프리팹")] private MissionItemPickup pickupPrefab;
    [SerializeField, KoreanLabel("미션 자원")] private MissionResource resource;
    [SerializeField, KoreanLabel("드롭 위치 (선택)")] private Transform dropPoint;

    private MonsterHealth health;
    private bool dropped;

    private void Awake() => health = GetComponent<MonsterHealth>();
    private void OnEnable() => health.Defeated.AddListener(Drop);
    private void OnDisable() => health.Defeated.RemoveListener(Drop);

    private void Drop()
    {
        if (dropped || pickupPrefab == null || resource == null)
        {
            return;
        }

        dropped = true;
        Vector3 position = dropPoint != null ? dropPoint.position : transform.position;
        MissionItemPickup pickup = Instantiate(pickupPrefab, position, Quaternion.identity);
        pickup.SetResource(resource);
    }
}
