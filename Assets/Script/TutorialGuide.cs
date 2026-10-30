using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using StarterAssets;

// 本番前のチュートリアル（README 2.4）。TutorialSceneだけに置く
// 「やること」を1つずつ出し、できたら次へ進める。最後にEnterでMainSceneへ
// 会話・設定パネルの出来事はDialogueManagerから知らせてもらう
public class TutorialGuide : MonoBehaviour
{
    public static TutorialGuide Instance;

    // チュートリアル中かどうか（会話データ・プロンプト・ログの置き場所の切り替えに使う）
    public static bool Active => Instance != null;

    [Header("やることの札（README 2.4）")]
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private TMP_Text guideText;

    [Header("案内役の村人")]
    [SerializeField] private string firstNpcId = "TUT001";
    [SerializeField] private string secondNpcId = "TUT002";

    [Header("進め方")]
    [Tooltip("1つ目のやること（歩く）を終えるのに歩く距離（m）")]
    [SerializeField] private float walkDistance = 3f;
    [Tooltip("できたことを見せてから次へ進むまでの秒数（実時間）")]
    [SerializeField] private float advanceDelay = 1f;
    [Tooltip("札の高さの最小値。文が長いときは行数に合わせて伸びる")]
    [SerializeField] private float minPanelHeight = 88f;

    private const string KeyColor = "#E0B354";
    private const int LastStep = 6;

    // 出すやることの番号。2回目は話しかけ・話し方・最後だけ（README 2.4「2回目の簡易版」）
    private static readonly int[] FullSteps = { 1, 2, 3, 4, 5, 6 };
    private static readonly int[] ShortSteps = { 2, 3, 6 };

    private int[] steps = FullSteps;
    private int step;
    private bool completing;
    private bool waitingForStart;

    private Transform player;
    private Vector3 lastPlayerPosition;
    private float walked;

    // 今の会話の相手と、その会話で選択肢を決めた・AIの返事を受けた回数
    private string dialogueNpc;
    private int dialogueActions;
    private bool settingsOpenedThisStep;

    // 毎フレーム探さないように、最初に作った文を使い回す
    private string talkBlockedHint;

    private DialogueManager dialogueManager;

    private static bool FreeInputOnly => ExperimentSettings.Instance != null && ExperimentSettings.Instance.FreeInputOnly;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        dialogueManager = FindAnyObjectByType<DialogueManager>();

        var controller = FindAnyObjectByType<ThirdPersonController>();
        if (controller != null)
        {
            player = controller.transform;
            lastPlayerPosition = player.position;
        }

        if (guidePanel != null)
            guidePanel.SetActive(true);
        else
            Debug.LogError("[Tutorial] やることの札（guidePanel）がありません。Tools/研究用/チュートリアルのシーンを作り直す で作り直してください");

        // 2回目は簡易版（README 2.4）。エディタでこのシーンを直接Playしたときは1回目扱い
        bool secondPlay = ExperimentConfig.HasValue && ExperimentConfig.Round == 2;
        steps = secondPlay ? ShortSteps : FullSteps;

        ExperimentLogger.Log("tutorial_start", detail: "condition=" + (FreeInputOnly ? "B" : "A") + ";mode=" + (secondPlay ? "short" : "full") + ";round=" + ExperimentConfig.Round);
        ShowStep(steps[0]);
    }

    private void Update()
    {
        if (completing)
            return;

        if (step == 1 && player != null)
        {
            // 横方向の移動だけを数える（ジャンプの上下は入れない）
            Vector3 d = player.position - lastPlayerPosition;
            d.y = 0f;
            walked += d.magnitude;
            lastPlayerPosition = player.position;

            if (walked >= walkDistance)
                Complete();
        }
        else if (step == LastStep && waitingForStart && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            Finish();
        }
    }

    // =========================
    // DialogueManagerから呼ぶ
    // =========================

    // クリスは4の札が出るまで話しかけられない（README 2.4）
    public bool CanTalkTo(string npcId)
    {
        return npcId != secondNpcId || step >= 4;
    }

    // 話しかけられない相手に近づいたときの案内の札の文。4より前は、次に話す相手がいつもケビン
    public string TalkBlockedHint()
    {
        if (talkBlockedHint == null)
            talkBlockedHint = "先に" + NpcName(firstNpcId, "ケビン") + "と話しましょう";
        return talkBlockedHint;
    }

    public void OnDialogueStarted(string npcId)
    {
        dialogueNpc = npcId;
        dialogueActions = 0;

        if (!completing && step == 2 && npcId == firstNpcId)
            Complete();
    }

    // 選択肢を決めた（条件A）・AIの返事を表示した（条件B）
    public void OnDialogueAction()
    {
        dialogueActions++;
    }

    public void OnDialogueClosed(string npcId)
    {
        bool talked = dialogueActions > 0;
        dialogueNpc = null;
        dialogueActions = 0;

        if (completing || !talked)
            return;

        if ((step == 3 && npcId == firstNpcId) || (step == 4 && npcId == secondNpcId))
            Complete();
    }

    public void OnSettingsOpened()
    {
        if (step == 5)
            settingsOpenedThisStep = true;
    }

    public void OnSettingsClosed()
    {
        if (!completing && step == 5 && settingsOpenedThisStep)
            Complete();
    }

    // =========================
    // やること
    // =========================

    private void ShowStep(int n)
    {
        step = n;
        completing = false;
        settingsOpenedThisStep = false;

        if (n == LastStep && dialogueManager != null)
            dialogueManager.LockForTutorialEnd();

        SetText(StepText(n));

        // 最後のやることは、表示した次のフレームからEnterを受け付ける
        if (n == LastStep)
            StartCoroutine(EnableStartNextFrame());
    }

    private IEnumerator EnableStartNextFrame()
    {
        yield return null;
        waitingForStart = true;
    }

    private string StepText(int n)
    {
        string first = NpcName(firstNpcId, "ケビン");
        string second = NpcName(secondNpcId, "クリス");

        // 改行は文の切れ目に手で入れる。1行は全角30字まで（文字の幅792px・26pxで約30字。README 2.4）
        switch (n)
        {
            case 1:
                return Key("W") + Key("A") + Key("S") + Key("D") + " で歩いてみましょう。\nマウスで見回せます";
            case 2:
                // 2回目の簡易版では、1回目と話し方が変わることを先に知らせる（「条件」とは言わない。README 2.4）
                return (steps == ShortSteps ? "今回は、村人との話し方が前回と違います。\n" : "右上の地図の白い丸が村人です。\n")
                    + first + "に近づいて " + Key("Enter") + " で話しかけましょう";
            case 3:
                return FreeInputOnly
                    ? "話したいことを書いて「送信」を押しましょう。\n返事を読んだら「会話をやめる」で終えます"
                    : Key("↑") + Key("↓") + " で選んで " + Key("Enter") + " で決めましょう。\n会話を最後まで進めます";
            case 4:
                return FreeInputOnly
                    ? second + "にも話しかけて、何か聞いてみましょう。\n終わったら「会話をやめる」で終えます"
                    : second + "にも話しかけて、\n会話を最後まで進めましょう";
            case 5:
                return Key("Q") + " で設定を開けます。音の大きさを変えたり、\n動けなくなったときに「初期位置に戻る」を押したりできます。\n開いて閉じてみましょう";
            default:
                // 時間は本番の制限時間から作る（README 2.4・5.1）
                var time = ExplorationSettings.Current;
                return "本番では右上に残り時間が出て、" + ExplorationSettings.FormatDuration(time.durationSeconds) + "たつと自動で終わります。\n残り"
                    + ExplorationSettings.FormatDuration(time.warningSeconds) + "になると、残り時間の色が変わります。\n準備ができたら " + Key("Enter");
        }
    }

    // 札の名前は頭上の名前と同じにする（NPCDialogue.DisplayName。見つからなければ予備の名前）
    private static string NpcName(string npcId, string fallback)
    {
        foreach (var npc in FindObjectsByType<NPCDialogue>(FindObjectsSortMode.None))
            if (npc.npcId == npcId)
                return npc.DisplayName;
        return fallback;
    }

    private void Complete()
    {
        completing = true;

        ExperimentLogger.Log("tutorial_step", detail: "step=" + step);

        SetText(StepText(step) + "\n<color=" + KeyColor + ">できました！</color>");
        StartCoroutine(AdvanceAfterDelay());
    }

    private IEnumerator AdvanceAfterDelay()
    {
        // 設定パネルを閉じた直後などtimeScaleに関係なく進める
        yield return new WaitForSecondsRealtime(advanceDelay);
        ShowStep(steps[System.Array.IndexOf(steps, step) + 1]);
    }

    private void Finish()
    {
        waitingForStart = false;
        completing = true;

        ExperimentLogger.Log("tutorial_end");
        ExperimentLogger.EndSessionNow("tutorial_end");

        SceneManager.LoadScene(TitleMenuUI.MainSceneName);
    }

    // 文節の途中では折り返さない（README 3.4と同じ処理）。札の高さは行数に合わせて伸ばす
    private void SetText(string text)
    {
        if (guideText == null)
            return;

        guideText.text = JapaneseLineBreaker.Format(text);

        if (guidePanel != null && guidePanel.transform is RectTransform panel)
        {
            guideText.ForceMeshUpdate();
            float padding = -guideText.rectTransform.sizeDelta.y;   // 札と文字の上下の余白の合計
            float height = Mathf.Max(minPanelHeight, guideText.preferredHeight + padding);
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, height);
        }
    }

    private static string Key(string key)
    {
        return "<color=" + KeyColor + ">[" + key + "]</color>";
    }
}
