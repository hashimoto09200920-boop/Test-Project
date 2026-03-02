using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Progress;
using Game.Gems;
using Game.Skills;

/// <summary>
/// AreaSelectシーンでジェムの管理（装備/解除/売却）を行うオーバーレイUI
/// PauseMenuUI方式：[ContextMenu("Setup Gem Management UI")] でHierarchyを自動生成
/// AreaSelectにあるボタンの onClick から Open() を呼ぶ
/// </summary>
public class GemManagementUI : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject dimPanel;
    [SerializeField] private GameObject gemPanel;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI slotLevelText;   // "スロット使用: 2 / 5"
    [SerializeField] private Button closeButton;

    [Header("Gem List")]
    [SerializeField] private Transform gemListContainer;      // ScrollView > Viewport > Content
    [SerializeField] private GameObject gemItemTemplate;      // 非表示テンプレートオブジェクト

    [Header("Empty Message")]
    [SerializeField] private TextMeshProUGUI emptyMessageText;

    // ========== Runtime State ==========
    private readonly List<GameObject> gemItemObjects = new List<GameObject>();

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        HideAllPanels();
    }

    // ========== Public API ==========

    /// <summary>ジェム管理パネルを開く</summary>
    public void Open()
    {
        if (dimPanel != null) dimPanel.SetActive(true);
        if (gemPanel != null) gemPanel.SetActive(true);
        RefreshGemList();
    }

    /// <summary>ジェム管理パネルを閉じる</summary>
    public void Close()
    {
        HideAllPanels();
    }

    private void HideAllPanels()
    {
        if (dimPanel != null) dimPanel.SetActive(false);
        if (gemPanel != null) gemPanel.SetActive(false);
    }

    // ========== Gem List ==========

    private void RefreshGemList()
    {
        if (gemListContainer == null)
        {
            Debug.LogError("[GemManagementUI] gemListContainer is null! Re-run Setup Gem Management UI.");
            return;
        }

        // 既存アイテムをクリア
        foreach (var obj in gemItemObjects)
        {
            if (obj != null) Destroy(obj);
        }
        gemItemObjects.Clear();

        var data = ProgressManager.Instance?.Data;
        if (data == null) { Debug.LogError("[GemManagementUI] ProgressManager data is null!"); return; }

        Debug.Log($"[GemManagementUI] RefreshGemList: inventory={data.gemInventory.Count}, container={gemListContainer.name}, containerActive={gemListContainer.gameObject.activeInHierarchy}");

        // スロット使用状況を更新
        int usedSlots = CalcUsedSlots(data);
        if (slotLevelText != null)
            slotLevelText.text = $"スロット使用: {usedSlots} / {data.slotLevel}";

        // 空メッセージ表示制御
        bool isEmpty = data.gemInventory.Count == 0;
        if (emptyMessageText != null) emptyMessageText.gameObject.SetActive(isEmpty);

        // ジェムアイテムを生成
        for (int i = 0; i < data.gemInventory.Count; i++)
        {
            CreateGemItem(data.gemInventory[i], i, data);
        }

        Debug.Log($"[GemManagementUI] Created {gemItemObjects.Count} items. Container childCount={gemListContainer.childCount}");

        // レイアウト即時再計算（ContentSizeFitterが次フレームまで待つのを防ぐ）
        Canvas.ForceUpdateCanvases();
        var contentRect = gemListContainer.GetComponent<RectTransform>();
        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Debug.Log($"[GemManagementUI] Content rect after rebuild: size={contentRect.rect.size}, pos={contentRect.anchoredPosition}");
            if (gemItemObjects.Count > 0)
            {
                var firstItem = gemItemObjects[0].GetComponent<RectTransform>();
                if (firstItem != null)
                    Debug.Log($"[GemManagementUI] First item: anchoredPos={firstItem.anchoredPosition}, size={firstItem.sizeDelta}, active={gemItemObjects[0].activeInHierarchy}");
            }
        }
    }

    private int CalcUsedSlots(ProgressData data)
    {
        int total = 0;
        foreach (int idx in data.equippedGemIndices)
        {
            if (idx < 0 || idx >= data.gemInventory.Count) continue;
            var gemDef = GemManager.Instance?.LoadGemDefinition(data.gemInventory[idx]);
            if (gemDef != null) total += gemDef.requiredSlots;
        }
        return total;
    }

    private void CreateGemItem(GemInstance gemInst, int inventoryIdx, ProgressData data)
    {
        GameObject itemObj;
        if (gemItemTemplate != null)
        {
            itemObj = Instantiate(gemItemTemplate, gemListContainer);
            itemObj.SetActive(true);
        }
        else
        {
            itemObj = CreateDefaultGemItem();
            itemObj.transform.SetParent(gemListContainer, false);
        }

        var gemDef = GemManager.Instance?.LoadGemDefinition(gemInst);
        bool isEquipped = data.equippedGemIndices.Contains(inventoryIdx);

        // テキスト参照
        var nameText       = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        var infoText       = itemObj.transform.Find("InfoText")?.GetComponent<TextMeshProUGUI>();
        var equipBtn       = itemObj.transform.Find("EquipButton")?.GetComponent<Button>();
        var equipBtnText   = itemObj.transform.Find("EquipButton/Text")?.GetComponent<TextMeshProUGUI>();
        var sellBtn        = itemObj.transform.Find("SellButton")?.GetComponent<Button>();
        var sellBtnText    = itemObj.transform.Find("SellButton/Text")?.GetComponent<TextMeshProUGUI>();
        var equippedBadge  = itemObj.transform.Find("EquippedBadge")?.gameObject;

        if (gemDef != null)
        {
            if (nameText != null)
                nameText.text = $"{gemDef.gemName}  [スロット×{gemDef.requiredSlots}]";

            // スキル情報テキスト
            string baseSkillName = gemDef.baseSkill != null ? gemDef.baseSkill.skillName : "（なし）";
            string bonus1 = string.IsNullOrEmpty(gemInst.bonusSkill1Name)
                ? "" : $"\n  ＋ {GetSkillDisplayName(gemInst.bonusSkill1Name)}";
            string bonus2 = string.IsNullOrEmpty(gemInst.bonusSkill2Name)
                ? "" : $"\n  ＋ {GetSkillDisplayName(gemInst.bonusSkill2Name)}";
            if (infoText != null)
                infoText.text = $"基本：{baseSkillName}{bonus1}{bonus2}";

            if (sellBtnText != null)
                sellBtnText.text = $"売却 ¥{gemDef.sellPrice}";
        }
        else
        {
            if (nameText != null) nameText.text = "（データ読み込み失敗）";
            if (infoText != null) infoText.text = "";
        }

        // 装備中バッジ
        if (equippedBadge != null) equippedBadge.SetActive(isEquipped);
        if (equipBtnText != null) equipBtnText.text = isEquipped ? "解除" : "装備";

        // ボタンイベント（インデックスをキャプチャ）
        int capturedIdx = inventoryIdx;
        if (equipBtn != null)
        {
            equipBtn.onClick.RemoveAllListeners();
            equipBtn.onClick.AddListener(() => OnEquipToggle(capturedIdx));
        }
        if (sellBtn != null)
        {
            sellBtn.onClick.RemoveAllListeners();
            sellBtn.onClick.AddListener(() => OnSell(capturedIdx));
        }

        gemItemObjects.Add(itemObj);
    }

    private string GetSkillDisplayName(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return "";
        var skill = Resources.Load<SkillDefinition>($"GameData/Skills/{assetName}");
        return skill != null ? skill.skillName : assetName;
    }

    // ========== Equip / Sell ==========

    private void OnEquipToggle(int inventoryIdx)
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null) return;

        if (data.equippedGemIndices.Contains(inventoryIdx))
        {
            // 解除
            data.equippedGemIndices.Remove(inventoryIdx);
        }
        else
        {
            // スロット上限チェックしてから装備
            var gemInst = data.gemInventory[inventoryIdx];
            var gemDef = GemManager.Instance?.LoadGemDefinition(gemInst);
            if (gemDef == null) return;

            int usedSlots = CalcUsedSlots(data);
            if (usedSlots + gemDef.requiredSlots > data.slotLevel)
            {
                Debug.Log($"[GemManagementUI] スロット不足 ({usedSlots}+{gemDef.requiredSlots} > {data.slotLevel})");
                // TODO: スロット不足メッセージを表示（Phase 9拡張）
                return;
            }
            data.equippedGemIndices.Add(inventoryIdx);
        }

        ProgressManager.Instance.Save();
        RefreshGemList();
    }

    private void OnSell(int inventoryIdx)
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null) return;
        if (inventoryIdx < 0 || inventoryIdx >= data.gemInventory.Count) return;

        var gemInst = data.gemInventory[inventoryIdx];
        var gemDef = GemManager.Instance?.LoadGemDefinition(gemInst);

        // 装備中なら解除
        data.equippedGemIndices.Remove(inventoryIdx);

        // 売却金額をgoldに加算
        if (gemDef != null)
            ProgressManager.Instance.AddGold(gemDef.sellPrice);

        // インベントリから削除
        data.gemInventory.RemoveAt(inventoryIdx);

        // equippedGemIndicesのインデックスを修正（削除による番号ずれ対策）
        for (int i = 0; i < data.equippedGemIndices.Count; i++)
        {
            if (data.equippedGemIndices[i] > inventoryIdx)
                data.equippedGemIndices[i]--;
        }

        ProgressManager.Instance.Save();
        RefreshGemList();
    }

    // ========== Default Gem Item Generator (Fallback) ==========

    private GameObject CreateDefaultGemItem()
    {
        // ルートアイテム
        var itemObj = new GameObject("GemItem");
        var itemRect = itemObj.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(600f, 110f);

        var itemLayout = itemObj.AddComponent<HorizontalLayoutGroup>();
        itemLayout.spacing = 8f;
        itemLayout.padding = new RectOffset(10, 10, 8, 8);
        itemLayout.childAlignment = TextAnchor.MiddleLeft;
        itemLayout.childControlWidth = false;
        itemLayout.childControlHeight = true;
        itemLayout.childForceExpandWidth = false;
        itemLayout.childForceExpandHeight = true;

        var itemBg = itemObj.AddComponent<Image>();
        itemBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var itemLE = itemObj.AddComponent<LayoutElement>();
        itemLE.preferredHeight = 110f;

        // テキストコンテナ
        var textContainer = new GameObject("TextContainer");
        textContainer.transform.SetParent(itemObj.transform, false);

        var tcRect = textContainer.AddComponent<RectTransform>();
        tcRect.sizeDelta = new Vector2(380f, 100f);

        var tcLayout = textContainer.AddComponent<VerticalLayoutGroup>();
        tcLayout.spacing = 4f;
        tcLayout.childAlignment = TextAnchor.UpperLeft;
        tcLayout.childControlWidth = true;
        tcLayout.childControlHeight = false;
        tcLayout.childForceExpandWidth = true;

        var tcLE = textContainer.AddComponent<LayoutElement>();
        tcLE.preferredWidth = 380f;

        // 名前テキスト
        var nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(textContainer.transform, false);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.fontSize = 18f;
        nameTMP.color = Color.white;
        nameTMP.fontStyle = FontStyles.Bold;
        var nameLE = nameObj.AddComponent<LayoutElement>();
        nameLE.preferredHeight = 26f;

        // 装備中バッジ（★ の代わりに小さいラベル）
        var badgeObj = new GameObject("EquippedBadge");
        badgeObj.transform.SetParent(textContainer.transform, false);
        var badgeTMP = badgeObj.AddComponent<TextMeshProUGUI>();
        badgeTMP.text = "[ 装備中 ]";
        badgeTMP.fontSize = 14f;
        badgeTMP.color = new Color(1f, 0.3f, 0.3f, 1f);
        var badgeLE = badgeObj.AddComponent<LayoutElement>();
        badgeLE.preferredHeight = 18f;
        badgeObj.SetActive(false);

        // スキル情報テキスト
        var infoObj = new GameObject("InfoText");
        infoObj.transform.SetParent(textContainer.transform, false);
        var infoTMP = infoObj.AddComponent<TextMeshProUGUI>();
        infoTMP.fontSize = 14f;
        infoTMP.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        var infoLE = infoObj.AddComponent<LayoutElement>();
        infoLE.preferredHeight = 56f;

        // ボタンコンテナ
        var btnContainer = new GameObject("ButtonContainer");
        btnContainer.transform.SetParent(itemObj.transform, false);

        var bcRect = btnContainer.AddComponent<RectTransform>();
        bcRect.sizeDelta = new Vector2(190f, 100f);

        var bcLayout = btnContainer.AddComponent<VerticalLayoutGroup>();
        bcLayout.spacing = 8f;
        bcLayout.childAlignment = TextAnchor.MiddleCenter;
        bcLayout.childControlWidth = true;
        bcLayout.childControlHeight = false;
        bcLayout.childForceExpandWidth = true;

        var bcLE = btnContainer.AddComponent<LayoutElement>();
        bcLE.preferredWidth = 190f;

        // 装備/解除ボタン
        CreateItemButton(btnContainer.transform, "EquipButton", "装備", 44f);

        // 売却ボタン
        CreateItemButton(btnContainer.transform, "SellButton", "売却", 44f);

        return itemObj;
    }

    private Button CreateItemButton(Transform parent, string name, string label, float height)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(180f, height);

        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.25f, 0.25f, 0.35f, 1f);

        var btn = btnObj.AddComponent<Button>();

        var le = btnObj.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    // ========== Editor Auto-Setup ==========

#if UNITY_EDITOR
    [ContextMenu("Setup Gem Management UI")]
    private void SetupGemManagementUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        CreateDimPanel(canvas.transform);
        CreateGemPanel(canvas.transform);

        // SerializedObjectでInspector参照を設定
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("dimPanel").objectReferenceValue = dimPanel;
        so.FindProperty("gemPanel").objectReferenceValue = gemPanel;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("slotLevelText").objectReferenceValue = slotLevelText;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("gemListContainer").objectReferenceValue = gemListContainer;
        so.FindProperty("emptyMessageText").objectReferenceValue = emptyMessageText;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[GemManagementUI] Setup complete! Add a button to AreaSelect and call Open() from onClick.");
    }

    [ContextMenu("Debug: Add Test Gems (Area01-09)")]
    private void AddTestGems()
    {
        if (ProgressManager.Instance == null || GemManager.Instance == null)
        {
            Debug.LogError("[GemManagementUI] ProgressManager or GemManager not found. Run in Play mode.");
            return;
        }

        int added = 0;
        for (int i = 1; i <= 9; i++)
        {
            string areaId = $"Area_{i:D2}";
            if (GemManager.Instance.TryAddGemForArea(areaId, out _))
                added++;
            else
                Debug.LogWarning($"[GemManagementUI] Failed to add gem for {areaId} (inventory full or no definition)");
        }

        ProgressManager.Instance.Save();
        RefreshGemList();
        Debug.Log($"[GemManagementUI] Added {added}/9 test gems.");
    }

    private void CreateDimPanel(Transform parent)
    {
        var dimObj = new GameObject("GemDimPanel");
        dimObj.transform.SetParent(parent, false);
        var rect = dimObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        var img = dimObj.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.75f);
        dimPanel = dimObj;
        dimObj.SetActive(false);
    }

    private void CreateGemPanel(Transform parent)
    {
        // メインパネル
        var panelObj = new GameObject("GemManagementPanel");
        panelObj.transform.SetParent(parent, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(680f, 700f);
        panelRect.anchoredPosition = Vector2.zero;
        var panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);

        var panelLayout = panelObj.AddComponent<VerticalLayoutGroup>();
        panelLayout.spacing = 10f;
        panelLayout.padding = new RectOffset(20, 20, 16, 16);
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        gemPanel = panelObj;

        // タイトル行（タイトル + 閉じるボタン）
        CreateHeaderRow(panelObj.transform);

        // タイトル下区切り線
        CreateSeparator(panelObj.transform);

        // スロット表示
        var slotObj = new GameObject("SlotLevelText");
        slotObj.transform.SetParent(panelObj.transform, false);
        slotLevelText = slotObj.AddComponent<TextMeshProUGUI>();
        slotLevelText.text = "スロット使用: 0 / 1";
        slotLevelText.fontSize = 20f;
        slotLevelText.alignment = TextAlignmentOptions.Center;
        slotLevelText.color = new Color(0.7f, 0.9f, 1f, 1f);
        var slotLE = slotObj.AddComponent<LayoutElement>();
        slotLE.preferredHeight = 28f;

        // 区切り線
        CreateSeparator(panelObj.transform);

        // ScrollView
        CreateScrollView(panelObj.transform);

        panelObj.SetActive(false);
    }

    private void CreateHeaderRow(Transform parent)
    {
        var headerObj = new GameObject("HeaderRow");
        headerObj.transform.SetParent(parent, false);
        // HorizontalLayoutGroupを使わずアンカーベースの絶対配置にする
        var headerLE = headerObj.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 50f;

        // タイトルテキスト（左端〜右端140px手前まで横幅をストレッチ）
        var titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(headerObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = new Vector2(-140f, 0f);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "GEM MANAGEMENT";
        titleText.fontSize = 30f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.color = Color.white;
        titleText.enableWordWrapping = false;

        // 閉じるボタン（右端アンカー・縦央揃え）
        var closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        var closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(1f, 0.5f);
        closeBtnRect.anchorMax = new Vector2(1f, 0.5f);
        closeBtnRect.sizeDelta = new Vector2(120f, 46f);
        closeBtnRect.anchoredPosition = new Vector2(-65f, 0f);
        var closeBtnImg = closeBtnObj.AddComponent<Image>();
        closeBtnImg.color = new Color(0.4f, 0.15f, 0.15f, 1f);
        closeButton = closeBtnObj.AddComponent<Button>();

        var closeBtnTextObj = new GameObject("Text");
        closeBtnTextObj.transform.SetParent(closeBtnObj.transform, false);
        var closeBtnTextRect = closeBtnTextObj.AddComponent<RectTransform>();
        closeBtnTextRect.anchorMin = Vector2.zero;
        closeBtnTextRect.anchorMax = Vector2.one;
        closeBtnTextRect.sizeDelta = Vector2.zero;
        var closeBtnTMP = closeBtnTextObj.AddComponent<TextMeshProUGUI>();
        closeBtnTMP.text = "閉じる";
        closeBtnTMP.fontSize = 20f;
        closeBtnTMP.alignment = TextAlignmentOptions.Center;
        closeBtnTMP.color = Color.white;
    }

    private void CreateSeparator(Transform parent)
    {
        var sepObj = new GameObject("Separator");
        sepObj.transform.SetParent(parent, false);
        var sepImg = sepObj.AddComponent<Image>();
        sepImg.color = new Color(0.3f, 0.3f, 0.4f, 1f);
        var sepLE = sepObj.AddComponent<LayoutElement>();
        sepLE.preferredHeight = 2f;
    }

    private void CreateScrollView(Transform parent)
    {
        // ScrollView
        var scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(parent, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.sizeDelta = new Vector2(640f, 520f);
        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        var scrollLE = scrollObj.AddComponent<LayoutElement>();
        scrollLE.preferredHeight = 520f;
        scrollLE.flexibleHeight = 1f;

        // Viewport
        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        viewportObj.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        var contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;

        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = viewportRect;

        // 空メッセージ（Contentの先頭に配置 → ScrollView内でスクロール範囲に含まれる）
        var emptyObj = new GameObject("EmptyMessage");
        emptyObj.transform.SetParent(contentObj.transform, false);
        emptyMessageText = emptyObj.AddComponent<TextMeshProUGUI>();
        emptyMessageText.text = "所持ジェムがありません\nArea のStage3をクリアしてジェムを入手しましょう";
        emptyMessageText.fontSize = 18f;
        emptyMessageText.alignment = TextAlignmentOptions.Center;
        emptyMessageText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        var emptyLE = emptyObj.AddComponent<LayoutElement>();
        emptyLE.preferredHeight = 80f;
        emptyObj.SetActive(false);

        gemListContainer = contentObj.transform;
    }
#endif
}
