using UnityEngine;

[CreateAssetMenu(
    fileName = "NewDialogue",
    menuName = "Dialogue/Dialogue Data"
)]
public class DialogueData : ScriptableObject
{
    public enum Speaker
    {
        [InspectorName("기존 방식")] Legacy = 0,
        [InspectorName("아리사")] Arisa = 1,
        [InspectorName("상대 캐릭터")] Partner = 2
    }

    [System.Serializable]
    public class DialogueLine
    {
        [InspectorName("화자")]
        public Speaker speaker;

        public string characterName;

        [TextArea(2, 5)]
        public string dialogueText;

        [InspectorName("더빙")]
        public AudioClip voiceClip;

        [Tooltip("상대 캐릭터 대사에서 지정하면 오른쪽 초상화를 교체합니다. 비워 두면 이전 초상화를 유지합니다.")]
        public Sprite characterImage;
    }

    [InspectorName("첫 상대 초상화")]
    public Sprite partnerPortrait;

    public DialogueLine[] dialogueLines;
}
