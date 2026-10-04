using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
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
    [SerializeField] private float tileWidth = 20f;
    [SerializeField] private float tileHeight = 20f;
    [Tooltip("タイル形状のSprite（アイコンと同じSprite Atlasに含まれるアセット）。未設定の場合はコード生成した図形にフォールバックする")]
    [SerializeField] private Sprite tileShapeSpriteAsset;

    [Header("Color Settings")]
    [SerializeField] private Color unacquiredTileColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color greyedOutColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Header("Blink Settings")]
    [SerializeField] private int blinkCount = 2;
    [SerializeField] private float blinkInterval = 0.15f;

    [Header("Debug (ドリンク画面チカチカ切り分け用・一時的)")]
    [Tooltip("★調査用：ONにするとスキルアイコン画像を表示しない")]
    [SerializeField] private bool debugHideIcon = false;
    [Tooltip("★調査用：ONにするとレベルタイルを表示しない")]
    [SerializeField] private bool debugHideTiles = false;

    private SkillDefinition skillData;
    private int currentLevel;
    private int gemLevel;
    private int shopLevel;
    private int maxLevel;
    private List<Image> tiles = new List<Image>();
    private SkillTooltip tooltip;
    private Color cardTileColor;
    private Color gemTileColor;
    private Color shopTileColor;

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
    public void Initialize(SkillDefinition skill, int level, int gemLevelCount, int shopLevelCount, Color cardColor, Color gemColor, Color shopColor, SkillTooltip tooltipRef, int defaultMaxTiles = 5)
    {
        skillData = skill;
        currentLevel = level;
        gemLevel = gemLevelCount;
        shopLevel = shopLevelCount;
        maxLevel = skill.maxAcquisitionCount;
        cardTileColor = cardColor;
        gemTileColor = gemColor;
        shopTileColor = shopColor;
        tooltip = tooltipRef;

        // defaultMaxTilesを設定
        maxTiles = defaultMaxTiles;

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

        // アイコン背景は不要（非表示）
        if (iconBackground != null)
        {
            iconBackground.enabled = false;
        }

        // アイコン画像（レベル0でも通常表示）
        if (iconImage != null)
        {
            // ★調査用：原因特定でき次第削除
            if (debugHideIcon)
            {
                iconImage.enabled = false;
            }
            // SkillDefinitionのiconが設定されている場合は優先して使用
            else if (skillData.icon != null)
            {
                iconImage.sprite = skillData.icon;
                iconImage.color = Color.white; // レベル0でも通常表示
                iconImage.enabled = true;
            }
            else if (iconImage.sprite != null)
            {
                // SkillDefinitionにiconが無くても、既にspriteが設定されていれば保持（Play前Inspector調整対応）
                iconImage.color = Color.white; // レベル0でも通常表示
                iconImage.enabled = true;
            }
            else
            {
                // スキルアイコンが完全に未設定の場合のみ非表示
                iconImage.enabled = false;
            }
        }

        // ★調査用：原因特定でき次第削除
        if (progressTilesContainer != null)
            progressTilesContainer.gameObject.SetActive(!debugHideTiles);

        // プログレスタイル更新
        UpdateProgressTiles();
    }

    /// <summary>
    /// プログレスタイルを作成
    /// </summary>
    private void CreateProgressTiles()
    {
        if (progressTilesContainer == null) return;

        // HorizontalLayoutGroupのspacingをtileSpacingに適用（Play前Inspector調整対応）
        HorizontalLayoutGroup layoutGroup = progressTilesContainer.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.spacing = tileSpacing;
        }

        // 既存のタイルをクリア
        // ★平行四辺形タイルは全カード共通の1枚のSpriteを使い回すため（GetOrCreateSharedParallelogramSprite）、
        //   ここでSprite/Textureを破棄する必要はない（GameObjectのみ破棄すればよい）。
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
        rect.sizeDelta = new Vector2(tileWidth, tileHeight);

        Image image = tileObj.AddComponent<Image>();

        // 平行四辺形スプライト生成（全タイル共通の1枚を使い回す。タイルごとに個別生成すると
        // 描画バッチが大量に分裂し、OPPO Reno11A実機でチラつきが発生することが確認されたため）
        image.sprite = GetSharedTileSprite();

        // LayoutElementを追加してサイズを強制
        LayoutElement layoutElement = tileObj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = tileWidth;
        layoutElement.preferredHeight = tileHeight;

        return tileObj;
    }

    /// <summary>全SkillHUDCardUIインスタンスで共有する平行四辺形Sprite（OPPO Reno11Aチカチカ対策）</summary>
    private static Sprite s_sharedParallelogramSprite;

    /// <summary>
    /// タイル形状のSpriteを返す。アイコンと同じSprite Atlasに含まれるアセット(tileShapeSpriteAsset)が
    /// 設定されていればそれを使う（アイコンとタイルの描画テクスチャが交互に切り替わることによる
    /// 描画バッチ分裂を防ぐため）。未設定の場合のみ、コード生成した共有Spriteにフォールバックする。
    /// </summary>
    private Sprite GetSharedTileSprite()
    {
        if (tileShapeSpriteAsset != null)
            return tileShapeSpriteAsset;

        if (s_sharedParallelogramSprite == null)
            s_sharedParallelogramSprite = CreateParallelogramSprite();
        return s_sharedParallelogramSprite;
    }

    /// <summary>
    /// 平行四辺形スプライトを生成
    /// </summary>
    private static Sprite CreateParallelogramSprite()
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
    /// タイル並び順：ジェム由来（赤）→ カード取得（シアン）→ 未取得（暗色）
    /// </summary>
    private void UpdateProgressTiles()
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] == null) continue;

            if (i < gemLevel)
            {
                // ジェム由来（ネオンオレンジ）
                tiles[i].color = gemTileColor;
            }
            else if (i < gemLevel + shopLevel)
            {
                // ショップ由来（ネオンパープル）
                tiles[i].color = shopTileColor;
            }
            else if (i < gemLevel + shopLevel + currentLevel)
            {
                // カード取得（ネオンシアン）
                tiles[i].color = cardTileColor;
            }
            else
            {
                // 未取得（暗色）
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
    /// カード取得時のレベル更新。gem/shopオフセットを考慮して新タイルをブリンクさせる。
    /// </summary>
    public void UpdateCardLevelWithBlink(int newCardLevel, int newGemLevel, int newShopLevel)
    {
        int oldCardLevel = currentLevel;
        currentLevel = newCardLevel;
        gemLevel     = newGemLevel;
        shopLevel    = newShopLevel;
        UpdateDisplay();

        // gem+shop分オフセットした位置の新タイルを収集
        int offset = gemLevel + shopLevel;
        List<Image> newTiles = new List<Image>();
        for (int i = oldCardLevel; i < newCardLevel; i++)
        {
            int tileIdx = offset + i;
            if (tileIdx < tiles.Count && tiles[tileIdx] != null)
                newTiles.Add(tiles[tileIdx]);
        }

        if (newTiles.Count > 0)
            StartCoroutine(BlinkNewTiles(newTiles));
    }

    /// <summary>
    /// レベルを更新し、新たに点灯したタイルを点滅させる
    /// </summary>
    public void UpdateLevelWithBlink(int newLevel)
    {
        int oldLevel = currentLevel;
        currentLevel = newLevel;
        UpdateDisplay();

        // 新しく取得したタイルを収集
        List<Image> newTiles = new List<Image>();
        for (int i = oldLevel; i < newLevel && i < tiles.Count; i++)
        {
            if (tiles[i] != null)
                newTiles.Add(tiles[i]);
        }

        if (newTiles.Count > 0)
        {
            StartCoroutine(BlinkNewTiles(newTiles));
        }
    }

    private IEnumerator BlinkNewTiles(List<Image> newTiles)
    {
        // カード取得によるブリンクなので cardTileColor を使用
        for (int i = 0; i < blinkCount; i++)
        {
            foreach (var tile in newTiles)
                if (tile != null) tile.color = unacquiredTileColor;
            yield return new WaitForSecondsRealtime(blinkInterval);

            foreach (var tile in newTiles)
                if (tile != null) tile.color = cardTileColor;
            yield return new WaitForSecondsRealtime(blinkInterval);
        }
    }

    /// <summary>現在のショップレベルを取得（GemSkillPreviewHUD の点滅判定用）</summary>
    public int ShopLevel => shopLevel;

    /// <summary>このカードのRectTransform（スキル選択の吸収演出で着地先として使う）</summary>
    public RectTransform GetRectTransform() => (RectTransform)transform;

    [Header("Absorb Pop (スキル選択カード吸収時の着地演出)")]
    [SerializeField] private float absorbPopScale = 1.35f;
    [SerializeField] private float absorbPopDuration = 0.25f;
    [SerializeField] private Color absorbFlashColor = Color.white;

    private Coroutine absorbPopCoroutine;

    /// <summary>吸収演出のカードが到達した瞬間に呼ぶ。アイコンが一瞬拡大+フラッシュして「受け取った」感を出す</summary>
    public void PlayAbsorbPop()
    {
        if (absorbPopCoroutine != null) StopCoroutine(absorbPopCoroutine);
        absorbPopCoroutine = StartCoroutine(AbsorbPopCoroutine());
    }

    private IEnumerator AbsorbPopCoroutine()
    {
        Vector3 baseScale = transform.localScale;
        Color iconBase = iconImage != null ? iconImage.color : Color.white;
        float elapsed = 0f;

        while (elapsed < absorbPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / absorbPopDuration);
            // 前半で拡大+フラッシュ、後半で元に戻す
            float swing = t < 0.5f ? (t / 0.5f) : (1f - (t - 0.5f) / 0.5f);
            transform.localScale = Vector3.Lerp(baseScale, baseScale * absorbPopScale, swing);
            if (iconImage != null) iconImage.color = Color.Lerp(iconBase, absorbFlashColor, swing);
            yield return null;
        }

        transform.localScale = baseScale;
        if (iconImage != null) iconImage.color = iconBase;
        absorbPopCoroutine = null;
    }

    /// <summary>
    /// 指定タイル範囲を指定設定で点滅させる（ショップ購入時のドリンク効果表示用）
    /// </summary>
    public void BlinkTileRange(int fromIdx, int toIdx, int count, float interval)
    {
        var tilesToBlink = new List<Image>();
        for (int i = fromIdx; i <= toIdx; i++)
            if (i >= 0 && i < tiles.Count && tiles[i] != null)
                tilesToBlink.Add(tiles[i]);
        if (tilesToBlink.Count > 0)
            StartCoroutine(BlinkTilesWithColor(tilesToBlink, shopTileColor, count, interval));
    }

    private IEnumerator BlinkTilesWithColor(List<Image> tilesToBlink, Color litColor, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            foreach (var tile in tilesToBlink)
                if (tile != null) tile.color = unacquiredTileColor;
            yield return new WaitForSecondsRealtime(interval);
            foreach (var tile in tilesToBlink)
                if (tile != null) tile.color = litColor;
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    /// <summary>
    /// スキルIDを取得
    /// </summary>
    public string GetSkillID()
    {
        return skillData != null ? skillData.name : "";
    }

    /// <summary>
    /// maxTiles値を取得（Inspector設定値を維持するため）
    /// </summary>
    public int GetMaxTiles()
    {
        return maxTiles;
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
