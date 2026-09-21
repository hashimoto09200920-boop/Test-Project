using UnityEngine;
using System.Collections;

/// <summary>
/// 指定Area・指定Stageの間だけ、コウモリの群れ（3〜5匹）が時々画面を横切る背景演出をスポーンする。
/// CrowFlybySpawnerと同じ「画面端から出現し直進+緩急+フェード間際に弧」がベース。
/// 1回のスポーンタイミングで複数匹をまとめて出し、それぞれ出現Y位置・速度・スポーンタイミングを
/// 少しずつランダムにずらすことで、群れっぽく見せる。
/// Play前のInspectorで全パラメータ調整可能。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する
/// （batFramesフィールドに羽ばたき3コマを割り当てること）。
/// </summary>
public class BatFlybySwarmSpawner : MonoBehaviour
{
    [Header("発生条件")]
    [Tooltip("この演出を発生させるArea番号")]
    [SerializeField] private int targetAreaNumber = 4;

    [Tooltip("この演出を発生させるStageインデックス（0=Stage1, 1=Stage2, 2=Stage3）")]
    [SerializeField] private int[] activeStageIndices = { 0, 1 };

    [Header("素材（羽ばたき3コマ・ループ再生）")]
    [Tooltip("羽ばたきの3コマ。素材は右向きに飛ぶ絵として用意すること（左向きは自動で水平反転）")]
    [SerializeField] private Sprite[] batFrames = new Sprite[3];

    [Tooltip("羽ばたきのコマ送り速度（1秒あたりのコマ数）")]
    [SerializeField] private float frameRate = 12f;

    [Tooltip("スプライトの表示スケール")]
    [SerializeField] private Vector3 batScale = new Vector3(0.2f, 0.2f, 1f);

    [Tooltip("コウモリの不透明度（1=不透明、0.8なら少し透けて見える）")]
    [Range(0f, 1f)]
    [SerializeField] private float batAlpha = 0.8f;

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Background";
    [SerializeField] private int sortingOrder = -6;

    [Header("群れの出現間隔（秒）")]
    [SerializeField] private float spawnIntervalMin = 6f;
    [SerializeField] private float spawnIntervalMax = 12f;

    [Header("1回のスポーンで出す匹数")]
    [SerializeField] private int swarmCountMin = 3;
    [SerializeField] private int swarmCountMax = 5;

    [Tooltip("群れ内での出現タイミングのずれ（秒）。0〜この値の範囲でランダムに遅延する")]
    [SerializeField] private float swarmSpawnStagger = 0.3f;

    [Tooltip("群れ内での出現Y位置のばらつき幅（基準位置から±この範囲）")]
    [SerializeField] private float swarmSpreadY = 0.8f;

    [Header("出現位置（ワールド座標）")]
    [Tooltip("どの高さ範囲に出すか（群れ基準位置の抽選範囲）")]
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

    [Header("飛行速度（緩急あり）")]
    [SerializeField] private float baseSpeedMin = 1.4f;
    [SerializeField] private float baseSpeedMax = 2.4f;
    [SerializeField] private float speedMultiplierMin = 0.6f;
    [SerializeField] private float speedMultiplierMax = 1.6f;
    [SerializeField] private float speedChangeIntervalMin = 0.4f;
    [SerializeField] private float speedChangeIntervalMax = 2.0f;
    [SerializeField] private float speedTransitionRate = 3f;

    [Header("不規則な上下フラッター（バットらしい動き）")]
    [Tooltip("上下フラッターの振れ幅（ワールド単位）")]
    [SerializeField] private float flutterAmplitude = 0.35f;
    [Tooltip("次にフラッター目標が変わるまでの間隔（秒）")]
    [SerializeField] private float flutterChangeIntervalMin = 0.1f;
    [SerializeField] private float flutterChangeIntervalMax = 0.4f;
    [Tooltip("フラッターオフセットが目標値へ近づく速さ")]
    [SerializeField] private float flutterTransitionRate = 4f;

    [Header("フェードアウト（一定時間後に縮小しながら遠ざかって消える）")]
    [SerializeField] private float visibleDuration = 4f;
    [SerializeField] private float fadeOutDuration = 1.5f;
    [Range(0f, 1f)]
    [SerializeField] private float minScaleAtFadeEnd = 0.3f;

    private Coroutine spawnCoroutine;
    private readonly System.Collections.Generic.List<BatFlybyController> activeInstances = new System.Collections.Generic.List<BatFlybyController>();

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
            yield return StartCoroutine(SpawnSwarm());
        }
    }

    private IEnumerator SpawnSwarm()
    {
        if (batFrames == null || batFrames.Length == 0 || batFrames[0] == null || Camera.main == null) yield break;

        Camera cam = Camera.main;
        bool startFromLeft = Random.value < 0.5f;
        float camX = cam.transform.position.x;
        float height = cam.orthographicSize;
        float width = height * cam.aspect;

        float horizontalSign = startFromLeft ? 1f : -1f;
        float startX = startFromLeft ? camX - width - spawnOffscreenMargin : camX + width + spawnOffscreenMargin;
        float baseY = Random.Range(spawnHeightMin, spawnHeightMax);

        int count = Random.Range(swarmCountMin, swarmCountMax + 1);
        for (int i = 0; i < count; i++)
        {
            SpawnBat(startX, baseY, horizontalSign);

            if (swarmSpawnStagger > 0f)
            {
                float delay = Random.Range(0f, swarmSpawnStagger);
                float elapsed = 0f;
                while (elapsed < delay)
                {
                    elapsed += Time.deltaTime * TimeScale;
                    yield return null;
                }
            }
        }
    }

    private void SpawnBat(float startX, float baseY, float horizontalSign)
    {
        float y = baseY + Random.Range(-swarmSpreadY, swarmSpreadY);

        // 上下方向は水平方向（左右反転）と無関係に独立でランダムにする（Crowと同じ方式）
        float verticalAngle = Random.Range(-maxLaunchAngleDegrees, maxLaunchAngleDegrees);
        float angleRad = verticalAngle * Mathf.Deg2Rad;
        Vector3 flyDir = new Vector3(horizontalSign * Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);

        GameObject go = new GameObject("BatFlyby");
        go.transform.position = new Vector3(startX, y, 0f);
        go.transform.localScale = batScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = batFrames[0];
        sr.color = new Color(0f, 0f, 0f, batAlpha);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        BatFlybyController controller = go.AddComponent<BatFlybyController>();
        float baseSpeed = Random.Range(baseSpeedMin, baseSpeedMax);
        float turnRate = Random.Range(turnRateMin, turnRateMax) * (Random.value < 0.5f ? 1f : -1f);
        float turnLeadTime = Random.Range(turnLeadTimeMin, turnLeadTimeMax);
        controller.Init(batFrames, frameRate, flyDir, baseSpeed,
            speedMultiplierMin, speedMultiplierMax, speedChangeIntervalMin, speedChangeIntervalMax,
            speedTransitionRate, horizontalSign > 0f,
            visibleDuration, fadeOutDuration, minScaleAtFadeEnd, turnRate, turnLeadTime,
            flutterAmplitude, flutterChangeIntervalMin, flutterChangeIntervalMax, flutterTransitionRate);
        activeInstances.Add(controller);
    }
}
