using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatWake : MonoBehaviour
{
    [SerializeField, InspectorName("선체 포말 애니메이터")] private Animator foam;
    [SerializeField, InspectorName("뱃머리 물보라")] private ParticleSystem bowSpray;
    [SerializeField, InspectorName("선미 물살")] private ParticleSystem sternWake;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private float bowRate;
    private float sternRate;
    private ParticleSystem.MinMaxCurve bowDrift;
    private ParticleSystem.MinMaxCurve sternDrift;

    internal bool HasLiveParticles => bowSpray.particleCount > 0 || sternWake.particleCount > 0;

    private void Awake()
    {
        bowRate = bowSpray.emission.rateOverDistance.constant;
        sternRate = sternWake.emission.rateOverDistance.constant;
        bowDrift = bowSpray.velocityOverLifetime.x;
        sternDrift = sternWake.velocityOverLifetime.x;
    }

    public void SetSpeedFraction(float fraction, float backgroundSpeed)
    {
        foam.SetFloat(Speed, Mathf.Clamp01(fraction), 0.15f, Time.fixedDeltaTime);
        ApplyBackgroundSpeed(bowSpray, bowRate, bowDrift, backgroundSpeed);
        ApplyBackgroundSpeed(sternWake, sternRate, sternDrift, backgroundSpeed);
    }

    private static void ApplyBackgroundSpeed(ParticleSystem particles, float rate,
        ParticleSystem.MinMaxCurve drift, float backgroundSpeed)
    {
        var emission = particles.emission;
        emission.rateOverTime = rate * backgroundSpeed;
        var velocity = particles.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(drift.constantMin - backgroundSpeed, drift.constantMax - backgroundSpeed);
    }

    public void Play()
    {
        bowSpray.Play();
        sternWake.Play();
    }

    public void Clear()
    {
        foam.SetFloat(Speed, 0f);
        ApplyBackgroundSpeed(bowSpray, bowRate, bowDrift, 0f);
        ApplyBackgroundSpeed(sternWake, sternRate, sternDrift, 0f);
        bowSpray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        sternWake.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
