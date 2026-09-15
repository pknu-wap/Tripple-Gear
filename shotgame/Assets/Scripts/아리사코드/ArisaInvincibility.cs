using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArisaInvincibility : MonoBehaviour
{
    private int skillInvincibleCount;

    public bool IsInvincible => skillInvincibleCount > 0;

    public void SetSkillInvincible(bool isInvincible)
    {
        if (isInvincible)
        {
            skillInvincibleCount++;
            return;
        }

        skillInvincibleCount = Mathf.Max(0, skillInvincibleCount - 1);
    }

    private void OnDisable()
    {
        skillInvincibleCount = 0;
    }
}
