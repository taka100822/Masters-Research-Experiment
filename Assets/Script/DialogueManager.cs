using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using StarterAssets;

public class DialogueManager : MonoBehaviour
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private ThirdPersonController playerController;

    private NPCDialogue currentNPC;
    private DialogueDatabase dialogueDatabase;

    private bool isOpen = false;

    private string[] currentLines;
    private int currentLineIndex;

    private void Start()
    {
        dialoguePanel.SetActive(false);

        dialogueDatabase =
            FindAnyObjectByType<DialogueDatabase>();
    }

    private void Update()
    {
        if (
            Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame
        )
        {
            if (!isOpen)
            {
                if (currentNPC != null)
                {
                    string[] lines =
                        dialogueDatabase.GetDialogue(
                            currentNPC.npcId
                        );

                    OpenDialogue(
                        currentNPC.npcId,
                        lines
                    );
                }
            }
            else
            {
                NextLine();
            }
        }
    }

    public void OpenDialogue(string speakerName, string[] lines)
    {
        if (lines.Length == 0)
        {
            return;
        }

        isOpen = true;

        dialoguePanel.SetActive(true);

        playerController.enabled = false;

        nameText.text = speakerName;

        currentLines = lines;
        currentLineIndex = 0;

        dialogueText.text = currentLines[currentLineIndex];
    }

    private void NextLine()
    {
        currentLineIndex++;

        if (currentLineIndex >= currentLines.Length)
        {
            CloseDialogue();
            return;
        }

        dialogueText.text = currentLines[currentLineIndex];
    }

    public void CloseDialogue()
    {
        isOpen = false;

        dialoguePanel.SetActive(false);

        playerController.enabled = true;
    }

    public void SetCurrentNPC(NPCDialogue npc)
    {
        currentNPC = npc;
    }
}