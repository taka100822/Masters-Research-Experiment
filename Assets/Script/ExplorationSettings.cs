using UnityEngine;

// 探索の制限時間と残り時間の合図（README 5.1）
// TutorialSceneの札も読むので、シーンではなくResourcesのファイルに置く（Assets/Resources/ExplorationSettings.asset）
[CreateAssetMenu(fileName = "ExplorationSettings", menuName = "Experiment/Exploration Settings")]
public class ExplorationSettings : ScriptableObject
{
    private const string ResourcePath = "ExplorationSettings";

    [Tooltip("制限時間（秒）")]
    [Min(1f)] public float durationSeconds = 180f;

    [Tooltip("残りがこの秒数以下になったら、文字の色を変えて窓を1回揺らす")]
    [Min(0f)] public float warningSeconds = 60f;

    private static ExplorationSettings current;

    // ファイルが無いときは初期値（180・60）で動く
    public static ExplorationSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<ExplorationSettings>(ResourcePath);
                if (current == null)
                {
                    Debug.LogWarning("Resources/" + ResourcePath + " が見つからないので、初期値の制限時間で動きます");
                    current = CreateInstance<ExplorationSettings>();
                }
            }
            return current;
        }
    }

    // 秒数を札の文にする（180→「3分」、270→「4分30秒」、30→「30秒」。README 2.4）
    public static string FormatDuration(float seconds)
    {
        int total = Mathf.RoundToInt(seconds);
        int minutes = total / 60;
        int rest = total % 60;
        if (minutes == 0)
            return rest + "秒";
        return rest == 0 ? minutes + "分" : minutes + "分" + rest + "秒";
    }
}
