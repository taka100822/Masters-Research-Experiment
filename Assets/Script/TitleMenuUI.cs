using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// タイトル画面のボタンと開発者用モーダル（README 3.1）
public class TitleMenuUI : MonoBehaviour
{
    public const string MainSceneName = "MainScene";

    [Header("タイトル")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button developerButton;
    [SerializeField] private TMP_Text settingsSummaryText;

    [Header("開発者用モーダル")]
    [SerializeField] private GameObject developerModal;
    [SerializeField] private TMP_InputField participantIdInput;
    [SerializeField] private TMP_Dropdown conditionDropdown;   // 0: A, 1: B
    [SerializeField] private TMP_Dropdown questDropdown;       // 0: クエスト1, 1: クエスト2
    [SerializeField] private Button closeButton;    // 「決定」：値を反映して閉じる
    [SerializeField] private Button cancelButton;   // 「キャンセル」：値を変えずに閉じる（README 3.8）。未設定でも動く
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
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelDeveloperModal);

        developerModal.SetActive(false);
        errorText.text = "";

        UpdateSummary();
    }

    private void OnClickStart()
    {
        // タイトルを通ったときは、表示中の設定を必ずMainSceneへ渡す
        ExperimentConfig.Set(ExperimentConfig.ParticipantId, ExperimentConfig.Condition, ExperimentConfig.QuestNumber);
        SceneManager.LoadScene(MainSceneName);
    }

    private void OpenDeveloperModal()
    {
        participantIdInput.text = ExperimentConfig.ParticipantId;
        conditionDropdown.SetValueWithoutNotify(ExperimentConfig.Condition == DialogueCondition.B_ChoiceAndInput ? 1 : 0);
        questDropdown.SetValueWithoutNotify(ExperimentConfig.QuestNumber - 1);
        errorText.text = "";
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

        var condition = conditionDropdown.value == 1
            ? DialogueCondition.B_ChoiceAndInput
            : DialogueCondition.A_ChoiceOnly;

        ExperimentConfig.Set(id, condition, questDropdown.value + 1);

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

    // 実験者の確認用。参加者が条件を意識しないよう「条件」の語は出さない（README 3.8）
    private void UpdateSummary()
    {
        string condition = ExperimentConfig.Condition == DialogueCondition.B_ChoiceAndInput ? "B" : "A";
        settingsSummaryText.text =
            ExperimentConfig.ParticipantId + "・" + condition + "・Q" + ExperimentConfig.QuestNumber;
    }
}
