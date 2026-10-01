using UnityEngine;
using TMPro;

public class BulletUIController : MonoBehaviour
{
    [Header("현재 탄약을 관리하는 총")]
    [SerializeField] private SubmachineGunFire gun;

    [Header("화면에 표시할 TextMeshPro 텍스트")]
    [SerializeField] private TextMeshProUGUI bulletText;

    private void Update()
    {
        UpdateBulletUI();
    }

    private void UpdateBulletUI()
    {
        if (gun == null || bulletText == null)
        {
            return;
        }

        bulletText.text = $"{gun.CurrentBulletCount} / {gun.MaxBulletCount}";
    }
}