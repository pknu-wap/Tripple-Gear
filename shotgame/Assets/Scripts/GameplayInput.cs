using System.Collections.Generic;
using UnityEngine;

internal static class GameplayInput
{
    private static readonly HashSet<MonoBehaviour> blockers = new HashSet<MonoBehaviour>();
    private static int lastUnblockedFrame = -1;

    internal static bool IsBlocked => blockers.Count > 0
        || lastUnblockedFrame == Time.frameCount || DialogueManager.BlocksGameplayInput;

    internal static void Block(MonoBehaviour owner) => blockers.Add(owner);

    internal static void Unblock(MonoBehaviour owner)
    {
        if (blockers.Remove(owner)) lastUnblockedFrame = Time.frameCount;
    }
}
