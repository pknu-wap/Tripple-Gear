using UnityEngine;

public class GunSelectionUI : MonoBehaviour
{
    [SerializeField] private GunSO submachineGun;
    [SerializeField] private GunSO shotgun;
    [SerializeField] private GunSO sniper;
    [SerializeField] private GunSO missile;

    public void SelectSubmachineGun()
    {
        SelectGun(submachineGun);
    }

    public void SelectShotgun()
    {
        SelectGun(shotgun);
    }

    public void SelectSniper()
    {
        SelectGun(sniper);
    }

    public void SelectMissile()
    {
        SelectGun(missile);
    }

    private void SelectGun(GunSO gun)
    {
        if (gun == null)
        {
            Debug.LogError("선택한 총 데이터가 연결되지 않았습니다.");
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager를 찾을 수 없습니다.");
            return;
        }

        GameManager.Instance.SelectGun(gun);
    }
}
