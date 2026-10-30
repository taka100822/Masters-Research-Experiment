using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// 時間切れのあとのアンケート画面と、2回目のあとのおわりの画面（README 3.7）
// 終了画面（QuestCompletePanel）に付ける。DialogueManager.EndGameが表示したときに中身を整える
public class EndScreenUI : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;

    [Header("アンケート回答完了")]
    [SerializeField] private GameObject actionGroup;
    [SerializeField] private Button completeButton;

    [Header("確認（間違って押してもすぐ進まないように）")]
    [SerializeField] private GameObject confirmGroup;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("BGM（アンケート画面を出したらフェードアウトして止める。README 3.7）")]
    [SerializeField] private AudioSource bgm;
    [SerializeField] private float bgmFadeSeconds = 1.5f;

    private const string QuestionnaireText =
        "時間になりました\n\n<size=24>お疲れさまでした。\n担当者の指示があるまでお待ちください。</size>";
    private const string FinishedText =
        "以上で終わりです\n\n<size=24>ご協力ありがとうございました。\n実験者の指示があるまでお待ちください。</size>";

    private void Awake()
    {
        completeButton.onClick.AddListener(() => ShowConfirm(true));
        noButton.onClick.AddListener(() => ShowConfirm(false));
        yesButton.onClick.AddListener(OnConfirmed);
    }

    private void OnEnable()
    {
        // ボタンを押せるようにカーソルを出す
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        messageText.text = QuestionnaireText;
        ShowConfirm(false);

        if (bgm != null && bgm.isPlaying)
            StartCoroutine(FadeOutBGM());
    }

    private IEnumerator FadeOutBGM()
    {
        float startVolume = bgm.volume;
        for (float t = 0f; t < bgmFadeSeconds; t += Time.unscaledDeltaTime)
        {
            bgm.volume = startVolume * (1f - t / bgmFadeSeconds);
            yield return null;
        }
        bgm.Stop();
    }

    private void ShowConfirm(bool confirm)
    {
        actionGroup.SetActive(!confirm);
        confirmGroup.SetActive(confirm);
    }

    // 1回目なら2回目のチュートリアルへ。2回目（とエディタで直接Playしたとき）はおわりの画面（README 2.5）
    private void OnConfirmed()
    {
        if (ExperimentConfig.HasValue && ExperimentConfig.Round == 1)
        {
            ExperimentConfig.AdvanceToSecondRound();
            SceneManager.LoadScene(TitleMenuUI.TutorialSceneName);
            return;
        }

        messageText.text = FinishedText;
        actionGroup.SetActive(false);
        confirmGroup.SetActive(false);
    }
}
