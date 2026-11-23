using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillPickupFloat : MonoBehaviour
{
    [SerializeField, KoreanLabel("부유 높이"), Min(0f)] private float height = 0.15f;
    [SerializeField, KoreanLabel("부유 주기"), Min(0.1f)] private float period = 1.45f;

    private Vector3 restingLocalPosition;
    private float enabledAt;

    private void OnEnable()
    {
        restingLocalPosition = transform.localPosition;
        enabledAt = Time.time;
    }

    private void LateUpdate()
    {
        float phase = (Time.time - enabledAt) * (2f * Mathf.PI / period);
        float offset = (1f - Mathf.Cos(phase)) * (height * 0.5f);
        transform.localPosition = restingLocalPosition + Vector3.up * offset;
    }

    private void OnDisable()
    {
        transform.localPosition = restingLocalPosition;
    }
}
