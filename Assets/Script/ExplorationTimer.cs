using System;
using UnityEngine;
using TMPro;

// 探索の制限時間（README 5.1）
// 説明画面を閉じたとき（Begin）から数え始める。ゲーム内の時間で数えるので、設定パネル表示中（timeScale=0）は止まる
public class ExplorationTimer : MonoBehaviour
{
    public static ExplorationTimer Instance;

    [Tooltip("制限時間（秒）")]
    [SerializeField] private float durationSeconds = 300f;

    [Header("残り時間の表示（未設定でも動く）")]
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private TMP_Text timerText;

    public bool IsRunning { get; private set; }
    public bool IsTimeUp { get; private set; }

    // 時間切れの瞬間に1回だけ呼ばれる
    public event Action OnTimeUp;

    private float elapsed;
    private int shownSeconds = -1;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(this);
    }

    private void Start()
    {
        if (timerPanel != null)
            timerPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Begin()
    {
        if (IsRunning || IsTimeUp)
            return;

        IsRunning = true;
        elapsed = 0f;

        if (timerPanel != null)
            timerPanel.SetActive(true);
        UpdateText();

        ExperimentLogger.Log("explore_start", detail: "duration=" + durationSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void Update()
    {
        if (!IsRunning)
            return;

        elapsed += Time.deltaTime;
        UpdateText();

        if (elapsed >= durationSeconds)
        {
            IsRunning = false;
            IsTimeUp = true;
            OnTimeUp?.Invoke();
        }
    }

    private void UpdateText()
    {
        if (timerText == null)
            return;

        // 残り4:32.4なら「4:33」（0:00になった瞬間に時間切れ）
        int remaining = Mathf.Max(0, Mathf.CeilToInt(durationSeconds - elapsed));
        if (remaining == shownSeconds)
            return;

        shownSeconds = remaining;
        timerText.text = "残り " + (remaining / 60) + ":" + (remaining % 60).ToString("00");
    }
}
