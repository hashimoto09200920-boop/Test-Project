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

    [Tooltip("サイクル境界（前サイクル最後 → 次サイクル先頭）で同じ曲が連続するのを避ける")]
    [SerializeField] private bool avoidSameConsecutive = true;

    [Header("Audio Settings")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Tooltip("シーン開始時に自動再生する")]
    [SerializeField] private bool playOnStart = true;

    private AudioSource audioSource;
    private int lastIndex = -1;
    private AudioClip[] activeClips;
    private int nextIndex = -1;
    private int[] shuffleQueue;
    private int queuePos;

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

        // ★SoundSettingsManagerの反映(OnSceneLoaded)を待たずとも、既にInstanceが存在するなら
        //   ここで直接取得しておく（プル）。Inspector上のテスト用初期値がそのまま使われる事故を防ぐ。
        if (SoundSettingsManager.Instance != null)
            volume = SoundSettingsManager.Instance.BGMVolume;
        audioSource.volume = volume;
    }

    private void Start()
    {
        // ★チュートリアル中はオープニング/エリアセレクトから続いているBGMをそのまま鳴らし続けたいため、
        // 通常のエリア別ゲームプレイBGMは再生しない
        if (GameSession.IsInTutorial) return;

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

        // ★SoundSettingsManager.ApplyBGMVolume()によるプッシュ反映がシーン遷移タイミングによっては
        //   間に合わないことがあるため、実際に再生を始める直前に現在の設定値を直接取得し直す（プル）。
        //   これによりInspector上の初期値（テスト用の低い値等）で再生されてしまう事故を防ぐ。
        if (SoundSettingsManager.Instance != null)
            Volume = SoundSettingsManager.Instance.BGMVolume;

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

    private void InitShuffleQueue(int avoidFirst = -1)
    {
        int count = activeClips.Length;
        shuffleQueue = new int[count];
        for (int i = 0; i < count; i++) shuffleQueue[i] = i;

        // Fisher-Yates shuffle
        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = shuffleQueue[i];
            shuffleQueue[i] = shuffleQueue[j];
            shuffleQueue[j] = tmp;
        }

        // サイクル境界で前サイクル末尾と同じ曲が先頭にならないよう入れ替え
        if (avoidSameConsecutive && avoidFirst >= 0 && count > 1 && shuffleQueue[0] == avoidFirst)
        {
            int swapWith = Random.Range(1, count);
            int tmp = shuffleQueue[0];
            shuffleQueue[0] = shuffleQueue[swapWith];
            shuffleQueue[swapWith] = tmp;
        }

        queuePos = 0;
    }

    private int PickIndex()
    {
        int count = activeClips.Length;
        if (count <= 1) return 0;

        if (shuffleQueue == null || queuePos >= shuffleQueue.Length)
            InitShuffleQueue(lastIndex);

        int idx = shuffleQueue[queuePos++];
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
