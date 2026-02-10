using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using Game.Skills;

/// <summary>
/// スキルHUD用の個別カードUI（画面左側の常時表示用）
/// </summary>
public class SkillHUDCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image iconBackground;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private Transform progressTilesContainer;

    [Header("Tile Settings")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private int maxTiles = 5;
    [SerializeField] private float tileSpacing = 5f;

    [Header("Color Settings")]
    [SerializeField] private Color unacquiredTileColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color greyedOutColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    private SkillDefinition skillData;
    private int currentLevel;
    private int maxLevel;
    private List<Image> tiles = new List<Image>();
    private SkillTooltip tooltip;
    private Color categoryColor;

    private void Awake()
    {
        // 子オブジェクトから自動的にコンポーネントを割り当て
        AutoAssignComponents();
    }

    /// <summary>
    /// 子オブジェクトから自動的にコンポーネントを割り当て
    /// </summary>
    private void AutoAssignComponents()
    {
        if (iconBackground == null)
        {
            Transform t = transform.Find("IconBackground");
            if (t != null) iconBackground = t.GetComponent<Image>();
        }

        if (iconImage == null)
        {
            Transform bgTransform = transform.Find("IconBackground");
            if (bgTransform != null)
            {
                Transform t = bgTransform.Find("IconImage");
                if (t != null) iconImage = t.GetComponent<Image>();
            }
        }

        // スキル名テキストは不要（アイコンとタイルのみ表示）

        if (progressTilesContainer == null)
        {
            Transform t = transform.Find("ProgressTiles");
            if (t != null) progressTilesContainer = t;
        }
    }

    /// <summary>
    /// スキルカードを初期化
    /// </summary>
    public void Initialize(SkillDefinition skill, int level, Color catColor, SkillTooltip tooltipRef)
    {
        skillData = skill;
        currentLevel = level;
        maxLevel = skill.maxAcquisitionCount;
        categoryColor = catColor;
        tooltip = tooltipRef;

        UpdateDisplay();
        CreateProgressTiles();
    }

    /// <summary>
    /// 表示を更新
    /// </summary>
    public void UpdateDisplay()
    {
        if (skillData == null) return;

        // スキル名テキストは不要（削除済み）

        // アイコン背景（カテゴリカラー）
        if (iconBackground != null)
        {
            iconBackground.color = currentLevel > 0 ? categoryColor : greyedOutColor;
        }

        // アイコン画像
        if (iconImage != null)
        {
            if (skillData.icon != null)
            {
                iconImage.sprite = skillData.icon;
                iconImage.color = currentLevel > 0 ? Color.white : greyedOutColor;
                iconImage.enabled = true;
            }
            else
            {
                // スキルアイコンが未設定の場合は非表示
                iconImage.enabled = false;
            }
        }

        // プログレスタイル更新
        UpdateProgressTiles();
    }

    /// <summary>
    /// プログレスタイルを作成
    /// </summary>
    private void CreateProgressTiles()
    {
        if (progressTilesContainer == null) return;

        // 既存のタイルをクリア
        foreach (var tile in tiles)
        {
            if (tile != null) Destroy(tile.gameObject);
        }
        tiles.Clear();

        // maxLevelが0（無制限）の場合はデフォルトで5タイル表示
        int tileCount = maxLevel > 0 ? maxLevel : maxTiles;

        for (int i = 0; i < tileCount; i++)
        {
            GameObject tileObj;

            if (tilePrefab != null)
            {
                tileObj = Instantiate(tilePrefab, progressTilesContainer);
            }
            else
            {
                // デフォルトタイル生成（平行四辺形）
                tileObj = CreateParallelogramTile();
                tileObj.transform.SetParent(progressTilesContainer, false);
            }

            Image tileImage = tileObj.GetComponent<Image>();
            if (tileImage == null)
            {
                tileImage = tileObj.AddComponent<Image>();
            }

            tiles.Add(tileImage);
        }

        UpdateProgressTiles();
    }

    /// <summary>
    /// 平行四辺形タイルを生成
    /// </summary>
    private GameObject CreateParallelogramTile()
    {
        GameObject tileObj = new GameObject("Tile");

        RectTransform rect = tileObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(20f, 20f);

        Image image = tileObj.AddComponent<Image>();

        // 平行四辺形スプライト生成
        Sprite parallelogramSprite = CreateParallelogramSprite();
        image.sprite = parallelogramSprite;

        // LayoutElementを追加してサイズを強制
        LayoutElement layoutElement = tileObj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 20f;
        layoutElement.preferredHeight = 20f;

        return tileObj;
    }

    /// <summary>
    /// 平行四辺形スプライトを生成
    /// </summary>
    private Sprite CreateParallelogramSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        // 平行四辺形の形状（斜めカット）
        float skew = 0.3f; // 傾き

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedY = y / (float)size;
                float skewOffset = normalizedY * skew * size;

                float left = skewOffset;
                float right = size - (size * skew - skewOffset);

                Color color = (x >= left && x < right) ? Color.white : Color.clear;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );

        return sprite;
    }

    /// <summary>
    /// プログレスタイルの色を更新
    /// </summary>
    private void UpdateProgressTiles()
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] == null) continue;

            if (i < currentLevel)
            {
                // 取得済み（カテゴリカラー）
                tiles[i].color = categoryColor;
            }
            else
            {
                // 未取得（暗い色）
                tiles[i].color = unacquiredTileColor;
            }
        }
    }

    /// <summary>
    /// レベルを更新
    /// </summary>
    public void UpdateLevel(int newLevel)
    {
        currentLevel = newLevel;
        UpdateDisplay();
    }

    /// <summary>
    /// スキルIDを取得
    /// </summary>
    public string GetSkillID()
    {
        return skillData != null ? skillData.name : "";
    }

    // ホバー時にツールチップ表示
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip != null && skillData != null)
        {
            tooltip.Show(skillData, currentLevel, maxLevel);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null)
        {
            tooltip.Hide();
        }
    }
}
