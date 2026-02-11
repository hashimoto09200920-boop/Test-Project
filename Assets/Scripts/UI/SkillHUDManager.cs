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

    [Header("Tile Settings")]
    [SerializeField] private int defaultMaxTiles = 5;

    [Header("Category Colors (HDR)")]
    [SerializeField] private Color categoryAColor = new Color(0f, 2.5f, 2.0f, 1f); // ネオンシアン
    [SerializeField] private Color categoryBColor = new Color(2.5f, 0f, 1.5f, 1f); // ネオンマゼンタ
    [SerializeField] private Color categoryCColor = new Color(2.5f, 0.1f, 0.5f, 1f); // ネオンレッド

    private Dictionary<string, SkillHUDCardUI> skillCards = new Dictionary<string, SkillHUDCardUI>();
    private List<SkillDefinition> allSkills;

#if UNITY_EDITOR
    /// <summary>
    /// Inspectorから実行：ヒエラルキー構造を自動生成
    /// </summary>
    [ContextMenu("Setup Hierarchy")]
    private void SetupHierarchy()
    {
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

        // 全スキルをロードしてSkillCardを事前生成
        LoadAllSkillsEditor();
        CreateAllSkillCardsEditor();

        Debug.Log("[SkillHUDManager] Hierarchy setup complete!");

        // Inspectorを更新
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Inspectorから実行：スキルA5/A6/A7の配置順序を変更
    /// </summary>
    [ContextMenu("Rename Skills A5-A7")]
    private void RenameSkillsA5A7()
    {
        Debug.Log("[SkillHUDManager] Starting skill rename operation...");

        // 変更対象のスキルパス
        string pathA5 = "Assets/Resources/GameData/Skills/Skill_A5_MaxStrokesUp.asset";
        string pathA6 = "Assets/Resources/GameData/Skills/Skill_A6_LeftLifetimeUp.asset";
        string pathA7 = "Assets/Resources/GameData/Skills/Skill_A7_RedLifetimeUp.asset";

        // 存在確認
        if (!System.IO.File.Exists(pathA5) || !System.IO.File.Exists(pathA6) || !System.IO.File.Exists(pathA7))
        {
            Debug.LogError("[SkillHUDManager] One or more skill assets not found! Aborting.");
            return;
        }

        // 循環的な名前変更のため、一時的な名前を使用
        string tempA5 = "Skill_A5_temp";
        string tempA6 = "Skill_A6_temp";
        string tempA7 = "Skill_A7_temp";

        // 新しい名前
        string newA5 = "Skill_A5_LeftLifetimeUp";
        string newA6 = "Skill_A6_RedLifetimeUp";
        string newA7 = "Skill_A7_MaxStrokesUp";

        try
        {
            // ステップ1: すべてを一時的な名前に変更
            string result1 = UnityEditor.AssetDatabase.RenameAsset(pathA5, tempA5);
            if (!string.IsNullOrEmpty(result1))
            {
                Debug.LogError($"[SkillHUDManager] Failed to rename A5 to temp: {result1}");
                return;
            }
            Debug.Log($"[SkillHUDManager] Renamed A5 to temp");

            string result2 = UnityEditor.AssetDatabase.RenameAsset(pathA6, tempA6);
            if (!string.IsNullOrEmpty(result2))
            {
                Debug.LogError($"[SkillHUDManager] Failed to rename A6 to temp: {result2}");
                return;
            }
            Debug.Log($"[SkillHUDManager] Renamed A6 to temp");

            string result3 = UnityEditor.AssetDatabase.RenameAsset(pathA7, tempA7);
            if (!string.IsNullOrEmpty(result3))
            {
                Debug.LogError($"[SkillHUDManager] Failed to rename A7 to temp: {result3}");
                return;
            }
            Debug.Log($"[SkillHUDManager] Renamed A7 to temp");

            // アセットデータベースを更新
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            // ステップ2: 一時的な名前から新しい名前に変更
            string tempPathA5 = "Assets/Resources/GameData/Skills/Skill_A5_temp.asset";
            string tempPathA6 = "Assets/Resources/GameData/Skills/Skill_A6_temp.asset";
            string tempPathA7 = "Assets/Resources/GameData/Skills/Skill_A7_temp.asset";

            result1 = UnityEditor.AssetDatabase.RenameAsset(tempPathA6, newA5); // 旧A6 → 新A5
            if (!string.IsNullOrEmpty(result1))
            {
                Debug.LogError($"[SkillHUDManager] Failed to rename temp A6 to new A5: {result1}");
                return;
            }
            Debug.Log($"[SkillHUDManager] Renamed temp A6 to new A5 (LeftLifetimeUp)");

            result2 = UnityEditor.AssetDatabase.RenameAsset(tempPathA7, newA6); // 旧A7 → 新A6
            if (!string.IsNullOrEmpty(result2))
            {
                Debug.LogError($"[SkillHUDManager] Failed to rename temp A7 to new A6: {result2}");
                return;
            }
            Debug.Log($"[SkillHUDManager] Renamed temp A7 to new A6 (RedLifetimeUp)");

            result3 = UnityEditor.AssetDatabase.RenameAsset(tempPathA5, newA7); // 旧A5 → 新A7
            if (!string.IsNullOrEmpty(result3))
            {
                Debug.LogError($"[SkillHUDManager] Failed to rename temp A5 to new A7: {result3}");
                return;
            }
            Debug.Log($"[SkillHUDManager] Renamed temp A5 to new A7 (MaxStrokesUp)");

            // アセットデータベースを更新
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            Debug.Log("[SkillHUDManager] Skill rename complete! Now removing old SkillCards...");

            // 古い A5/A6/A7 のカードを削除
            RemoveOldSkillCards();

            Debug.Log("[SkillHUDManager] Old SkillCards removed. Now running Setup Hierarchy...");

            // Setup Hierarchy を自動実行して SkillCard を再生成
            SetupHierarchy();

            Debug.Log("[SkillHUDManager] All operations complete! Please verify the results.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SkillHUDManager] Error during rename: {e.Message}");
        }
    }

    /// <summary>
    /// 古いスキルカードを削除（リネーム後のクリーンアップ用）
    /// </summary>
    [ContextMenu("Remove Old A5-A7 Cards")]
    private void RemoveOldSkillCards()
    {
        // SkillHUDパネルを探す
        Transform skillHUD = transform.Find("SkillHUD");
        if (skillHUD == null)
        {
            Debug.LogWarning("[SkillHUDManager] SkillHUD not found!");
            return;
        }

        Transform categoryAGrid = skillHUD.Find("CategoryA_Grid");
        if (categoryAGrid == null)
        {
            Debug.LogWarning("[SkillHUDManager] CategoryA_Grid not found!");
            return;
        }

        // 削除対象の古いカード名（リネーム前の名前）
        string[] oldCardNames = new string[]
        {
            "SkillCard_Skill_A5_MaxStrokesUp",
            "SkillCard_Skill_A6_LeftLifetimeUp",
            "SkillCard_Skill_A7_RedLifetimeUp"
        };

        int removedCount = 0;
        foreach (string cardName in oldCardNames)
        {
            Transform oldCard = categoryAGrid.Find(cardName);
            if (oldCard != null)
            {
                Debug.Log($"[SkillHUDManager] Removing old card: {cardName}");
                DestroyImmediate(oldCard.gameObject);
                removedCount++;
            }
        }

        Debug.Log($"[SkillHUDManager] Removed {removedCount} old SkillCards");
    }

    /// <summary>
    /// Editor専用：全スキルをロード
    /// </summary>
    private void LoadAllSkillsEditor()
    {
        SkillDefinition[] skills = UnityEditor.AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>(path))
            .OrderBy(s => s.name)
            .ToArray();

        allSkills = skills.ToList();
        Debug.Log($"[SkillHUDManager] Loaded {allSkills.Count} skills for editor setup");
    }

    /// <summary>
    /// Editor専用：全スキルカードを事前生成
    /// </summary>
    private void CreateAllSkillCardsEditor()
    {
        if (allSkills == null || allSkills.Count == 0)
        {
            Debug.LogWarning("[SkillHUDManager] No skills loaded!");
            return;
        }

        // カテゴリA
        CreateSkillCardsEditor(SkillCategory.CategoryA, categoryAGrid, categoryAColor);

        // カテゴリB
        CreateSkillCardsEditor(SkillCategory.CategoryB, categoryBGrid, categoryBColor);

        // カテゴリC（その他）
        var otherSkills = allSkills.Where(s => s.category != SkillCategory.CategoryA && s.category != SkillCategory.CategoryB).ToList();
        if (otherSkills.Count > 0 && categoryCGrid != null)
        {
            foreach (var skill in otherSkills)
            {
                CreateSkillCardEditor(skill, categoryCGrid, categoryCColor);
            }
        }

        Debug.Log($"[SkillHUDManager] Created {allSkills.Count} skill cards in editor");
    }

    /// <summary>
    /// Editor専用：カテゴリ別にスキルカードを生成
    /// </summary>
    private void CreateSkillCardsEditor(SkillCategory category, Transform gridContainer, Color catColor)
    {
        if (gridContainer == null) return;

        var categorySkills = allSkills.Where(s => s.category == category).ToList();

        foreach (var skill in categorySkills)
        {
            CreateSkillCardEditor(skill, gridContainer, catColor);
        }
    }

    /// <summary>
    /// Editor専用：個別スキルカードを生成
    /// </summary>
    private void CreateSkillCardEditor(SkillDefinition skill, Transform parent, Color catColor)
    {
        // 既に存在するか確認
        Transform existing = parent.Find($"SkillCard_{skill.name}");
        if (existing != null)
        {
            Debug.Log($"[SkillHUDManager] SkillCard_{skill.name} already exists, skipping");
            return;
        }

        GameObject cardObj = CreateDefaultCard();
        cardObj.name = $"SkillCard_{skill.name}";
        cardObj.transform.SetParent(parent, false);

        SkillHUDCardUI card = cardObj.GetComponent<SkillHUDCardUI>();
        if (card == null)
        {
            card = cardObj.AddComponent<SkillHUDCardUI>();
        }

        // Editor専用の初期化（スキル情報を保存）
        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(card);
        // maxTilesはInspectorで調整可能なままにする
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(cardObj);

        Debug.Log($"[SkillHUDManager] Created SkillCard_{skill.name}");
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

        // 既存のHierarchy構造を検証・参照取得（生成はしない）
        if (!ValidateAndAssignReferences())
        {
            Debug.LogError("[SkillHUDManager] HUD構造が見つかりません！\n" +
                          "SkillHUDManagerを右クリック → 'Setup Hierarchy'を実行してください。");
            return; // 構造がない場合は処理中断
        }

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
    /// 既存のHierarchy構造を検証し、参照を取得（生成はしない）
    /// </summary>
    private bool ValidateAndAssignReferences()
    {
        // SkillHUDパネルを探す
        Transform skillHUD = transform.Find("SkillHUD");
        if (skillHUD == null)
        {
            Debug.LogWarning("[SkillHUDManager] SkillHUDパネルが見つかりません。");
            return false;
        }

        // カテゴリ参照を取得
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

        // ツールチップ参照を取得
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Transform tooltipTransform = canvas.transform.Find("SkillTooltip");
            if (tooltipTransform != null)
            {
                tooltip = tooltipTransform.GetComponent<SkillTooltip>();
            }
        }

        // 必須コンポーネントのチェック
        bool isValid = categoryAGrid != null && categoryBGrid != null;

        if (!isValid)
        {
            Debug.LogWarning("[SkillHUDManager] 必須のグリッドコンポーネントが見つかりません。");
        }

        return isValid;
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
    /// HUDを初期化（既存のSkillCardを検索・初期化）
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

        // 既存のスキルカードを検索・初期化
        InitializeExistingSkillCards(SkillCategory.CategoryA, categoryAGrid, categoryAColor);
        InitializeExistingSkillCards(SkillCategory.CategoryB, categoryBGrid, categoryBColor);

        // カテゴリC（その他）も初期化
        var otherSkills = allSkills.Where(s => s.category != SkillCategory.CategoryA && s.category != SkillCategory.CategoryB).ToList();
        if (otherSkills.Count > 0 && categoryCGrid != null)
        {
            foreach (var skill in otherSkills)
            {
                InitializeExistingSkillCard(skill, categoryCGrid, categoryCColor);
            }
        }

        Debug.Log($"[SkillHUDManager] Initialized {skillCards.Count} skill cards");
    }

    /// <summary>
    /// 既存のスキルカードを検索・初期化
    /// </summary>
    private void InitializeExistingSkillCards(SkillCategory category, Transform gridContainer, Color catColor)
    {
        if (gridContainer == null) return;

        var categorySkills = allSkills.Where(s => s.category == category).ToList();

        foreach (var skill in categorySkills)
        {
            InitializeExistingSkillCard(skill, gridContainer, catColor);
        }
    }

    /// <summary>
    /// 個別スキルカードを検索・初期化（生成はしない）
    /// </summary>
    private void InitializeExistingSkillCard(SkillDefinition skill, Transform parent, Color catColor)
    {
        // 既存のSkillCardを検索
        Transform cardTransform = parent.Find($"SkillCard_{skill.name}");
        if (cardTransform == null)
        {
            Debug.LogWarning($"[SkillHUDManager] SkillCard_{skill.name} not found! Run 'Setup Hierarchy' first.");
            return;
        }

        SkillHUDCardUI card = cardTransform.GetComponent<SkillHUDCardUI>();
        if (card == null)
        {
            Debug.LogWarning($"[SkillHUDManager] SkillHUDCardUI component not found on {cardTransform.name}");
            return;
        }

        // 現在の取得レベルを取得
        int currentLevel = skillManager != null ? skillManager.GetSkillAcquisitionCount(skill) : 0;

        // カード初期化（Inspector設定のmaxTilesを維持）
        card.Initialize(skill, currentLevel, catColor, tooltip, card.GetMaxTiles());

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

        // アイコン背景（非表示）
        GameObject iconBg = new GameObject("IconBackground");
        iconBg.transform.SetParent(cardObj.transform, false);
        RectTransform iconBgRect = iconBg.AddComponent<RectTransform>();
        iconBgRect.sizeDelta = new Vector2(50f, 50f);
        Image iconBgImage = iconBg.AddComponent<Image>();
        iconBgImage.enabled = false; // 背景画像は不要なので非表示

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
