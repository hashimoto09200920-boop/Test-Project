using UnityEngine;

/// <summary>
/// Area10 Final Stage背景の星演出。小さい星は最奥レイヤーが見える範囲（画面中央あたり）に
/// ランダムな数・配置の星団としてまとめて配置し、大きい星は画面全体にランダムな形・色で散らす。
/// どちらも色はAreaSelectの10エリアカラーからランダム選択する（StarFlareSpawnerと同じ値）。
/// Play前のInspectorで全パラメータ調整可能。追加のアート素材は不要（TwinkleStarControllerが手続き的に生成）。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する。
/// </summary>
public class Area10StarFieldSpawner : MonoBehaviour
{
    [Header("色（AreaSelectの10エリアカラー、MeteorEffect/StarFlareSpawnerと同じ値）")]
    [SerializeField] private Color[] areaColors = new Color[]
    {
        new Color(0.60784316f, 0.56078434f, 0.78039217f, 1f),
        new Color(0.29803923f, 0.6862745f, 0.49019608f, 1f),
        new Color(0.5529412f, 0.6f, 0.68235296f, 1f),
        new Color(0.8784314f, 0.47843137f, 0.24705882f, 1f),
        new Color(0.69803923f, 0.22745098f, 0.32156864f, 1f),
        new Color(0.8784314f, 0.6901961f, 0.30980393f, 1f),
        new Color(0.30980393f, 0.56078434f, 0.8784314f, 1f),
        new Color(0.37254903f, 0.8392157f, 0.8392157f, 1f),
        new Color(0.6392157f, 0.68235296f, 0.8784314f, 1f),
        new Color(0.91f, 0.79f, 0.42f, 1f),
    };

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Background";
    [Tooltip("FarLayer=-10, MidLayer=-9, Silhouette=-8 より手前、Gameplayレイヤー（敵/プレイヤー）より奥")]
    [SerializeField] private int sortingOrder = -7;

    [Header("小さい星：星団（最奥レイヤーが見える範囲＝画面中央あたり）")]
    [Tooltip("星団の中心をランダムに置く範囲（ワールド座標）")]
    [SerializeField] private Vector2 clusterRegionMin = new Vector2(-4f, -2f);
    [SerializeField] private Vector2 clusterRegionMax = new Vector2(4f, 2f);
    [Tooltip("星団の数（この範囲でランダム）")]
    [SerializeField] private int clusterCountMin = 3;
    [SerializeField] private int clusterCountMax = 6;
    [Tooltip("1つの星団に含まれる星の数（この範囲でランダム）")]
    [SerializeField] private int starsPerClusterMin = 4;
    [SerializeField] private int starsPerClusterMax = 10;
    [Tooltip("星団の中心から各星がどれだけ散らばるか（ワールド単位半径）")]
    [SerializeField] private float clusterRadius = 0.6f;
    [SerializeField] private float smallStarSizeMin = 0.04f;
    [SerializeField] private float smallStarSizeMax = 0.09f;

    [Header("大きい星：画面全体に散らす")]
    [SerializeField] private int bigStarCountMin = 6;
    [SerializeField] private int bigStarCountMax = 10;
    [SerializeField] private Vector2 bigStarRegionMin = new Vector2(-10.5f, -5f);
    [SerializeField] private Vector2 bigStarRegionMax = new Vector2(10.5f, 5f);
    [SerializeField] private float bigStarSizeMin = 0.15f;
    [SerializeField] private float bigStarSizeMax = 0.35f;
    [Tooltip("大きい星の腕の本数をこの中からランダムに選ぶ（本数が変わるごとに形の印象が変わる）")]
    [SerializeField] private int[] bigStarAxisCounts = { 4, 6, 8 };
    [Tooltip("腕の鋭さ（大きいほど腕が細く鋭くなる）")]
    [SerializeField] private float bigStarSharpnessMin = 4f;
    [SerializeField] private float bigStarSharpnessMax = 8f;

    [Header("明滅タイミング（秒）共通")]
    [SerializeField] private float flashInDurationMin = 0.3f;
    [SerializeField] private float flashInDurationMax = 0.8f;
    [SerializeField] private float holdDurationMin = 0.5f;
    [SerializeField] private float holdDurationMax = 1.5f;
    [SerializeField] private float fadeOutDurationMin = 0.8f;
    [SerializeField] private float fadeOutDurationMax = 2f;
    [SerializeField] private float waitBetweenMin = 0.5f;
    [SerializeField] private float waitBetweenMax = 3f;
    [Range(0f, 1f)]
    [SerializeField] private float smallStarMaxAlpha = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float bigStarMaxAlpha = 0.9f;

    [Header("発生条件（★他Area・他Stageに絶対影響させないための判定）")]
    [Tooltip("Area10ボスラッシュのFinal Stageに該当するStageIndex（waveStagesの4番目=index3）")]
    [SerializeField] private int finalStageIndex = 3;

    [Header("Final Stage中のFar透過調整（小さい星の視認性向上用）")]
    [Tooltip("Background_FarのSpriteRenderer。未設定なら透過調整は行わない")]
    [SerializeField] private SpriteRenderer farLayerForFade;
    [Tooltip("Final Stage中、Farレイヤーのアルファ値をこの値に変更する（1=変更なし、0=完全透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float finalStageFarAlpha = 1f;

    private bool isActive = false;
    private float originalFarAlpha = 1f;
    private bool farAlphaOverridden = false;

    private void OnEnable()
    {
        EnemySpawner.OnStageStarted += OnStageStarted;
    }

    private void OnDisable()
    {
        EnemySpawner.OnStageStarted -= OnStageStarted;
        StopEffect();
    }

    private void OnStageStarted(int stageIndex)
    {
        bool shouldBeActive = GameSession.IsBossRushActive && stageIndex == finalStageIndex;
        if (shouldBeActive && !isActive)
        {
            isActive = true;
            SpawnClusters();
            SpawnBigStars();
        }
        else if (!shouldBeActive && isActive)
        {
            StopEffect();
        }
    }

    // ★一度きりの適用だと、(1)Inspectorで後から数値を変えても反映されない
    //   (2)FarLayerFadeのフェードイン完了時にalpha=1へ強制的に戻される処理と競合して
    //   上書きされてしまう、という2つの問題があった。Final Stage中は毎フレーム
    //   継続して強制することで、常に最新の設定値を確実に反映させる。
    private void Update()
    {
        if (!isActive) return;
        ApplyFarAlpha();
    }

    private void ApplyFarAlpha()
    {
        if (farLayerForFade == null) return;

        // ★Farが「時間帯巡回」演出(TimeOfDayFade)を使っている場合、元のSpriteRenderer自体は
        //   enabled=falseで非表示になっており、実際に見えているのは内部生成された別レイヤー。
        //   その場合は必ずTimeOfDayFade側の外部乗算値を使わないと見た目に反映されない。
        TimeOfDayFade timeOfDayFade = farLayerForFade.GetComponent<TimeOfDayFade>();
        if (timeOfDayFade != null)
        {
            timeOfDayFade.ExternalAlphaMultiplier = finalStageFarAlpha;
        }

        if (!farAlphaOverridden)
        {
            originalFarAlpha = farLayerForFade.color.a;
            farAlphaOverridden = true;
        }
        Color c = farLayerForFade.color;
        c.a = finalStageFarAlpha;
        farLayerForFade.color = c;
    }

    private void RestoreFarAlpha()
    {
        if (farLayerForFade == null || !farAlphaOverridden) return;

        TimeOfDayFade timeOfDayFade = farLayerForFade.GetComponent<TimeOfDayFade>();
        if (timeOfDayFade != null)
        {
            timeOfDayFade.ExternalAlphaMultiplier = 1f;
        }

        Color c = farLayerForFade.color;
        c.a = originalFarAlpha;
        farLayerForFade.color = c;
        farAlphaOverridden = false;
    }

    private void StopEffect()
    {
        isActive = false;
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        RestoreFarAlpha();
    }

    private Color RandomAreaColor()
    {
        return (areaColors != null && areaColors.Length > 0)
            ? areaColors[Random.Range(0, areaColors.Length)]
            : Color.white;
    }

    private void SpawnClusters()
    {
        int clusterCount = Random.Range(clusterCountMin, clusterCountMax + 1);
        for (int c = 0; c < clusterCount; c++)
        {
            Vector2 clusterCenter = new Vector2(
                Random.Range(clusterRegionMin.x, clusterRegionMax.x),
                Random.Range(clusterRegionMin.y, clusterRegionMax.y));

            int starCount = Random.Range(starsPerClusterMin, starsPerClusterMax + 1);
            for (int s = 0; s < starCount; s++)
            {
                Vector2 offset = Random.insideUnitCircle * clusterRadius;
                Vector3 pos = new Vector3(clusterCenter.x + offset.x, clusterCenter.y + offset.y, 0f);

                GameObject go = new GameObject("TwinkleStar_Small");
                go.transform.SetParent(transform, false);
                go.transform.position = pos;

                var star = go.AddComponent<TwinkleStarController>();
                star.InitSmall(
                    RandomAreaColor(),
                    Random.Range(smallStarSizeMin, smallStarSizeMax),
                    Random.Range(flashInDurationMin, flashInDurationMax),
                    Random.Range(holdDurationMin, holdDurationMax),
                    Random.Range(fadeOutDurationMin, fadeOutDurationMax),
                    waitBetweenMin, waitBetweenMax,
                    smallStarMaxAlpha,
                    Random.Range(0f, waitMaxForInitialDelay()),
                    sortingLayerName, sortingOrder);
            }
        }
    }

    private void SpawnBigStars()
    {
        int count = Random.Range(bigStarCountMin, bigStarCountMax + 1);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(bigStarRegionMin.x, bigStarRegionMax.x),
                Random.Range(bigStarRegionMin.y, bigStarRegionMax.y),
                0f);

            GameObject go = new GameObject("TwinkleStar_Big");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;

            int axisCount = (bigStarAxisCounts != null && bigStarAxisCounts.Length > 0)
                ? bigStarAxisCounts[Random.Range(0, bigStarAxisCounts.Length)]
                : 4;

            var star = go.AddComponent<TwinkleStarController>();
            star.InitBig(
                RandomAreaColor(),
                Random.Range(bigStarSizeMin, bigStarSizeMax),
                axisCount,
                Random.Range(bigStarSharpnessMin, bigStarSharpnessMax),
                Random.Range(flashInDurationMin, flashInDurationMax),
                Random.Range(holdDurationMin, holdDurationMax),
                Random.Range(fadeOutDurationMin, fadeOutDurationMax),
                waitBetweenMin, waitBetweenMax,
                bigStarMaxAlpha,
                Random.Range(0f, waitMaxForInitialDelay()),
                sortingLayerName, sortingOrder + 1);
        }
    }

    // 全ての星が同時にflash inしないよう、開始直後だけランダムな初期遅延を持たせる
    private float waitMaxForInitialDelay() => Mathf.Max(waitBetweenMax, holdDurationMax + flashInDurationMax);

    // Play前のScene viewで出現範囲を可視化（ワールド座標そのまま、Inspector値変更に自動追従）
    private void OnDrawGizmos()
    {
        DrawRegionGizmo(clusterRegionMin, clusterRegionMax, new Color(0.3f, 0.8f, 1f, 1f), "小さい星の範囲");
        DrawRegionGizmo(bigStarRegionMin, bigStarRegionMax, new Color(1f, 0.6f, 0.15f, 1f), "大きい星の範囲");
    }

    private void DrawRegionGizmo(Vector2 min, Vector2 max, Color color, string label)
    {
        Vector3 center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
        Vector3 size = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), 0f);

        Gizmos.color = color;
        Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(new Vector3(min.x, max.y, 0f), label);
#endif
    }
}
