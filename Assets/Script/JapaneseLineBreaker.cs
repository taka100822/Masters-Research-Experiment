using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// 会話の本文を文節で区切り、文節の途中で改行されないようにする（README 3.10）
// 区切りはBudouXの日本語モデル（Resources/BudouX/ja.json、Apache 2.0）を使う
public static class JapaneseLineBreaker
{
    private const string ModelPath = "BudouX/ja";
    private const char ZeroWidthSpace = '​';
    private const string SentenceEnders = "。！？!?";
    private const string ClosingBrackets = "」』）)";

    private static Dictionary<string, Dictionary<string, int>> model;
    private static double baseScore;
    private static bool triedLoad;

    // 文節ごとに<nobr>で囲み、文節の間に幅ゼロの空白（改行してよい位置）を入れた文を返す
    // TMPのタグ（<...>）と改行はそのまま残す。モデルが読めなければ元の文を返す
    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text) || !EnsureLoaded())
            return text;

        var result = new StringBuilder(text.Length * 3);
        var plain = new StringBuilder();

        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];

            if (c == '<')
            {
                int end = text.IndexOf('>', i);
                if (end > i)
                {
                    AppendPhrases(result, plain);
                    result.Append(text, i, end - i + 1);
                    i = end + 1;
                    continue;
                }
            }

            if (c == '\n' || c == '\r')
            {
                AppendPhrases(result, plain);
                result.Append(c);
                i++;
                continue;
            }

            plain.Append(c);
            i++;
        }
        AppendPhrases(result, plain);

        return result.ToString();
    }

    // 上のFormatに加えて、targetに並べてmaxLines行以内に収まるときだけ文の終わり（。！？）の後で改行する
    // 候補は前から順に試し、収まらない候補は飛ばす。行数を数えられないときは改行を足さない
    public static string Format(string text, TMP_Text target, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || target == null || maxLines <= 0)
            return Format(text);

        if (target.GetTextInfo(Format(text)).lineCount == 0)
            return Format(text);

        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];

            // TMPのタグの中は候補にしない
            if (c == '<')
            {
                int end = text.IndexOf('>', i);
                if (end > i)
                {
                    i = end + 1;
                    continue;
                }
            }

            if (SentenceEnders.IndexOf(c) < 0)
            {
                i++;
                continue;
            }

            int breakAt = i;
            while (breakAt < text.Length && SentenceEnders.IndexOf(text[breakAt]) >= 0) breakAt++;
            while (breakAt < text.Length && ClosingBrackets.IndexOf(text[breakAt]) >= 0) breakAt++;

            // 本文の最後と、すでに改行があるところは候補にしない
            if (breakAt >= text.Length)
                break;
            if (text[breakAt] == '\n' || text[breakAt] == '\r')
            {
                i = breakAt;
                continue;
            }

            string candidate = text.Insert(breakAt, "\n");
            if (target.GetTextInfo(Format(candidate)).lineCount <= maxLines)
            {
                text = candidate;
                breakAt++;
            }
            i = breakAt;
        }

        return Format(text);
    }

    private static void AppendPhrases(StringBuilder result, StringBuilder plain)
    {
        if (plain.Length == 0)
            return;

        List<string> phrases = Parse(plain.ToString());
        for (int i = 0; i < phrases.Count; i++)
        {
            if (i > 0)
                result.Append(ZeroWidthSpace);

            if (phrases[i].Length > 1)
                result.Append("<nobr>").Append(phrases[i]).Append("</nobr>");
            else
                result.Append(phrases[i]);
        }

        plain.Clear();
    }

    // BudouXのparserと同じ計算（各位置の前後1〜3文字の特徴の点数を足し、正なら区切る）
    private static List<string> Parse(string s)
    {
        var phrases = new List<string>();
        var current = new StringBuilder();
        current.Append(s[0]);

        for (int i = 1; i < s.Length; i++)
        {
            // サロゲートペアの途中では区切らない
            if (char.IsLowSurrogate(s[i]))
            {
                current.Append(s[i]);
                continue;
            }

            double score = baseScore;
            if (i > 2) score += Score("UW1", s, i - 3, 1);
            if (i > 1) score += Score("UW2", s, i - 2, 1);
            score += Score("UW3", s, i - 1, 1);
            score += Score("UW4", s, i, 1);
            if (i + 1 < s.Length) score += Score("UW5", s, i + 1, 1);
            if (i + 2 < s.Length) score += Score("UW6", s, i + 2, 1);
            if (i > 1) score += Score("BW1", s, i - 2, 2);
            score += Score("BW2", s, i - 1, 2);
            if (i + 1 < s.Length) score += Score("BW3", s, i, 2);
            if (i > 2) score += Score("TW1", s, i - 3, 3);
            if (i > 1) score += Score("TW2", s, i - 2, 3);
            if (i + 1 < s.Length) score += Score("TW3", s, i - 1, 3);
            if (i + 2 < s.Length) score += Score("TW4", s, i, 3);

            if (score > 0)
            {
                phrases.Add(current.ToString());
                current.Clear();
            }
            current.Append(s[i]);
        }
        phrases.Add(current.ToString());

        return phrases;
    }

    private static int Score(string feature, string s, int start, int length)
    {
        return model.TryGetValue(feature, out var table)
            && table.TryGetValue(s.Substring(start, length), out int value)
            ? value
            : 0;
    }

    private static bool EnsureLoaded()
    {
        if (triedLoad)
            return model != null;
        triedLoad = true;

        TextAsset json = Resources.Load<TextAsset>(ModelPath);
        if (json == null)
        {
            Debug.LogWarning("BudouX model not found: Resources/" + ModelPath);
            return false;
        }

        model = ParseModel(json.text);
        if (model == null)
        {
            Debug.LogWarning("BudouX model could not be parsed: Resources/" + ModelPath);
            return false;
        }

        // BudouXと同じく、全特徴の点数の合計の半分を引いた値から始める
        long total = 0;
        foreach (var table in model.Values)
            foreach (int value in table.Values)
                total += value;
        baseScore = -total * 0.5;

        return true;
    }

    // モデルの形 {"UW1":{"文字":点数,...},...} だけを読む小さなJSONパーサ
    // （JsonUtilityは辞書を読めないため）
    private static Dictionary<string, Dictionary<string, int>> ParseModel(string json)
    {
        var result = new Dictionary<string, Dictionary<string, int>>();
        int pos = 0;

        if (!Expect(json, ref pos, '{')) return null;
        while (true)
        {
            SkipSpaces(json, ref pos);
            if (pos < json.Length && json[pos] == '}') return result;

            string feature = ReadString(json, ref pos);
            if (feature == null || !Expect(json, ref pos, ':') || !Expect(json, ref pos, '{')) return null;

            var table = new Dictionary<string, int>();
            while (true)
            {
                SkipSpaces(json, ref pos);
                if (pos < json.Length && json[pos] == '}') { pos++; break; }

                string key = ReadString(json, ref pos);
                if (key == null || !Expect(json, ref pos, ':')) return null;

                SkipSpaces(json, ref pos);
                int start = pos;
                while (pos < json.Length && (json[pos] == '-' || char.IsDigit(json[pos]))) pos++;
                if (!int.TryParse(json.Substring(start, pos - start), out int value)) return null;
                table[key] = value;

                SkipSpaces(json, ref pos);
                if (pos < json.Length && json[pos] == ',') pos++;
            }
            result[feature] = table;

            SkipSpaces(json, ref pos);
            if (pos < json.Length && json[pos] == ',') pos++;
        }
    }

    private static string ReadString(string json, ref int pos)
    {
        if (!Expect(json, ref pos, '"')) return null;

        var sb = new StringBuilder();
        while (pos < json.Length)
        {
            char c = json[pos++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }

            if (pos >= json.Length) return null;
            char e = json[pos++];
            switch (e)
            {
                case 'u':
                    if (pos + 4 > json.Length) return null;
                    sb.Append((char)System.Convert.ToInt32(json.Substring(pos, 4), 16));
                    pos += 4;
                    break;
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                default: sb.Append(e); break;
            }
        }
        return null;
    }

    private static bool Expect(string json, ref int pos, char c)
    {
        SkipSpaces(json, ref pos);
        if (pos >= json.Length || json[pos] != c) return false;
        pos++;
        return true;
    }

    private static void SkipSpaces(string json, ref int pos)
    {
        while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
    }
}
