using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class DialogueDatabase : MonoBehaviour
{
    [System.Serializable]
    public class Node
    {
        public int id;
        public string text;
        public int nextId;

        public string choiceA;
        public int choiceA_next;

        public string choiceB;
        public int choiceB_next;

        public int allowInput;

        // startQuest / clue:<clueId> / complete / 空（README 19.6.3）
        public string action;
    }

    private Dictionary<int, Node> nodes = new();

    private void Awake()
    {
        LoadCSV("Dialogue/NPC001");

        Debug.Log("Node Count = " + nodes.Count);
    }

    public bool LoadCSV(string path)
    {
        TextAsset csv = Resources.Load<TextAsset>(path);

        if (csv == null)
            return false;

        nodes.Clear();

        string[] lines = csv.text.Split('\n');

        for (int i = 1; i < lines.Length; i++) // 1行目はヘッダー
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string line = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] values = line.Split(',');


            Node node = new Node
            {
                id = int.Parse(values[0]),
                text = values[1],

                nextId =
                    int.TryParse(values[2].Trim(), out int next)
                    ? next
                    : -1,

                choiceA = values[3],

                choiceA_next =
                    int.TryParse(values[4].Trim(), out int aNext)
                    ? aNext
                    : -1,

                choiceB = values[5],

                choiceB_next =
                    int.TryParse(values[6].Trim(), out int bNext)
                    ? bNext
                    : -1,

                allowInput =
                    (values.Length > 7 && int.TryParse(values[7].Trim(), out int ai))
                    ? ai
                    : 0,

                action = values.Length > 8 ? values[8].Trim() : ""
            };
            nodes[node.id] = node;
        }

        return true;
    }

    public bool HasNode(int id)
    {
        return nodes.ContainsKey(id);
    }

    public Node GetNode(int id)
    {
        return nodes.ContainsKey(id) ? nodes[id] : null;
    }
}