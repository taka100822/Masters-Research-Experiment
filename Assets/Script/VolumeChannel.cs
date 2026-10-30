using UnityEngine;

// AudioSourceに付けて、BGM／SEの設定音量を反映する（標準音量 × 設定音量）
// SEの標準音量はSEVolumeTable、BGM（と表に無いSE）は元のAudioSourceのVolume
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
        var settings = AudioVolumeSettings.Instance;
        if (settings == null)
        {
            source.volume = baseVolume;
            return;
        }

        // SEは表の標準音量を使う（README 7.4.1）。表に無い音はAudioSourceのVolumeのまま
        float standard = type == VolumeType.SE
            ? settings.GetSEBaseVolume(source.resource, baseVolume)
            : baseVolume;

        source.volume = standard * settings.GetVolume(type);
    }
}
