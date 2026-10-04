using UnityEngine;

// AudioSourceに付けて、BGM／SEの設定音量を反映する（元の音量 × 設定音量）
[RequireComponent(typeof(AudioSource))]
public class VolumeChannel : MonoBehaviour
{
    [SerializeField] private VolumeType type = VolumeType.SE;

    private AudioSource source;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    private void Start()
    {
        if (AudioVolumeSettings.Instance != null)
            AudioVolumeSettings.Instance.OnChanged += Apply;

        Apply();
    }

    private void OnDestroy()
    {
        if (AudioVolumeSettings.Instance != null)
            AudioVolumeSettings.Instance.OnChanged -= Apply;
    }

    private void Apply()
    {
        float volume = AudioVolumeSettings.Instance != null
            ? AudioVolumeSettings.Instance.GetVolume(type)
            : 1f;

        source.volume = baseVolume * volume;
    }
}
