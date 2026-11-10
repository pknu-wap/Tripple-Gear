using UnityEngine;
using UnityEditor;

public class DialogueEditorWindow : EditorWindow
{
    // 편집할 대화 데이터
    private DialogueData dialogueData;

    // Unity 상단 메뉴에 편집 창 추가
    [MenuItem("Tools/Dialogue Editor")]
    public static void ShowWindow()
    {
        GetWindow<DialogueEditorWindow>("Dialogue Editor");
    }

    // 편집 창에 표시할 내용
    private void OnGUI()
    {
        GUILayout.Label(
            "Dialogue Editor",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(10);

        // 대화 데이터 선택
        dialogueData = (DialogueData)EditorGUILayout.ObjectField(
            "Dialogue Data",
            dialogueData,
            typeof(DialogueData),
            false
        );

        EditorGUILayout.Space(10);

        // 데이터가 선택되지 않았다면 안내
        if (dialogueData == null)
        {
            EditorGUILayout.HelpBox(
                "대화 데이터(DialogueData)를 선택해주세요.",
                MessageType.Info
            );

            return;
        }

        // 선택한 대화 데이터를 편집할 수 있도록 준비
        SerializedObject serializedData =
            new SerializedObject(dialogueData);

        serializedData.Update();

        // DialogueData 안의 dialogueLines 배열 가져오기
        SerializedProperty dialogueLines =
            serializedData.FindProperty("dialogueLines");

        EditorGUILayout.LabelField(
            "Dialogue Lines",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(5);

        // 대화 목록 표시 및 수정
        EditorGUILayout.PropertyField(
            dialogueLines,
            true
        );

        // 변경 사항 적용
        serializedData.ApplyModifiedProperties();

        EditorGUILayout.Space(10);

        // 저장 버튼
        if (GUILayout.Button("Save Dialogue"))
        {
            EditorUtility.SetDirty(dialogueData);
            AssetDatabase.SaveAssets();

            Debug.Log("대화 데이터가 저장되었습니다.");
        }
    }
}