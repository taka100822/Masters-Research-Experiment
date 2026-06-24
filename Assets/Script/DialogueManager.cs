using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using StarterAssets;

public class DialogueManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject talkHint;
    [SerializeField] private GameObject nextIndicator;
    [SerializeField] private GameObject choicePanel;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text[] choiceTexts;

    [Header("Player")]
    [SerializeField] private ThirdPersonController playerController;

    private NPCDialogue currentNPC;
    private DialogueDatabase dialogueDatabase;

    // CSVノード制御
    private int currentNodeId;
    private DialogueDatabase.Node currentNode;

    // 選択肢
    private List<Choice> choices = new List<Choice>();
    private int choiceIndex = 0;

    // =========================
    // 初期化
    // =========================
    private void Start()
    {
        dialoguePanel.SetActive(false);
        talkHint.SetActive(false);
        nextIndicator.SetActive(false);
        choicePanel.SetActive(false);

        dialogueDatabase = FindAnyObjectByType<DialogueDatabase>();
    }

    // =========================
    // Update
    // =========================
    private void Update()
    {
        if (choicePanel.activeSelf)
        {
            HandleChoiceInput();
            return;
        }

        if (!GameManager.Instance.IsFree())
        {
            HandleDialogueInput();
            return;
        }

        HandleFreeMoveInput();
    }

    private bool SubmitPressed()
    {
        return
            Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
            Mouse.current.leftButton.wasPressedThisFrame;
    }

    // =========================
    // 自由移動
    // =========================
    private void HandleFreeMoveInput()
    {
        // Debug.Log("CLICK DETECTED");
        // Debug.Log("NPC = " + (currentNPC != null));
        talkHint.SetActive(currentNPC != null);

        if (!SubmitPressed() || currentNPC == null)
            return;

        dialogueDatabase.LoadCSV(
            "Dialogue/" + currentNPC.csvFileName
        );

        StartNode(0);
    }

    // =========================
    // ノード開始
    // =========================
    private void StartNode(int id)
    {
        Debug.Log("StartNode called: " + id);
        
        currentNodeId = id;
        currentNode = dialogueDatabase.GetNode(id);
        Debug.Log("Node = " + currentNode);

        GameManager.Instance.SetState(GameState.InDialogue);

        dialoguePanel.SetActive(true);
        talkHint.SetActive(false);

        playerController.enabled = false;

        ShowNode();
    }

    // =========================
    // ノード表示
    // =========================
    private void ShowNode()
    {
        if (currentNode == null)
        {
            CloseDialogue();
            return;
        }

        dialogueText.text = currentNode.text;
        nameText.text = currentNPC.npcId;

        ShowChoicesFromNode();
    }

    // =========================
    // 選択肢生成（CSV）
    // =========================
    private void ShowChoicesFromNode()
    {
        var list = new List<Choice>();

        if (!string.IsNullOrEmpty(currentNode.choiceA))
        {
            list.Add(new Choice
            {
                text = currentNode.choiceA,
                onSelect = () => GoToNode(currentNode.choiceA_next)
            });
        }

        if (!string.IsNullOrEmpty(currentNode.choiceB))
        {
            list.Add(new Choice
            {
                text = currentNode.choiceB,
                onSelect = () => GoToNode(currentNode.choiceB_next)
            });
        }

        if (list.Count > 0)
        {
            ShowChoices(list);
            nextIndicator.SetActive(false);
        }
        else
        {
            nextIndicator.SetActive(true);
        }
    }

    // =========================
    // ノード移動
    // =========================
    private void GoToNode(int id)
    {
        if (id < 0)
        {
            CloseDialogue();
            return;
        }

        currentNodeId = id;
        currentNode = dialogueDatabase.GetNode(id);

        ShowNode();
    }

    // =========================
    // 会話終了
    // =========================
    public void CloseDialogue()
    {
        GameManager.Instance.SetState(GameState.FreeMove);

        dialoguePanel.SetActive(false);
        choicePanel.SetActive(false);
        nextIndicator.SetActive(false);

        playerController.enabled = true;

        if (currentNPC != null)
        {
            currentNPC.GetComponent<NPCInteraction>()
                .SetBubbleVisible(true);
        }
    }

    // =========================
    // NPCセット
    // =========================
    public void SetCurrentNPC(NPCDialogue npc)
    {
        currentNPC = npc;
    }

    // =========================
    // 選択肢UI
    // =========================
    public void ShowChoices(List<Choice> newChoices)
    {
        choices = newChoices;
        choiceIndex = 0;

        choicePanel.SetActive(true);
        UpdateChoiceUI();

        playerController.enabled = false;
    }

    private void UpdateChoiceUI()
    {
        for (int i = 0; i < choiceTexts.Length; i++)
        {
            if (i < choices.Count)
            {
                choiceTexts[i].text = choices[i].text;
                choiceTexts[i].color =
                    (i == choiceIndex) ? Color.yellow : Color.white;
            }
            else
            {
                choiceTexts[i].text = "";
            }
        }
    }

    private void HandleChoiceInput()
    {
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            choiceIndex--;
            if (choiceIndex < 0) choiceIndex = choices.Count - 1;
            UpdateChoiceUI();
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            choiceIndex++;
            if (choiceIndex >= choices.Count) choiceIndex = 0;
            UpdateChoiceUI();
        }

        if (SubmitPressed())
        {
            choicePanel.SetActive(false);
            choices[choiceIndex].onSelect?.Invoke();
        }
    }

    private void HandleDialogueInput()
    {
        if (!SubmitPressed()) return;

        // 選択肢が出ているなら無視
        if (choicePanel.activeSelf)
            return;

        if (currentNode.nextId >= 0)
        {
            GoToNode(currentNode.nextId);
        }
        else
        {
            CloseDialogue();
        }
    }

    // =========================
    // 選択肢クラス
    // =========================
    public class Choice
    {
        public string text;
        public Action onSelect;
    }
}