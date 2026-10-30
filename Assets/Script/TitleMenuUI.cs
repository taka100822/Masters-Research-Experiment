using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// タイトル画面のボタンと開発者用モーダル（README 3.1）
public class TitleMenuUI : MonoBehaviour
{
    public const string MainSceneName = "MainScene";
    public const string TutorialSceneName = "TutorialScene";

    [Header("タイトル")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button developerButton;
    [SerializeField] private TMP_Text settingsSummaryText;

    [Header("開発者用モーダル")]
    [SerializeField] private GameObject developerModal;
    [SerializeField] private TMP_InputField participantIdInput;
    [SerializeField] private TMP_Dropdown roundDropdown;   // 0: 1回目から, 1: 2回目から（README 3.1）
    [Tooltip("IDから決まるグループと各回の条件・話題セット（入力不可）")]
    [SerializeField] private TMP_Text groupInfoText;
    [SerializeField] private Button closeButton;    // 「決定」：値を反映して閉じる
    [SerializeField] private Button cancelButton;   // 「キャンセル」：値を変えずに閉じる（README 3.1）。未設定でも動く
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private TMP_Text logPathText;   // ログの保存先（README 6.2）

    [Tooltip("モーダル表示中に隠すもの（タイトル文字・はじめるボタンが窓の後ろからはみ出して見えないように）")]
    [SerializeField] private GameObject[] hideWhileModalOpen;

    private void Start()
    {
        // MainSceneではStarter Assetsがカーソルを固定するので、タイトルでは必ず表示する
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        startButton.onClick.AddListener(OnClickStart);
        developerButton.onClick.AddListener(OpenDeveloperModal);
        closeButton.onClick.AddListener(CloseDeveloperModal);
        participantIdInput.onValueChanged.AddListener(_ => UpdateGroupInfo());
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelDeveloperModal);

        developerModal.SetActive(false);
        errorText.text = "";

        UpdateSummary();
    }

    private void OnClickStart()
    {
        // 参加者IDが未設定のうちは始めない（ボタンも押せない。README 3.1）
        if (ExperimentConfig.Group == 0)
            return;

        // タイトルを通ったときは、表示中の設定を必ず次のシーンへ渡す
        ExperimentConfig.Set(ExperimentConfig.ParticipantId, ExperimentConfig.Round);

        // ふだんはチュートリアルから。Shiftを押しながらなら飛ばす（実験者の動作確認用。README 2.4）
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool skipTutorial = keyboard != null && keyboard.shiftKey.isPressed;
        SceneManager.LoadScene(skipTutorial ? MainSceneName : TutorialSceneName);
    }

    private void OpenDeveloperModal()
    {
        participantIdInput.text = ExperimentConfig.ParticipantId;
        roundDropdown.SetValueWithoutNotify(ExperimentConfig.Round - 1);
        errorText.text = "";
        UpdateGroupInfo();
        if (logPathText != null)
            logPathText.text = "ログ保存先：" + ExperimentLogger.LogDirectory;

        developerModal.SetActive(true);
        SetTitleContentVisible(false);
    }

    private void CloseDeveloperModal()
    {
        string id = participantIdInput.text.Trim();
        if (string.IsNullOrEmpty(id))
        {
            errorText.text = "参加者IDを入力してください";
            return;
        }
        if (ExperimentConfig.GroupOf(id) == 0)
        {
            errorText.text = "参加者IDの末尾に番号を付けてください";
            return;
        }

        ExperimentConfig.Set(id, roundDropdown.value + 1);

        developerModal.SetActive(false);
        SetTitleContentVisible(true);
        UpdateSummary();
    }

    // 入力した値は捨てる。次に開いたときはExperimentConfigの値から入れ直す
    private void CancelDeveloperModal()
    {
        developerModal.SetActive(false);
        SetTitleContentVisible(true);
    }

    private void SetTitleContentVisible(bool visible)
    {
        foreach (var go in hideWhileModalOpen)
            if (go != null)
                go.SetActive(visible);
    }

    // 入力中のIDから決まる組み合わせを見せる（例：グループ1：1回目 A・S1／2回目 B・S2）
    private void UpdateGroupInfo()
    {
        if (groupInfoText == null)
            return;

        int group = ExperimentConfig.GroupOf(participantIdInput.text.Trim());
        groupInfoText.text = group == 0
            ? "→ グループ：IDの番号から決まります"
            : "→ グループ" + group + "：1回目 " + PlayLabel(group, 1) + "／2回目 " + PlayLabel(group, 2);
    }

    private static string PlayLabel(int group, int round)
    {
        string condition = ExperimentConfig.ConditionOf(group, round) == DialogueCondition.B_FreeInputOnly ? "B" : "A";
        return condition + "・S" + ExperimentConfig.TopicSetOf(group, round);
    }

    // 実験者の確認用。参加者が条件を意識しないよう「条件」の語は出さない（README 3.1）
    // IDが未設定のうちは「はじめる」を押せない
    private void UpdateSummary()
    {
        bool ready = ExperimentConfig.Group != 0;
        startButton.interactable = ready;

        settingsSummaryText.text = ready
            ? ExperimentConfig.ParticipantId + "・" + ExperimentConfig.Round + "回目・" + PlayLabel(ExperimentConfig.Group, ExperimentConfig.Round)
            : "未設定";
    }
}
