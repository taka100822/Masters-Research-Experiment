using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using StarterAssets;

// 時間切れのあとのアンケート画面と、2回目のあとのおわりの画面（README 3.7）
// 終了画面（QuestCompletePanel）に付ける。DialogueManager.EndGameが表示したときに中身を整える
public class EndScreenUI : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;

    [Header("アンケート回答完了")]
    [SerializeField] private GameObject actionGroup;
    [SerializeField] private Button completeButton;

    [Header("アンケートを開く（README 3.7）。未設定なら何もしない")]
    [SerializeField] private Button openFormButton;

    // Googleフォーム「ゲーム体験アンケート」の事前入力URL。フォームの質問を作り直したら欄の番号も直す（README 3.7）
    private const string FormUrl = "https://docs.google.com/forms/d/e/1FAIpQLSch5ArOd7LTy5Y08wKCqhAbPFtd0PiVjQHsY3Fz0iQ8rceuVQ/viewform?usp=pp_url";
    private const string IdEntry = "entry.1776098365";     // プレイヤーID
    private const string RoundEntry = "entry.1533040520";  // 何回目のプレイですか？（1回目／2回目）

    [Header("確認（間違って押してもすぐ進まないように）")]
    [SerializeField] private GameObject confirmGroup;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("BGM（アンケート画面を出したらフェードアウトして止める。README 3.7）")]
    [SerializeField] private AudioSource bgm;
    [SerializeField] private float bgmFadeSeconds = 1.5f;

    private const string QuestionnaireText =
        "時間になりました\n\n<size=24>お疲れさまでした。\n「アンケートを開く」を押して、アンケートに答えてください。\n答え終わったら、この画面に戻って「アンケート回答完了」を押してください。</size>";
    private const string FinishedText =
        "以上で終わりです\n\n<size=24>ご協力ありがとうございました。\n実験者の指示があるまでお待ちください。</size>";

    private void Awake()
    {
        completeButton.onClick.AddListener(() => ShowConfirm(true));
        noButton.onClick.AddListener(() => ShowConfirm(false));
        yesButton.onClick.AddListener(OnConfirmed);
        if (openFormButton != null)
            openFormButton.onClick.AddListener(OpenForm);
    }

    // プレイヤーIDと回を入れた状態でフォームを開く。条件・話題セットは入れない（参加者の目に入るため）
    // 押したことはログに書かない（本番のログは時間切れで閉じているため。README 2.5）
    private void OpenForm()
    {
        string id = ExperimentSettings.Instance != null ? ExperimentSettings.Instance.participantId : ExperimentConfig.ParticipantId;
        int round = ExperimentConfig.HasValue ? ExperimentConfig.Round : 1;

        OpenInChrome(FormUrl +
            "&" + IdEntry + "=" + System.Uri.EscapeDataString(id ?? "") +
            "&" + RoundEntry + "=" + System.Uri.EscapeDataString(round + "回目"));
    }

    // Chromeで開く。入っていない・開けないときは既定のブラウザで開く（README 3.7）
    private static void OpenInChrome(string url)
    {
        try
        {
            // UseShellExecuteなら、chrome.exeの場所はWindowsが登録情報（App Paths）から探す
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("chrome.exe", "\"" + url + "\"")
            {
                UseShellExecute = true,
            });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Chromeで開けなかったので既定のブラウザで開きます: " + e.Message);
            Application.OpenURL(url);
        }
    }

    private void OnEnable()
    {
        // ボタンを押せるようにカーソルを出す
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // フォームから戻ってフォーカスが戻ったとき、Starter Assetsがカーソルを固定し直さないようにする
        var inputs = FindAnyObjectByType<StarterAssetsInputs>();
        if (inputs != null)
            inputs.cursorLocked = false;

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
