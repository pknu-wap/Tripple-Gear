using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class WaveTrigger : MonoBehaviour
{
    [SerializeField, KoreanLabel("웨이브 매니저")] private WaveManager waveManager;
    [SerializeField, KoreanLabel("플레이어 감지 레이어")] private LayerMask playerLayers = ~0;
    [SerializeField, KoreanLabel("지정 플레이어 (선택)")] private ArisaHorizontalMovement requiredPlayer;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        waveManager = GetComponentInParent<WaveManager>();
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnValidate()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartWave(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartWave(other);
    }

    private void TryStartWave(Collider2D other)
    {
        if (!isActiveAndEnabled || waveManager == null || waveManager.HasStarted
            || (playerLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        ArisaHorizontalMovement player = other.GetComponentInParent<ArisaHorizontalMovement>();
        if (player == null || (requiredPlayer != null && player != requiredPlayer))
        {
            return;
        }
        waveManager.TryStartWave();
    }
}
