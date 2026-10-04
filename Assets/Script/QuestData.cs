using System.Collections.Generic;
using UnityEngine;

// クエスト1件分の静的データ（README 19章）
[CreateAssetMenu(fileName = "QuestData", menuName = "Quest/QuestData")]
public class QuestData : ScriptableObject
{
    [System.Serializable]
    public class Clue
    {
        [Tooltip("CSVのaction列 clue:<clueId> で指定するID（place / time / person / feature）")]
        public string clueId;

        [Tooltip("QuestPanelに出すカテゴリ名（場所・時間・人物・特徴）")]
        public string category;

        [Tooltip("手がかりの内容")]
        public string text;
    }

    public int questId;
    public string title;
    public List<Clue> clues = new();

    public Clue GetClue(string clueId)
    {
        return clues.Find(c => c.clueId == clueId);
    }
}
