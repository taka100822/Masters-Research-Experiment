using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine.Networking;

// スプレッドシートをCSVで読む（PromptSheetImporter・DialogueSheetImporterで共通。README 4.7）
// シートは「リンクを知っている全員が閲覧可」にしておく（ログインなしでCSVを読むため）
public static class SheetCsv
{
    private const int TimeoutSeconds = 20;

    public static string ExportUrl(string sheetId, string gid)
    {
        return "https://docs.google.com/spreadsheets/d/" + sheetId + "/export?format=csv&gid=" + gid;
    }

    // titleはプログレスバーの見出し
    public static bool Download(string sheetId, string gid, string title, out string text, out string error)
    {
        text = null;
        error = null;
        using var req = UnityWebRequest.Get(ExportUrl(sheetId, gid));
        req.timeout = TimeoutSeconds;
        var op = req.SendWebRequest();
        try
        {
            while (!op.isDone)
                EditorUtility.DisplayProgressBar(title, "スプレッドシートを読んでいます…", req.downloadProgress);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            error = req.error;
            return false;
        }

        // 閲覧できないときはCSVではなくログイン画面のHTMLが返る
        string type = req.GetResponseHeader("Content-Type") ?? "";
        if (!type.StartsWith("text/csv"))
        {
            error = "CSVではない返事でした（" + type + "）";
            return false;
        }

        text = Encoding.UTF8.GetString(req.downloadHandler.data);
        return true;
    }

    // RFC 4180 のCSV（"で囲んだセルの中の改行・カンマ・""を扱う）
    public static List<List<string>> Parse(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char ch = csv[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                }
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else cell.Append(ch);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
