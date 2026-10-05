using UnityEngine;

// シングルトン。ChatGPTClientなどMainScene全体で使うコンポーネントを持つGameObjectの目印
// （ゲームの状態はDialogueManagerが管理する。README 4.1）
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
