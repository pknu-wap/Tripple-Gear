using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("대화 데이터")]
    [SerializeField]
    private DialogueData dialogueData;

    [Header("대화 UI")]
    [SerializeField]
    private GameObject dialoguePanel;

    [SerializeField]
    private Image characterImage;

    [SerializeField]
    private TextMeshProUGUI characterNameText;

    [SerializeField]
    private TextMeshProUGUI dialogueText;

    [SerializeField]
    private GameObject dialoguePrefab;
    private GameObject dialogueInstance;

    private int currentIndex = 0;
    private bool isDialogueActive = false;

    private void Start()
    {
        dialoguePanel.SetActive(false);
    }

    private void Update()
    {
        if (isDialogueActive &&
            (Input.GetKeyDown(KeyCode.Space) ||
             Input.GetKeyDown(KeyCode.E)))
        {
            NextDialogue();
        }
    }

    public void StartDialogue()
    {
        if (dialogueData == null ||
            dialogueData.dialogueLines.Length == 0)
        {
            Debug.LogWarning("대화 데이터가 없습니다.");
            return;
        }

        currentIndex = 0;
        isDialogueActive = true;

        dialoguePanel.SetActive(true);

        ShowDialogue();
    }

    public void NextDialogue()
    {
        currentIndex++;

        if (currentIndex >= dialogueData.dialogueLines.Length)
        {
            EndDialogue();
            return;
        }

        ShowDialogue();
    }

    private void ShowDialogue()
    {
        DialogueData.DialogueLine line =
            dialogueData.dialogueLines[currentIndex];

        characterNameText.text = line.characterName;
        dialogueText.text = line.dialogueText;
        characterImage.sprite = line.characterImage;

        characterImage.enabled = line.characterImage != null;
    }

    public void CreateDialogueUI()
    {
        dialogueInstance = Instantiate(
            dialoguePrefab,
            transform
        );
    }

    private void EndDialogue()
    {
        isDialogueActive = false;
        dialoguePanel.SetActive(false);

        Debug.Log("대화 종료");
    }
}
