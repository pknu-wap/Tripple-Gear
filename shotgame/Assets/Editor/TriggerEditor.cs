using System.Linq;
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(Trigger), true)]
public sealed class TriggerEditor : Editor
{
    private static readonly GUIContent ModeLabel = new GUIContent("트리거 종류");
    private static readonly GUIContent[] ModeOptions =
    {
        new GUIContent("Wave Trigger"),
        new GUIContent("Mission Trigger"),
        new GUIContent("Story Trigger"),
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            DrawProperty("m_Script");
        }

        TriggerMode mode = ((Trigger)target).Mode;
        bool mixedModes = targets.Any(item => ((Trigger)item).Mode != mode);
        SerializedProperty modeProperty = serializedObject.FindProperty("triggerMode");
        Rect rect = EditorGUILayout.GetControlRect();
        using (var property = new EditorGUI.PropertyScope(rect, ModeLabel, modeProperty))
        using (new EditorGUI.MixedValueScope(mixedModes))
        using (var change = new EditorGUI.ChangeCheckScope())
        {
            int selected = EditorGUI.Popup(rect, property.content, mixedModes ? -1 : (int)mode, ModeOptions);
            if (change.changed)
            {
                modeProperty.intValue = selected;
                serializedObject.ApplyModifiedProperties();
                return;
            }
        }

        DrawProperty("triggerManager");
        DrawProperty("playerLayers");
        DrawProperty("requiredPlayer");
        DrawProperty("onTriggered");

        if (mixedModes)
        {
            EditorGUILayout.HelpBox("종류가 같은 트리거를 선택하면 해당 설정을 함께 편집할 수 있습니다.", MessageType.Info);
        }
        else if (mode == TriggerMode.Wave)
        {
            DrawProperty("cameraManager");
            DrawProperty("monsters");
            DrawProperty("waveStarted");
            DrawProperty("waveCompleted");
            DrawProperty("musicToFadeOut");
            DrawProperty("musicToFadeIn");
            DrawProperty("musicFadeDuration");
            DrawProperty("musicTransitionTiming");
        }
        else
        {
            string section = mode == TriggerMode.Mission ? "미션 설정" : "스토리 설정";
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(section, EditorStyles.boldLabel);
            DrawProperty("triggerOnce");
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProperty(string propertyName)
    {
        KoreanLabelEditorUtility.DrawProperty(serializedObject.FindProperty(propertyName), target.GetType());
    }
}
