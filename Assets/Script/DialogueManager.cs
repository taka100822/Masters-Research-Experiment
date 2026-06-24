using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using StarterAssets;

public class DialogueManager : MonoBehaviour
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject talkHint;
    [SerializeField] private GameObject nextIndicator;

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;

    [SerializeField] private ThirdPersonController playerController;

    private NPCDialogue currentNPC;
    private DialogueDatabase dialogueDatabase;

    private string[] currentLines;
    private int currentLineIndex;

    private bool isOpen = false;

    private void Start()
    {
        dialoguePanel.SetActive(false);
        talkHint.SetActive(false);
        nextIndicator.SetActive(false);

        dialogueDatabase = FindAnyObjectByType<DialogueDatabase>();
    }

    private void Update()
    {
        if (!GameManager.Instance.IsFree())
        {
            HandleDialogueInput();
            return;
        }

        HandleFreeMoveInput();
    }

    // =========================
    // 自由移動時
    // =========================
    private void HandleFreeMoveInput()
    {
        if (currentNPC != null)
        {
            talkHint.SetActive(true);
        }
        else
        {
            talkHint.SetActive(false);
        }

        bool talk =
            Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
            Mouse.current.leftButton.wasPressedThisFrame;

        if (!talk) return;

        if (currentNPC == null) return;

        string[] lines =
            dialogueDatabase.GetDialogue(currentNPC.npcId);

        OpenDialogue(currentNPC.npcId, lines);
    }

    // =========================
    // 会話中
    // =========================
    private void HandleDialogueInput()
    {
        bool advance =
            Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
            Mouse.current.leftButton.wasPressedThisFrame;

        if (!advance) return;

        NextLine();
    }

    // =========================
    // 会話開始
    // =========================
    public void OpenDialogue(string speakerName, string[] lines)
    {
        if (lines == null || lines.Length == 0) return;

        isOpen = true;

        GameManager.Instance.SetState(GameState.InDialogue);

        dialoguePanel.SetActive(true);
        talkHint.SetActive(false);

        playerController.enabled = false;

        nameText.text = speakerName;

        currentLines = lines;
        currentLineIndex = 0;

        dialogueText.text = currentLines[currentLineIndex];

        nextIndicator.SetActive(currentLines.Length > 1);
    }

    // =========================
    // 次の行へ
    // =========================
    private void NextLine()
    {
        currentLineIndex++;

        if (currentLineIndex >= currentLines.Length)
        {
            CloseDialogue();
            return;
        }

        dialogueText.text = currentLines[currentLineIndex];

        nextIndicator.SetActive(currentLineIndex < currentLines.Length - 1);
    }

    // =========================
    // 会話終了
    // =========================
    public void CloseDialogue()
    {
        isOpen = false;

        GameManager.Instance.SetState(GameState.FreeMove);

        dialoguePanel.SetActive(false);
        nextIndicator.SetActive(false);

        playerController.enabled = true;
    }

    // =========================
    // NPC登録（Trigger側から呼ぶ）
    // =========================
    public void SetCurrentNPC(NPCDialogue npc)
    {
        currentNPC = npc;

        talkHint.SetActive(npc != null && GameManager.Instance.IsFree());
    }
}