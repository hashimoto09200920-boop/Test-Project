using UnityEngine;
using System.Collections;

/// <summary>
/// 指定Area・指定Stageの間だけ、手前のシルエットの木々周辺に蝶をランダム出現させる背景演出。
/// Play前のInspectorで全パラメータ調整可能。
/// Hierarchy: 空のGameObjectを作成しこのスクリプトをアタッチするだけで動作する
/// （butterflyFramesフィールドに羽ばたき3コマを割り当てること）。
/// 出現ゾーン（Left/Right/BottomCenter）はGameObject選択時にSceneビューで黄色い枠として
/// 可視化されるので、実際のシルエット画像に合わせてPlay前に調整すること。
/// </summary>
public class ButterflyFlutterSpawner : MonoBehaviour
{
    [Header("発生条件")]
    [Tooltip("この演出を発生させるArea番号")]
    [SerializeField] private int targetAreaNumber = 2;

    [Tooltip("この演出を発生させるStageインデックス（0=Stage1, 1=Stage2, 2=Stage3）")]
    [SerializeField] private int[] activeStageIndices = { 0, 1 };

    [Header("素材（羽ばたき3コマ・ループ再生）")]
    [SerializeField] private Sprite[] butterflyFrames = new Sprite[3];

    [Tooltip("羽ばたきのコマ送り速度（1秒あたりのコマ数）")]
    [SerializeField] private float frameRate = 12f;

    [Tooltip("スプライトの表示スケール")]
    [SerializeField] private Vector3 butterflyScale = new Vector3(0.15f, 0.15f, 1f);

    [Tooltip("蝶の色味（シルエット風にするなら黒のまま。カラーの絵を使うなら白にしてアルファのみ適用）")]
    [SerializeField] private Color butterflyTintColor = Color.black;

    [Tooltip("蝶の不透明度（1=不透明、0.85なら少し透けて見える）")]
    [Range(0f, 1f)]
    [SerializeField] private float butterflyAlpha = 0.85f;

    [Header("描画順")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -5;

    [Header("出現間隔（秒）")]
    [SerializeField] private float spawnIntervalMin = 3f;
    [SerializeField] private float spawnIntervalMax = 6f;

    [Header("出現ゾーン（ワールド座標・実際のシルエット画像に合わせて調整）")]
    [Tooltip("左側の木々のあるエリア（中心座標・サイズ）")]
    [SerializeField] private Vector2 leftZoneCenter = new Vector2(-4.5f, 1.5f);
    [SerializeField] private Vector2 leftZoneSize = new Vector2(2.5f, 4f);

    [Tooltip("右側の木々のあるエリア（中心座標・サイズ）")]
    [SerializeField] private Vector2 rightZoneCenter = new Vector2(4.5f, 1.5f);
    [SerializeField] private Vector2 rightZoneSize = new Vector2(2.5f, 4f);

    [Tooltip("中央下部の茂みエリア（中心座標・サイズ）")]
    [SerializeField] private Vector2 bottomCenterZoneCenter = new Vector2(0f, -2f);
    [SerializeField] private Vector2 bottomCenterZoneSize = new Vector2(3f, 1f);

    [Header("その場でひらひら動く量")]
    [SerializeField] private float driftAmplitudeMin = 0.4f;
    [SerializeField] private float driftAmplitudeMax = 0.9f;
    [SerializeField] private float driftFrequencyMin = 0.3f;
    [SerializeField] private float driftFrequencyMax = 0.8f;

    [Header("フェードアウト（一定時間後に縮小しながら消える）")]
    [Tooltip("フェードアウトを開始するまでの表示時間（秒）")]
    [SerializeField] private float visibleDuration = 5f;

    [Tooltip("フェードアウト（透明化+縮小）にかける時間（秒）")]
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Tooltip("フェードアウト完了時点でのスケール倍率")]
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
            SpawnButterfly();
        }
    }

    private void SpawnButterfly()
    {
        if (butterflyFrames == null || butterflyFrames.Length == 0 || butterflyFrames[0] == null) return;

        Vector2 center;
        Vector2 size;
        switch (Random.Range(0, 3))
        {
            case 0: center = leftZoneCenter; size = leftZoneSize; break;
            case 1: center = rightZoneCenter; size = rightZoneSize; break;
            default: center = bottomCenterZoneCenter; size = bottomCenterZoneSize; break;
        }

        float x = center.x + Random.Range(-size.x * 0.5f, size.x * 0.5f);
        float y = center.y + Random.Range(-size.y * 0.5f, size.y * 0.5f);

        GameObject go = new GameObject("ButterflyFlutter");
        go.transform.position = new Vector3(x, y, 0f);
        go.transform.localScale = butterflyScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = butterflyFrames[0];
        sr.color = new Color(butterflyTintColor.r, butterflyTintColor.g, butterflyTintColor.b, butterflyAlpha);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        ButterflyController controller = go.AddComponent<ButterflyController>();
        float ampX = Random.Range(driftAmplitudeMin, driftAmplitudeMax);
        float ampY = Random.Range(driftAmplitudeMin, driftAmplitudeMax);
        float freqX = Random.Range(driftFrequencyMin, driftFrequencyMax);
        float freqY = Random.Range(driftFrequencyMin, driftFrequencyMax);
        controller.Init(butterflyFrames, frameRate, ampX, ampY, freqX, freqY,
            visibleDuration, fadeOutDuration, minScaleAtFadeEnd);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        DrawZoneGizmo(leftZoneCenter, leftZoneSize);
        DrawZoneGizmo(rightZoneCenter, rightZoneSize);
        DrawZoneGizmo(bottomCenterZoneCenter, bottomCenterZoneSize);
    }

    private void DrawZoneGizmo(Vector2 center, Vector2 size)
    {
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0.1f));
    }
}
