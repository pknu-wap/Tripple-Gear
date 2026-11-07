using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public abstract class TriggerBase : MonoBehaviour
{
    [SerializeField, KoreanLabel("트리거 매니저"), Tooltip("부모 계층에 Trigger Manager가 없을 때 사용합니다.")]
    private TriggerManager triggerManager;
    [SerializeField, KoreanLabel("플레이어 감지 레이어")] private LayerMask playerLayers = ~0;
    [SerializeField, KoreanLabel("지정 플레이어 (선택)")] private ArisaHorizontalMovement requiredPlayer;
    [SerializeField, KoreanLabel("트리거 발동 이벤트")] private UnityEvent onTriggered = new UnityEvent();

    public UnityEvent Triggered => onTriggered;

    protected TriggerManager Manager
    {
        get
        {
            TriggerManager parentManager = GetComponentInParent<TriggerManager>(true);
            return parentManager != null ? parentManager : triggerManager;
        }
    }

    protected virtual void Reset()
    {
        ConfigureCollider();
    }

    protected void Awake()
    {
        ConfigureCollider();
    }

    protected void OnValidate()
    {
        ConfigureCollider();
    }

    private void ConfigureCollider()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    protected void OnTriggerEnter2D(Collider2D other)
    {
        TryActivate(other, Manager);
    }

    protected void TryActivate(Collider2D other, TriggerManager manager)
    {
        if (!isActiveAndEnabled || (playerLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        ArisaHorizontalMovement player = other.GetComponentInParent<ArisaHorizontalMovement>();
        if (player != null && (requiredPlayer == null || player == requiredPlayer))
        {
            TryActivate(manager);
        }
    }

    protected bool TryActivate(TriggerManager manager)
    {
        if (!isActiveAndEnabled || manager == null || !manager.TryActivate(this))
        {
            return false;
        }

        onTriggered.Invoke();
        return true;
    }

    internal abstract bool Activate(TriggerManager manager);
}
