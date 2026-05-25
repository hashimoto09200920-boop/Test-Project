using System.Collections.Generic;
using Game.Skills;
using UnityEngine;

public class BlockItemManager : MonoBehaviour
{
    public static BlockItemManager Instance { get; private set; }

    [Header("Item Prefab")]
    [Tooltip("BlockItemコンポーネントを持つPrefab（Gold/Life共通）")]
    [SerializeField] private BlockItem itemPrefab;

    [Header("Icons")]
    [Tooltip("Goldアイテムのアイコンスプライト")]
    [SerializeField] private Sprite goldIcon;
    [Tooltip("Lifeアイテムのアイコンスプライト（B3スキルアイコン推奨）")]
    [SerializeField] private Sprite lifeIcon;

    [Header("Popup")]
    [Tooltip("収集時のポップアップテキストPrefab")]
    [SerializeField] private BlockItemPopupText popupTextPrefab;
    [Range(0.01f, 1f)]
    [Tooltip("Goldポップアップのワールドスケール")]
    [SerializeField] private float popupWorldScaleGold = 0.05f;
    [Range(0.01f, 1f)]
    [Tooltip("Lifeポップアップのワールドスケール")]
    [SerializeField] private float popupWorldScaleLife = 0.05f;

    private readonly List<BlockItem> activeItems = new List<BlockItem>();
    private EnemySpawner enemySpawner;
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        enemySpawner = FindObjectOfType<EnemySpawner>();
    }

    private void OnEnable()
    {
        WallHealth.OnAnyBlockBroken += OnBlockBroken;
        EnemySpawner.OnStageStarted += OnStageStarted;
    }

    private void OnDisable()
    {
        WallHealth.OnAnyBlockBroken -= OnBlockBroken;
        EnemySpawner.OnStageStarted -= OnStageStarted;
    }

    // =========================================================
    // イベントハンドラ
    // =========================================================

    private void OnBlockBroken(Vector3 pos)
    {
        StageBlockConfig cfg = enemySpawner?.CurrentAreaConfig?.stageBlockConfig;
        if (cfg == null || itemPrefab == null) return;

        bool isGold = Random.Range(0f, 100f) < cfg.goldDropRate;

        int amount;
        Sprite icon;
        AudioClip se;
        BlockItem.ItemType type;

        if (isGold)
        {
            type = BlockItem.ItemType.Gold;
            amount = Random.Range(cfg.goldDropMin, cfg.goldDropMax + 1);
            icon = goldIcon;
            se = cfg.itemCollectSEGold;
        }
        else
        {
            type = BlockItem.ItemType.Life;
            amount = cfg.lifeDropAmount;
            icon = lifeIcon;
            se = cfg.itemCollectSELife;
        }

        BlockItem item = Instantiate(itemPrefab, pos, Quaternion.identity);
        item.itemType = type;
        item.amount = amount;
        item.icon = icon;
        item.collectSE = se;
        item.baseScale = isGold ? cfg.goldItemSize : cfg.lifeItemSize;

        // アイテムのWorld上のビジュアルを設定
        SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = icon;

        activeItems.Add(item);
    }

    private void OnStageStarted(int stageIndex)
    {
        ClearAllItems();
    }

    // =========================================================
    // アイテム管理
    // =========================================================

    private void ClearAllItems()
    {
        foreach (BlockItem item in activeItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        activeItems.Clear();
    }

    /// <summary>ライン収集でアイテムが収集を開始した時に呼ばれる（リストから除外）</summary>
    public void OnItemCollectionStarted(BlockItem item)
    {
        activeItems.Remove(item);
    }

    /// <summary>円成立時に円内のアイテムを一括収集（StrokeManagerから呼ばれる）</summary>
    public void CollectItemsInCircle(Bounds circleBounds, Vector3 circleCenter)
    {
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            BlockItem item = activeItems[i];
            if (item == null) { activeItems.RemoveAt(i); continue; }
            if (item.IsCollected) { activeItems.RemoveAt(i); continue; }
            if (circleBounds.Contains(item.transform.position))
            {
                activeItems.RemoveAt(i);
                item.CollectFromCircle(circleCenter);
            }
        }
    }

    // =========================================================
    // 効果適用
    // =========================================================

    /// <summary>BlockItemの収集完了時に呼ばれる（BlockItemから呼ばれる）</summary>
    public void ApplyEffect(BlockItem.ItemType type, int amount, Vector3 pos, Sprite icon, AudioClip se)
    {
        // SE再生
        if (se != null && audioSource != null)
        {
            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
            audioSource.PlayOneShot(se, vol);
        }

        // 効果適用
        if (type == BlockItem.ItemType.Gold)
        {
            GoldManager.Instance?.AddSessionGold(amount);
        }
        else
        {
            PixelDancerController dancer = FindFirstObjectByType<PixelDancerController>();
            dancer?.Heal(amount);
        }

        // ポップアップ表示
        if (popupTextPrefab != null)
        {
            BlockItemPopupText popup = Instantiate(popupTextPrefab, pos, Quaternion.identity);
            float popupScale = type == BlockItem.ItemType.Gold ? popupWorldScaleGold : popupWorldScaleLife;
            popup.transform.localScale = Vector3.one * popupScale;
            popup.Show(type, amount, icon);
        }
    }
}
