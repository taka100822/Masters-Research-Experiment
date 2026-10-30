using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// スプレッドシート「NPC会話テキスト」から会話データのCSVへ取り込む（README 4.8）
// メニュー「Tools/研究用」から実行する。ゲームの実行中はシートを読まない（ゲームは今までどおりCSVを読む）
public static class DialogueSheetImporter
{
    private const string MenuPath = "Tools/研究用/会話テキストをスプレッドシートから取り込む";
    private const string Title = "会話テキストの取り込み";

    // シートは「リンクを知っている全員が閲覧可」にしておく（ログインなしでCSVを読むため）
    private const string SheetId = "1UrnokKpW8CRvu7GmQE8EDhN05eMvUk8BzUUhgON8yIs";
    private const string SheetGid = "0";   // タブ「dialogue」

    private const string DialogueRoot = "Assets/Resources/dialogue";
    private static readonly string[] Folders = { "S1", "S2", "Tutorial" };
    private static readonly string[] Actions = { "", "topic:1", "topic:2" };

    // DialogueDatabase.LoadCSVが読む形式（README 4.4）。allowInputは使っていないので常に0
    private const string Header = "id,text,nextId,choiceA,choiceA_next,choiceB,choiceB_next,allowInput,action";

    private class Npc
    {
        public string path;                         // 例：S1/NPC003
        public readonly List<string[]> nodes = new();   // シートのD〜K列（id, text, nextId, choiceA, choiceA_next, choiceB, choiceB_next, action）
        public readonly List<int> lines = new();    // シートの行番号（警告用）
    }

    private class Change
    {
        public string path;
        public string csv;
        public string detail;   // 例：変更：20・21、新規
    }

    [MenuItem(MenuPath)]
    public static void Import()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(Title, "Playを止めてから実行してください。", "OK");
            return;
        }

        if (!SheetCsv.Download(SheetId, SheetGid, Title, out string csv, out string error))
        {
            EditorUtility.DisplayDialog(Title, "シートを読めませんでした。\n" + error + "\n\nシートの共有が「リンクを知っている全員が閲覧可」になっているか確認してください。", "OK");
            return;
        }

        var rows = SheetCsv.Parse(csv);
        var npcs = new List<Npc>();
        var byPath = new Dictionary<string, Npc>();
        int skipped = 0;

        // 1行目は見出し。同じ人の行はシートの順にまとめる
        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            string folder = Cell(r, 0);
            string npcId = Cell(r, 1);
            if (folder == "" && npcId == "")
                continue;

            int line = i + 1;
            if (System.Array.IndexOf(Folders, folder) < 0 || npcId == "")
            {
                Debug.LogWarning("[DialogueSheet] " + line + "行目：folder「" + folder + "」・npcId「" + npcId + "」は取り込めません（folderはS1／S2／Tutorial）");
                skipped++;
                continue;
            }

            string path = folder + "/" + npcId;
            if (!byPath.TryGetValue(path, out Npc npc))
            {
                npc = new Npc { path = path };
                byPath[path] = npc;
                npcs.Add(npc);
            }
            npc.nodes.Add(new[] { Cell(r, 3), Cell(r, 4), Cell(r, 5), Cell(r, 6), Cell(r, 7), Cell(r, 8), Cell(r, 9), Cell(r, 10) });
            npc.lines.Add(line);
        }

        var changes = new List<Change>();
        int same = 0;
        foreach (var npc in npcs)
        {
            if (!Validate(npc))
            {
                skipped++;
                continue;
            }

            string newCsv = BuildCsv(npc);
            string file = DialogueRoot + "/" + npc.path + ".csv";
            string oldCsv = File.Exists(file) ? File.ReadAllText(file).Replace("\r\n", "\n") : null;
            if (oldCsv == newCsv)
            {
                same++;
                continue;
            }

            changes.Add(new Change { path = npc.path, csv = newCsv, detail = oldCsv == null ? "新規" : "変更：" + ChangedIds(oldCsv, newCsv) });
        }

        if (changes.Count == 0)
        {
            Debug.Log("[DialogueSheet] 変更 0・新規 0・同じ " + same + (skipped > 0 ? "・飛ばした " + skipped : ""));
            EditorUtility.DisplayDialog(Title, "変わった会話はありません（同じ " + same + (skipped > 0 ? "・飛ばした " + skipped + "。理由はConsole" : "") + "）。", "OK");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("次の会話を書き換えます。");
        sb.AppendLine();
        foreach (var c in changes)
            sb.AppendLine(c.path + " " + c.detail);
        if (skipped > 0)
        {
            sb.AppendLine();
            sb.AppendLine("取り込めない行・人が " + skipped + " あります（理由はConsole）。");
        }
        sb.AppendLine();
        sb.Append("実験期間中は会話を変えないでください（README 1.5）。");
        if (!EditorUtility.DisplayDialog(Title, sb.ToString(), "取り込む", "やめる"))
            return;

        int updated = 0, created = 0;
        foreach (var c in changes)
        {
            string file = DialogueRoot + "/" + c.path + ".csv";
            if (File.Exists(file)) updated++;
            else created++;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, c.csv, new UTF8Encoding(false));   // 今のCSVと同じ：BOMなし・LF
            Debug.Log("[DialogueSheet] " + c.path + " " + c.detail);
        }
        AssetDatabase.Refresh();

        Debug.Log("[DialogueSheet] 変更 " + updated + "・新規 " + created + "・同じ " + same + (skipped > 0 ? "・飛ばした " + skipped : ""));
    }

    private static string Cell(List<string> r, int i)
    {
        return r.Count > i ? r[i].Trim() : "";
    }

    // 1つでもおかしいノードがあれば、その人は取り込まない（README 4.8）
    private static bool Validate(Npc npc)
    {
        var ids = new HashSet<int>();
        bool ok = true;

        void Warn(int k, string message)
        {
            Debug.LogWarning("[DialogueSheet] " + npc.lines[k] + "行目（" + npc.path + "）：" + message + "。この人は取り込みません");
            ok = false;
        }

        for (int k = 0; k < npc.nodes.Count; k++)
        {
            if (!int.TryParse(npc.nodes[k][0], out int id))
                Warn(k, "id「" + npc.nodes[k][0] + "」が整数ではありません");
            else if (!ids.Add(id))
                Warn(k, "id " + id + " が重複しています");
        }
        if (!ok)
            return false;
        if (!ids.Contains(0))
        {
            Debug.LogWarning("[DialogueSheet] " + npc.path + "：ノード0がありません。この人は取り込みません");
            return false;
        }

        for (int k = 0; k < npc.nodes.Count; k++)
        {
            var n = npc.nodes[k];
            if (n[1] == "")
                Warn(k, "textが空です");

            // DialogueDatabaseは1行ずつ・カンマで区切って読むので、セルの中の改行とカンマは使えない
            for (int c = 1; c < n.Length; c++)
            {
                if (n[c].Contains(","))
                    Warn(k, "半角カンマ「,」があります（読点「、」を使ってください）");
                if (n[c].Contains("\n") || n[c].Contains("\r"))
                    Warn(k, "セルの中で改行しています");
            }

            CheckNext(n[2], "nextId", k, ids, Warn);
            CheckChoice(n[3], n[4], "choiceA", k, ids, Warn);
            CheckChoice(n[5], n[6], "choiceB", k, ids, Warn);

            if (System.Array.IndexOf(Actions, n[7]) < 0)
                Warn(k, "action「" + n[7] + "」は使えません（空・topic:1・topic:2）");
        }
        return ok;
    }

    private static void CheckChoice(string text, string next, string name, int k, HashSet<int> ids, System.Action<int, string> warn)
    {
        if ((text == "") != (next == ""))
            warn(k, name + "の文と遷移先（" + name + "_next）は両方書くか、両方空にしてください");
        else
            CheckNext(next, name + "_next", k, ids, warn);
    }

    // 空・-1・その人にあるノードのどれか
    private static void CheckNext(string value, string name, int k, HashSet<int> ids, System.Action<int, string> warn)
    {
        if (value == "" || value == "-1")
            return;
        if (!int.TryParse(value, out int next))
            warn(k, name + "「" + value + "」が整数ではありません");
        else if (!ids.Contains(next))
            warn(k, name + " " + next + " のノードがありません");
    }

    private static string BuildCsv(Npc npc)
    {
        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');
        foreach (var n in npc.nodes)
            sb.Append(string.Join(",", n[0], n[1], n[2], n[3], n[4], n[5], n[6], "0", n[7])).Append('\n');
        return sb.ToString();
    }

    // 行が違うノードのid（足された・消されたものも含む）。例：20・21
    private static string ChangedIds(string oldCsv, string newCsv)
    {
        var oldRows = RowsById(oldCsv);
        var newRows = RowsById(newCsv);
        var changed = new List<string>();
        foreach (var kv in newRows)
            if (!oldRows.TryGetValue(kv.Key, out string row) || row != kv.Value)
                changed.Add(kv.Key);
        foreach (var key in oldRows.Keys)
            if (!newRows.ContainsKey(key))
                changed.Add(key + "（削除）");
        return changed.Count > 0 ? string.Join("・", changed) : "並び順";
    }

    private static Dictionary<string, string> RowsById(string csv)
    {
        var rows = new Dictionary<string, string>();
        var lines = csv.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line == "") continue;
            rows[line.Split(',')[0]] = line;
        }
        return rows;
    }
}
