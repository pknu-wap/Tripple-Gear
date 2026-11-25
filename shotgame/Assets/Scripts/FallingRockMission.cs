using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MissionObjective))]
public sealed class FallingRockMission : MonoBehaviour
{
    [SerializeField, KoreanLabel("낙석 프리팹")] private Rigidbody2D rockPrefab;
    [SerializeField, KoreanLabel("낙석 생성 범위")] private BoxCollider2D spawnArea;
    [SerializeField, KoreanLabel("낙석 중단 높이"), Tooltip("플레이어가 이 오브젝트의 월드 Y에 도달하면 미션을 완료합니다.")]
    private Transform finishHeight;
    [SerializeField, KoreanLabel("생성 간격 (초)"), Min(0.1f)] private float spawnInterval = 0.5f;
    [SerializeField, KoreanLabel("낙하 속도 (유닛/초)"), Min(0.1f)] private float fallSpeed = 16f;
    [SerializeField, KoreanLabel("낙석 수명 (초)"), Min(0.1f)] private float rockLifetime = 8f;

    private MissionObjective objective;
    private double nextSpawnTime;

    private void Awake() => objective = GetComponent<MissionObjective>();

    private void Update()
    {
        if (!objective.IsRunning)
        {
            nextSpawnTime = 0d;
            return;
        }

        ArisaHealth player = objective.Owner?.MissionPlayerHealth;
        if (player == null || player.IsDead) return;

        if (finishHeight != null && player.transform.position.y >= finishHeight.position.y)
        {
            objective.RecordSuccess();
            return;
        }

        double now = Time.timeAsDouble;
        if (spawnArea == null || rockPrefab == null || now < nextSpawnTime) return;

        Vector2 halfSize = spawnArea.size * 0.5f;
        Vector2 localPoint = spawnArea.offset + new Vector2(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y));
        Vector3 position = spawnArea.transform.TransformPoint(localPoint);
        Rigidbody2D rock = Instantiate(rockPrefab, position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        rock.gravityScale = 0f;
        rock.linearVelocity = Vector2.down * Mathf.Max(0.1f, fallSpeed);
        rock.angularVelocity = Random.Range(-110f, 110f);
        Destroy(rock.gameObject, Mathf.Max(0.1f, rockLifetime));
        nextSpawnTime = now + Mathf.Max(0.1f, spawnInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnArea == null) return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = spawnArea.transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.56f, 0.2f);
        Gizmos.DrawWireCube(spawnArea.offset, spawnArea.size);
        Gizmos.matrix = previousMatrix;

        if (finishHeight != null)
        {
            Bounds bounds = spawnArea.bounds;
            Vector3 goal = finishHeight.position;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(bounds.min.x, goal.y, goal.z), new Vector3(bounds.max.x, goal.y, goal.z));
        }
        Gizmos.color = previousColor;
    }
}
