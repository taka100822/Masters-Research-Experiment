using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// スプレッドシート「NPCプロンプト」からPromptDataアセットへ取り込む（README 4.7）
// メニュー「Tools/研究用」から実行する。ゲームの実行中はシートを読まない
public static class PromptSheetImporter
{
    private const string MenuPath = "Tools/研究用/プロンプトをスプレッドシートから取り込む";
    private const string Title = "プロンプトの取り込み";

    // シートは「リンクを知っている全員が閲覧可」にしておく（ログインなしでCSVを読むため）
    private const string SheetId = "1DpBEnJ_7bN44l1gGB1NgiLCCUcDbHpGMiggjoKVDg7M";
    private const string SheetGid = "0";   // タブ「prompts」

    private const string PromptRoot = "Assets/Resources/PromptData";
    private static readonly string[] Folders = { "S1", "S2", "Tutorial" };

    private class Change
    {
        public string path;        // 例：S1/NPC003
        public string prompt;
        public PromptData asset;   // nullなら新規
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
        var changes = new List<Change>();
        var seen = new HashSet<string>();
        int same = 0, skipped = 0;

        // 1行目は見出し
        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            string folder = r.Count > 0 ? r[0].Trim() : "";
            string npcId = r.Count > 1 ? r[1].Trim() : "";
            if (folder == "" && npcId == "")
                continue;

            int line = i + 1;
            if (System.Array.IndexOf(Folders, folder) < 0 || npcId == "")
            {
                Debug.LogWarning("[PromptSheet] " + line + "行目：folder「" + folder + "」・npcId「" + npcId + "」は取り込めません（folderはS1／S2／Tutorial）");
                skipped++;
                continue;
            }

            string path = folder + "/" + npcId;
            if (!seen.Add(path))
            {
                Debug.LogWarning("[PromptSheet] " + line + "行目：" + path + " が重複しています。最初の行だけ使います");
                skipped++;
                continue;
            }

            // 改行と前後の空白をそろえる（見た目が同じならハッシュも同じになるように）
            string prompt = (r.Count > 3 ? r[3] : "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            if (prompt == "")
            {
                Debug.LogWarning("[PromptSheet] " + line + "行目：" + path + " のsystemPromptが空です");
                skipped++;
                continue;
            }

            var asset = AssetDatabase.LoadAssetAtPath<PromptData>(PromptRoot + "/" + path + ".asset");
            if (asset != null && asset.systemPrompt == prompt)
            {
                same++;
                continue;
            }

            changes.Add(new Change { path = path, prompt = prompt, asset = asset });
        }

        if (changes.Count == 0)
        {
            Debug.Log("[PromptSheet] 変更 0・新規 0・同じ " + same + (skipped > 0 ? "・飛ばした " + skipped : ""));
            EditorUtility.DisplayDialog(Title, "変わったプロンプトはありません（同じ " + same + "）。", "OK");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("次のプロンプトを書き換えます。");
        sb.AppendLine();
        foreach (var c in changes)
        {
            sb.AppendLine(c.asset == null
                ? c.path + "（新規） " + ExperimentLogger.Hash8(c.prompt)
                : c.path + " " + ExperimentLogger.Hash8(c.asset.systemPrompt) + " → " + ExperimentLogger.Hash8(c.prompt));
        }
        sb.AppendLine();
        sb.Append("実験期間中はプロンプトを変えないでください（README 1.5）。");
        if (!EditorUtility.DisplayDialog(Title, sb.ToString(), "取り込む", "やめる"))
            return;

        int updated = 0, created = 0;
        foreach (var c in changes)
        {
            if (c.asset != null)
            {
                Undo.RecordObject(c.asset, Title);
                c.asset.systemPrompt = c.prompt;
                EditorUtility.SetDirty(c.asset);
                updated++;
            }
            else
            {
                string dir = PromptRoot + "/" + c.path.Substring(0, c.path.IndexOf('/'));
                Directory.CreateDirectory(dir);
                var asset = ScriptableObject.CreateInstance<PromptData>();
                asset.systemPrompt = c.prompt;
                AssetDatabase.CreateAsset(asset, PromptRoot + "/" + c.path + ".asset");
                created++;
            }
            Debug.Log("[PromptSheet] " + c.path + " hash=" + ExperimentLogger.Hash8(c.prompt));
        }
        AssetDatabase.SaveAssets();

        Debug.Log("[PromptSheet] 変更 " + updated + "・新規 " + created + "・同じ " + same + (skipped > 0 ? "・飛ばした " + skipped : ""));
    }
}
