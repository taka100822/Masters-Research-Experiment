using UnityEngine;

public class NPCDialogue : MonoBehaviour
{
    [Tooltip("ログ・CSV用の固定ID（NPC001など）。話題セットが変わっても同じ（位置の番号。README 11.6）")]
    public string npcId;

    [Tooltip("会話ウィンドウに表示する名前（話題セット1）。空ならnpcIdを表示")]
    public string displayName;

    [Header("話題セット2（README 11.6）")]
    [Tooltip("話題セット2のときの名前。空ならdisplayNameのまま")]
    public string set2DisplayName;

    [Tooltip("話題セットごとの衣装（[0]=セット1、[1]=セット2）。使うセットの衣装だけを表示する")]
    public GameObject[] costumesBySet;

    public string DisplayName
    {
        get
        {
            string name = TopicSet == 2 && !string.IsNullOrEmpty(set2DisplayName) ? set2DisplayName : displayName;
            return string.IsNullOrEmpty(name) ? npcId : name;
        }
    }

    [Header("CSVファイル名")]
    public string csvFileName;

    [Tooltip("セット別のプロンプトが無いときに使う予備（README 4.5）")]
    public PromptData promptData;

    private static int TopicSet => ExperimentSettings.Instance != null ? ExperimentSettings.Instance.topicSet : 1;

    // ExperimentSettingsはAwakeで値を読むので、衣装はStartで切り替える
    private void Start()
    {
        if (costumesBySet == null || costumesBySet.Length == 0)
            return;

        int index = Mathf.Clamp(TopicSet - 1, 0, costumesBySet.Length - 1);
        for (int i = 0; i < costumesBySet.Length; i++)
            if (costumesBySet[i] != null)
                costumesBySet[i].SetActive(i == index);
    }
}
