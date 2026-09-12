using UnityEngine;
using System.Collections;

/// <summary>
/// 指定Area・指定Stageの間だけ、星が時々ひときわ強く瞬く（フレア）背景演出をスポーンする。
/// Play前のInspectorで全パラメータ調整可能。追加のアート素材は不要（手続き的に生成）。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する。
/// </summary>
public class StarFlareSpawner : MonoBehaviour
{
    [Header("発生条件")]
    [Tooltip("この演出を発生させるArea番号")]
    [SerializeField] private int targetAreaNumber = 9;

    [Tooltip("この演出を発生させるStageインデックス（0=Stage1, 1=Stage2, 2=Stage3）")]
    [SerializeField] private int[] activeStageIndices = { 0, 1, 2 };

    [Header("見た目")]
    [Tooltip("この中からランダムに1色選んでフレアの色にする（AreaSelectの10エリアカラー、MeteorEffectと同じ値）")]
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
    [SerializeField] private float sizeMin = 0.15f;
    [SerializeField] private float sizeMax = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float glowMaxAlpha = 0.6f;

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Background";
    [SerializeField] private int sortingOrder = -9;

    [Header("出現位置（ワールド座標・星が見える範囲）")]
    [SerializeField] private float spawnXMin = -8f;
    [SerializeField] private float spawnXMax = 8f;
    [SerializeField] private float spawnYMin = -1f;
    [SerializeField] private float spawnYMax = 4.5f;

    [Header("出現間隔（秒）")]
    [SerializeField] private float spawnIntervalMin = 2f;
    [SerializeField] private float spawnIntervalMax = 5f;

    [Header("タイミング（秒）")]
    [Tooltip("一瞬で明るくなるまでの時間")]
    [SerializeField] private float flashInDurationMin = 0.1f;
    [SerializeField] private float flashInDurationMax = 0.25f;
    [SerializeField] private float holdDurationMin = 0.1f;
    [SerializeField] private float holdDurationMax = 0.4f;
    [SerializeField] private float fadeOutDurationMin = 0.8f;
    [SerializeField] private float fadeOutDurationMax = 1.8f;

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
            SpawnFlare();
        }
    }

    private void SpawnFlare()
    {
        float x = Random.Range(spawnXMin, spawnXMax);
        float y = Random.Range(spawnYMin, spawnYMax);

        GameObject go = new GameObject("StarFlare");
        go.transform.position = new Vector3(x, y, 0f);

        StarFlareController controller = go.AddComponent<StarFlareController>();
        float size = Random.Range(sizeMin, sizeMax);
        float flashIn = Random.Range(flashInDurationMin, flashInDurationMax);
        float hold = Random.Range(holdDurationMin, holdDurationMax);
        float fadeOut = Random.Range(fadeOutDurationMin, fadeOutDurationMax);

        Color color = (areaColors != null && areaColors.Length > 0)
            ? areaColors[Random.Range(0, areaColors.Length)]
            : new Color(1f, 0.97f, 0.85f, 1f);

        controller.Init(color, size, flashIn, hold, fadeOut, glowMaxAlpha, sortingLayerName, sortingOrder);
    }
}
