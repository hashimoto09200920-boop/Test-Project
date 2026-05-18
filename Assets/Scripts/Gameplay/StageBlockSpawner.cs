using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 各StageのStart時に画面内にブロックをランダム散布し、StageCleared時に全消去する。
/// Area毎の設定（Sprite/HP/数）はAreaConfig.StageBlockConfigで管理。
/// FortressEnemyのPattern3（Scatter Blocks）と同じジッタードグリッド方式で均等配置。
/// </summary>
public class StageBlockSpawner : MonoBehaviour
{
    [Header("Block")]
    [Tooltip("散布するブロックのPrefab（Block_Scatter推奨。BlockWobble/WallHealth必須）")]
    [SerializeField] private GameObject blockPrefab;

    [Tooltip("生成するブロックの親Transform。未設定時はシーンルートに配置する。")]
    [SerializeField] private Transform blockRoot;

    [Header("Spawn Settings")]
    [Tooltip("SkillHUDの横幅（ピクセル単位）。SkillHUDが見つからない場合のフォールバック値。")]
    [SerializeField] private float skillHudPixelWidth = 280f;

    [Tooltip("プレイヤー周辺の除外半径（ワールド単位）。0で無効。")]
    [SerializeField] private float playerExcludeRadius = 2f;

    [Tooltip("Floorの上端から上方向への除外高さ（ワールド単位）")]
    [SerializeField] private float floorExcludeHeight = 2f;

    [Tooltip("1ブロックあたりの出現間隔（秒）。ドミノ演出。")]
    [SerializeField] private float spawnInterval = 0.08f;

    [Tooltip("ブロックごとに加算する回転角度（度）")]
    [SerializeField] private float rotationStep = 15f;

    [Tooltip("回転角度へ加えるランダム幅（度）")]
    [Range(0f, 180f)]
    [SerializeField] private float rotationVariance = 5f;

    [Tooltip("1ブロックあたりの配置試行最大回数")]
    [SerializeField] private int maxAttempts = 25;

    [Header("Clear Settings")]
    [Tooltip("Stageクリア時の点滅回数")]
    [SerializeField] private int blinkCount = 3;

    [Tooltip("点滅1回あたりのOFF/ON時間（秒）")]
    [SerializeField] private float blinkInterval = 0.12f;

    // =========================================================
    // ランタイム
    // =========================================================
    private readonly List<GameObject> blocks = new List<GameObject>();
    private RectTransform skillHudRect;
    private EnemySpawner enemySpawner;
    private bool isClearing = false;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================
    private void Start()
    {
        skillHudRect  = GameObject.Find("SkillHUD")?.GetComponent<RectTransform>();
        enemySpawner  = FindObjectOfType<EnemySpawner>();

        EnemySpawner.OnStageStarted += OnStageStarted;
        EnemySpawner.OnStageCleared += OnStageCleared;
    }

    private void OnDestroy()
    {
        EnemySpawner.OnStageStarted -= OnStageStarted;
        EnemySpawner.OnStageCleared -= OnStageCleared;
        DestroyBlocks();
    }

    // =========================================================
    // イベントハンドラ
    // =========================================================
    private void OnStageStarted(int stageIndex)
    {
        StartCoroutine(SpawnBlocks(stageIndex));
    }

    private void OnStageCleared(int stageIndex)
    {
        StartCoroutine(BlinkAndClear());
    }

    // =========================================================
    // スポーン
    // =========================================================
    private IEnumerator SpawnBlocks(int stageIndex)
    {
        if (blockPrefab == null) yield break;

        StageBlockConfig config = enemySpawner?.CurrentAreaConfig?.stageBlockConfig;
        if (config == null) yield break;

        int count = 0;
        if (stageIndex < config.blockCountPerStage.Length)
        {
            BlockCountRange range = config.blockCountPerStage[stageIndex];
            count = Random.Range(Mathf.Min(range.min, range.max), Mathf.Max(range.min, range.max) + 1);
        }
        if (count <= 0) yield break;

        yield return null; // Camera.main 初期化を待つ
        if (Camera.main == null) yield break;

        float halfH    = Camera.main.orthographicSize;
        float halfW    = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        // SkillHUD除外（左端）
        float xMin = GetSkillHudRightWorldX(camPos.x, halfW);
        float xMax = camPos.x + halfW;

        // Floor除外（下端）
        float yMin = camPos.y - halfH;
        FloorHealth floor = FindObjectOfType<FloorHealth>();
        if (floor != null)
        {
            Collider2D floorCol = floor.GetComponent<Collider2D>();
            float floorTopY = (floorCol != null) ? floorCol.bounds.max.y : floor.transform.position.y;
            yMin = floorTopY + floorExcludeHeight;
        }
        float yMax = camPos.y + halfH;

        // プレイヤー位置
        PixelDancerController player = FindObjectOfType<PixelDancerController>();
        Vector3 playerPos = player != null
            ? player.transform.position
            : new Vector3(float.MaxValue, float.MaxValue, 0f);

        // ジッタードグリッド（FortressEnemy Pattern3と同方式）
        float areaW = xMax - xMin;
        float areaH = yMax - yMin;
        int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(count * (areaW / areaH))));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / cols));
        float cellW = areaW / cols;
        float cellH = areaH / rows;

        int totalCells = cols * rows;
        int[] indices   = BuildShuffledIndices(totalCells);

        int placed = 0;
        for (int ci = 0; ci < totalCells && placed < count; ci++)
        {
            int col      = indices[ci] % cols;
            int row      = indices[ci] / cols;
            float xCellMin = xMin + cellW * col;
            float yCellMin = yMin + cellH * row;

            bool success = false;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float x = Random.Range(xCellMin, xCellMin + cellW);
                float y = Random.Range(yCellMin, yCellMin + cellH);
                Vector3 candidate = new Vector3(x, y, transform.position.z);

                if (playerExcludeRadius > 0f &&
                    Vector3.Distance(candidate, playerPos) < playerExcludeRadius)
                    continue;

                float variance = Random.Range(-rotationVariance * 0.5f, rotationVariance * 0.5f);
                float rotZ = rotationStep * placed + variance;
                Quaternion rot = Quaternion.Euler(0f, 0f, rotZ);

                GameObject block = Instantiate(blockPrefab, candidate, rot, blockRoot);

                // Spriteを上書き（2種ランダム選択）
                Sprite chosenSprite = PickRandomSprite(config.blockSprite, config.blockSprite2);
                if (chosenSprite != null)
                {
                    SpriteRenderer sr = block.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sprite = chosenSprite;
                }

                // HPを上書き
                WallHealth wh = block.GetComponent<WallHealth>();
                if (wh != null) wh.SetMaxHp(config.blockHp);

                blocks.Add(block);
                success = true;
                placed++;
                break;
            }

            if (success)
                yield return new WaitForSeconds(spawnInterval);
        }
    }

    // =========================================================
    // 消去
    // =========================================================
    private IEnumerator BlinkAndClear()
    {
        if (isClearing) yield break;
        isClearing = true;

        foreach (var b in blocks)
        {
            if (b == null) continue;
            WallHealth wh = b.GetComponent<WallHealth>();
            if (wh != null)
                wh.StartBlinkAndDestroy(blinkCount, blinkInterval);
            else
                Destroy(b);
        }

        yield return new WaitForSeconds(blinkCount * blinkInterval * 2f);
        blocks.Clear();
        isClearing = false;
    }

    private void DestroyBlocks()
    {
        foreach (var b in blocks)
            if (b != null) Destroy(b);
        blocks.Clear();
    }

    // =========================================================
    // ヘルパー
    // =========================================================
    private float GetSkillHudRightWorldX(float camX, float halfW)
    {
        if (skillHudRect != null)
        {
            Canvas rootCanvas = skillHudRect.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
                Vector3[] canvasCorners = new Vector3[4];
                Vector3[] hudCorners    = new Vector3[4];
                canvasRT.GetWorldCorners(canvasCorners);
                skillHudRect.GetWorldCorners(hudCorners);

                float canvasLeft  = canvasCorners[0].x;
                float canvasRight = canvasCorners[2].x;
                float canvasWidth = canvasRight - canvasLeft;

                if (canvasWidth > 0.001f)
                {
                    float hudRight = Mathf.Max(hudCorners[2].x, hudCorners[3].x);
                    float t = (hudRight - canvasLeft) / canvasWidth;
                    return camX - halfW + t * (halfW * 2f);
                }
            }
        }
        if (skillHudPixelWidth > 0f && Screen.width > 0)
            return camX - halfW + skillHudPixelWidth * ((halfW * 2f) / Screen.width);
        return camX - halfW;
    }

    private static Sprite PickRandomSprite(Sprite s1, Sprite s2)
    {
        if (s1 == null && s2 == null) return null;
        if (s1 == null) return s2;
        if (s2 == null) return s1;
        return Random.value < 0.5f ? s1 : s2;
    }

    private static int[] BuildShuffledIndices(int count)
    {
        int[] indices = new int[count];
        for (int i = 0; i < count; i++) indices[i] = i;
        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
        }
        return indices;
    }
}
