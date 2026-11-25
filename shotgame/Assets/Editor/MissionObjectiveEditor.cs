using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(MissionObjective))]
public sealed class MissionObjectiveEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
        {
            DrawProperty("m_Script");
        }

        DrawProperty("goalType");
        DrawProperty("requiredCount");
        DrawProperty("requiredSeconds");
        DrawProperty("targetMonsters");

        SerializedProperty goal = serializedObject.FindProperty("goalType");
        if (!goal.hasMultipleDifferentValues && goal.intValue == (int)MissionObjective.GoalType.DefeatTargets)
        {
            PlacedMonsterEntranceInspector.Draw(serializedObject.FindProperty("targetMonsters"),
                serializedObject.FindProperty("targetEntrances"));
        }

        DrawProperty("keepTargetsVisibleBeforeStart");
        DrawProperty("missionBoat");
        DrawProperty("boatBattleMode");
        DrawProperty("onStarted");
        DrawProperty("onEnded");
        DrawProperty("onProgressChanged");
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProperty(string name) =>
        KoreanLabelEditorUtility.DrawProperty(serializedObject.FindProperty(name), typeof(MissionObjective));
}

internal static class PlacedMonsterEntranceInspector
{
    internal static void Draw(SerializedProperty monsters, SerializedProperty entrances)
    {
        if (!monsters.isExpanded || monsters.arraySize == 0 || monsters.serializedObject.isEditingMultipleObjects)
        {
            return;
        }

        EditorGUILayout.LabelField("배치 몬스터 등장 연출", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            for (int i = 0; i < monsters.arraySize; i++)
            {
                Object reference = monsters.GetArrayElementAtIndex(i).objectReferenceValue;
                GameObject monster = reference is Component component ? component.gameObject : reference as GameObject;
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
}
