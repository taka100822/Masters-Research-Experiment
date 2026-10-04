using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// タイトル画面のボタンと開発者用モーダル（README 13章 タイトル画面）
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
    [SerializeField] private Button closeButton;
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

    private void SetTitleContentVisible(bool visible)
    {
        foreach (var go in hideWhileModalOpen)
            if (go != null)
                go.SetActive(visible);
    }

    private void UpdateSummary()
    {
        string condition = ExperimentConfig.Condition == DialogueCondition.B_ChoiceAndInput ? "B" : "A";
        settingsSummaryText.text =
            "参加者 " + ExperimentConfig.ParticipantId +
            " ／ 条件" + condition +
            " ／ クエスト" + ExperimentConfig.QuestNumber;
    }
}
