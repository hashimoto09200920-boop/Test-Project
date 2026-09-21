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

    private void Start()
    {
        SpawnClusters();
        SpawnBigStars();
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
}
