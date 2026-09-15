using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(GunFireLogic), true)]
public sealed class GunFireLogicDrawer : PropertyDrawer
{
    private static readonly LogicOption[] Options =
    {
        new("직선 탄환", typeof(StraightProjectileFireLogic)),
        new("순간 직선", typeof(HitscanLineFireLogic)),
        new("샷건 박스 판정", typeof(ShotgunBoxFireLogic)),
        new("미사일 폭발 탄환", typeof(MissileExplosionFireLogic)),
    };

    private static readonly Dictionary<Type, Dictionary<string, GUIContent>> LabelCache = new();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        EditorGUI.indentLevel++;
        Rect typeRect = NextLine(position, 1);
        int currentIndex = GetCurrentIndex(property);
        int selectedIndex = EditorGUI.Popup(typeRect, "Logic Type", currentIndex, GetOptionLabels());
        if (selectedIndex != currentIndex || property.managedReferenceValue == null)
        {
            property.managedReferenceValue = Activator.CreateInstance(Options[selectedIndex].Type);
            property.serializedObject.ApplyModifiedProperties();
        }

        if (property.isExpanded)
        {
            DrawChildProperties(position, property);
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float height = lineHeight + spacing + lineHeight;

        if (!property.isExpanded)
        {
            return height;
        }

        SerializedProperty child = property.Copy();
        SerializedProperty end = child.GetEndProperty();
        bool enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            height += spacing + EditorGUI.GetPropertyHeight(child, true);
            enterChildren = false;
        }

        return height;
    }

    private static void DrawChildProperties(Rect position, SerializedProperty property)
    {
        SerializedProperty child = property.Copy();
        SerializedProperty end = child.GetEndProperty();
        bool enterChildren = true;
        int lineIndex = 2;

        Type logicType = property.managedReferenceValue?.GetType();
        Dictionary<string, GUIContent> labels = GetLabels(logicType);

        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            float propertyHeight = EditorGUI.GetPropertyHeight(child, true);
            Rect childRect = new Rect(
                position.x,
                position.y + lineIndex * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing),
                position.width,
                propertyHeight
            );

            if (labels.TryGetValue(child.name, out GUIContent childLabel))
            {
                EditorGUI.PropertyField(childRect, child, childLabel, true);
            }
            else
            {
                EditorGUI.PropertyField(childRect, child, true);
            }

            lineIndex += Mathf.CeilToInt(propertyHeight / EditorGUIUtility.singleLineHeight);
            enterChildren = false;
        }
    }

    private static Rect NextLine(Rect position, int lineIndex)
    {
        float lineWithSpacing = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        return new Rect(
            position.x,
            position.y + lineIndex * lineWithSpacing,
            position.width,
            EditorGUIUtility.singleLineHeight
        );
    }

    private static int GetCurrentIndex(SerializedProperty property)
    {
        Type currentType = property.managedReferenceValue?.GetType();
        for (int i = 0; i < Options.Length; i++)
        {
            if (Options[i].Type == currentType)
            {
                return i;
            }
        }

        return 0;
    }

    private static string[] GetOptionLabels()
    {
        string[] labels = new string[Options.Length];
        for (int i = 0; i < Options.Length; i++)
        {
            labels[i] = Options[i].Label;
        }

        return labels;
    }

    private static Dictionary<string, GUIContent> GetLabels(Type type)
    {
        if (type == null)
        {
            return new Dictionary<string, GUIContent>();
        }

        if (LabelCache.TryGetValue(type, out Dictionary<string, GUIContent> cached))
        {
            return cached;
        }

        Dictionary<string, GUIContent> labels = new();
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            KoreanLabelAttribute labelAttribute = fields[i].GetCustomAttribute<KoreanLabelAttribute>(true);
            if (labelAttribute != null && !string.IsNullOrWhiteSpace(labelAttribute.Label))
            {
                labels[fields[i].Name] = new GUIContent(labelAttribute.Label);
            }
        }

        LabelCache[type] = labels;
        return labels;
    }

    private readonly struct LogicOption
    {
        public LogicOption(string label, Type type)
        {
            Label = label;
            Type = type;
        }

        public string Label { get; }
        public Type Type { get; }
    }
}
