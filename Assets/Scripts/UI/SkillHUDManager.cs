using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Game.Skills;

/// <summary>
/// 画面左側のスキルHUD全体を管理
/// </summary>
public class SkillHUDManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkillManager skillManager;
    [SerializeField] private SkillTooltip tooltip;

    [Header("Grid Containers")]
    [SerializeField] private Transform categoryAGrid;
    [SerializeField] private Transform categoryBGrid;
    [SerializeField] private Transform categoryCGrid;

    [Header("Category Headers")]
    [SerializeField] private TextMeshProUGUI categoryAHeader;
    [SerializeField] private TextMeshProUGUI categoryBHeader;
    [SerializeField] private TextMeshProUGUI categoryCHeader;

    [Header("Card Settings")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Category Colors (HDR)")]
    [SerializeField] private Color categoryAColor = new Color(0f, 2.5f, 2.0f, 1f); // ネオンシアン
    [SerializeField] private Color categoryBColor = new Color(2.5f, 0f, 1.5f, 1f); // ネオンマゼンタ
    [SerializeField] private Color categoryCColor = new Color(2.5f, 0.1f, 0.5f, 1f); // ネオンレッド

    private Dictionary<string, SkillHUDCardUI> skillCards = new Dictionary<string, SkillHUDCardUI>();
    private List<SkillDefinition> allSkills;

    /// <summary>
    /// Inspectorから実行：ヒエラルキー構造を自動生成
    /// </summary>
    [ContextMenu("Setup Hierarchy")]
    private void SetupHierarchy()
    {
#if UNITY_EDITOR
        Debug.Log("[SkillHUDManager] Setting up hierarchy...");

        // SkillHUDパネルを探す
        Transform skillHUD = transform.Find("SkillHUD");
        if (skillHUD == null)
        {
            Debug.LogError("[SkillHUDManager] SkillHUD not found! Please create it first.");
            return;
        }

        // カテゴリAのヘッダーとグリッドを作成
        CreateCategorySection(skillHUD, "CategoryA", -20f, -55f, 350f, "カテゴリA", ref categoryAHeader, ref categoryAGrid);

        // カテゴリBのヘッダーとグリッドを作成
        CreateCategorySection(skillHUD, "CategoryB", -410f, -445f, 280f, "カテゴリB", ref categoryBHeader, ref categoryBGrid);

        // カテゴリCのヘッダーとグリッドを作成
        CreateCategorySection(skillHUD, "CategoryC", -730f, -765f, 140f, "カテゴリC", ref categoryCHeader, ref categoryCGrid);

        // ツールチップを作成
        CreateTooltip();

        // ツールチップのテキストフィールドを設定
        if (tooltip != null)
        {
            SetupTooltipReferences();
        }

        Debug.Log("[SkillHUDManager] Hierarchy setup complete!");

        // Inspectorを更新
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// ツールチップのSerializeField参照を設定
    /// </summary>
    private void SetupTooltipReferences()
    {
        if (tooltip == null) return;

        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(tooltip);

        Transform tooltipTransform = tooltip.transform;

        // 各フィールドを探して設定
        SetSerializedField(so, "skillNameText", tooltipTransform.Find("SkillName"));
        SetSerializedField(so, "categoryText", tooltipTransform.Find("Category"));
        SetSerializedField(so, "levelText", tooltipTransform.Find("Level"));
        SetSerializedField(so, "descriptionText", tooltipTransform.Find("Description"));
        SetSerializedField(so, "currentEffectText", tooltipTransform.Find("CurrentEffect"));
        SetSerializedField(so, "nextEffectText", tooltipTransform.Find("NextEffect"));
        SetSerializedField(so, "canvasGroup", tooltip.transform);

        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(tooltip);
    }

    /// <summary>
    /// SerializedFieldを設定
    /// </summary>
    private void SetSerializedField(UnityEditor.SerializedObject so, string fieldName, Transform target)
    {
        if (target == null) return;

        UnityEditor.SerializedProperty prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            if (fieldName == "canvasGroup")
            {
                prop.objectReferenceValue = target.GetComponent<CanvasGroup>();
            }
            else
            {
                prop.objectReferenceValue = target.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    /// <summary>
    /// カテゴリセクション（ヘッダー + グリッド）を作成
    /// </summary>
    private void CreateCategorySection(Transform parent, string categoryName, float headerY, float gridY, float gridHeight,
                                       string displayName, ref TextMeshProUGUI headerField, ref Transform gridField)
    {
        // ヘッダー作成
        Transform existingHeader = parent.Find($"{categoryName}_Header");
        GameObject headerObj;

        if (existingHeader != null)
        {
            headerObj = existingHeader.gameObject;
            Debug.Log($"[SkillHUDManager] Found existing {categoryName}_Header");
        }
        else
        {
            headerObj = new GameObject($"{categoryName}_Header");
            headerObj.transform.SetParent(parent, false);

            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(0, 1);
            headerRect.pivot = new Vector2(0, 1);
            headerRect.anchoredPosition = new Vector2(10f, headerY);
            headerRect.sizeDelta = new Vector2(380f, 30f);

            TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
            headerText.text = displayName;
            headerText.fontSize = 20f;
            headerText.alignment = TextAlignmentOptions.Left;

            Debug.Log($"[SkillHUDManager] Created {categoryName}_Header");
        }

        headerField = headerObj.GetComponent<TextMeshProUGUI>();

        // グリッド作成
        Transform existingGrid = parent.Find($"{categoryName}_Grid");
        GameObject gridObj;

        if (existingGrid != null)
        {
            gridObj = existingGrid.gameObject;
            Debug.Log($"[SkillHUDManager] Found existing {categoryName}_Grid");
        }
        else
        {
            gridObj = new GameObject($"{categoryName}_Grid");
            gridObj.transform.SetParent(parent, false);

            RectTransform gridRect = gridObj.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0, 1);
            gridRect.anchorMax = new Vector2(0, 1);
            gridRect.pivot = new Vector2(0, 1);
            gridRect.anchoredPosition = new Vector2(10f, gridY);
            gridRect.sizeDelta = new Vector2(380f, gridHeight);

            GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(180f, 60f);
            gridLayout.spacing = new Vector2(10f, 5f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 2;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            Debug.Log($"[SkillHUDManager] Created {categoryName}_Grid");
        }

        gridField = gridObj.transform;
    }

    /// <summary>
    /// ツールチップを作成
    /// </summary>
    private void CreateTooltip()
    {
        // Canvasを探す
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SkillHUDManager] Canvas not found!");
            return;
        }

        Transform existingTooltip = canvas.transform.Find("SkillTooltip");
        GameObject tooltipObj;

        if (existingTooltip != null)
        {
            tooltipObj = existingTooltip.gameObject;
            Debug.Log("[SkillHUDManager] Found existing SkillTooltip");
        }
        else
        {
            tooltipObj = new GameObject("SkillTooltip");
            tooltipObj.transform.SetParent(canvas.transform, false);

            RectTransform tooltipRect = tooltipObj.AddComponent<RectTransform>();
            tooltipRect.sizeDelta = new Vector2(300f, 250f);
            tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
            tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
            tooltipRect.pivot = new Vector2(0f, 1f);

            // 背景
            Image tooltipBg = tooltipObj.AddComponent<Image>();
            tooltipBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // CanvasGroup追加
            CanvasGroup canvasGroup = tooltipObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            // テキストフィールドを作成
            CreateTooltipTextField(tooltipObj.transform, "SkillName", 0f, -10f, 280f, 30f, 18f, TextAlignmentOptions.Center);
            CreateTooltipTextField(tooltipObj.transform, "Category", 0f, -45f, 280f, 20f, 14f, TextAlignmentOptions.Left);
            CreateTooltipTextField(tooltipObj.transform, "Level", 0f, -70f, 280f, 20f, 14f, TextAlignmentOptions.Left);
            CreateTooltipTextField(tooltipObj.transform, "Description", 0f, -95f, 280f, 60f, 12f, TextAlignmentOptions.TopLeft);
            CreateTooltipTextField(tooltipObj.transform, "CurrentEffect", 0f, -160f, 280f, 20f, 12f, TextAlignmentOptions.Left);
            CreateTooltipTextField(tooltipObj.transform, "NextEffect", 0f, -185f, 280f, 20f, 12f, TextAlignmentOptions.Left);

            Debug.Log("[SkillHUDManager] Created SkillTooltip");
        }

        tooltip = tooltipObj.GetComponent<SkillTooltip>();
        if (tooltip == null)
        {
            tooltip = tooltipObj.AddComponent<SkillTooltip>();
        }
    }

    /// <summary>
    /// ツールチップ用のテキストフィールドを作成
    /// </summary>
    private void CreateTooltipTextField(Transform parent, string name, float x, float y, float width, float height,
                                       float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(x, y);
        textRect.sizeDelta = new Vector2(width, height);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
    }
#endif

    private void Start()
    {
        // SkillManagerの参照を取得
        if (skillManager == null)
        {
            skillManager = SkillManager.Instance;
        }

        // ヒエラルキーを自動構築（未作成の場合のみ）
        AutoSetupHierarchy();

        // 全スキルをロード
        LoadAllSkills();

        // HUD初期化
        InitializeHUD();

        // スキル取得イベントに登録
        if (skillManager != null)
        {
            skillManager.OnSkillAcquired += OnSkillAcquired;
        }
    }

    /// <summary>
    /// ヒエラルキーを自動構築（ランタイム用）
    /// </summary>
    private void AutoSetupHierarchy()
    {
        // SkillHUDパネルを探す（または作成）
        Transform skillHUD = transform.Find("SkillHUD");
        if (skillHUD == null)
        {
            // SkillHUDパネルを作成
            GameObject hudObj = new GameObject("SkillHUD");
            hudObj.transform.SetParent(transform, false);

            RectTransform hudRect = hudObj.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 1);
            hudRect.anchorMax = new Vector2(0, 1);
            hudRect.pivot = new Vector2(0, 1);
            hudRect.anchoredPosition = new Vector2(0, 0);
            hudRect.sizeDelta = new Vector2(400f, 1040f);

            // 背景画像（オプション）
            Image bgImage = hudObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 150f / 255f);

            skillHUD = hudObj.transform;
            Debug.Log("[SkillHUDManager] Created SkillHUD panel");
        }

        // カテゴリセクションを作成（既に存在する場合はスキップ）
        if (skillHUD.Find("CategoryA_Header") == null)
        {
            CreateCategorySectionRuntime(skillHUD, "CategoryA", -20f, -55f, 350f, "カテゴリA");
        }

        if (skillHUD.Find("CategoryB_Header") == null)
        {
            CreateCategorySectionRuntime(skillHUD, "CategoryB", -410f, -445f, 280f, "カテゴリB");
        }

        if (skillHUD.Find("CategoryC_Header") == null)
        {
            CreateCategorySectionRuntime(skillHUD, "CategoryC", -730f, -765f, 140f, "カテゴリC");
        }

        // ツールチップを作成（既に存在する場合はスキップ）
        CreateTooltipRuntime();

        // 参照を設定
        categoryAHeader = skillHUD.Find("CategoryA_Header")?.GetComponent<TextMeshProUGUI>();
        categoryAGrid = skillHUD.Find("CategoryA_Grid");
        categoryBHeader = skillHUD.Find("CategoryB_Header")?.GetComponent<TextMeshProUGUI>();
        categoryBGrid = skillHUD.Find("CategoryB_Grid");
        categoryCHeader = skillHUD.Find("CategoryC_Header")?.GetComponent<TextMeshProUGUI>();
        categoryCGrid = skillHUD.Find("CategoryC_Grid");

        // カテゴリヘッダーの色設定
        if (categoryAHeader != null) categoryAHeader.color = categoryAColor;
        if (categoryBHeader != null) categoryBHeader.color = categoryBColor;
        if (categoryCHeader != null) categoryCHeader.color = categoryCColor;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Transform tooltipTransform = canvas.transform.Find("SkillTooltip");
            if (tooltipTransform != null)
            {
                tooltip = tooltipTransform.GetComponent<SkillTooltip>();
            }
        }

        Debug.Log("[SkillHUDManager] Auto setup hierarchy complete");
    }

    /// <summary>
    /// カテゴリセクションを作成（ランタイム用）
    /// </summary>
    private void CreateCategorySectionRuntime(Transform parent, string categoryName, float headerY, float gridY, float gridHeight, string displayName)
    {
        // ヘッダー作成
        GameObject headerObj = new GameObject($"{categoryName}_Header");
        headerObj.transform.SetParent(parent, false);

        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(0, 1);
        headerRect.pivot = new Vector2(0, 1);
        headerRect.anchoredPosition = new Vector2(10f, headerY);
        headerRect.sizeDelta = new Vector2(380f, 30f);

        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = displayName;
        headerText.fontSize = 20f;
        headerText.alignment = TextAlignmentOptions.Left;

        // グリッド作成
        GameObject gridObj = new GameObject($"{categoryName}_Grid");
        gridObj.transform.SetParent(parent, false);

        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0, 1);
        gridRect.anchorMax = new Vector2(0, 1);
        gridRect.pivot = new Vector2(0, 1);
        gridRect.anchoredPosition = new Vector2(10f, gridY);
        gridRect.sizeDelta = new Vector2(380f, gridHeight);

        GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(160f, 60f);
        gridLayout.spacing = new Vector2(10f, 5f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 2;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
    }

    /// <summary>
    /// ツールチップを作成（ランタイム用）
    /// </summary>
    private void CreateTooltipRuntime()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Transform existingTooltip = canvas.transform.Find("SkillTooltip");
        if (existingTooltip != null) return; // 既に存在する場合はスキップ

        GameObject tooltipObj = new GameObject("SkillTooltip");
        tooltipObj.transform.SetParent(canvas.transform, false);

        RectTransform tooltipRect = tooltipObj.AddComponent<RectTransform>();
        tooltipRect.sizeDelta = new Vector2(300f, 250f);
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 1f);

        // 背景
        Image tooltipBg = tooltipObj.AddComponent<Image>();
        tooltipBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        // CanvasGroup追加
        CanvasGroup canvasGroup = tooltipObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        // テキストフィールドを作成
        CreateTooltipTextFieldRuntime(tooltipObj.transform, "SkillName", 0f, -10f, 280f, 30f, 18f, TextAlignmentOptions.Center);
        CreateTooltipTextFieldRuntime(tooltipObj.transform, "Category", 0f, -45f, 280f, 20f, 14f, TextAlignmentOptions.Left);
        CreateTooltipTextFieldRuntime(tooltipObj.transform, "Level", 0f, -70f, 280f, 20f, 14f, TextAlignmentOptions.Left);
        CreateTooltipTextFieldRuntime(tooltipObj.transform, "Description", 0f, -95f, 280f, 60f, 12f, TextAlignmentOptions.TopLeft);
        CreateTooltipTextFieldRuntime(tooltipObj.transform, "CurrentEffect", 0f, -160f, 280f, 20f, 12f, TextAlignmentOptions.Left);
        CreateTooltipTextFieldRuntime(tooltipObj.transform, "NextEffect", 0f, -185f, 280f, 20f, 12f, TextAlignmentOptions.Left);

        // SkillTooltipコンポーネントを追加
        tooltipObj.AddComponent<SkillTooltip>();

        Debug.Log("[SkillHUDManager] Created SkillTooltip");
    }

    /// <summary>
    /// ツールチップ用テキストフィールドを作成（ランタイム用）
    /// </summary>
    private void CreateTooltipTextFieldRuntime(Transform parent, string name, float x, float y, float width, float height, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(x, y);
        textRect.sizeDelta = new Vector2(width, height);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
    }

    private void OnDestroy()
    {
        // イベント登録解除
        if (skillManager != null)
        {
            skillManager.OnSkillAcquired -= OnSkillAcquired;
        }
    }

    /// <summary>
    /// 全スキルをResourcesからロード
    /// </summary>
    private void LoadAllSkills()
    {
        SkillDefinition[] skills = Resources.LoadAll<SkillDefinition>("GameData/Skills");
        allSkills = skills.OrderBy(s => s.name).ToList();

        Debug.Log($"[SkillHUDManager] Loaded {allSkills.Count} skills");
    }

    /// <summary>
    /// HUDを初期化
    /// </summary>
    private void InitializeHUD()
    {
        if (allSkills == null || allSkills.Count == 0)
        {
            Debug.LogWarning("[SkillHUDManager] No skills loaded!");
            return;
        }

        // カテゴリヘッダーの色設定
        if (categoryAHeader != null) categoryAHeader.color = categoryAColor;
        if (categoryBHeader != null) categoryBHeader.color = categoryBColor;
        if (categoryCHeader != null) categoryCHeader.color = categoryCColor;

        // カテゴリ別にスキルカードを生成
        CreateSkillCards(SkillCategory.CategoryA, categoryAGrid, categoryAColor);
        CreateSkillCards(SkillCategory.CategoryB, categoryBGrid, categoryBColor);

        // カテゴリC（その他）も作成
        var otherSkills = allSkills.Where(s => s.category != SkillCategory.CategoryA && s.category != SkillCategory.CategoryB).ToList();
        if (otherSkills.Count > 0 && categoryCGrid != null)
        {
            foreach (var skill in otherSkills)
            {
                CreateSkillCard(skill, categoryCGrid, categoryCColor);
            }
        }

        Debug.Log($"[SkillHUDManager] Created {skillCards.Count} skill cards");
    }

    /// <summary>
    /// 特定カテゴリのスキルカードを作成
    /// </summary>
    private void CreateSkillCards(SkillCategory category, Transform gridContainer, Color catColor)
    {
        if (gridContainer == null) return;

        var categorySkills = allSkills.Where(s => s.category == category).ToList();

        foreach (var skill in categorySkills)
        {
            CreateSkillCard(skill, gridContainer, catColor);
        }
    }

    /// <summary>
    /// 個別スキルカードを生成
    /// </summary>
    private void CreateSkillCard(SkillDefinition skill, Transform parent, Color catColor)
    {
        GameObject cardObj;

        if (cardPrefab != null)
        {
            cardObj = Instantiate(cardPrefab, parent);
        }
        else
        {
            // デフォルトカード生成
            cardObj = CreateDefaultCard();
            cardObj.transform.SetParent(parent, false);
        }

        SkillHUDCardUI card = cardObj.GetComponent<SkillHUDCardUI>();
        if (card == null)
        {
            card = cardObj.AddComponent<SkillHUDCardUI>();
        }

        // 現在の取得レベルを取得
        int currentLevel = skillManager != null ? skillManager.GetSkillAcquisitionCount(skill) : 0;

        // カード初期化
        card.Initialize(skill, currentLevel, catColor, tooltip);

        // 辞書に登録
        skillCards[skill.name] = card;
    }

    /// <summary>
    /// デフォルトのカードUIを生成
    /// </summary>
    private GameObject CreateDefaultCard()
    {
        GameObject cardObj = new GameObject("SkillCard");

        RectTransform rect = cardObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160f, 60f);

        // 簡易的なレイアウト（アイコンとタイルのみ）
        HorizontalLayoutGroup layout = cardObj.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 8f;
        layout.padding = new RectOffset(5, 5, 5, 5);

        // アイコン背景
        GameObject iconBg = new GameObject("IconBackground");
        iconBg.transform.SetParent(cardObj.transform, false);
        RectTransform iconBgRect = iconBg.AddComponent<RectTransform>();
        iconBgRect.sizeDelta = new Vector2(50f, 50f);
        Image iconBgImage = iconBg.AddComponent<Image>();

        // アイコン画像（背景の子として）
        GameObject iconImg = new GameObject("IconImage");
        iconImg.transform.SetParent(iconBg.transform, false);
        RectTransform iconImgRect = iconImg.AddComponent<RectTransform>();
        iconImgRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconImgRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconImgRect.pivot = new Vector2(0.5f, 0.5f);
        iconImgRect.anchoredPosition = Vector2.zero;
        iconImgRect.sizeDelta = new Vector2(45f, 45f);
        Image iconImage = iconImg.AddComponent<Image>();
        iconImage.enabled = false; // デフォルトでは非表示（スキルアイコンがセットされたら表示）

        // プログレスタイルコンテナ
        GameObject tilesContainer = new GameObject("ProgressTiles");
        tilesContainer.transform.SetParent(cardObj.transform, false);
        RectTransform tilesRect = tilesContainer.AddComponent<RectTransform>();
        tilesRect.sizeDelta = new Vector2(95f, 25f);
        HorizontalLayoutGroup tilesLayout = tilesContainer.AddComponent<HorizontalLayoutGroup>();
        tilesLayout.spacing = 3f;
        tilesLayout.childControlWidth = false;
        tilesLayout.childControlHeight = false;
        tilesLayout.childForceExpandWidth = false;
        tilesLayout.childForceExpandHeight = false;
        tilesLayout.childAlignment = TextAnchor.MiddleLeft;

        return cardObj;
    }

    /// <summary>
    /// スキル取得時のイベントハンドラ
    /// </summary>
    private void OnSkillAcquired(SkillDefinition skill)
    {
        if (skill == null) return;

        if (skillCards.TryGetValue(skill.name, out SkillHUDCardUI card))
        {
            int newLevel = skillManager != null ? skillManager.GetSkillAcquisitionCount(skill) : 0;
            card.UpdateLevel(newLevel);
        }
    }

    /// <summary>
    /// 全スキルカードの表示を更新
    /// </summary>
    public void RefreshAllCards()
    {
        if (skillManager == null || allSkills == null) return;

        foreach (var kvp in skillCards)
        {
            string skillName = kvp.Key;
            SkillHUDCardUI card = kvp.Value;

            // allSkillsからSkillDefinitionを検索
            SkillDefinition skill = allSkills.FirstOrDefault(s => s.name == skillName);
            if (skill != null)
            {
                int currentLevel = skillManager.GetSkillAcquisitionCount(skill);
                card.UpdateLevel(currentLevel);
            }
        }
    }
}
