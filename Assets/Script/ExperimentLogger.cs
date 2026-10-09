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
        "session_id,participant_id,condition,set,timestamp,play_time,event_type,npc_id,node_id,text,player_input,choice_index,clue_id,duration,is_error,detail";

    private static readonly string[] NpcIds = { "NPC001", "NPC002", "NPC003", "NPC004", "NPC005", "NPC006" };

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
    private int topicSet;
    private string startTime;

    // 集計（README 6.7）
    private int dialogueCount, choiceCount, freeInputCount, aiErrorCount, settingsOpenCount, respawnCount;
    private float? exploreStartTime, timeUpTime;
    private float aiWaitSeconds;
    private readonly HashSet<string> topicsHeard = new();   // "NPC001:1" の形。同じ話題は1つと数える
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
        topicSet = settings != null ? settings.topicSet : 0;

        var now = DateTime.Now;
        startTime = now.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
        string stamp = now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        sessionId = stamp + "_" + participantId;

        try
        {
            Directory.CreateDirectory(LogDirectory);
            string fileName = SafeFileName(participantId) + "_" + condition + "_S" + topicSet + "_" + stamp + ".csv";
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
            topicSet.ToString(CultureInfo.InvariantCulture),
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
                aiWaitSeconds += ReadLatencySeconds(detail);
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

            case "explore_start":
                exploreStartTime = PlayTime;
                break;

            case "topic_heard":
                npcId ??= dialogueNpc;
                topicsHeard.Add(npcId + ":" + ReadValue(detail, "topic"));
                detail = Append(detail, "topics_heard=" + topicsHeard.Count + ";npcs_talked=" + talkedNpcs.Count);
                break;

            case "time_up":
                timeUpTime = PlayTime;
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
            "session_id", "participant_id", "condition", "set", "start_time", "end_reason", "total_play_time",
            "explore_start_time", "time_up_time",
            "dialogue_count", "choice_count", "free_input_count", "ai_error_count", "ai_wait_total",
            "npcs_talked", "topics_heard", "settings_open_count", "respawn_count"
        };
        foreach (var id in NpcIds) header.Add("talk_" + id);

        var row = new List<string>
        {
            sessionId, participantId, condition, topicSet.ToString(CultureInfo.InvariantCulture), startTime, reason,
            F(PlayTime), F(exploreStartTime), F(timeUpTime),
            dialogueCount.ToString(), choiceCount.ToString(), freeInputCount.ToString(), aiErrorCount.ToString(), F(aiWaitSeconds),
            talkedNpcs.Count.ToString(), topicsHeard.Count.ToString(),
            settingsOpenCount.ToString(), respawnCount.ToString()
        };
        foreach (var id in NpcIds)
            row.Add(talkCounts.TryGetValue(id, out int n)
                ? n + "/" + talkSeconds[id].ToString("0.0", CultureInfo.InvariantCulture)
                : "0/0.0");

        try
        {
            string path = Path.Combine(LogDirectory, "summary.csv");
            string headerLine = ToCsvLine(header.ToArray());

            // 列の違う古いsummary.csv（クエスト版など）があれば、別名で残して新しく作り直す
            if (File.Exists(path))
            {
                string firstLine;
                using (var sr = new StreamReader(path, Encoding.UTF8))
                    firstLine = sr.ReadLine();
                if (firstLine != headerLine)
                {
                    string old = Path.Combine(LogDirectory, "summary_old_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
                    File.Move(path, old);
                    Debug.Log("[Log] summary.csv had different columns. Moved to " + old);
                }
            }

            bool isNew = !File.Exists(path);
            using var sw = new StreamWriter(path, true, new UTF8Encoding(true));
            if (isNew)
                sw.WriteLine(headerLine);
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

    // detailの「key=value;…」からvalueを取り出す。無ければnull
    private static string ReadValue(string detail, string key)
    {
        if (string.IsNullOrEmpty(detail))
            return null;
        foreach (var part in detail.Split(';'))
            if (part.StartsWith(key + "="))
                return part.Substring(key.Length + 1);
        return null;
    }

    // ai_responseのlatency_ms（DialogueManagerが書く）を秒にする。プロンプトが無いときなどは0
    private static float ReadLatencySeconds(string detail)
    {
        return long.TryParse(ReadValue(detail, "latency_ms"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms)
            ? ms / 1000f
            : 0f;
    }

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
