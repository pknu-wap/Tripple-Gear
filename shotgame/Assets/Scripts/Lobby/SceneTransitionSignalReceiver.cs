using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

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
    private static string pendingFromSceneName;
    private static string pendingToSceneName;
    private static bool pendingSceneLoaded;

    public string LastFromSceneName => pendingFromSceneName;
    public string LastToSceneName => pendingToSceneName;
    public bool HasReceivedTransitionSignal => pendingToSceneName != null;
    public SceneTransitionEvent SceneTransitioned => sceneTransitioned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        ClearPendingSignal();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void ClearPendingSignal()
    {
        pendingFromSceneName = null;
        pendingToSceneName = null;
        pendingSceneLoaded = false;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingToSceneName == null || mode != LoadSceneMode.Single)
        {
            return;
        }

        if (pendingSceneLoaded || !string.Equals(pendingToSceneName, scene.name, System.StringComparison.Ordinal))
        {
            ClearPendingSignal();
            return;
        }

        pendingSceneLoaded = true;
    }

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

    public static void LoadSceneWithSignal(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"로드할 수 없는 씬입니다: {sceneName}");
            return;
        }

        string destination = System.IO.Path.GetFileName(sceneName);
        if (destination.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
        {
            destination = System.IO.Path.GetFileNameWithoutExtension(destination);
        }
        NotifySceneTransition(SceneManager.GetActiveScene().name, destination);
        SceneManager.LoadScene(sceneName);
    }

    public static void NotifySceneTransition(string fromSceneName, string toSceneName)
    {
        SetPendingSignal(fromSceneName, toSceneName);
        SceneTransitionSignalReceiver receiver = FindReceiver();
        if (receiver != null)
        {
            receiver.PublishTransition(fromSceneName, toSceneName);
        }
    }

    public static bool TryConsumeSceneTransition(string expectedFromSceneName, string expectedToSceneName)
    {
        if (pendingToSceneName == null)
        {
            return false;
        }

        bool matchesSource = string.IsNullOrWhiteSpace(expectedFromSceneName)
            || string.Equals(pendingFromSceneName, expectedFromSceneName, System.StringComparison.Ordinal);
        bool matchesDestination = string.Equals(pendingToSceneName, expectedToSceneName, System.StringComparison.Ordinal);

        if (!matchesSource || !matchesDestination)
        {
            return false;
        }

        ClearPendingSignal();
        return true;
    }

    public void ReceiveSceneTransition(string fromSceneName, string toSceneName)
    {
        SetPendingSignal(fromSceneName, toSceneName);
        PublishTransition(fromSceneName, toSceneName);
    }

    private static void SetPendingSignal(string fromSceneName, string toSceneName)
    {
        pendingFromSceneName = fromSceneName;
        pendingToSceneName = toSceneName;
        pendingSceneLoaded = false;
    }

    private void PublishTransition(string fromSceneName, string toSceneName)
    {
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
