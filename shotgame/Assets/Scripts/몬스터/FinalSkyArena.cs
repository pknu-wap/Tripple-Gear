using UnityEngine;

[DisallowMultipleComponent]
public sealed class FinalSkyArena : MonoBehaviour
{
    [SerializeField] private Camera framingCamera;
    [SerializeField] private SpriteRenderer clouds;
    [SerializeField] private Shader cloudScrollShader;
    [SerializeField, KoreanLabel("구름 이동 속도"), Min(0f)] private float cloudDriftSpeed = 0.038f;
    [SerializeField] private Transform arisaSpawn;
    [SerializeField] private SpriteRenderer arisaArt;
    [SerializeField] private ArisaFlightArms arisaArms;
    [SerializeField] private MonsterHealth boss;
    [SerializeField] private Transform laserMuzzle;
    [SerializeField] private Transform boostNozzle;
    [SerializeField] private Transform upperMissilePod;
    [SerializeField] private Transform lowerMissilePod;
    [SerializeField] private Transform attackEffects;

    internal Camera FramingCamera => framingCamera;
    internal SpriteRenderer Clouds => clouds;
    internal Shader CloudScrollShader => cloudScrollShader;
    internal float CloudDriftSpeed => cloudDriftSpeed;
    internal Transform ArisaSpawn => arisaSpawn;
    internal SpriteRenderer ArisaArt => arisaArt;
    internal ArisaFlightArms ArisaArms => arisaArms;
    internal MonsterHealth Boss => boss;
    internal Transform LaserMuzzle => laserMuzzle;
    internal Transform BoostNozzle => boostNozzle;
    internal Transform UpperMissilePod => upperMissilePod;
    internal Transform LowerMissilePod => lowerMissilePod;
    internal Transform AttackEffects => attackEffects;

    internal bool IsConfigured => framingCamera != null && framingCamera.orthographic
        && clouds != null && clouds.sprite != null && arisaSpawn != null && arisaArt != null && arisaArt.sprite != null
        && arisaArms != null && arisaArms.IsConfigured
        && boss != null && boss.Data != null && boss.GetComponent<Collider2D>() != null
        && laserMuzzle != null && boostNozzle != null && upperMissilePod != null && lowerMissilePod != null
        && attackEffects != null;
}
