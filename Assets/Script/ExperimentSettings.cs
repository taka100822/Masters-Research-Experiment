using UnityEngine;

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

    [Header("プレイするクエスト（1: 消えた薬草 / 2: 祭りの道具探し）")]
    [Range(1, 2)]
    public int questNumber = 1;

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
        }
    }
}
