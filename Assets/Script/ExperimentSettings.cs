using UnityEngine;
using UnityEngine.Serialization;

public enum DialogueCondition
{
    A_ChoiceOnly,
    B_FreeInputOnly
}

// 実験者がPlay前にInspectorで設定する、プレイ全体の実験設定
public class ExperimentSettings : MonoBehaviour
{
    public static ExperimentSettings Instance;

    [Header("参加者")]
    public string participantId = "P000";

    [Header("対話条件（A: 選択肢のみ / B: 自由入力のみ）")]
    public DialogueCondition condition = DialogueCondition.A_ChoiceOnly;

    [Header("話題セット（1 / 2）（README 1.3）")]
    [Range(1, 2)]
    [FormerlySerializedAs("questNumber")]
    public int topicSet = 1;

    // 条件Bでは選択肢を出さず、自由入力だけで話す（README 1.8）
    public bool FreeInputOnly => condition == DialogueCondition.B_FreeInputOnly;

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
