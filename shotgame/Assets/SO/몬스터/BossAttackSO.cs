using UnityEngine;

[CreateAssetMenu(fileName = "챕터1보스패턴SO", menuName = "Shotgame/몬스터/보스 패턴")]
public sealed class BossAttackSO : ScriptableObject
{
    [Header("공통")]
    [SerializeField, KoreanLabel("첫 공격 대기"), Min(0f)] private float initialDelay = 2f;
    [SerializeField, KoreanLabel("패턴 사이 대기"), Min(0.1f)] private float recoveryTime = 1.8f;
    [SerializeField, KoreanLabel("공격 예고 시간"), Min(0.1f)] private float warningTime = 0.8f;
    [SerializeField, KoreanLabel("지형 레이어")] private LayerMask groundLayers;

    [Header("미사일")]
    [SerializeField, KoreanLabel("미사일 프리팹")] private GameObject missilePrefab;
    [SerializeField, KoreanLabel("폭발 프리팹")] private GameObject explosionPrefab;
    [SerializeField, KoreanLabel("미사일 너비"), Min(0.1f)] private float missileWidth = 1.4f;
    [SerializeField, KoreanLabel("미사일 피해"), Min(1)] private int missileDamage = 1;
    [SerializeField, KoreanLabel("폭발 반경"), Min(0.1f)] private float explosionRadius = 0.9f;
    [SerializeField, KoreanLabel("폭발 표시 시간"), Min(0.1f)] private float explosionLifetime = 1.5f;
    [SerializeField, KoreanLabel("폭발 이펙트 크기"), Min(0.1f)] private float explosionScale = 0.6f;
    [SerializeField, KoreanLabel("발사 간격"), Min(0.1f)] private float missileInterval = 0.45f;

    [Header("낙하 미사일")]
    [SerializeField, KoreanLabel("최소 발사 수"), Min(1)] private int rainMinCount = 4;
    [SerializeField, KoreanLabel("최대 발사 수"), Min(1)] private int rainMaxCount = 7;
    [SerializeField, KoreanLabel("낙하 속도"), Min(0.1f)] private float rainSpeed = 5f;

    [Header("가로 미사일")]
    [SerializeField, KoreanLabel("최소 생성 수"), Min(1)] private int horizontalMinCount = 7;
    [SerializeField, KoreanLabel("최대 생성 수"), Min(1)] private int horizontalMaxCount = 10;
    [SerializeField, KoreanLabel("이동 속도"), Min(0.1f)] private float horizontalSpeed = 3f;

    [Header("레이저")]
    [SerializeField, KoreanLabel("레이저 프리팹")] private GameObject laserPrefab;
    [SerializeField, KoreanLabel("발사 시간"), Min(0.1f)] private float laserDuration = 2f;
    [SerializeField, KoreanLabel("레이저 두께"), Min(0.02f)] private float laserWidth = 0.22f;
    [SerializeField, KoreanLabel("레이저 피해"), Min(1)] private int laserDamage = 1;
    [SerializeField, KoreanLabel("레이저 피격 간격"), Min(0.1f)] private float laserHitInterval = 0.45f;

    [Header("원거리 몹 소환")]
    [SerializeField, KoreanLabel("원거리 몹 프리팹")] private GameObject rangedMonsterPrefab;
    [SerializeField, KoreanLabel("소환 수"), Range(1, 4)] private int summonCount = 2;

    public float InitialDelay => Mathf.Max(0f, initialDelay);
    public float RecoveryTime => Mathf.Max(0.1f, recoveryTime);
    public float WarningTime => Mathf.Max(0.1f, warningTime);
    public LayerMask GroundLayers => groundLayers.value != 0 ? groundLayers : LayerMask.GetMask("Box", "Stair");
    public GameObject MissilePrefab => missilePrefab;
    public GameObject ExplosionPrefab => explosionPrefab;
    public float MissileWidth => Mathf.Max(0.1f, missileWidth);
    public int MissileDamage => Mathf.Max(1, missileDamage);
    public float ExplosionRadius => Mathf.Max(0.1f, explosionRadius);
    public float ExplosionLifetime => Mathf.Max(0.1f, explosionLifetime);
    public float ExplosionScale => Mathf.Max(0.1f, explosionScale);
    public float MissileInterval => Mathf.Max(0.1f, missileInterval);
    public int RainMinCount => Mathf.Max(1, rainMinCount);
    public int RainMaxCount => Mathf.Max(RainMinCount, rainMaxCount);
    public float RainSpeed => Mathf.Max(0.1f, rainSpeed);
    public int HorizontalMinCount => Mathf.Max(1, horizontalMinCount);
    public int HorizontalMaxCount => Mathf.Max(HorizontalMinCount, horizontalMaxCount);
    public float HorizontalSpeed => Mathf.Max(0.1f, horizontalSpeed);
    public GameObject LaserPrefab => laserPrefab;
    public float LaserDuration => Mathf.Max(0.1f, laserDuration);
    public float LaserWidth => Mathf.Max(0.02f, laserWidth);
    public int LaserDamage => Mathf.Max(1, laserDamage);
    public float LaserHitInterval => Mathf.Max(0.1f, laserHitInterval);
    public GameObject RangedMonsterPrefab => rangedMonsterPrefab;
    public int SummonCount => Mathf.Clamp(summonCount, 1, 4);
}
