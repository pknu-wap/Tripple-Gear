using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArisaFlightArms : MonoBehaviour
{
    [SerializeField] private SpriteRenderer armSprite;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float restAngle = 24f;

    private Vector3 shoulder;
    private Vector3 baseScale;
    private Vector3 armArtPosition;
    private Sprite restingSprite;
    private SubmachineGunFire gun;
    private ArisaMouseAim aim;
    private float reloadTilt;

    internal bool IsConfigured => armSprite != null && armSprite.sprite != null && muzzle != null;
    internal Vector3 MuzzlePosition => muzzle.position;
    internal float MagazineScale => Mathf.Abs(armSprite.transform.lossyScale.y);

    internal Vector3 MagazinePosition(float rise)
    {
        return armSprite.transform.TransformPoint(new Vector3(0.12f, -0.22f))
            + Vector3.up * (rise * MagazineScale);
    }

    internal void Bind(SubmachineGunFire flightGun, ArisaMouseAim flightAim)
    {
        gun = flightGun;
        aim = flightAim;
        shoulder = transform.localPosition;
        baseScale = transform.localScale;
        armArtPosition = armSprite.transform.localPosition;
        restingSprite = armSprite.sprite;
        reloadTilt = 0f;
    }

    internal void RestorePose()
    {
        transform.localPosition = shoulder;
        transform.localScale = baseScale;
        transform.localRotation = Quaternion.Euler(0f, 0f, restAngle);
        armSprite.transform.localPosition = armArtPosition;
        armSprite.sprite = restingSprite;
    }

    internal void Present(Vector3 aimTarget, bool facingLeft, Color color)
    {
        armSprite.color = color;
        if (gun != null)
        {
            Sprite frame = gun.ArisaArmSprite;
            if (frame != null)
            {
                armSprite.sprite = frame;
                Vector2 sizeDelta = frame.bounds.size - restingSprite.bounds.size;
                Vector3 scale = armSprite.transform.localScale;
                armSprite.transform.localPosition = armArtPosition
                    + new Vector3(sizeDelta.x * scale.x, -sizeDelta.y * scale.y) * 0.5f;
            }
            reloadTilt = Mathf.MoveTowards(reloadTilt, gun.IsReloading ? restAngle + 8f : 0f, 180f * Time.deltaTime);
        }
        AimAt(aimTarget, facingLeft);
    }

    internal void AimAt(Vector3 aimTarget, bool facingLeft)
    {
        float sign = facingLeft ? -1f : 1f;
        transform.localPosition = new Vector3(shoulder.x * sign, shoulder.y, shoulder.z);
        transform.localScale = new Vector3(baseScale.x * sign, baseScale.y, baseScale.z);
        Vector3 direction = transform.parent.InverseTransformPoint(aimTarget) - transform.localPosition;
        float elevation = direction.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg : 0f;
        float recoil = aim != null ? aim.GetFireShakeAngleOffset() : 0f;
        float angle = (Mathf.Clamp(elevation, -70f, 70f) + restAngle - reloadTilt + recoil) * sign;
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
