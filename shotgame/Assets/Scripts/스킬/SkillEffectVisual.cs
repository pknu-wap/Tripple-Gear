using UnityEngine;

internal static class SkillEffectVisual
{
    private static Material material;

    internal static Material Material
    {
        get
        {
            if (material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
            return material;
        }
    }

    internal static GameObject CreateRisingSparks(Transform owner, float duration, string effectName,
        Color firstColor, Color secondColor)
    {
        GameObject effect = new GameObject(effectName);
        effect.SetActive(false);
        effect.transform.SetParent(owner, false);
        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.48f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.13f);
        main.startColor = new ParticleSystem.MinMaxGradient(firstColor, secondColor);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 36f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.85f, 0.22f, 0.02f);
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = 2.8f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Material;
        renderer.sortingOrder = 46;
        effect.SetActive(true);
        particles.Play();
        Object.Destroy(effect, duration);
        return effect;
    }
}
