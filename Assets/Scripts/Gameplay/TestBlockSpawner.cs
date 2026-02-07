using UnityEngine;

/// <summary>
/// テスト用：敵の周囲に1ブロック単位でブロックを配置し、破壊回数をテストする
/// </summary>
public class TestBlockSpawner : MonoBehaviour
{
    [Header("Prefab / Parent")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private Transform blockRoot;

    [Header("Target Enemy")]
    [Tooltip("このTransformの周囲にブロックを配置する（未設定なら自分の位置）")]
    [SerializeField] private Transform targetEnemy;

    [Header("Pattern")]
    [SerializeField] private BlockPattern pattern = BlockPattern.AroundEnemy;

    [Header("Spacing")]
    [SerializeField] private float blockSpacing = 1.1f;

    [Header("Offset")]
    [Tooltip("敵からの距離（ブロック配置の半径）")]
    [SerializeField] private float distanceFromEnemy = 2.0f;

    [Header("Block Appearance")]
    [Tooltip("ブロックのスケール（サイズ）")]
    [SerializeField] private Vector3 blockScale = new Vector3(0.5f, 0.5f, 1f);

    [Header("Debug")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float spawnDelay = 1.0f; // 敵生成を待つための遅延
    [SerializeField] private bool showDebugLog = true;

    public enum BlockPattern
    {
        AroundEnemy,        // 敵の周囲8方向
        CircleAround,       // 敵の周囲に円形配置
        Custom              // カスタム位置配列
    }

    private void Start()
    {
        if (showDebugLog)
        {
            Debug.Log($"[TestBlockSpawner] Start - spawnOnStart={spawnOnStart}, spawnDelay={spawnDelay}");
        }

        if (spawnOnStart)
        {
            // 遅延実行で敵の生成を待つ
            Invoke(nameof(DelayedSpawn), spawnDelay);
        }
    }

    private void DelayedSpawn()
    {
        // Target Enemyが未設定なら自動検索
        if (targetEnemy == null)
        {
            SetTargetToFirstEnemy();
        }

        Spawn();
    }

    [ContextMenu("Spawn Test Blocks")]
    public void Spawn()
    {
        if (showDebugLog)
        {
            Debug.Log($"[TestBlockSpawner] Spawn called - blockPrefab={blockPrefab != null}, blockRoot={blockRoot != null}, targetEnemy={targetEnemy != null}");
        }

        if (blockPrefab == null)
        {
            Debug.LogError("[TestBlockSpawner] blockPrefab is not set. Please assign a block prefab in the Inspector.");
            return;
        }

        if (blockRoot == null)
        {
            Debug.LogError("[TestBlockSpawner] blockRoot is not set. Please assign a block root transform in the Inspector.");
            return;
        }

        // 既存ブロックを削除
        ClearChildren(blockRoot);

        // ターゲット位置を決定
        Vector3 center = targetEnemy != null ? targetEnemy.position : transform.position;

        if (showDebugLog)
        {
            Debug.Log($"[TestBlockSpawner] Spawning blocks at center: {center}, pattern: {pattern}");
        }

        switch (pattern)
        {
            case BlockPattern.AroundEnemy:
                SpawnAroundEnemy(center);
                break;
            case BlockPattern.CircleAround:
                SpawnCircleAround(center);
                break;
        }
    }

    /// <summary>
    /// 敵の周囲8方向（上下左右＋斜め）にブロックを配置
    /// </summary>
    private void SpawnAroundEnemy(Vector3 center)
    {
        // 8方向のオフセット
        Vector2[] offsets = new Vector2[]
        {
            new Vector2(0, 1),      // 上
            new Vector2(1, 1),      // 右上
            new Vector2(1, 0),      // 右
            new Vector2(1, -1),     // 右下
            new Vector2(0, -1),     // 下
            new Vector2(-1, -1),    // 左下
            new Vector2(-1, 0),     // 左
            new Vector2(-1, 1)      // 左上
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 pos = center + (Vector3)(offsets[i].normalized * distanceFromEnemy);
            CreateBlock(pos, $"TestBlock_{i:D2}");
        }

        Debug.Log($"[TestBlockSpawner] Spawned {offsets.Length} blocks around enemy at {center}");
    }

    /// <summary>
    /// 敵の周囲に円形でブロックを配置（12個）
    /// </summary>
    private void SpawnCircleAround(Vector3 center)
    {
        int blockCount = 12;
        float angleStep = 360f / blockCount;

        for (int i = 0; i < blockCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * distanceFromEnemy;
            float y = Mathf.Sin(angle) * distanceFromEnemy;

            Vector3 pos = center + new Vector3(x, y, 0f);
            CreateBlock(pos, $"TestBlock_Circle_{i:D2}");
        }

        Debug.Log($"[TestBlockSpawner] Spawned {blockCount} blocks in circle around enemy at {center}");
    }

    /// <summary>
    /// ブロックを作成
    /// </summary>
    private void CreateBlock(Vector3 position, string blockName)
    {
        GameObject block = Instantiate(blockPrefab, position, Quaternion.identity, blockRoot);
        block.name = blockName;

        // スケールを設定
        block.transform.localScale = blockScale;

        if (showDebugLog)
        {
            Debug.Log($"[TestBlockSpawner] Created block: {blockName} at {position}");
        }

        // WallHealthコンポーネントがあればデバッグログを有効化
        WallHealth wallHealth = block.GetComponent<WallHealth>();
        if (wallHealth != null)
        {
            // リフレクションでlogDebugフィールドを有効化（privateなので）
            var field = typeof(WallHealth).GetField("logDebug",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(wallHealth, true);
            }
        }
        else if (showDebugLog)
        {
            Debug.LogWarning($"[TestBlockSpawner] Block {blockName} has no WallHealth component!");
        }
    }

    /// <summary>
    /// テスト用：敵の位置を手動設定
    /// </summary>
    [ContextMenu("Set Target to First Enemy")]
    public void SetTargetToFirstEnemy()
    {
        if (showDebugLog)
        {
            Debug.Log("[TestBlockSpawner] Searching for enemy in scene...");
        }

        EnemyStats enemy = FindFirstObjectByType<EnemyStats>();
        if (enemy != null)
        {
            targetEnemy = enemy.transform;
            Debug.Log($"[TestBlockSpawner] Target set to: {targetEnemy.name} at position {targetEnemy.position}");
        }
        else
        {
            Debug.LogWarning("[TestBlockSpawner] No enemy found in scene. Make sure an enemy with EnemyStats component exists.");
        }
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (targetEnemy == null && Application.isPlaying == false)
            return;

        Vector3 center = targetEnemy != null ? targetEnemy.position : transform.position;

        // 配置範囲を可視化
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, distanceFromEnemy);

        // パターンのプレビュー
        if (pattern == BlockPattern.AroundEnemy)
        {
            Vector2[] offsets = new Vector2[]
            {
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(1, -1),
                new Vector2(0, -1), new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1)
            };

            Gizmos.color = Color.cyan;
            foreach (var offset in offsets)
            {
                Vector3 pos = center + (Vector3)(offset.normalized * distanceFromEnemy);
                Gizmos.DrawWireCube(pos, Vector3.one * 0.5f);
            }
        }
    }
}
