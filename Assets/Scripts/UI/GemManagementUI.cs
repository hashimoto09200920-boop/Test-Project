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

    [Header("Display Settings")]
    [SerializeField] private string slotDisplayFormat = "スロット×{0}";
    [SerializeField] private float skillIconSize = 40f;
    [Tooltip("アイコン列の固定幅（全スキルでテキスト開始位置を揃える）\n最大アイコン幅(80px)より少し大きい値を設定")]
    [SerializeField] private float iconColumnWidth = 90f;
    [Tooltip("スキル行の統一高さ（最大アイコン高さ=A10の70pxを基準）\nアイコン画像サイズはそのまま、行間を均一にする")]
    [SerializeField] private float skillRowHeight = 70f;

    [Header("Skill Category Colors")]
    [Tooltip("スキル行の背景グラデーション開始色 - CategoryA（左端）")]
    [SerializeField] private Color categoryAColor = new Color(0.15f, 0.25f, 0.45f, 0.85f);
    [Tooltip("スキル行の背景グラデーション開始色 - CategoryB（左端）")]
    [SerializeField] private Color categoryBColor = new Color(0.15f, 0.38f, 0.22f, 0.85f);
    [Tooltip("スキル行の背景グラデーション開始色 - CategoryC（左端）")]
    [SerializeField] private Color categoryCColor = new Color(0.45f, 0.18f, 0.15f, 0.85f);
    [Tooltip("スキル行の背景グラデーション終端色（右端）\n全カテゴリ共通")]
    [SerializeField] private Color gradientEndColor = new Color(0.05f, 0.05f, 0.08f, 0.7f);
    [Tooltip("ONにするとグラデーション、OFFにするとカテゴリカラーの単色")]
    [SerializeField] private bool useGradientBackground = true;

    [Header("SE")]
    [SerializeField] private AudioClip equipSE;
    [SerializeField] private AudioClip unequipSE;
    [SerializeField] private AudioClip sellSE;
    [SerializeField] private AudioClip slotFullSE;

    [Header("Skill Preview HUD")]
    [SerializeField] private GemSkillPreviewHUD gemSkillPreviewHUD;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private Button debugAddGemsButton;

    [Header("Sell Confirmation Dialog")]
    [SerializeField] private GameObject sellConfirmPanel;
    [SerializeField] private TextMeshProUGUI sellConfirmText;
    [SerializeField] private Button sellConfirmYesBtn;
    [SerializeField] private Button sellConfirmNoBtn;

    // ========== Runtime State ==========
    private readonly List<GameObject> gemItemObjects = new List<GameObject>();
    private AudioSource audioSource;
    private int pendingSellIdx = -1;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (sellConfirmYesBtn != null)
            sellConfirmYesBtn.onClick.AddListener(ConfirmSell);
        if (sellConfirmNoBtn != null)
            sellConfirmNoBtn.onClick.AddListener(HideSellConfirmDialog);

        if (debugAddGemsButton != null)
        {
            debugAddGemsButton.gameObject.SetActive(debugMode);
            if (debugMode)
                debugAddGemsButton.onClick.AddListener(DebugAddAllGems);
        }

        HideAllPanels();
    }

    private void PlaySE(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, vol);
    }

    // ========== Public API ==========

    /// <summary>ジェム管理パネルを開く</summary>
    public void Open()
    {
        if (dimPanel != null) dimPanel.SetActive(true);
        if (gemPanel != null) gemPanel.SetActive(true);
        gemSkillPreviewHUD?.Show();
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
        if (sellConfirmPanel != null) sellConfirmPanel.SetActive(false);
        pendingSellIdx = -1;
        gemSkillPreviewHUD?.Hide();
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
                {
                    Debug.Log($"[GemManagementUI] First item: anchoredPos={firstItem.anchoredPosition}, size={firstItem.sizeDelta}, active={gemItemObjects[0].activeInHierarchy}");
                    Vector3[] corners = new Vector3[4];
                    firstItem.GetWorldCorners(corners);
                    Debug.Log($"[GemManagementUI] First item WORLD: BL={corners[0]:F0}  TR={corners[2]:F0}");
                    var viewport = gemListContainer.parent?.GetComponent<RectTransform>();
                    if (viewport != null)
                    {
                        viewport.GetWorldCorners(corners);
                        Debug.Log($"[GemManagementUI] Viewport WORLD:    BL={corners[0]:F0}  TR={corners[2]:F0}");
                    }
                }
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
        var nameText       = itemObj.transform.Find("TextContainer/NameRow/NameText")?.GetComponent<TextMeshProUGUI>();
        var skillIconsCont = itemObj.transform.Find("TextContainer/SkillIconsContainer");
        var equipBtn       = itemObj.transform.Find("ButtonContainer/EquipButton")?.GetComponent<Button>();
        var equipBtnText   = itemObj.transform.Find("ButtonContainer/EquipButton/Text")?.GetComponent<TextMeshProUGUI>();
        var sellBtn        = itemObj.transform.Find("ButtonContainer/SellButton")?.GetComponent<Button>();
        var sellBtnText    = itemObj.transform.Find("ButtonContainer/SellButton/Text")?.GetComponent<TextMeshProUGUI>();
        var equippedBadge  = itemObj.transform.Find("TextContainer/NameRow/EquippedBadge")?.gameObject;

        if (gemDef != null)
        {
            if (nameText != null)
                nameText.text = $"{gemDef.gemName}  {string.Format(slotDisplayFormat, gemDef.requiredSlots)}";

            // スキルアイコン行にデータを設定（テンプレートの行を再利用）
            if (skillIconsCont != null)
            {
                SkillDefinition b1Def = string.IsNullOrEmpty(gemInst.bonusSkill1Name) ? null
                    : Resources.Load<SkillDefinition>($"GameData/Skills/{gemInst.bonusSkill1Name}");
                SkillDefinition b2Def = string.IsNullOrEmpty(gemInst.bonusSkill2Name) ? null
                    : Resources.Load<SkillDefinition>($"GameData/Skills/{gemInst.bonusSkill2Name}");

                PopulateSkillRow(skillIconsCont.Find("SkillRow_Base"),   gemDef.baseSkill);
                PopulateSkillRow(skillIconsCont.Find("SkillRow_Bonus1"), b1Def);
                PopulateSkillRow(skillIconsCont.Find("SkillRow_Bonus2"), b2Def);

                ApplyGemItemHeight(itemObj, skillIconsCont, gemDef.baseSkill, b1Def, b2Def);
            }

            if (sellBtnText != null)
                sellBtnText.text = "売却";
        }
        else
        {
            if (nameText != null) nameText.text = "（データ読み込み失敗）";
        }

        // 装備中バッジ
        if (equippedBadge != null) equippedBadge.SetActive(isEquipped);
        if (equipBtnText != null) equipBtnText.text = isEquipped ? "解除" : "装備";

        // 装備中は売却ボタンをグレーアウト
        if (sellBtn != null)
            sellBtn.interactable = !isEquipped;

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
            sellBtn.onClick.AddListener(() => ShowSellConfirmation(capturedIdx));
        }

        gemItemObjects.Add(itemObj);
    }

    /// <summary>
    /// テンプレート用のスキル行を名前付きで生成する（Play前Inspector調整対応）
    /// </summary>
    private GameObject CreateSkillRow(string rowName, Transform parent)
    {
        var rowObj = new GameObject(rowName);
        rowObj.transform.SetParent(parent, false);

        // 行背景（カテゴリカラー）- PopulateSkillRow で色を上書き
        rowObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.5f);

        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 6f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;   // LayoutElement.preferredWidth をアイコン幅に使用
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false; // iconDisplaySize.y を行高に使用

        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.preferredHeight = skillIconSize;

        // アイコン列コンテナ（固定幅でテキスト開始位置を統一）
        var iconContainerObj = new GameObject("IconContainer");
        iconContainerObj.transform.SetParent(rowObj.transform, false);
        var icLE = iconContainerObj.AddComponent<LayoutElement>();
        icLE.minWidth       = iconColumnWidth;
        icLE.preferredWidth  = iconColumnWidth;
        icLE.preferredHeight = skillIconSize;

        // アイコン画像（コンテナ内中央配置、iconDisplayOffset で個別調整可能）
        var iconObj = new GameObject("IconImage");
        iconObj.transform.SetParent(iconContainerObj.transform, false);
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin       = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax       = new Vector2(0.5f, 0.5f);
        iconRect.pivot           = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta       = new Vector2(skillIconSize, skillIconSize);
        iconRect.anchoredPosition = Vector2.zero;
        iconObj.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        // スキル名テキスト
        var nameObj = new GameObject("SkillName");
        nameObj.transform.SetParent(rowObj.transform, false);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "";
        nameTMP.fontSize = 20f;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = Color.black;
        nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        nameTMP.enableWordWrapping = false;
        nameObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

        return rowObj;
    }

    /// <summary>
    /// 既存のスキル行にデータを設定する（skill が null なら非表示）
    /// </summary>
    private void PopulateSkillRow(Transform row, SkillDefinition skill)
    {
        if (row == null) return;
        bool hasSkill = skill != null;
        row.gameObject.SetActive(hasSkill);
        if (!hasSkill) return;

        var iconContainer = row.Find("IconContainer");
        var iconImg       = iconContainer?.Find("IconImage")?.GetComponent<Image>();
        var nameTMP       = row.Find("SkillName")?.GetComponent<TextMeshProUGUI>();

        if (iconImg != null)
        {
            iconImg.sprite = skill.icon;
            iconImg.color  = skill.icon != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);

            // アイコンサイズとオフセットを適用
            var iconRect = iconImg.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.sizeDelta = skill.iconDisplaySize != Vector2.zero
                    ? skill.iconDisplaySize
                    : new Vector2(skillIconSize, skillIconSize);
                iconRect.anchoredPosition = skill.iconDisplayOffset;
            }

            // 行背景グラデーションをカテゴリで設定
            var rowBg = row.GetComponent<Image>();
            if (rowBg != null)
            {
                Color startColor = skill.category switch
                {
                    Game.Skills.SkillCategory.CategoryA => categoryAColor,
                    Game.Skills.SkillCategory.CategoryB => categoryBColor,
                    Game.Skills.SkillCategory.CategoryC => categoryCColor,
                    _ => new Color(0.15f, 0.15f, 0.15f, 0.5f)
                };
                if (useGradientBackground)
                {
                    rowBg.sprite = CreateHorizontalGradientSprite(startColor, gradientEndColor);
                    rowBg.type = Image.Type.Simple;
                    rowBg.color = Color.white;
                }
                else
                {
                    rowBg.sprite = null;
                    rowBg.color = startColor;
                }
            }

            // 行とコンテナの高さを統一（アイコン画像サイズは変えず、行間を揃える）
            var containerLE = iconContainer?.GetComponent<LayoutElement>();
            if (containerLE != null) containerLE.preferredHeight = skillRowHeight;
            var rowLE = row.GetComponent<LayoutElement>();
            if (rowLE != null) rowLE.preferredHeight = skillRowHeight;
        }
        if (nameTMP != null)
            nameTMP.text = skill.skillName;
    }

    /// <summary>
    /// スキルの iconDisplaySize を元にジェム枠とアイコンコンテナの高さを動的に設定する
    /// </summary>
    private void ApplyGemItemHeight(GameObject itemObj, Transform skillIconsCont,
        SkillDefinition baseDef, SkillDefinition b1Def, SkillDefinition b2Def)
    {
        float IconH(SkillDefinition def) => def != null ? skillRowHeight : 0f;

        const float rowSpacing  = 4f;
        const float nameRowH    = 28f;
        const float contentGap  = 6f;
        const float paddingVert = 16f; // top 8 + bottom 8

        float totalIconH = 0f;
        if (baseDef != null) totalIconH += IconH(baseDef);
        if (b1Def   != null) totalIconH += rowSpacing + IconH(b1Def);
        if (b2Def   != null) totalIconH += rowSpacing + IconH(b2Def);
        if (totalIconH <= 0f) totalIconH = skillIconSize;

        // SkillIconsContainer の高さを更新
        var iconsLE = skillIconsCont?.GetComponent<LayoutElement>();
        if (iconsLE != null) iconsLE.preferredHeight = totalIconH;

        // GemItem の RectTransform と LayoutElement を両方更新（親VLGが childControlHeight=false のため両方必要）
        float totalH = nameRowH + contentGap + totalIconH + paddingVert;
        var itemRect = itemObj.GetComponent<RectTransform>();
        if (itemRect != null) itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, totalH);
        var itemLE = itemObj.GetComponent<LayoutElement>();
        if (itemLE != null) itemLE.preferredHeight = totalH;
    }

    /// <summary>
    /// 左→右のグラデーションSpriteを生成（スキル行背景用）
    /// </summary>
    private Sprite CreateHorizontalGradientSprite(Color leftColor, Color rightColor)
    {
        const int width = 64;
        const int height = 4;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        for (int x = 0; x < width; x++)
        {
            Color c = Color.Lerp(leftColor, rightColor, x / (float)(width - 1));
            for (int y = 0; y < height; y++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private string GetSkillDisplayName(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return "";
        var skill = Resources.Load<SkillDefinition>($"GameData/Skills/{assetName}");
        return skill != null ? skill.skillName : assetName;
    }

    // ========== Sell Confirmation ==========

    private void ShowSellConfirmation(int inventoryIdx)
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null || inventoryIdx < 0 || inventoryIdx >= data.gemInventory.Count) return;

        pendingSellIdx = inventoryIdx;

        var gemDef = GemManager.Instance?.LoadGemDefinition(data.gemInventory[inventoryIdx]);
        int price = gemDef?.sellPrice ?? 0;

        if (sellConfirmText != null)
            sellConfirmText.text = $"このジェムを売却しますか？\n¥{price}";

        if (sellConfirmPanel != null)
            sellConfirmPanel.SetActive(true);
    }

    private void HideSellConfirmDialog()
    {
        pendingSellIdx = -1;
        if (sellConfirmPanel != null)
            sellConfirmPanel.SetActive(false);
    }

    private void ConfirmSell()
    {
        if (pendingSellIdx < 0) return;
        int idx = pendingSellIdx;
        HideSellConfirmDialog();
        OnSell(idx);
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
            PlaySE(unequipSE);
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
                PlaySE(slotFullSE);
                return;
            }
            data.equippedGemIndices.Add(inventoryIdx);
            PlaySE(equipSE);
        }

        ProgressManager.Instance.Save();
        RefreshGemList();
        gemSkillPreviewHUD?.Refresh();
    }

    private void OnSell(int inventoryIdx)
    {
        var data = ProgressManager.Instance?.Data;
        if (data == null) return;
        if (inventoryIdx < 0 || inventoryIdx >= data.gemInventory.Count) return;

        // 装備中は売却不可
        if (data.equippedGemIndices.Contains(inventoryIdx))
        {
            Debug.LogWarning("[GemManagementUI] 装備中のジェムは売却できません。");
            return;
        }

        var gemInst = data.gemInventory[inventoryIdx];
        var gemDef = GemManager.Instance?.LoadGemDefinition(gemInst);

        // 売却金額を PersistentGold に加算
        if (gemDef != null && gemDef.sellPrice > 0)
            GoldManager.Instance?.AddPersistentGold(gemDef.sellPrice);

        // インベントリから削除
        data.gemInventory.RemoveAt(inventoryIdx);

        // equippedGemIndicesのインデックスを修正（削除による番号ずれ対策）
        for (int i = 0; i < data.equippedGemIndices.Count; i++)
        {
            if (data.equippedGemIndices[i] > inventoryIdx)
                data.equippedGemIndices[i]--;
        }

        PlaySE(sellSE);
        ProgressManager.Instance.Save();
        RefreshGemList();
        gemSkillPreviewHUD?.Refresh();
    }

    // ========== Debug ==========

    private void DebugAddAllGems()
    {
        if (ProgressManager.Instance == null || GemManager.Instance == null)
        {
            Debug.LogError("[GemManagementUI] ProgressManager or GemManager not found.");
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
        Debug.Log($"[GemManagementUI] Debug: Added {added}/9 gems.");
    }

    // ========== Default Gem Item Generator (Fallback) ==========

    private GameObject CreateDefaultGemItem()
    {
        // ルートアイテム
        var itemObj = new GameObject("GemItem");
        var itemRect = itemObj.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(600f, 200f); // CreateGemItem で動的に上書きされる

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
        itemLE.preferredHeight = 180f;

        // テキストコンテナ
        var textContainer = new GameObject("TextContainer");
        textContainer.transform.SetParent(itemObj.transform, false);

        var tcRect = textContainer.AddComponent<RectTransform>();
        tcRect.sizeDelta = new Vector2(380f, 164f);

        var tcLayout = textContainer.AddComponent<VerticalLayoutGroup>();
        tcLayout.spacing = 6f;
        tcLayout.childAlignment = TextAnchor.UpperLeft;
        tcLayout.childControlWidth = true;
        tcLayout.childControlHeight = true;
        tcLayout.childForceExpandWidth = true;
        tcLayout.childForceExpandHeight = false;

        var tcLE = textContainer.AddComponent<LayoutElement>();
        tcLE.preferredWidth = 380f;

        // NameRow：ジェム名 ＋ 装備中バッジ（横並び）
        var nameRowObj = new GameObject("NameRow");
        nameRowObj.transform.SetParent(textContainer.transform, false);
        var nrLayout = nameRowObj.AddComponent<HorizontalLayoutGroup>();
        nrLayout.spacing = 6f;
        nrLayout.childAlignment = TextAnchor.MiddleLeft;
        nrLayout.childControlWidth = true;
        nrLayout.childControlHeight = true;
        nrLayout.childForceExpandWidth = false;
        nrLayout.childForceExpandHeight = true;
        var nrLE = nameRowObj.AddComponent<LayoutElement>();
        nrLE.preferredHeight = 28f;

        // 名前テキスト（NameRow 内、伸縮）
        var nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(nameRowObj.transform, false);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.fontSize = 20f;
        nameTMP.color = Color.white;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.enableWordWrapping = false;
        var nameLE = nameObj.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1f;

        // 装備中バッジ（NameRow 内、右側固定）
        var badgeObj = new GameObject("EquippedBadge");
        badgeObj.transform.SetParent(nameRowObj.transform, false);
        var badgeTMP = badgeObj.AddComponent<TextMeshProUGUI>();
        badgeTMP.text = "[ 装備中 ]";
        badgeTMP.fontSize = 14f;
        badgeTMP.color = new Color(1f, 0.3f, 0.3f, 1f);
        badgeTMP.alignment = TextAlignmentOptions.MidlineRight;
        var badgeLE = badgeObj.AddComponent<LayoutElement>();
        badgeLE.minWidth = 80f;
        badgeLE.preferredWidth = 80f;
        badgeObj.SetActive(false);

        // スキルアイコンコンテナ（3行固定、Play前Inspector調整対応）
        var iconsContainerObj = new GameObject("SkillIconsContainer");
        iconsContainerObj.transform.SetParent(textContainer.transform, false);
        var iconsLayout = iconsContainerObj.AddComponent<VerticalLayoutGroup>();
        iconsLayout.spacing = 4f;
        iconsLayout.childAlignment = TextAnchor.UpperLeft;
        iconsLayout.childControlWidth = true;
        iconsLayout.childControlHeight = true;
        iconsLayout.childForceExpandWidth = true;
        iconsLayout.childForceExpandHeight = false;
        iconsContainerObj.AddComponent<LayoutElement>().preferredHeight = 128f; // CreateGemItem で動的に上書きされる

        // 3行を事前生成（Hierarchyに出てPlay前からInspectorで個別サイズ調整可能）
        CreateSkillRow("SkillRow_Base",   iconsContainerObj.transform);
        CreateSkillRow("SkillRow_Bonus1", iconsContainerObj.transform);
        CreateSkillRow("SkillRow_Bonus2", iconsContainerObj.transform);

        // ボタンコンテナ
        var btnContainer = new GameObject("ButtonContainer");
        btnContainer.transform.SetParent(itemObj.transform, false);

        var bcRect = btnContainer.AddComponent<RectTransform>();
        bcRect.sizeDelta = new Vector2(110f, 164f);

        var bcLayout = btnContainer.AddComponent<VerticalLayoutGroup>();
        bcLayout.spacing = 8f;
        bcLayout.childAlignment = TextAnchor.MiddleRight;
        bcLayout.childControlWidth = false;
        bcLayout.childControlHeight = true;
        bcLayout.childForceExpandWidth = false;
        bcLayout.childForceExpandHeight = false;

        var bcLE = btnContainer.AddComponent<LayoutElement>();
        bcLE.preferredWidth = 110f;

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
        btnRect.sizeDelta = new Vector2(100f, height);

        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.25f, 0.25f, 0.35f, 1f);

        var btn = btnObj.AddComponent<Button>();

        var le = btnObj.AddComponent<LayoutElement>();
        le.preferredWidth = 100f;
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
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    // ========== Editor Auto-Setup ==========

#if UNITY_EDITOR
    [ContextMenu("Setup Sell Confirm Dialog")]
    private void SetupSellConfirmDialog()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[GemManagementUI] Canvas not found!"); return; }

        // 既存削除
        var existing = canvas.transform.Find("SellConfirmOverlay");
        if (existing != null) DestroyImmediate(existing.gameObject);

        // オーバーレイ（全画面暗転）
        var overlayObj = new GameObject("SellConfirmOverlay");
        overlayObj.transform.SetParent(canvas.transform, false);
        var overlayRect = overlayObj.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        var overlayImg = overlayObj.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.6f);
        overlayObj.SetActive(false);

        // ダイアログパネル
        var dialogObj = new GameObject("SellConfirmDialog");
        dialogObj.transform.SetParent(overlayObj.transform, false);
        var dialogRect = dialogObj.AddComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(420f, 200f);
        dialogRect.anchoredPosition = Vector2.zero;
        var dialogBg = dialogObj.AddComponent<Image>();
        dialogBg.color = new Color(0.1f, 0.1f, 0.15f, 1f);
        var dialogLayout = dialogObj.AddComponent<VerticalLayoutGroup>();
        dialogLayout.spacing = 16f;
        dialogLayout.padding = new RectOffset(24, 24, 24, 24);
        dialogLayout.childAlignment = TextAnchor.MiddleCenter;
        dialogLayout.childControlWidth = true;
        dialogLayout.childControlHeight = true;
        dialogLayout.childForceExpandWidth = true;
        dialogLayout.childForceExpandHeight = false;

        // 確認テキスト
        var textObj = new GameObject("ConfirmText");
        textObj.transform.SetParent(dialogObj.transform, false);
        var confirmTMP = textObj.AddComponent<TextMeshProUGUI>();
        confirmTMP.text = "このジェムを売却しますか？\n¥0";
        confirmTMP.fontSize = 20f;
        confirmTMP.alignment = TextAlignmentOptions.Center;
        confirmTMP.color = Color.white;
        var textLE = textObj.AddComponent<LayoutElement>();
        textLE.preferredHeight = 64f;

        // ボタン行
        var btnRowObj = new GameObject("ButtonRow");
        btnRowObj.transform.SetParent(dialogObj.transform, false);
        var btnRowLayout = btnRowObj.AddComponent<HorizontalLayoutGroup>();
        btnRowLayout.spacing = 24f;
        btnRowLayout.childAlignment = TextAnchor.MiddleCenter;
        btnRowLayout.childControlWidth = false;
        btnRowLayout.childControlHeight = true;
        btnRowLayout.childForceExpandHeight = true;
        var btnRowLE = btnRowObj.AddComponent<LayoutElement>();
        btnRowLE.preferredHeight = 52f;

        // 売るボタン
        var yesBtn = CreateConfirmButton(btnRowObj.transform, "YesButton", "売　る", new Color(0.6f, 0.15f, 0.15f, 1f));

        // やめるボタン
        var noBtn = CreateConfirmButton(btnRowObj.transform, "NoButton", "やめる", new Color(0.25f, 0.25f, 0.35f, 1f));

        // SerializedObjectでアサイン
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("sellConfirmPanel").objectReferenceValue = overlayObj;
        so.FindProperty("sellConfirmText").objectReferenceValue = confirmTMP;
        so.FindProperty("sellConfirmYesBtn").objectReferenceValue = yesBtn;
        so.FindProperty("sellConfirmNoBtn").objectReferenceValue = noBtn;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] SellConfirmDialog created!");
    }

    private Button CreateConfirmButton(Transform parent, string name, string label, Color bgColor)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(160f, 52f);
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = bgColor;
        var btn = btnObj.AddComponent<Button>();
        btnObj.AddComponent<LayoutElement>().preferredWidth = 160f;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    [ContextMenu("Setup Gem Item Template")]
    private void SetupGemItemTemplate()
    {
        // 既存のテンプレートを削除
        var existing = transform.Find("GemItemTemplate");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
            Debug.Log("[GemManagementUI] Removed existing GemItemTemplate.");
        }

        // テンプレートを生成（CreateDefaultGemItemと同じ構造）
        var templateObj = CreateDefaultGemItem();
        templateObj.name = "GemItemTemplate";
        templateObj.transform.SetParent(transform, false);
        templateObj.SetActive(false); // 非表示テンプレート

        // gemItemTemplateフィールドにアサイン
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("gemItemTemplate").objectReferenceValue = templateObj;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] GemItemTemplate created! Adjust font sizes in Inspector, then save.");
    }

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

    [ContextMenu("Setup Debug Add Gems Button")]
    private void SetupDebugAddGemsButton()
    {
        if (gemPanel == null) { Debug.LogError("[GemManagementUI] gemPanel is null! Run 'Setup Gem Management UI' first."); return; }

        // 既存削除
        var existing = gemPanel.transform.Find("DebugAddGemsButton");
        if (existing != null) DestroyImmediate(existing.gameObject);

        // ボタン生成（ScrollViewの直前に挿入）
        var btnObj = new GameObject("DebugAddGemsButton");
        btnObj.transform.SetParent(gemPanel.transform, false);

        // ScrollView の手前に移動
        var scrollView = gemPanel.transform.Find("ScrollView");
        if (scrollView != null)
            btnObj.transform.SetSiblingIndex(scrollView.GetSiblingIndex());

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(400f, 60f);

        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.5f, 0.1f, 0.5f, 1f); // 紫：デバッグ用とわかるように

        var btn = btnObj.AddComponent<Button>();

        var le = btnObj.AddComponent<LayoutElement>();
        le.preferredHeight = 60f;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "[DEBUG] 全ジェム取得";
        tmp.fontSize = 20f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // Inspector にアサイン
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("debugAddGemsButton").objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemManagementUI] Debug button created! Enable 'Debug Mode' in Inspector to show it at runtime.");
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
        panelRect.sizeDelta = new Vector2(680f, 900f);
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
        scrollRect.sizeDelta = new Vector2(640f, 720f);
        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        var scrollLE = scrollObj.AddComponent<LayoutElement>();
        scrollLE.preferredHeight = 720f;
        scrollLE.flexibleHeight = 1f;

        // Viewport
        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        viewportObj.AddComponent<RectMask2D>();

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
