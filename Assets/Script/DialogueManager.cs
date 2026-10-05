using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using StarterAssets;
using Unity.Cinemachine;

public class DialogueManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject talkHint;
    [SerializeField] private GameObject nextIndicator;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private GameObject inputPanel;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private GameObject questCompletePanel;
    [SerializeField] private SettingsPanelUI settingsPanel;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text[] choiceTexts;
    [Tooltip("本文が収まる行数。収まるときだけ文の終わりで改行する（README 3.10）")]
    [SerializeField] private int maxDialogueLines = 3;

    // 会話UIの見た目（README 3.7）。未設定でも動く
    [Header("Dialogue UI Style")]
    [SerializeField] private TMP_Text keyGuideText;
    [SerializeField] private TMP_Text talkHintText;
    [SerializeField] private TMP_Text inputHeaderText;
    [SerializeField] private UnityEngine.UI.Image[] choiceRows;
    [SerializeField] private UnityEngine.UI.Button sendButton;

    private static readonly Color ChoiceSelectedColor = new Color32(0xF6, 0xEE, 0xDC, 0xFF);   // 漆喰
    private static readonly Color ChoiceNormalColor = new Color32(0xB9, 0xA8, 0x8E, 0xFF);     // 煤
    private static readonly Color ChoiceRowSelectedColor = new Color32(0xE0, 0xB3, 0x54, 0x40); // 真鍮25%
    private const string KeyColor = "#E0B354";

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

    // AIの返答待ち・失敗時に出す文（README 4.6）
    private const string WaitingText = "……";
    private const string AIErrorText = "（うまく伝わらなかったようだ。もう一度話しかけてみよう）";

    // 送れなかった文章。次に「入力する」を選んだとき入力欄に戻す
    private string lastFailedInput;

    // 「初期位置に戻る」用に、ゲーム開始時のプレイヤーの位置・向きを記録しておく
    private Vector3 playerStartPosition;
    private Quaternion playerStartRotation;

    private void Start()
    {
        inputs = playerController.GetComponent<StarterAssetsInputs>();

        playerStartPosition = playerController.transform.position;
        playerStartRotation = playerController.transform.rotation;

        // タイトル画面から来た場合、Starter Assetsのカーソル固定（フォーカス時のみ）が働かないのでここで固定する
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        dialoguePanel.SetActive(false);
        talkHint.SetActive(false);
        nextIndicator.SetActive(false);
        choicePanel.SetActive(false);
        inputPanel.SetActive(false);

        if (questCompletePanel != null)
            questCompletePanel.SetActive(false);

        dialogueDatabase = FindAnyObjectByType<DialogueDatabase>();
        chatGPT = GameManager.Instance.GetComponent<ChatGPTClient>();

        // 空欄（空白だけ）の間は送信できない（README 3.7）
        inputField.onValueChanged.AddListener(_ => UpdateSendButton());
    }

    private void UpdateSendButton()
    {
        if (sendButton != null)
            sendButton.interactable = !string.IsNullOrWhiteSpace(inputField.text);
    }

    // 操作案内（README 3.7）
    private void SetKeyGuide(string text)
    {
        if (keyGuideText != null)
            keyGuideText.text = text;
    }

    private static string Key(string key)
    {
        return "<color=" + KeyColor + ">[" + key + "]</color>";
    }

    // InDialogue（選択肢なし）のときの案内。次が無ければ「閉じる」
    private void SetDialogueKeyGuide(bool isLast)
    {
        SetKeyGuide(Key("Enter") + " " + (isLast ? "閉じる" : "次へ"));
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

            case GameState.InSettings:
                HandleSettingsInput();
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
        // 設定パネルは探索中だけ開ける（自由入力中の「q」で開かないように）
        if (Keyboard.current.qKey.wasPressedThisFrame && settingsPanel != null)
        {
            OpenSettings();
            return;
        }

        talkHint.SetActive(currentNPC != null);

        if (currentNPC != null && talkHintText != null)
            talkHintText.text = Key("Enter") + " " + currentNPC.DisplayName + "と話す";

        if (!TalkPressed() || currentNPC == null)
            return;

        if (!LoadDialogueCSV(currentNPC.csvFileName))
        {
            Debug.LogError("Dialogue CSV not found: " + currentNPC.csvFileName);
            return;
        }

        StartNode(SelectStartNode());
    }

    // Dialogue/Q{クエスト番号}/ → Dialogue/Common/ の順で探す（README 4.4）
    private bool LoadDialogueCSV(string csvFileName)
    {
        int questNumber = ExperimentSettings.Instance != null
            ? ExperimentSettings.Instance.questNumber
            : 1;

        return dialogueDatabase.LoadCSV("Dialogue/Q" + questNumber + "/" + csvFileName)
            || dialogueDatabase.LoadCSV("Dialogue/Common/" + csvFileName);
    }

    // クエスト状態ごとの開始ノード。無ければ後ろの候補へ（README 4.4）
    private int SelectStartNode()
    {
        int[] candidates = new[] { 0 };

        var quest = QuestManager.Instance;
        if (quest != null)
        {
            switch (quest.State)
            {
                case QuestState.InProgress:
                    candidates = quest.HasAllClues()
                        ? new[] { 200, 100, 0 }
                        : new[] { 100, 0 };
                    break;

                case QuestState.Completed:
                    candidates = new[] { 300, 0 };
                    break;
            }
        }

        foreach (int id in candidates)
        {
            if (dialogueDatabase.HasNode(id))
                return id;
        }

        return 0;
    }

    private void StartNode(int id)
    {
        currentNodeId = id;
        currentNode = dialogueDatabase.GetNode(id);

        currentState = GameState.InDialogue;

        dialoguePanel.SetActive(true);
        talkHint.SetActive(false);

        StopPlayer();

        ExperimentLogger.Log("dialogue_start", npcId: CurrentNpcId, nodeId: id,
            detail: "quest_state=" + (QuestManager.Instance != null ? QuestManager.Instance.State.ToString() : "none"));

        ShowNode();
    }

    private string CurrentNpcId => currentNPC != null ? currentNPC.npcId : null;

    private void ShowNode()
    {
        if (currentNode == null)
        {
            CloseDialogue();
            return;
        }

        dialogueText.text = JapaneseLineBreaker.Format(currentNode.text, dialogueText, maxDialogueLines);
        nameText.text = currentNPC != null ? currentNPC.DisplayName : "";

        ExperimentLogger.Log("npc_line", npcId: CurrentNpcId, nodeId: currentNodeId, text: currentNode.text);

        ExecuteNodeAction(currentNode.action);

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

        // 自由入力は条件Bのときのみ（条件はクエスト・NPCに依存しない全体設定）
        if (currentNode.allowInput == 1 &&
            ExperimentSettings.Instance != null &&
            ExperimentSettings.Instance.AllowFreeInput)
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
            // 選択肢から遷移してきた場合もInChoiceのままにしない（前の選択肢が再実行されるのを防ぐ）
            currentState = GameState.InDialogue;
            nextIndicator.SetActive(true);
            SetDialogueKeyGuide(currentNode.nextId < 0);
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

    // CSVのaction列をクエストに反映する（README 4.4）
    private void ExecuteNodeAction(string action)
    {
        var quest = QuestManager.Instance;
        if (string.IsNullOrEmpty(action) || quest == null)
            return;

        if (action == "startQuest")
            quest.StartQuest();
        else if (action.StartsWith("clue:"))
            quest.AddClue(action.Substring("clue:".Length));
        else if (action == "complete")
            quest.Complete();
        else
            Debug.LogWarning("Unknown node action: " + action);
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

        SetKeyGuide(Key("↑") + Key("↓") + " 選ぶ　" + Key("Enter") + " 決定");

        StopPlayer();
    }

    // 選択中の行は▶＋真鍮の下地、使わない行は隠す（README 3.7）
    private void UpdateChoiceUI()
    {
        for (int i = 0; i < choiceTexts.Length; i++)
        {
            bool used = i < choices.Count;
            bool selected = used && i == choiceIndex;

            if (used)
            {
                // 選ばれていない行も▶の幅をあけて、文の頭をそろえる
                string cursor = selected
                    ? "<color=" + KeyColor + ">▶</color> "
                    : "<alpha=#00>▶<alpha=#FF> ";
                choiceTexts[i].text = cursor + choices[i].text;
                choiceTexts[i].color = selected ? ChoiceSelectedColor : ChoiceNormalColor;
            }
            else
            {
                choiceTexts[i].text = "";
            }

            if (choiceRows != null && i < choiceRows.Length && choiceRows[i] != null)
            {
                choiceRows[i].gameObject.SetActive(used);
                choiceRows[i].color = selected ? ChoiceRowSelectedColor : Color.clear;
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

            ExperimentLogger.Log("choice_select", npcId: CurrentNpcId, nodeId: currentNodeId,
                text: choices[choiceIndex].text, choiceIndex: choiceIndex);

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

        // 前回送れなかった文章があれば残しておき、そのまま送り直せるようにする（README 4.6）
        inputField.text = lastFailedInput ?? "";
        inputField.ActivateInputField();
        UpdateSendButton();

        if (inputHeaderText != null)
            inputHeaderText.text = (currentNPC != null ? currentNPC.DisplayName : "") + "に話しかける";

        SetKeyGuide("入力したら「送信」を押してください");
    }

    // 「やめる」ボタン（README 3.7）。書きかけの文章は捨て、同じノードの選択肢に戻る
    public void OnClickCancelInput()
    {
        if (currentState != GameState.InTyping)
            return;

        ExperimentLogger.Log("free_input_cancel", npcId: CurrentNpcId, nodeId: currentNodeId);

        inputPanel.SetActive(false);
        inputField.text = "";
        lastFailedInput = null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        ShowChoicesFromNode();
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

        string npcId = CurrentNpcId;
        int nodeId = currentNodeId;
        ExperimentLogger.Log("free_input_submit", npcId: npcId, nodeId: nodeId, playerInput: text);

        PromptData prompt = LoadPromptData(currentNPC, out string promptPath);
        if (prompt == null)
        {
            Debug.LogError("PromptData not found: " + currentNPC.npcId);
            const string fallback = "……（今は話せないようだ）";
            ExperimentLogger.Log("ai_response", npcId: npcId, nodeId: nodeId, text: fallback, isError: true, detail: "prompt=none");
            ShowAIResponse(fallback);
            return;
        }

        // 返答を待っている間は「……」を出す（止まって見えないように。README 4.6）
        dialoguePanel.SetActive(true);
        nextIndicator.SetActive(false);
        dialogueText.text = WaitingText;
        SetKeyGuide("返事を待っています…");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await chatGPT.SendChatMessage(
            text,
            prompt.systemPrompt
        );
        stopwatch.Stop();

        string shown = result.Success ? result.Text : AIErrorText;
        lastFailedInput = result.Success ? null : text;

        string detail = "prompt=" + promptPath + ";hash=" + ExperimentLogger.Hash8(prompt.systemPrompt) + ";latency_ms=" + stopwatch.ElapsedMilliseconds;
        if (!result.Success)
            detail += ";error=" + result.Error;

        ExperimentLogger.Log("ai_response", npcId: npcId, nodeId: nodeId, text: shown, isError: !result.Success, detail: detail);

        ShowAIResponse(shown);
    }

    // PromptData/Q{クエスト番号}/ → PromptData/Common/ → NPCDialogue.promptData の順で探す（README 4.5）
    // pathには見つかった場所（ログ用。例：Q1/NPC005）が入る
    private PromptData LoadPromptData(NPCDialogue npc, out string path)
    {
        int questNumber = ExperimentSettings.Instance != null
            ? ExperimentSettings.Instance.questNumber
            : 1;

        path = "Q" + questNumber + "/" + npc.npcId;
        PromptData prompt = Resources.Load<PromptData>("PromptData/" + path);
        if (prompt == null)
        {
            path = "Common/" + npc.npcId;
            prompt = Resources.Load<PromptData>("PromptData/" + path);
        }
        if (prompt == null)
        {
            path = "NPCDialogue/" + npc.npcId;
            prompt = npc.promptData;
        }

        return prompt;
    }

    private void ShowAIResponse(string reply)
    {
        currentState = GameState.InDialogue;

        dialoguePanel.SetActive(true);
        choicePanel.SetActive(false);

        dialogueText.text = JapaneseLineBreaker.Format(reply, dialogueText, maxDialogueLines);

        nextIndicator.SetActive(true);
        SetDialogueKeyGuide(currentNode.nextId < 0);
    }

    // =========================
    // Close
    // =========================

    public void CloseDialogue()
    {
        ExperimentLogger.Log("dialogue_end", npcId: CurrentNpcId);

        currentState = GameState.FreeMove;

        dialoguePanel.SetActive(false);
        choicePanel.SetActive(false);
        inputPanel.SetActive(false);
        nextIndicator.SetActive(false);

        SetTypingMode(false);

        ResetPlayerInputs();

        playerController.enabled = true;

        // 達成会話を閉じたら終了画面へ（README 3.5）
        if (QuestManager.Instance != null &&
            QuestManager.Instance.State == QuestState.Completed)
        {
            EndGame();
        }
    }

    private void EndGame()
    {
        currentState = GameState.Ended;

        talkHint.SetActive(false);
        StopPlayer();

        if (questCompletePanel != null)
            questCompletePanel.SetActive(true);

        ExperimentLogger.EndSessionNow("quest_complete");
    }

    // プレイヤーを止める。enabled=falseだけだと走りモーションと足音が続くため、速度も0にする
    private void StopPlayer()
    {
        if (playerController.enabled)
            playerController.StopMotion();

        playerController.enabled = false;
    }

    // 会話中・設定中に押されていた入力が、操作復帰後に残らないようにする
    private void ResetPlayerInputs()
    {
        if (inputs != null)
        {
            inputs.jump = false;
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
        }
    }

    // =========================
    // Settings（README 3.6）
    // =========================

    private void OpenSettings()
    {
        currentState = GameState.InSettings;

        talkHint.SetActive(false);
        SetTypingMode(true); // 移動停止・カーソル表示

        Time.timeScale = 0f;
        settingsPanel.Open();

        ExperimentLogger.Log("settings_open");
    }

    private void HandleSettingsInput()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            CloseSettings();
        }
    }

    // 設定パネルの「閉じる」ボタン（README 3.9）。Qで閉じたときと同じ処理
    public void CloseSettingsFromButton()
    {
        if (currentState == GameState.InSettings)
            CloseSettings();
    }

    private void CloseSettings()
    {
        ExperimentLogger.Log("settings_close");

        settingsPanel.Close();
        Time.timeScale = 1f;

        ResetPlayerInputs();
        SetTypingMode(false); // 移動再開・カーソル非表示

        currentState = GameState.FreeMove;
    }

    // 設定パネルの「初期位置に戻る」（README 3.6）。クエストの進み具合はそのまま
    public void RespawnPlayer()
    {
        var player = playerController.transform;
        Vector3 delta = playerStartPosition - player.position;

        ExperimentLogger.Log("respawn", detail: string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "from_x={0:0.00};from_z={1:0.00}", player.position.x, player.position.z));

        var cc = playerController.GetComponent<CharacterController>();
        cc.enabled = false;
        player.SetPositionAndRotation(playerStartPosition, playerStartRotation);
        cc.enabled = true;

        // カメラが村の上空を横切って追いかけないよう、瞬間移動したことを伝える
        foreach (var vcam in FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsSortMode.None))
            vcam.OnTargetObjectWarped(vcam.Follow != null ? vcam.Follow : player, delta);

        Debug.Log("[Player] Respawn to start position");

        if (currentState == GameState.InSettings)
            CloseSettings();
    }

    public void SetCurrentNPC(NPCDialogue npc)
    {
        currentNPC = npc;
    }

    private void SetTypingMode(bool active)
    {
        // プレイヤー操作制御
        if (active)
            StopPlayer();
        else
            playerController.enabled = true;

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