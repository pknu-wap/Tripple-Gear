using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(MonoBehaviour), true)]
public sealed class KoreanLabelMonoBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (!KoreanLabelEditorUtility.HasKoreanLabel(target.GetType()))
        {
            DrawDefaultInspector();
            return;
        }

        KoreanLabelEditorUtility.DrawInspector(serializedObject, target.GetType());
    }
}

[CanEditMultipleObjects]
[CustomEditor(typeof(ScriptableObject), true)]
public sealed class KoreanLabelScriptableObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (!KoreanLabelEditorUtility.HasKoreanLabel(target.GetType()))
        {
            DrawDefaultInspector();
            return;
        }

        KoreanLabelEditorUtility.DrawInspector(serializedObject, target.GetType());
    }
}

internal static class KoreanLabelEditorUtility
{
    private const BindingFlags FieldFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<Type, Dictionary<string, LabelData>> LabelCache = new();

    public static bool HasKoreanLabel(Type targetType)
    {
        Dictionary<string, LabelData> labels = GetLabels(targetType);
        return labels.Count > 0;
    }

    public static void DrawInspector(SerializedObject serializedObject, Type targetType)
    {
        serializedObject.Update();

        Dictionary<string, LabelData> labels = GetLabels(targetType);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
            {
                if (labels.TryGetValue(property.name, out LabelData labelData))
                {
                    EditorGUILayout.PropertyField(property, labelData.Content, true);
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            enterChildren = false;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static Dictionary<string, LabelData> GetLabels(Type targetType)
    {
        if (targetType == null)
        {
            return new Dictionary<string, LabelData>();
        }

        if (LabelCache.TryGetValue(targetType, out Dictionary<string, LabelData> cachedLabels))
        {
            return cachedLabels;
        }

        Dictionary<string, LabelData> labels = new();
        for (Type currentType = targetType; currentType != null; currentType = currentType.BaseType)
        {
            FieldInfo[] fields = currentType.GetFields(FieldFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                KoreanLabelAttribute labelAttribute = field.GetCustomAttribute<KoreanLabelAttribute>(true);
                if (labelAttribute == null || string.IsNullOrWhiteSpace(labelAttribute.Label))
                {
                    continue;
                }

                TooltipAttribute tooltipAttribute = field.GetCustomAttribute<TooltipAttribute>(true);
                labels[field.Name] = new LabelData(labelAttribute.Label, tooltipAttribute?.tooltip);
            }
        }

        LabelCache[targetType] = labels;
        return labels;
    }

    private readonly struct LabelData
    {
        public LabelData(string label, string tooltip)
        {
            Content = new GUIContent(label, tooltip);
        }

        public GUIContent Content { get; }
    }
}
