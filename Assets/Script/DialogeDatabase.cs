using System.Collections.Generic;
using UnityEngine;

public class DialogueDatabase : MonoBehaviour
{
    private Dictionary<string, List<string>> dialogues
        = new Dictionary<string, List<string>>();

    private void Awake()
    {
        LoadCSV();
    }

    private void LoadCSV()
    {
        TextAsset csv = Resources.Load<TextAsset>("dialogue");

        string[] lines = csv.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] columns = lines[i].Split(',');

            string npcName = columns[0].Trim();
            string text = columns[1].Trim();

            if (!dialogues.ContainsKey(npcName))
            {
                dialogues[npcName] = new List<string>();
            }

            dialogues[npcName].Add(text);
        }
    }

    public string[] GetDialogue(string npcName)
    {
        if (dialogues.ContainsKey(npcName))
        {
            return dialogues[npcName].ToArray();
        }

        return new string[0];
    }
}