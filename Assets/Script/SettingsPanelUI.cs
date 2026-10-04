using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// 設定パネルの表示とスライダー・ボタンの処理（README 13章 設定パネル）
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

    public bool IsOpen => settingsPanel.activeSelf;

    private void Start()
    {
        bgmSlider.onValueChanged.AddListener(v => OnVolumeChanged(VolumeType.BGM, v));
        seSlider.onValueChanged.AddListener(v => OnVolumeChanged(VolumeType.SE, v));

        sensitivitySlider.minValue = LookSensitivitySettings.MinSensitivity;
        sensitivitySlider.maxValue = LookSensitivitySettings.MaxSensitivity;
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

        titleButton.onClick.AddListener(OnClickTitle);
        respawnButton.onClick.AddListener(OnClickRespawn);

        settingsPanel.SetActive(false);
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
        settingsPanel.SetActive(true);
    }

    public void Close()
    {
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
