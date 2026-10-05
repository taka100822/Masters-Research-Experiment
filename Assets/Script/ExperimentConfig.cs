// タイトル画面（開発者用モーダル）で設定した実験設定を、MainSceneへ渡すためのstaticな置き場（README 3.1）
// ファイルには保存しない。アプリを起動し直すと初期値に戻る
public static class ExperimentConfig
{
    // タイトル画面を通ったときだけtrue。falseならMainSceneのExperimentSettingsはInspectorの値を使う
    public static bool HasValue { get; private set; }

    public static string ParticipantId { get; private set; } = "P000";
    public static DialogueCondition Condition { get; private set; } = DialogueCondition.A_ChoiceOnly;
    public static int QuestNumber { get; private set; } = 1;

    public static void Set(string participantId, DialogueCondition condition, int questNumber)
    {
        ParticipantId = participantId;
        Condition = condition;
        QuestNumber = questNumber;
        HasValue = true;
    }
}
