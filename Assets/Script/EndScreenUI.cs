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

    private const string QuestionnaireText =
        "時間になりました\n\n<size=24>お疲れさまでした。アンケートに回答してください。\n回答が終わったら、下のボタンを押してください。</size>";
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
