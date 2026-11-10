using UnityEngine;

[CreateAssetMenu(
    fileName = "NewDialogue",
    menuName = "Dialogue/Dialogue Data"
)]
public class DialogueData : ScriptableObject
{
    [System.Serializable]
    public class DialogueLine
    {
        public string characterName;

        [TextArea(2, 5)]
        public string dialogueText;

        public Sprite characterImage;
    }

    public DialogueLine[] dialogueLines;
}