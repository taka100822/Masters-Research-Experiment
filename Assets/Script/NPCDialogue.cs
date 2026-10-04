using UnityEngine;

public class NPCDialogue : MonoBehaviour
{
    [Tooltip("ログ・CSV用の固定ID（NPC001など）")]
    public string npcId;

    [Tooltip("会話ウィンドウに表示する名前。空ならnpcIdを表示")]
    public string displayName;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? npcId : displayName;

    public bool allowUserInput;

    [Header("CSVファイル名")]
    public string csvFileName;

    public PromptData promptData;
}