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
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = FormatBody(body);

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

    /// <summary>行頭の「・」「•」を色付きの点にし、2行目以降の折り返しも本文の頭に揃える。キーワードに色を付ける</summary>
    private string FormatBody(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;
        string hex = ColorUtility.ToHtmlStringRGB(bulletColor);
        var sb = new StringBuilder();
        string[] lines = body.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (i > 0) sb.Append('\n');
            if (line.StartsWith("・") || line.StartsWith("•"))
            {
                string rest = line.Substring(1).TrimStart();
                sb.Append($"<color=#{hex}>{line[0]}</color><indent={bulletIndent}>{Colorize(rest)}</indent>");
            }
            else sb.Append(Colorize(line));
        }
        return sb.ToString();
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
    public void OnBeginDrag(PointerEventData e) { dragDx = 0f; }
    public void OnDrag(PointerEventData e) { dragDx += e.delta.x; }
    public void OnEndDrag(PointerEventData e)
    {
        if (Mathf.Abs(dragDx) < swipeThreshold) return;
        Go(dragDx < 0f ? 1 : -1); // 左へスワイプ＝次のページ
    }

#if UNITY_EDITOR
    public List<KeywordColor> EditorKeywords => keywords;
#endif
}
