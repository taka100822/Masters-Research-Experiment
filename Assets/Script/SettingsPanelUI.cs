using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 設定パネルの表示とスライダー・ボタンの処理（README 13章 設定パネル）
// 開閉のタイミング（Qキー・状態遷移）はDialogueManagerが管理する
public class SettingsPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;
    [SerializeField] private TMP_Text bgmValueText;
    [SerializeField] private TMP_Text seValueText;
    [SerializeField] private Button titleButton;

    public bool IsOpen => settingsPanel.activeSelf;

    private void Start()
    {
        bgmSlider.onValueChanged.AddListener(v => OnSliderChanged(VolumeType.BGM, v));
        seSlider.onValueChanged.AddListener(v => OnSliderChanged(VolumeType.SE, v));
        titleButton.onClick.AddListener(OnClickTitle);

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

        UpdateValueTexts();
        settingsPanel.SetActive(true);
    }

    public void Close()
    {
        settingsPanel.SetActive(false);
    }

    private void OnSliderChanged(VolumeType type, float value)
    {
        if (AudioVolumeSettings.Instance != null)
            AudioVolumeSettings.Instance.SetVolume(type, value);

        UpdateValueTexts();
    }

    private void UpdateValueTexts()
    {
        bgmValueText.text = Mathf.RoundToInt(bgmSlider.value * 100) + "%";
        seValueText.text = Mathf.RoundToInt(seSlider.value * 100) + "%";
    }

    private void OnClickTitle()
    {
        // タイトル画面はまだ無い
        Debug.Log("[Settings] タイトルへ戻る（未実装）");
    }
}
