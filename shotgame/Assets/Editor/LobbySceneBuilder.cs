using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbySceneBuilder
{
    private const string LobbyScenePath = "Assets/Scenes/로비.unity";
    private const string InGameSampleScenePath = "Assets/Scenes/인게임샘플.unity";
    private const string TestBattleScenePath = "Assets/Scenes/테스트 배틀씬.unity";
    private const string TestBattleSceneName = "테스트 배틀씬";

    public static void BuildLobbyAndBattleScenes()
    {
        EnsureTestBattleScene();
        CreateLobbyScene();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureTestBattleScene()
    {
        if (File.Exists(TestBattleScenePath))
        {
            return;
        }

        if (File.Exists(InGameSampleScenePath))
        {
            AssetDatabase.CopyAsset(InGameSampleScenePath, TestBattleScenePath);
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = TestBattleSceneName;
        EditorSceneManager.SaveScene(scene, TestBattleScenePath);
    }

    private static void CreateLobbyScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Camera camera = CreateCamera();
        Canvas canvas = CreateCanvas();
        CreateBlackBackground(canvas.transform);

        LobbyMenuController controller = CreateLobbyController();
        CreateTransitionSignalReceiver();

        Text title = CreateText(canvas.transform, "Title", "Tripple gear", 56, TextAnchor.MiddleCenter);
        RectTransform titleTransform = title.rectTransform;
        titleTransform.anchorMin = new Vector2(0.5f, 0.5f);
        titleTransform.anchorMax = new Vector2(0.5f, 0.5f);
        titleTransform.anchoredPosition = new Vector2(0f, 160f);
        titleTransform.sizeDelta = new Vector2(720f, 90f);

        Button newGameButton = CreateButton(canvas.transform, "NewGameButton", "새게임", new Vector2(0f, 60f));
        Button loadGameButton = CreateButton(canvas.transform, "LoadGameButton", "불러오기", new Vector2(0f, 0f));
        Button optionsButton = CreateButton(canvas.transform, "OptionsButton", "옵션", new Vector2(0f, -60f));
        Button quitButton = CreateButton(canvas.transform, "QuitButton", "종료", new Vector2(0f, -120f));

        SerializedObject controllerObject = new SerializedObject(controller);
        controllerObject.FindProperty("newGameSceneName").stringValue = TestBattleSceneName;
        controllerObject.FindProperty("newGameButton").objectReferenceValue = newGameButton;
        controllerObject.FindProperty("loadGameButton").objectReferenceValue = loadGameButton;
        controllerObject.FindProperty("optionsButton").objectReferenceValue = optionsButton;
        controllerObject.FindProperty("quitButton").objectReferenceValue = quitButton;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(newGameButton.onClick, controller.StartNewGame);
        UnityEventTools.AddPersistentListener(loadGameButton.onClick, controller.LoadGame);
        UnityEventTools.AddPersistentListener(optionsButton.onClick, controller.OpenOptions);
        UnityEventTools.AddPersistentListener(quitButton.onClick, controller.QuitGame);

        Selection.activeObject = camera;
        EditorSceneManager.SaveScene(scene, LobbyScenePath);
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Lobby Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();

        return canvas;
    }

    private static void CreateBlackBackground(Transform parent)
    {
        GameObject backgroundObject = new GameObject("Black Background");
        backgroundObject.transform.SetParent(parent, false);

        Image background = backgroundObject.AddComponent<Image>();
        background.color = Color.black;

        RectTransform rectTransform = background.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static LobbyMenuController CreateLobbyController()
    {
        GameObject controllerObject = new GameObject("LobbyMenuController");
        return controllerObject.AddComponent<LobbyMenuController>();
    }

    private static SceneTransitionSignalReceiver CreateTransitionSignalReceiver()
    {
        GameObject receiverObject = new GameObject("SceneTransitionSignalReceiver");
        return receiverObject.AddComponent<SceneTransitionSignalReceiver>();
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string text,
        int fontSize,
        TextAnchor alignment
    )
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        Text textComponent = textObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = GetDefaultFont();
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;

        return textComponent;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.14f, 0.14f, 0.14f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.14f, 0.14f, 0.14f, 1f);
        colors.highlightedColor = new Color(0.24f, 0.24f, 0.24f, 1f);
        colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        RectTransform buttonTransform = button.GetComponent<RectTransform>();
        buttonTransform.anchorMin = new Vector2(0.5f, 0.5f);
        buttonTransform.anchorMax = new Vector2(0.5f, 0.5f);
        buttonTransform.anchoredPosition = position;
        buttonTransform.sizeDelta = new Vector2(320f, 48f);

        Text labelText = CreateText(buttonObject.transform, "Text", label, 24, TextAnchor.MiddleCenter);
        RectTransform labelTransform = labelText.rectTransform;
        labelTransform.anchorMin = Vector2.zero;
        labelTransform.anchorMax = Vector2.one;
        labelTransform.offsetMin = Vector2.zero;
        labelTransform.offsetMax = Vector2.zero;

        return button;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void UpdateBuildSettings()
    {
        string[] scenePaths =
        {
            LobbyScenePath,
            "Assets/Scenes/아리사움직임.unity",
            InGameSampleScenePath,
            TestBattleScenePath,
        };

        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[scenePaths.Length];
        for (int i = 0; i < scenePaths.Length; i++)
        {
            scenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
        }

        EditorBuildSettings.scenes = scenes;
    }
}
