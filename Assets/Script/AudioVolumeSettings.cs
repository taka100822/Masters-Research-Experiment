using System;
using UnityEngine;
using UnityEngine.Audio;

public enum VolumeType
{
    BGM,
    SE
}

// BGM・SE音量を1か所で保持する（README 3.8）
// 参加者間で条件をそろえるため保存はせず、起動するたびに初期値に戻す
public class AudioVolumeSettings : MonoBehaviour
{
    public static AudioVolumeSettings Instance;

    [SerializeField, Range(0f, 1f)] private float defaultBGMVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float defaultSEVolume = 0.8f;
    [SerializeField] private SEVolumeTable seVolumeTable;

    public float BGMVolume { get; private set; }
    public float SEVolume { get; private set; }

    public event Action OnChanged;

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

        BGMVolume = defaultBGMVolume;
        SEVolume = defaultSEVolume;

        if (seVolumeTable != null)
            seVolumeTable.OnChanged += NotifyChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this && seVolumeTable != null)
            seVolumeTable.OnChanged -= NotifyChanged;
    }

    // SEの標準音量（README 7.4.1）。表に無い音はfallback（AudioSourceのVolume）を使う
    public float GetSEBaseVolume(AudioResource sound, float fallback)
    {
        return seVolumeTable != null ? seVolumeTable.GetVolume(sound, fallback) : fallback;
    }

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }

    public float GetVolume(VolumeType type)
    {
        return type == VolumeType.BGM ? BGMVolume : SEVolume;
    }

    public void SetVolume(VolumeType type, float volume)
    {
        volume = Mathf.Clamp01(volume);

        if (type == VolumeType.BGM)
            BGMVolume = volume;
        else
            SEVolume = volume;

        OnChanged?.Invoke();
    }
}
