using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using StarterAssets;

public class DialogueManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject talkHint;
    [SerializeField] private GameObject nextIndicator;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private GameObject inputPanel;
    [SerializeField] private TMP_InputField inputField;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text[] choiceTexts;

    [Header("Player")]
    [SerializeField] private ThirdPersonController playerController;

    [Header("ChatGPT")]
    private ChatGPTClient chatGPT;

    private NPCDialogue currentNPC;
    private DialogueDatabase dialogueDatabase;

    private GameState currentState = GameState.FreeMove;

    private int currentNodeId;
    private DialogueDatabase.Node currentNode;

    private List<Choice> choices = new();
    private int choiceIndex = 0;

    private StarterAssetsInputs inputs;

    private void Start()
    {
        inputs = playerController.GetComponent<StarterAssetsInputs>();

        dialoguePanel.SetActive(false);
        talkHint.SetActive(false);
        nextIndicator.SetActive(false);
        choicePanel.SetActive(false);
        inputPanel.SetActive(false);

        dialogueDatabase = FindAnyObjectByType<DialogueDatabase>();
        chatGPT = GameManager.Instance.GetComponent<ChatGPTClient>();
    }

    private void Update()
    {
        switch (currentState)
        {
            case GameState.FreeMove:
                HandleFreeMoveInput();
                break;

            case GameState.InDialogue:
                HandleDialogueInput();
                break;

            case GameState.InChoice:
                HandleChoiceInput();
                break;

            case GameState.InTyping:
                HandleTypingInput();
                break;
        }
    }

    // =========================
    // Input Split
    // =========================

    private bool TalkPressed()
    {
        return Keyboard.current.enterKey.wasPressedThisFrame;
    }

    private bool DialogueNextPressed()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return false;

        return Keyboard.current.enterKey.wasPressedThisFrame;
    }

    private bool ChoiceSubmitPressed()
    {
        return Keyboard.current.enterKey.wasPressedThisFrame;
    }

    // =========================
    // Free Move
    // =========================

    private void HandleFreeMoveInput()
    {
        talkHint.SetActive(currentNPC != null);

        if (!TalkPressed() || currentNPC == null)
            return;

        dialogueDatabase.LoadCSV("Dialogue/" + currentNPC.csvFileName);

        StartNode(0);
    }

    private void StartNode(int id)
    {
        currentNodeId = id;
        currentNode = dialogueDatabase.GetNode(id);

        currentState = GameState.InDialogue;

        dialoguePanel.SetActive(true);
        talkHint.SetActive(false);

        playerController.enabled = false;

        ShowNode();
    }

    private void ShowNode()
    {
        if (currentNode == null)
        {
            CloseDialogue();
            return;
        }

        dialogueText.text = currentNode.text;
        nameText.text = currentNPC != null ? currentNPC.npcId : "";

        ShowChoicesFromNode();
    }

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

        if (currentNode.allowInput == 1)
        {
            list.Add(new Choice
            {
                text = "入力する",
                onSelect = OpenInputMode
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

    private void HandleDialogueInput()
    {
        if (!DialogueNextPressed())
            return;

        if (currentNode.nextId >= 0)
            GoToNode(currentNode.nextId);
        else
            CloseDialogue();
    }

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
    // Choice
    // =========================

    private void ShowChoices(List<Choice> newChoices)
    {
        choices = newChoices;
        choiceIndex = 0;

        currentState = GameState.InChoice;

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
                choiceTexts[i].color = (i == choiceIndex) ? Color.yellow : Color.white;
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

        if (ChoiceSubmitPressed())
        {
            choicePanel.SetActive(false);
            choices[choiceIndex].onSelect?.Invoke();
        }
    }

    // =========================
    // Input Mode
    // =========================

    private void OpenInputMode()
    {
        currentState = GameState.InTyping;

        choicePanel.SetActive(false);
        inputPanel.SetActive(true);

        SetTypingMode(true);

        inputField.text = "";
        inputField.ActivateInputField();
    }

    private void HandleTypingInput()
    {
        // Enterでは送信しない（IMETROUBLE回避）
    }

    public void OnClickSend()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SubmitTypedText();
    }

    private async void SubmitTypedText()
    {
        string text = inputField.text;

        inputPanel.SetActive(false);

        string reply = await chatGPT.SendChatMessage(
            text,
            currentNPC.promptData.systemPrompt
        );

        ShowAIResponse(reply);
    }

    private void ShowAIResponse(string reply)
    {
        currentState = GameState.InDialogue;

        dialoguePanel.SetActive(true);
        choicePanel.SetActive(false);

        dialogueText.text = reply;

        nextIndicator.SetActive(true);
    }

    // =========================
    // Close
    // =========================

    public void CloseDialogue()
    {
        currentState = GameState.FreeMove;

        dialoguePanel.SetActive(false);
        choicePanel.SetActive(false);
        inputPanel.SetActive(false);
        nextIndicator.SetActive(false);

        SetTypingMode(false);

        if (inputs != null)
        {
            inputs.jump = false;
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
        }

        playerController.enabled = true;
    }

    public void SetCurrentNPC(NPCDialogue npc)
    {
        currentNPC = npc;
    }

    private void SetTypingMode(bool active)
    {
        // プレイヤー操作制御
        playerController.enabled = !active;

        if (active)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // =========================
    // Class
    // =========================

    public class Choice
    {
        public string text;
        public Action onSelect;
    }
}