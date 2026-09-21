using System.Collections;
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
    private bool isFadingVolume = false;

    // ★Area10ボスラッシュ専用：各エリアにつき3曲全部ではなく、ランダムに選んだ1曲だけを
    //   先読み・再生することで負荷を1/3に減らすためのキャッシュ（エリア番号→選ばれた曲）。
    private System.Collections.Generic.Dictionary<int, AudioClip> pickedClipForArea = new System.Collections.Generic.Dictionary<int, AudioClip>();

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

        // フェード中はFadeOutAndSwitchToAreaルーチン側がvolumeを制御するので上書きしない
        if (!isFadingVolume) audioSource.volume = volume;

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

    /// <summary>
    /// 現在の曲をフェードアウトしてから、指定エリア番号のBGMリストに切り替えて再生する（Area10ボスラッシュ専用）。
    /// 既存のノーリピートシャッフル状態はリセットされる。
    /// </summary>
    public void FadeOutAndSwitchToArea(int areaNumber, float fadeOutDuration, System.Action onSwitched = null)
    {
        StartCoroutine(FadeOutAndSwitchRoutine(areaNumber, fadeOutDuration, onSwitched));
    }

    /// <summary>
    /// 指定エリア番号のBGMリストに切り替えるだけで、再生はしない（Area10ボスラッシュのStage1最初のボス専用）。
    /// StageIntroController側の既存のPlayRandom()呼び出し（スポットライト点灯後）が正しい曲リストで
    /// 再生できるようにするための準備のみ行う。
    /// </summary>
    public void SwitchToAreaWithoutPlaying(int areaNumber)
    {
        activeClips = FindClipsForArea(areaNumber);
        shuffleQueue = null;
        nextIndex = -1;
        PreloadAllClips();
    }

    /// <summary>
    /// 指定エリア番号のBGMを読み込み開始するだけで、activeClipsの切替も再生も一切行わない
    /// （Area10ボスラッシュ専用：9体分のBGMを開始直後にまとめて先読みしておくため）。
    /// フェードアウトの数秒だけでは長い曲のデコードが間に合わずPlay()時にフリーズすることがあったため、
    /// Area10開始時点（Stage1〜3を通してプレイする長い時間）から先読みを始めておく。
    /// </summary>
    public void PreloadClipsForArea(int areaNumber)
    {
        AudioClip[] clips = FindClipsForArea(areaNumber);
        if (clips == null) return;
        foreach (var clip in clips)
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
    }

    /// <summary>
    /// Area10ボスラッシュ専用：指定エリアの3曲からランダムに1曲だけを選んで先読みする。
    /// 3曲全部を読み込むPreloadClipsForArea()より負荷が1/3で済む（このボスが倒されるまでは
    /// 選ばれたこの1曲だけを流し続けるため、他の2曲は最初から不要）。
    /// 一度選んだ曲はpickedClipForAreaに記憶し、同じエリアに対して呼び直しても曲が変わらないようにする。
    /// </summary>
    public void PreloadPickedClipForArea(int areaNumber)
    {
        if (pickedClipForArea.ContainsKey(areaNumber)) return;
        AudioClip[] clips = FindClipsForArea(areaNumber);
        if (clips == null || clips.Length == 0) return;
        AudioClip picked = clips[Random.Range(0, clips.Length)];
        pickedClipForArea[areaNumber] = picked;
        if (picked != null && picked.loadState == AudioDataLoadState.Unloaded)
            picked.LoadAudioData();
    }

    private AudioClip GetOrPickClipForArea(int areaNumber)
    {
        if (!pickedClipForArea.TryGetValue(areaNumber, out AudioClip clip) || clip == null)
        {
            PreloadPickedClipForArea(areaNumber);
            pickedClipForArea.TryGetValue(areaNumber, out clip);
        }
        return clip;
    }

    /// <summary>
    /// PreloadPickedClipForArea()で選んだ1曲に切り替えるだけで、再生はしない
    /// （Area10ボスラッシュのStage1最初のボス専用。SwitchToAreaWithoutPlaying()の1曲版）。
    /// </summary>
    public void SwitchToPickedClipWithoutPlaying(int areaNumber)
    {
        AudioClip clip = GetOrPickClipForArea(areaNumber);
        activeClips = clip != null ? new AudioClip[] { clip } : null;
        shuffleQueue = null;
        nextIndex = -1;
    }

    /// <summary>
    /// FadeOutAndSwitchToArea()の1曲版（Area10ボスラッシュ専用）。
    /// PreloadPickedClipForArea()で選んだ1曲へフェード切替し、そのボスが倒されるまでループ再生する
    /// （activeClipsが1曲だけになるため、既存のShuffleOnEnd時の「曲が終わったら次を再生」ロジックが
    /// 自然に同じ曲を再生し続ける＝実質ループになる。既存コードの変更不要）。
    /// </summary>
    public void FadeOutAndSwitchToPickedClip(int areaNumber, float fadeOutDuration, System.Action onSwitched = null)
    {
        StartCoroutine(FadeOutAndSwitchToPickedClipRoutine(areaNumber, fadeOutDuration, onSwitched));
    }

    private IEnumerator FadeOutAndSwitchToPickedClipRoutine(int areaNumber, float fadeOutDuration, System.Action onSwitched)
    {
        if (audioSource == null) yield break;

        AudioClip upcoming = GetOrPickClipForArea(areaNumber);
        if (upcoming != null && upcoming.loadState == AudioDataLoadState.Unloaded)
            upcoming.LoadAudioData();

        if (fadeOutDuration > 0f && audioSource.isPlaying)
        {
            isFadingVolume = true;
            float startVolume = audioSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            isFadingVolume = false;
        }

        AudioClip[] singleClipArray = upcoming != null ? new AudioClip[] { upcoming } : null;
        yield return StartCoroutine(WaitForClipsLoaded(singleClipArray, 2f));

        audioSource.Stop();
        audioSource.clip = null;

        activeClips = singleClipArray;
        shuffleQueue = null;
        nextIndex = -1;

        if (activeClips != null && activeClips.Length > 0)
            PlayRandom();

        onSwitched?.Invoke();
    }

    private IEnumerator FadeOutAndSwitchRoutine(int areaNumber, float fadeOutDuration, System.Action onSwitched)
    {
        if (audioSource == null) yield break;

        // ★次のAreaのBGMを先読みしておく（LoadAudioData()は重いデコード処理のため、
        //   切替の瞬間にまとめて呼ぶと一瞬フリーズする。フェードアウトの数秒を使って
        //   バックグラウンドで完了させておくことで、切替時のヒッチを防ぐ）。
        //   Area10ボスラッシュではPreloadClipsForArea()により開始時点から先読み済みのはずだが、
        //   念のためここでも呼んでおく（Area10以外の通常フローでも安全に動作する）。
        AudioClip[] upcomingClips = FindClipsForArea(areaNumber);
        if (upcomingClips != null)
        {
            foreach (var clip in upcomingClips)
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                    clip.LoadAudioData();
        }

        if (fadeOutDuration > 0f && audioSource.isPlaying)
        {
            isFadingVolume = true;
            float startVolume = audioSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            isFadingVolume = false;
        }

        // ★フェードアウトの間だけでは先読みが間に合わない場合の最終安全策。
        //   デコードが終わっていなければPlay()の瞬間に同期待ちが発生してフリーズするため、
        //   ここで明示的に完了を待つ（最大2秒。通常はPreloadClipsForArea()により既に完了しているはず
        //   なので、ここで実際に待つことはほぼ無い）。
        yield return StartCoroutine(WaitForClipsLoaded(upcomingClips, 2f));

        audioSource.Stop();
        audioSource.clip = null;

        activeClips = upcomingClips;
        shuffleQueue = null;
        nextIndex = -1;

        if (activeClips != null && activeClips.Length > 0)
            PlayRandom();

        onSwitched?.Invoke();
    }

    /// <summary>
    /// 指定エリア番号のBGMリストから、ランダムではなく指定インデックスの曲を確実に再生する
    /// （Area10 Final Stage専用：前半/後半フェーズで固定の曲を使い分けるため）。
    /// PickIndex()のシャッフルを経由しないため、常に同じ曲になる。
    /// </summary>
    public void FadeOutAndSwitchToAreaClipIndex(int areaNumber, int clipIndex, float fadeOutDuration, System.Action onSwitched = null)
    {
        StartCoroutine(FadeOutAndSwitchToAreaClipIndexRoutine(areaNumber, clipIndex, fadeOutDuration, onSwitched));
    }

    private IEnumerator FadeOutAndSwitchToAreaClipIndexRoutine(int areaNumber, int clipIndex, float fadeOutDuration, System.Action onSwitched)
    {
        if (audioSource == null) yield break;

        AudioClip[] clips = FindClipsForArea(areaNumber);
        AudioClip upcoming = (clips != null && clipIndex >= 0 && clipIndex < clips.Length) ? clips[clipIndex] : null;
        if (upcoming == null)
        {
            Debug.LogWarning($"[GameplayBgmRandomPlayer] FadeOutAndSwitchToAreaClipIndex: Area{areaNumber}のclips[{clipIndex}]が見つかりません。");
            yield break;
        }
        if (upcoming.loadState == AudioDataLoadState.Unloaded)
            upcoming.LoadAudioData();

        if (fadeOutDuration > 0f && audioSource.isPlaying)
        {
            isFadingVolume = true;
            float startVolume = audioSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            isFadingVolume = false;
        }

        AudioClip[] singleClipArray = new AudioClip[] { upcoming };
        yield return StartCoroutine(WaitForClipsLoaded(singleClipArray, 2f));

        audioSource.Stop();
        audioSource.clip = null;

        activeClips = singleClipArray;
        shuffleQueue = null;
        nextIndex = -1;

        PlayRandom();

        onSwitched?.Invoke();
    }

    private IEnumerator WaitForClipsLoaded(AudioClip[] clips, float maxWait)
    {
        if (clips == null) yield break;
        float elapsed = 0f;
        while (elapsed < maxWait)
        {
            bool allLoaded = true;
            foreach (var clip in clips)
            {
                if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
                {
                    allLoaded = false;
                    break;
                }
            }
            if (allLoaded) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
