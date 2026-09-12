using UnityEngine;
using System.Collections;

/// <summary>
/// 指定Area・指定Stageの間だけ、カラスが時々画面を横切る背景演出をスポーンする。
/// Play前のInspectorで全パラメータ調整可能。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する
/// （crowFramesフィールドに羽ばたき4コマを割り当てること）。
/// </summary>
public class CrowFlybySpawner : MonoBehaviour
{
    [Header("発生条件")]
    [Tooltip("この演出を発生させるArea番号")]
    [SerializeField] private int targetAreaNumber = 1;

    [Tooltip("この演出を発生させるStageインデックス（0=Stage1, 1=Stage2, 2=Stage3）")]
    [SerializeField] private int[] activeStageIndices = { 0, 1 };

    [Header("素材（羽ばたき4コマ・ループ再生）")]
    [Tooltip("羽ばたきの4コマ。素材は右向きに飛ぶ絵として用意すること（左向きは自動で水平反転）")]
    [SerializeField] private Sprite[] crowFrames = new Sprite[4];

    [Tooltip("羽ばたきのコマ送り速度（1秒あたりのコマ数）")]
    [SerializeField] private float frameRate = 10f;

    [Tooltip("スプライトの表示スケール")]
    [SerializeField] private Vector3 crowScale = new Vector3(0.3f, 0.3f, 1f);

    [Tooltip("カラスの不透明度（1=不透明、0.8なら少し透けて見える）")]
    [Range(0f, 1f)]
    [SerializeField] private float crowAlpha = 0.8f;

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -6;

    [Header("出現間隔（秒）")]
    [SerializeField] private float spawnIntervalMin = 5f;
    [SerializeField] private float spawnIntervalMax = 10f;

    [Header("出現位置（ワールド座標）")]
    [Tooltip("どの高さ範囲に出すか（下限を下げると低い位置からも出現するようになる）")]
    [SerializeField] private float spawnHeightMin = 0.5f;
    [SerializeField] private float spawnHeightMax = 4.5f;

    [Tooltip("画面端からどれだけ外側に出現させるか")]
    [SerializeField] private float spawnOffscreenMargin = 1.5f;

    [Header("飛行角度")]
    [Tooltip("水平方向から何度までランダムに斜めに飛ばすか（±この範囲）")]
    [SerializeField] private float maxLaunchAngleDegrees = 25f;

    [Tooltip("飛行中に進行方向がゆっくり曲がる角速度の範囲（度/秒）。弧を描く軌道になる。0にすると直進のまま")]
    [SerializeField] private float turnRateMin = 3f;
    [SerializeField] private float turnRateMax = 8f;

    [Tooltip("フェードアウト開始の何秒前から曲がり始めるか（この範囲でランダム）")]
    [SerializeField] private float turnLeadTimeMin = 0.5f;
    [SerializeField] private float turnLeadTimeMax = 1f;

    [Header("飛行速度（緩急あり・鳥らしいflap-glide）")]
    [Tooltip("基準となる飛行速度の範囲")]
    [SerializeField] private float baseSpeedMin = 1.2f;
    [SerializeField] private float baseSpeedMax = 2.0f;

    [Tooltip("基準速度に対する速度倍率の変動範囲（0.6〜1.6なら基準速度の60%〜160%の間でランダムに変化する）")]
    [SerializeField] private float speedMultiplierMin = 0.6f;
    [SerializeField] private float speedMultiplierMax = 1.6f;

    [Tooltip("次に速度目標が変わるまでの間隔（秒）。この範囲内でランダムに決まり、緩急のタイミングも毎回変わる")]
    [SerializeField] private float speedChangeIntervalMin = 0.4f;
    [SerializeField] private float speedChangeIntervalMax = 2.0f;

    [Tooltip("速度が目標値へ近づく速さ（大きいほど急に加減速する。小さいほどなめらかに変化する）")]
    [SerializeField] private float speedTransitionRate = 3f;

    [Header("フェードアウト（一定時間後に縮小しながら遠ざかって消える）")]
    [Tooltip("フェードアウトを開始するまでの飛行時間（秒）")]
    [SerializeField] private float visibleDuration = 4f;

    [Tooltip("フェードアウト（透明化+縮小）にかける時間（秒）")]
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Tooltip("フェードアウト完了時点でのスケール倍率（0.3なら30%まで縮んでから消える）")]
    [Range(0f, 1f)]
    [SerializeField] private float minScaleAtFadeEnd = 0.3f;

    private Coroutine spawnCoroutine;

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
            StopSpawning();
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
            SpawnCrow();
        }
    }

    private void SpawnCrow()
    {
        if (crowFrames == null || crowFrames.Length == 0 || crowFrames[0] == null || Camera.main == null) return;

        Camera cam = Camera.main;
        bool startFromLeft = Random.value < 0.5f;
        float camX = cam.transform.position.x;
        float height = cam.orthographicSize;
        float width = height * cam.aspect;

        float horizontalSign = startFromLeft ? 1f : -1f;
        float startX = startFromLeft ? camX - width - spawnOffscreenMargin : camX + width + spawnOffscreenMargin;
        float y = Random.Range(spawnHeightMin, spawnHeightMax);

        // 上下方向は水平方向（左右反転）と無関係に独立でランダムにする
        // （+angle=斜め上、-angle=斜め下。水平反転に関わらずこの符号を保つ）
        float verticalAngle = Random.Range(-maxLaunchAngleDegrees, maxLaunchAngleDegrees);
        float angleRad = verticalAngle * Mathf.Deg2Rad;
        Vector3 flyDir = new Vector3(horizontalSign * Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);

        GameObject go = new GameObject("CrowFlyby");
        go.transform.position = new Vector3(startX, y, 0f);
        go.transform.localScale = crowScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = crowFrames[0];
        sr.color = new Color(0f, 0f, 0f, crowAlpha);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        CrowController controller = go.AddComponent<CrowController>();
        float baseSpeed = Random.Range(baseSpeedMin, baseSpeedMax);
        float turnRate = Random.Range(turnRateMin, turnRateMax) * (Random.value < 0.5f ? 1f : -1f);
        float turnLeadTime = Random.Range(turnLeadTimeMin, turnLeadTimeMax);
        controller.Init(crowFrames, frameRate, flyDir, baseSpeed,
            speedMultiplierMin, speedMultiplierMax, speedChangeIntervalMin, speedChangeIntervalMax,
            speedTransitionRate, horizontalSign > 0f,
            visibleDuration, fadeOutDuration, minScaleAtFadeEnd, turnRate, turnLeadTime);
    }
}
