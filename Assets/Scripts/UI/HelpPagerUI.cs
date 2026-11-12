using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 中断メニューのHELP画面（05_Game／チュートリアル共通）を、1ページ1項目のページ送りで見せる。
/// ・見出しにアイコン ・本文は箇条書き（2行目以降も字下げを揃える） ・キーワードの色分け（チュートリアルのガイドと同じ色）
/// ・◀ ▶ ボタン／ページの点／左右スワイプで切り替え。中断中（timeScale=0）でも動くよう実時間で動かす。
/// 部品はメニュー「Tools/中断画面/HELPをページ送りにする」でPlay前に生成する。
/// 文言はLocalizationTableの pause.help.page{番号}.title / .body（日本語・英語）。未登録時はInspectorの日本語を表示する
/// </summary>
public class HelpPagerUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [System.Serializable]
    public class Page
    {
        public Sprite icon;
        [Tooltip("このページだけアイコンを大きく／小さくする倍率（1＝そのまま。中心を保ったまま拡大縮小）")]
        public float iconScale = 1f;
        [Tooltip("このページだけアイコンをずらす量(px)。マイナスのYで下へ")]
        public Vector2 iconOffset;
        [Tooltip("LocalizationTableが無い時の見出し（日本語）")]
        public string fallbackTitle;
        [Tooltip("LocalizationTableが無い時の本文（日本語）。1行1項目、行頭に「・」")]
        [TextArea(3, 8)] public string fallbackBody;
        [Tooltip("本文の各行（・の行）の左に出すアイコン。[0]＝1行目。空の行は「・」のまま（2026/11/12）")]
        public Sprite[] lineIcons;
    }

    [System.Serializable]
    public class KeywordColor
    {
        public string keyword;
        public Color color = Color.yellow;
    }

    [Header("参照（メニューで自動設定）")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Image[] dots;

    [Header("本文の行のアイコン（2026/11/12）")]
    [Tooltip("行のアイコンを置く入れ物（メニューで本文の子に作る）。行のアイコンの数より多く用意する")]
    [SerializeField] private Image[] lineIconSlots;
    [Tooltip("行のアイコンの大きさ（本文の文字の大きさに対する倍率）")]
    [SerializeField] private float lineIconSizeEm = 1.3f;
    [Tooltip("アイコンのある行の、文字の始まる位置（本文の文字の大きさに対する倍率）。アイコンはこの幅の真ん中に置く")]
    [SerializeField] private float lineIconIndentEm = 1.7f;
    [Tooltip("行のアイコンのずらし（本文の文字の大きさに対する倍率）。上下の中央は、その行の最初の文字の上下の中央に合わせてある")]
    [SerializeField] private Vector2 lineIconOffsetEm = Vector2.zero;
    [Tooltip("見出しのアイコンが無いページで、見出しの文字を始める左の位置（アイコンの左端と同じ）")]
    [SerializeField] private float titleLeftWithoutIcon = 40f;

    [Header("本文のスクロール（2026/11/12）")]
    [Tooltip("本文を表示する枠（はみ出した分は隠す）。本文が枠より長い時は上下にスクロールできる。メニューで設定")]
    [SerializeField] private RectTransform bodyViewport;
    [Tooltip("スクロールできる時だけ右端に出すつまみ（メニューで設定）")]
    [SerializeField] private Image scrollThumb;
    [Tooltip("マウスのホイール1目盛りで動く量")]
    [SerializeField] private float wheelScrollAmount = 60f;
    [Tooltip("ドラッグを縦のスクロールとみなす、縦の動きの量（これより横に大きく動いたらページ送り）")]
    [SerializeField] private float dragDirectionThreshold = 12f;

    [Header("ページ（順番どおりに表示）")]
    [SerializeField] private string localizationKeyPrefix = "pause.help.page";
    [SerializeField] private Page[] pages;

    [Header("見た目")]
    [SerializeField] private Color bulletColor = new Color(0.45f, 0.9f, 1f, 1f);
    [Tooltip("箇条書きの2行目以降の字下げ量")]
    [SerializeField] private string bulletIndent = "1.15em";
    [SerializeField] private Color dotActiveColor = new Color(0.45f, 0.95f, 1f, 1f);
    [SerializeField] private Color dotInactiveColor = new Color(0.45f, 0.5f, 0.6f, 0.6f);
    [SerializeField] private float dotActiveScale = 1.35f;
    [Tooltip("先頭／最後のページで押せない方の矢印の濃さ")]
    [SerializeField, Range(0f, 1f)] private float disabledArrowAlpha = 0.25f;

    [Header("切り替え")]
    [SerializeField] private float transitionSeconds = 0.22f;
    [SerializeField] private float transitionSlide = 50f;
    [Tooltip("この距離以上横にスワイプしたらページを切り替える")]
    [SerializeField] private float swipeThreshold = 80f;
    [SerializeField] private AudioClip pageTurnSe;
    [SerializeField, Range(0f, 1f)] private float pageTurnSeVolume = 0.8f;

    [Header("キーワードの色分け（チュートリアルのガイドと同じ色を、メニューでコピーして設定）")]
    [SerializeField] private List<KeywordColor> keywords = new List<KeywordColor>();

    private int current;
    private float transitionT = -1f;
    private int transitionDir;
    private Vector2 contentBasePos;
    private bool baseCaptured;
    private Vector2 iconBaseSize, iconBasePos;
    private bool iconBaseCaptured;
    private float dragDx;
    private AudioSource seSource;
    private Regex keywordRegex;
    private Dictionary<string, string> keywordHex;
    private int lineIconDirty;          // 行のアイコンを置き直す残りフレーム数（文字の並びが決まるのを待つため数フレーム続ける）
    private float titleBaseLeft;
    private bool titleBaseCaptured;

    private void Awake()
    {
        if (prevButton != null) prevButton.onClick.AddListener(() => Go(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Go(1));
        seSource = gameObject.AddComponent<AudioSource>();
        seSource.playOnAwake = false;
        seSource.spatialBlend = 0f;
        seSource.ignoreListenerPause = true; // 中断中でも鳴らす
    }

    private void OnEnable()
    {
        if (!baseCaptured && contentRect != null) { contentBasePos = contentRect.anchoredPosition; baseCaptured = true; }
        current = 0; // HELPを開くたびに1ページ目から
        ShowPage(0, animate: true);
    }

    /// <summary>ページごとのアイコンの大きさ・位置（基準はメニューで作った時の大きさ・位置）</summary>
    private void ApplyIconLayout(Page p)
    {
        var rt = iconImage.rectTransform;
        if (!iconBaseCaptured) { iconBaseSize = rt.sizeDelta; iconBasePos = rt.anchoredPosition; iconBaseCaptured = true; }
        float s = p.iconScale > 0f ? p.iconScale : 1f;
        Vector2 size = iconBaseSize * s;
        Vector2 grow = size - iconBaseSize;
        // 中心を保つ：基準点（pivot）の位置を、増えた大きさの分だけ戻す
        Vector2 pv = rt.pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = iconBasePos + new Vector2((pv.x - 0.5f) * grow.x, (pv.y - 0.5f) * grow.y) + p.iconOffset;
    }

    public void Go(int delta)
    {
        int count = pages != null ? pages.Length : 0;
        int next = Mathf.Clamp(current + delta, 0, Mathf.Max(0, count - 1));
        if (next == current) return;
        transitionDir = delta > 0 ? 1 : -1;
        current = next;
        ShowPage(current, animate: true);
        if (pageTurnSe != null && seSource != null)
        {
            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
            seSource.PlayOneShot(pageTurnSe, pageTurnSeVolume * vol);
        }
    }

    private void ShowPage(int i, bool animate)
    {
        if (pages == null || pages.Length == 0) return;
        i = Mathf.Clamp(i, 0, pages.Length - 1);
        var p = pages[i];
        string title = Game.Localization.LocalizationManager.GetStatic($"{localizationKeyPrefix}{i}.title", p.fallbackTitle);
        string body = Game.Localization.LocalizationManager.GetStatic($"{localizationKeyPrefix}{i}.body", p.fallbackBody);

        if (iconImage != null) { iconImage.sprite = p.icon; iconImage.enabled = p.icon != null; iconImage.preserveAspect = true; ApplyIconLayout(p); }
        if (titleText != null)
        {
            titleText.text = title;
            // 見出しのアイコンが無いページは、見出しをアイコンの位置から始める（2026/11/12）
            var tr = titleText.rectTransform;
            if (!titleBaseCaptured) { titleBaseLeft = tr.offsetMin.x; titleBaseCaptured = true; }
            tr.offsetMin = new Vector2(p.icon != null ? titleBaseLeft : titleLeftWithoutIcon, tr.offsetMin.y);
        }
        if (bodyText != null) bodyText.text = FormatBody(body, p);
        HideLineIcons();
        scroll = 0f; // ページを変えたら一番上から
        lineIconDirty = 3;

        for (int d = 0; dots != null && d < dots.Length; d++)
        {
            if (dots[d] == null) continue;
            bool used = d < pages.Length;
            dots[d].gameObject.SetActive(used);
            dots[d].color = d == i ? dotActiveColor : dotInactiveColor;
            dots[d].rectTransform.localScale = Vector3.one * (d == i ? dotActiveScale : 1f);
        }
        SetArrow(prevButton, i > 0);
        SetArrow(nextButton, i < pages.Length - 1);

        if (animate) transitionT = 0f;
    }

    private void SetArrow(Button b, bool on)
    {
        if (b == null) return;
        b.interactable = on;
        var g = b.targetGraphic;
        if (g != null) { Color c = g.color; c.a = on ? 1f : disabledArrowAlpha; g.color = c; }
    }

    private static bool HasAnyIcon(Page p)
    {
        if (p == null || p.lineIcons == null) return false;
        foreach (var s in p.lineIcons) if (s != null) return true;
        return false;
    }

    /// <summary>
    /// 行頭の「・」「•」を色付きの点にし、2行目以降の折り返しも本文の頭に揃える。キーワードに色を付ける。
    /// 行のアイコンがあるページは、アイコンの行は「・」を消して、アイコンの幅だけ文字を右へずらす（アイコンは PlaceLineIcons で置く）
    /// </summary>
    private string FormatBody(string body, Page page)
    {
        if (string.IsNullOrEmpty(body)) return body;
        string hex = ColorUtility.ToHtmlStringRGB(bulletColor);
        bool iconPage = HasAnyIcon(page);
        string indent = iconPage ? $"{lineIconIndentEm.ToString(System.Globalization.CultureInfo.InvariantCulture)}em" : bulletIndent;
        var sb = new StringBuilder();
        string[] lines = body.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (i > 0) sb.Append('\n');
            if (line.StartsWith("・") || line.StartsWith("•"))
            {
                string rest = line.Substring(1).TrimStart();
                bool hasIcon = iconPage && i < page.lineIcons.Length && page.lineIcons[i] != null;
                if (hasIcon) sb.Append($"<indent={indent}>{Colorize(rest)}</indent>");
                else sb.Append($"<color=#{hex}>{line[0]}</color><indent={indent}>{Colorize(rest)}</indent>");
            }
            else sb.Append(Colorize(line));
        }
        return sb.ToString();
    }

    private void HideLineIcons()
    {
        if (lineIconSlots == null) return;
        foreach (var s in lineIconSlots) if (s != null && s.enabled) s.enabled = false;
    }

    /// <summary>
    /// 行のアイコンを、その行の最初の文字の左に置く。上下は「最初の文字の形の上端と下端の真ん中」に合わせる
    /// （行の高さの真ん中に合わせると、日本語の文字より上にずれて見えるため）。本文の文字の大きさが自動で変わっても追従する
    /// </summary>
    private void PlaceLineIcons()
    {
        HideLineIcons();
        if (bodyText == null || lineIconSlots == null || pages == null || current < 0 || current >= pages.Length) return;
        var page = pages[current];
        if (!HasAnyIcon(page)) return;
        bodyText.ForceMeshUpdate();
        var ti = bodyText.textInfo;
        int paragraph = 0, slot = 0;
        bool needFirst = true;
        for (int c = 0; c < ti.characterCount; c++)
        {
            var ch = ti.characterInfo[c];
            if (ch.character == '\n') { paragraph++; needFirst = true; continue; }
            if (!needFirst || !ch.isVisible) continue;
            needFirst = false;
            if (paragraph >= page.lineIcons.Length || page.lineIcons[paragraph] == null) continue;
            if (slot >= lineIconSlots.Length) break;
            var img = lineIconSlots[slot++];
            if (img == null) continue;
            float pt = ch.pointSize;
            // ★横は「本文の左端」と「行の最初の文字」の間の、実際に空いている幅の真ん中に置く
            //   （emの計算だけで置くと、フォントの大きさの補正の分だけ文字に重なることがあったため）
            float left = bodyText.rectTransform.rect.xMin + bodyText.margin.x;
            float gap = Mathf.Max(1f, ch.origin - left);
            float size = Mathf.Min(pt * lineIconSizeEm, gap * 0.9f);
            float cx = (left + ch.origin) * 0.5f + lineIconOffsetEm.x * pt;
            float cy = (ch.topLeft.y + ch.bottomLeft.y) * 0.5f + lineIconOffsetEm.y * pt;
            img.sprite = page.lineIcons[paragraph];
            img.preserveAspect = true;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(size, size);
            rt.localPosition = new Vector3(cx, cy, 0f);
            img.enabled = true;
        }
    }

    private void LateUpdate()
    {
        if (lineIconDirty <= 0) return;
        lineIconDirty--;
        UpdateBodyHeight();
        PlaceLineIcons();
    }

    // ---------- 本文のスクロール ----------
    private float scroll;          // 本文を上へずらしている量（0＝一番上）
    private float maxScroll;

    /// <summary>本文の高さを文字の量に合わせ、枠より長ければスクロールできるようにする</summary>
    private void UpdateBodyHeight()
    {
        if (bodyText == null || bodyViewport == null) { maxScroll = 0f; UpdateThumb(); return; }
        var body = bodyText.rectTransform;
        float viewH = bodyViewport.rect.height;
        float width = body.rect.width;
        float need = width > 0f ? bodyText.GetPreferredValues(bodyText.text, width, 0f).y : viewH;
        float h = Mathf.Max(viewH, need + 4f);
        body.sizeDelta = new Vector2(body.sizeDelta.x, h);
        maxScroll = Mathf.Max(0f, h - viewH);
        ApplyScroll();
    }

    private void ApplyScroll()
    {
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);
        if (bodyText != null && bodyViewport != null)
        {
            var body = bodyText.rectTransform;
            body.anchoredPosition = new Vector2(body.anchoredPosition.x, scroll);
        }
        UpdateThumb();
    }

    private void UpdateThumb()
    {
        if (scrollThumb == null) return;
        bool can = maxScroll > 0.5f && bodyViewport != null;
        if (scrollThumb.enabled != can) scrollThumb.enabled = can;
        if (!can) return;
        float viewH = bodyViewport.rect.height;
        float contentH = viewH + maxScroll;
        float thumbH = Mathf.Max(30f, viewH * viewH / contentH);
        var rt = scrollThumb.rectTransform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, thumbH);
        float t = maxScroll > 0f ? scroll / maxScroll : 0f;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -t * (viewH - thumbH));
    }

    private float CanvasScale()
    {
        var c = GetComponentInParent<Canvas>();
        return c != null ? Mathf.Max(0.0001f, c.rootCanvas.scaleFactor) : 1f;
    }

    private string Colorize(string text)
    {
        if (string.IsNullOrEmpty(text) || keywords == null || keywords.Count == 0) return text;
        if (keywordRegex == null)
        {
            keywordHex = new Dictionary<string, string>();
            var list = new List<string>();
            foreach (var k in keywords)
            {
                if (k == null || string.IsNullOrEmpty(k.keyword) || keywordHex.ContainsKey(k.keyword)) continue;
                keywordHex[k.keyword] = ColorUtility.ToHtmlStringRGB(k.color);
                list.Add(k.keyword);
            }
            if (list.Count == 0) return text;
            list.Sort((a, b) => b.Length.CompareTo(a.Length));
            var parts = new List<string>();
            foreach (var w in list) parts.Add(Regex.Escape(w));
            keywordRegex = new Regex(string.Join("|", parts));
        }
        return keywordRegex.Replace(text, m => keywordHex.TryGetValue(m.Value, out string h) ? $"<color=#{h}>{m.Value}</color>" : m.Value);
    }

    private void Update()
    {
        // マウスのホイールで本文をスクロール（スクロールできる時だけ）
        if (maxScroll > 0f)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) { scroll -= wheel * wheelScrollAmount; ApplyScroll(); }
        }
        if (transitionT < 0f || contentRect == null) return;
        transitionT += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(transitionT / Mathf.Max(0.01f, transitionSeconds));
        float e = 1f - (1f - k) * (1f - k);
        float dir = transitionDir == 0 ? 1f : transitionDir;
        contentRect.anchoredPosition = contentBasePos + new Vector2((1f - e) * transitionSlide * dir, 0f);
        if (contentGroup != null) contentGroup.alpha = e;
        if (k >= 1f) { transitionT = -1f; transitionDir = 0; }
    }

    // ---------- スワイプ ----------
    // 縦に動かしたら本文のスクロール（スクロールできる時だけ）、横に動かしたらページ送り
    private float dragDy;
    private int dragMode; // 0＝まだ決めていない 1＝ページ送り 2＝スクロール
    public void OnBeginDrag(PointerEventData e) { dragDx = 0f; dragDy = 0f; dragMode = 0; }
    public void OnDrag(PointerEventData e)
    {
        dragDx += e.delta.x;
        dragDy += e.delta.y;
        if (dragMode == 0 && (Mathf.Abs(dragDx) > dragDirectionThreshold || Mathf.Abs(dragDy) > dragDirectionThreshold))
            dragMode = (Mathf.Abs(dragDy) > Mathf.Abs(dragDx) && maxScroll > 0f) ? 2 : 1;
        if (dragMode == 2)
        {
            scroll += e.delta.y / CanvasScale(); // 上へなぞると本文が上へ動く（下の文が見える）
            ApplyScroll();
        }
    }
    public void OnEndDrag(PointerEventData e)
    {
        if (dragMode == 2) return;
        if (Mathf.Abs(dragDx) < swipeThreshold) return;
        Go(dragDx < 0f ? 1 : -1); // 左へスワイプ＝次のページ
    }

#if UNITY_EDITOR
    public List<KeywordColor> EditorKeywords => keywords;
    public void EditorWireScroll(RectTransform viewport, Image thumb) { bodyViewport = viewport; scrollThumb = thumb; }
#endif
}
