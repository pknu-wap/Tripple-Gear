using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class StairOneWayPlatform : MonoBehaviour
{
    private void Awake()
    {
        PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
        if (effector == null)
        {
            effector = gameObject.AddComponent<PlatformEffector2D>();
        }

        effector.useOneWay = true;
        effector.useOneWayGrouping = true;
        effector.surfaceArc = 180f;
        GetComponent<BoxCollider2D>().usedByEffector = true;
    }
}
