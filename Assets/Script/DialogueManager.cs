using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
    [Tooltip("終了画面（README 3.7）")]
    [SerializeField] private GameObject questCompletePanel;
    [Tooltip("探索前の説明画面（README 3.7）。未設定ならすぐに探索を始める")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private SettingsPanelUI settingsPanel;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text[] choiceTexts;
    [Tooltip("本文が収まる行数。収まるときだけ文の終わりで改行する（README 3.4）")]
    [SerializeField] private int maxDialogueLines = 3;

    // 会話UIの見た目（README 3.3）。未設定でも動く
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
    private static readonly Color KeyGuideHighlightColor = new Color32(0xE0, 0xB3, 0x54, 0xFF); // 真鍮
    private static readonly Color SendFlashColor = new Color32(0xF6, 0xEE, 0xDC, 0xFF);         // 漆喰

    // 自由入力中にEnterを押したときの合図（README 3.3）
    private const float SendFlashSeconds = 0.5f;
    private const float KeyGuideHighlightSeconds = 1f;
    private Coroutine enterNudge;
    private Color sendButtonBaseColor;
    private Color keyGuideBaseColor;

    // IMEの変換中か・最後に変換中の文字が変わったフレーム（変換を確定するEnterを見分ける）
    private bool imeComposing;
    private int lastImeCompositionFrame = -10;

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
    // 待っている間は「……」から始めて0.4秒ごとに点の数を替える（止まって見えないように。README 4.6）
    private static readonly string[] WaitingTexts = { "…", "……", "………" };
    private const float WaitingTextSeconds = 0.4f;
    private Coroutine waitingAnimation;
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

        // 空欄（空白だけ）の間は送信できない（README 3.3）
        inputField.onValueChanged.AddListener(_ => UpdateSendButton());

        if (sendButton != null && sendButton.targetGraphic != null)
            sendButtonBaseColor = sendButton.targetGraphic.color;
        if (keyGuideText != null)
            keyGuideBaseColor = keyGuideText.color;
        if (Keyboard.current != null)
            Keyboard.current.onIMECompositionChange += OnIMECompositionChange;

        if (ExplorationTimer.Instance != null)
            ExplorationTimer.Instance.OnTimeUp += HandleTimeUp;

        // 説明画面を閉じるまでは動けない。閉じたら5分を数え始める（README 3.7）
        if (introPanel != null)
        {
            currentState = GameState.Intro;
            introPanel.SetActive(true);
            StopPlayer();
        }
        else
        {
            BeginExploration();
        }
    }

    private void OnDestroy()
    {
        if (ExplorationTimer.Instance != null)
            ExplorationTimer.Instance.OnTimeUp -= HandleTimeUp;
        if (Keyboard.current != null)
            Keyboard.current.onIMECompositionChange -= OnIMECompositionChange;
    }

    private void OnIMECompositionChange(UnityEngine.InputSystem.LowLevel.IMECompositionString composition)
    {
        imeComposing = !string.IsNullOrEmpty(composition.ToString());
        lastImeCompositionFrame = Time.frameCount;
    }

    // =========================
    // 説明画面と制限時間（README 3.7・5.1）
    // =========================

    private static bool IsTimeUp => ExplorationTimer.Instance != null && ExplorationTimer.Instance.IsTimeUp;

    private void HandleIntroInput()
    {
        if (!Keyboard.current.enterKey.wasPressedThisFrame)
            return;

        introPanel.SetActive(false);
        ResetPlayerInputs();
        playerController.enabled = true;
        currentState = GameState.FreeMove;

        BeginExploration();
    }

    private void BeginExploration()
    {
        if (ExplorationTimer.Instance != null)
            ExplorationTimer.Instance.Begin();
    }

    // 途中で切らず、今のやりとりの区切りで終了画面にする。探索中ならすぐ（README 5.1）
    private void HandleTimeUp()
    {
        ExperimentLogger.Log("time_up", detail: "state=" + currentState);

        if (currentState == GameState.FreeMove)
            EndGame();
    }

    private void UpdateSendButton()
    {
        if (sendButton != null)
            sendButton.interactable = !string.IsNullOrWhiteSpace(inputField.text);
    }

    // 操作案内（README 3.3）
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
        if (DeveloperBackToTitlePressed())
        {
            BackToTitle();
            return;
        }

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

            case GameState.Intro:
                HandleIntroInput();
                break;
        }
    }

    // =========================
    // 開発者用コマンド（README 2.5）
    // =========================

    private const string TitleSceneName = "TitleScene";

    // Ctrl+Shift+T。どの状態でも効く。画面には出さない（参加者に見せないため）
    private static bool DeveloperBackToTitlePressed()
    {
        var keyboard = Keyboard.current;
        return keyboard != null &&
               keyboard.ctrlKey.isPressed &&
               keyboard.shiftKey.isPressed &&
               keyboard.tKey.wasPressedThisFrame;
    }

    // タイトルへ戻る。進み具合はリセットされる（シーンを読み直すため）。参加者ID・回は残る
    private void BackToTitle()
    {
        Debug.Log("[Developer] Back to title");
        ExperimentLogger.EndSessionNow("back_to_title");

        Time.timeScale = 1f; // 設定パネル表示中は止めているので戻してから移る
        SceneManager.LoadScene(TitleSceneName);
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
        if (IsTimeUp)
            return;

        // 設定パネルは探索中だけ開ける（自由入力中の「q」で開かないように）
        if (Keyboard.current.qKey.wasPressedThisFrame && settingsPanel != null)
        {
            OpenSettings();
            return;
        }

        talkHint.SetActive(currentNPC != null);

        // チュートリアルでは、札の順番がまだ来ていない相手とは話せない（README 2.4）
        bool canTalk = currentNPC != null
            && (TutorialGuide.Instance == null || TutorialGuide.Instance.CanTalkTo(currentNPC.npcId));

        if (currentNPC != null && talkHintText != null)
            talkHintText.text = canTalk
                ? Key("Enter") + " " + currentNPC.DisplayName + "と話す"
                : TutorialGuide.Instance.TalkBlockedHint();

        if (!TalkPressed() || !canTalk)
            return;

        if (!LoadDialogueCSV(currentNPC.csvFileName))
        {
            Debug.LogError("Dialogue CSV not found: " + currentNPC.csvFileName);
            return;
        }

        // 会話はいつも0から始まる（README 5.3）
        StartNode(0);
    }

    private static int TopicSet => ExperimentSettings.Instance != null ? ExperimentSettings.Instance.topicSet : 1;

    // Dialogue/S{話題セット番号}/ から読む（README 4.4）。チュートリアルでは Dialogue/Tutorial/（README 2.4）
    private static string DataFolder => TutorialGuide.Active ? "Tutorial" : "S" + TopicSet;

    private bool LoadDialogueCSV(string csvFileName)
    {
        return dialogueDatabase.LoadCSV("Dialogue/" + DataFolder + "/" + csvFileName);
    }

    private void StartNode(int id)
    {
        currentNodeId = id;
        currentNode = dialogueDatabase.GetNode(id);

        currentState = GameState.InDialogue;

        dialoguePanel.SetActive(true);
        talkHint.SetActive(false);

        StopPlayer();

        ExperimentLogger.Log("dialogue_start", npcId: CurrentNpcId, nodeId: id);
        TutorialGuide.Instance?.OnDialogueStarted(CurrentNpcId);

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

    // 条件B：選択肢を出さず、ノード0のセリフのあとは自由入力だけで話す（README 1.8）
    private static bool FreeInputOnly => ExperimentSettings.Instance != null && ExperimentSettings.Instance.FreeInputOnly;

    private void ShowChoicesFromNode()
    {
        if (FreeInputOnly)
        {
            currentState = GameState.InDialogue;
            nextIndicator.SetActive(true);
            SetDialogueKeyGuide(false);
            return;
        }

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

        // 時間切れのあとは次のノードへ進まない
        if (IsTimeUp)
        {
            CloseDialogue();
            return;
        }

        // 条件B：あいさつ・AIの返答のあとは自由入力へ（README 1.8）
        if (FreeInputOnly)
        {
            OpenInputMode();
            return;
        }

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

    // CSVのaction列。topic:<番号>で話題を聞いたことを記録する（README 5.3）
    private void ExecuteNodeAction(string action)
    {
        if (string.IsNullOrEmpty(action))
            return;

        if (action.StartsWith("topic:"))
            ExperimentLogger.Log("topic_heard", npcId: CurrentNpcId, nodeId: currentNodeId,
                detail: "topic=" + action.Substring("topic:".Length));
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

    // 選択中の行は▶＋真鍮の下地、使わない行は隠す（README 3.3）
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
            TutorialGuide.Instance?.OnDialogueAction();

            // 時間切れのあとは選んだ先へ進まない（README 5.1）
            if (IsTimeUp)
            {
                CloseDialogue();
                return;
            }

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

    // 「会話をやめる」ボタン（README 1.8・3.3）。書きかけの文章は捨てて会話を閉じる
    // （条件Aでは自由入力の窓が出ないので、ここに来るのは条件Bだけ）
    public void OnClickCancelInput()
    {
        if (currentState != GameState.InTyping)
            return;

        StopEnterNudge();
        inputPanel.SetActive(false);
        inputField.text = "";
        lastFailedInput = null;

        CloseDialogue("end=quit_button");
    }

    // Enterでは送信しない（IMEの変換中のEnterで誤送信しないため。README 4.1）。
    // 代わりに入力欄を選び直し、文字があれば「送信」を目立たせる（README 3.3）
    private void HandleTypingInput()
    {
        if (!Keyboard.current.enterKey.wasPressedThisFrame)
            return;

        // AIの返事を待っている間（入力の窓が隠れている間）は何もしない（README 3.3）
        if (!inputPanel.activeSelf)
            return;

        // 変換を確定するEnterは無視する（このフレームか1つ前に変換中の文字があった）
        if (imeComposing || Time.frameCount - lastImeCompositionFrame <= 1)
            return;

        StopEnterNudge();
        enterNudge = StartCoroutine(EnterNudge());
    }

    private System.Collections.IEnumerator EnterNudge()
    {
        bool highlight = !string.IsNullOrWhiteSpace(inputField.text);
        if (highlight && keyGuideText != null)
            keyGuideText.color = KeyGuideHighlightColor;

        // 1行の入力欄はEnterで選択が外れるので、次のフレームで選び直して文の最後にカーソルを置く
        yield return null;
        inputField.ActivateInputField();
        float start = Time.unscaledTime;
        yield return null;
        inputField.MoveTextEnd(false);

        if (highlight)
        {
            float t;
            while ((t = Time.unscaledTime - start) < KeyGuideHighlightSeconds)
            {
                if (sendButton != null && sendButton.targetGraphic != null)
                {
                    float k = t < SendFlashSeconds ? Mathf.Sin(t / SendFlashSeconds * Mathf.PI) : 0f;
                    sendButton.targetGraphic.color = Color.Lerp(sendButtonBaseColor, SendFlashColor, k);
                }
                yield return null;
            }
        }

        enterNudge = null;
        RestoreEnterNudgeColors();
    }

    private void StopEnterNudge()
    {
        if (enterNudge != null)
        {
            StopCoroutine(enterNudge);
            enterNudge = null;
        }
        RestoreEnterNudgeColors();
    }

    private void RestoreEnterNudgeColors()
    {
        if (sendButton != null && sendButton.targetGraphic != null)
            sendButton.targetGraphic.color = sendButtonBaseColor;
        if (keyGuideText != null)
            keyGuideText.color = keyGuideBaseColor;
    }

    public void OnClickSend()
    {
        StopEnterNudge();
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
        StopWaitingAnimation();
        waitingAnimation = StartCoroutine(AnimateWaitingText());
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

    // PromptData/S{話題セット番号}/ → NPCDialogue.promptData の順で探す（README 4.5）
    // pathには見つかった場所（ログ用。例：S1/NPC005、チュートリアルではTutorial/TUT001）が入る
    private PromptData LoadPromptData(NPCDialogue npc, out string path)
    {
        path = DataFolder + "/" + npc.npcId;
        PromptData prompt = Resources.Load<PromptData>("PromptData/" + path);
        if (prompt == null)
        {
            path = "NPCDialogue/" + npc.npcId;
            prompt = npc.promptData;
        }

        return prompt;
    }

    private System.Collections.IEnumerator AnimateWaitingText()
    {
        for (int i = 1; ; i = (i + 1) % WaitingTexts.Length)
        {
            dialogueText.text = WaitingTexts[i];
            yield return new WaitForSecondsRealtime(WaitingTextSeconds);
        }
    }

    private void StopWaitingAnimation()
    {
        if (waitingAnimation == null)
            return;

        StopCoroutine(waitingAnimation);
        waitingAnimation = null;
    }

    private void ShowAIResponse(string reply)
    {
        StopWaitingAnimation();
        currentState = GameState.InDialogue;

        dialoguePanel.SetActive(true);
        choicePanel.SetActive(false);

        dialogueText.text = JapaneseLineBreaker.Format(reply, dialogueText, maxDialogueLines);
        TutorialGuide.Instance?.OnDialogueAction();

        nextIndicator.SetActive(true);
        SetDialogueKeyGuide(!FreeInputOnly && currentNode.nextId < 0);
    }

    // =========================
    // Close
    // =========================

    public void CloseDialogue(string detail = null)
    {
        ExperimentLogger.Log("dialogue_end", npcId: CurrentNpcId, detail: detail);
        TutorialGuide.Instance?.OnDialogueClosed(CurrentNpcId);

        currentState = GameState.FreeMove;

        dialoguePanel.SetActive(false);
        choicePanel.SetActive(false);
        inputPanel.SetActive(false);
        nextIndicator.SetActive(false);

        SetTypingMode(false);

        ResetPlayerInputs();

        playerController.enabled = true;

        // 時間切れのあとに会話を閉じたら終了画面へ（README 5.1）
        if (IsTimeUp)
            EndGame();
    }

    private void EndGame()
    {
        currentState = GameState.Ended;

        talkHint.SetActive(false);
        StopPlayer();

        if (questCompletePanel != null)
            questCompletePanel.SetActive(true);

        ExperimentLogger.EndSessionNow("time_up");
    }

    // チュートリアルの最後（「準備ができたら Enter」）。話しかけ・移動・設定パネルを止める（README 2.4）
    public void LockForTutorialEnd()
    {
        currentState = GameState.Ended;

        talkHint.SetActive(false);
        StopPlayer();
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
    // Settings（README 3.8）
    // =========================

    private void OpenSettings()
    {
        currentState = GameState.InSettings;

        talkHint.SetActive(false);
        SetTypingMode(true); // 移動停止・カーソル表示

        Time.timeScale = 0f;
        settingsPanel.Open();

        ExperimentLogger.Log("settings_open");
        TutorialGuide.Instance?.OnSettingsOpened();
    }

    private void HandleSettingsInput()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            CloseSettings();
        }
    }

    // 設定パネルの「閉じる」ボタン（README 3.8）。Qで閉じたときと同じ処理
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
        TutorialGuide.Instance?.OnSettingsClosed();
    }

    // 設定パネルの「初期位置に戻る」（README 3.8）。残り時間はそのまま
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