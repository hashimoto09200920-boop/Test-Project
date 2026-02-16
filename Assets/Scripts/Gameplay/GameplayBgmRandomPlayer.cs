using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class GameplayBgmRandomPlayer : MonoBehaviour
{
    public enum PlayMode
    {
        LoopOne,        // (a) 選ばれた1曲をループ
        ShuffleOnEnd    // (b) 曲が終わったら次をランダム再生
    }

    [Header("BGM Clips")]
    [Tooltip("ランダム再生するBGMクリップ一覧（Assets/Audio/BGM から設定）")]
    [SerializeField] private AudioClip[] bgmClips;

    [Header("Mode")]
    [SerializeField] private PlayMode playMode = PlayMode.ShuffleOnEnd;

    [Tooltip("ShuffleOnEnd のとき、直前と同じ曲の連続を避ける")]
    [SerializeField] private bool avoidSameConsecutive = true;

    [Header("Audio Settings")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Tooltip("シーン開始時に自動再生する")]
    [SerializeField] private bool playOnStart = true;

    private AudioSource audioSource;
    private int lastIndex = -1;

    /// <summary>
    /// BGM音量（SoundSettingsManagerから変更可能）
    /// </summary>
    public float Volume
    {
        get => volume;
        set
        {
            volume = Mathf.Clamp01(value);
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }
        }
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // Unity 6: AudioSource設定（2D想定）
        audioSource.playOnAwake = false;
        audioSource.loop = false;             // モード側で制御
        audioSource.spatialBlend = 0f;        // 2D
        audioSource.volume = volume;
    }

    private void Start()
    {
        if (!playOnStart) return;

        PlayRandom();
    }

    private void Update()
    {
        if (audioSource == null) return;

        // 音量をInspector変更した場合にも追従
        audioSource.volume = volume;

        if (playMode != PlayMode.ShuffleOnEnd) return;

        // 再生終了を検知して次へ（Pause中などもあるので安全側でチェック）
        if (!audioSource.isPlaying && audioSource.clip != null)
        {
            PlayRandom();
        }
    }

    public void PlayRandom()
    {
        if (audioSource == null) return;
        if (bgmClips == null || bgmClips.Length == 0) return;

        int index = PickIndex();
        AudioClip clip = bgmClips[index];
        if (clip == null) return;

        audioSource.clip = clip;

        if (playMode == PlayMode.LoopOne)
        {
            audioSource.loop = true;
            audioSource.Play();
        }
        else
        {
            audioSource.loop = false;
            audioSource.Play();
        }
    }

    private int PickIndex()
    {
        int count = bgmClips.Length;
        if (count <= 1) return 0;

        int idx = Random.Range(0, count);

        if (!avoidSameConsecutive) return idx;

        // 直前と同じを避ける（最大数回トライして、最後は妥協）
        if (idx == lastIndex)
        {
            int tries = 0;
            while (idx == lastIndex && tries < 8)
            {
                idx = Random.Range(0, count);
                tries++;
            }
        }

        lastIndex = idx;
        return idx;
    }

    // Inspectorでモードを変えた時に安全に反映したい場合の手動用
    public void Stop()
    {
        if (audioSource == null) return;
        audioSource.Stop();
        audioSource.clip = null;
    }
}
