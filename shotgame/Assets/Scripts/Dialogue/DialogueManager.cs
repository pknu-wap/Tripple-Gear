using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DialogueManager : MonoBehaviour
{
    private static readonly HashSet<DialogueManager> activeDialogues = new HashSet<DialogueManager>();
    private static int lastDialogueEndFrame = -1;

    [Header("대화 데이터")]
    [SerializeField]
    private DialogueData dialogueData;

    [Header("대화 UI")]
    [SerializeField]
    private GameObject dialoguePanel;

    [SerializeField]
    private Image characterImage;

    [SerializeField]
    private Image partnerImage;

    [SerializeField, Range(0f, 1f)]
    private float listenerOpacity = 0.4f;

    [SerializeField]
    private TextMeshProUGUI characterNameText;

    [SerializeField]
    private TextMeshProUGUI dialogueText;

    [SerializeField]
    private Button nextButton;

    [SerializeField]
    private GameObject dialoguePrefab;
    private GameObject dialogueInstance;

    [SerializeField, Min(1f), InspectorName("초당 표시 글자 수")]
    private float charactersPerSecond = 35f;

    private int currentIndex = 0;
    private int lastAdvanceFrame = -1;
    private int dialogueCharacterCount;
    private float visibleCharacterProgress;
    private DialogueData.DialogueLine[] activeLines;
    private Sprite arisaPortrait;
    private Sprite activePartnerPortrait;

    internal static int ActiveDialogueCount => activeDialogues.Count;

    internal bool IsPlaying(DialogueData data) => data != null && activeLines == data.dialogueLines;

    internal static bool BlocksGameplayInput =>
        activeDialogues.Count > 0 || lastDialogueEndFrame == Time.frameCount;

    private void Awake()
    {
        if (characterImage != null)
        {
            arisaPortrait = characterImage.sprite;
        }
    }

    private void OnEnable()
    {
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextDialogue);
        }
    }

    private void OnDisable()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(NextDialogue);
        }

        if (activeLines != null)
        {
            EndDialogue();
        }
    }

    private void Start()
    {
        dialoguePanel.SetActive(activeLines != null);
    }

    private void Update()
    {
        if (activeLines == null)
        {
            return;
        }

        if (AdvancePressed())
        {
            NextDialogue();
            return;
        }

        if (dialogueText.maxVisibleCharacters < dialogueCharacterCount)
        {
            visibleCharacterProgress += Time.unscaledDeltaTime * charactersPerSecond;
            dialogueText.maxVisibleCharacters = Mathf.Min(dialogueCharacterCount, Mathf.FloorToInt(visibleCharacterProgress));
        }
    }

    private static bool AdvancePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);
#else
        return false;
#endif
    }

    public void StartDialogue()
    {
        PlayDialogue(dialogueData);
    }

    public void PlayDialogue(DialogueData data)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (data == null || data.dialogueLines == null || data.dialogueLines.Length == 0)
        {
            Debug.LogWarning("대화 데이터가 없습니다.", this);
            return;
        }

        currentIndex = 0;
        lastAdvanceFrame = Time.frameCount;
        activeLines = data.dialogueLines;
        activePartnerPortrait = data.partnerPortrait;
        activeDialogues.Add(this);
        GameplayPause.Acquire(this);

        dialoguePanel.SetActive(true);

        ShowDialogue();
    }

    public void NextDialogue()
    {
        if (activeLines == null || lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastAdvanceFrame = Time.frameCount;

        if (dialogueText.maxVisibleCharacters < dialogueCharacterCount)
        {
            dialogueText.maxVisibleCharacters = dialogueCharacterCount;
            return;
        }

        currentIndex++;

        if (currentIndex >= activeLines.Length)
        {
            EndDialogue();
            return;
        }

        ShowDialogue();
    }

    private void ShowDialogue()
    {
        DialogueData.DialogueLine line = activeLines[currentIndex];

        characterNameText.text = line.characterName;
        dialogueText.text = line.dialogueText;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();
        dialogueCharacterCount = dialogueText.textInfo.characterCount;
        visibleCharacterProgress = 0f;

        if (line.speaker == DialogueData.Speaker.Legacy)
        {
            SetPortrait(characterImage, line.characterImage, 1f);
            SetPortrait(partnerImage, null, 1f);
            return;
        }

        bool arisaIsSpeaking = line.speaker == DialogueData.Speaker.Arisa;
        if (!arisaIsSpeaking && line.characterImage != null)
        {
            activePartnerPortrait = line.characterImage;
        }

        SetPortrait(characterImage, arisaPortrait, arisaIsSpeaking ? 1f : listenerOpacity);
        SetPortrait(partnerImage, activePartnerPortrait, arisaIsSpeaking ? listenerOpacity : 1f);
    }

    private static void SetPortrait(Image image, Sprite portrait, float opacity)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = portrait;
        image.enabled = portrait != null;
        Color color = image.color;
        color.a = opacity;
        image.color = color;
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
        lastDialogueEndFrame = Time.frameCount;
        activeDialogues.Remove(this);
        GameplayPause.Release(this);
        activeLines = null;
        activePartnerPortrait = null;
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        Debug.Log("대화 종료");
    }
}
