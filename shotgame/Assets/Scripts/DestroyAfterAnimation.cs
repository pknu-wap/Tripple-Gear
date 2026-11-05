using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class DestroyAfterAnimation : MonoBehaviour
{
    private IEnumerator Start()
    {
        Animator animator = GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Destroy(gameObject);
            yield break;
        }

        // Let the Animator enter its initial state before checking playback progress.
        yield return null;

        while (animator != null && animator.isActiveAndEnabled)
        {
            if (!animator.IsInTransition(0)
                && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
            {
                break;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
