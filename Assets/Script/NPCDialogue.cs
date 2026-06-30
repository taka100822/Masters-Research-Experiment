using UnityEngine;

public class NPCDialogue : MonoBehaviour
{
    public string npcId;
    public bool allowUserInput;

    [Header("CSVファイル名")]
    public string csvFileName;

    public PromptData promptData;
}