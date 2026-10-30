using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// SEごとの標準音量の表（README 7.4.1）
// 足音はAudioRandomContainerで鳴っているので、キーはAudioClipではなくAudioResourceにする
[CreateAssetMenu(fileName = "SEVolumeTable", menuName = "Audio/SE Volume Table")]
public class SEVolumeTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AudioResource sound;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    // Playモード中に表を変えたとき、鳴っている音にすぐ反映するため
    public event Action OnChanged;

    // 表に無い音はfallback（AudioSourceのVolume）を返す
    public float GetVolume(AudioResource sound, float fallback)
    {
        if (sound == null)
            return fallback;

        foreach (var entry in entries)
        {
            if (entry.sound == sound)
                return entry.volume;
        }

        return fallback;
    }

    private void OnValidate()
    {
        OnChanged?.Invoke();
    }
}
