using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuestState
{
    NotStarted,
    InProgress,
    Completed
}

// クエストの進行状態と取得済み手がかりを管理する（README 19.6）
// StartQuest / AddClue / Complete は実験ログ（Phase 8）のフックポイントにもなる
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public QuestData Quest { get; private set; }
    public QuestState State { get; private set; } = QuestState.NotStarted;

    public event Action OnChanged;

    private readonly HashSet<string> collectedClues = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void Start()
    {
        // ExperimentSettingsのAwake後に読むためStartで行う
        int questNumber = ExperimentSettings.Instance != null
            ? ExperimentSettings.Instance.questNumber
            : 1;

        Quest = Resources.Load<QuestData>("Quest/Q" + questNumber);

        if (Quest == null)
            Debug.LogError("QuestData not found: Quest/Q" + questNumber);
    }

    public void StartQuest()
    {
        if (State != QuestState.NotStarted)
            return;

        State = QuestState.InProgress;
        Debug.Log("[Quest] Start: " + Quest.title);

        OnChanged?.Invoke();
    }

    public void AddClue(string clueId)
    {
        if (State != QuestState.InProgress)
            return;

        if (Quest.GetClue(clueId) == null)
        {
            Debug.LogWarning("[Quest] Unknown clueId: " + clueId);
            return;
        }

        if (!collectedClues.Add(clueId))
            return;

        Debug.Log("[Quest] Clue: " + clueId);

        OnChanged?.Invoke();
    }

    public bool HasClue(string clueId)
    {
        return collectedClues.Contains(clueId);
    }

    public bool HasAllClues()
    {
        return Quest != null && Quest.clues.TrueForAll(c => collectedClues.Contains(c.clueId));
    }

    public void Complete()
    {
        if (State != QuestState.InProgress || !HasAllClues())
            return;

        State = QuestState.Completed;
        Debug.Log("[Quest] Complete: " + Quest.title);

        OnChanged?.Invoke();
    }
}
