using UnityEngine;

[DisallowMultipleComponent]
public sealed class TriggerManager : MonoBehaviour
{
    [SerializeField, HideInInspector] private TriggerMode triggerMode;

    internal TriggerMode LegacyMode => triggerMode;

    internal bool TryActivate(TriggerBase trigger)
    {
        return isActiveAndEnabled && trigger.Activate(this);
    }
}
