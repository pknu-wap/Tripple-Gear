using UnityEngine;

[CreateAssetMenu(fileName = "NewBulletData", menuName = "UI/Bullet Data")]
public class BulletData : ScriptableObject
{
    [Header("총알 정보")]
    public int currentBulletCount = 30; // 현재 총알 개수
    public int maxBulletCount = 30;     // 최대 총알 개수
}