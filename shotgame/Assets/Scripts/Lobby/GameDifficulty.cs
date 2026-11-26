using UnityEngine;

internal static class GameDifficulty
{
    internal static bool IsEasy { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => IsEasy = false;

    internal static int ScaleMonsterHp(int baseHp)
    {
        if (!IsEasy || baseHp <= 0)
        {
            return baseHp;
        }

        return (int)((2L * baseHp + 1) / 3);
    }
}
