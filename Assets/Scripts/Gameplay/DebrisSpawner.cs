using UnityEngine;
using System.Collections;

/// <summary>
/// 指定Area・指定Stageの間だけ、岩/氷の欠片が画面下から上昇気流に巻き上げられていく
/// 背景演出をスポーンする（Area8のblockSprite/blockSprite2の欠片という設定）。
/// VortexScroll（MidLayerの雲）と同じ「速度の緩急＋それに連動した左右ウェーブ」を使う。
/// Play前のInspectorで全パラメータ調整可能。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する
/// （debrisSpritesフィールドに岩片・氷片の素材を割り当てること。複数あればランダムに選ぶ）。
/// </summary>
public class DebrisSpawner : MonoBehaviour
{
    [Header("発生条件")]
    [Tooltip("この演出を発生させるArea番号")]
    [SerializeField] private int targetAreaNumber = 8;

    [Tooltip("この演出を発生させるStageインデックス（0=Stage1, 1=Stage2, 2=Stage3）")]
    [SerializeField] private int[] activeStageIndices = { 0, 1 };

    [Header("素材（岩片3種+氷片3種。複数あればランダムに1個選ぶ）")]
    [SerializeField] private Sprite[] debrisSprites = new Sprite[6];

    [SerializeField] private Vector3 debrisScaleMin = new Vector3(0.08f, 0.08f, 1f);
    [SerializeField] private Vector3 debrisScaleMax = new Vector3(0.18f, 0.18f, 1f);

    [Tooltip("不透明度（1=不透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float debrisAlpha = 0.9f;

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -5;

    [Header("出現間隔（秒）")]
    [SerializeField] private float spawnIntervalMin = 1.5f;
    [SerializeField] private float spawnIntervalMax = 3.5f;

    [Header("1回のスポーンでまとめて出す個数")]
    [SerializeField] private int burstCountMin = 3;
    [SerializeField] private int burstCountMax = 6;
    [Tooltip("まとめ出現内での出現タイミングのずれ（秒）。0〜この値の範囲でランダムに遅延する")]
    [SerializeField] private float burstSpawnStagger = 0.15f;
    [Tooltip("まとめ出現内での出現X位置のばらつき幅（基準位置から±この範囲）")]
    [SerializeField] private float burstSpreadX = 1.5f;

    [Header("出現位置（ワールド座標）")]
    [Tooltip("画面下端からどれだけ下に出現させるか")]
    [SerializeField] private float spawnBelowScreenMargin = 1f;
    [SerializeField] private float spawnXMin = -8f;
    [SerializeField] private float spawnXMax = 8f;

    [Header("上昇速度の緩急（VortexScrollと同じ方式）")]
    [SerializeField] private float minSpeed = 2f;
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float minHoldDuration = 0.5f;
    [SerializeField] private float maxHoldDuration = 2f;
    [SerializeField] private float speedTransitionRate = 8f;

    [Header("左右ウェーブ（上昇速度に連動）")]
    [SerializeField] private float waveAmplitude = 0.6f;
    [SerializeField] private float waveFrequency = 40f;

    [Header("宙を漂う回転")]
    [Tooltip("回転速度の範囲（度/秒）。±この範囲でランダムに決まる")]
    [SerializeField] private float rotationSpeedRange = 90f;

    [Header("フェードアウト（一定時間上昇後、縮小しながら消える）")]
    [SerializeField] private float visibleDurationMin = 2f;
    [SerializeField] private float visibleDurationMax = 4f;
    [SerializeField] private float fadeOutDuration = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float minScaleAtFadeEnd = 0.3f;

    private Coroutine spawnCoroutine;
    private readonly System.Collections.Generic.List<DebrisController> activeInstances = new System.Collections.Generic.List<DebrisController>();

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void OnEnable()
    {
        EnemySpawner.OnStageStarted += OnStageStarted;
    }

    private void OnDisable()
    {
        EnemySpawner.OnStageStarted -= OnStageStarted;
        StopSpawning();
    }

    private void OnStageStarted(int stageIndex)
    {
        bool areaMatches = GameSession.HasValidArea() && GameSession.SelectedArea.areaNumber == targetAreaNumber;
        bool stageMatches = System.Array.IndexOf(activeStageIndices, stageIndex) >= 0;

        if (areaMatches && stageMatches)
            StartSpawning();
        else
        {
            StopSpawning();
            FadeOutAllActive();
        }
    }

    /// <summary>対象Stageから外れた瞬間に、既に画面上にいる個体を即座にフェードアウトさせる</summary>
    private void FadeOutAllActive()
    {
        for (int i = 0; i < activeInstances.Count; i++)
        {
            if (activeInstances[i] != null) activeInstances[i].ForceFadeOut();
        }
        activeInstances.Clear();
    }

    private void StartSpawning()
    {
        if (spawnCoroutine != null) return;
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    private void StopSpawning()
    {
        if (spawnCoroutine == null) return;
        StopCoroutine(spawnCoroutine);
        spawnCoroutine = null;
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            float wait = Random.Range(spawnIntervalMin, spawnIntervalMax);
            float elapsed = 0f;
            while (elapsed < wait)
            {
                elapsed += Time.deltaTime * TimeScale;
                yield return null;
            }
            yield return StartCoroutine(SpawnBurst());
        }
    }

    private IEnumerator SpawnBurst()
    {
        if (Camera.main == null) yield break;

        float centerX = Random.Range(spawnXMin, spawnXMax);
        int count = Random.Range(burstCountMin, burstCountMax + 1);

        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Clamp(centerX + Random.Range(-burstSpreadX, burstSpreadX), spawnXMin, spawnXMax);
            SpawnDebris(x);

            if (burstSpawnStagger > 0f)
            {
                float delay = Random.Range(0f, burstSpawnStagger);
                float elapsed = 0f;
                while (elapsed < delay)
                {
                    elapsed += Time.deltaTime * TimeScale;
                    yield return null;
                }
            }
        }
    }

    private void SpawnDebris(float x)
    {
        if (debrisSprites == null || debrisSprites.Length == 0 || Camera.main == null) return;

        Sprite sprite = debrisSprites[Random.Range(0, debrisSprites.Length)];
        if (sprite == null) return;

        Camera cam = Camera.main;
        float camBottom = cam.transform.position.y - cam.orthographicSize;
        float y = camBottom - spawnBelowScreenMargin;

        GameObject go = new GameObject("Debris");
        go.transform.position = new Vector3(x, y, 0f);
        go.transform.localScale = new Vector3(
            Random.Range(debrisScaleMin.x, debrisScaleMax.x),
            Random.Range(debrisScaleMin.y, debrisScaleMax.y),
            1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(1f, 1f, 1f, debrisAlpha);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        DebrisController controller = go.AddComponent<DebrisController>();
        float visibleDuration = Random.Range(visibleDurationMin, visibleDurationMax);
        controller.Init(minSpeed, maxSpeed, minHoldDuration, maxHoldDuration, speedTransitionRate,
            waveAmplitude, waveFrequency, rotationSpeedRange,
            visibleDuration, fadeOutDuration, minScaleAtFadeEnd);
        activeInstances.Add(controller);
    }
}
