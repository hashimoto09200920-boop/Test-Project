using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Localization;
using Game.UI;

/// <summary>
/// 03_AreaSelectのミッション画面（2026/11/07）。
/// ・左側の「MissionButton」で開き、左上の閉じるボタンで閉じる。開いている間は後ろのボタンのホバー演出を止める（ButtonHoverEffect.ModalPanelRoot）
/// ・一覧は「RowTemplate」（非表示の見本）をプレイ中に50行分複製して並べる。並び順は 受け取れる → 進行中 → 受取済
/// ・「受け取る」：受け取れる間は光がゆっくり強弱、押すと「+300 G」等の文字が浮かび行が光ってから並べ替える。報酬の種類で効果音を変える
/// ・右上の達成数は「達成」＋数字＋ゲージ。受け取れる件数はMissionButtonの右上のバッジに出す
/// ・文字のフォントは各文字のLanguageFontSwitch（言語で切替）と、数字だけの文字はGameNumberFont（Bungee）
/// Hierarchyは Tools > ミッション >「6. ミッション画面を作成」で作る。見た目はすべてPlay前にInspectorで調整できる。
/// </summary>
public class MissionPanelUI : MonoBehaviour
{
    [Header("AreaSelectのボタンとバッジ")]
    [SerializeField] private Button missionButton;
    [SerializeField] private GameObject badgeRoot;
    [SerializeField] private TextMeshProUGUI badgeText;
    [Tooltip("この数より多い時は「9+」のように表示する")]
    [SerializeField] private int badgeMaxCount = 9;
    [Tooltip("バッジの鼓動（ドクン）の間隔（秒）。0で鼓動しない")]
    [SerializeField] private float badgeBeatInterval = 1.2f;
    [Tooltip("ドクンの時に大きくなる倍率")]
    [SerializeField] private float badgeBeatScale = 1.25f;
    [Tooltip("ドクン1回にかける時間（秒）。大きくなって戻るまで")]
    [SerializeField] private float badgeBeatDuration = 0.35f;

    [Header("ミッション画面")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI headerMissionText;
    [SerializeField] private TextMeshProUGUI headerProgressText;
    [SerializeField] private TextMeshProUGUI headerRewardText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private MissionRowUI rowTemplate;

    [Header("右上の達成数（「達成」＋数字＋ゲージ）")]
    [SerializeField] private TextMeshProUGUI achievedLabelText;
    [SerializeField] private TextMeshProUGUI achievedCountText;
    [SerializeField] private TextMeshProUGUI achievedTotalText;
    [Tooltip("ゲージの中身。横の長さを「受け取った数／全体」にする")]
    [SerializeField] private RectTransform achievedGaugeFill;

    [Header("受け取った時に浮かぶ文字（非表示の見本）")]
    [SerializeField] private TextMeshProUGUI claimPopTemplate;
    [SerializeField] private float claimPopRise = 70f;
    [SerializeField] private float claimPopDuration = 0.9f;
    [Tooltip("受け取った後、行が光ってから並べ替えるまでの時間（秒）")]
    [SerializeField] private float claimResortDelay = 0.6f;
    [SerializeField] private Color rowFlashColor = new Color(0.85f, 1f, 0.6f, 1f);
    [SerializeField] private string passivePopJa = "+パッシブ";
    [SerializeField] private string passivePopEn = "+PASSIVE";
    [Tooltip("虹の雫を受け取った時に浮かぶ文字（{0}=個数）")]
    [SerializeField] private string rainbowDropPopJa = "+虹の雫 ×{0}";
    [SerializeField] private string rainbowDropPopEn = "+PRISM DROP ×{0}";
    [SerializeField] private string foodTicketPopJa = "+お食事券 ×{0}";
    [Tooltip("お知らせのミッション（次のエリアの解放・スロット+1）の報酬の欄の文字（日本語）。エリア名はまだ出さない")]
    [SerializeField] private string noticeValueJa = "次のエリア・スロット+1";
    [SerializeField] private string noticeValueEn = "Next area / Slot +1";
    [SerializeField] private string noticeTypeJa = "解放";
    [SerializeField] private string noticeTypeEn = "UNLOCK";
    [SerializeField] private Color noticeTypeColor = new Color(0.35f, 0.9f, 1f, 1f);
    [Header("虹の雫などのミッションに付いたお知らせ（解放・スロット+1）を、報酬の値の下に小さく1行（2026/11/11）")]
    [SerializeField] private string noticeSubJa = "＋次のエリア・スロット+1";
    [SerializeField] private string noticeSubEn = "+ Next area / Slot +1";
    [Tooltip("報酬の値（×1など）の中心からの、お知らせの行の位置のずれ")]
    [SerializeField] private Vector2 noticeSubOffset = new Vector2(0f, -22f);
    [Tooltip("お知らせの行の文字の大きさ（報酬の値の文字の大きさに対する倍率）")]
    [Range(0.3f, 1f)] [SerializeField] private float noticeSubSizeRatio = 0.6f;
    [SerializeField] private string foodTicketPopEn = "+FOOD TICKET ×{0}";

    [Header("「受け取る」ボタンの呼吸する光")]
    [SerializeField] private float glowSpeed = 1.6f;
    [SerializeField, Range(0f, 1f)] private float glowAlphaMin = 0.15f;
    [SerializeField, Range(0f, 1f)] private float glowAlphaMax = 0.75f;

    [Header("行の色（各行の背景.pngの白い線に掛ける色）")]
    [SerializeField] private Color rowColorInProgress = new Color(0.45f, 0.85f, 1f, 1f);
    [SerializeField] private Color rowColorClaimable = new Color(0.72f, 1f, 0.23f, 1f);
    [SerializeField] private Color rowColorClaimed = new Color(0.35f, 0.37f, 0.5f, 1f);
    [Tooltip("受け取り済みの行の文字の不透明度")]
    [SerializeField, Range(0f, 1f)] private float claimedTextAlpha = 0.45f;

    [Header("文字の色")]
    [SerializeField] private Color nameColor = Color.white;
    [SerializeField] private Color passiveTypeColor = new Color(0.72f, 1f, 0.23f, 1f);
    [SerializeField] private Color goldTypeColor = new Color(1f, 0.82f, 0.29f, 1f);
    [SerializeField] private Color rainbowDropTypeColor = new Color(0.85f, 0.38f, 0.94f, 1f);
    [SerializeField] private Color foodTicketTypeColor = new Color(1f, 0.45f, 0.7f, 1f);
    [SerializeField, Range(0f, 1f)] private float rewardChipFillAlpha = 0.15f;
    [SerializeField] private Color progressFillColor = new Color(0.45f, 0.85f, 1f, 1f);
    [SerializeField] private Color progressFillDoneColor = new Color(0.72f, 1f, 0.23f, 1f);
    [SerializeField] private Color progressTextColor = new Color(0.55f, 0.58f, 0.75f, 1f);
    [SerializeField] private Color progressTextDoneColor = new Color(0.72f, 1f, 0.23f, 1f);

    [Header("「受け取る」ボタンの色（「受け取る」ボタン.pngに掛ける色）")]
    [SerializeField] private Color claimButtonColor = Color.white;
    [SerializeField] private Color claimButtonDisabledColor = new Color(0.35f, 0.35f, 0.4f, 1f);
    [SerializeField] private Color claimTextColor = Color.white;
    [SerializeField] private Color claimTextDisabledColor = new Color(0.45f, 0.47f, 0.6f, 1f);

    [Header("表示する文字（日本語）")]
    [SerializeField] private string titleJa = "MISSION";
    [SerializeField] private string achievedLabelJa = "達成";
    [SerializeField] private string headerMissionJa = "ミッション";
    [SerializeField] private string headerProgressJa = "進捗";
    [SerializeField] private string headerRewardJa = "報酬";
    [SerializeField] private string passiveJa = "パッシブ";
    [SerializeField] private string goldJa = "ゴールド";
    [SerializeField] private string rainbowDropJa = "虹の雫";
    [SerializeField] private string foodTicketJa = "お食事券";
    [SerializeField] private string claimJa = "受け取る";
    [SerializeField] private string inProgressJa = "進行中";
    [SerializeField] private string claimedJa = "受取済";

    [Header("表示する文字（英語）")]
    [SerializeField] private string titleEn = "MISSION";
    [SerializeField] private string achievedLabelEn = "CLEARED";
    [SerializeField] private string headerMissionEn = "MISSION";
    [SerializeField] private string headerProgressEn = "PROGRESS";
    [SerializeField] private string headerRewardEn = "REWARD";
    [SerializeField] private string passiveEn = "PASSIVE";
    [SerializeField] private string goldEn = "GOLD";
    [SerializeField] private string rainbowDropEn = "PRISM DROP";
    [SerializeField] private string foodTicketEn = "TICKET";
    [SerializeField] private string claimEn = "CLAIM";
    [Tooltip("進行中のボタンの文字（英語）。「IN PROGRESS」はボタンからはみ出したので短い「ONGOING」に（2026/11/12。シーンに保存された古い値を使わないよう欄の名前を変えた）")]
    [SerializeField] private string inProgressEnShort = "ONGOING";
    [SerializeField] private string claimedEn = "CLAIMED";
    [Tooltip("段の札の文字。[0]=2段目、[1]=3段目、[2]=4段目、[3]=5段目")]
    [SerializeField] private string[] tierLabels = { "II", "III", "IV", "V" };
    [SerializeField] private string goldValueFormat = "{0:N0} G";
    [Tooltip("虹の雫の個数の表示（{0}=個数）")]
    [SerializeField] private string rainbowDropValueFormat = "×{0}";

    [Header("効果音（報酬の種類で変える）")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip goldClaimSE;
    [SerializeField, Range(0f, 1f)] private float goldClaimSEVolume = 1f;
    [Tooltip("虹の雫を受け取った時（空ならゴールドと同じ効果音）")]
    [SerializeField] private AudioClip rainbowDropClaimSE;
    [SerializeField, Range(0f, 1f)] private float rainbowDropClaimSEVolume = 1f;
    [Tooltip("パッシブ（CategoryAのスキル）を受け取った時")]
    [SerializeField] private AudioClip passiveSkillClaimSE;
    [SerializeField, Range(0f, 1f)] private float passiveSkillClaimSEVolume = 1f;
    [Tooltip("パッシブ（CategoryA以外）を受け取った時")]
    [SerializeField] private AudioClip passiveOtherClaimSE;
    [SerializeField, Range(0f, 1f)] private float passiveOtherClaimSEVolume = 1f;
    [Tooltip("画面を開く・閉じる時（空なら鳴らさない）")]
    [SerializeField] private AudioClip openCloseSE;
    [SerializeField, Range(0f, 1f)] private float openCloseSEVolume = 1f;

    private readonly List<MissionRowUI> rows = new List<MissionRowUI>();
    private bool rowsBuilt;
    private bool claimAnimating;

    private static bool IsEnglish => LocalizationManager.CurrentLanguageStatic == GameLanguage.English;

    private void Awake()
    {
        if (missionButton != null) missionButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (rowTemplate != null)
        {
            // 数字だけの文字は数字用フォント（Bungee）に。見本に1回だけ掛ければ、複製した行も同じになる
            GameNumberFont.Apply(rowTemplate.progressText);
            GameNumberFont.Apply(rowTemplate.rewardGoldText);
            rowTemplate.gameObject.SetActive(false);
        }
        GameNumberFont.Apply(achievedCountText);
        GameNumberFont.Apply(achievedTotalText);
        GameNumberFont.Apply(badgeText);
        // 浮かぶ文字は「+パッシブ」のように日本語も出すので、数字用フォントにはしない（LanguageFontSwitchの見出し用フォント）
        if (claimPopTemplate != null) claimPopTemplate.gameObject.SetActive(false);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        MissionProgressManager.OnMissionClaimed += HandleClaimed;
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        RefreshBadge();
    }

    private void OnDisable()
    {
        MissionProgressManager.OnMissionClaimed -= HandleClaimed;
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        if (ButtonHoverEffect.ModalPanelRoot == (panelRoot != null ? panelRoot.transform : null)) ButtonHoverEffect.ModalPanelRoot = null;
    }

    private void Start() => RefreshBadge();

    private void Update()
    {
        UpdateBadgeBeat();
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;
        // 受け取れる行の「受け取る」ボタンの光をゆっくり強弱させる
        float t = (Mathf.Sin(Time.unscaledTime * glowSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float a = Mathf.Lerp(glowAlphaMin, glowAlphaMax, t);
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.claimGlow == null || !r.claimGlow.gameObject.activeInHierarchy) continue;
            var c = r.claimGlow.color; c.a = a; r.claimGlow.color = c;
        }
    }

    // =====================================================================
    // 開く・閉じる
    // =====================================================================

    public void Open()
    {
        if (panelRoot == null) return;
        BuildRowsIfNeeded();
        panelRoot.SetActive(true);
        ButtonHoverEffect.ModalPanelRoot = panelRoot.transform; // 後ろのAreaSelectのボタンのホバー演出を止める
        RefreshAll();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        PlaySE(openCloseSE, openCloseSEVolume);
    }

    public void Close()
    {
        if (panelRoot == null) return;
        if (ButtonHoverEffect.ModalPanelRoot == panelRoot.transform) ButtonHoverEffect.ModalPanelRoot = null;
        panelRoot.SetActive(false);
        claimAnimating = false;
        RefreshBadge();
        PlaySE(openCloseSE, openCloseSEVolume);
    }

    // =====================================================================
    // 一覧
    // =====================================================================

    private void BuildRowsIfNeeded()
    {
        if (rowsBuilt) return;
        rowsBuilt = true;
        var mgr = MissionProgressManager.Instance;
        if (mgr == null || mgr.Catalog == null || rowTemplate == null || content == null)
        {
            Debug.LogWarning("[MissionPanelUI] ミッション一覧（MissionCatalog）か、RowTemplate/Contentが見つかりません");
            return;
        }
        foreach (var def in mgr.Catalog.missions)
        {
            if (def == null) continue;
            var row = Instantiate(rowTemplate, content);
            row.name = $"Row_{def.id}";
            row.definition = def;
            if (row.claimButton != null)
            {
                var captured = row;
                row.claimButton.onClick.AddListener(() => OnClaimPressed(captured));
            }
            rows.Add(row);
        }
    }

    private void RefreshAll()
    {
        ApplyStaticTexts();
        var mgr = MissionProgressManager.Instance;
        if (mgr == null) return;

        // 並び順：受け取れる(0) → 進行中(1) → 受取済(2)。同じ状態の中は一覧の順番
        var visible = new List<(MissionRowUI row, int state, int index)>();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var def = row.definition;
            bool show = mgr.IsVisible(def);
            row.gameObject.SetActive(show);
            if (!show) continue;
            int state = mgr.IsClaimed(def.id) ? 2 : mgr.IsAchieved(def) ? 0 : 1;
            ApplyRow(row, def, state, mgr);
            visible.Add((row, state, i));
        }
        // ★2026/11/11 同じ状態の中は、カテゴリ順（エリア→線・反射→撃破→スローモーション→アイテム→その他）、その中は一覧の順番
        visible.Sort((a, b) =>
        {
            if (a.state != b.state) return a.state.CompareTo(b.state);
            // 報酬の種類の順（解放→パッシブ→ゴールド→お食事券→虹の雫）（2026/11/11 ユーザー指示）
            int ra = RewardOrder(a.row.definition), rb = RewardOrder(b.row.definition);
            if (ra != rb) return ra.CompareTo(rb);
            int ca = a.row.definition.Category, cb = b.row.definition.Category;
            if (ca != cb) return ca.CompareTo(cb);
            return a.index.CompareTo(b.index);
        });
        for (int i = 0; i < visible.Count; i++) visible[i].row.transform.SetSiblingIndex(i + 1); // 0番目はRowTemplate

        int total = mgr.Catalog != null ? mgr.Catalog.missions.Count : 0;
        int claimed = mgr.GetClaimedCount();
        SetText(achievedCountText, claimed.ToString());
        SetText(achievedTotalText, $"/ {total}");
        if (achievedGaugeFill != null)
        {
            float r = total > 0 ? (float)claimed / total : 0f;
            achievedGaugeFill.anchorMin = Vector2.zero;
            achievedGaugeFill.anchorMax = new Vector2(Mathf.Clamp01(r), 1f);
            achievedGaugeFill.offsetMin = Vector2.zero;
            achievedGaugeFill.offsetMax = Vector2.zero;
        }
        RefreshBadge();
    }

    private void ApplyStaticTexts()
    {
        bool en = IsEnglish;
        SetText(titleText, en ? titleEn : titleJa);
        SetText(achievedLabelText, en ? achievedLabelEn : achievedLabelJa);
        SetText(headerMissionText, en ? headerMissionEn : headerMissionJa);
        SetText(headerProgressText, en ? headerProgressEn : headerProgressJa);
        SetText(headerRewardText, en ? headerRewardEn : headerRewardJa);
    }

    private void ApplyRow(MissionRowUI row, MissionDefinition def, int state, MissionProgressManager mgr)
    {
        bool en = IsEnglish;
        bool claimed = state == 2;
        bool claimable = state == 0;
        row.claimable = claimable;
        float textAlpha = claimed ? claimedTextAlpha : 1f;

        if (row.background != null)
            row.background.color = claimed ? rowColorClaimed : claimable ? rowColorClaimable : rowColorInProgress;

        SetText(row.nameText, def.GetDisplayName(en), textAlpha, nameColor);
        // 2段目以降は段の札（II〜V）を出す
        bool showTier = !string.IsNullOrEmpty(def.requiresClaimedId) && def.passiveTier >= 2;
        if (row.tierTag != null) row.tierTag.SetActive(showTier);
        if (showTier) SetText(row.tierText, TierLabel(def.passiveTier), textAlpha);

        int progress = Mathf.Min(mgr.GetProgress(def), def.target);
        bool done = progress >= def.target;
        row.SetProgress01(def.target > 0 ? (float)progress / def.target : 0f);
        if (row.progressFillImage != null) row.progressFillImage.color = done ? progressFillDoneColor : progressFillColor;
        SetText(row.progressText, $"{progress:N0} / {def.target:N0}", textAlpha, done ? progressTextDoneColor : progressTextColor);

        bool isGold = def.rewardType == MissionRewardType.Gold;
        bool isDrop = def.rewardType == MissionRewardType.RainbowDrop;
        bool isTicket = def.rewardType == MissionRewardType.FoodTicket;
        bool isNotice = def.rewardType == MissionRewardType.Notice;
        Color typeColor = isGold ? goldTypeColor : isDrop ? rainbowDropTypeColor : isTicket ? foodTicketTypeColor : isNotice ? noticeTypeColor : passiveTypeColor;
        string typeLabel = isGold ? (en ? goldEn : goldJa) : isDrop ? (en ? rainbowDropEn : rainbowDropJa) : isTicket ? (en ? foodTicketEn : foodTicketJa) : isNotice ? (en ? noticeTypeEn : noticeTypeJa) : (en ? passiveEn : passiveJa);
        SetText(row.rewardTypeText, typeLabel, textAlpha, typeColor);
        if (row.rewardChipImage != null) { var cc = typeColor; cc.a = rewardChipFillAlpha * textAlpha; row.rewardChipImage.color = cc; }
        if (row.rewardValueText != null) row.rewardValueText.gameObject.SetActive(!isGold);
        if (row.rewardGoldText != null) row.rewardGoldText.gameObject.SetActive(isGold);
        if (isGold) SetText(row.rewardGoldText, string.Format(goldValueFormat, def.goldAmount), textAlpha, goldTypeColor);
        else if (isDrop) SetText(row.rewardValueText, string.Format(rainbowDropValueFormat, def.rainbowDropAmount), textAlpha, rainbowDropTypeColor);
        else if (isTicket) SetText(row.rewardValueText, string.Format(rainbowDropValueFormat, def.foodTicketAmount), textAlpha, foodTicketTypeColor);
        else if (isNotice) SetText(row.rewardValueText, en ? noticeValueEn : noticeValueJa, textAlpha, noticeTypeColor);
        else SetText(row.rewardValueText, en && !string.IsNullOrEmpty(def.rewardLabelEn) ? def.rewardLabelEn : def.rewardLabel, textAlpha);
        ApplyNoticeSub(row, def, en, textAlpha, !isNotice && !isGold && def.GetNoticeLines(en).Length > 0);

        if (row.claimButton != null) row.claimButton.interactable = claimable;
        if (row.claimButtonImage != null)
        {
            row.claimButtonImage.gameObject.SetActive(!claimed);
            row.claimButtonImage.color = claimable ? claimButtonColor : claimButtonDisabledColor;
        }
        if (row.claimGlow != null) row.claimGlow.gameObject.SetActive(claimable);
        string label = claimed ? (en ? claimedEn : claimedJa) : claimable ? (en ? claimEn : claimJa) : (en ? inProgressEnShort : inProgressJa);
        SetText(row.claimText, label, 1f, claimed ? rowColorClaimable : claimable ? claimTextColor : claimTextDisabledColor);
    }

    /// <summary>
    /// 虹の雫などのミッションにお知らせ（解放・スロット+1）が付いている時、報酬の値（×1）の下に小さく1行出す（2026/11/11）。
    /// 行の部品は報酬の値の文字の子に1回だけ作る（字体は報酬の値と同じ）。お知らせが無い行では隠す
    /// </summary>
    private void ApplyNoticeSub(MissionRowUI row, MissionDefinition def, bool en, float alpha, bool show)
    {
        if (row.rewardValueText == null) return;
        var parent = row.rewardValueText.transform;
        var t = parent.Find("NoticeSub");
        if (!show) { if (t != null) t.gameObject.SetActive(false); return; }
        TextMeshProUGUI sub;
        if (t == null)
        {
            var go = new GameObject("NoticeSub", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            sub = go.AddComponent<TextMeshProUGUI>();
            sub.font = row.rewardValueText.font;
            sub.fontSharedMaterial = row.rewardValueText.fontSharedMaterial;
            sub.alignment = TextAlignmentOptions.Center;
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            sub.overflowMode = TextOverflowModes.Overflow;
            sub.raycastTarget = false;
        }
        else sub = t.GetComponent<TextMeshProUGUI>();
        if (sub == null) return;
        sub.gameObject.SetActive(true);
        var rt = sub.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(row.rewardValueText.rectTransform.rect.width + 120f, 30f);
        rt.anchoredPosition = noticeSubOffset;
        sub.fontSize = row.rewardValueText.fontSize * noticeSubSizeRatio;
        SetText(sub, en ? noticeSubEn : noticeSubJa, alpha, noticeTypeColor);
    }

    /// <summary>ミッション画面で並べる報酬の種類の順（解放→パッシブ→ゴールド→お食事券→虹の雫）</summary>
    private static int RewardOrder(MissionDefinition d)
    {
        switch (d.rewardType)
        {
            case MissionRewardType.Notice: return 0;
            case MissionRewardType.Passive: return 1;
            case MissionRewardType.Gold: return 2;
            case MissionRewardType.FoodTicket: return 3;
            case MissionRewardType.RainbowDrop: return 4;
        }
        return 5;
    }

    private string TierLabel(int tier)
    {
        int i = tier - 2;
        return (tierLabels != null && i >= 0 && i < tierLabels.Length) ? tierLabels[i] : tier.ToString();
    }

    private static void SetText(TextMeshProUGUI t, string s, float alpha = 1f, Color? color = null)
    {
        if (t == null) return;
        t.text = s ?? "";
        Color c = color ?? t.color;
        c.a = alpha;
        t.color = c;
    }

    // =====================================================================
    // 受け取り
    // =====================================================================

    private void OnClaimPressed(MissionRowUI row)
    {
        if (claimAnimating) return; // 演出中の連打を受け付けない
        var mgr = MissionProgressManager.Instance;
        if (mgr == null || row == null) return;
        var def = row.definition;
        claimAnimating = true; // TryClaim中に呼ばれるHandleClaimedで、演出の前に並べ替えてしまわないように先に立てる
        if (!mgr.TryClaim(def)) { claimAnimating = false; return; }

        if (def.rewardType == MissionRewardType.Gold) PlaySE(goldClaimSE, goldClaimSEVolume);
        else if (def.rewardType == MissionRewardType.RainbowDrop)
        {
            if (rainbowDropClaimSE != null) PlaySE(rainbowDropClaimSE, rainbowDropClaimSEVolume);
            else PlaySE(goldClaimSE, goldClaimSEVolume);
        }
        else if (def.rewardType == MissionRewardType.FoodTicket) PlaySE(goldClaimSE, goldClaimSEVolume);
        else if (def.rewardType == MissionRewardType.Notice) PlaySE(passiveOtherClaimSE, passiveOtherClaimSEVolume);
        else if (def.passiveType == MissionPassiveType.CategoryASkill) PlaySE(passiveSkillClaimSE, passiveSkillClaimSEVolume);
        else PlaySE(passiveOtherClaimSE, passiveOtherClaimSEVolume);

        StartCoroutine(ClaimEffectRoutine(row, def));
    }

    private IEnumerator ClaimEffectRoutine(MissionRowUI row, MissionDefinition def)
    {
        if (row.claimButton != null) row.claimButton.interactable = false;
        if (row.claimGlow != null) row.claimGlow.gameObject.SetActive(false);

        // 浮かぶ文字
        if (claimPopTemplate != null && row.claimButton != null)
        {
            var pop = Instantiate(claimPopTemplate, panelRoot.transform);
            pop.gameObject.SetActive(true);
            bool isGold = def.rewardType == MissionRewardType.Gold;
            bool isDrop = def.rewardType == MissionRewardType.RainbowDrop;
            bool isTicket = def.rewardType == MissionRewardType.FoodTicket;
            bool isNotice = def.rewardType == MissionRewardType.Notice;
            pop.text = isNotice ? (IsEnglish ? noticeValueEn : noticeValueJa)
                : isGold ? string.Format("+" + goldValueFormat, def.goldAmount)
                : isDrop ? string.Format(IsEnglish ? rainbowDropPopEn : rainbowDropPopJa, def.rainbowDropAmount)
                : isTicket ? string.Format(IsEnglish ? foodTicketPopEn : foodTicketPopJa, def.foodTicketAmount)
                : (IsEnglish ? passivePopEn : passivePopJa);
            pop.color = isGold ? goldTypeColor : isDrop ? rainbowDropTypeColor : isTicket ? foodTicketTypeColor : isNotice ? noticeTypeColor : passiveTypeColor;
            pop.rectTransform.position = row.claimButton.transform.position;
            StartCoroutine(PopRoutine(pop));
        }

        // 行を一瞬光らせる
        float flash = Mathf.Max(0.05f, claimResortDelay);
        Color baseColor = row.background != null ? row.background.color : Color.white;
        float t = 0f;
        while (t < flash)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / flash);
            if (row.background != null) row.background.color = Color.Lerp(baseColor, rowFlashColor, k);
            yield return null;
        }
        claimAnimating = false;
        if (panelRoot != null && panelRoot.activeInHierarchy) RefreshAll(); // 光り終わってから並べ替える
    }

    private IEnumerator PopRoutine(TextMeshProUGUI pop)
    {
        var rt = pop.rectTransform;
        Vector2 start = rt.anchoredPosition;
        Color c0 = pop.color;
        float t = 0f;
        float dur = Mathf.Max(0.05f, claimPopDuration);
        while (t < dur && pop != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            rt.anchoredPosition = start + new Vector2(0f, claimPopRise * (1f - (1f - k) * (1f - k)));
            var c = c0; c.a = 1f - k * k; pop.color = c;
            yield return null;
        }
        if (pop != null) Destroy(pop.gameObject);
    }

    private void HandleClaimed(MissionDefinition def)
    {
        // 画面を開いている時の並べ替えは、受け取り演出（ClaimEffectRoutine）の最後に行う
        if (panelRoot == null || !panelRoot.activeInHierarchy) RefreshBadge();
        else if (!claimAnimating) RefreshAll();
    }

    private void HandleLanguageChanged()
    {
        if (panelRoot != null && panelRoot.activeInHierarchy && !claimAnimating) RefreshAll();
    }

    // =====================================================================
    // バッジ・効果音
    // =====================================================================

    /// <summary>受け取れる報酬がある間、バッジを心臓の鼓動のように「ドクン」と大きくする（1.2秒ごと）</summary>
    private void UpdateBadgeBeat()
    {
        if (badgeRoot == null || !badgeRoot.activeInHierarchy) return;
        float s = 1f;
        if (badgeBeatInterval > 0f && badgeBeatDuration > 0f)
        {
            float t = Time.unscaledTime % badgeBeatInterval;
            if (t < badgeBeatDuration)
            {
                // 2回打つ鼓動（ドクッ・ドクン）：前半で大きく、後半で少し小さく
                float k = t / badgeBeatDuration;
                float beat = k < 0.5f ? Mathf.Sin(k * 2f * Mathf.PI) : 0.6f * Mathf.Sin((k - 0.5f) * 2f * Mathf.PI);
                s = 1f + (badgeBeatScale - 1f) * Mathf.Max(0f, beat);
            }
        }
        badgeRoot.transform.localScale = new Vector3(s, s, 1f);
    }

    private void RefreshBadge()
    {
        var mgr = MissionProgressManager.Instance;
        int n = mgr != null ? mgr.GetClaimableCount() : 0;
        if (badgeRoot != null) badgeRoot.SetActive(n > 0);
        if (badgeText != null) badgeText.text = n > badgeMaxCount ? $"{badgeMaxCount}+" : n.ToString();
    }

    private void PlaySE(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        float master = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
        audioSource.PlayOneShot(clip, volume * master);
    }
}
