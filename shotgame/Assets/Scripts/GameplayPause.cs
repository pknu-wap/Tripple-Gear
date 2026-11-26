using System.Collections.Generic;
using UnityEngine;

internal static class GameplayPause
{
    private static readonly HashSet<object> owners = new HashSet<object>();
    private static float previousTimeScale;

    internal static bool IsPaused => owners.Count > 0;
    internal static bool IsPausedOnlyForDialogue =>
        IsPaused && owners.Count == DialogueManager.ActiveDialogueCount;

    internal static void Acquire(object owner)
    {
        if (owners.Count == 0) previousTimeScale = Time.timeScale;
        owners.Add(owner);
        Time.timeScale = 0f;
    }

    internal static void Release(object owner)
    {
        if (owners.Remove(owner) && owners.Count == 0) Time.timeScale = previousTimeScale;
    }
}
