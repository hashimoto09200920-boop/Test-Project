using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Progress;
using Game.Gems;
using Game.Skills;

/// <summary>
/// Pixel Dancer HP と Floor HP を HUD 上に表示するUI
/// SlowMotionGauge の上に配置（PixelDancer行 → Floor行 → Gauge の順）
/// Editor拡張でPlay前にHierarchy生成、Inspector調整可能
/// </summary>
public class HPStatusHUDUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PixelDancerControllerへの参照（空欄でAutoFind）")]
    [SerializeField] private PixelDancerController pixelDancer;

    [Tooltip("FloorHealthへの参照（空欄でAutoFind）")]
    [SerializeField] private FloorHealth floorHealth;

    [Header("HP Row: Pixel Dancer")]
    [Tooltip("PixelDancerアイコン画像（InspectorでSpriteを割り当て）")]
    [SerializeField] private Image pixelDancerIcon;

    [Tooltip("PixelDancer HP テキスト")]
    [SerializeField] private TextMeshProUGUI pixelDancerHPText;

    [Header("HP Row: Floor")]
    [Tooltip("Floor アイコン画像（InspectorでSpriteを割り当て）")]
    [SerializeField] private Image floorIcon;

    [Tooltip("Floor HP テキスト")]
    [SerializeField] private TextMeshProUGUI floorHPText;


    [Header("Layout Settings")]
    [Tooltip("HUD全体の位置（Anchored Position、左上アンカー基準）")]
    [SerializeField] private Vector2 hudPosition = new Vector2(145f, -110f);

    [Tooltip("PixelDancer行の高さ（px）")]
    [SerializeField] private float pixelDancerRowHeight = 60f;

    [Tooltip("Floor行の高さ（px）")]
    [SerializeField] private float floorRowHeight = 90f;

    [Tooltip("行間スペース（px）")]
    [SerializeField] private float rowSpacing = 5f;

    [Tooltip("HUD全体の幅（px）")]
    [SerializeField] private float rowWidth = 160f;

    [Tooltip("アイコンサイズ（px）—Inspectorで画像と合わせて調整")]
    [SerializeField] private float iconSize = 60f;

    [Tooltip("テキストフォントサイズ")]
    [SerializeField] private float fontSize = 14f;

    // ========== Public API ==========

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    // ========== Lifecycle ==========

    private void Start()
    {
        if (pixelDancer == null)
            pixelDancer = FindFirstObjectByType<PixelDancerController>();
        if (floorHealth == null)
            floorHealth = FindFirstObjectByType<FloorHealth>();

        UpdateDisplay();
    }

    private void Update()
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (pixelDancerHPText != null)
        {
            if (pixelDancer != null)
                pixelDancerHPText.text = $"{pixelDancer.CurrentHP}/{pixelDancer.MaxHP}";
            else
            {
                int max = CalcEffectiveHP(PixelDancerController.SavedInitialHP, SkillEffectType.PixelDancerHPUp);
                pixelDancerHPText.text = $"{max}/{max}";
            }
        }

        if (floorHPText != null)
        {
            if (floorHealth != null)
                floorHPText.text = $"{floorHealth.CurrentHP}/{floorHealth.MaxHP}";
            else
            {
                int max = CalcEffectiveHP(FloorHealth.SavedMaxHP, SkillEffectType.FloorHPUp);
                floorHPText.text = $"{max}/{max}";
            }
        }
    }

    /// <summary>
    /// 装備中ジェムのHPアップスキルを加算した実効HP値を返す（03_AreaSelect用フォールバック）
    /// </summary>
    private static int CalcEffectiveHP(int baseHP, SkillEffectType targetType)
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null) return baseHP;

        float additive = 0f;
        float multiplier = 1f;
        int usedSlots = 0;

        foreach (int idx in data.equippedGemIndices)
        {
            if (idx < 0 || idx >= data.gemInventory.Count) continue;
            var gemInst = data.gemInventory[idx];
            var gemDef = GemManager.Instance?.LoadGemDefinition(gemInst);
            if (gemDef == null) continue;
            if (usedSlots + gemDef.requiredSlots > data.slotLevel) continue;
            usedSlots += gemDef.requiredSlots;

            AccumulateSkillEffect(GemManager.Instance?.LoadBaseSkill(gemInst), targetType, ref additive, ref multiplier);
            AccumulateSkillEffect(GemManager.Instance?.LoadBonusSkill1(gemInst), targetType, ref additive, ref multiplier);
            AccumulateSkillEffect(GemManager.Instance?.LoadBonusSkill2(gemInst), targetType, ref additive, ref multiplier);
        }

        int result = Mathf.RoundToInt((baseHP + additive) * multiplier);
        return Mathf.Max(1, result);
    }

    private static void AccumulateSkillEffect(SkillDefinition skill, SkillEffectType targetType,
        ref float additive, ref float multiplier)
    {
        if (skill == null || skill.effectType != targetType) return;
        if (skill.isMultiplier)
            multiplier *= skill.effectValue;
        else
            additive += skill.effectValue;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Inspectorから実行：HP表示HUDのHierarchyを生成
    /// </summary>
    [ContextMenu("Setup HP Status HUD")]
    private void SetupHPStatusHUD()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[HPStatusHUDUI] Canvas not found!");
            return;
        }

        // 既存のHPStatusHUDを削除して再生成
        Transform existing = canvas.transform.Find("HPStatusHUD");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
            Debug.Log("[HPStatusHUDUI] Existing HPStatusHUD removed.");
        }

        float totalHeight = pixelDancerRowHeight + rowSpacing + floorRowHeight;

        // === HPStatusHUD 親オブジェクト ===
        GameObject hudParent = new GameObject("HPStatusHUD");
        hudParent.transform.SetParent(canvas.transform, false);

        RectTransform parentRect = hudParent.AddComponent<RectTransform>();
        parentRect.anchorMin = new Vector2(0f, 1f);
        parentRect.anchorMax = new Vector2(0f, 1f);
        parentRect.pivot = new Vector2(0.5f, 0.5f);
        parentRect.sizeDelta = new Vector2(rowWidth, totalHeight);
        parentRect.anchoredPosition = hudPosition;

        VerticalLayoutGroup vlg = hudParent.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.spacing = rowSpacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        // === PixelDancer HP 行（上） ===
        Image pdIconRef = null;
        TextMeshProUGUI pdTextRef = null;
        CreateHPRow(hudParent.transform, "PixelDancerHPRow", pixelDancerRowHeight, ref pdIconRef, ref pdTextRef);

        // === Floor HP 行（下） ===
        Image floorIconRef = null;
        TextMeshProUGUI floorTextRef = null;
        CreateHPRow(hudParent.transform, "FloorHPRow", floorRowHeight, ref floorIconRef, ref floorTextRef);

        // SerializedObject で参照を保存
        UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("pixelDancerIcon").objectReferenceValue = pdIconRef;
        so.FindProperty("pixelDancerHPText").objectReferenceValue = pdTextRef;
        so.FindProperty("floorIcon").objectReferenceValue = floorIconRef;
        so.FindProperty("floorHPText").objectReferenceValue = floorTextRef;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(hudParent);

        Debug.Log("[HPStatusHUDUI] Setup complete! HPStatusHUD を Canvas 直下に生成しました。\n" +
                  "次の作業: PixelDancerIcon / FloorIcon の Sprite を Inspector で割り当ててください。");
    }

    /// <summary>
    /// HP表示行（Icon + HPText）を生成
    /// </summary>
    private void CreateHPRow(Transform parent, string rowName, float rowH,
                              ref Image iconRef, ref TextMeshProUGUI textRef)
    {
        float textWidth = rowWidth - iconSize - 5f;

        GameObject row = new GameObject(rowName);
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(rowWidth, rowH);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 5f;
        hlg.padding = new RectOffset(0, 0, 0, 0);

        // --- アイコン ---
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(row.transform, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);

        iconRef = iconObj.AddComponent<Image>();
        iconRef.color = Color.white;
        iconRef.preserveAspect = true;

        // --- HP テキスト ---
        GameObject textObj = new GameObject("HPText");
        textObj.transform.SetParent(row.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(textWidth, rowH);

        textRef = textObj.AddComponent<TextMeshProUGUI>();
        textRef.fontSize = fontSize;
        textRef.color = Color.white;
        textRef.alignment = TextAlignmentOptions.MidlineLeft;
        textRef.text = "0/0";
    }

    /// <summary>
    /// Inspectorから実行：hudPosition等の設定値をHPStatusHUDのRectTransformに反映する
    /// （OnValidateを使わないことで、Play前後の値の巻き戻しを防止）
    /// </summary>
    [ContextMenu("Apply Layout Settings")]
    private void ApplyLayoutSettings()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[HPStatusHUDUI] Canvas not found!");
            return;
        }

        Transform hudParent = canvas.transform.Find("HPStatusHUD");
        if (hudParent == null)
        {
            Debug.LogWarning("[HPStatusHUDUI] HPStatusHUD が見つかりません。先に 'Setup HP Status HUD' を実行してください。");
            return;
        }

        float totalHeight = pixelDancerRowHeight + rowSpacing + floorRowHeight;

        RectTransform parentRect = hudParent.GetComponent<RectTransform>();
        if (parentRect != null)
        {
            UnityEditor.Undo.RecordObject(parentRect, "Apply HP HUD Layout");
            parentRect.anchoredPosition = hudPosition;
            parentRect.sizeDelta = new Vector2(rowWidth, totalHeight);
            UnityEditor.EditorUtility.SetDirty(parentRect);
        }

        // PixelDancerHPRow
        ApplyRowLayout(hudParent, "PixelDancerHPRow", pixelDancerRowHeight);

        // FloorHPRow
        ApplyRowLayout(hudParent, "FloorHPRow", floorRowHeight);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hudParent.gameObject.scene);
        Debug.Log($"[HPStatusHUDUI] Layout applied. Position={hudPosition}, ParentSize=({rowWidth}, {totalHeight})");
    }

    private void ApplyRowLayout(Transform hudParent, string rowName, float rowH)
    {
        Transform row = hudParent.Find(rowName);
        if (row == null) return;

        RectTransform rowRect = row.GetComponent<RectTransform>();
        if (rowRect != null)
        {
            UnityEditor.Undo.RecordObject(rowRect, "Apply HP HUD Layout");
            rowRect.sizeDelta = new Vector2(rowWidth, rowH);
            UnityEditor.EditorUtility.SetDirty(rowRect);
        }

        RectTransform iconRect = row.Find("Icon")?.GetComponent<RectTransform>();
        if (iconRect != null)
        {
            UnityEditor.Undo.RecordObject(iconRect, "Apply HP HUD Layout");
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            UnityEditor.EditorUtility.SetDirty(iconRect);
        }

        RectTransform textRect = row.Find("HPText")?.GetComponent<RectTransform>();
        if (textRect != null)
        {
            UnityEditor.Undo.RecordObject(textRect, "Apply HP HUD Layout");
            float textWidth = rowWidth - iconSize - 5f;
            textRect.sizeDelta = new Vector2(textWidth, rowH);
            UnityEditor.EditorUtility.SetDirty(textRect);
        }
    }
#endif
}
