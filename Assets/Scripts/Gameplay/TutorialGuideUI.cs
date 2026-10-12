using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// チュートリアルの「何をすればよいか」を分かりやすくするガイド表示一式。
/// ①ミッション欄 ②チェックリスト ③成功演出 ④ステップ完了のお知らせ ⑤お手本の指 ⑥スポットライト
/// ⑦弾の目印 ⑧ジャスト反射のタイミングリング ⑨カードの自動縮小 ⑩キーワードの色分け ⑪追加のヒント。
/// UI一式はメニュー「Tools/チュートリアル/ガイドUIを作成」でPlay前に生成・参照設定される（Inspectorで全項目調整可能）。
/// 進行はTutorialFlowControllerから呼ばれる。時間はすべて実時間（スローモーション・一時停止の影響を受けない）
/// </summary>
public class TutorialGuideUI : MonoBehaviour
{
    public enum FingerMotion { None, Swipe, DoubleSwipe, Circle, LongPressSwipe, Tap, Hold }
    public enum GuideAnchor { BulletPath, FieldCenter, Soul, UITarget }

    [System.Serializable]
    public class GuideEntry
    {
        [Tooltip("Inspectorで見分けるためのメモ（動作には無関係）")]
        public string memo;
        [Tooltip("対象のステップ番号（0始まり。1/10＝0）")]
        public int stepIndex;
        [Tooltip("対象のヒント番号（0始まり）。-1＝そのステップの全ヒント")]
        public int hintIndex = -1;

        [Header("⑤ お手本の指")]
        public FingerMotion finger = FingerMotion.None;
        public GuideAnchor fingerAnchor = GuideAnchor.FieldCenter;
        [Tooltip("fingerAnchor=UITargetの時に指で示すUI")]
        public RectTransform fingerUITarget;
        [Tooltip("指の位置のずらし量（ワールド座標。UITargetの時はCanvas座標）")]
        public Vector2 fingerOffset;
        [Tooltip("なぞる長さ／円の半径（ワールド座標）")]
        public float fingerSize = 1.6f;

        [Header("⑥ スポットライト")]
        public RectTransform spotlightTarget;
        public float spotlightPadding = 14f;

        [Header("⑦⑧ 弾の目印・タイミングリング")]
        public bool bulletGuide;
        public bool timingRing;

        [Header("⑪ 追加のヒント（LocalizationTableのキー。未登録ならfallbackを表示）")]
        public string extraHintKey;
        [TextArea(1, 3)] public string extraHintFallback;
        [Tooltip("2種類目の失敗用（例：1/10①で「反射したが敵に当たらなかった」）。空なら1種類目と同じ文言")]
        public string extraHintKey2;
        [TextArea(1, 3)] public string extraHintFallback2;
    }

    [System.Serializable]
    public class KeywordColor
    {
        public string keyword;
        public Color color = Color.yellow;
    }

    [System.Serializable]
    public class MissionRow
    {
        public RectTransform root;
        public Image box;
        public Image check;
        public TextMeshProUGUI text;
    }

    // ================= 参照（メニューで自動設定） =================
    [Header("参照（Tools/チュートリアル/ガイドUIを作成 で自動設定）")]
    [SerializeField] private RectTransform frontRoot;
    [SerializeField] private CanvasGroup frontGroup;
    [SerializeField] private RectTransform backRoot;
    [Tooltip("TutorialCanvas > Panel の暗幕Image。スポットライト中は代わりに⑥の暗幕を使うため一時的に透明にする")]
    [SerializeField] private Image panelDimImage;

    [Header("① ② ミッション欄・チェックリスト")]
    [SerializeField] private RectTransform missionPanel;
    [SerializeField] private TextMeshProUGUI missionHeaderText;
    [Tooltip("見出し「ミッション」を載せるタブ（枠の上辺から飛び出す）。見出しの文字幅に合わせて横幅を自動調整する")]
    [SerializeField] private RectTransform missionTab;
    [Tooltip("タブの横幅＝見出しの文字幅＋左右この余白")]
    [SerializeField] private float missionTabTextPadding = 26f;
    [Tooltip("ミッション欄の枠（面取りの二重線）。ゆっくり明滅させる")]
    [SerializeField] private Image missionFrameImage;
    [Tooltip("ミッション欄のタブの枠（同じく明滅）")]
    [SerializeField] private Image missionTabFrameImage;
    [SerializeField, Range(0f, 1f)] private float missionFramePulseMin = 0.7f;
    [SerializeField] private float missionFramePulseSpeed = 1.6f;
    [SerializeField] private TextMeshProUGUI missionProgressText;
    [SerializeField] private MissionRow[] missionRows;
    [SerializeField] private TextMeshProUGUI extraHintText;
    [SerializeField] private TextMeshProUGUI footerText;
    [SerializeField] private float headerHeight = 58f;
    [SerializeField] private float rowHeight = 48f;
    [SerializeField] private float extraHintHeight = 66f;
    [SerializeField] private float footerHeight = 50f;
    [SerializeField] private float panelBottomPadding = 14f;
    [SerializeField] private Color rowCurrentColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color rowDoneColor = new Color(0.45f, 0.95f, 0.6f, 1f);
    [SerializeField] private Color rowPendingColor = new Color(0.62f, 0.64f, 0.72f, 1f);
    [Tooltip("ミッション欄の横幅（カードと一時停止ボタンに重ならない範囲で調整）")]
    [SerializeField] private float missionPanelWidth = 760f;
    [Tooltip("ミッション欄の横位置（画面中央からのずらし量）。成功演出も同じ横位置に揃える")]
    [SerializeField] private float missionPanelPosX = 370f;
    [Tooltip("ONの場合、実行時に「カードの右端」〜「右側の目安UI（一時停止ボタン）の左端」の空きいっぱいに幅と位置を合わせる（画面の縦横比に関係なく最大幅になる）。" +
        "OFFの場合はmissionPanelWidth/PosXの固定値")]
    [SerializeField] private bool fitMissionPanelToFreeSpace = true;
    [Tooltip("右側の目安UI（未設定なら一時停止ボタンの周りのリングTimerGaugeBackground→無ければPauseButtonを名前で探す）")]
    [SerializeField] private RectTransform missionRightLimit;
    [Tooltip("カード・右側の目安UIとの間に空ける余白")]
    [SerializeField] private float missionPanelSideMargin = 24f;
    [Tooltip("ミッションの文字の最大サイズ。そのステップで一番長い行が収まる大きさに、全行を同じサイズで揃える")]
    [SerializeField] private float rowFontSizeMax = 24f;
    [Tooltip("ミッションの文字の最小サイズ（これでも収まらない行は2行に折り返す）")]
    [SerializeField] private float rowFontSizeMin = 16f;
    [Tooltip("今のヒントの行が明滅する速さ")]
    [SerializeField] private float currentRowPulseSpeed = 3f;

    [Header("③ 成功の演出")]
    [SerializeField] private RectTransform successPop;
    [SerializeField] private TextMeshProUGUI successText;
    [SerializeField] private float successPopSeconds = 1.1f;

    [Header("④ ステップ完了のお知らせ（次へボタンのアイコンを脈動）")]
    [SerializeField] private RectTransform nextButtonIcon;
    [SerializeField] private float nextPulseScale = 1.18f;
    [SerializeField] private float nextPulseSpeed = 4f;

    [Header("⑤ お手本の指")]
    [SerializeField] private RectTransform finger;
    [SerializeField] private Image fingerImage;
    [SerializeField] private RectTransform touchRing;
    [SerializeField] private Image touchRingImage;
    [SerializeField, Range(0f, 1f)] private float fingerAlpha = 0.85f;
    [SerializeField] private float swipeSeconds = 0.7f;
    [SerializeField] private float circleSeconds = 1.3f;
    [SerializeField] private float holdSeconds = 1.1f;
    [SerializeField] private float loopPauseSeconds = 0.7f;
    [Tooltip("プレイヤーが操作したら指を消し、この秒数操作が無ければ再表示する")]
    [SerializeField] private float idleReshowSeconds = 5f;
    [Tooltip("FieldCenter：ピクセルダンサーの位置からどれだけ上を中心にするか（ワールド座標）")]
    [SerializeField] private float fieldCenterHeight = 2.6f;

    [Header("⑥ スポットライト")]
    [SerializeField] private Image[] spotDims;
    [SerializeField] private RectTransform spotFrame;
    [SerializeField] private Image spotFrameImage;
    [SerializeField] private RectTransform spotArrow;
    [SerializeField, Range(0f, 1f)] private float spotDimAlpha = 0.5f;
    [SerializeField] private float spotArrowDistance = 46f;

    [Header("⑦ 弾の目印")]
    [SerializeField] private RectTransform bulletGuideRoot;
    [SerializeField] private Image[] guideDots;
    [SerializeField] private RectTransform zoneMarker;
    [SerializeField] private Image zoneMarkerImage;
    [SerializeField] private TextMeshProUGUI zoneLabel;
    [Tooltip("的→ピクセルダンサーの距離のうち、どの位置に「ここに線を引く」を置くか（0=的、1=ダンサー）")]
    [SerializeField, Range(0.1f, 0.95f)] private float bulletPathT = 0.6f;
    [Tooltip("「ここに線を引く」の目印の長さ（ワールド座標）")]
    [SerializeField] private float zoneWidthWorld = 2.2f;

    [Header("⑧ ジャスト反射のタイミングリング")]
    [SerializeField] private RectTransform timingRing;
    [SerializeField] private Image timingRingImage;
    [SerializeField] private TextMeshProUGUI nowLabel;
    [SerializeField] private float ringMaxSize = 240f;
    [SerializeField] private float ringMinSize = 64f;
    [Tooltip("弾が目印に届く何秒前からリングを縮め始めるか")]
    [SerializeField] private float ringStartSeconds = 1.3f;
    [Tooltip("弾が目印に届く何秒前から「今！」を出すか")]
    [SerializeField] private float nowLeadSeconds = 0.3f;

    [Header("⑨ カードの自動縮小")]
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private CanvasGroup bodyGroup;
    [SerializeField] private Button collapseToggle;
    [SerializeField] private TextMeshProUGUI collapseToggleLabel;
    [SerializeField] private bool autoCollapseOnStroke = true;
    [Tooltip("ステップ表示からこの秒数経つまでは自動縮小しない（文章を読む時間）")]
    [SerializeField] private float minShownSecondsBeforeCollapse = 1.5f;
    [SerializeField] private float collapsedHeight = 330f;
    [SerializeField] private float collapseSeconds = 0.25f;

    [Header("⑩ キーワードの色分け（説明文に自動で色を付ける。日本語・英語を両方登録）")]
    [SerializeField] private List<KeywordColor> keywords = new List<KeywordColor>();

    [Header("⑤⑥⑦⑧⑪ ステップごとの設定")]
    [SerializeField] private List<GuideEntry> entries = new List<GuideEntry>();

    [Header("⑪ 追加のヒント（今のタスクの行の文言と一時的に入れ替えて表示。欄の高さは変わらない）")]
    [Tooltip("同じヒントでこの回数失敗したら追加のヒントを出す")]
    [SerializeField] private int failureThreshold = 2;
    [Tooltip("追加のヒントを表示しておく秒数。過ぎたら元のタスクの文言に戻る（また失敗すると再表示）")]
    [SerializeField] private float tipShowSeconds = 6f;
    [SerializeField] private Color tipColor = new Color(1f, 0.62f, 0.3f, 1f);
    [Tooltip("追加のヒントが長い時に縮める最小の文字サイズ")]
    [SerializeField] private float tipFontSizeMin = 14f;

    // ================= 文言キー =================
    private const string KeyMission = "tutorial.guide.mission";
    private const string KeyProgress = "tutorial.guide.progress";
    private const string KeySuccess = "tutorial.guide.success";
    private const string KeyStepClear = "tutorial.guide.stepClear";
    private const string KeyDrawHere = "tutorial.guide.drawHere";
    private const string KeyNow = "tutorial.guide.now";
    private const string KeyTipPrefix = "tutorial.guide.tipPrefix";

    // ================= 状態 =================
    private int stepIndex = -1;
    private int hintIndex = -1;
    private int hintCount;
    private bool stepCompleted;
    private bool practiceStep;
    private float stepShownTime;
    private GuideEntry activeEntry;
    private readonly Dictionary<int, int> failures = new Dictionary<int, int>();
    private string[] hintTexts;
    private float uniformRowSize = 24f;
    private int tipRow = -1;
    private float tipUntil;

    private float panelDimNormalAlpha = 0.35f;
    private bool spotlightActive;

    private float successPopTimer = -1f;
    private Vector2 successPopBasePos;

    private bool fingerSuppressed;
    private float lastInputTime;
    private float fingerLoopStart;

    private EnemyBullet trackedBullet;
    private Rigidbody2D trackedBulletRb;
    private Transform bulletSpawn;
    private Vector2 bulletDir = Vector2.down;
    private PixelDancerController dancer;
    private Camera cam;

    private bool collapsed;
    private float collapseK;            // 0=展開 1=縮小
    private Vector2 cardExpandedSize;
    private Vector2 cardExpandedPos;
    private bool cardBaseCaptured;

    private Regex keywordRegex;
    private Dictionary<string, string> keywordHex;

    public bool IsReady => frontRoot != null && missionPanel != null;

    // ================= Unity =================
    private void Awake()
    {
        if (panelDimImage != null) panelDimNormalAlpha = panelDimImage.color.a;
        ApplyMissionPanelRect();
        if (successPop != null) successPopBasePos = successPop.anchoredPosition;
        if (collapseToggle != null) collapseToggle.onClick.AddListener(ToggleCollapse);
        CaptureCardBase();
        HideAll();
    }

    private void CaptureCardBase()
    {
        if (cardBaseCaptured || cardRect == null) return;
        cardExpandedSize = cardRect.sizeDelta;
        cardExpandedPos = cardRect.anchoredPosition;
        cardBaseCaptured = true;
    }

    private void HideAll()
    {
        if (missionPanel != null) missionPanel.gameObject.SetActive(false);
        if (successPop != null) successPop.gameObject.SetActive(false);
        SetFingerVisible(false);
        SetSpotlight(null);
        if (bulletGuideRoot != null) bulletGuideRoot.gameObject.SetActive(false);
        if (timingRing != null) timingRing.gameObject.SetActive(false);
        if (nowLabel != null) nowLabel.gameObject.SetActive(false);
        if (collapseToggle != null) collapseToggle.gameObject.SetActive(false);
    }

    // ================= TutorialFlowControllerから呼ぶ =================

    /// <summary>練習弾の発射位置と向き（⑦⑧の目印の位置計算に使う）</summary>
    public void SetBulletPath(Transform spawn, Vector2 dir)
    {
        bulletSpawn = spawn;
        if (dir.sqrMagnitude > 0.0001f) bulletDir = dir.normalized;
    }

    /// <summary>説明文のキーワードに色を付ける（⑩）</summary>
    public string ColorizeKeywords(string text)
    {
        if (string.IsNullOrEmpty(text) || keywords == null || keywords.Count == 0) return text;
        if (keywordRegex == null) BuildKeywordRegex();
        if (keywordRegex == null) return text;
        return keywordRegex.Replace(text, m =>
            keywordHex.TryGetValue(m.Value, out string hex) ? $"<color=#{hex}>{m.Value}</color>" : m.Value);
    }

    private void BuildKeywordRegex()
    {
        keywordHex = new Dictionary<string, string>();
        var list = new List<string>();
        foreach (var k in keywords)
        {
            if (k == null || string.IsNullOrEmpty(k.keyword) || keywordHex.ContainsKey(k.keyword)) continue;
            keywordHex[k.keyword] = ColorUtility.ToHtmlStringRGB(k.color);
            list.Add(k.keyword);
        }
        if (list.Count == 0) return;
        list.Sort((a, b) => b.Length.CompareTo(a.Length)); // 長い語を優先（「スローモーションゲージ」を「スローモーション」より先に）
        var parts = new List<string>();
        foreach (var w in list) parts.Add(Regex.Escape(w));
        keywordRegex = new Regex(string.Join("|", parts));
    }

    /// <summary>ステップを表示した時</summary>
    public void OnStepShown(int step, string[] localizedHints, bool requiresPractice, bool alreadyCompleted)
    {
        stepIndex = step;
        hintIndex = requiresPractice ? 0 : -1;
        hintCount = localizedHints != null ? localizedHints.Length : 0;
        practiceStep = requiresPractice && hintCount > 0;
        stepCompleted = false;
        stepShownTime = Time.unscaledTime;
        failures.Clear();
        tipRow = -1;
        hintTexts = localizedHints != null ? (string[])localizedHints.Clone() : null;
        trackedBullet = null;
        successPopTimer = -1f;
        if (successPop != null) successPop.gameObject.SetActive(false);
        if (nextButtonIcon != null) nextButtonIcon.localScale = Vector3.one;

        SetCollapsed(false, instant: false);
        if (collapseToggle != null) collapseToggle.gameObject.SetActive(practiceStep);

        if (missionPanel != null) missionPanel.gameObject.SetActive(practiceStep);
        if (practiceStep)
        {
            if (missionHeaderText != null) missionHeaderText.text = Loc(KeyMission, "ミッション");
            FitMissionTab();
            for (int i = 0; missionRows != null && i < missionRows.Length; i++)
            {
                var row = missionRows[i];
                if (row == null || row.root == null) continue;
                bool used = i < hintCount;
                row.root.gameObject.SetActive(used);
                if (used && row.text != null) row.text.text = localizedHints[i];
            }
            if (footerText != null)
            {
                footerText.gameObject.SetActive(alreadyCompleted);
                footerText.text = Loc(KeyStepClear, "クリア！「→」で進めます");
            }
            if (extraHintText != null) extraHintText.gameObject.SetActive(false);
            ApplyMissionPanelRect(); // 画面の縦横比に合わせて空きいっぱいの幅にしてから文字サイズを決める
            ApplyUniformRowFont();
            RefreshRows();
            LayoutMissionPanel();
        }

        RefreshActiveEntry();
    }

    /// <summary>ヒントを1つ達成した時（stepDone=全部達成）</summary>
    public void OnHintCompleted(int doneHintIndex, int nextHintIndex, bool stepDone)
    {
        if (!practiceStep) return;
        hintIndex = stepDone ? hintCount : nextHintIndex;
        RestoreRowTip();
        stepCompleted = stepDone;
        failures.Clear();
        if (extraHintText != null) extraHintText.gameObject.SetActive(false);
        PlaySuccessPop();
        RefreshRows();
        if (stepDone && footerText != null)
        {
            footerText.text = Loc(KeyStepClear, "クリア！「→」で進めます");
            footerText.gameObject.SetActive(true);
        }
        LayoutMissionPanel();
        RefreshActiveEntry();
    }

    /// <summary>失敗を1回報告（⑪）。同じヒントで規定回数失敗したら追加のヒントを出す</summary>
    public void ReportFailure(int step, int hint, int variant = 0)
    {
        if (step != stepIndex || hint != hintIndex || stepCompleted) return;
        int key = step * 100 + hint; // 失敗の種類に関係なく、そのヒントでの失敗回数として数える
        failures.TryGetValue(key, out int n);
        n++;
        failures[key] = n;
        if (n < Mathf.Max(1, failureThreshold)) return;

        GuideEntry e = FindEntry(step, hint, requireExtraHint: true);
        if (e == null) return;
        bool second = variant == 1 && (!string.IsNullOrEmpty(e.extraHintKey2) || !string.IsNullOrEmpty(e.extraHintFallback2));
        string tip = second ? Loc(e.extraHintKey2, e.extraHintFallback2) : Loc(e.extraHintKey, e.extraHintFallback);
        if (string.IsNullOrEmpty(tip)) return;
        ShowRowTip(hint, Loc(KeyTipPrefix, "ヒント：") + tip);
    }

    /// <summary>今のタスクの行を、一時的に追加のヒントの文言に入れ替える（欄の高さを変えずにエネミーと被らないようにするため）</summary>
    private void ShowRowTip(int row, string text)
    {
        if (missionRows == null || row < 0 || row >= missionRows.Length) return;
        var t = missionRows[row]?.text;
        if (t == null) return;
        t.text = text;
        // 長いヒントは行に収まるよう縮める（上限は他の行と揃えたサイズ）
        t.enableAutoSizing = true;
        t.fontSizeMax = uniformRowSize;
        t.fontSizeMin = Mathf.Min(tipFontSizeMin, uniformRowSize);
        tipRow = row;
        tipUntil = Time.unscaledTime + Mathf.Max(0.5f, tipShowSeconds);
    }

    /// <summary>追加のヒントを表示中なら、元のタスクの文言に戻す</summary>
    private void RestoreRowTip()
    {
        if (tipRow < 0) return;
        var t = (missionRows != null && tipRow < missionRows.Length) ? missionRows[tipRow]?.text : null;
        if (t != null)
        {
            if (hintTexts != null && tipRow < hintTexts.Length) t.text = hintTexts[tipRow];
            t.enableAutoSizing = false;
            t.fontSize = uniformRowSize;
        }
        tipRow = -1;
        RefreshRows();
    }

    /// <summary>今飛んでいる練習弾（⑦⑧の対象）</summary>
    public void TrackBullet(EnemyBullet bullet)
    {
        trackedBullet = bullet;
        trackedBulletRb = bullet != null ? bullet.GetComponent<Rigidbody2D>() : null;
    }

    /// <summary>プレイヤーが線を引き始めた時（⑨カードの自動縮小）</summary>
    public void NotifyPlayerStroke()
    {
        if (!autoCollapseOnStroke || !practiceStep || collapsed) return;
        if (Time.unscaledTime - stepShownTime < minShownSecondsBeforeCollapse) return;
        SetCollapsed(true, instant: false);
    }

    /// <summary>10/10の最後の「チュートリアル完了！」</summary>
    public void ShowFinal(string text)
    {
        tipRow = -1;
        stepCompleted = true;
        activeEntry = null;
        SetFingerVisible(false);
        SetSpotlight(null);
        if (missionPanel == null) return;
        missionPanel.gameObject.SetActive(true);
        if (missionHeaderText != null) missionHeaderText.text = text;
        FitMissionTab();
        if (missionProgressText != null) missionProgressText.text = "";
        if (missionRows != null) foreach (var r in missionRows) if (r != null && r.root != null) r.root.gameObject.SetActive(false);
        if (extraHintText != null) extraHintText.gameObject.SetActive(false);
        if (footerText != null) footerText.gameObject.SetActive(false);
        hintCount = 0;
        LayoutMissionPanel();
        PlaySuccessPop();
    }

    public void SetFrontAlpha(float a)
    {
        if (frontGroup != null) frontGroup.alpha = a;
    }

    public void OnTutorialFinished()
    {
        stepIndex = -1;
        activeEntry = null;
        HideAll();
        if (panelDimImage != null) SetPanelDimAlpha(panelDimNormalAlpha);
    }

    // ================= 内部：状態更新 =================

    private void RefreshRows()
    {
        if (missionProgressText != null)
        {
            int shown = Mathf.Min(hintIndex + 1, hintCount);
            string fmt = Loc(KeyProgress, "タスク {0}/{1}");
            missionProgressText.text = SafeFormat(fmt, Mathf.Max(1, shown), hintCount);
        }
        for (int i = 0; missionRows != null && i < missionRows.Length && i < hintCount; i++)
        {
            var row = missionRows[i];
            if (row == null) continue;
            bool done = i < hintIndex;
            bool current = i == hintIndex;
            Color c = done ? rowDoneColor : current ? rowCurrentColor : rowPendingColor;
            if (row.text != null) { row.text.color = c; row.text.fontStyle = current ? FontStyles.Bold : FontStyles.Normal; }
            if (row.box != null) row.box.color = c;
            if (row.check != null) row.check.enabled = done;
        }
    }

    /// <summary>ミッション欄の横幅・横位置を決める（成功演出の横位置も揃える）</summary>
    private void ApplyMissionPanelRect()
    {
        float width = missionPanelWidth, posX = missionPanelPosX;
        if (Application.isPlaying && fitMissionPanelToFreeSpace) TryFitToFreeSpace(ref width, ref posX);
        if (missionPanel != null)
        {
            missionPanel.sizeDelta = new Vector2(width, missionPanel.sizeDelta.y);
            missionPanel.anchoredPosition = new Vector2(posX, missionPanel.anchoredPosition.y);
        }
        if (successPop != null)
            successPop.anchoredPosition = new Vector2(posX, successPop.anchoredPosition.y);
    }

    /// <summary>カードの右端〜右側の目安UIの左端の空きを、このCanvas座標で測って幅・位置を求める</summary>
    private bool TryFitToFreeSpace(ref float width, ref float posX)
    {
        if (frontRoot == null || cardRect == null || missionPanel == null) return false;
        if (missionRightLimit == null)
        {
            // ★PauseButton自体(70x70)より、その周りのネオンリング(TimerGaugeBackground 130x130)の方が大きく、
            //   ボタンの矩形で測るとリングに重なるため、リングを優先する
            var go = GameObject.Find("TimerGaugeBackground");
            if (go == null) go = GameObject.Find("PauseButton");
            if (go != null) missionRightLimit = go.transform as RectTransform;
        }
        if (!TryGetUIRect(cardRect, out Rect card)) return false;
        float left = card.xMax + missionPanelSideMargin;
        float right = frontRoot.rect.xMax - missionPanelSideMargin;
        if (missionRightLimit != null && missionRightLimit.gameObject.activeInHierarchy && TryGetUIRect(missionRightLimit, out Rect lim))
            right = lim.xMin - missionPanelSideMargin;
        if (right - left < 200f) return false;
        width = right - left;
        // missionPanelは上端中央アンカー（frontRootの中央が0）のため、中心のx座標がそのままanchoredPosition.x
        posX = (left + right) * 0.5f;
        return true;
    }

    /// <summary>
    /// そのステップの全ミッション行を同じ文字サイズに揃える。一番長い行（太字で計算）が1行に収まる大きさを求め、
    /// 最大〜最小サイズの範囲で全行に適用する（行ごとの自動縮小だと行ごとに大きさがバラバラになるため）
    /// </summary>
    private void ApplyUniformRowFont()
    {
        if (missionRows == null) return;
        float size = rowFontSizeMax;
        for (int i = 0; i < missionRows.Length && i < hintCount; i++)
        {
            var t = missionRows[i]?.text;
            if (t == null) continue;
            t.enableAutoSizing = false;
            t.fontSize = rowFontSizeMax;
            var style = t.fontStyle;
            t.fontStyle = FontStyles.Bold; // 今のヒントは太字になるため、太字で幅を測る
            float preferred = t.GetPreferredValues(t.text, float.PositiveInfinity, float.PositiveInfinity).x;
            t.fontStyle = style;
            float avail = t.rectTransform.rect.width;
            if (preferred > 0.01f && avail > 0.01f)
                size = Mathf.Min(size, Mathf.Floor(rowFontSizeMax * avail / preferred));
        }
        size = Mathf.Clamp(size, rowFontSizeMin, rowFontSizeMax);
        uniformRowSize = size;
        for (int i = 0; i < missionRows.Length && i < hintCount; i++)
        {
            var t = missionRows[i]?.text;
            if (t == null) continue;
            t.fontSize = size;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // ★Play前でもInspectorの幅・位置の変更をSceneに即反映する（OnValidate中に直接RectTransformを変えると警告が出るため1フレーム遅らせる）
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            ApplyMissionPanelRect();
            if (missionPanel != null) UnityEditor.EditorUtility.SetDirty(missionPanel);
            if (successPop != null) UnityEditor.EditorUtility.SetDirty(successPop);
        };
    }
#endif

    private void LayoutMissionPanel()
    {
        if (missionPanel == null) return;
        float y = headerHeight;
        for (int i = 0; missionRows != null && i < missionRows.Length; i++)
        {
            var row = missionRows[i];
            if (row == null || row.root == null || !row.root.gameObject.activeSelf) continue;
            row.root.anchoredPosition = new Vector2(row.root.anchoredPosition.x, -y);
            y += rowHeight;
        }
        if (extraHintText != null && extraHintText.gameObject.activeSelf)
        {
            extraHintText.rectTransform.anchoredPosition = new Vector2(extraHintText.rectTransform.anchoredPosition.x, -y);
            y += extraHintHeight;
        }
        if (footerText != null && footerText.gameObject.activeSelf)
        {
            footerText.rectTransform.anchoredPosition = new Vector2(footerText.rectTransform.anchoredPosition.x, -y);
            y += footerHeight;
        }
        missionPanel.sizeDelta = new Vector2(missionPanel.sizeDelta.x, y + panelBottomPadding);
    }

    private void RefreshActiveEntry()
    {
        activeEntry = (stepCompleted || stepIndex < 0) ? null : FindEntry(stepIndex, hintIndex, requireExtraHint: false);
        fingerSuppressed = false;
        fingerLoopStart = Time.unscaledTime;
        if (activeEntry == null || activeEntry.finger == FingerMotion.None) SetFingerVisible(false);
        SetSpotlight(activeEntry != null ? activeEntry.spotlightTarget : null);
        if (bulletGuideRoot != null) bulletGuideRoot.gameObject.SetActive(activeEntry != null && activeEntry.bulletGuide);
        if (timingRing != null) timingRing.gameObject.SetActive(false);
        if (nowLabel != null) nowLabel.gameObject.SetActive(false);
        if (zoneLabel != null) zoneLabel.text = Loc(KeyDrawHere, "ここに線を引く");
        if (nowLabel != null) nowLabel.text = Loc(KeyNow, "今！");
    }

    private GuideEntry FindEntry(int step, int hint, bool requireExtraHint)
    {
        GuideEntry wildcard = null;
        foreach (var e in entries)
        {
            if (e == null || e.stepIndex != step) continue;
            if (requireExtraHint && string.IsNullOrEmpty(e.extraHintKey) && string.IsNullOrEmpty(e.extraHintFallback)
                && string.IsNullOrEmpty(e.extraHintKey2) && string.IsNullOrEmpty(e.extraHintFallback2)) continue;
            if (e.hintIndex == hint) return e;
            if (e.hintIndex < 0 && wildcard == null) wildcard = e;
        }
        return wildcard;
    }

    // ================= 毎フレーム =================

    /// <summary>タブの横幅を見出しの文字幅に合わせる（「ミッション」「MISSION」「チュートリアル完了！」で幅が変わるため）</summary>
    private void FitMissionTab()
    {
        if (missionTab == null || missionHeaderText == null) return;
        float w = missionHeaderText.GetPreferredValues(missionHeaderText.text, float.PositiveInfinity, float.PositiveInfinity).x;
        missionTab.sizeDelta = new Vector2(w + missionTabTextPadding * 2f, missionTab.sizeDelta.y);
    }

    private void UpdateMissionFramePulse(float now)
    {
        float a = Mathf.Lerp(missionFramePulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(now * missionFramePulseSpeed));
        if (missionFrameImage != null) { Color c = missionFrameImage.color; c.a = a; missionFrameImage.color = c; }
        if (missionTabFrameImage != null) { Color c = missionTabFrameImage.color; c.a = a; missionTabFrameImage.color = c; }
    }

    private void LateUpdate()
    {
        if (stepIndex < 0 || frontRoot == null) return;
        float now = Time.unscaledTime;
        if (cam == null) cam = Camera.main;
        if (dancer == null) dancer = FindFirstObjectByType<PixelDancerController>();

        TrackInput(now);
        UpdateRowPulse(now);
        UpdateMissionFramePulse(now);
        UpdateSuccessPop();
        UpdateNextPulse(now);
        UpdateCollapse();
        UpdateFinger(now);
        UpdateSpotlight(now);
        UpdateBulletGuide(now);
    }

    private void TrackInput(float now)
    {
        bool pressed = Input.GetMouseButtonDown(0);
        for (int i = 0; !pressed && i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Began) pressed = true;
        if (pressed)
        {
            lastInputTime = now;
            fingerSuppressed = true;
        }
        else if (fingerSuppressed && now - lastInputTime >= idleReshowSeconds)
        {
            fingerSuppressed = false;
            fingerLoopStart = now;
        }
    }

    private void UpdateRowPulse(float now)
    {
        if (!practiceStep || stepCompleted || missionRows == null) return;
        if (tipRow >= 0 && now >= tipUntil) RestoreRowTip();
        if (hintIndex < 0 || hintIndex >= missionRows.Length) return;
        var row = missionRows[hintIndex];
        if (row == null) return;
        float k = 0.75f + 0.25f * Mathf.Sin(now * currentRowPulseSpeed);
        Color c = tipRow == hintIndex ? tipColor : rowCurrentColor;
        c.a = k;
        if (row.text != null) row.text.color = c;
        if (row.box != null) row.box.color = c;
    }

    private void PlaySuccessPop()
    {
        if (successPop == null) return;
        if (successText != null) successText.text = Loc(KeySuccess, "成功！");
        successPop.gameObject.SetActive(true);
        successPopTimer = 0f;
    }

    private void UpdateSuccessPop()
    {
        if (successPop == null || successPopTimer < 0f) return;
        successPopTimer += Time.unscaledDeltaTime;
        float t = successPopTimer / Mathf.Max(0.1f, successPopSeconds);
        if (t >= 1f)
        {
            successPopTimer = -1f;
            successPop.gameObject.SetActive(false);
            return;
        }
        float scale = t < 0.18f ? Mathf.Lerp(0.3f, 1.25f, t / 0.18f)
                    : t < 0.3f ? Mathf.Lerp(1.25f, 1f, (t - 0.18f) / 0.12f) : 1f;
        float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
        successPop.localScale = Vector3.one * scale;
        successPop.anchoredPosition = successPopBasePos + new Vector2(0f, t > 0.7f ? (t - 0.7f) * 60f : 0f);
        if (successText != null) successText.alpha = alpha;
    }

    private void UpdateNextPulse(float now)
    {
        if (nextButtonIcon == null) return;
        if (!stepCompleted || !practiceStep) { nextButtonIcon.localScale = Vector3.one; return; }
        float k = 0.5f + 0.5f * Mathf.Sin(now * nextPulseSpeed);
        nextButtonIcon.localScale = Vector3.one * Mathf.Lerp(1f, nextPulseScale, k);
    }

    // ---------- ⑨ カード ----------
    private void ToggleCollapse() => SetCollapsed(!collapsed, instant: false);

    private void SetCollapsed(bool value, bool instant)
    {
        CaptureCardBase();
        collapsed = value;
        if (instant) collapseK = value ? 1f : 0f;
        if (collapseToggleLabel != null) collapseToggleLabel.text = value ? "▼" : "▲";
        if (bodyGroup != null) bodyGroup.blocksRaycasts = false;
        ApplyCollapse();
    }

    private void UpdateCollapse()
    {
        float target = collapsed ? 1f : 0f;
        if (Mathf.Approximately(collapseK, target)) return;
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, collapseSeconds);
        collapseK = Mathf.MoveTowards(collapseK, target, step);
        ApplyCollapse();
    }

    private void ApplyCollapse()
    {
        if (cardRect == null || !cardBaseCaptured) return;
        float k = collapseK * collapseK * (3f - 2f * collapseK);
        float h = Mathf.Lerp(cardExpandedSize.y, Mathf.Min(collapsedHeight, cardExpandedSize.y), k);
        // ★下端の位置を変えずに上側だけ縮める（ボタンは下端基準の配置のため、位置が変わらない）
        float dy = (cardExpandedSize.y - h) * cardRect.pivot.y;
        cardRect.sizeDelta = new Vector2(cardExpandedSize.x, h);
        cardRect.anchoredPosition = cardExpandedPos - new Vector2(0f, dy);
        if (bodyGroup != null) bodyGroup.alpha = 1f - Mathf.Clamp01(k * 1.6f);
    }

    // ---------- ⑤ 指 ----------
    private void SetFingerVisible(bool v)
    {
        if (finger != null && finger.gameObject.activeSelf != v) finger.gameObject.SetActive(v);
        if (touchRing != null && !v && touchRing.gameObject.activeSelf) touchRing.gameObject.SetActive(false);
    }

    private void UpdateFinger(float now)
    {
        var e = activeEntry;
        if (e == null || e.finger == FingerMotion.None || fingerSuppressed || finger == null)
        {
            SetFingerVisible(false);
            return;
        }
        if (!TryGetFingerPose(e, now - fingerLoopStart, out Vector2 pos, out bool press, out float ringK, out float alpha))
        {
            SetFingerVisible(false);
            return;
        }
        SetFingerVisible(alpha > 0.01f);
        finger.anchoredPosition = pos;
        finger.localScale = Vector3.one * (press ? 0.92f : 1f);
        if (fingerImage != null) { Color c = fingerImage.color; c.a = alpha * fingerAlpha; fingerImage.color = c; }
        if (touchRing != null)
        {
            bool showRing = press && alpha > 0.01f;
            if (touchRing.gameObject.activeSelf != showRing) touchRing.gameObject.SetActive(showRing);
            if (showRing)
            {
                touchRing.anchoredPosition = pos;
                touchRing.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.2f, ringK);
                if (touchRingImage != null) { Color c = touchRingImage.color; c.a = alpha * (1f - 0.6f * ringK); touchRingImage.color = c; }
            }
        }
    }

    /// <summary>指の位置・押下状態を求める（Canvas座標）。表示できない時はfalse</summary>
    private bool TryGetFingerPose(GuideEntry e, float t, out Vector2 pos, out bool press, out float ringK, out float alpha)
    {
        pos = Vector2.zero; press = false; ringK = 0f; alpha = 0f;
        if (!TryGetAnchor(e, out Vector2 anchorLocal, out Vector3 anchorWorld, out bool isWorld)) return false;

        float pause = Mathf.Max(0f, loopPauseSeconds);
        Vector3 perp = new Vector3(-bulletDir.y, bulletDir.x, 0f);
        if (perp.sqrMagnitude < 0.01f) perp = Vector3.right;
        if (perp.x < 0f) perp = -perp;

        switch (e.finger)
        {
            case FingerMotion.Swipe:
            {
                float total = swipeSeconds + pause;
                float p = Mathf.Repeat(t, total);
                if (p > swipeSeconds) return true; // 休み中（非表示）
                float k = Ease(p / swipeSeconds);
                Vector3 a = anchorWorld - perp * e.fingerSize * 0.5f, b = anchorWorld + perp * e.fingerSize * 0.5f;
                pos = isWorld ? WorldToLocal(Vector3.Lerp(a, b, k)) : anchorLocal;
                press = true; ringK = p / swipeSeconds; alpha = Edge(p / swipeSeconds);
                return true;
            }
            case FingerMotion.DoubleSwipe:
            {
                float total = swipeSeconds * 2f + 0.25f + pause;
                float p = Mathf.Repeat(t, total);
                float lift = 0.6f;
                int idx = p < swipeSeconds ? 0 : (p >= swipeSeconds + 0.25f && p < swipeSeconds * 2f + 0.25f) ? 1 : -1;
                if (idx < 0) return true;
                float lp = idx == 0 ? p : p - swipeSeconds - 0.25f;
                float k = Ease(lp / swipeSeconds);
                Vector3 c = anchorWorld + Vector3.up * (idx == 0 ? lift : -lift);
                Vector3 a = c - perp * e.fingerSize * 0.5f, b = c + perp * e.fingerSize * 0.5f;
                pos = isWorld ? WorldToLocal(Vector3.Lerp(a, b, k)) : anchorLocal;
                press = true; ringK = lp / swipeSeconds; alpha = Edge(lp / swipeSeconds);
                return true;
            }
            case FingerMotion.Circle:
            {
                float total = circleSeconds + pause;
                float p = Mathf.Repeat(t, total);
                if (p > circleSeconds) return true;
                float k = p / circleSeconds;
                float ang = Mathf.PI * 0.5f + k * Mathf.PI * 2.1f;
                Vector3 w = anchorWorld + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * e.fingerSize;
                pos = isWorld ? WorldToLocal(w) : anchorLocal;
                press = true; ringK = Mathf.Repeat(k * 3f, 1f); alpha = Edge(k);
                return true;
            }
            case FingerMotion.LongPressSwipe:
            {
                float total = holdSeconds + swipeSeconds + pause;
                float p = Mathf.Repeat(t, total);
                if (p > holdSeconds + swipeSeconds) return true;
                Vector3 a = anchorWorld - perp * e.fingerSize * 0.5f, b = anchorWorld + perp * e.fingerSize * 0.5f;
                if (p < holdSeconds)
                {
                    pos = isWorld ? WorldToLocal(a) : anchorLocal;
                    ringK = p / holdSeconds;   // 長押し中はリングが広がる
                    alpha = Mathf.Clamp01(p / 0.15f);
                }
                else
                {
                    float sp = (p - holdSeconds) / swipeSeconds;
                    pos = isWorld ? WorldToLocal(Vector3.Lerp(a, b, Ease(sp))) : anchorLocal;
                    ringK = 1f; alpha = sp > 0.85f ? (1f - sp) / 0.15f : 1f;
                }
                press = true;
                return true;
            }
            case FingerMotion.Tap:
            case FingerMotion.Hold:
            {
                float approach = 0.35f, pressDur = e.finger == FingerMotion.Hold ? holdSeconds : 0.3f;
                float total = approach + pressDur + 0.2f + pause;
                float p = Mathf.Repeat(t, total);
                Vector2 target = isWorld ? WorldToLocal(anchorWorld) : anchorLocal;
                Vector2 from = target + new Vector2(60f, -70f);
                if (p < approach) { pos = Vector2.Lerp(from, target, Ease(p / approach)); alpha = p / approach; }
                else if (p < approach + pressDur) { pos = target; press = true; ringK = (p - approach) / pressDur; alpha = 1f; }
                else if (p < approach + pressDur + 0.2f) { pos = Vector2.Lerp(target, from, (p - approach - pressDur) / 0.2f); alpha = 1f - (p - approach - pressDur) / 0.2f; }
                else return true;
                return true;
            }
        }
        return false;
    }

    private bool TryGetAnchor(GuideEntry e, out Vector2 local, out Vector3 world, out bool isWorld)
    {
        local = Vector2.zero; world = Vector3.zero; isWorld = true;
        switch (e.fingerAnchor)
        {
            case GuideAnchor.UITarget:
                if (e.fingerUITarget == null || !e.fingerUITarget.gameObject.activeInHierarchy) return false;
                if (!TryGetUIRect(e.fingerUITarget, out Rect r)) return false;
                local = r.center + e.fingerOffset;
                isWorld = false;
                return true;
            case GuideAnchor.Soul:
                if (dancer == null || !dancer.IsFalling || dancer.SoulTransform == null || !dancer.SoulTransform.gameObject.activeInHierarchy) return false;
                world = dancer.SoulTransform.position + (Vector3)e.fingerOffset;
                return cam != null;
            case GuideAnchor.BulletPath:
                if (!TryGetZoneWorld(out world)) return false;
                world += (Vector3)e.fingerOffset;
                return cam != null;
            default: // FieldCenter
                if (dancer == null) return false;
                world = dancer.transform.position + Vector3.up * fieldCenterHeight + (Vector3)e.fingerOffset;
                return cam != null;
        }
    }

    private static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    private static float Edge(float x) => Mathf.Clamp01(Mathf.Min(x / 0.12f, (1f - x) / 0.12f));

    // ---------- ⑥ スポットライト ----------
    private RectTransform spotTarget;

    private void SetSpotlight(RectTransform target)
    {
        spotTarget = target;
        bool on = target != null;
        if (spotDims != null) foreach (var d in spotDims) if (d != null) d.gameObject.SetActive(on);
        if (spotFrame != null) spotFrame.gameObject.SetActive(on);
        if (spotArrow != null) spotArrow.gameObject.SetActive(false);
        if (on != spotlightActive)
        {
            spotlightActive = on;
            // ★Panelの全面暗幕の代わりに、照らす部分だけ抜いた暗幕を使う
            SetPanelDimAlpha(on ? 0f : panelDimNormalAlpha);
        }
    }

    private void SetPanelDimAlpha(float a)
    {
        if (panelDimImage == null) return;
        Color c = panelDimImage.color; c.a = a; panelDimImage.color = c;
    }

    private void UpdateSpotlight(float now)
    {
        if (spotTarget == null || backRoot == null) return;
        bool ok = spotTarget.gameObject.activeInHierarchy && TryGetUIRect(spotTarget, out Rect r);
        if (!ok)
        {
            if (spotDims != null) foreach (var d in spotDims) if (d != null) d.gameObject.SetActive(false);
            if (spotFrame != null) spotFrame.gameObject.SetActive(false);
            SetPanelDimAlpha(panelDimNormalAlpha);
            return;
        }
        TryGetUIRect(spotTarget, out r);
        float pad = activeEntry != null ? activeEntry.spotlightPadding : 12f;
        r.xMin -= pad; r.yMin -= pad; r.xMax += pad; r.yMax += pad;
        SetPanelDimAlpha(0f);

        Rect full = backRoot.rect;
        if (spotDims != null && spotDims.Length >= 4)
        {
            SetRect(spotDims[0], new Rect(full.xMin, r.yMax, full.width, full.yMax - r.yMax));          // 上
            SetRect(spotDims[1], new Rect(full.xMin, full.yMin, full.width, r.yMin - full.yMin));        // 下
            SetRect(spotDims[2], new Rect(full.xMin, r.yMin, r.xMin - full.xMin, r.height));             // 左
            SetRect(spotDims[3], new Rect(r.xMax, r.yMin, full.xMax - r.xMax, r.height));                // 右
            foreach (var d in spotDims) { if (d == null) continue; d.gameObject.SetActive(true); Color c = d.color; c.a = spotDimAlpha; d.color = c; }
        }
        if (spotFrame != null)
        {
            spotFrame.gameObject.SetActive(true);
            spotFrame.anchoredPosition = r.center;
            spotFrame.sizeDelta = r.size;
            if (spotFrameImage != null) { Color c = spotFrameImage.color; c.a = 0.55f + 0.45f * Mathf.Sin(now * 4f); spotFrameImage.color = c; }
        }
        // 矢印：指を出さない時だけ。画面中央側から照らす部分を指す
        bool arrow = spotArrow != null && (activeEntry == null || activeEntry.finger == FingerMotion.None);
        if (spotArrow != null) spotArrow.gameObject.SetActive(arrow);
        if (arrow)
        {
            Vector2 dir = (Vector2.zero - r.center);
            if (dir.sqrMagnitude < 1f) dir = Vector2.down;
            dir.Normalize();
            float reach = Mathf.Abs(dir.x) * r.width * 0.5f + Mathf.Abs(dir.y) * r.height * 0.5f;
            float bob = 10f * Mathf.Sin(now * 5f);
            spotArrow.anchoredPosition = r.center + dir * (reach + spotArrowDistance + bob);
            float ang = Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg; // 矢印画像は右向き。照らす部分の方向へ回す
            spotArrow.localRotation = Quaternion.Euler(0f, 0f, ang);
        }
    }

    private static void SetRect(Image img, Rect r)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        rt.anchoredPosition = r.center;
        rt.sizeDelta = new Vector2(Mathf.Max(0f, r.width), Mathf.Max(0f, r.height));
    }

    // ---------- ⑦⑧ 弾の目印・タイミングリング ----------
    private bool TryGetZoneWorld(out Vector3 zone)
    {
        zone = Vector3.zero;
        if (bulletSpawn == null || dancer == null) return false;
        Vector3 s = bulletSpawn.position;
        float along = Vector2.Dot((Vector2)(dancer.transform.position - s), bulletDir);
        if (along <= 0.1f) return false;
        zone = s + (Vector3)(bulletDir * along * bulletPathT);
        return true;
    }

    private void UpdateBulletGuide(float now)
    {
        var e = activeEntry;
        bool wantGuide = e != null && e.bulletGuide;
        bool wantRing = e != null && e.timingRing;
        if (!wantGuide && !wantRing)
        {
            if (bulletGuideRoot != null && bulletGuideRoot.gameObject.activeSelf) bulletGuideRoot.gameObject.SetActive(false);
            if (timingRing != null && timingRing.gameObject.activeSelf) timingRing.gameObject.SetActive(false);
            if (nowLabel != null && nowLabel.gameObject.activeSelf) nowLabel.gameObject.SetActive(false);
            return;
        }
        if (cam == null || !TryGetZoneWorld(out Vector3 zoneW)) return;

        bool bulletLive = trackedBullet != null && trackedBullet.gameObject.activeInHierarchy && !trackedBullet.IsReflected;
        Vector2 zoneL = WorldToLocal(zoneW);
        Vector3 perp = new Vector3(-bulletDir.y, bulletDir.x, 0f);

        // ⑦：目印（常時）＋弾から目印までの点線（弾が飛んでいる間）
        if (bulletGuideRoot != null)
        {
            if (bulletGuideRoot.gameObject.activeSelf != wantGuide) bulletGuideRoot.gameObject.SetActive(wantGuide);
            if (wantGuide && zoneMarker != null)
            {
                Vector2 a = WorldToLocal(zoneW - perp * zoneWidthWorld * 0.5f), b = WorldToLocal(zoneW + perp * zoneWidthWorld * 0.5f);
                zoneMarker.anchoredPosition = zoneL;
                zoneMarker.sizeDelta = new Vector2(Vector2.Distance(a, b), zoneMarker.sizeDelta.y);
                zoneMarker.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                if (zoneMarkerImage != null) { Color c = zoneMarkerImage.color; c.a = 0.55f + 0.35f * Mathf.Sin(now * 4f); zoneMarkerImage.color = c; }
                if (zoneLabel != null) zoneLabel.rectTransform.anchoredPosition = zoneL + new Vector2(0f, -46f);
            }
            if (guideDots != null)
            {
                Vector2 from = bulletLive ? WorldToLocal(trackedBullet.transform.position) : zoneL;
                for (int i = 0; i < guideDots.Length; i++)
                {
                    var d = guideDots[i];
                    if (d == null) continue;
                    bool on = wantGuide && bulletLive;
                    if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
                    if (!on) continue;
                    float k = (i + Mathf.Repeat(now * 2f, 1f)) / guideDots.Length; // 目印へ向かって流れる
                    d.rectTransform.anchoredPosition = Vector2.Lerp(from, zoneL, k);
                    Color c = d.color; c.a = 0.35f + 0.6f * k; d.color = c;
                }
            }
        }

        // ⑧：弾が目印に届くまでの時間でリングを縮め、届く直前に「今！」
        bool ringOn = false, nowOn = false;
        if (wantRing && bulletLive && timingRing != null)
        {
            float speed = trackedBulletRb != null ? trackedBulletRb.linearVelocity.magnitude : 0f;
            float dist = Vector2.Dot((Vector2)(zoneW - trackedBullet.transform.position), bulletDir);
            if (speed > 0.01f)
            {
                float tt = dist / speed;
                if (tt <= ringStartSeconds && tt > -0.15f)
                {
                    ringOn = true;
                    float k = Mathf.Clamp01(tt / ringStartSeconds);
                    timingRing.anchoredPosition = WorldToLocal(trackedBullet.transform.position);
                    float size = Mathf.Lerp(ringMinSize, ringMaxSize, k);
                    timingRing.sizeDelta = new Vector2(size, size);
                    nowOn = tt <= nowLeadSeconds;
                    if (timingRingImage != null)
                        timingRingImage.color = nowOn ? new Color(1f, 0.35f, 0.35f, 1f) : new Color(1f, 0.85f, 0.3f, Mathf.Lerp(1f, 0.4f, k));
                }
            }
        }
        if (timingRing != null && timingRing.gameObject.activeSelf != ringOn) timingRing.gameObject.SetActive(ringOn);
        if (nowLabel != null)
        {
            if (nowLabel.gameObject.activeSelf != nowOn) nowLabel.gameObject.SetActive(nowOn);
            if (nowOn) nowLabel.rectTransform.anchoredPosition = zoneL + new Vector2(0f, 56f);
        }
    }

    // ================= 座標変換 =================
    private Vector2 WorldToLocal(Vector3 world)
    {
        if (cam == null || frontRoot == null) return Vector2.zero;
        Vector2 screen = cam.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(frontRoot, screen, null, out Vector2 local);
        return local;
    }

    private static readonly Vector3[] corners = new Vector3[4];

    /// <summary>別Canvas（Screen Space - Camera等）のUIの位置を、このガイドのCanvas座標の矩形で返す</summary>
    private bool TryGetUIRect(RectTransform target, out Rect rect)
    {
        rect = default;
        if (target == null || frontRoot == null) return false;
        Canvas c = target.GetComponentInParent<Canvas>();
        Camera uiCam = null;
        if (c != null)
        {
            Canvas root = c.rootCanvas;
            if (root.renderMode != RenderMode.ScreenSpaceOverlay) uiCam = root.worldCamera != null ? root.worldCamera : Camera.main;
        }
        target.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(uiCam, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frontRoot, sp, null, out Vector2 l);
            min = Vector2.Min(min, l); max = Vector2.Max(max, l);
        }
        if (max.x - min.x < 1f || max.y - min.y < 1f) return false;
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    // ================= 文言 =================
    private static string Loc(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        return Game.Localization.LocalizationManager.GetStatic(key, fallback);
    }

    private static string SafeFormat(string fmt, int a, int b)
    {
        try { return string.Format(fmt, a, b); } catch (System.FormatException) { return $"{a}/{b}"; }
    }

#if UNITY_EDITOR
    /// <summary>メニューから参照・既定値を書き込むためのアクセス（Editor専用）</summary>
    public List<GuideEntry> EditorEntries => entries;
    public List<KeywordColor> EditorKeywords => keywords;
#endif
}
