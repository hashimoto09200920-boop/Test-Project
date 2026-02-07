using UnityEngine;

public class BlockSpawner : MonoBehaviour
{
    [Header("Prefab / Parent")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private Transform blockRoot;

    [Header("Grid")]
    [SerializeField] private int columns = 10;
    [SerializeField] private int rows = 5;
    [SerializeField] private float spacingX = 1.1f;
    [SerializeField] private float spacingY = 0.6f;

    [Header("Start Position (World)")]
    [SerializeField] private Vector2 startPosition = new Vector2(-5.0f, 3.5f);

    private void Start()
    {
        Spawn();
    }

    [ContextMenu("Spawn")]
    public void Spawn()
    {
        // 必須チェック（設定漏れを即発見するため）
        if (blockPrefab == null)
        {
            Debug.LogError("[BlockSpawner] blockPrefab is not set.");
            return;
        }
        if (blockRoot == null)
        {
            Debug.LogError("[BlockSpawner] blockRoot is not set.");
            return;
        }

        // 既存ブロックがあれば消してから生成（再生し直しやSpawn再実行に強い）
        ClearChildren(blockRoot);

        // グリッド生成
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Vector3 pos = new Vector3(
                    startPosition.x + (x * spacingX),
                    startPosition.y - (y * spacingY),
                    0f
                );

                GameObject block = Instantiate(blockPrefab, pos, Quaternion.identity, blockRoot);
                block.name = $"Block_{y:D2}_{x:D2}";
            }
        }
    }

    private void ClearChildren(Transform parent)
    {
        // 子を安全に全削除
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }
}
