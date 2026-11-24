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
            DrawProperty("waveLockCamera");
            DrawProperty("cameraManager");
            DrawProperty("monsters");
            DrawWaveEntrances();
            DrawProperty("enemySpawnGroups");
            DrawProperty("waveStarted");
            DrawProperty("waveCompleted");
            DrawProperty("musicToFadeOut");
            DrawProperty("musicToFadeIn");
            DrawProperty("musicFadeDuration");
            DrawProperty("musicTransitionTiming");
        }
        else if (mode == TriggerMode.Mission)
        {
            DrawProperty("missionIntroTimeline");
            DrawProperty("missionObjectives");
            if (serializedObject.FindProperty("missionObjectives").arraySize == 0)
            {
                DrawProperty("triggerOnce");
            }
            DrawProperty("missionCombination");
            DrawProperty("enemySpawnGroups");
            DrawProperty("continueSpawningAfterMissionClear");
            DrawProperty("clearMissionSpawnedEnemies");
            DrawProperty("missionLockCamera");
            SerializedProperty lockCamera = serializedObject.FindProperty("missionLockCamera");
            if (lockCamera.boolValue || lockCamera.hasMultipleDifferentValues)
            {
                DrawProperty("cameraManager");
            }
            DrawProperty("missionPresentation");
            SerializedProperty presentation = serializedObject.FindProperty("missionPresentation");
            if (!presentation.hasMultipleDifferentValues)
            {
                switch ((Trigger.ClearPresentation)presentation.intValue)
                {
                    case Trigger.ClearPresentation.Animator:
                        DrawProperty("missionAnimator");
                        DrawProperty("missionAnimatorTrigger");
                        break;
                    case Trigger.ClearPresentation.Timeline:
                        DrawProperty("missionTimeline");
                        break;
                }
            }
            DrawProperty("missionStarted");
            DrawProperty("missionCompleted");
        }
        else
        {
            DrawProperty("triggerOnce");
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawWaveEntrances()
    {
        SerializedProperty monsters = serializedObject.FindProperty("monsters");
        if (!monsters.isExpanded || monsters.arraySize == 0 || serializedObject.isEditingMultipleObjects)
        {
            return;
        }

        SerializedProperty entrances = serializedObject.FindProperty("waveEntrances");
        EditorGUILayout.LabelField("배치 몬스터 등장 연출", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            for (int i = 0; i < monsters.arraySize; i++)
            {
                Object monster = monsters.GetArrayElementAtIndex(i).objectReferenceValue;
                if (monster == null) continue;

                int index = FindEntranceIndex(entrances, monster);
                SerializedProperty entry = index >= 0 ? entrances.GetArrayElementAtIndex(index) : null;
                SerializedProperty settings = entry?.FindPropertyRelative("entrance");
                var type = settings != null
                    ? (MonsterEntranceType)settings.FindPropertyRelative("type").intValue
                    : MonsterEntranceType.Normal;
                var selected = (MonsterEntranceType)EditorGUILayout.EnumPopup($"Element {i} · {monster.name}", type);
                if (selected != type)
                {
                    if (selected == MonsterEntranceType.Normal)
                    {
                        entrances.DeleteArrayElementAtIndex(index);
                    }
                    else
                    {
                        if (entry == null) entry = AddEntrance(entrances, monster);
                        settings = entry.FindPropertyRelative("entrance");
                        settings.FindPropertyRelative("type").intValue = (int)selected;
                    }
                }
                if (selected == MonsterEntranceType.Normal) continue;

                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("start"), new GUIContent("출현 위치 (선택)"));
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("duration"));
                    if (selected != MonsterEntranceType.SkyDrop)
                    {
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("arcHeight"));
                    }
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("effectPrefab"));
                }
            }
        }
    }

    private static int FindEntranceIndex(SerializedProperty entrances, Object monster)
    {
        for (int i = 0; i < entrances.arraySize; i++)
        {
            SerializedProperty entry = entrances.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("monster").objectReferenceValue == monster) return i;
        }
        return -1;
    }

    private static SerializedProperty AddEntrance(SerializedProperty entrances, Object monster)
    {
        int index = entrances.arraySize;
        entrances.arraySize++;
        SerializedProperty entry = entrances.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("monster").objectReferenceValue = monster;
        entry.FindPropertyRelative("start").objectReferenceValue = null;
        SerializedProperty settings = entry.FindPropertyRelative("entrance");
        settings.FindPropertyRelative("duration").floatValue = .75f;
        settings.FindPropertyRelative("arcHeight").floatValue = 3.5f;
        settings.FindPropertyRelative("effectPrefab").objectReferenceValue = null;
        return entry;
    }

    private void DrawProperty(string propertyName)
    {
        KoreanLabelEditorUtility.DrawProperty(serializedObject.FindProperty(propertyName), target.GetType());
    }
}
