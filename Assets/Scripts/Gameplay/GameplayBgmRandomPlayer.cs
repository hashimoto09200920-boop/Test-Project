using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class GameplayBgmRandomPlayer : MonoBehaviour
{
    public enum PlayMode
    {
        LoopOne,        // 選ばれた1曲をループ
        ShuffleOnEnd    // 曲が終わったら次をランダム再生
    }

    /// <summary>
    /// エリア番号とBGMクリップのペア
    /// </summary>
    [System.Serializable]
    public class AreaBgmEntry
    {
        [Tooltip("AreaConfig.areaNumber と合わせる（1〜9）")]
        public int areaNumber;
        [Tooltip("このエリアで使うBGMクリップ一覧（複数設定でランダム再生）")]
        public AudioClip[] clips;
    }

    [Header("エリア別 BGM")]
    [Tooltip("Area番号ごとにBGMクリップを設定する。BGM_Playerオブジェクトのこの欄で全エリアをまとめて管理できる。")]
    [SerializeField] private AreaBgmEntry[] areaBgmList;

    [Header("デフォルト BGM")]
    [Tooltip("AreaBgmListに対応エリアが見つからない場合のフォールバック")]
    [SerializeField] private AudioClip[] defaultBgmClips;

    [Header("エディタ用フォールバック")]
    [Tooltip("05_Gameを直接Playした時（AreaSelectを経由しない時）に使うエリア番号")]
    [SerializeField] private int editorFallbackAreaNumber = 1;

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
    private AudioClip[] activeClips;
    private int nextIndex = -1;

    public float Volume
    {
        get => volume;
        set
        {
            volume = Mathf.Clamp01(value);
            if (audioSource != null)
                audioSource.volume = volume;
        }
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
    }

    private void Start()
    {
        // エリア番号を決定（GameSessionがなければエディタ用フォールバック）
        int areaNum = editorFallbackAreaNumber;
        AreaConfig area = GameSession.SelectedArea;
        if (area != null)
            areaNum = area.areaNumber;

        // AreaBgmListからエリア番号に対応するクリップを検索
        activeClips = FindClipsForArea(areaNum);

        if (activeClips != null && activeClips.Length > 0)
            Debug.Log($"[GameplayBgmRandomPlayer] Area {areaNum} のBGM {activeClips.Length}曲を使用");
        else
            Debug.LogWarning($"[GameplayBgmRandomPlayer] Area {areaNum} のBGMが未設定。defaultBgmClipsを確認してください。");

        // 全クリップをプリロード（Streamingでない場合のスパイク対策）
        PreloadAllClips();

        if (!playOnStart) return;
        PlayRandom();
    }

    private void PreloadAllClips()
    {
        if (activeClips == null) return;
        foreach (var clip in activeClips)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
        }
    }

    private void Update()
    {
        if (audioSource == null) return;

        audioSource.volume = volume;

        if (playMode != PlayMode.ShuffleOnEnd) return;

        if (!audioSource.isPlaying && audioSource.clip != null)
            PlayRandom();
    }

    private AudioClip[] FindClipsForArea(int areaNum)
    {
        if (areaBgmList != null)
        {
            foreach (var entry in areaBgmList)
            {
                if (entry.areaNumber == areaNum && entry.clips != null && entry.clips.Length > 0)
                    return entry.clips;
            }
        }
        return defaultBgmClips;
    }

    public void PlayRandom()
    {
        if (audioSource == null) return;
        if (activeClips == null || activeClips.Length == 0) return;

        // 事前にピックしていた次曲インデックスがあれば使う
        int index = (nextIndex >= 0) ? nextIndex : PickIndex();
        nextIndex = -1;

        AudioClip clip = activeClips[index];
        if (clip == null) return;

        // ロードが完了していなければ待機してからセット
        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        audioSource.clip = clip;
        audioSource.loop = (playMode == PlayMode.LoopOne);
        audioSource.Play();

        Debug.Log($"[GameplayBgmRandomPlayer] 再生: {clip.name} (index={index})");

        // 次の曲を今のうちにピックしてプリロード
        if (playMode == PlayMode.ShuffleOnEnd && activeClips.Length > 1)
        {
            nextIndex = PickIndex();
            AudioClip next = activeClips[nextIndex];
            if (next != null && next.loadState == AudioDataLoadState.Unloaded)
                next.LoadAudioData();
        }
    }

    private int PickIndex()
    {
        int count = activeClips.Length;
        if (count <= 1) return 0;

        int idx = Random.Range(0, count);

        if (avoidSameConsecutive && idx == lastIndex)
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

    public void Stop()
    {
        if (audioSource == null) return;
        audioSource.Stop();
        audioSource.clip = null;
    }
}
