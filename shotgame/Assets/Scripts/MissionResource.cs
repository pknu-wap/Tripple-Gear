using UnityEngine;

[DisallowMultipleComponent]
public sealed class MissionResource : MonoBehaviour
{
    public int Amount { get; private set; }

    public void Collect(int amount)
    {
        if (amount > 0)
        {
            Amount += Mathf.Min(amount, int.MaxValue - Amount);
        }
    }

    public int Withdraw(int maximum)
    {
        int amount = Mathf.Clamp(maximum, 0, Amount);
        Amount -= amount;
        return amount;
    }
}
