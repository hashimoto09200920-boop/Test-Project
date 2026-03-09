using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Shop;
using Game.Skills;

/// <summary>
/// AreaSelectシーンでショップ（ドリンク購入）を管理するオーバーレイUI
/// GemManagementUIと同じパターン：ContextMenuでHierarchyを自動生成
///
/// 手順:
///   1. ContextMenu「① Create Shop Button in AreaPanel」でボタン生成
///   2. ContextMenu「② Setup Shop Panel」でパネル生成
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject dimPanel;
    [SerializeField] private Image shopBgImage;
    [SerializeField] private Image shopCharacterImage;
    [SerializeField] private Image shopCounterImage;
    [SerializeField] private Image customerImage;
    [SerializeField] private GameObject shopPanel;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private Button closeButton;

    [Header("Drink List")]
    [SerializeField] private Transform drinkListContainer;

    [Header("Background Animation")]
    [Tooltip("明暗アニメーションの最小輝度（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float bgBrightnessMin = 0.7f;
    [Tooltip("明暗アニメーションの最大輝度（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float bgBrightnessMax = 1.0f;
    [Tooltip("明暗アニメーションの速度の最小値（Hz）")]
    [SerializeField] private float bgAnimSpeedMin = 0.2f;
    [Tooltip("明暗アニメーションの速度の最大値（Hz）")]
    [SerializeField] private float bgAnimSpeedMax = 0.7f;
    [Tooltip("速度が次のランダム値に変化するまでの時間（秒）")]
    [SerializeField] private float bgSpeedChangeInterval = 3f;

    [Header("Character Transform")]
    [Tooltip("バーテンダー画像のX位置（Canvas中央からのオフセット）")]
    [SerializeField] private float characterPosX = 400f;
    [Tooltip("バーテンダー画像のY位置（画面下端からのオフセット）")]
    [SerializeField] private float characterPosY = 0f;
    [Tooltip("バーテンダー画像の横幅")]
    [SerializeField] private float characterWidth = 600f;
    [Tooltip("バーテンダー画像の高さ")]
    [SerializeField] private float characterHeight = 900f;

    [Header("Counter Transform")]
    [Tooltip("カウンター画像のX位置")]
    [SerializeField] private float counterPosX = 0f;
    [Tooltip("カウンター画像のY位置（画面下端からのオフセット）")]
    [SerializeField] private float counterPosY = 0f;
    [Tooltip("カウンター画像の横幅")]
    [SerializeField] private float counterWidth = 1920f;
    [Tooltip("カウンター画像の高さ")]
    [SerializeField] private float counterHeight = 400f;

    [Header("Customer Transform")]
    [Tooltip("客画像のX位置")]
    [SerializeField] private float customerPosX = -400f;
    [Tooltip("客画像のY位置（画面下端からのオフセット）")]
    [SerializeField] private float customerPosY = 0f;
    [Tooltip("客画像の横幅")]
    [SerializeField] private float customerWidth = 500f;
    [Tooltip("客画像の高さ")]
    [SerializeField] private float customerHeight = 800f;

    [Header("Character Animation")]
    [Tooltip("バーテンダーのアニメーションフレーム（順番に表示）")]
    [SerializeField] private Sprite[] characterAnimFrames;
    [Tooltip("アニメーションの再生速度（fps）")]
    [SerializeField] private float characterAnimFps = 6f;

    [Header("Open Fade")]
    [Tooltip("パネルを開く時のフェード時間（秒）。0にするとフェードなし")]
    [SerializeField] private float openFadeDuration = 0.5f;

    [Header("SE")]
    [SerializeField] private AudioClip buySE;
    [SerializeField] private AudioClip closeSE;
    [SerializeField] private AudioClip insufficientGoldSE;

    private readonly List<GameObject> drinkItemObjects = new List<GameObject>();
    private AudioSource audioSource;
    private Coroutine bgAnimCoroutine;
    private Coroutine characterAnimCoroutine;
    private bool isOpening = false;
    private bool isClosing = false;

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

        HideAllPanels();
    }

    // ========== Public API ==========

    /// <summary>ショップパネルを開く（ShopButtonのonClickから呼ぶ）</summary>
    public void Open()
    {
        if (isOpening) return;
        StartCoroutine(OpenWithFade());
    }

    private IEnumerator OpenWithFade()
    {
        isOpening = true;
        yield return StartCoroutine(FadeScreen(0f, 1f));

        if (dimPanel != null) dimPanel.SetActive(true);
        if (shopBgImage != null)
        {
            shopBgImage.gameObject.SetActive(true);
            if (bgAnimCoroutine != null) StopCoroutine(bgAnimCoroutine);
            bgAnimCoroutine = StartCoroutine(AnimateBgBrightness());
        }
        if (shopCharacterImage != null)
        {
            shopCharacterImage.gameObject.SetActive(true);
            if (characterAnimFrames != null && characterAnimFrames.Length > 1)
            {
                if (characterAnimCoroutine != null) StopCoroutine(characterAnimCoroutine);
                characterAnimCoroutine = StartCoroutine(AnimateCharacter());
            }
        }
        if (shopCounterImage != null) shopCounterImage.gameObject.SetActive(true);
        if (customerImage != null) customerImage.gameObject.SetActive(true);
        if (shopPanel != null) shopPanel.SetActive(true);
        RefreshGoldDisplay();
        RefreshDrinkList();

        yield return StartCoroutine(FadeScreen(1f, 0f));
        isOpening = false;
    }

    private IEnumerator FadeScreen(float from, float to)
    {
        if (openFadeDuration <= 0f) yield break;

        GameObject fadeObj = new GameObject("OpenFade");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;

        UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        Image fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, from);

        RectTransform rt = imageObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        float elapsed = 0f;
        while (elapsed < openFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, elapsed / openFadeDuration));
            yield return null;
        }

        Destroy(fadeObj);
    }

    /// <summary>ショップパネルを閉じる</summary>
    public void Close()
    {
        if (isClosing) return;
        StartCoroutine(CloseWithFade());
    }

    private IEnumerator CloseWithFade()
    {
        isClosing = true;
        PlaySE(closeSE);
        yield return StartCoroutine(FadeScreen(0f, 1f));
        HideAllPanels();
        yield return StartCoroutine(FadeScreen(1f, 0f));
        FindObjectOfType<Game.UI.AreaSelectMenu>()?.ResetPanelTransition();
        isClosing = false;
    }

    private void HideAllPanels()
    {
        if (bgAnimCoroutine != null) { StopCoroutine(bgAnimCoroutine); bgAnimCoroutine = null; }
        if (characterAnimCoroutine != null) { StopCoroutine(characterAnimCoroutine); characterAnimCoroutine = null; }
        if (dimPanel != null) dimPanel.SetActive(false);
        if (shopBgImage != null)
        {
            shopBgImage.color = Color.white; // 輝度をリセット
            shopBgImage.gameObject.SetActive(false);
        }
        if (shopCharacterImage != null) shopCharacterImage.gameObject.SetActive(false);
        if (shopCounterImage != null) shopCounterImage.gameObject.SetActive(false);
        if (customerImage != null) customerImage.gameObject.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    private System.Collections.IEnumerator AnimateBgBrightness()
    {
        float t           = 0f;
        float currentSpeed = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax);
        float targetSpeed  = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax);
        float speedTimer   = 0f;

        while (true)
        {
            float dt = Time.unscaledDeltaTime;

            // 速度をスムーズに目標値へ移行
            speedTimer += dt;
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, dt * 1.5f);

            // インターバルごとに新しいランダム目標速度を設定
            if (speedTimer >= bgSpeedChangeInterval)
            {
                targetSpeed = Random.Range(bgAnimSpeedMin, bgAnimSpeedMax);
                speedTimer  = 0f;
            }

            t += dt * currentSpeed;

            // Sin波で bgBrightnessMin〜bgBrightnessMax を滑らかに行き来
            float brightness = Mathf.Lerp(bgBrightnessMin, bgBrightnessMax,
                                          (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f);
            if (shopBgImage != null)
                shopBgImage.color = new Color(brightness, brightness, brightness, 1f);
            yield return null;
        }
    }

    private System.Collections.IEnumerator AnimateCharacter()
    {
        int frameIndex = 0;
        float interval = 1f / Mathf.Max(characterAnimFps, 0.1f);

        while (true)
        {
            if (shopCharacterImage != null && characterAnimFrames != null && characterAnimFrames.Length > 0)
                shopCharacterImage.sprite = characterAnimFrames[frameIndex % characterAnimFrames.Length];

            frameIndex++;
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    // ========== Display ==========

    private void RefreshGoldDisplay()
    {
        if (goldText == null) return;
        int gold = GoldManager.Instance != null ? GoldManager.Instance.PersistentGold : 0;
        goldText.text = $"所持ゴールド: {gold}G";
    }

    private void RefreshDrinkList()
    {
        foreach (var obj in drinkItemObjects)
            if (obj != null) Destroy(obj);
        drinkItemObjects.Clear();

        if (drinkListContainer == null) return;

        var drinks = Resources.LoadAll<DrinkDefinition>("GameData/Drinks");
        foreach (var drink in drinks)
            CreateDrinkItem(drink);
    }

    private void CreateDrinkItem(DrinkDefinition drink)
    {
        var itemObj = new GameObject(drink.drinkName);
        itemObj.transform.SetParent(drinkListContainer, false);

        itemObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        itemObj.AddComponent<LayoutElement>().preferredHeight = 100f;

        var layout = itemObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        // アイコン
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(itemObj.transform, false);
        var iconLE = iconObj.AddComponent<LayoutElement>();
        iconLE.minWidth = 80f;
        iconLE.preferredWidth = 80f;
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = drink.icon;
        iconImg.color = drink.icon != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);
        iconImg.preserveAspect = true;

        // テキストコンテナ
        var tcObj = new GameObject("TextContainer");
        tcObj.transform.SetParent(itemObj.transform, false);
        tcObj.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var tcLayout = tcObj.AddComponent<VerticalLayoutGroup>();
        tcLayout.spacing = 4f;
        tcLayout.childAlignment = TextAnchor.MiddleLeft;
        tcLayout.childControlWidth = true;
        tcLayout.childControlHeight = true;
        tcLayout.childForceExpandWidth = true;
        tcLayout.childForceExpandHeight = false;

        // ドリンク名
        var nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(tcObj.transform, false);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text = drink.drinkName;
        nameTMP.fontSize = 22f;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = Color.white;
        nameTMP.enableWordWrapping = false;
        nameObj.AddComponent<LayoutElement>().preferredHeight = 30f;

        // 説明文
        var descObj = new GameObject("DescText");
        descObj.transform.SetParent(tcObj.transform, false);
        var descTMP = descObj.AddComponent<TextMeshProUGUI>();
        descTMP.text = !string.IsNullOrEmpty(drink.description)
            ? drink.description
            : BuildAutoDescription(drink);
        descTMP.fontSize = 16f;
        descTMP.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        descObj.AddComponent<LayoutElement>().preferredHeight = 24f;

        // 購入ボタン
        var buyBtnObj = new GameObject("BuyButton");
        buyBtnObj.transform.SetParent(itemObj.transform, false);
        var buyBtnLE = buyBtnObj.AddComponent<LayoutElement>();
        buyBtnLE.minWidth = 120f;
        buyBtnLE.preferredWidth = 120f;
        buyBtnObj.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.2f, 1f);
        var buyBtn = buyBtnObj.AddComponent<Button>();

        var buyTxtObj = new GameObject("Text");
        buyTxtObj.transform.SetParent(buyBtnObj.transform, false);
        var buyTxtRect = buyTxtObj.AddComponent<RectTransform>();
        buyTxtRect.anchorMin = Vector2.zero;
        buyTxtRect.anchorMax = Vector2.one;
        buyTxtRect.sizeDelta = Vector2.zero;
        var buyTMP = buyTxtObj.AddComponent<TextMeshProUGUI>();
        buyTMP.text = $"{drink.price}G\n購入";
        buyTMP.fontSize = 18f;
        buyTMP.alignment = TextAlignmentOptions.Center;
        buyTMP.color = Color.white;

        var capturedDrink = drink;
        buyBtn.onClick.AddListener(() => OnBuyDrink(capturedDrink));

        drinkItemObjects.Add(itemObj);
    }

    // ========== Purchase ==========

    private void OnBuyDrink(DrinkDefinition drink)
    {
        if (GoldManager.Instance == null) return;

        // 有効な候補スキルを収集（3枠のうちnullでないもの）
        var candidates = new System.Collections.Generic.List<SkillDefinition>();
        if (drink.targetSkill1 != null) candidates.Add(drink.targetSkill1);
        if (drink.targetSkill2 != null) candidates.Add(drink.targetSkill2);
        if (drink.targetSkill3 != null) candidates.Add(drink.targetSkill3);

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[ShopUI] {drink.drinkName}: 対象スキルが1つも設定されていません。");
            return;
        }

        if (GoldManager.Instance.PersistentGold < drink.price)
        {
            PlaySE(insufficientGoldSE);
            Debug.Log($"[ShopUI] ゴールド不足: 必要={drink.price}, 所持={GoldManager.Instance.PersistentGold}");
            return;
        }

        GoldManager.Instance.SpendPersistentGold(drink.price);

        // ランダム選択（重複なし）。候補数より多い場合は全候補を選択
        int count = Mathf.Min(drink.selectionCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            var selected = candidates[idx];
            candidates.RemoveAt(idx);
            DrinkSession.AddBoost(selected.name, drink.levelUpCount);
            Debug.Log($"[ShopUI] 購入: {drink.drinkName} → {selected.name} +{drink.levelUpCount}回");
        }

        PlaySE(buySE);
        RefreshGoldDisplay();
    }

    /// <summary>description が空欄の時の自動生成テキスト</summary>
    private string BuildAutoDescription(DrinkDefinition drink)
    {
        // 有効なスキル名リスト
        var names = new System.Collections.Generic.List<string>();
        if (drink.targetSkill1 != null) names.Add(drink.targetSkill1.skillName);
        if (drink.targetSkill2 != null) names.Add(drink.targetSkill2.skillName);
        if (drink.targetSkill3 != null) names.Add(drink.targetSkill3.skillName);

        if (names.Count == 0) return "対象スキル未設定";

        // 候補が1つ、かつ選択数も1 → シンプル表示
        if (names.Count == 1 && drink.selectionCount == 1)
            return $"{names[0]}を{drink.levelUpCount}回分レベルアップ";

        // 複数候補 or 複数選択
        string skillList = string.Join(" / ", names);
        int actualCount = Mathf.Min(drink.selectionCount, names.Count);
        return $"{skillList}\nからランダムに{actualCount}つ選択（各+{drink.levelUpCount}レベル）";
    }

    // ========== SE ==========

    private void PlaySE(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, vol);
    }

    // ========== Editor Auto-Setup ==========

#if UNITY_EDITOR
    [ContextMenu("① Create Shop Button in AreaPanel")]
    private void CreateShopButton()
    {
        var areaPanelGO = GameObject.Find("AreaPanel");
        if (areaPanelGO == null)
        {
            Debug.LogError("[ShopUI] 'AreaPanel' が見つかりません。03_AreaSelect シーンを開いているか確認してください。");
            return;
        }

        // 既存削除
        var existing = areaPanelGO.transform.Find("ShopButton");
        if (existing != null) DestroyImmediate(existing.gameObject);

        // ボタン本体
        var btnObj = new GameObject("ShopButton");
        btnObj.transform.SetParent(areaPanelGO.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin        = new Vector2(0.5f, 0f);
        btnRect.anchorMax        = new Vector2(0.5f, 0f);
        btnRect.pivot            = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(750f, 460f); // GemButton(720) と BackButton(200) の中間
        btnRect.sizeDelta        = new Vector2(160f, 150f);

        // ボタン画像（仮色: 暖色系でショップらしく）
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.55f, 0.35f, 0.1f, 1f);

        var btn = btnObj.AddComponent<Button>();

        // テキスト
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "SHOP";
        tmp.fontSize = 30f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // onClick に Open() を登録
        var so = new UnityEditor.SerializedObject(btn);
        so.Update();
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        calls.arraySize = 1;
        var call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = this;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "ShopUI, Assembly-CSharp";
        call.FindPropertyRelative("m_MethodName").stringValue = "Open";
        call.FindPropertyRelative("m_Mode").intValue = 1;      // Void
        call.FindPropertyRelative("m_CallState").intValue = 2; // RuntimeOnly
        so.ApplyModifiedProperties();

        UnityEditor.EditorUtility.SetDirty(areaPanelGO);
        Debug.Log("[ShopUI] ShopButton を AreaPanel に生成しました。pos=(750, 460), size=(160, 150)\n※画像スプライトを Inspector で差し替えてください。");
    }

    [ContextMenu("② Setup Shop Panel")]
    private void SetupShopPanel()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("[ShopUI] Canvas が見つかりません。"); return; }

        // 既存削除
        foreach (var name in new[] { "ShopDimPanel", "ShopBgImage", "ShopCharacterImage", "ShopCounter", "Customer", "ShopPanel" })
        {
            var existing = canvas.transform.Find(name);
            if (existing != null) DestroyImmediate(existing.gameObject);
        }

        // レイヤー順に生成（後の兄弟ほど手前に描画）
        // 1. 暗幕
        CreateShopDimPanel(canvas.transform);
        // 2. バーカウンター背景画像
        CreateShopBgImage(canvas.transform);
        // 3. 人物画像
        CreateShopCharacterImage(canvas.transform);
        // 4. カウンター
        CreateShopCounterImage(canvas.transform);
        // 5. 客
        CreateCustomerImage(canvas.transform);
        // 6. 商品UIパネル（最前面）
        CreateShopMainPanel(canvas.transform);

        // Inspector 参照を設定
        var so = new UnityEditor.SerializedObject(this);
        so.Update();
        so.FindProperty("dimPanel").objectReferenceValue = dimPanel;
        so.FindProperty("shopBgImage").objectReferenceValue = shopBgImage;
        so.FindProperty("shopCharacterImage").objectReferenceValue = shopCharacterImage;
        so.FindProperty("shopCounterImage").objectReferenceValue = shopCounterImage;
        so.FindProperty("customerImage").objectReferenceValue = customerImage;
        so.FindProperty("shopPanel").objectReferenceValue = shopPanel;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("goldText").objectReferenceValue = goldText;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("drinkListContainer").objectReferenceValue = drinkListContainer;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[ShopUI] ショップパネルを生成しました！\n" +
                  "レイヤー順（下→上）: ShopDimPanel → ShopBgImage → ShopCharacterImage → ShopCounter → Customer → ShopPanel\n" +
                  "・ShopBgImage の Source Image に バーカウンター背景画像 を設定してください（1920×1080）\n" +
                  "・ShopCharacterImage の Source Image に バーテンダー人物画像 を設定してください\n" +
                  "・Resources/GameData/Drinks/ に DrinkDefinition アセットを配置してください。");
    }

    [ContextMenu("③ Apply Character Transform")]
    private void ApplyCharacterTransform()
    {
        if (shopCharacterImage == null) { Debug.LogError("[ShopUI] ShopCharacterImage が見つかりません。"); return; }
        var r = shopCharacterImage.GetComponent<RectTransform>();
        r.anchorMin        = new Vector2(0.5f, 0f);
        r.anchorMax        = new Vector2(0.5f, 0f);
        r.pivot            = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(characterPosX, characterPosY);
        r.sizeDelta        = new Vector2(characterWidth, characterHeight);
        UnityEditor.EditorUtility.SetDirty(shopCharacterImage.gameObject);
        Debug.Log($"[ShopUI] ShopCharacterImage 更新: Pos=({characterPosX}, {characterPosY}), Size=({characterWidth}, {characterHeight})");
    }

    [ContextMenu("④ Apply Counter Transform")]
    private void ApplyCounterTransform()
    {
        if (shopCounterImage == null) { Debug.LogError("[ShopUI] ShopCounter が見つかりません。"); return; }
        var r = shopCounterImage.GetComponent<RectTransform>();
        r.anchoredPosition = new Vector2(counterPosX, counterPosY);
        r.sizeDelta        = new Vector2(counterWidth, counterHeight);
        UnityEditor.EditorUtility.SetDirty(shopCounterImage.gameObject);
        Debug.Log($"[ShopUI] ShopCounter 更新: Pos=({counterPosX}, {counterPosY}), Size=({counterWidth}, {counterHeight})");
    }

    [ContextMenu("⑤ Apply Customer Transform")]
    private void ApplyCustomerTransform()
    {
        if (customerImage == null) { Debug.LogError("[ShopUI] Customer が見つかりません。"); return; }
        var r = customerImage.GetComponent<RectTransform>();
        r.anchorMin        = new Vector2(0.5f, 0f);
        r.anchorMax        = new Vector2(0.5f, 0f);
        r.pivot            = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(customerPosX, customerPosY);
        r.sizeDelta        = new Vector2(customerWidth, customerHeight);
        UnityEditor.EditorUtility.SetDirty(customerImage.gameObject);
        Debug.Log($"[ShopUI] Customer 更新: Pos=({customerPosX}, {customerPosY}), Size=({customerWidth}, {customerHeight})");
    }

    private void CreateShopBgImage(Transform parent)
    {
        var obj = new GameObject("ShopBgImage");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        // AreaSelect 背景と同サイズ（Canvas全体に stretch）
        rect.anchorMin        = Vector2.zero;
        rect.anchorMax        = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = Vector2.zero;
        shopBgImage = obj.AddComponent<Image>();
        shopBgImage.color           = Color.white;
        shopBgImage.preserveAspect  = false; // 全体を埋めるため false
        obj.SetActive(false);
    }

    private void CreateShopCharacterImage(Transform parent)
    {
        var obj = new GameObject("ShopCharacterImage");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin        = Vector2.zero;
        rect.anchorMax        = Vector2.one;
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = Vector2.zero;
        shopCharacterImage = obj.AddComponent<Image>();
        shopCharacterImage.color          = Color.white;
        shopCharacterImage.preserveAspect = true;
        obj.SetActive(false);
    }

    private void CreateShopCounterImage(Transform parent)
    {
        var obj = new GameObject("ShopCounter");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0f);
        rect.anchorMax        = new Vector2(0.5f, 0f);
        rect.pivot            = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(counterPosX, counterPosY);
        rect.sizeDelta        = new Vector2(counterWidth, counterHeight);
        shopCounterImage = obj.AddComponent<Image>();
        shopCounterImage.color          = Color.white;
        shopCounterImage.preserveAspect = true;
        obj.SetActive(false);
    }

    private void CreateCustomerImage(Transform parent)
    {
        var obj = new GameObject("Customer");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin        = Vector2.zero;
        rect.anchorMax        = Vector2.one;
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = Vector2.zero;
        customerImage = obj.AddComponent<Image>();
        customerImage.color          = Color.white;
        customerImage.preserveAspect = true;
        obj.SetActive(false);
    }

    private void CreateShopDimPanel(Transform parent)
    {
        var obj = new GameObject("ShopDimPanel");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        var img = obj.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.75f);
        dimPanel = obj;
        obj.SetActive(false);
    }

    private void CreateShopMainPanel(Transform parent)
    {
        // メインパネル
        var panelObj = new GameObject("ShopPanel");
        panelObj.transform.SetParent(parent, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta        = new Vector2(700f, 800f);
        panelRect.anchoredPosition = Vector2.zero;
        var panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);
        var panelLayout = panelObj.AddComponent<VerticalLayoutGroup>();
        panelLayout.spacing          = 10f;
        panelLayout.padding          = new RectOffset(20, 20, 16, 16);
        panelLayout.childAlignment   = TextAnchor.UpperCenter;
        panelLayout.childControlWidth       = true;
        panelLayout.childControlHeight      = true;
        panelLayout.childForceExpandWidth   = true;
        panelLayout.childForceExpandHeight  = false;
        shopPanel = panelObj;

        // ヘッダー行
        var headerObj = new GameObject("HeaderRow");
        headerObj.transform.SetParent(panelObj.transform, false);
        var headerLayout = headerObj.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing                = 8f;
        headerLayout.childAlignment         = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth      = true;
        headerLayout.childControlHeight     = true;
        headerLayout.childForceExpandWidth  = false;
        headerLayout.childForceExpandHeight = true;
        headerObj.AddComponent<LayoutElement>().preferredHeight = 60f;

        // タイトル
        var titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(headerObj.transform, false);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text              = "SHOP";
        titleText.fontSize          = 36f;
        titleText.fontStyle         = FontStyles.Bold;
        titleText.color             = Color.white;
        titleText.enableWordWrapping = false;
        titleObj.AddComponent<LayoutElement>().preferredWidth = 150f;

        // ゴールド表示
        var goldObj = new GameObject("GoldText");
        goldObj.transform.SetParent(headerObj.transform, false);
        goldText = goldObj.AddComponent<TextMeshProUGUI>();
        goldText.text      = "所持ゴールド: 0G";
        goldText.fontSize  = 22f;
        goldText.color     = new Color(1f, 0.9f, 0.3f, 1f);
        goldText.alignment = TextAlignmentOptions.MidlineLeft;
        goldObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // 閉じるボタン
        var closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        closeBtnObj.AddComponent<Image>().color = new Color(0.4f, 0.15f, 0.15f, 1f);
        closeButton = closeBtnObj.AddComponent<Button>();
        closeBtnObj.AddComponent<LayoutElement>().preferredWidth = 120f;
        var closeTxtObj = new GameObject("Text");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        var closeTxtRect = closeTxtObj.AddComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.sizeDelta = Vector2.zero;
        var closeTMP = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTMP.text      = "閉じる";
        closeTMP.fontSize  = 22f;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeTMP.color     = Color.white;

        // 区切り線
        var sepObj = new GameObject("Separator");
        sepObj.transform.SetParent(panelObj.transform, false);
        sepObj.AddComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 1f);
        sepObj.AddComponent<LayoutElement>().preferredHeight = 2f;

        // ScrollView
        var scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(panelObj.transform, false);
        scrollViewObj.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;
        scrollViewObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        // Viewport
        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot     = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        var contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing               = 8f;
        contentLayout.padding               = new RectOffset(8, 8, 8, 8);
        contentLayout.childAlignment        = TextAnchor.UpperLeft;
        contentLayout.childControlWidth     = true;
        contentLayout.childControlHeight    = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        drinkListContainer = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.content  = contentRect;

        panelObj.SetActive(false);
    }
#endif
}
