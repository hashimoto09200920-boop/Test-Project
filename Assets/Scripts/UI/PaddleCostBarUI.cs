using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 白線・赤線・総本数をバー形式で表示するUI
/// 全てPlay前のInspectorで調整可能
/// </summary>
public class PaddleCostBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PaddleCostManager costManager;
    [SerializeField] private StrokeManager strokeManager;
    [SerializeField] private PaddleDrawer paddleDrawer;

    [Header("Bar Size Settings")]
    [SerializeField] private float baseBarWidth = 200f; // 初期値の時のバー幅

    [Header("White Line Bar")]
    [SerializeField] private Image whiteBarFill;
    [SerializeField] private Image whiteBarBackground;
    [SerializeField] private TextMeshProUGUI whiteBarText;
    [SerializeField] private Material whiteBarMaterial;
    [ColorUsage(true, true)] [SerializeField] private Color whiteBarColorLeft = Color.white;
    [ColorUsage(true, true)] [SerializeField] private Color whiteBarColorRight = new Color(0f, 2.5f, 2.0f, 1f); // ネオンシアン (HDR)
    [SerializeField] private Color whiteBarBgColor = new Color(0f, 0f, 0f, 0.5f);
    [ColorUsage(true, true)] [SerializeField] private Color whiteBarPenaltyColor = new Color(3.0f, 0.5f, 0.5f, 1f); // ペナルティ中の赤（HDR）
    [SerializeField] private float whiteBarPenaltyBlinkFrequency = 3f;

    [Header("Red Line Bar")]
    [SerializeField] private Image redBarFill;
    [SerializeField] private Image redBarBackground;
    [SerializeField] private TextMeshProUGUI redBarText;
    [SerializeField] private Material redBarMaterial;
    [ColorUsage(true, true)] [SerializeField] private Color redBarColorLeft = new Color(2.5f, 1.0f, 0f, 1f); // ネオンオレンジ (HDR)
    [ColorUsage(true, true)] [SerializeField] private Color redBarColorRight = new Color(2.5f, 0f, 0f, 1f); // ネオンレッド (HDR)
    [SerializeField] private Color redBarBgColor = new Color(0f, 0f, 0f, 0.5f);
    [ColorUsage(true, true)] [SerializeField] private Color redBarPenaltyColor = new Color(3.0f, 0.5f, 0.5f, 1f); // ペナルティ中の赤（HDR）
    [SerializeField] private float redBarPenaltyBlinkFrequency = 3f;

    [Header("Stroke Tiles Bar")]
    [SerializeField] private Transform tileContainer;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Material tileMaterial;
    [ColorUsage(true, true)] [SerializeField] private Color tileActiveColorLeft = new Color(2.0f, 0f, 2.5f, 1f); // ネオンパープル (HDR)
    [ColorUsage(true, true)] [SerializeField] private Color tileActiveColorRight = new Color(2.5f, 1.0f, 0f, 1f); // ネオンオレンジ (HDR)
    [SerializeField] private Color tileInactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f); // グレー

    [Header("Text Settings")]
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int fontSize = 14;
    [SerializeField] private string numberFormat = "F1"; // 小数点第1位まで表示

    [Header("Runtime")]
    [SerializeField] private List<Image> tiles = new List<Image>();

    private void Awake()
    {
        // 参照チェック
        if (costManager == null)
        {
            costManager = FindFirstObjectByType<PaddleCostManager>();
            if (costManager == null)
                Debug.LogWarning("[PaddleCostBarUI] PaddleCostManager not found.");
        }

        if (strokeManager == null)
        {
            strokeManager = FindFirstObjectByType<StrokeManager>();
            if (strokeManager == null)
                Debug.LogWarning("[PaddleCostBarUI] StrokeManager not found.");
        }

        if (paddleDrawer == null)
        {
            paddleDrawer = FindFirstObjectByType<PaddleDrawer>();
            if (paddleDrawer == null)
                Debug.LogWarning("[PaddleCostBarUI] PaddleDrawer not found.");
        }

        // マテリアルのインスタンスを作成（元のマテリアルを変更しないため）
        if (whiteBarFill != null && whiteBarMaterial != null)
        {
            whiteBarFill.material = new Material(whiteBarMaterial);
            whiteBarFill.material.SetColor("_ColorLeft", whiteBarColorLeft);
            whiteBarFill.material.SetColor("_ColorRight", whiteBarColorRight);
        }

        if (redBarFill != null && redBarMaterial != null)
        {
            redBarFill.material = new Material(redBarMaterial);
            redBarFill.material.SetColor("_ColorLeft", redBarColorLeft);
            redBarFill.material.SetColor("_ColorRight", redBarColorRight);
        }

        // テキストカラーとフォントサイズを適用
        ApplyTextSettings();

        // SkillManagerのイベントを購読（スキル取得時にタイルを更新）
        var skillManager = Game.Skills.SkillManager.Instance;
        if (skillManager != null)
        {
            skillManager.OnSkillAcquired += OnSkillAcquired;
        }
    }

    private void Start()
    {
        // 1フレーム遅延してタイルを生成（SkillTestTool.Start()がスキルを適用した後に実行するため）
        StartCoroutine(LateRegenerateTiles());
    }

    /// <summary>
    /// 1フレーム遅延してタイルを再生成（スキル適用後に実行）
    /// </summary>
    private System.Collections.IEnumerator LateRegenerateTiles()
    {
        yield return null; // 1フレーム待機（SkillTestTool.Start()完了後）
        RegenerateTiles();
    }

    private void OnDestroy()
    {
        // イベント購読を解除
        var skillManager = Game.Skills.SkillManager.Instance;
        if (skillManager != null)
        {
            skillManager.OnSkillAcquired -= OnSkillAcquired;
        }
    }

    /// <summary>
    /// スキル取得時の処理
    /// </summary>
    private void OnSkillAcquired(Game.Skills.SkillDefinition skill)
    {
        // スキル取得時にタイルを再生成（MaxStrokesが変わった可能性があるため）
        if (skill.effectType == Game.Skills.SkillEffectType.MaxStrokesUp)
        {
            RegenerateTiles();
        }
    }

    private void Update()
    {
        UpdateWhiteBar();
        UpdateRedBar();
        UpdateStrokeTiles();
    }

    /// <summary>
    /// 白線バーを更新
    /// </summary>
    private void UpdateWhiteBar()
    {
        if (costManager == null || whiteBarFill == null || whiteBarText == null) return;

        float current = costManager.LeftCurrentCost;
        float max = costManager.LeftMaxCost;

        // ペナルティ点滅（背景バー）
        if (whiteBarBackground != null)
        {
            bool isInPenalty = costManager.IsLeftInPenaltyDelay;
            bool isUISuspended = Game.UI.SkillSelectionUI.IsShowing ||
                                 (PauseManager.Instance != null && PauseManager.Instance.IsPaused);
            if (isInPenalty && !isUISuspended)
            {
                float blink = Mathf.Abs(Mathf.Sin(Time.unscaledTime * whiteBarPenaltyBlinkFrequency * Mathf.PI));
                whiteBarBackground.color = Color.Lerp(whiteBarBgColor, whiteBarPenaltyColor, blink);
            }
            else if (isInPenalty)
            {
                whiteBarBackground.color = whiteBarPenaltyColor; // 点滅停止中は赤固定
            }
            else
            {
                whiteBarBackground.color = whiteBarBgColor;
            }
        }

        // RectTransformのwidthを変更してバーを表現（グラデーション対応）
        RectTransform fillRect = whiteBarFill.GetComponent<RectTransform>();
        if (fillRect != null)
        {
            RectTransform parentRect = fillRect.parent.GetComponent<RectTransform>();

            // 親のバー全体の幅を、スキルレベルに応じて調整
            if (parentRect != null)
            {
                var skillManager = Game.Skills.SkillManager.Instance;
                if (skillManager != null && skillManager.BaseLeftMaxCost > 0)
                {
                    float ratio = max / skillManager.BaseLeftMaxCost;
                    float newWidth = baseBarWidth * ratio;
                    parentRect.sizeDelta = new Vector2(newWidth, parentRect.sizeDelta.y);
                }
            }

            float maxWidth = parentRect != null ? parentRect.rect.width : baseBarWidth;
            float fillRatio = max > 0 ? Mathf.Clamp01(current / max) : 0f;

            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(maxWidth * fillRatio, 0);
        }

        // テキスト更新（現在値/最大値 (回復量 / 持続時間)）
        float recovery = costManager != null ? costManager.LeftRecoverPerSecond : 0f;
        float lifetime = paddleDrawer != null ? paddleDrawer.NormalLifetime : 0f;
        whiteBarText.text = $"{current.ToString(numberFormat)}/{max.ToString(numberFormat)} ({recovery.ToString(numberFormat)} / {lifetime.ToString(numberFormat)})";
    }

    /// <summary>
    /// 赤線バーを更新
    /// </summary>
    private void UpdateRedBar()
    {
        if (costManager == null || redBarFill == null || redBarText == null) return;

        float current = costManager.RedCurrentCost;
        float max = costManager.RedMaxCost;

        // ペナルティ点滅（背景バー）
        if (redBarBackground != null)
        {
            bool isInPenalty = costManager.IsRedInPenaltyDelay;
            bool isUISuspended = Game.UI.SkillSelectionUI.IsShowing ||
                                 (PauseManager.Instance != null && PauseManager.Instance.IsPaused);
            if (isInPenalty && !isUISuspended)
            {
                float blink = Mathf.Abs(Mathf.Sin(Time.unscaledTime * redBarPenaltyBlinkFrequency * Mathf.PI));
                redBarBackground.color = Color.Lerp(redBarBgColor, redBarPenaltyColor, blink);
            }
            else if (isInPenalty)
            {
                redBarBackground.color = redBarPenaltyColor; // 点滅停止中は赤固定
            }
            else
            {
                redBarBackground.color = redBarBgColor;
            }
        }

        // RectTransformのwidthを変更してバーを表現（グラデーション対応）
        RectTransform fillRect = redBarFill.GetComponent<RectTransform>();
        if (fillRect != null)
        {
            RectTransform parentRect = fillRect.parent.GetComponent<RectTransform>();

            // 親のバー全体の幅を、スキルレベルに応じて調整
            if (parentRect != null)
            {
                var skillManager = Game.Skills.SkillManager.Instance;
                if (skillManager != null && skillManager.BaseRedMaxCost > 0)
                {
                    float ratio = max / skillManager.BaseRedMaxCost;
                    float newWidth = baseBarWidth * ratio;
                    parentRect.sizeDelta = new Vector2(newWidth, parentRect.sizeDelta.y);
                }
            }

            float maxWidth = parentRect != null ? parentRect.rect.width : baseBarWidth;
            float fillRatio = max > 0 ? Mathf.Clamp01(current / max) : 0f;

            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(maxWidth * fillRatio, 0);
        }

        // テキスト更新（現在値/最大値 (回復量 / 持続時間)）
        float recovery = costManager != null ? costManager.RedRecoverPerSecond : 0f;
        float lifetime = paddleDrawer != null ? paddleDrawer.RedLifetime : 0f;
        redBarText.text = $"{current.ToString(numberFormat)}/{max.ToString(numberFormat)} ({recovery.ToString(numberFormat)} / {lifetime.ToString(numberFormat)})";
    }

    /// <summary>
    /// ストロークタイルを更新
    /// </summary>
    private void UpdateStrokeTiles()
    {
        if (strokeManager == null) return;

        int maxStrokes = strokeManager.MaxStrokes;
        int activeCount = strokeManager.ActiveStrokesCount;
        int remainingCount = maxStrokes - activeCount; // 残り本数

        // タイル表示枚数（最大5枚）
        int displayTileCount = Mathf.Min(maxStrokes, 5);

        // タイル枚数が変わった場合は再生成
        if (tiles.Count != displayTileCount)
        {
            RegenerateTiles();
            return;
        }

        // 残り本数分のタイルを明るく、使用中のタイルを暗く
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] != null)
            {
                // 残り本数分が明るい（使える分、グラデーション）、それ以外は暗い（使用中、グレー）
                Color newColor = i < remainingCount ? Color.white : tileInactiveColor;
                tiles[i].color = newColor;
                Debug.Log($"[PaddleCostBarUI] Tile {i}: remainingCount={remainingCount}, maxStrokes={maxStrokes}, activeCount={activeCount}, color={newColor}, material={(tiles[i].material != null ? tiles[i].material.name : "null")}");
            }
        }
    }

    /// <summary>
    /// タイルを再生成（MaxStrokesが変わった時）
    /// </summary>
    private void RegenerateTiles()
    {
        if (tileContainer == null || tilePrefab == null || strokeManager == null) return;

        // 既存のタイルを削除
        for (int i = tiles.Count - 1; i >= 0; i--)
        {
            if (tiles[i] != null)
            {
                Destroy(tiles[i].gameObject);
            }
        }
        tiles.Clear();

        // タイル表示枚数（最大5枚）
        int maxStrokes = strokeManager.MaxStrokes;
        int displayTileCount = Mathf.Min(maxStrokes, 5);

        // 新しい枚数でタイルを生成
        for (int i = 0; i < displayTileCount; i++)
        {
            GameObject tileObj = Instantiate(tilePrefab, tileContainer);
            tileObj.name = $"Tile_{i + 1}";
            Image img = tileObj.GetComponent<Image>();
            if (img != null)
            {
                tiles.Add(img);

                // グラデーションマテリアルを適用
                if (tileMaterial != null)
                {
                    img.material = new Material(tileMaterial);
                    img.material.SetColor("_ColorLeft", tileActiveColorLeft);
                    img.material.SetColor("_ColorRight", tileActiveColorRight);
                }

                img.color = tileInactiveColor;
            }
        }

        Debug.Log($"[PaddleCostBarUI] Regenerated {displayTileCount} tiles (MaxStrokes={maxStrokes}, Frame={Time.frameCount})");
    }

    /// <summary>
    /// タイルを初期化（既にHierarchyに存在するタイルを収集）
    /// </summary>
    private void InitializeTiles()
    {
        if (tileContainer == null) return;

        tiles.Clear();

        // 既存のタイルを収集
        for (int i = 0; i < tileContainer.childCount; i++)
        {
            Transform child = tileContainer.GetChild(i);
            Image img = child.GetComponent<Image>();
            if (img != null)
            {
                tiles.Add(img);
            }
        }

        Debug.Log($"[PaddleCostBarUI] Initialized {tiles.Count} stroke tiles.");
    }

    /// <summary>
    /// テキスト設定を適用
    /// </summary>
    private void ApplyTextSettings()
    {
        if (whiteBarText != null)
        {
            whiteBarText.color = textColor;
            whiteBarText.fontSize = fontSize;
        }

        if (redBarText != null)
        {
            redBarText.color = textColor;
            redBarText.fontSize = fontSize;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Inspectorから実行：UI階層構造を自動生成
    /// </summary>
    [ContextMenu("Setup Hierarchy")]
    private void SetupHierarchy()
    {
        Debug.Log("[PaddleCostBarUI] Setting up hierarchy...");

        // マテリアルとプレハブを作成
        CreateMaterialsAndPrefabs();

        // 白線バー作成
        CreateBarSection("WhiteBar", -10f, ref whiteBarFill, ref whiteBarText, whiteBarMaterial);

        // 赤線バー作成
        CreateBarSection("RedBar", -40f, ref redBarFill, ref redBarText, redBarMaterial);

        // 総本数バー作成
        CreateStrokeBarSection();

        // 参照を探す
        if (costManager == null)
        {
            costManager = FindFirstObjectByType<PaddleCostManager>();
            Debug.Log($"[PaddleCostBarUI] CostManager auto-assigned: {costManager != null}");
        }

        if (strokeManager == null)
        {
            strokeManager = FindFirstObjectByType<StrokeManager>();
            Debug.Log($"[PaddleCostBarUI] StrokeManager auto-assigned: {strokeManager != null}");
        }

        Debug.Log("[PaddleCostBarUI] Hierarchy setup complete!");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// バーセクション（白線・赤線）を作成
    /// </summary>
    private void CreateBarSection(string barName, float posY, ref Image fillField, ref TextMeshProUGUI textField, Material mat)
    {
        // バー親オブジェクト
        Transform barParent = transform.Find(barName);
        GameObject barObj;

        if (barParent != null)
        {
            barObj = barParent.gameObject;
            Debug.Log($"[PaddleCostBarUI] Found existing {barName}");
        }
        else
        {
            barObj = new GameObject(barName);
            barObj.transform.SetParent(transform, false);

            RectTransform barRect = barObj.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 1);
            barRect.anchorMax = new Vector2(0, 1);
            barRect.pivot = new Vector2(0, 1);
            barRect.anchoredPosition = new Vector2(0, posY);
            barRect.sizeDelta = new Vector2(200f, 20f);

            Debug.Log($"[PaddleCostBarUI] Created {barName}");
        }

        // Background
        Transform bgTransform = barObj.transform.Find("Background");
        if (bgTransform == null)
        {
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(barObj.transform, false);

            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.5f);

            Debug.Log($"[PaddleCostBarUI] Created {barName}/Background");
        }

        // FillBar
        Transform fillTransform = barObj.transform.Find("FillBar");
        GameObject fillObj;

        if (fillTransform != null)
        {
            fillObj = fillTransform.gameObject;
        }
        else
        {
            fillObj = new GameObject("FillBar");
            fillObj.transform.SetParent(barObj.transform, false);

            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            Debug.Log($"[PaddleCostBarUI] Created {barName}/FillBar");
        }

        Image fillImage = fillObj.GetComponent<Image>();
        if (fillImage == null)
        {
            fillImage = fillObj.AddComponent<Image>();
        }

        fillImage.type = Image.Type.Simple; // グラデーション対応のためSimpleに変更

        if (mat != null)
        {
            fillImage.material = mat;
        }

        fillField = fillImage;

        // ValueText
        Transform textTransform = barObj.transform.Find("ValueText");
        GameObject textObj;

        if (textTransform != null)
        {
            textObj = textTransform.gameObject;
        }
        else
        {
            textObj = new GameObject("ValueText");
            textObj.transform.SetParent(barObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(1, 0.5f);
            textRect.anchorMax = new Vector2(1, 0.5f);
            textRect.pivot = new Vector2(0, 0.5f);
            textRect.anchoredPosition = new Vector2(10f, 0);
            textRect.sizeDelta = new Vector2(80f, 20f);

            Debug.Log($"[PaddleCostBarUI] Created {barName}/ValueText");
        }

        TextMeshProUGUI tmpText = textObj.GetComponent<TextMeshProUGUI>();
        if (tmpText == null)
        {
            tmpText = textObj.AddComponent<TextMeshProUGUI>();
        }

        tmpText.text = "20.0/20.0";
        tmpText.fontSize = fontSize;
        tmpText.color = textColor;
        tmpText.alignment = TextAlignmentOptions.Left;

        // フォントアセットを探して割り当て
        var fontAsset = UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset NotoSansJP-Regular")
            .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path))
            .FirstOrDefault();

        if (fontAsset != null)
        {
            tmpText.font = fontAsset;
        }

        textField = tmpText;
    }

    /// <summary>
    /// 総本数バーセクションを作成
    /// </summary>
    private void CreateStrokeBarSection()
    {
        // StrokeBar親オブジェクト
        Transform strokeBarTransform = transform.Find("StrokeBar");
        GameObject strokeBarObj;

        if (strokeBarTransform != null)
        {
            strokeBarObj = strokeBarTransform.gameObject;
            Debug.Log("[PaddleCostBarUI] Found existing StrokeBar");
        }
        else
        {
            strokeBarObj = new GameObject("StrokeBar");
            strokeBarObj.transform.SetParent(transform, false);

            RectTransform strokeBarRect = strokeBarObj.AddComponent<RectTransform>();
            strokeBarRect.anchorMin = new Vector2(0, 1);
            strokeBarRect.anchorMax = new Vector2(0, 1);
            strokeBarRect.pivot = new Vector2(0, 1);
            strokeBarRect.anchoredPosition = new Vector2(0, -80f);
            strokeBarRect.sizeDelta = new Vector2(200f, 40f);

            Debug.Log("[PaddleCostBarUI] Created StrokeBar");
        }

        // TileContainer
        Transform containerTransform = strokeBarObj.transform.Find("TileContainer");
        GameObject containerObj;

        if (containerTransform != null)
        {
            containerObj = containerTransform.gameObject;
        }
        else
        {
            containerObj = new GameObject("TileContainer");
            containerObj.transform.SetParent(strokeBarObj.transform, false);

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup layout = containerObj.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 5f;

            Debug.Log("[PaddleCostBarUI] Created StrokeBar/TileContainer");
        }

        tileContainer = containerObj.transform;
    }

    /// <summary>
    /// マテリアルとプレハブを作成
    /// </summary>
    private void CreateMaterialsAndPrefabs()
    {
        // シェーダーを探す
        Shader gradientShader = Shader.Find("UI/HorizontalGradient");
        if (gradientShader == null)
        {
            Debug.LogWarning("[PaddleCostBarUI] UI/HorizontalGradient shader not found. Please import the shader first.");
            return;
        }

        // Materials フォルダを確保
        string materialsFolder = "Assets/Materials";
        if (!UnityEditor.AssetDatabase.IsValidFolder(materialsFolder))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
        }

        // WhiteBarGradient マテリアル
        string whiteMatPath = $"{materialsFolder}/WhiteBarGradient.mat";
        Material whiteMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(whiteMatPath);
        if (whiteMat == null)
        {
            whiteMat = new Material(gradientShader);
            whiteMat.SetColor("_ColorLeft", Color.white);
            whiteMat.SetColor("_ColorRight", whiteBarColorRight);
            UnityEditor.AssetDatabase.CreateAsset(whiteMat, whiteMatPath);
            Debug.Log($"[PaddleCostBarUI] Created {whiteMatPath}");
        }
        whiteBarMaterial = whiteMat;

        // RedBarGradient マテリアル
        string redMatPath = $"{materialsFolder}/RedBarGradient.mat";
        Material redMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(redMatPath);
        if (redMat == null)
        {
            redMat = new Material(gradientShader);
            redMat.SetColor("_ColorLeft", redBarColorLeft);
            redMat.SetColor("_ColorRight", redBarColorRight);
            UnityEditor.AssetDatabase.CreateAsset(redMat, redMatPath);
            Debug.Log($"[PaddleCostBarUI] Created {redMatPath}");
        }
        redBarMaterial = redMat;

        // TileGradient マテリアル
        string tileMatPath = $"{materialsFolder}/TileGradient.mat";
        Material tileMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(tileMatPath);
        if (tileMat == null)
        {
            tileMat = new Material(gradientShader);
            tileMat.SetColor("_ColorLeft", tileActiveColorLeft);
            tileMat.SetColor("_ColorRight", tileActiveColorRight);
            UnityEditor.AssetDatabase.CreateAsset(tileMat, tileMatPath);
            Debug.Log($"[PaddleCostBarUI] Created {tileMatPath}");
        }
        tileMaterial = tileMat;

        // StrokeTile プレハブ
        string prefabsFolder = "Assets/Prefabs/UI";
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!UnityEditor.AssetDatabase.IsValidFolder(prefabsFolder))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }

        string tilePrefabPath = $"{prefabsFolder}/StrokeTile.prefab";
        GameObject tilePrefabObj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(tilePrefabPath);
        if (tilePrefabObj == null)
        {
            GameObject tileTemp = new GameObject("StrokeTile");
            RectTransform tileRect = tileTemp.AddComponent<RectTransform>();
            tileRect.sizeDelta = new Vector2(50f, 15f); // 横長（線をイメージ）

            Image tileImage = tileTemp.AddComponent<Image>();
            tileImage.color = Color.white;

            tilePrefabObj = UnityEditor.PrefabUtility.SaveAsPrefabAsset(tileTemp, tilePrefabPath);
            DestroyImmediate(tileTemp);
            Debug.Log($"[PaddleCostBarUI] Created {tilePrefabPath}");
        }
        tilePrefab = tilePrefabObj;

        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
    }

    /// <summary>
    /// Inspectorから実行：ストロークタイルを自動生成
    /// </summary>
    [ContextMenu("Generate Stroke Tiles")]
    private void GenerateStrokeTiles()
    {
        if (tileContainer == null)
        {
            Debug.LogError("[PaddleCostBarUI] TileContainer is not assigned.");
            return;
        }

        if (tilePrefab == null)
        {
            Debug.LogError("[PaddleCostBarUI] TilePrefab is not assigned.");
            return;
        }

        if (strokeManager == null)
        {
            strokeManager = FindFirstObjectByType<StrokeManager>();
            if (strokeManager == null)
            {
                Debug.LogError("[PaddleCostBarUI] StrokeManager not found.");
                return;
            }
        }

        // 既存のタイルを削除
        while (tileContainer.childCount > 0)
        {
            DestroyImmediate(tileContainer.GetChild(0).gameObject);
        }

        tiles.Clear();

        // MaxStrokesの数だけタイルを生成
        int maxStrokes = strokeManager.MaxStrokes;
        for (int i = 0; i < maxStrokes; i++)
        {
            GameObject tileObj = Instantiate(tilePrefab, tileContainer);
            tileObj.name = $"Tile_{i + 1}";
            Image img = tileObj.GetComponent<Image>();
            if (img != null)
            {
                tiles.Add(img);
                img.color = tileInactiveColor; // 初期状態は非アクティブ色
            }
        }

        Debug.Log($"[PaddleCostBarUI] Generated {maxStrokes} stroke tiles.");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Inspectorから実行：マテリアルカラーを再適用
    /// </summary>
    [ContextMenu("Refresh Material Colors")]
    private void RefreshMaterialColors()
    {
        if (whiteBarFill != null && whiteBarFill.material != null)
        {
            whiteBarFill.material.SetColor("_ColorLeft", whiteBarColorLeft);
            whiteBarFill.material.SetColor("_ColorRight", whiteBarColorRight);
            Debug.Log("[PaddleCostBarUI] White bar material colors refreshed.");
        }

        if (redBarFill != null && redBarFill.material != null)
        {
            redBarFill.material.SetColor("_ColorLeft", redBarColorLeft);
            redBarFill.material.SetColor("_ColorRight", redBarColorRight);
            Debug.Log("[PaddleCostBarUI] Red bar material colors refreshed.");
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
