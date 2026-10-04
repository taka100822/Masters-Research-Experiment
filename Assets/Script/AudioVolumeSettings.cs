using System;
using UnityEngine;

public enum VolumeType
{
    BGM,
    SE
}

// BGM・SE音量を1か所で保持する（README 13章 設定パネル）
// 参加者間で条件をそろえるため保存はせず、起動するたびに初期値に戻す
public class AudioVolumeSettings : MonoBehaviour
{
    public static AudioVolumeSettings Instance;

    [SerializeField, Range(0f, 1f)] private float defaultBGMVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float defaultSEVolume = 0.8f;

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
