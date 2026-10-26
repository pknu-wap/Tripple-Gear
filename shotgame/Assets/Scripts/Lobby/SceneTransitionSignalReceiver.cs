using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class SceneTransitionSignalReceiver : MonoBehaviour
{
    [System.Serializable]
    public sealed class SceneTransitionEvent : UnityEvent<string, string>
    {
    }

    [SerializeField] private bool keepAcrossScenes = true;
    [SerializeField] private SceneTransitionEvent sceneTransitioned = new SceneTransitionEvent();

    public static SceneTransitionSignalReceiver Instance { get; private set; }
    public string LastFromSceneName { get; private set; }
    public string LastToSceneName { get; private set; }
    public bool HasReceivedTransitionSignal { get; private set; }
    public SceneTransitionEvent SceneTransitioned => sceneTransitioned;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (keepAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void NotifySceneTransition(string fromSceneName, string toSceneName)
    {
        SceneTransitionSignalReceiver receiver = FindReceiver();

        if (receiver == null)
        {
            Debug.LogWarning($"씬 전환 신호 수신 오브젝트가 없습니다: {fromSceneName} -> {toSceneName}");
            return;
        }

        receiver.ReceiveSceneTransition(fromSceneName, toSceneName);
    }

    public static bool TryConsumeSceneTransition(string expectedFromSceneName, string expectedToSceneName)
    {
        SceneTransitionSignalReceiver receiver = FindReceiver();
        if (receiver == null || !receiver.HasReceivedTransitionSignal)
        {
            return false;
        }

        bool matches = string.Equals(receiver.LastFromSceneName, expectedFromSceneName, System.StringComparison.Ordinal)
            && string.Equals(receiver.LastToSceneName, expectedToSceneName, System.StringComparison.Ordinal);

        if (!matches)
        {
            return false;
        }

        receiver.HasReceivedTransitionSignal = false;
        receiver.LastFromSceneName = null;
        receiver.LastToSceneName = null;
        return true;
    }

    public void ReceiveSceneTransition(string fromSceneName, string toSceneName)
    {
        LastFromSceneName = fromSceneName;
        LastToSceneName = toSceneName;
        HasReceivedTransitionSignal = true;

        Debug.Log($"씬 전환 신호 수신: {fromSceneName} -> {toSceneName}", this);
        sceneTransitioned?.Invoke(fromSceneName, toSceneName);
    }

    private static SceneTransitionSignalReceiver FindReceiver()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        return FindFirstObjectByType<SceneTransitionSignalReceiver>();
#else
        return FindObjectOfType<SceneTransitionSignalReceiver>();
#endif
    }
}
