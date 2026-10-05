using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// 設定パネルの表示とスライダー・ボタンの処理（README 3.6）
// 開閉のタイミング（Qキー・状態遷移）はDialogueManagerが管理する
public class SettingsPanelUI : MonoBehaviour
{
    public const string TitleSceneName = "TitleScene";

    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text bgmValueText;
    [SerializeField] private TMP_Text seValueText;
    [SerializeField] private TMP_Text sensitivityValueText;
    [SerializeField] private Button titleButton;
    [SerializeField] private Button respawnButton;

    // 「タイトルへ戻る」の確認と「閉じる」ボタン（README 3.9）。未設定なら確認なしですぐ戻る
    [Header("確認・閉じる")]
    [SerializeField] private GameObject actionGroup;
    [SerializeField] private GameObject confirmGroup;
    [SerializeField] private Button confirmTitleButton;
    [SerializeField] private Button cancelTitleButton;
    [SerializeField] private Button closeButton;

    public bool IsOpen => settingsPanel.activeSelf;

    private void Start()
    {
        bgmSlider.onValueChanged.AddListener(v => OnVolumeChanged(VolumeType.BGM, v));
        seSlider.onValueChanged.AddListener(v => OnVolumeChanged(VolumeType.SE, v));

        sensitivitySlider.minValue = LookSensitivitySettings.MinSensitivity;
        sensitivitySlider.maxValue = LookSensitivitySettings.MaxSensitivity;
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

        respawnButton.onClick.AddListener(OnClickRespawn);

        if (confirmGroup != null && confirmTitleButton != null)
        {
            titleButton.onClick.AddListener(() => ShowTitleConfirm(true));
            confirmTitleButton.onClick.AddListener(OnClickTitle);
            if (cancelTitleButton != null)
                cancelTitleButton.onClick.AddListener(() => ShowTitleConfirm(false));
        }
        else
        {
            titleButton.onClick.AddListener(OnClickTitle);
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(OnClickClose);

        settingsPanel.SetActive(false);
    }

    // ふだんのボタンの並びと確認を切り替える。確認中は「閉じる」を隠し、決定のボタンを1つにする（Qでは閉じられる）
    private void ShowTitleConfirm(bool show)
    {
        if (confirmGroup != null)
            confirmGroup.SetActive(show);
        if (actionGroup != null)
            actionGroup.SetActive(!show);
        if (closeButton != null)
            closeButton.gameObject.SetActive(!show);
    }

    private void OnClickClose()
    {
        var dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null)
            dialogueManager.CloseSettingsFromButton();
    }

    public void Open()
    {
        var volume = AudioVolumeSettings.Instance;
        if (volume != null)
        {
            bgmSlider.SetValueWithoutNotify(volume.BGMVolume);
            seSlider.SetValueWithoutNotify(volume.SEVolume);
        }

        if (LookSensitivitySettings.Instance != null)
            sensitivitySlider.SetValueWithoutNotify(LookSensitivitySettings.Instance.Sensitivity);

        UpdateValueTexts();
        ShowTitleConfirm(false);
        settingsPanel.SetActive(true);
    }

    // 確認中に閉じたら確認は取り消す（次に開いたときはふだんの並びから）
    public void Close()
    {
        ShowTitleConfirm(false);
        settingsPanel.SetActive(false);
    }

    private void OnVolumeChanged(VolumeType type, float value)
    {
        if (AudioVolumeSettings.Instance != null)
            AudioVolumeSettings.Instance.SetVolume(type, value);

        UpdateValueTexts();
    }

    private void OnSensitivityChanged(float value)
    {
        if (LookSensitivitySettings.Instance != null)
            LookSensitivitySettings.Instance.SetSensitivity(value);

        UpdateValueTexts();
    }

    private void UpdateValueTexts()
    {
        bgmValueText.text = Mathf.RoundToInt(bgmSlider.value * 100) + "%";
        seValueText.text = Mathf.RoundToInt(seSlider.value * 100) + "%";
        sensitivityValueText.text = sensitivitySlider.value.ToString("0.0");
    }

    // タイトルへ戻る。クエストの進み具合はリセットされる（MainSceneを読み直すため）
    private void OnClickTitle()
    {
        Debug.Log("[Settings] Back to title");
        ExperimentLogger.EndSessionNow("back_to_title");

        Time.timeScale = 1f; // 設定パネル表示中は止めているので戻してから移る
        SceneManager.LoadScene(TitleSceneName);
    }

    private void OnClickRespawn()
    {
        var dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null)
            dialogueManager.RespawnPlayer();
    }
}
