using UnityEngine;
using TMPro;

// 画面右上のクエスト進行チェックリスト（README 3.5）
public class QuestPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject questPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text[] clueTexts;

    private void Start()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnChanged += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        var quest = QuestManager.Instance;

        // 受注前は表示しない
        bool visible = quest != null && quest.Quest != null && quest.State != QuestState.NotStarted;
        questPanel.SetActive(visible);

        if (!visible)
            return;

        titleText.text = quest.Quest.title;

        for (int i = 0; i < clueTexts.Length; i++)
        {
            if (i >= quest.Quest.clues.Count)
            {
                clueTexts[i].text = "";
                continue;
            }

            var clue = quest.Quest.clues[i];

            // 未入手の手がかりは内容を伏せ、カテゴリ名だけ出す
            clueTexts[i].text = quest.HasClue(clue.clueId)
                ? "■ " + clue.category + "：" + clue.text
                : "□ " + clue.category;
        }
    }
}
