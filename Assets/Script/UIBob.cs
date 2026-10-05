using UnityEngine;

// UIを上下にゆっくり揺らす。会話送りの▼に使う（README 3.7）
[RequireComponent(typeof(RectTransform))]
public class UIBob : MonoBehaviour
{
    [SerializeField] private float amplitude = 4f;
    [SerializeField] private float speed = 3f;

    private RectTransform rect;
    private Vector2 basePosition;

    private void Awake()
    {
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;
    }

    private void OnEnable()
    {
        if (rect != null)
            rect.anchoredPosition = basePosition;
    }

    private void Update()
    {
        rect.anchoredPosition = basePosition + Vector2.up * (Mathf.Sin(Time.unscaledTime * speed) * amplitude);
    }
}
