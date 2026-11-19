using UnityEngine;

[DisallowMultipleComponent]
public sealed class MissionAnimationRelay : MonoBehaviour
{
    [SerializeField, KoreanLabel("미션 트리거")] private Trigger missionTrigger;

    public void FinishMissionPresentation()
    {
        if (missionTrigger != null)
        {
            missionTrigger.FinishMissionPresentation();
        }
    }
}
