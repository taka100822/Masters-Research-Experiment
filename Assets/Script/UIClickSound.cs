using UnityEngine;
using UnityEngine.UI;

// ボタンのクリック音（README 7.4.2）
// 起動時にシーン内のButton（非表示も含む）を集めて、onClickにクリック音を足す
// 「はじめる」などはすぐ次のシーンを読み込むので、DontDestroyOnLoadのAudioSourceで鳴らして途切れないようにする
public class UIClickSound : MonoBehaviour
{
    [SerializeField] private AudioClip clip;

    // 3シーンで共有する
    private static AudioSource sharedSource;

    private void Start()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
            button.onClick.AddListener(Play);
    }

    private void Play()
    {
        if (clip == null)
            return;

        if (sharedSource == null)
        {
            var go = new GameObject("UIClickSoundSource");
            DontDestroyOnLoad(go);
            sharedSource = go.AddComponent<AudioSource>();
            sharedSource.playOnAwake = false;
            sharedSource.spatialBlend = 0f;
        }

        // VolumeChannelと同じ「標準音量（SEVolumeTable）× SEの設定音量」を、鳴らす瞬間に求める
        var settings = AudioVolumeSettings.Instance;
        float volume = settings != null
            ? settings.GetSEBaseVolume(clip, 1f) * settings.GetVolume(VolumeType.SE)
            : 1f;

        sharedSource.PlayOneShot(clip, volume);
    }
}
