using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Progress;
using Game.Gems;
using Game.Skills;

/// <summary>
/// AreaSelectシーン用のジェム由来スキルプレビューHUD
/// SkillManagerを使わず ProgressManager + GemManager から直接計算して表示する。
/// GemManagementUI から Show() / Hide() / Refresh() を呼ぶ。
/// [ContextMenu("Setup Hierarchy")] で Hierarchy を自動生成してから使う。
/// </summary>
public class GemSkillPreviewHUD : MonoBehaviour
{
    [Header("Grid Containers")]
    [SerializeField] private Transform categoryAGrid;
    [SerializeField] private Transform categoryBGrid;
    [SerializeField] private Transform categoryCGrid;

    [Header("Category Headers")]
    [SerializeField] private TextMeshProUGUI categoryAHeader;
    [SerializeField] private TextMeshProUGUI categoryBHeader;
    [SerializeField] private TextMeshProUGUI categoryCHeader;

    [Header("Tile Settings")]
    [SerializeField] private int defaultMaxTiles = 5;

    [Header("Category Colors (HDR) - ヘッダー表示用")]
    [SerializeField] private Color categoryAColor = new Color(0f, 2.5f, 2.0f, 1f);
    [SerializeField] private Color categoryBColor = new Color(2.5f, 0f, 1.5f, 1f);
    [SerializeField] private Color categoryCColor = new Color(2.5f, 0.1f, 0.5f, 1f);

    [Header("Tile Colors (HDR)")]
    [SerializeField] private Color cardTileColor = new Color(0f, 2.5f, 2.0f, 1f);   // カード取得：ネオンシアン（AreaSelectでは未使用）
    [SerializeField] private Color gemTileColor  = new Color(2.5f, 0.7f, 0f, 1f);   // ジェム由来：ネオンオレンジ
    [SerializeField] private Color shopTileColor = new Color(1.5f, 0f, 2.5f, 1f);   // ショップ由来：ネオンパープル

    [Header("Tooltip（AreaSelectシーンにSkillTooltipがあれば自動取得）")]
    [SerializeField] private SkillTooltip tooltip;

    private Dictionary<string, SkillHUDCardUI> skillCards = new Dictionary<string, SkillHUDCardUI>();
    private List<SkillDefinition> allSkills;
    private bool isInitialized = false;

    private void Awake()
    {
        // Inspector保存値を上書きしてタイル色を確実に最新値に設定
        gemTileColor  = new Color(2.5f, 0.7f, 0f, 1f);   // ネオンオレンジ
        shopTileColor = new Color(1.5f, 0f, 2.5f, 1f);   // ネオンパープル
    }

    private void Start()
    {
        if (isInitialized) return; // Show() で先に初期化済みの場合はスキップ
        if (!ValidateAndAssignReferences()) return;
        LoadAllSkills();
        InitializeHUD();
        isInitialized = true;
        Refresh();
    }

    // ================== Public API ==================

    /// <summary>HUDを表示してジェム効果を反映する</summary>
    public void Show()
    {
        gameObject.SetActive(true);

        // Start() は SetActive(true) の次フレームに実行されるため、
        // 初回 Show 時（Start() 未実行）は手動で初期化する
        if (!isInitialized)
        {
            if (ValidateAndAssignReferences())
            {
                LoadAllSkills();
                InitializeHUD();
                isInitialized = true;
            }
        }

        Refresh();
    }

    /// <summary>HUDを非表示にする</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 装備中ジェムからスキルレベルを再計算して全カードを更新する。
    /// GemManagementUI で装備変更・売却した後に呼ぶ。
    /// </summary>
    public void Refresh()
    {
        if (skillCards.Count == 0) return;

        var gemCounts  = CalcGemSkillCounts();
        var shopBoosts = Game.Shop.DrinkSession.ActiveBoosts;

        foreach (var kvp in skillCards)
        {
            var skill = allSkills?.FirstOrDefault(s => s.name == kvp.Key);
            if (skill == null) continue;

            int gemLvl  = gemCounts.TryGetValue(kvp.Key, out int gc) ? gc : 0;
            int shopLvl = shopBoosts.TryGetValue(kvp.Key, out int sc) ? sc : 0;
            kvp.Value.Initialize(skill, 0, gemLvl, shopLvl, cardTileColor, gemTileColor, shopTileColor, tooltip, kvp.Value.GetMaxTiles());
        }
    }

    // ================== Private Helpers ==================

    /// <summary>
    /// 装備中ジェムを走査し「スキル名 → ジェム由来取得回数」を返す
    /// </summary>
    private Dictionary<string, int> CalcGemSkillCounts()
    {
        var result = new Dictionary<string, int>();
        var data = ProgressManager.Instance?.Data;
        if (data == null) return result;

        int usedSlots = 0;
        foreach (int idx in data.equippedGemIndices)
        {
            if (idx < 0 || idx >= data.gemInventory.Count) continue;

            var gemInst = data.gemInventory[idx];
            var gemDef  = GemManager.Instance?.LoadGemDefinition(gemInst);
            if (gemDef == null) continue;

            // スロット上限（GemManager.ApplyEquippedGems と同じロジック）
            if (usedSlots + gemDef.requiredSlots > data.slotLevel) continue;
            usedSlots += gemDef.requiredSlots;

            AddCount(result, gemDef.baseSkill?.name);
            AddCount(result, GemManager.Instance?.LoadBonusSkill1(gemInst)?.name);
            AddCount(result, GemManager.Instance?.LoadBonusSkill2(gemInst)?.name);
        }
        return result;
    }

    private static void AddCount(Dictionary<string, int> dict, string skillName)
    {
        if (string.IsNullOrEmpty(skillName)) return;
        dict.TryGetValue(skillName, out int cur);
        dict[skillName] = cur + 1;
    }

    private void LoadAllSkills()
    {
        allSkills = Resources.LoadAll<SkillDefinition>("GameData/Skills")
            .OrderBy(s => s.name).ToList();
    }

    private void InitializeHUD()
    {
        if (categoryAHeader != null) categoryAHeader.color = categoryAColor;
        if (categoryBHeader != null) categoryBHeader.color = categoryBColor;
        if (categoryCHeader != null) categoryCHeader.color = categoryCColor;

        InitExistingCards(SkillCategory.CategoryA, categoryAGrid);
        InitExistingCards(SkillCategory.CategoryB, categoryBGrid);

        if (allSkills != null && categoryCGrid != null)
        {
            foreach (var skill in allSkills.Where(s =>
                s.category != SkillCategory.CategoryA &&
                s.category != SkillCategory.CategoryB))
            {
                InitExistingCard(skill, categoryCGrid);
            }
        }

        Debug.Log($"[GemSkillPreviewHUD] Initialized {skillCards.Count} skill cards.");
    }

    private void InitExistingCards(SkillCategory category, Transform grid)
    {
        if (allSkills == null || grid == null) return;
        foreach (var skill in allSkills.Where(s => s.category == category))
            InitExistingCard(skill, grid);
    }

    private void InitExistingCard(SkillDefinition skill, Transform parent)
    {
        var cardT = parent?.Find($"SkillCard_{skill.name}");
        if (cardT == null)
        {
            Debug.LogWarning($"[GemSkillPreviewHUD] SkillCard_{skill.name} not found. 'Setup Hierarchy'を実行してください。");
            return;
        }
        var card = cardT.GetComponent<SkillHUDCardUI>();
        if (card == null) return;

        card.Initialize(skill, 0, 0, 0, cardTileColor, gemTileColor, shopTileColor, tooltip, card.GetMaxTiles());
        skillCards[skill.name] = card;
    }

    private bool ValidateAndAssignReferences()
    {
        Transform skillHUD = transform.Find("SkillHUD");
        if (skillHUD == null)
        {
            Debug.LogWarning("[GemSkillPreviewHUD] SkillHUD が見つかりません。'Setup Hierarchy'を実行してください。");
            return false;
        }

        categoryAHeader = skillHUD.Find("CategoryA_Header")?.GetComponent<TextMeshProUGUI>();
        categoryAGrid   = skillHUD.Find("CategoryA_Grid");
        categoryBHeader = skillHUD.Find("CategoryB_Header")?.GetComponent<TextMeshProUGUI>();
        categoryBGrid   = skillHUD.Find("CategoryB_Grid");
        categoryCHeader = skillHUD.Find("CategoryC_Header")?.GetComponent<TextMeshProUGUI>();
        categoryCGrid   = skillHUD.Find("CategoryC_Grid");

        if (categoryAHeader != null) categoryAHeader.color = categoryAColor;
        if (categoryBHeader != null) categoryBHeader.color = categoryBColor;
        if (categoryCHeader != null) categoryCHeader.color = categoryCColor;

        // SkillTooltipが未設定ならCanvasから自動取得
        if (tooltip == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                tooltip = canvas.transform.Find("SkillTooltip")?.GetComponent<SkillTooltip>();
        }

        return categoryAGrid != null && categoryBGrid != null;
    }

    // ================== Editor Auto-Setup ==================

#if UNITY_EDITOR
    [ContextMenu("Setup Hierarchy")]
    private void SetupHierarchy()
    {
        // ── 外側コンテナ（このGameObject自身）の RectTransform ──
        // 05_Game の SkillHUD と同じ設定値
        var outerRect = GetComponent<RectTransform>();
        if (outerRect != null)
        {
            outerRect.anchorMin        = new Vector2(0f, 1f);
            outerRect.anchorMax        = new Vector2(0f, 1f);
            outerRect.pivot            = new Vector2(0f, 1f);
            outerRect.anchoredPosition = Vector2.zero;
            outerRect.sizeDelta        = new Vector2(280f, 1080f);
        }

        // ── 内側 SkillHUD パネル（外側コンテナに追従するストレッチ） ──
        Transform skillHUD = transform.Find("SkillHUD");
        if (skillHUD == null)
        {
            var hudObj = new GameObject("SkillHUD");
            hudObj.transform.SetParent(transform, false);
            skillHUD = hudObj.transform;
            hudObj.AddComponent<RectTransform>();
        }
        var hudRect = skillHUD.GetComponent<RectTransform>();
        hudRect.anchorMin        = Vector2.zero;
        hudRect.anchorMax        = Vector2.one;
        hudRect.pivot            = new Vector2(0f, 1f);
        hudRect.anchoredPosition = Vector2.zero;
        hudRect.sizeDelta        = Vector2.zero;

        // ── Background / Line / Line(1) を先に生成（描画順：背面） ──
        CreateBackgroundAndLines(skillHUD);

        // ── カテゴリセクション（05_Game 実測値） ──
        CreateCategorySection(skillHUD, "CategoryA", -20f,  -430f, 350f, "カテゴリA", ref categoryAHeader, ref categoryAGrid);
        CreateCategorySection(skillHUD, "CategoryB", -410f, -710f, 280f, "カテゴリB", ref categoryBHeader, ref categoryBGrid);
        CreateCategorySection(skillHUD, "CategoryC", -730f, -940f, 140f, "カテゴリC", ref categoryCHeader, ref categoryCGrid);

        LoadAllSkillsEditor();
        CreateAllSkillCardsEditor();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemSkillPreviewHUD] Setup Hierarchy complete!");
    }

    /// <summary>
    /// Background / Line / Line(1) を生成する（05_Game 実測値）
    /// 生成済みの場合はスキップ。スプライトは Assets/Art/HUD/ からロード。
    /// </summary>
    private void CreateBackgroundAndLines(Transform parent)
    {
        var bgSprite   = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/HUD/HUD.png");
        var lineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/HUD/Line.png");

        // Background（既存でも値を上書き）
        var bgT = parent.Find("Background");
        GameObject bgObj = bgT != null ? bgT.gameObject : new GameObject("Background");
        if (bgT == null) bgObj.transform.SetParent(parent, false);
        bgObj.transform.SetAsFirstSibling();
        var bgRect = bgObj.GetComponent<RectTransform>() ?? bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin        = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax        = new Vector2(0.5f, 0.5f);
        bgRect.pivot            = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = new Vector2(0f, -2f);
        bgRect.sizeDelta        = new Vector2(630f, 1150f);
        var bgImg = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
        bgImg.color  = Color.white;
        bgImg.sprite = bgSprite;

        // Line（Y=350、既存でも値を上書き）
        var lineT = parent.Find("Line");
        GameObject lineObj = lineT != null ? lineT.gameObject : new GameObject("Line");
        if (lineT == null) lineObj.transform.SetParent(parent, false);
        var lineRect = lineObj.GetComponent<RectTransform>() ?? lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin        = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax        = new Vector2(0.5f, 0.5f);
        lineRect.pivot            = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = new Vector2(0f, 350f);
        lineRect.sizeDelta        = new Vector2(350f, 300f);
        var lineImg = lineObj.GetComponent<Image>() ?? lineObj.AddComponent<Image>();
        lineImg.color  = new Color(1f, 1f, 1f, 0.078f);
        lineImg.sprite = lineSprite;

        // Line (1)（Y=120、既存でも値を上書き）
        var line1T = parent.Find("Line (1)");
        GameObject line1Obj = line1T != null ? line1T.gameObject : new GameObject("Line (1)");
        if (line1T == null) line1Obj.transform.SetParent(parent, false);
        var line1Rect = line1Obj.GetComponent<RectTransform>() ?? line1Obj.AddComponent<RectTransform>();
        line1Rect.anchorMin        = new Vector2(0.5f, 0.5f);
        line1Rect.anchorMax        = new Vector2(0.5f, 0.5f);
        line1Rect.pivot            = new Vector2(0.5f, 0.5f);
        line1Rect.anchoredPosition = new Vector2(0f, 120f);
        line1Rect.sizeDelta        = new Vector2(350f, 300f);
        var line1Img = line1Obj.GetComponent<Image>() ?? line1Obj.AddComponent<Image>();
        line1Img.color  = new Color(1f, 1f, 1f, 0.078f);
        line1Img.sprite = lineSprite;
    }

    private void LoadAllSkillsEditor()
    {
        allSkills = UnityEditor.AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>(path))
            .OrderBy(s => s.name)
            .ToList();
        Debug.Log($"[GemSkillPreviewHUD] Loaded {allSkills.Count} skills (editor)");
    }

    private void CreateAllSkillCardsEditor()
    {
        if (allSkills == null) return;
        CreateSkillCardsEditor(SkillCategory.CategoryA, categoryAGrid);
        CreateSkillCardsEditor(SkillCategory.CategoryB, categoryBGrid);
        if (categoryCGrid != null)
        {
            foreach (var skill in allSkills.Where(s =>
                s.category != SkillCategory.CategoryA &&
                s.category != SkillCategory.CategoryB))
            {
                CreateSkillCardEditor(skill, categoryCGrid);
            }
        }
    }

    private void CreateSkillCardsEditor(SkillCategory category, Transform grid)
    {
        if (grid == null || allSkills == null) return;
        foreach (var skill in allSkills.Where(s => s.category == category))
            CreateSkillCardEditor(skill, grid);
    }

    private void CreateSkillCardEditor(SkillDefinition skill, Transform parent)
    {
        if (parent.Find($"SkillCard_{skill.name}") != null) return;

        var cardObj = CreateDefaultCard();
        cardObj.name = $"SkillCard_{skill.name}";
        cardObj.transform.SetParent(parent, false);

        var card = cardObj.GetComponent<SkillHUDCardUI>();
        if (card == null) card = cardObj.AddComponent<SkillHUDCardUI>();

        // 05_Game の SkillCard 実測値をセット
        var so = new UnityEditor.SerializedObject(card);
        so.FindProperty("maxTiles").intValue       = skill.maxAcquisitionCount > 0 ? skill.maxAcquisitionCount : 4;
        so.FindProperty("tileWidth").floatValue    = 12f;
        so.FindProperty("tileHeight").floatValue   = 12f;
        so.FindProperty("tileSpacing").floatValue  = 0f;
        so.FindProperty("blinkCount").intValue     = 4;
        so.FindProperty("blinkInterval").floatValue = 0.4f;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(cardObj);
        Debug.Log($"[GemSkillPreviewHUD] Created SkillCard_{skill.name} (maxTiles={skill.maxAcquisitionCount})");
    }

    private void CreateCategorySection(Transform parent, string categoryName,
        float headerY, float gridY, float gridHeight, string displayName,
        ref TextMeshProUGUI headerField, ref Transform gridField)
    {
        // Header（既存でも値を上書き）
        var existingHeader = parent.Find($"{categoryName}_Header");
        GameObject headerObj = existingHeader != null
            ? existingHeader.gameObject
            : new GameObject($"{categoryName}_Header");
        if (existingHeader == null) headerObj.transform.SetParent(parent, false);

        var hr = headerObj.GetComponent<RectTransform>() ?? headerObj.AddComponent<RectTransform>();
        hr.anchorMin        = new Vector2(0f, 1f);
        hr.anchorMax        = new Vector2(0f, 1f);
        hr.pivot            = new Vector2(0f, 1f);
        hr.anchoredPosition = new Vector2(10f, headerY);
        hr.sizeDelta        = new Vector2(300f, 30f);

        var ht = headerObj.GetComponent<TextMeshProUGUI>() ?? headerObj.AddComponent<TextMeshProUGUI>();
        ht.text      = displayName;
        ht.fontSize  = 20f;
        ht.alignment = TextAlignmentOptions.Left;

        headerField = ht;

        // Grid（既存でも RectTransform と GridLayoutGroup を上書き）
        var existingGrid = parent.Find($"{categoryName}_Grid");
        GameObject gridObj = existingGrid != null
            ? existingGrid.gameObject
            : new GameObject($"{categoryName}_Grid");
        if (existingGrid == null) gridObj.transform.SetParent(parent, false);

        var gr = gridObj.GetComponent<RectTransform>() ?? gridObj.AddComponent<RectTransform>();
        gr.anchorMin        = new Vector2(0f, 1f);
        gr.anchorMax        = new Vector2(0f, 1f);
        gr.pivot            = new Vector2(0f, 1f);
        gr.anchoredPosition = new Vector2(35f, gridY);
        gr.sizeDelta        = new Vector2(380f, gridHeight);

        var gg = gridObj.GetComponent<GridLayoutGroup>() ?? gridObj.AddComponent<GridLayoutGroup>();
        gg.cellSize       = new Vector2(110f, 50f);
        gg.spacing        = new Vector2(0f, 6f);
        gg.constraint     = GridLayoutGroup.Constraint.FixedColumnCount;
        gg.constraintCount = 2;
        gg.childAlignment = TextAnchor.UpperLeft;

        gridField = gridObj.transform;
    }

    private GameObject CreateDefaultCard()
    {
        var obj = new GameObject("SkillCard");
        obj.AddComponent<RectTransform>().sizeDelta = new Vector2(110f, 50f);

        var layout = obj.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth  = false;
        layout.childControlHeight = false;
        layout.childAlignment     = TextAnchor.MiddleLeft;
        layout.spacing            = 8f;
        layout.padding            = new RectOffset(5, 5, 5, 5);

        // IconBackground
        var iconBg = new GameObject("IconBackground");
        iconBg.transform.SetParent(obj.transform, false);
        iconBg.AddComponent<RectTransform>().sizeDelta = new Vector2(40f, 40f);
        iconBg.AddComponent<Image>().enabled = false;

        // IconImage
        var iconImg = new GameObject("IconImage");
        iconImg.transform.SetParent(iconBg.transform, false);
        var ir = iconImg.AddComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.anchoredPosition = Vector2.zero;
        ir.sizeDelta        = new Vector2(36f, 36f);
        iconImg.AddComponent<Image>().enabled = false;

        // ProgressTiles
        var tiles = new GameObject("ProgressTiles");
        tiles.transform.SetParent(obj.transform, false);
        tiles.AddComponent<RectTransform>().sizeDelta = new Vector2(95f, 25f);
        var tl = tiles.AddComponent<HorizontalLayoutGroup>();
        tl.spacing              = 0f;
        tl.childControlWidth    = false;
        tl.childControlHeight   = false;
        tl.childForceExpandWidth  = false;
        tl.childForceExpandHeight = false;
        tl.childAlignment       = TextAnchor.MiddleLeft;

        return obj;
    }
#endif
}
