using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillRuntime : MonoBehaviour
{
    private Coroutine activeCoroutine;
    private IEnumerator activeRoutine;
    private Action activeCleanup;

    public bool IsPlaying => activeCoroutine != null;

    public void Play(IEnumerator routine, Action cleanup)
    {
        StopPlaying();
        activeRoutine = routine;
        activeCleanup = cleanup;
        activeCoroutine = StartCoroutine(PlayRoutine(routine));
    }

    public void StopPlaying()
    {
        if (activeCoroutine == null)
        {
            return;
        }

        StopCoroutine(activeCoroutine);
        Finish();
    }

    private IEnumerator PlayRoutine(IEnumerator routine)
    {
        while (routine != null)
        {
            object current;
            bool moveNext;

            try
            {
                moveNext = routine.MoveNext();
                current = moveNext ? routine.Current : null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                break;
            }

            if (!moveNext)
            {
                break;
            }

            yield return current;
        }

        Finish();
    }

    private void Finish()
    {
        Action cleanup = activeCleanup;
        IEnumerator routine = activeRoutine;
        activeCoroutine = null;
        activeRoutine = null;
        activeCleanup = null;
        (routine as IDisposable)?.Dispose();
        cleanup?.Invoke();
    }

    private void OnDisable()
    {
        StopPlaying();
    }
}
