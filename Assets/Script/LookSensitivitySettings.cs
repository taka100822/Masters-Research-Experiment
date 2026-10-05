using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 視点（カメラ回転）の感度。PlayerInputのLookアクションに実行中だけ倍率を掛ける（README 3.6）
// 購入アセット（ThirdPersonController・StarterAssets.inputactions）は変更しない
// 参加者間で条件をそろえるため保存はせず、起動するたびに初期値に戻す
public class LookSensitivitySettings : MonoBehaviour
{
    public static LookSensitivitySettings Instance;

    public const float MinSensitivity = 0.2f;
    public const float MaxSensitivity = 3f;

    [SerializeField, Range(MinSensitivity, MaxSensitivity)] private float defaultSensitivity = 1f;

    public float Sensitivity { get; private set; }

    public event Action OnChanged;

    private InputAction lookAction;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        Sensitivity = defaultSensitivity;
    }

    private void Start()
    {
        var playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null)
            lookAction = playerInput.actions.FindAction("Look");

        Apply();
    }

    public void SetSensitivity(float value)
    {
        Sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        Apply();
        OnChanged?.Invoke();
    }

    private void Apply()
    {
        if (lookAction == null)
            return;

        // 既存のプロセッサ（反転・デバイスごとの倍率）の後ろに感度の倍率を足す
        string scale = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "ScaleVector2(x={0},y={0})", Sensitivity);

        for (int i = 0; i < lookAction.bindings.Count; i++)
        {
            var binding = lookAction.bindings[i];
            if (binding.isComposite)
                continue;

            string processors = string.IsNullOrEmpty(binding.processors)
                ? scale
                : binding.processors + "," + scale;

            lookAction.ApplyBindingOverride(i, new InputBinding { overrideProcessors = processors });
        }
    }
}
