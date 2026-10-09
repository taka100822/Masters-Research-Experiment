using UnityEngine;
using UnityEngine.Serialization;

public enum DialogueCondition
{
    A_ChoiceOnly,
    B_ChoiceAndInput
}

// 実験者がPlay前にInspectorで設定する、プレイ全体の実験設定
public class ExperimentSettings : MonoBehaviour
{
    public static ExperimentSettings Instance;

    [Header("参加者")]
    public string participantId = "P000";

    [Header("対話条件（A: 選択肢のみ / B: 選択肢＋自由入力）")]
    public DialogueCondition condition = DialogueCondition.A_ChoiceOnly;

    [Header("話題セット（1 / 2）（README 11.3）")]
    [Range(1, 2)]
    [FormerlySerializedAs("questNumber")]
    public int topicSet = 1;

    public bool AllowFreeInput => condition == DialogueCondition.B_ChoiceAndInput;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        // タイトル画面から来た場合はそこで設定した値を使う（直接MainSceneをPlayしたときはInspectorの値のまま）
        if (ExperimentConfig.HasValue)
        {
            participantId = ExperimentConfig.ParticipantId;
            condition = ExperimentConfig.Condition;
            topicSet = ExperimentConfig.TopicSet;
        }

        UnityEngine.Debug.Log("[Experiment] participant=" + participantId + " condition=" + condition + " set=" + topicSet);
    }
}
