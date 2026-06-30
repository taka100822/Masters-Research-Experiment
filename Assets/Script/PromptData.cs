using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Prompt Data")]
public class PromptData : ScriptableObject
{
    [TextArea(3, 10)]
    public string systemPrompt;
}