using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class TriggerManager : MonoBehaviour
{
    // Preserves the mode of triggers saved before they had their own setting.
    [SerializeField, HideInInspector] private TriggerMode triggerMode;

    internal TriggerMode LegacyMode => triggerMode;

    private readonly Dictionary<Camera, Trigger> activeWaves = new Dictionary<Camera, Trigger>();
    private readonly List<Trigger> wavesToUpdate = new List<Trigger>();

    internal bool TryActivate(TriggerBase trigger)
    {
        return isActiveAndEnabled && trigger.Activate(this);
    }

    internal bool TryTrackWave(Camera camera, Trigger wave)
    {
        return activeWaves.TryAdd(camera, wave);
    }

    internal void UntrackWave(Camera camera)
    {
        activeWaves.Remove(camera);
    }

    private void LateUpdate()
    {
        // Completion listeners can start or cancel other waves during this update.
        wavesToUpdate.Clear();
        wavesToUpdate.AddRange(activeWaves.Values);
        foreach (Trigger wave in wavesToUpdate)
        {
            if (wave != null)
            {
                wave.UpdateWave();
            }
        }
        wavesToUpdate.Clear();
    }

    private void OnDisable()
    {
        foreach (Trigger wave in new List<Trigger>(activeWaves.Values))
        {
            if (wave != null)
            {
                wave.CancelWave();
            }
        }
    }
}
