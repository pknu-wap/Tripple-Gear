using UnityEngine;

public abstract class MonsterProjectile : MonoBehaviour
{
    public abstract void Launch(Vector2 direction, float speed, float lifetime, int damage, ArisaHealth player);
}
