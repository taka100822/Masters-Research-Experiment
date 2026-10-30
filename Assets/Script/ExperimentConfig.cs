// タイトル画面（開発者用モーダル）で設定した実験設定を、各シーンへ渡すためのstaticな置き場（README 3.1）
// 実験者が入れるのは参加者IDと始める回だけ。条件・話題セットはIDの番号のグループから決める（README 1.3・2.5）
// ファイルには保存しない。アプリを起動し直すと初期値に戻る
public static class ExperimentConfig
{
    // タイトル画面を通ったときだけtrue。falseならMainSceneのExperimentSettingsはInspectorの値を使う
    public static bool HasValue { get; private set; }

    // 初期値は空（P000のまま始めてしまわないように）
    public static string ParticipantId { get; private set; } = "";

    // 今が何回目のプレイか（1／2）
    public static int Round { get; private set; } = 1;

    public static int Group => GroupOf(ParticipantId);
    public static DialogueCondition Condition => ConditionOf(Group, Round);
    public static int TopicSet => TopicSetOf(Group, Round);

    public static void Set(string participantId, int round)
    {
        ParticipantId = participantId;
        Round = round;
        HasValue = true;
    }

    // 1回目のアンケートが終わったら2回目へ（README 2.5）
    public static void AdvanceToSecondRound()
    {
        Round = 2;
    }

    // グループ＝(番号 − 1) を4で割った余り ＋ 1。番号はIDの末尾の数字（"P006"→6→グループ2）。番号が無ければ0
    public static int GroupOf(string participantId)
    {
        int number = TrailingNumber(participantId);
        return number > 0 ? (number - 1) % 4 + 1 : 0;
    }

    // グループ1：A・S1→B・S2、2：A・S2→B・S1、3：B・S1→A・S2、4：B・S2→A・S1（README 1.3）
    public static DialogueCondition ConditionOf(int group, int round)
    {
        bool firstIsB = group >= 3;
        bool isB = round == 2 ? !firstIsB : firstIsB;
        return isB ? DialogueCondition.B_FreeInputOnly : DialogueCondition.A_ChoiceOnly;
    }

    public static int TopicSetOf(int group, int round)
    {
        int firstSet = group % 2 == 1 ? 1 : 2;
        return round == 2 ? 3 - firstSet : firstSet;
    }

    private static int TrailingNumber(string id)
    {
        if (string.IsNullOrEmpty(id))
            return 0;

        int start = id.Length;
        while (start > 0 && char.IsDigit(id[start - 1]))
            start--;

        return int.TryParse(id.Substring(start), out int n) ? n : 0;
    }
}
