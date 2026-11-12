using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Gems;
using Game.Progress;
using Game.Skills;
using Game.Localization;

/// <summary>
/// ジェム画面（02_Gem）の「ジェムのリロール」（2026/11/08）。
/// ・ジェムを選んでボタンを押す → 確認画面 → ゴールドを払う → スキルとスキルの数を全部引き直す
/// ・値段は売却額の満額×priceMultiplier（使用回数が減っていても満額）
/// ・今とまったく同じスキルの組み合わせは出ない（GemManager.RollReroll）
/// ・演出：金貨がゴールドの所持数からカードへ飛ぶ → 行がスロットのように高速で切り替わる → 1行ずつ止まる → 前と後を並べた結果画面
/// GemManagementUIの処理には手を入れず、選択中のジェム（SelectedGemIndex）とカード（GemItemObjects）を読むだけ。
/// 部品はメニュー「Tools/ジェム画面/ジェムのリロールを組み込む（02_Gemを開いて実行）」でPlay前に作る。位置・色・秒数・文言はInspectorで調整できる
/// </summary>
public class GemRerollUI : MonoBehaviour
{
    [System.Serializable]
    public class ResultRow
    {
        public GameObject root;
        public Image icon;
        public TMP_Text nameText;
        [Tooltip("行の背景（カテゴリ色を塗る）")]
        public Image background;
        [Tooltip("行の左端の縦線（カテゴリ色を鮮やかにした色）")]
        public Image accent;
    }

    [Header("結果画面の行の色（ジェム画面のカードと同じ。メニューでGemManagementUIの値を写す）")]
    [SerializeField] private Color categoryAColor = new Color(0.70f, 0.90f, 0.90f, 0.8f);
    [SerializeField] private Color categoryBColor = new Color(0.79f, 0.70f, 0.90f, 0.8f);
    [SerializeField] private Color categoryCColor = new Color(0.90f, 0.79f, 0.63f, 0.8f);
    [Tooltip("行の高さに対する、アイコンの元の大きさ（SkillDefinitionのIcon Display Size）の倍率。ジェム画面と同じ大きさなら1")]
    [SerializeField] private float rowIconScale = 1f;
    [Header("行の左端の縦線（ジェム画面のGemScreenFXと同じ計算）")]
    [SerializeField] private float accentSaturationScale = 1.6f;
    [SerializeField, Range(0f, 1f)] private float accentBrightness = 1f;
    [SerializeField, Range(0f, 0.3f)] private float accentLumaDiff = 0.05f;
    [SerializeField, Range(0.5f, 1f)] private float rowDisplayBrightness = 0.9f;

    [Header("参照（メニューで自動設定）")]
    [SerializeField] private GemManagementUI gemUI;
    [SerializeField] private Button rerollButton;
    [SerializeField] private Image buttonFrame;
    [SerializeField] private Image buttonIcon;
    [SerializeField] private TMP_Text buttonLabel;
    [SerializeField] private TMP_Text priceText;

    [Header("確認画面（メニューで自動設定）")]
    [SerializeField] private GameObject confirmOverlay;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text goldBeforeText;
    [SerializeField] private TMP_Text goldAfterText;
    [SerializeField] private TMP_Text notEnoughText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("結果画面（メニューで自動設定）")]
    [SerializeField] private GameObject resultOverlay;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultBeforeLabel;
    [SerializeField] private TMP_Text resultAfterLabel;
    [SerializeField] private ResultRow[] beforeRows = new ResultRow[3];
    [SerializeField] private ResultRow[] afterRows = new ResultRow[3];
    [SerializeField] private Button resultOkButton;
    [SerializeField] private TMP_Text resultOkLabel;
    [Tooltip("「リロール結果」の左に置くアイコン（リロールボタンと同じ回転矢印）")]
    [SerializeField] private RectTransform resultTitleIconLeft;
    [Tooltip("「リロール結果」の右に置くアイコン")]
    [SerializeField] private RectTransform resultTitleIconRight;
    [Tooltip("見出しの文字の端からアイコンの中心までの距離(px)。文字の長さ（日本語／英語）に合わせて毎回並べ直す")]
    [SerializeField] private float resultTitleIconGap = 50f;

    [Header("演出の部品（メニューで自動設定）")]
    [Tooltip("画面全体の入れ物（金貨・入力止め）。演出中だけ表示")]
    [SerializeField] private RectTransform fxLayer;
    [Tooltip("金貨の見本（非表示）。演出のたびに複製して飛ばす")]
    [SerializeField] private Image coinTemplate;
    [Tooltip("光の見本（非表示）。行やカードに重ねて光らせる")]
    [SerializeField] private Image flashTemplate;
    [Tooltip("金貨が飛び立つ場所（右上のゴールドのアイコン）")]
    [SerializeField] private RectTransform goldTarget;

    [Header("値段")]
    [Tooltip("リロールの値段＝売却額の満額×この倍率")]
    [SerializeField, Min(1)] private int priceMultiplier = 4;

    [Header("ボタンの色")]
    [SerializeField] private Color frameEnabledColor = Color.white;
    [SerializeField] private Color frameDisabledColor = new Color(0.45f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color priceColor = new Color(1f, 0.85f, 0.3f, 1f);
    [Tooltip("ゴールドが足りない時の値段の色")]
    [SerializeField] private Color priceNotEnoughColor = new Color(1f, 0.3f, 0.3f, 1f);
    [Tooltip("値段の表示（{0}=値段）")]
    [SerializeField] private string priceFormat = "{0:N0} G";

    [Header("文字（日本語）")]
    [SerializeField] private string buttonLabelJa = "リロール";
    [Tooltip("確認画面の文（{0}=値段）。売却の確認画面「このジェムを{0}Gで売却しますか？」と同じ形")]
    [SerializeField] private string confirmPriceFormatJa = "このジェムを{0}Gでリロールしますか？";
    [SerializeField] private string notEnoughJa = "ゴールドが足りません";
    [SerializeField] private string resultTitleJa = "リロール結果";
    [SerializeField] private string beforeLabelJa = "元のスキル";
    [SerializeField] private string afterLabelJa = "新しいスキル";
    [SerializeField] private string okJa = "OK";

    [Header("文字（英語）")]
    [SerializeField] private string buttonLabelEn = "REROLL";
    [SerializeField] private string confirmPriceFormatEn = "Reroll this gem for {0}G?";
    [SerializeField] private string notEnoughEn = "Not enough gold";
    [SerializeField] private string resultTitleEn = "REROLL RESULT";
    [SerializeField] private string beforeLabelEn = "BEFORE";
    [SerializeField] private string afterLabelEn = "AFTER";
    [SerializeField] private string okEn = "OK";

    [Header("演出：金貨（秒・実時間）")]
    [SerializeField] private int coinCount = 8;
    [SerializeField] private float coinInterval = 0.04f;
    [SerializeField] private float coinFlySeconds = 0.45f;
    [Tooltip("金貨が描く山なりの高さ(px)")]
    [SerializeField] private float coinArcHeight = 160f;
    [SerializeField] private float coinStartScale = 1f;
    [SerializeField] private float coinEndScale = 0.5f;

    [Header("演出：引き直し中（秒・実時間）")]
    [SerializeField] private float spinSeconds = 0.8f;
    [Tooltip("行の中身が切り替わる間隔")]
    [SerializeField] private float spinInterval = 0.06f;
    [Tooltip("カードの震えの大きさ(px)")]
    [SerializeField] private float shakeAmount = 4f;
    [Tooltip("引き直し中の行の色（行の背景に掛ける）")]
    [SerializeField] private Color spinRowTint = new Color(0.75f, 0.95f, 1f, 1f);
    [Tooltip("スキルの数が減る時、消える行が灰色になって消えるまでの時間")]
    [SerializeField] private float removeSeconds = 0.3f;
    [SerializeField] private Color removeRowColor = new Color(0.35f, 0.35f, 0.4f, 1f);

    [Header("演出：結果（秒・実時間）")]
    [Tooltip("1行ずつ止まる間隔")]
    [SerializeField] private float revealInterval = 0.2f;
    [SerializeField] private float revealPopScale = 1.12f;
    [SerializeField] private float revealPopSeconds = 0.15f;
    [SerializeField] private float flashSeconds = 0.35f;
    [SerializeField] private Color landFlashColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("スキルの数が増えた時、新しい行の光の色（ライム）")]
    [SerializeField] private Color addFlashColor = new Color(0.72f, 1f, 0.24f, 0.95f);
    [Tooltip("最後にカード全体が光る色")]
    [SerializeField] private Color finishFlashColor = new Color(0.72f, 1f, 0.24f, 0.6f);
    [Tooltip("止まり終わってから結果画面を出すまで")]
    [SerializeField] private float resultDelay = 0.35f;

    [Header("効果音")]
    [SerializeField] private AudioClip buttonSE;
    [SerializeField] private AudioClip cancelSE;
    [Tooltip("ゴールドが足りない状態でリロールボタンを押した時（空ならbuttonSE）。確認画面は出るが、YESは押せない")]
    [SerializeField] private AudioClip notEnoughSE;
    [SerializeField] private AudioClip paySE;
    [SerializeField, Range(0f, 1f)] private float paySEVolume = 1f;
    [SerializeField] private AudioClip spinTickSE;
    [SerializeField, Range(0f, 1f)] private float spinTickSEVolume = 0.5f;
    [Tooltip("何回切り替わるごとにカチッと鳴らすか")]
    [SerializeField, Min(1)] private int spinTickEvery = 2;
    [SerializeField] private AudioClip rowLandSE;
    [SerializeField, Range(0f, 1f)] private float rowLandSEVolume = 0.8f;
    [Tooltip("スキルの数が増えた時（新しい行）")]
    [SerializeField] private AudioClip rowAddSE;
    [SerializeField, Range(0f, 1f)] private float rowAddSEVolume = 1f;
    [Tooltip("スキルの数が減った時（行が消える）")]
    [SerializeField] private AudioClip rowRemoveSE;
    [SerializeField, Range(0f, 1f)] private float rowRemoveSEVolume = 0.8f;
    [SerializeField] private AudioClip finishSE;
    [SerializeField, Range(0f, 1f)] private float finishSEVolume = 1f;

    private static readonly string[] RowNames = { "SkillRow_Base", "SkillRow_Bonus1", "SkillRow_Bonus2" };

    private AudioSource audioSource;
    private bool busy;
    private int pendingIdx = -1;
    private int pendingPrice;
    private List<SkillDefinition> skillPool;

    /// <summary>リロールの確認・演出・結果画面の途中か</summary>
    public bool IsBusy => busy;

    private static bool IsEnglish => LocalizationManager.CurrentLanguageStatic == GameLanguage.English;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; }
        if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollButtonClicked);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);
        if (resultOkButton != null) resultOkButton.onClick.AddListener(OnResultOk);
        GameNumberFont.Apply(priceText);
        GameNumberFont.Apply(costText);
        GameNumberFont.Apply(goldBeforeText);
        GameNumberFont.Apply(goldAfterText);
        if (confirmOverlay != null) confirmOverlay.SetActive(false);
        if (resultOverlay != null) resultOverlay.SetActive(false);
        if (fxLayer != null) fxLayer.gameObject.SetActive(false);
        if (coinTemplate != null) coinTemplate.gameObject.SetActive(false);
        if (flashTemplate != null) flashTemplate.gameObject.SetActive(false);
    }

    // =====================================================================
    // ボタン
    // =====================================================================

    private void LateUpdate()
    {
        UpdateButton();
        if (resultOverlay != null && resultOverlay.activeInHierarchy) LayoutTitleIcons();
    }

    /// <summary>「リロール結果」の左右のアイコンを、文字の長さに合わせて文字のすぐ外側へ置く（見出しと同じ高さ）</summary>
    private void LayoutTitleIcons()
    {
        if (resultTitleText == null) return;
        float half = resultTitleText.GetPreferredValues(resultTitleText.text).x * 0.5f;
        var trt = resultTitleText.rectTransform;
        Vector2 c = trt.anchoredPosition;
        if (resultTitleIconLeft != null) resultTitleIconLeft.anchoredPosition = new Vector2(c.x - half - resultTitleIconGap, c.y);
        if (resultTitleIconRight != null) resultTitleIconRight.anchoredPosition = new Vector2(c.x + half + resultTitleIconGap, c.y);
    }

    private bool TryGetSelected(out int idx, out GemInstance gem)
    {
        idx = gemUI != null ? gemUI.SelectedGemIndex : -1;
        gem = null;
        var data = ProgressManager.Instance?.Data;
        if (data == null || idx < 0 || idx >= data.gemInventory.Count) return false;
        gem = data.gemInventory[idx];
        return gem != null;
    }

    private int PriceOf(GemInstance gem) => GemManager.Instance != null ? GemManager.Instance.GetRerollPrice(gem, priceMultiplier) : 0;
    private static int CurrentGold => GoldManager.Instance != null ? GoldManager.Instance.PersistentGold : 0;

    private void UpdateButton()
    {
        if (rerollButton == null) return;
        bool has = TryGetSelected(out _, out var gem);
        // 初期ジェム（GemDefinition.isStarterGem）はリロールできない（2026/11/10）。選んでいない時と同じく押せない・値段も出さない
        if (has && GemManager.Instance != null && GemManager.Instance.IsStarterGem(gem)) has = false;
        // ★確認画面（YES/NO）を出している間は押せる状態のままにする。押せなくすると、ホバー拡大（売却ボタンと同じくクリック後も維持）が解除されるため。
        //   確認画面の後ろは画面全体を覆うので、この間にボタンが押されることはない
        bool confirmShowing = confirmOverlay != null && confirmOverlay.activeInHierarchy;
        bool interactable = has && (!busy || confirmShowing);
        if (rerollButton.interactable != interactable)
        {
            rerollButton.interactable = interactable;
            // ★色は切り替わった時だけ塗る（毎フレーム塗るとホバーの点滅を上書きしてしまう）
            if (buttonFrame != null) buttonFrame.color = interactable ? frameEnabledColor : frameDisabledColor;
            if (buttonIcon != null) buttonIcon.color = interactable ? Color.white : frameDisabledColor;
        }
        if (buttonLabel != null)
        {
            string label = IsEnglish ? buttonLabelEn : buttonLabelJa;
            if (buttonLabel.text != label) buttonLabel.text = label;
        }
        if (priceText != null)
        {
            priceText.gameObject.SetActive(has);
            if (has)
            {
                int price = PriceOf(gem);
                string s = string.Format(priceFormat, price);
                if (priceText.text != s) priceText.text = s;
                priceText.color = CurrentGold >= price ? priceColor : priceNotEnoughColor;
            }
        }
    }

    private void OnRerollButtonClicked()
    {
        if (busy || !TryGetSelected(out int idx, out var gem)) return;
        if (GemManager.Instance != null && GemManager.Instance.IsStarterGem(gem)) return; // 初期ジェムはリロールできない
        int price = PriceOf(gem);
        PlaySE(CurrentGold >= price ? buttonSE : (notEnoughSE != null ? notEnoughSE : buttonSE));
        // ★売却・無限化ボタンと同じく、確認画面のYES/NOが終わるまでホバー拡大を維持する（閉じた時にHideConfirmでForceReset）
        var hover = rerollButton != null ? rerollButton.GetComponent<Game.UI.ButtonHoverEffect>() : null;
        if (hover != null) hover.LockEnlargedThroughTransition();
        ShowConfirm(idx, gem, price);
    }

    // =====================================================================
    // 確認画面
    // =====================================================================

    private void ShowConfirm(int idx, GemInstance gem, int price)
    {
        pendingIdx = idx;
        pendingPrice = price;
        busy = true;
        bool en = IsEnglish;
        int gold = CurrentGold;
        bool enough = gold >= price;

        if (confirmText != null) confirmText.text = string.Format(en ? confirmPriceFormatEn : confirmPriceFormatJa, price);
        if (costText != null) { costText.text = string.Format(priceFormat, price); costText.color = enough ? priceColor : priceNotEnoughColor; }
        if (goldBeforeText != null) goldBeforeText.text = gold.ToString("N0");
        if (goldAfterText != null)
        {
            goldAfterText.text = enough ? (gold - price).ToString("N0") : "-";
            goldAfterText.color = enough ? Color.white : priceNotEnoughColor;
        }
        if (notEnoughText != null)
        {
            notEnoughText.gameObject.SetActive(!enough);
            notEnoughText.text = en ? notEnoughEn : notEnoughJa;
        }
        if (confirmYesButton != null)
        {
            confirmYesButton.interactable = enough;
            var g = confirmYesButton.targetGraphic != null ? confirmYesButton.targetGraphic : confirmYesButton.GetComponentInChildren<Image>();
            if (g != null) g.color = enough ? Color.white : frameDisabledColor;
            foreach (var img in confirmYesButton.GetComponentsInChildren<Image>(true)) img.color = enough ? Color.white : frameDisabledColor;
        }
        if (confirmOverlay != null)
        {
            confirmOverlay.transform.SetAsLastSibling();
            confirmOverlay.SetActive(true);
            SetModal(confirmOverlay.transform); // 後ろのボタンのホバー演出を止める
        }
    }

    /// <summary>モーダル（確認画面・演出・結果画面）の間、後ろのボタンのホバー演出を止める。nullで解除（自分が設定した時だけ）</summary>
    private Transform modalSetByMe;
    private void SetModal(Transform root)
    {
        if (root == null)
        {
            if (modalSetByMe != null && Game.UI.ButtonHoverEffect.ModalPanelRoot == modalSetByMe) Game.UI.ButtonHoverEffect.ModalPanelRoot = null;
            modalSetByMe = null;
            return;
        }
        Game.UI.ButtonHoverEffect.ModalPanelRoot = root;
        modalSetByMe = root;
    }

    private void OnDisable() => SetModal(null);

    private void HideConfirm()
    {
        if (confirmOverlay != null) confirmOverlay.SetActive(false);
        ResetHover(confirmYesButton);
        ResetHover(confirmNoButton);
        ResetHover(rerollButton);
    }

    private static void ResetHover(Button b)
    {
        if (b == null) return;
        var h = b.GetComponent<Game.UI.ButtonHoverEffect>();
        if (h != null) h.ForceReset();
    }

    private void OnConfirmNo()
    {
        PlaySE(cancelSE);
        HideConfirm();
        SetModal(null);
        pendingIdx = -1;
        busy = false;
    }

    private void OnConfirmYes()
    {
        if (pendingIdx < 0) return;
        if (GoldManager.Instance == null || GemManager.Instance == null) { OnConfirmNo(); return; }
        var data = ProgressManager.Instance?.Data;
        if (data == null || pendingIdx >= data.gemInventory.Count) { OnConfirmNo(); return; }
        if (GoldManager.Instance.PersistentGold < pendingPrice) return; // 念のため（YESは押せない状態）

        var gem = data.gemInventory[pendingIdx];
        var rolled = GemManager.Instance.RollReroll(gem);
        if (rolled == null) { OnConfirmNo(); return; }

        HideConfirm();
        GoldManager.Instance.SpendPersistentGold(pendingPrice);
        var before = new GemInstance { baseSkillName = gem.baseSkillName, bonusSkill1Name = gem.bonusSkill1Name, bonusSkill2Name = gem.bonusSkill2Name };
        Debug.Log($"[GemReroll] idx={pendingIdx} {gem.gemDefinitionName} {pendingPrice}G：{SkillsText(before)} → {SkillsText(rolled)}");
        StartCoroutine(RerollRoutine(pendingIdx, before, rolled));
        pendingIdx = -1;
    }

    private static string SkillsText(GemInstance g) => $"[{g.baseSkillName}, {g.bonusSkill1Name}, {g.bonusSkill2Name}]";

    // =====================================================================
    // 演出
    // =====================================================================

    private IEnumerator RerollRoutine(int idx, GemInstance before, GemInstance rolled)
    {
        busy = true;
        if (fxLayer != null) { fxLayer.gameObject.SetActive(true); fxLayer.SetAsLastSibling(); SetModal(fxLayer); }

        var card = CardAt(idx);
        // ① 金貨：ゴールドの所持数からカードへ
        PlaySE(paySE, paySEVolume);
        if (card != null) yield return CoinRoutine((RectTransform)card.transform);

        // ② 引き直し中：行が高速で切り替わり、カードが震える
        int oldCount = CountSkills(before), newCount = CountSkills(rolled);
        if (card != null) yield return SpinRoutine(card, oldCount, newCount);

        // ③ データを書き換えてカードを作り直す（装備中なら左の一覧も更新される）
        GemManager.Instance.ApplyReroll(idx, rolled);
        if (gemUI != null) gemUI.RefreshAfterGemChanged();
        yield return null; // 作り直したカードの配置が決まるのを待つ

        // ④ 1行ずつ止まる
        card = CardAt(idx);
        if (card != null) yield return RevealRoutine(card, oldCount);

        yield return Wait(resultDelay);
        if (fxLayer != null) fxLayer.gameObject.SetActive(false);
        ShowResult(before, rolled);
    }

    private GameObject CardAt(int idx)
    {
        if (gemUI == null) return null;
        var items = gemUI.GemItemObjects;
        return (items != null && idx >= 0 && idx < items.Count) ? items[idx] : null;
    }

    private static int CountSkills(GemInstance g)
        => (string.IsNullOrEmpty(g.baseSkillName) ? 0 : 1) + (string.IsNullOrEmpty(g.bonusSkill1Name) ? 0 : 1) + (string.IsNullOrEmpty(g.bonusSkill2Name) ? 0 : 1);

    private static List<Transform> ActiveRows(GameObject card)
    {
        var list = new List<Transform>();
        var cont = card != null ? card.transform.Find("TextContainer/SkillIconsContainer") : null;
        if (cont == null) return list;
        foreach (var n in RowNames)
        {
            var r = cont.Find(n);
            if (r != null && r.gameObject.activeSelf) list.Add(r);
        }
        return list;
    }

    private static IEnumerator Wait(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
    }

    private IEnumerator CoinRoutine(RectTransform cardRt)
    {
        if (coinTemplate == null || fxLayer == null || goldTarget == null || coinCount <= 0) yield break;
        Vector3 from = goldTarget.position;
        Vector3 to = cardRt.position;
        float scale = fxLayer.lossyScale.y > 0f ? fxLayer.lossyScale.y : 1f;
        var coins = new List<(RectTransform rt, float start)>();
        for (int i = 0; i < coinCount; i++)
        {
            var c = Instantiate(coinTemplate, fxLayer);
            c.gameObject.SetActive(true);
            c.rectTransform.position = from;
            c.rectTransform.localScale = Vector3.one * coinStartScale;
            coins.Add((c.rectTransform, i * coinInterval));
        }
        float total = (coinCount - 1) * coinInterval + coinFlySeconds, t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            foreach (var (rt, start) in coins)
            {
                if (rt == null) continue;
                float k = Mathf.Clamp01((t - start) / Mathf.Max(0.01f, coinFlySeconds));
                float e = 1f - (1f - k) * (1f - k);
                Vector3 p = Vector3.Lerp(from, to, e) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * coinArcHeight * scale);
                rt.position = p;
                rt.localScale = Vector3.one * Mathf.Lerp(coinStartScale, coinEndScale, k);
                rt.gameObject.SetActive(k > 0f && k < 1f);
            }
            yield return null;
        }
        foreach (var (rt, _) in coins) if (rt != null) Destroy(rt.gameObject);
    }

    private IEnumerator SpinRoutine(GameObject card, int oldCount, int newCount)
    {
        var rows = ActiveRows(card);
        var cardRt = (RectTransform)card.transform;
        Vector2 basePos = cardRt.anchoredPosition;
        var pool = GetSkillPool();

        // 行の背景の元の色（引き直し中だけ色を掛ける）
        var bgs = new List<(Image img, Color col)>();
        foreach (var r in rows) { var img = r.GetComponent<Image>(); if (img != null) bgs.Add((img, img.color)); }
        foreach (var (img, _) in bgs) img.color = spinRowTint;

        float t = 0f, next = 0f;
        int ticks = 0;
        while (t < spinSeconds)
        {
            t += Time.unscaledDeltaTime;
            if (t >= next && pool.Count > 0)
            {
                next += Mathf.Max(0.02f, spinInterval);
                foreach (var r in rows) ShowSkillOnRow(r, pool[Random.Range(0, pool.Count)]);
                if (ticks % Mathf.Max(1, spinTickEvery) == 0) PlaySE(spinTickSE, spinTickSEVolume);
                ticks++;
            }
            cardRt.anchoredPosition = basePos + Random.insideUnitCircle * shakeAmount;
            yield return null;
        }
        cardRt.anchoredPosition = basePos;
        foreach (var (img, col) in bgs) if (img != null) img.color = col;

        // スキルの数が減る時：減る分の行（下から）を灰色にして消す
        int remove = Mathf.Clamp(oldCount - newCount, 0, rows.Count);
        if (remove > 0)
        {
            PlaySE(rowRemoveSE, rowRemoveSEVolume);
            var fading = new List<CanvasGroup>();
            for (int i = rows.Count - remove; i < rows.Count; i++)
            {
                var img = rows[i].GetComponent<Image>();
                if (img != null) img.color = removeRowColor;
                fading.Add(GetCanvasGroup(rows[i]));
            }
            float f = 0f;
            while (f < removeSeconds)
            {
                f += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(f / Mathf.Max(0.01f, removeSeconds));
                foreach (var cg in fading) if (cg != null) cg.alpha = a;
                yield return null;
            }
        }
    }

    private static void ShowSkillOnRow(Transform row, SkillDefinition skill)
    {
        if (row == null || skill == null) return;
        var icon = row.Find("IconContainer/IconImage")?.GetComponent<Image>();
        if (icon != null)
        {
            icon.sprite = skill.icon;
            icon.color = Color.white;
            if (skill.iconDisplaySize != Vector2.zero) icon.rectTransform.sizeDelta = skill.iconDisplaySize;
        }
        var nm = row.Find("SkillName")?.GetComponent<TextMeshProUGUI>();
        if (nm != null) nm.text = skill.GetLocalizedName();
    }

    private IEnumerator RevealRoutine(GameObject card, int oldCount)
    {
        var rows = ActiveRows(card);
        var groups = new List<CanvasGroup>();
        foreach (var r in rows) { var cg = GetCanvasGroup(r); cg.alpha = 0f; groups.Add(cg); }

        for (int i = 0; i < rows.Count; i++)
        {
            yield return Wait(revealInterval);
            bool isNew = i >= oldCount;
            groups[i].alpha = 1f;
            PlaySE(isNew ? rowAddSE : rowLandSE, isNew ? rowAddSEVolume : rowLandSEVolume);
            Flash(rows[i], isNew ? addFlashColor : landFlashColor);
            StartCoroutine(PopRoutine(rows[i]));
        }

        yield return Wait(revealPopSeconds);
        PlaySE(finishSE, finishSEVolume);
        var bg = card.transform.Find("ItemBg");
        Flash(bg != null ? bg : card.transform, finishFlashColor);
    }

    private IEnumerator PopRoutine(Transform t)
    {
        if (t == null) yield break;
        float d = Mathf.Max(0.01f, revealPopSeconds), e = 0f;
        while (e < d && t != null)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / d);
            t.localScale = Vector3.one * Mathf.Lerp(revealPopScale, 1f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one;
    }

    private void Flash(Transform parent, Color color)
    {
        if (flashTemplate == null || parent == null) return;
        var f = Instantiate(flashTemplate, parent);
        f.gameObject.SetActive(true);
        var rt = f.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        f.transform.SetAsLastSibling();
        f.raycastTarget = false;
        StartCoroutine(FlashRoutine(f, color));
    }

    private IEnumerator FlashRoutine(Image f, Color color)
    {
        float d = Mathf.Max(0.01f, flashSeconds), e = 0f;
        while (e < d && f != null)
        {
            e += Time.unscaledDeltaTime;
            var c = color; c.a = color.a * (1f - Mathf.Clamp01(e / d));
            f.color = c;
            yield return null;
        }
        if (f != null) Destroy(f.gameObject);
    }

    private static CanvasGroup GetCanvasGroup(Transform t)
    {
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    private List<SkillDefinition> GetSkillPool()
    {
        if (skillPool != null) return skillPool;
        skillPool = new List<SkillDefinition>();
        foreach (var s in Resources.LoadAll<SkillDefinition>("GameData/Skills"))
            if (s != null && s.icon != null) skillPool.Add(s);
        return skillPool;
    }

    // =====================================================================
    // 結果画面（前と後を並べる）
    // =====================================================================

    private void ShowResult(GemInstance before, GemInstance after)
    {
        bool en = IsEnglish;
        if (resultTitleText != null) resultTitleText.text = en ? resultTitleEn : resultTitleJa;
        if (resultBeforeLabel != null) resultBeforeLabel.text = en ? beforeLabelEn : beforeLabelJa;
        if (resultAfterLabel != null) resultAfterLabel.text = en ? afterLabelEn : afterLabelJa;
        if (resultOkLabel != null) resultOkLabel.text = en ? okEn : okJa;
        FillRows(beforeRows, before);
        FillRows(afterRows, after);
        if (resultOverlay != null)
        {
            resultOverlay.transform.SetAsLastSibling();
            resultOverlay.SetActive(true);
            SetModal(resultOverlay.transform);
        }
        else { SetModal(null); busy = false; }
    }

    private void FillRows(ResultRow[] rows, GemInstance g)
    {
        if (rows == null) return;
        var names = new List<string>();
        if (!string.IsNullOrEmpty(g.baseSkillName)) names.Add(g.baseSkillName);
        if (!string.IsNullOrEmpty(g.bonusSkill1Name)) names.Add(g.bonusSkill1Name);
        if (!string.IsNullOrEmpty(g.bonusSkill2Name)) names.Add(g.bonusSkill2Name);
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null || row.root == null) continue;
            var skill = i < names.Count ? Resources.Load<SkillDefinition>($"GameData/Skills/{names[i]}") : null;
            row.root.SetActive(skill != null);
            if (skill != null) FillRow(row, skill);
        }
    }

    /// <summary>1行に1つのスキルを出す（ジェム画面のカードの行と同じ：カテゴリ色の背景・左端の縦線・アイコンの大きさ・黒い文字）</summary>
    private void FillRow(ResultRow row, SkillDefinition skill)
    {
        if (row.icon != null)
        {
            row.icon.sprite = skill.icon;
            row.icon.color = skill.icon != null ? Color.white : Color.clear;
            Vector2 size = skill.iconDisplaySize != Vector2.zero ? skill.iconDisplaySize : new Vector2(40f, 40f);
            row.icon.rectTransform.sizeDelta = size * rowIconScale;
            row.icon.rectTransform.anchoredPosition = skill.iconDisplayOffset * rowIconScale;
        }
        if (row.nameText != null) row.nameText.text = skill.GetLocalizedName();
        Color cat = skill.category switch
        {
            SkillCategory.CategoryA => categoryAColor,
            SkillCategory.CategoryB => categoryBColor,
            SkillCategory.CategoryC => categoryCColor,
            _ => new Color(0.15f, 0.15f, 0.15f, 0.5f),
        };
        if (row.background != null) row.background.color = cat;
        if (row.accent != null) row.accent.color = Vivid(cat);
    }

    // GemScreenFX.Vivid と同じ計算（行の背景色から、左端の縦線の色を作る）
    private Color Vivid(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        Color vivid = Color.HSVToRGB(h, Mathf.Clamp01(s * accentSaturationScale), Mathf.Max(v, accentBrightness));
        Color shown = new Color(c.r * rowDisplayBrightness, c.g * rowDisplayBrightness, c.b * rowDisplayBrightness, 1f);
        float rowL = Luma(shown), vividL = Luma(vivid);
        float t = vividL > rowL + 0.0001f ? Mathf.Clamp01(accentLumaDiff / (vividL - rowL)) : 1f;
        Color o = Color.Lerp(shown, vivid, t);
        o.a = 1f;
        return o;
    }

    private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    private void OnResultOk()
    {
        PlaySE(buttonSE);
        if (resultOverlay != null) resultOverlay.SetActive(false);
        ResetHover(resultOkButton);
        SetModal(null);
        busy = false;
    }

    // =====================================================================

    private void PlaySE(AudioClip clip, float volume = 1f)
    {
        if (audioSource == null || clip == null) return;
        float vol = volume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        audioSource.PlayOneShot(clip, vol);
    }

#if UNITY_EDITOR
    [ContextMenu("確認画面を表示して確認（Play前）")]
    private void PreviewConfirm()
    {
        if (confirmOverlay == null) return;
        if (confirmText != null) confirmText.text = string.Format(confirmPriceFormatJa, 400);
        if (costText != null) { costText.text = string.Format(priceFormat, 760); costText.color = priceColor; }
        if (goldBeforeText != null) goldBeforeText.text = "12,000";
        if (goldAfterText != null) { goldAfterText.text = "11,240"; goldAfterText.color = Color.white; }
        if (notEnoughText != null) { notEnoughText.text = notEnoughJa; notEnoughText.gameObject.SetActive(false); }
        confirmOverlay.SetActive(true);
        UnityEditor.EditorUtility.SetDirty(confirmOverlay);
    }

    [ContextMenu("結果画面を表示して確認（Play前）")]
    private void PreviewResult()
    {
        if (resultOverlay == null) return;
        if (resultTitleText != null) resultTitleText.text = resultTitleJa;
        if (resultBeforeLabel != null) resultBeforeLabel.text = beforeLabelJa;
        if (resultAfterLabel != null) resultAfterLabel.text = afterLabelJa;
        if (resultOkLabel != null) resultOkLabel.text = okJa;
        var skills = Resources.LoadAll<SkillDefinition>("GameData/Skills");
        for (int i = 0; i < 3; i++)
        {
            foreach (var rows in new[] { beforeRows, afterRows })
            {
                if (rows == null || i >= rows.Length || rows[i] == null || rows[i].root == null) continue;
                bool show = !(rows == beforeRows && i == 2);
                rows[i].root.SetActive(show);
                var s = skills.Length > 0 ? skills[(i * 3 + (rows == afterRows ? 5 : 0)) % skills.Length] : null;
                if (s != null) FillRow(rows[i], s);
            }
        }
        resultOverlay.SetActive(true);
        LayoutTitleIcons();
        UnityEditor.EditorUtility.SetDirty(resultOverlay);
    }

    [ContextMenu("確認画面・結果画面を隠す")]
    private void PreviewHide()
    {
        if (confirmOverlay != null) { confirmOverlay.SetActive(false); UnityEditor.EditorUtility.SetDirty(confirmOverlay); }
        if (resultOverlay != null) { resultOverlay.SetActive(false); UnityEditor.EditorUtility.SetDirty(resultOverlay); }
    }
#endif
}
