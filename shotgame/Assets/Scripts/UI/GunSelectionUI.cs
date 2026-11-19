using UnityEngine;

public class GunSelectionUI : MonoBehaviour
{
    [SerializeField] private GunSO submachineGun;
    [SerializeField] private GunSO shotgun;
    [SerializeField] private GunSO sniper;
    [SerializeField] private GunSO missile;

    public void SelectSubmachineGun()
    {
        GameManager.Instance.SelectGun(submachineGun);
    }

    public void SelectShotgun()
    {
        GameManager.Instance.SelectGun(shotgun);
    }

    public void SelectSniper()
    {
        GameManager.Instance.SelectGun(sniper);
    }

    public void SelectMissile()
    {
        GameManager.Instance.SelectGun(missile);
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