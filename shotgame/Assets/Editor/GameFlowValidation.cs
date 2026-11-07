using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

// Run from a separate batch Editor: -executeMethod GameFlowValidation.Run
[InitializeOnLoad]
public static class GameFlowValidation
{
    private const string RunningKey = "GameFlowValidation.Running";
    private static double deadline;
    private static int playStage;
    private static GameObject gameplayPlayer;
    private static float gameplayCheckUntil;
    private static int readyFrames;

    static GameFlowValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 60;
                playStage = 0;
                readyFrames = 0;
                EditorApplication.update += CheckPlayMode;
            }
        };
    }

    public static void Run()
    {
        try
        {
            TestSequence();
            TestCancellation();
            TestImmediateCompletion();
            TestEmptySequence();
            TestInvalidChapters();
            TestSignalRouting();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/시네마틱샘플.unity");
            var sequence = UnityEngine.Object.FindFirstObjectByType<InGameSequenceController>();
            var cutscene = UnityEngine.Object.FindFirstObjectByType<TimelineCutscenePlayer>();
            Require(sequence != null && cutscene != null, "Migrated components exist");
            Require(Get(sequence, "intro") == cutscene, "Intro reference preserved");
            var button = UnityEngine.Object.FindFirstObjectByType<GameFlowSignalSender>().GetComponent<Button>();
            Require(button != null && button.GetComponent<GameFlowSignalSender>() != null
                && button.onClick.GetPersistentEventCount() == 0, "Button sends a signal without bypassing receiver");
            var receiver = sequence.GetComponent<GameFlowSignalReceiver>();
            var received = (UnityEngine.Events.UnityEvent)Get(receiver, "onReceived");
            Require(received.GetPersistentTarget(0) == sequence
                && received.GetPersistentMethodName(0) == "BeginSequence", "Signal receiver connected to sequence");
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                        "No missing script: " + child.name);
            Debug.Log("FLOW VALIDATION: edit-mode checks passed; starting actual scene directly without lobby.");
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Fail(e); }
    }

    private static void CheckPlayMode()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play-mode timeout");
            if (playStage == 2)
            {
                var lobby = UnityEngine.Object.FindFirstObjectByType<LobbyMenuController>();
                if (lobby == null) return;
                lobby.StartNewGame();
                playStage = 3;
                return;
            }
            var sequence = UnityEngine.Object.FindFirstObjectByType<InGameSequenceController>();
            if (sequence == null) return;
            var cutscene = UnityEngine.Object.FindFirstObjectByType<TimelineCutscenePlayer>();
            if (playStage == 3)
            {
                if (sequence.State == InGameSequenceController.SequenceState.Ready) return;
                Require(SceneManager.GetActiveScene().name == "시네마틱샘플"
                    && sequence.State == InGameSequenceController.SequenceState.Intro && cutscene.IsPlaying,
                    "Lobby scene request starts intro");
                playStage = 5;
                readyFrames = 0;
                SceneManager.LoadScene("시네마틱샘플");
                return;
            }
            if (playStage == 5)
            {
                Require(sequence.State == InGameSequenceController.SequenceState.Ready,
                    "Consumed scene request must not replay after a plain reload");
                if (++readyFrames < 3) return;
                var host = new GameObject("Other scene sender test");
                var sender = host.AddComponent<GameFlowSignalSender>();
                Set(sender, "targetSceneName", "시네마틱샘플");
                playStage = 6;
                sender.Send();
                return;
            }
            if (playStage == 6)
            {
                if (sequence.State == InGameSequenceController.SequenceState.Ready) return;
                Require(sequence.State == InGameSequenceController.SequenceState.Intro,
                    "Reusable sender loads destination and delivers request");
                SessionState.SetBool(RunningKey, false);
                Debug.Log("FLOW VALIDATION PASSED: signal routing, local button, event receiver, lobby request, reusable scene sender, consumed request, natural intro/retry, player active after completion, chapters.");
                EditorApplication.Exit(0);
                return;
            }
            if (playStage == 0)
            {
                Require(sequence.State == InGameSequenceController.SequenceState.Ready && !cutscene.IsPlaying,
                    "Direct scene entry waits for a signal");
                if (++readyFrames < 3) return;
                TestEventReceiver();
                var button = UnityEngine.Object.FindFirstObjectByType<GameFlowSignalSender>().GetComponent<Button>();
                button.onClick.Invoke();
                Require(sequence.State == InGameSequenceController.SequenceState.Intro && cutscene.IsPlaying,
                    "Local button signal starts intro");
                // Exercise cancellation and retry before letting the real Timeline finish.
                cutscene.Cancel();
                Require(sequence.State == InGameSequenceController.SequenceState.Ready, "Intro cancellation allows retry");
                button.onClick.Invoke();
                Require(cutscene.IsPlaying, "Intro retry starts");
                var director = (PlayableDirector)Get(cutscene, "introTimeline");
                gameplayPlayer = null;
                foreach (var track in ((TimelineAsset)director.playableAsset).GetOutputTracks())
                {
                    if (!(track is ActivationTrack activation)) continue;
                    var target = director.GetGenericBinding(track) as GameObject;
                    if (target == null || target.GetComponent<ArisaHorizontalMovement>() == null) continue;
                    Require(activation.postPlaybackState == ActivationTrack.PostPlaybackState.Active,
                        "Player activation track must leave player active");
                    gameplayPlayer = target;
                }
                Require(gameplayPlayer != null, "Player activation binding found");
                // Allow natural playback to catch activation changes at the clip boundary.
                playStage = 1;
                return;
            }
            if (sequence.State == InGameSequenceController.SequenceState.Intro) return;
            Require(sequence.State == InGameSequenceController.SequenceState.Playing && cutscene.IsCompleted,
                "Timeline completes into gameplay");
            Require(gameplayPlayer != null && gameplayPlayer.activeSelf && gameplayPlayer.activeInHierarchy,
                "Player remains active after Timeline completion");
            if (playStage == 1)
            {
                gameplayCheckUntil = Time.unscaledTime + 3f;
                playStage = 4;
            }
            if (Time.unscaledTime < gameplayCheckUntil) return;
            sequence.BeginSequence();
            Require(sequence.State == InGameSequenceController.SequenceState.Playing, "Duplicate start ignored");
            Require(SceneManager.GetActiveScene().name == "시네마틱샘플", "No scene transition");
            playStage = 2;
            SceneManager.LoadScene("로비");
        }
        catch (Exception e) { Fail(e); }
    }

    private static void TestSignalRouting()
    {
        Scene first = EditorSceneManager.OpenScene("Assets/Scenes/로비.unity", OpenSceneMode.Single);
        Scene second = EditorSceneManager.OpenScene("Assets/Scenes/시네마틱샘플.unity", OpenSceneMode.Additive);
        int firstCount = 0, secondCount = 0;
        Action a = () => firstCount++;
        Action b = () => secondCount++;
        try
        {
            Require(GameFlowSignals.Register(first, "TestEvent", a), "Register first route");
            Require(GameFlowSignals.Register(second, "TestEvent", b), "Register second route");
            GameFlowSignals.Send(first, "TestEvent");
            Require(firstCount == 1 && secondCount == 0, "Signals are scoped to a scene");
            Require(!GameFlowSignals.Send(first, "Unknown"), "Unknown signals are rejected");
            GameFlowSignals.Unregister(first, "TestEvent", a);
            Require(!GameFlowSignals.Send(first, "TestEvent"), "Disabled receiver leaves no stale subscription");
            Require(!GameFlowSignals.LoadSceneAndSend("MissingSceneForTest", "TestEvent"), "Invalid destination rejected");
        }
        finally
        {
            GameFlowSignals.Unregister(first, "TestEvent", a);
            GameFlowSignals.Unregister(second, "TestEvent", b);
            EditorSceneManager.CloseScene(second, true);
        }
    }

    private static void TestEventReceiver()
    {
        var host = new GameObject("Generic event test");
        host.SetActive(false);
        try
        {
            var receiver = host.AddComponent<GameFlowSignalReceiver>();
            Set(receiver, "signalName", "OpenGate");
            int count = 0;
            ((UnityEngine.Events.UnityEvent)Get(receiver, "onReceived")).AddListener(() => count++);
            host.SetActive(true);
            GameFlowSignals.Send(host.scene, "OpenGate");
            Require(count == 0, "Early signal waits for receiver Start");
            typeof(GameFlowSignalReceiver).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(receiver, null);
            GameFlowSignals.Send(host.scene, "OpenGate");
            Require(count == 1, "Generic event fires once when configured once");
            host.SetActive(false);
            Require(!GameFlowSignals.Send(host.scene, "OpenGate"), "Disabled event receiver is unregistered");
        }
        finally { UnityEngine.Object.Destroy(host); }
    }

    private static void TestSequence()
    {
        var host = new GameObject("Sequence test");
        try
        {
            var flow = host.AddComponent<InGameSequenceController>();
            var chapters = MakeChapters(host, 3);
            Set(flow, "chapters", chapters);
            int completed = 0;
            flow.SequenceCompleted.AddListener(() => completed++);
            flow.BeginSequence();
            flow.BeginSequence();
            Require(flow.CurrentChapterIndex == 0 && chapters[0].HasStarted && !chapters[1].HasStarted, "First chapter only");
            chapters[2].CompleteChapter();
            Require(flow.CurrentChapterIndex == 0, "Future completion ignored");
            chapters[0].CompleteChapter();
            chapters[0].CompleteChapter();
            Require(flow.CurrentChapterIndex == 1 && chapters[1].HasStarted, "Duplicate completion ignored");
            chapters[1].CompleteChapter();
            Require(flow.CurrentChapterIndex == 2, "Third chapter");
            chapters[2].CompleteChapter();
            chapters[2].CompleteChapter();
            Require(flow.State == InGameSequenceController.SequenceState.Completed && completed == 1, "Complete once");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void TestCancellation()
    {
        var host = new GameObject("Cancel test");
        try
        {
            var flow = host.AddComponent<InGameSequenceController>();
            var chapters = MakeChapters(host, 2);
            Set(flow, "chapters", chapters);
            flow.BeginSequence();
            flow.CancelSequence();
            chapters[0].CompleteChapter();
            Require(flow.State == InGameSequenceController.SequenceState.Cancelled
                && chapters[0].IsCancelled && !chapters[1].HasStarted, "Cancellation blocks advancement");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void TestImmediateCompletion()
    {
        var host = new GameObject("Immediate test");
        try
        {
            var flow = host.AddComponent<InGameSequenceController>();
            var chapters = MakeChapters(host, 3);
            Set(flow, "chapters", chapters);
            foreach (var chapter in chapters) chapter.Started.AddListener(chapter.CompleteChapter);
            flow.BeginSequence();
            Require(flow.State == InGameSequenceController.SequenceState.Completed, "Synchronous callbacks supported");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void TestEmptySequence()
    {
        var host = new GameObject("Empty test");
        try
        {
            var flow = host.AddComponent<InGameSequenceController>();
            flow.BeginSequence();
            Require(flow.State == InGameSequenceController.SequenceState.Playing, "No intro or chapters still starts gameplay");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void TestInvalidChapters()
    {
        var host = new GameObject("Invalid test");
        try
        {
            var flow = host.AddComponent<InGameSequenceController>();
            var chapter = MakeChapters(host, 1)[0];
            Set(flow, "chapters", new[] { chapter, chapter });
            flow.BeginSequence();
            Require(flow.State == InGameSequenceController.SequenceState.Ready && !chapter.HasStarted, "Duplicate configuration rejected");
            Set(flow, "chapters", new ChapterController[] { null });
            flow.BeginSequence();
            Require(flow.State == InGameSequenceController.SequenceState.Ready, "Null configuration rejected");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static ChapterController[] MakeChapters(GameObject host, int count)
    {
        var result = new ChapterController[count];
        for (int i = 0; i < count; i++)
        {
            var child = new GameObject("Chapter " + i);
            child.transform.SetParent(host.transform);
            result[i] = child.AddComponent<ChapterController>();
        }
        return result;
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Fail(Exception e)
    {
        SessionState.SetBool(RunningKey, false);
        Debug.LogException(e);
        EditorApplication.Exit(1);
    }
}
