using UnityEngine;
using System.Collections;

/// <summary>
/// 指定Area・指定Stageの間だけ、タンブルウィードが地面を転がって時々横切る背景演出をスポーンする。
/// Play前のInspectorで全パラメータ調整可能。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する
/// （tumbleweedSpritesフィールドに素材を割り当てること。複数あればランダムに選ぶ）。
/// </summary>
public class TumbleweedSpawner : MonoBehaviour
{
    [Header("発生条件")]
    [Tooltip("この演出を発生させるArea番号")]
    [SerializeField] private int targetAreaNumber = 6;

    [Tooltip("この演出を発生させるStageインデックス（0=Stage1, 1=Stage2, 2=Stage3）")]
    [SerializeField] private int[] activeStageIndices = { 0, 1 };

    [Header("素材（複数あればランダムに1個選ぶ）")]
    [SerializeField] private Sprite[] tumbleweedSprites = new Sprite[2];

    [SerializeField] private Vector3 tumbleweedScale = new Vector3(0.25f, 0.25f, 1f);

    [Tooltip("不透明度（1=不透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float tumbleweedAlpha = 0.85f;

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Background";
    [SerializeField] private int sortingOrder = -5;

    [Header("出現間隔（秒）")]
    [SerializeField] private float spawnIntervalMin = 5f;
    [SerializeField] private float spawnIntervalMax = 10f;

    [Header("出現位置（ワールド座標）")]
    [Tooltip("地面（砂丘のライン）の高さの範囲。ダンサーの位置(Y約-2.9)より上になるよう調整すること")]
    [SerializeField] private float groundYMin = -1.5f;
    [SerializeField] private float groundYMax = -0.3f;

    [Tooltip("画面端からどれだけ外側に出現させるか")]
    [SerializeField] private float spawnOffscreenMargin = 1.5f;

    [Header("転がる速度（風の緩急あり）")]
    [SerializeField] private float baseSpeedMin = 1.5f;
    [SerializeField] private float baseSpeedMax = 3f;
    [SerializeField] private float speedMultiplierMin = 0.5f;
    [SerializeField] private float speedMultiplierMax = 1.6f;
    [SerializeField] private float speedChangeIntervalMin = 0.5f;
    [SerializeField] private float speedChangeIntervalMax = 2f;
    [SerializeField] private float speedTransitionRate = 2.5f;

    [Header("回転（転がる見た目）")]
    [Tooltip("ワールド1単位移動するごとに何度回転するか（大きいほど速く回っているように見える）")]
    [SerializeField] private float rotationDegPerUnit = 220f;

    [Header("進行方向の角度・弧（Crowと同じ方式）")]
    [Tooltip("水平方向から何度までランダムに斜めに転がらせるか（±この範囲）")]
    [SerializeField] private float maxLaunchAngleDegrees = 10f;

    [Tooltip("飛行中に進行方向がゆっくり曲がる角速度の範囲（度/秒）。0にすると直進のまま")]
    [SerializeField] private float turnRateMin = 3f;
    [SerializeField] private float turnRateMax = 8f;

    [Tooltip("フェードアウト開始の何秒前から曲がり始めるか（この範囲でランダム）")]
    [SerializeField] private float turnLeadTimeMin = 0.5f;
    [SerializeField] private float turnLeadTimeMax = 1f;

    [Header("地面の凹凸で跳ねるバウンド")]
    [SerializeField] private float bounceAmplitude = 0.1f;
    [SerializeField] private float bounceFrequency = 1.5f;

    [Header("フェードアウト（一定時間後に縮小しながら消える）")]
    [SerializeField] private float visibleDurationMin = 4f;
    [SerializeField] private float visibleDurationMax = 7f;
    [SerializeField] private float fadeOutDuration = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float minScaleAtFadeEnd = 0.4f;

    private Coroutine spawnCoroutine;
    private readonly System.Collections.Generic.List<TumbleweedController> activeInstances = new System.Collections.Generic.List<TumbleweedController>();

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
        bool areaMatches = !GameSession.IsBossRushActive && GameSession.HasValidArea() && GameSession.GetEffectiveAreaNumber() == targetAreaNumber;
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
            SpawnTumbleweed();
        }
    }

    private void SpawnTumbleweed()
    {
        if (tumbleweedSprites == null || tumbleweedSprites.Length == 0 || Camera.main == null) return;

        Sprite sprite = tumbleweedSprites[Random.Range(0, tumbleweedSprites.Length)];
        if (sprite == null) return;

        Camera cam = Camera.main;
        float camX = cam.transform.position.x;
        float height = cam.orthographicSize;
        float width = height * cam.aspect;

        // Area6の風はFogScroll(extraFogLayers)と同じく右→左（Vector3.left）に統一されているため、
        // タンブルウィードも必ず右端から出現し、左へ転がっていく（ランダム方向にはしない）
        float dirSign = -1f;
        float startX = camX + width + spawnOffscreenMargin;
        float y = Random.Range(groundYMin, groundYMax);

        // 水平方向から若干の角度をつける（水平方向とは独立にランダム、Crowと同じ方式）
        float verticalAngle = Random.Range(-maxLaunchAngleDegrees, maxLaunchAngleDegrees);
        float angleRad = verticalAngle * Mathf.Deg2Rad;
        Vector3 initialDir = new Vector3(dirSign * Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);

        GameObject go = new GameObject("Tumbleweed");
        go.transform.position = new Vector3(startX, y, 0f);
        go.transform.localScale = tumbleweedScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0f, 0f, 0f, tumbleweedAlpha);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        TumbleweedController controller = go.AddComponent<TumbleweedController>();
        float speed = Random.Range(baseSpeedMin, baseSpeedMax);
        float visibleDuration = Random.Range(visibleDurationMin, visibleDurationMax);
        float turnRate = Random.Range(turnRateMin, turnRateMax) * (Random.value < 0.5f ? 1f : -1f);
        float turnLeadTime = Random.Range(turnLeadTimeMin, turnLeadTimeMax);
        controller.Init(initialDir, speed, y,
            speedMultiplierMin, speedMultiplierMax, speedChangeIntervalMin, speedChangeIntervalMax, speedTransitionRate,
            rotationDegPerUnit, bounceAmplitude, bounceFrequency,
            visibleDuration, fadeOutDuration, minScaleAtFadeEnd,
            turnRate, turnLeadTime);
        activeInstances.Add(controller);
    }
}
