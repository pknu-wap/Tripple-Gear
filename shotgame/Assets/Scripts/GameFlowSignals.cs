using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-scoped requests. A scene transition carries one request, never a scene object reference.
public static class GameFlowSignals
{
    public const string BeginGame = "BeginGame";
    private static readonly Dictionary<string, Action> receivers = new Dictionary<string, Action>();
    private static int pendingBuildIndex = -1;
    private static string pendingSignal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        receivers.Clear();
        pendingBuildIndex = -1;
        pendingSignal = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static string Key(Scene scene, string signal) => scene.handle + ":" + signal;

    public static bool Register(Scene scene, string signal, Action receiver)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(signal) || receiver == null) return false;
        string key = Key(scene, signal);
        if (receivers.ContainsKey(key))
        {
            Debug.LogError($"같은 씬에 같은 신호 수신기가 중복되어 있습니다: {scene.name} / {signal}");
            return false;
        }
        receivers.Add(key, receiver);
        return true;
    }

    public static void Unregister(Scene scene, string signal, Action receiver)
    {
        string key = Key(scene, signal);
        if (receivers.TryGetValue(key, out Action registered) && registered == receiver) receivers.Remove(key);
    }

    public static bool Send(Scene scene, string signal)
    {
        if (!string.IsNullOrWhiteSpace(signal) && receivers.TryGetValue(Key(scene, signal), out Action receiver))
        {
            receiver.Invoke();
            return true;
        }
        Debug.LogWarning($"활성화된 신호 수신기가 없습니다: {scene.name} / {signal}");
        return false;
    }

    public static bool LoadSceneAndSend(string sceneName, string signal)
    {
        if (pendingBuildIndex >= 0) return false;
        int index = FindBuildIndex(sceneName);
        if (index < 0 || string.IsNullOrWhiteSpace(signal))
        {
            Debug.LogError($"빌드 목록의 씬과 신호 이름을 확인해 주세요: {sceneName} / {signal}");
            return false;
        }
        pendingBuildIndex = index;
        pendingSignal = signal;
        try
        {
            SceneManager.LoadScene(index, LoadSceneMode.Single);
            return true;
        }
        catch (Exception exception)
        {
            pendingBuildIndex = -1;
            pendingSignal = null;
            Debug.LogException(exception);
            return false;
        }
    }

    private static int FindBuildIndex(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return -1;
        int match = -1;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (path != sceneName && Path.GetFileNameWithoutExtension(path) != sceneName) continue;
            if (match >= 0) return -1; // Ambiguous short name: use the full asset path.
            match = i;
        }
        return match;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingBuildIndex < 0) return;
        int expected = pendingBuildIndex;
        string signal = pendingSignal;
        // Consume before dispatch; callbacks can safely request another transition.
        pendingBuildIndex = -1;
        pendingSignal = null;
        if (scene.buildIndex == expected) Send(scene, signal);
        else Debug.LogWarning("다른 씬이 로드되어 대기 중이던 진행 요청을 취소했습니다.");
    }
}
