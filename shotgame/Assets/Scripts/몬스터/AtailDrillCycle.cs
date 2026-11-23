using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class AtailDrillCycle : MonoBehaviour
{
    private static readonly int RotationState = Animator.StringToHash("Base Layer.AtailDrillRotation");

    [SerializeField, Min(0.1f)] private float rotationsPerSecond = 1.35f;
    private Animator animator;

    private void Awake() => animator = GetComponent<Animator>();

    private void OnEnable()
    {
        animator.speed = 0f;
        animator.Update(0f);
        SampleRotation();
    }

    private void LateUpdate() => SampleRotation();

    private void SampleRotation()
    {
        // Sample the same clock through the cut and while the combat renderer is underwater.
        float phase = (float)(Time.timeAsDouble * rotationsPerSecond % 1d);
        animator.Play(RotationState, 0, phase);
        animator.Update(0f);
    }
}
