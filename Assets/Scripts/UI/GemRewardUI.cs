using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Gems;
using Game.Progress;
using Game.Skills;

/// <summary>
/// Stage3クリア後のジェム選択・取得UI
/// 事前に [ContextMenu("Setup Gem Reward UI")] でHierarchyを生成すること
///
/// 呼び出し方: gemRewardUI.Open(areaId)
/// 終了後:     GameResultUI.ShowAllClearResult() に自動で続く
/// </summary>
public class GemRewardUI : MonoBehaviour
{
    // ===== Panel References =====

    [Header("Panels")]
    [SerializeField] private GameObject dimPanel;
    [SerializeField] private GameObject phase1Panel;
    [SerializeField] private GameObject confirmDialog;
    [SerializeField] private GameObject fullMessagePanel;
    [SerializeField] private GameObject phase2Panel;

    // ===== Phase1 =====

    [Header("Phase1")]
    [SerializeField] private TextMeshProUGUI phase1TitleText;
    [SerializeField] private GemRewardCardUI card0;
    [SerializeField] private GemRewardCardUI card1;
    [SerializeField] private GemRewardCardUI card2;

    // ===== Confirm Dialog =====

    [Header("Confirm Dialog")]
    [SerializeField] private TextMeshProUGUI confirmMessageText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    // ===== Full Message =====

    [Header("Full Message (上限満杯)")]
    [SerializeField] private TextMeshProUGUI fullMessageText;
    [SerializeField] private Button fullCloseButton;
    [SerializeField] private RectTransform fullBgRect;

    // ===== Phase2 =====

    [Header("Phase2")]
    [SerializeField] private TextMeshProUGUI phase2TitleText;
    [SerializeField] private GemRewardCardUI phase2SelectedCard;     // 中央・選択済み
    [SerializeField] private GemRewardCardUI phase2UnselectedCard0;  // 左・非選択
    [SerializeField] private GemRewardCardUI phase2UnselectedCard1;  // 右・非選択
    [SerializeField] private Button closeButton;

    // ===== Phase1 CardRow =====

    [Header("Phase1 CardRow")]
    [SerializeField] private RectTransform phase1CardRow;
    [SerializeField] private RectTransform phase2CardRow;
    [SerializeField] private float skillHudWidth = 280f;

    // ===== Animation =====

    [Header("Animation")]
    [SerializeField] private float cardRevealDuration    = 0.4f;
    [SerializeField] private float unselectedRevealDelay = 0.6f;
    [Tooltip("Phase1カード登場のスタッガー間隔（秒）")]
    [SerializeField] private float entranceStaggerInterval = 0.12f;
    [Tooltip("YesButton押下後、SelectedCard表示までの待機時間（秒）")]
    [SerializeField] private float cardRevealDelay = 0.5f;
    [Tooltip("ConfirmBg のフェードイン時間（秒）")]
    [SerializeField] private float confirmFadeInDuration = 0.2f;
    [Tooltip("ConfirmBg のフェードアウト時間（秒）")]
    [SerializeField] private float confirmFadeOutDuration = 0.2f;
    [Tooltip("Phase1・Phase2 タイトルのタイプライター間隔（秒/文字）")]
    [SerializeField] private float titleCharInterval = 0.045f;

    // ===== Text =====

    [Header("Text (Inspector で変更可)")]
    [TextArea(2, 6)]
    [SerializeField] private string fullMsgStr       = "ジェムの所持数が上限（30個）です。\nジェムを売却してから再挑戦してください。";
    [SerializeField] private string phase2TitleStr   = "ジェムを取得しました！";

    // ===== SE =====

    [Header("SE")]
    [Tooltip("カード落下SE（インデックス順に card0/1/2 に対応。足りない場合は最後の要素を使用）")]
    [SerializeField] private AudioClip[] cardDropSEs;
    [SerializeField] private AudioClip selectSE;
    [SerializeField] private AudioClip confirmYesSE;
    [SerializeField] private AudioClip confirmNoSE;
    [SerializeField] private AudioClip cardRevealSE;
    [SerializeField] private AudioClip closeSE;

    // ===== References =====

    [Header("References")]
    [SerializeField] private GameResultUI gameResultUI;
    [SerializeField] private WaveTimerUI waveTimerUI;
    [SerializeField] private GameObject pixceldancer;
    [SerializeField] private GameObject floor;
    [SerializeField] private StageIntroController stageIntroController;

    // ===== Auto Transit =====

    [Header("Auto Transit")]
    [Tooltip("ジェム取得後、自動で03_AreaSelectに遷移するまでの秒数")]
    [SerializeField] private float autoTransitDelay = 3f;

    // ===== Debug =====

    [Header("Debug")]
    [SerializeField] private bool debugSkipToGemReward = false;
    [SerializeField] private string debugAreaId = "Area_01";
    [Tooltip("デバッグ時にOpen()を呼ぶまでの遅延秒数")]
    [SerializeField] private float debugOpenDelay = 3f;
    [Tooltip("Phase1の3枚カードでスキルIconとSkillNameを表示する（ONで表示、OFFで非表示）")]
    [SerializeField] private bool debugShowSkillsInPhase1 = false;

    // ===== Runtime =====

#if UNITY_EDITOR
    /// <summary>ゲームプレイをスキップしてGemRewardから開始するデバッグフラグ</summary>
    public static bool DebugSkipGameplay = false;
#endif

    private GemInstance[]    rolledGems;
    private GemDefinition    currentGemDef;
    private Coroutine        autoTransitCoroutine;
    private int              selectedIndex = -1;
    private AudioSource      audioSource;
    private GemRewardCardUI[] selectionCards;
    private string           _phase1TitleCache;

    // =====================================================
    // Lifecycle
    // =====================================================

    private void Awake()
    {
#if UNITY_EDITOR
        DebugSkipGameplay = debugSkipToGemReward;
#endif

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        selectionCards = new[] { card0, card1, card2 };

        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton  != null) confirmNoButton.onClick.AddListener(OnConfirmNo);
        if (fullCloseButton  != null) fullCloseButton.onClick.AddListener(OnFullClose);
        if (closeButton      != null) closeButton.onClick.AddListener(OnClose);

        HideAll();
    }

    private void Start()
    {
#if UNITY_EDITOR
        if (debugSkipToGemReward)
        {
            StartCoroutine(DebugOpenDelayed());
        }
#endif
    }

#if UNITY_EDITOR
    private IEnumerator DebugOpenDelayed()
    {
        yield return new WaitForSecondsRealtime(debugOpenDelay);
        string normalizedAreaId = System.Text.RegularExpressions.Regex.Replace(
            debugAreaId, @"^area_", "Area_");
        Open(normalizedAreaId);
    }
#endif

    private void HideAll()
    {
        dimPanel?.SetActive(false);
        phase1Panel?.SetActive(false);
        confirmDialog?.SetActive(false);
        fullMessagePanel?.SetActive(false);
        phase2Panel?.SetActive(false);
    }

    // =====================================================
    // Open
    // =====================================================

    /// <summary>
    /// EnemySpawner から Stage3 クリア後に呼ぶ
    /// </summary>
    public void Open(string areaId)
    {
        if (gameResultUI == null)
            gameResultUI = FindObjectOfType<GameResultUI>();
        if (waveTimerUI == null)
            waveTimerUI = FindObjectOfType<WaveTimerUI>();

        waveTimerUI?.SetPauseButtonVisible(false);
        if (PaddleDrawer.Instance != null) PaddleDrawer.Instance.enabled = false;
        SlowMotionUIManager.Instance?.SetInputEnabled(false);
        HideAll();

        // インベントリ上限チェック
        var inventory = ProgressManager.Instance?.Data?.gemInventory;
        if (inventory == null || inventory.Count >= GemManager.MaxInventory)
        {
            ShowFullMessage();
            return;
        }

        // 3つロール（インベントリには追加しない）
        currentGemDef = GemManager.Instance?.GetGemDefinitionForArea(areaId);
        rolledGems    = GemManager.Instance?.RollGemsForArea(areaId, 3);

        if (rolledGems == null || rolledGems.Length == 0 || currentGemDef == null)
        {
            Debug.LogWarning($"[GemRewardUI] Failed to roll gems for {areaId}. Falling back to AllClear.");
            gameResultUI?.ShowAllClearResult();
            return;
        }

        ShowPhase1();
    }

    // =====================================================
    // Phase 1 – 選択
    // =====================================================

    private void ShowPhase1()
    {
        selectedIndex = -1;
        // Play前にTMPコンポーネントのtextに設定した値をキャッシュしてからクリア
        _phase1TitleCache = phase1TitleText != null ? phase1TitleText.text : "";
        if (phase1TitleText != null) phase1TitleText.text = "";

        for (int i = 0; i < selectionCards.Length; i++)
        {
            var card = selectionCards[i];
            if (card == null || i >= rolledGems.Length) continue;

            var gem    = rolledGems[i];
            var bonus1 = GemManager.Instance.LoadBonusSkill1(gem);
            var bonus2 = GemManager.Instance.LoadBonusSkill2(gem);
            card.Setup(gem, currentGemDef, bonus1, bonus2, showSkills: debugShowSkillsInPhase1);
            card.SetState(GemRewardCardUI.CardState.Normal);
            card.HoverEnabled = false; // 全カード着地後に一括で有効化

            int idx = i;
            var btn = card.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnCardSelected(idx));
            }
        }

        dimPanel?.SetActive(true);
        phase1Panel?.SetActive(true);

        // カードをスタッガーで登場し、全枚着地後にホバーを一括有効化
        StartCoroutine(EntranceAllCardsCoroutine());
    }

    private IEnumerator EntranceAllCardsCoroutine()
    {
        // エントランス中はボタン無効・ホバー無効
        foreach (var card in selectionCards)
        {
            if (card == null) continue;
            var btn = card.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        // 全カードのエントランス開始（スタッガーごとにSE）
        for (int i = 0; i < selectionCards.Length; i++)
        {
            if (selectionCards[i] != null)
                StartCoroutine(selectionCards[i].EntranceCoroutine(i * entranceStaggerInterval));
        }
        StartCoroutine(PlayDropSEWithStagger());

        // 最後のカードが着地+バウンド完了するまで待つ
        // +1フレーム(layout settle) + 最後のdelay + 落下+バウンド時間
        float lastDelay = (selectionCards.Length - 1) * entranceStaggerInterval;
        float lastEntranceDuration = selectionCards[selectionCards.Length - 1] != null
            ? selectionCards[selectionCards.Length - 1].EntranceTotalDuration
            : 0.5f;
        yield return new WaitForSecondsRealtime(Time.unscaledDeltaTime + lastDelay + lastEntranceDuration);

        // 全カードのボタン有効・ホバー有効化
        foreach (var card in selectionCards)
        {
            if (card == null) continue;
            var btn = card.GetComponent<Button>();
            if (btn != null) btn.interactable = true;
            card.HoverEnabled = true;
        }

        // 全カード着地後にタイトルタイプライター開始
        StartCoroutine(TypewriterCoroutine(phase1TitleText, _phase1TitleCache, titleCharInterval));
    }

    private IEnumerator PlayDropSEWithStagger()
    {
        if (cardDropSEs == null || cardDropSEs.Length == 0) yield break;
        // EntranceCoroutine 内の1フレーム待機に合わせて1フレーム待つ
        yield return null;
        for (int i = 0; i < selectionCards.Length; i++)
        {
            int idx = Mathf.Min(i, cardDropSEs.Length - 1);
            PlaySE(cardDropSEs[idx]);
            if (i < selectionCards.Length - 1)
                yield return new WaitForSecondsRealtime(entranceStaggerInterval);
        }
    }

    private void OnCardSelected(int index)
    {
        selectedIndex = index;
        PlaySE(selectSE);

        for (int i = 0; i < selectionCards.Length; i++)
        {
            if (selectionCards[i] == null) continue;

            // ホバー無効化 & スケールをリセット
            selectionCards[i].HoverEnabled = false;
            selectionCards[i].ResetScale();

            // 全カードのボタンを無効化
            var btn = selectionCards[i].GetComponent<Button>();
            if (btn != null) btn.interactable = false;

            if (i == index)
            {
                // 選択カード: Selected 状態に変更 + 点滅開始
                selectionCards[i].SetState(GemRewardCardUI.CardState.Selected);
                selectionCards[i].StartBlink();
            }
            else
            {
                // 非選択カード: アニメーションで Dimmed 状態へ移行
                selectionCards[i].SetState(GemRewardCardUI.CardState.Dimmed);
                StartCoroutine(selectionCards[i].DimExitCoroutine());
            }
        }

        StartCoroutine(ShowConfirmWithDelay(0.15f));
    }

    private IEnumerator ShowConfirmWithDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (confirmDialog == null) yield break;

        var cg = confirmDialog.GetComponent<CanvasGroup>();
        if (cg == null) cg = confirmDialog.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        confirmDialog.SetActive(true);

        float elapsed = 0f;
        while (elapsed < confirmFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / confirmFadeInDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    // =====================================================
    // Confirm Dialog
    // =====================================================

    private void OnConfirmYes()
    {
        if (selectedIndex < 0 || rolledGems == null) return;
        PlaySE(confirmYesSE);
        GemManager.Instance?.AddGemToInventory(rolledGems[selectedIndex]);
        StartCoroutine(OpenChestThenPhase2());
    }

    private IEnumerator OpenChestThenPhase2()
    {
        // ConfirmBgを非表示にしてから演出
        if (confirmDialog != null) confirmDialog.SetActive(false);
        if (selectionCards[selectedIndex] != null)
        {
            selectionCards[selectedIndex].StopBlink();
            yield return StartCoroutine(selectionCards[selectedIndex].OpenChestCoroutine());
        }
        OnClose();
    }

    private void OnConfirmNo()
    {
        PlaySE(confirmNoSE);
        StartCoroutine(HideConfirmAndRestore());
    }

    private IEnumerator HideConfirmAndRestore()
    {
        if (confirmDialog != null)
        {
            var cg = confirmDialog.GetComponent<CanvasGroup>();
            if (cg == null) cg = confirmDialog.AddComponent<CanvasGroup>();

            cg.interactable = false;
            float elapsed = 0f;
            while (elapsed < confirmFadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(1f - elapsed / confirmFadeOutDuration);
                yield return null;
            }
            cg.alpha = 1f;
            cg.interactable = true;
            confirmDialog.SetActive(false);
        }

        selectedIndex = -1;
        foreach (var card in selectionCards)
        {
            if (card == null) continue;
            card.StopBlink();
            card.SetState(GemRewardCardUI.CardState.Normal);
            card.HoverEnabled = true;
            card.ResetScale();
            var btn = card.GetComponent<Button>();
            if (btn != null) btn.interactable = true;
        }
    }

    // =====================================================
    // Phase 2 – 結果
    // =====================================================


    private IEnumerator Phase2Sequence()
    {
        confirmDialog?.SetActive(false);
        phase1Panel?.SetActive(false);
        dimPanel?.SetActive(false);
        foreach (var card in selectionCards) card?.StopBlink();

        // 非選択インデックスを収集
        var unselected = new List<int>();
        for (int i = 0; i < rolledGems.Length; i++)
            if (i != selectedIndex) unselected.Add(i);

        // Phase2 カードをセットアップ
        SetupPhase2Card(phase2SelectedCard,    rolledGems[selectedIndex],       GemRewardCardUI.CardState.ResultSelected);
        if (unselected.Count > 0) SetupPhase2Card(phase2UnselectedCard0, rolledGems[unselected[0]], GemRewardCardUI.CardState.ResultUnselected);
        if (unselected.Count > 1) SetupPhase2Card(phase2UnselectedCard1, rolledGems[unselected[1]], GemRewardCardUI.CardState.ResultUnselected);

        // Phase2 の全カードはタップ不可（結果表示のみ）
        foreach (var c in new[] { phase2SelectedCard, phase2UnselectedCard0, phase2UnselectedCard1 })
        {
            var btn = c?.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        // selectedIndex の位置に SelectedCard が来るよう HLG の並び順を変更
        // selectedIndex=0 → [Selected][Unsel0][Unsel1]
        // selectedIndex=1 → [Unsel0][Selected][Unsel1]
        // selectedIndex=2 → [Unsel0][Unsel1][Selected]
        {
            var slots = new GemRewardCardUI[3];
            int unselSlot = 0;
            for (int i = 0; i < 3; i++)
            {
                slots[i] = (i == selectedIndex)
                    ? phase2SelectedCard
                    : (unselSlot++ == 0 ? phase2UnselectedCard0 : phase2UnselectedCard1);
            }
            for (int i = 0; i < slots.Length; i++)
                slots[i].transform.SetSiblingIndex(i);
        }

        // SelectedCard のみ alpha=0 から開始
        // UnselectedCards は SetState(ResultUnselected) が設定した alpha=0.45f のまま維持
        // （alpha=0 にすると DimPanel が透けて黒い四角が見え、FadeIn 開始時に点滅する原因になる）
        phase2SelectedCard?.SetAlpha(0f);
        if (closeButton != null) closeButton.interactable = false;

        if (phase2TitleText != null) phase2TitleText.text = "";
        phase2Panel?.SetActive(true);

        // 間を空けてからSelectedCard表示
        yield return new WaitForSecondsRealtime(cardRevealDelay);

        // SE再生 & タイトル打ち込み & 選択ジェムフェードイン を並行実行
        PlaySE(cardRevealSE);
        StartCoroutine(TypewriterCoroutine(phase2TitleText, phase2TitleStr, titleCharInterval));
        yield return StartCoroutine(FadeIn(phase2SelectedCard, cardRevealDuration));

        // ② バウンス
        yield return StartCoroutine(phase2SelectedCard.BounceCoroutine());

        // ③ 少し待ってから Close ボタンを有効化 & 自動遷移開始
        yield return new WaitForSecondsRealtime(unselectedRevealDelay);
        if (closeButton != null) closeButton.interactable = true;
        autoTransitCoroutine = StartCoroutine(AutoTransitCoroutine());
    }

    private IEnumerator AutoTransitCoroutine()
    {
        yield return new WaitForSecondsRealtime(autoTransitDelay);
        OnClose();
    }

    private void SetupPhase2Card(GemRewardCardUI card, GemInstance gem, GemRewardCardUI.CardState state)
    {
        if (card == null || gem == null) return;
        var bonus1 = GemManager.Instance.LoadBonusSkill1(gem);
        var bonus2 = GemManager.Instance.LoadBonusSkill2(gem);
        card.Setup(gem, currentGemDef, bonus1, bonus2);
        card.SetState(state);
    }

    private IEnumerator FadeIn(GemRewardCardUI card, float duration)
    {
        if (card == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            card.SetAlpha(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        card.SetAlpha(1f);
    }

    /// <summary>テキストを1文字ずつ表示する打ち込み演出</summary>
    private IEnumerator TypewriterCoroutine(TextMeshProUGUI label, string fullText, float charInterval = 0.045f)
    {
        if (label == null) yield break;
        label.text = "";
        foreach (char c in fullText)
        {
            label.text += c;
            yield return new WaitForSecondsRealtime(charInterval);
        }
    }

    // =====================================================
    // Full Message (上限満杯)
    // =====================================================

    private void ShowFullMessage()
    {
        if (fullMessageText != null) fullMessageText.text = fullMsgStr;

        // SkillHUDを除いた領域の中央に配置
        if (fullBgRect != null)
            fullBgRect.anchoredPosition = new Vector2(skillHudWidth * 0.5f, fullBgRect.anchoredPosition.y);

        stageIntroController?.SetSpotlightsVisible(false);
        BlockItemManager.Instance?.ClearAllItems();

        dimPanel?.SetActive(true);
        fullMessagePanel?.SetActive(true);
    }

    private void OnFullClose()
    {
        PlaySE(closeSE);
        waveTimerUI?.SetPauseButtonVisible(true);
        stageIntroController?.SetSpotlightsVisible(true);
        HideAll();
        gameResultUI?.AutoReturn();
    }

    // =====================================================
    // Close
    // =====================================================

    private void OnClose()
    {
        if (autoTransitCoroutine != null) { StopCoroutine(autoTransitCoroutine); autoTransitCoroutine = null; }
        PlaySE(closeSE);
        waveTimerUI?.SetPauseButtonVisible(true);
        HideAll();
        gameResultUI?.AutoReturn();
    }

    // =====================================================
    // SE
    // =====================================================

    private void PlaySE(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, vol);
    }

    // =====================================================
    // ContextMenu Setup（初回一度だけ実行）
    // =====================================================

#if UNITY_EDITOR
    /// <summary>
    /// YesButton / NoButton に UIButtonEffect を追加する（初回一度だけ実行）
    /// </summary>
    [ContextMenu("Add Button Effects to Confirm Buttons")]
    private void AddButtonEffectsToConfirmButtons()
    {
        foreach (var btn in new[] { confirmYesButton, confirmNoButton })
        {
            if (btn == null) continue;
            if (btn.GetComponent<UIButtonEffect>() == null)
                btn.gameObject.AddComponent<UIButtonEffect>();
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemRewardUI] UIButtonEffect を YesButton / NoButton に追加しました。");
    }

    /// <summary>
    /// Phase1 CardRow の横位置を SkillHUD 幅を除いたエリアの中央に調整する
    /// ※ Hierarchy を削除しない安全な操作
    /// </summary>
    [ContextMenu("Adjust Phase1 CardRow Position")]
    private void AdjustPhase1CardRowPosition()
    {
        if (phase1CardRow == null)
        {
            Debug.LogError("[GemRewardUI] phase1CardRow が未設定です。Inspector で CardRow を割り当ててください。");
            return;
        }

        float offsetX = skillHudWidth / 2f;
        var pos = phase1CardRow.anchoredPosition;
        pos.x = offsetX;
        phase1CardRow.anchoredPosition = pos;

        UnityEditor.EditorUtility.SetDirty(phase1CardRow);
        Debug.Log($"[GemRewardUI] Phase1 CardRow position adjusted: x = {offsetX} (skillHudWidth={skillHudWidth})");
    }

    /// <summary>
    /// Phase2 SelectedCard にグローImageを追加する（初回一度だけ実行）
    /// ※ Hierarchy を削除しない安全な操作
    /// </summary>
    [ContextMenu("Add Glow to Phase2 SelectedCard")]
    private void AddGlowToPhase2SelectedCard()
    {
        if (phase2SelectedCard == null)
        {
            Debug.LogError("[GemRewardUI] phase2SelectedCard が未設定です。");
            return;
        }

        // 既存の Glow を削除
        var existing = phase2SelectedCard.transform.Find("Glow");
        if (existing != null) DestroyImmediate(existing.gameObject);

        var cardRT = phase2SelectedCard.GetComponent<RectTransform>();
        float pad = 60f;
        var size = cardRT != null
            ? new Vector2(cardRT.sizeDelta.x + pad * 2, cardRT.sizeDelta.y + pad * 2)
            : new Vector2(570f, 670f);

        var glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(phase2SelectedCard.transform, false);
        glowObj.transform.SetSiblingIndex(0); // CardBg の後ろ

        var rt = glowObj.AddComponent<RectTransform>();
        rt.anchorMin         = new Vector2(0.5f, 0.5f);
        rt.anchorMax         = new Vector2(0.5f, 0.5f);
        rt.pivot             = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition  = Vector2.zero;
        rt.sizeDelta         = size;

        var img = glowObj.AddComponent<Image>();
        img.color          = Color.white; // スプライトのalpha値をそのまま使う
        img.raycastTarget  = false;

        var so = new UnityEditor.SerializedObject(phase2SelectedCard);
        so.FindProperty("glowImage").objectReferenceValue = img;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(phase2SelectedCard);

        glowObj.SetActive(false); // 初期は非表示
        Debug.Log("[GemRewardUI] Phase2 SelectedCard に Glow を追加しました。");
    }

    [ContextMenu("Adjust Close Button Position")]
    private void AdjustCloseButtonPosition()
    {
        if (closeButton == null)
        {
            Debug.LogError("[GemRewardUI] closeButton が未設定です。");
            return;
        }
        float offsetX = skillHudWidth / 2f;
        var rt = closeButton.GetComponent<RectTransform>();
        var pos = rt.anchoredPosition;
        pos.x = offsetX;
        rt.anchoredPosition = pos;
        UnityEditor.EditorUtility.SetDirty(rt);
        Debug.Log($"[GemRewardUI] CloseButton position adjusted: x = {offsetX}");
    }

    [ContextMenu("Adjust Phase2 CardRow Position")]
    private void AdjustPhase2CardRowPosition()
    {
        if (phase2CardRow == null)
        {
            Debug.LogError("[GemRewardUI] phase2CardRow が未設定です。Inspector で CardRow を割り当ててください。");
            return;
        }

        float offsetX = skillHudWidth / 2f;
        var pos = phase2CardRow.anchoredPosition;
        pos.x = offsetX;
        phase2CardRow.anchoredPosition = pos;

        UnityEditor.EditorUtility.SetDirty(phase2CardRow);
        Debug.Log($"[GemRewardUI] Phase2 CardRow position adjusted: x = {offsetX} (skillHudWidth={skillHudWidth})");
    }

    /// <summary>
    /// Phase1・Phase2 の TitleText を SkillHUD 幅を除いたエリアの中央に調整する
    /// ※ Hierarchy を削除しない安全な操作
    /// </summary>
    [ContextMenu("Adjust Title Text Position")]
    private void AdjustTitleTextPosition()
    {
        float offsetX = skillHudWidth / 2f;

        if (phase1TitleText != null)
        {
            var rt = phase1TitleText.GetComponent<RectTransform>();
            var pos = rt.anchoredPosition;
            pos.x = offsetX;
            rt.anchoredPosition = pos;
            UnityEditor.EditorUtility.SetDirty(rt);
        }

        if (phase2TitleText != null)
        {
            var rt = phase2TitleText.GetComponent<RectTransform>();
            var pos = rt.anchoredPosition;
            pos.x = offsetX;
            rt.anchoredPosition = pos;
            UnityEditor.EditorUtility.SetDirty(rt);
        }

        Debug.Log($"[GemRewardUI] TitleText positions adjusted: x = {offsetX} (skillHudWidth={skillHudWidth})");
    }

    /// <summary>
    /// ConfirmDialog の ConfirmBg を SkillHUD 幅を除いたエリアの中央に調整する
    /// ※ Hierarchy を削除しない安全な操作
    /// </summary>
    [ContextMenu("Adjust Confirm Dialog Position")]
    private void AdjustConfirmDialogPosition()
    {
        if (confirmDialog == null)
        {
            Debug.LogError("[GemRewardUI] confirmDialog が未設定です。");
            return;
        }

        var confirmBg = confirmDialog.transform.Find("ConfirmBg");
        if (confirmBg == null)
        {
            Debug.LogError("[GemRewardUI] ConfirmBg が見つかりません。");
            return;
        }

        float offsetX = skillHudWidth / 2f;
        var rt = confirmBg.GetComponent<RectTransform>();
        var pos = rt.anchoredPosition;
        pos.x = offsetX;
        rt.anchoredPosition = pos;

        UnityEditor.EditorUtility.SetDirty(rt);
        Debug.Log($"[GemRewardUI] ConfirmBg position adjusted: x = {offsetX} (skillHudWidth={skillHudWidth})");
    }

    [ContextMenu("Setup Gem Reward UI")]
    private void SetupGemRewardUI()
    {
        // 既存子オブジェクトをすべて削除
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var so = new UnityEditor.SerializedObject(this);

        // ---- DimPanel ----
        var dim = CreateFullScreenPanel("DimPanel", new Color(0f, 0f, 0f, 0.75f));
        dim.SetActive(false);
        so.FindProperty("dimPanel").objectReferenceValue = dim;

        // ---- Phase1Panel ----
        var p1 = CreateFullScreenPanel("Phase1Panel", Color.clear);
        p1.SetActive(false);
        so.FindProperty("phase1Panel").objectReferenceValue = p1;

        var p1Title = CreateTMP("TitleText", p1.transform, "ジェムを1つ選んでください", 36f,
            new Vector2(0f, 380f), new Vector2(1400f, 60f));
        so.FindProperty("phase1TitleText").objectReferenceValue = p1Title;

        // Phase1 CardRow（HLG）
        var cardRow = CreateHLGRow("CardRow", p1.transform, new Vector2(1400f, 520f), new Vector2(0f, -30f), 40f);

        var c0 = CreateGemCard("GemCard0", cardRow.transform, 380f, 500f, so);
        var c1 = CreateGemCard("GemCard1", cardRow.transform, 380f, 500f, so);
        var c2 = CreateGemCard("GemCard2", cardRow.transform, 380f, 500f, so);
        so.FindProperty("card0").objectReferenceValue = c0;
        so.FindProperty("card1").objectReferenceValue = c1;
        so.FindProperty("card2").objectReferenceValue = c2;

        // ---- ConfirmDialog ----
        var confirm = CreateFullScreenPanel("ConfirmDialog", Color.clear);
        confirm.SetActive(false);
        so.FindProperty("confirmDialog").objectReferenceValue = confirm;

        var confirmBg = CreateBoxPanel("ConfirmBg", confirm.transform, new Vector2(640f, 300f), Vector2.zero,
            new Color(0.08f, 0.08f, 0.18f, 0.97f));

        var confirmMsg = CreateTMP("MessageText", confirmBg.transform, "このジェムを取得しますか？", 30f,
            new Vector2(0f, 80f), new Vector2(560f, 60f));
        so.FindProperty("confirmMessageText").objectReferenceValue = confirmMsg;

        var yesBtn = CreateButton("YesButton", confirmBg.transform, "はい",
            new Vector2(-120f, -90f), new Vector2(200f, 64f), new Color(0.2f, 0.45f, 0.25f, 1f));
        var noBtn  = CreateButton("NoButton",  confirmBg.transform, "いいえ",
            new Vector2( 120f, -90f), new Vector2(200f, 64f), new Color(0.45f, 0.2f, 0.2f, 1f));
        so.FindProperty("confirmYesButton").objectReferenceValue = yesBtn;
        so.FindProperty("confirmNoButton").objectReferenceValue  = noBtn;

        // ---- FullMessagePanel ----
        var full = CreateFullScreenPanel("FullMessagePanel", Color.clear);
        full.SetActive(false);
        so.FindProperty("fullMessagePanel").objectReferenceValue = full;

        var fullBg = CreateBoxPanel("FullBg", full.transform, new Vector2(860f, 320f),
            new Vector2(skillHudWidth * 0.5f, 0f),
            new Color(0.08f, 0.08f, 0.18f, 0.97f));
        so.FindProperty("fullBgRect").objectReferenceValue = fullBg.GetComponent<RectTransform>();

        var fullMsg = CreateTMP("MessageText", fullBg.transform,
            "ジェムの所持数が上限（30個）です。\nジェムを売却してから再挑戦してください。", 26f,
            new Vector2(0f, 90f), new Vector2(780f, 100f));
        fullMsg.enableWordWrapping = true;
        so.FindProperty("fullMessageText").objectReferenceValue = fullMsg;

        var fullClose = CreateButton("CloseButton", fullBg.transform, "閉じる",
            new Vector2(0f, -105f), new Vector2(220f, 64f), new Color(0.25f, 0.25f, 0.45f, 1f));
        so.FindProperty("fullCloseButton").objectReferenceValue = fullClose;

        // ---- Phase2Panel ----
        var p2 = CreateFullScreenPanel("Phase2Panel", Color.clear);
        p2.SetActive(false);
        so.FindProperty("phase2Panel").objectReferenceValue = p2;

        var p2Title = CreateTMP("TitleText", p2.transform, "ジェムを取得しました！", 36f,
            new Vector2(0f, 380f), new Vector2(1400f, 60f));
        so.FindProperty("phase2TitleText").objectReferenceValue = p2Title;

        // Phase2 CardRow: [非選択0][選択(中央・大)][非選択1]
        var p2CardRow = CreateHLGRow("CardRow", p2.transform, new Vector2(1400f, 580f), new Vector2(0f, -20f), 40f);

        var unsel0 = CreateGemCard("Unselected0",  p2CardRow.transform, 340f, 430f, so);
        var selCard = CreateGemCard("SelectedCard", p2CardRow.transform, 450f, 550f, so);
        var unsel1  = CreateGemCard("Unselected1",  p2CardRow.transform, 340f, 430f, so);
        so.FindProperty("phase2UnselectedCard0").objectReferenceValue = unsel0;
        so.FindProperty("phase2SelectedCard").objectReferenceValue    = selCard;
        so.FindProperty("phase2UnselectedCard1").objectReferenceValue = unsel1;

        var closeBtn = CreateButton("CloseButton", p2.transform, "閉じる",
            new Vector2(0f, -420f), new Vector2(220f, 64f), new Color(0.25f, 0.25f, 0.45f, 1f));
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;

        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GemRewardUI] Setup complete! Assign GameResultUI in Inspector.");
    }

    // ---- ContextMenu Helper Methods ----

    private GameObject CreateFullScreenPanel(string name, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        var img = obj.AddComponent<Image>();
        img.color = color;
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return obj;
    }

    private GameObject CreateBoxPanel(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<Image>().color = color;
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return obj;
    }

    private TextMeshProUGUI CreateTMP(string name, Transform parent, string text, float fontSize,
        Vector2 anchoredPos, Vector2 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    private Button CreateButton(string name, Transform parent, string label,
        Vector2 pos, Vector2 size, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<Image>().color = color;
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var btn = obj.AddComponent<Button>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        var textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 26f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private GameObject CreateHLGRow(string name, Transform parent, Vector2 size, Vector2 pos, float spacing)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment       = TextAnchor.MiddleCenter;
        hlg.childControlWidth    = false;
        hlg.childControlHeight   = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = spacing;
        return obj;
    }

    private GemRewardCardUI CreateGemCard(string name, Transform parent, float width, float height,
        UnityEditor.SerializedObject parentSO)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);

        obj.AddComponent<CanvasGroup>();

        // Button（タップ用）
        var btn = obj.AddComponent<Button>();

        // 背景 Image
        var bgObj = new GameObject("CardBg");
        bgObj.transform.SetParent(obj.transform, false);
        var bgRT = bgObj.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.25f, 1f);
        btn.targetGraphic = bgImg;

        // ジェム名 Text
        var nameObj = new GameObject("GemNameText");
        nameObj.transform.SetParent(obj.transform, false);
        var nameRT = nameObj.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot     = new Vector2(0.5f, 1f);
        nameRT.anchoredPosition = new Vector2(0f, -10f);
        nameRT.sizeDelta = new Vector2(0f, 48f);
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = "Gem Name";
        nameTMP.fontSize  = 24f;
        nameTMP.color     = Color.white;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.enableWordWrapping = false;

        // セパレータ
        var sep = new GameObject("Separator");
        sep.transform.SetParent(obj.transform, false);
        var sepRT = sep.AddComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0.05f, 1f);
        sepRT.anchorMax = new Vector2(0.95f, 1f);
        sepRT.pivot     = new Vector2(0.5f, 1f);
        sepRT.anchoredPosition = new Vector2(0f, -62f);
        sepRT.sizeDelta = new Vector2(0f, 2f);
        sep.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);

        // SkillContainer（VLG）
        var skillCont = new GameObject("SkillContainer");
        skillCont.transform.SetParent(obj.transform, false);
        var skillRT = skillCont.AddComponent<RectTransform>();
        skillRT.anchorMin = new Vector2(0f, 0f);
        skillRT.anchorMax = new Vector2(1f, 1f);
        skillRT.offsetMin = new Vector2(10f, 10f);
        skillRT.offsetMax = new Vector2(-10f, -72f);
        var vlg = skillCont.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment       = TextAnchor.UpperLeft;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(8, 8, 8, 8);

        // GemRewardCardUI コンポーネントを追加してフィールドを設定
        var cardUI = obj.AddComponent<GemRewardCardUI>();
        var cardSO = new UnityEditor.SerializedObject(cardUI);
        cardSO.FindProperty("cardBgImage").objectReferenceValue    = bgImg;
        cardSO.FindProperty("gemNameText").objectReferenceValue    = nameTMP;
        cardSO.FindProperty("skillContainer").objectReferenceValue = skillCont.transform;
        cardSO.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(cardUI);

        return cardUI;
    }
#endif
}
