using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// 実験ログ（README 6章）
// 1セッション（MainSceneの開始〜終了）＝1ファイル。1行1イベントを書くたびにファイルへ書き出す
// セッションの終わりに summary.csv へ集計を1行追記する
public class ExperimentLogger : MonoBehaviour
{
    public static ExperimentLogger Instance;

    private const string EventHeader =
        "session_id,participant_id,condition,quest,timestamp,play_time,event_type,npc_id,node_id,text,player_input,choice_index,clue_id,duration,is_error,detail";

    private static readonly string[] NpcIds = { "NPC001", "NPC002", "NPC003", "NPC004", "NPC005", "NPC006" };
    private static readonly string[] ClueIds = { "place", "time", "person", "feature" };

    // ビルドでは.exeの隣、エディタではAssetsの中（README 6.2）
    public static string LogDirectory =>
        Application.isEditor
            ? Path.Combine(Application.dataPath, "ExperimentLogs")
            : Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ExperimentLogs");

    private StreamWriter writer;
    private bool sessionEnded;

    private string sessionId;
    private string participantId;
    private string condition;
    private int quest;
    private string startTime;

    // 集計（README 6.7）
    private int dialogueCount, choiceCount, freeInputCount, aiErrorCount, settingsOpenCount, respawnCount;
    private float? questStartTime, questCompleteTime;
    private readonly Dictionary<string, float> clueTimes = new();
    private readonly Dictionary<string, int> talkCounts = new();
    private readonly Dictionary<string, float> talkSeconds = new();
    private readonly HashSet<string> talkedNpcs = new();

    // 会話中・設定パネル表示中の情報
    private string dialogueNpc;
    private float dialogueStart;
    private int dialogueChoices, dialogueInputs;
    private float? settingsOpenedAt;

    private static float PlayTime => Time.timeSinceLevelLoad;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(this);
    }

    private void Start()
    {
        EnsureSession();
    }

    private void OnApplicationQuit()
    {
        EndSession("app_quit");
    }

    private void OnDestroy()
    {
        // シーンの切り替えなど、終わり方を記録せずに消えるときもファイルは閉じる
        EndSession("scene_unloaded");
        if (Instance == this)
            Instance = null;
    }

    // =========================
    // 外から呼ぶAPI
    // =========================

    public static void Log(
        string eventType,
        string npcId = null,
        int? nodeId = null,
        string text = null,
        string playerInput = null,
        int? choiceIndex = null,
        string clueId = null,
        float? duration = null,
        bool isError = false,
        string detail = null)
    {
        if (Instance == null)
            return;

        Instance.Write(eventType, npcId, nodeId, text, playerInput, choiceIndex, clueId, duration, isError, detail);
    }

    public static void EndSessionNow(string reason)
    {
        if (Instance != null)
            Instance.EndSession(reason);
    }

    public static string Hash8(string s)
    {
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
        var sb = new StringBuilder();
        for (int i = 0; i < 4; i++)
            sb.Append(bytes[i].ToString("x2"));
        return sb.ToString();
    }

    // =========================
    // セッション
    // =========================

    private void EnsureSession()
    {
        if (writer != null || sessionEnded)
            return;

        var settings = ExperimentSettings.Instance;
        participantId = settings != null ? settings.participantId : "unknown";
        condition = settings != null && settings.condition == DialogueCondition.B_ChoiceAndInput ? "B" : "A";
        quest = settings != null ? settings.questNumber : 0;

        var now = DateTime.Now;
        startTime = now.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
        string stamp = now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        sessionId = stamp + "_" + participantId;

        try
        {
            Directory.CreateDirectory(LogDirectory);
            string fileName = SafeFileName(participantId) + "_" + condition + "_Q" + quest + "_" + stamp + ".csv";
            string path = Path.Combine(LogDirectory, fileName);

            writer = new StreamWriter(path, false, new UTF8Encoding(true)) { AutoFlush = true };
            writer.WriteLine(EventHeader);

            Debug.Log("[Log] Session log: " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("[Log] Could not open log file: " + e.Message);
            writer = null;
            return;
        }

        Write("session_start", detail:
            "app=" + Application.version +
            ";unity=" + Application.unityVersion +
            ";model=" + ChatGPTClient.ModelName +
            ";platform=" + Application.platform);
    }

    private void EndSession(string reason)
    {
        if (sessionEnded || writer == null)
            return;

        // 開いたままの設定パネル・会話は閉じたことにしてから終わる
        if (settingsOpenedAt.HasValue)
            Write("settings_close");
        if (dialogueNpc != null)
            Write("dialogue_end", dialogueNpc);

        Write("session_end", duration: PlayTime, detail: "reason=" + reason);

        writer.Dispose();
        writer = null;
        sessionEnded = true;

        AppendSummary(reason);
    }

    // =========================
    // 1行書く
    // =========================

    private void Write(
        string eventType,
        string npcId = null,
        int? nodeId = null,
        string text = null,
        string playerInput = null,
        int? choiceIndex = null,
        string clueId = null,
        float? duration = null,
        bool isError = false,
        string detail = null)
    {
        EnsureSession();
        if (writer == null)
            return;

        UpdateStats(eventType, ref npcId, ref duration, ref detail, isError, clueId);

        var cols = new[]
        {
            sessionId,
            participantId,
            condition,
            quest.ToString(CultureInfo.InvariantCulture),
            DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
            PlayTime.ToString("0.000", CultureInfo.InvariantCulture),
            eventType,
            npcId,
            nodeId?.ToString(CultureInfo.InvariantCulture),
            text,
            playerInput,
            choiceIndex?.ToString(CultureInfo.InvariantCulture),
            clueId,
            duration?.ToString("0.000", CultureInfo.InvariantCulture),
            isError ? "1" : "",
            detail
        };

        try
        {
            writer.WriteLine(ToCsvLine(cols));
        }
        catch (Exception e)
        {
            Debug.LogError("[Log] Write failed: " + e.Message);
        }
    }

    // イベントごとの集計と、会話時間など他のイベントから計算する値の補完
    private void UpdateStats(string eventType, ref string npcId, ref float? duration, ref string detail, bool isError, string clueId)
    {
        switch (eventType)
        {
            case "dialogue_start":
                dialogueNpc = npcId;
                dialogueStart = PlayTime;
                dialogueChoices = 0;
                dialogueInputs = 0;
                dialogueCount++;
                if (npcId != null)
                    talkedNpcs.Add(npcId);
                break;

            case "choice_select":
                choiceCount++;
                dialogueChoices++;
                break;

            case "free_input_submit":
                freeInputCount++;
                dialogueInputs++;
                break;

            case "ai_response":
                if (isError)
                    aiErrorCount++;
                break;

            case "dialogue_end":
                if (dialogueNpc == null)
                    break;
                npcId = dialogueNpc;
                float talk = PlayTime - dialogueStart;
                duration = talk;
                detail = Append(detail, "choices=" + dialogueChoices + ";inputs=" + dialogueInputs);
                talkCounts[npcId] = (talkCounts.TryGetValue(npcId, out int c) ? c : 0) + 1;
                talkSeconds[npcId] = (talkSeconds.TryGetValue(npcId, out float s) ? s : 0f) + talk;
                dialogueNpc = null;
                break;

            case "quest_start":
                questStartTime = PlayTime;
                break;

            case "clue_get":
                if (clueId != null && !clueTimes.ContainsKey(clueId))
                    clueTimes[clueId] = PlayTime;
                npcId ??= dialogueNpc;
                detail = Append(detail, "clues_collected=" + clueTimes.Count + ";npcs_talked=" + talkedNpcs.Count);
                break;

            case "quest_complete":
                questCompleteTime = PlayTime;
                if (questStartTime.HasValue)
                    duration = PlayTime - questStartTime.Value;
                break;

            case "settings_open":
                settingsOpenCount++;
                settingsOpenedAt = PlayTime;
                break;

            case "settings_close":
                // 設定パネル表示中は時間が止まるので、開いていた時間は実時間で測る
                if (settingsOpenedAt.HasValue)
                    duration = Time.unscaledTime - settingsOpenedRealtime;
                settingsOpenedAt = null;
                detail = Append(detail, CurrentSettingsDetail());
                break;

            case "respawn":
                respawnCount++;
                break;
        }

        if (eventType == "settings_open")
            settingsOpenedRealtime = Time.unscaledTime;
    }

    private float settingsOpenedRealtime;

    private static string CurrentSettingsDetail()
    {
        var volume = AudioVolumeSettings.Instance;
        var look = LookSensitivitySettings.Instance;
        return string.Format(CultureInfo.InvariantCulture, "bgm={0:0.00};se={1:0.00};sensitivity={2:0.0}",
            volume != null ? volume.BGMVolume : 0f,
            volume != null ? volume.SEVolume : 0f,
            look != null ? look.Sensitivity : 0f);
    }

    // =========================
    // summary.csv
    // =========================

    private void AppendSummary(string reason)
    {
        var header = new List<string>
        {
            "session_id", "participant_id", "condition", "quest", "start_time", "end_reason", "total_play_time",
            "quest_start_time", "quest_complete_time", "quest_duration",
            "dialogue_count", "choice_count", "free_input_count", "ai_error_count", "settings_open_count", "respawn_count"
        };
        foreach (var id in ClueIds) header.Add("clue_" + id + "_time");
        foreach (var id in NpcIds) header.Add("talk_" + id);

        var row = new List<string>
        {
            sessionId, participantId, condition, quest.ToString(CultureInfo.InvariantCulture), startTime, reason,
            F(PlayTime), F(questStartTime), F(questCompleteTime),
            questStartTime.HasValue && questCompleteTime.HasValue ? F(questCompleteTime.Value - questStartTime.Value) : "",
            dialogueCount.ToString(), choiceCount.ToString(), freeInputCount.ToString(), aiErrorCount.ToString(),
            settingsOpenCount.ToString(), respawnCount.ToString()
        };
        foreach (var id in ClueIds) row.Add(clueTimes.TryGetValue(id, out float t) ? F(t) : "");
        foreach (var id in NpcIds)
            row.Add(talkCounts.TryGetValue(id, out int n)
                ? n + "/" + talkSeconds[id].ToString("0.0", CultureInfo.InvariantCulture)
                : "0/0.0");

        try
        {
            string path = Path.Combine(LogDirectory, "summary.csv");
            bool isNew = !File.Exists(path);
            using var sw = new StreamWriter(path, true, new UTF8Encoding(true));
            if (isNew)
                sw.WriteLine(ToCsvLine(header.ToArray()));
            sw.WriteLine(ToCsvLine(row.ToArray()));
        }
        catch (Exception e)
        {
            Debug.LogError("[Log] Could not write summary.csv: " + e.Message);
        }
    }

    // =========================
    // ユーティリティ
    // =========================

    private static string F(float? v) =>
        v.HasValue ? v.Value.ToString("0.000", CultureInfo.InvariantCulture) : "";

    private static string Append(string detail, string more) =>
        string.IsNullOrEmpty(detail) ? more : detail + ";" + more;

    private static string ToCsvLine(string[] cols)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cols.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Escape(cols[i]));
        }
        return sb.ToString();
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string SafeFileName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
