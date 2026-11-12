using Game.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ジェム画面・フード画面の左のスキルHUD（GemSkillPreviewHUD）で、スキルの説明を出す小さな窓（2026/11/12）。
/// ・PC：スキルにカーソルを乗せると出て、外すと消える
/// ・スマホ：スキルをタップすると出る。同じスキルをもう一度タップ、またはほかの場所をタップで消える。別のスキルをタップで切り替わる
/// ・出る場所はHUDのすぐ右（高さは押したスキルの横にそろえ、HUDの上下からはみ出さないよう内側に寄せる）
/// ・中身：アイコン・スキル名・説明・どこからいくつ上がっているか（ジェム・フード・パッシブ）
/// ゲーム中のスキルHUDは今まで通り SkillTooltip（カーソルを乗せた時だけ）を使う。この窓はつながっているHUDのカードだけで使う
/// 作成はメニュー「Tools/スキルHUD/ジェム・フード画面のスキル説明を作る」
/// </summary>
public class SkillInfoPopupUI : MonoBehaviour
{
    [Header("部品（メニューで設定）")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI breakdownText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("位置")]
    [Tooltip("HUD（この窓の親）の右端からの隙間")]
    [SerializeField] private float gapFromHud = 16f;
    [Tooltip("HUDの上下の端から、これ以上は窓を寄せない余白")]
    [SerializeField] private float edgeMargin = 20f;

    [Header("内訳の文字（{0}＝上がる数）")]
    [SerializeField] private string gemFormatJa = "ジェム +{0}";
    [SerializeField] private string foodFormatJa = "フード +{0}";
    [SerializeField] private string passiveFormatJa = "パッシブ +{0}";
    [SerializeField] private string noneJa = "ジェム・フード・パッシブでの強化なし";
    [SerializeField] private string gemFormatEn = "Gem +{0}";
    [SerializeField] private string foodFormatEn = "Food +{0}";
    [SerializeField] private string passiveFormatEn = "Passive +{0}";
    [SerializeField] private string noneEn = "No boosts from Gems, Food, or Passives";
    [Tooltip("内訳の色（HUDのマス目の色とそろえる：ジェム＝オレンジ・フード＝紫・パッシブ＝ライム）")]
    [SerializeField] private Color gemColor = new Color(1f, 0.55f, 0.1f, 1f);
    [SerializeField] private Color foodColor = new Color(0.8f, 0.35f, 1f, 1f);
    [SerializeField] private Color passiveColor = new Color(0.65f, 1f, 0.2f, 1f);
    [SerializeField] private Color noneColor = new Color(0.7f, 0.7f, 0.75f, 1f);

    [Header("HUDの下の一言（押せることを知らせる）")]
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private string hintJa = "スキルをタッチで説明";
    [SerializeField] private string hintEn = "Tap a skill for details";

    private SkillHUDCardUI current;
    private bool pinned;              // タップで出した（ほかの場所をタップするまで消さない）
    private bool cardPressedThisFrame;

    private void Awake()
    {
        Hide();
    }

    [Header("描く順番")]
    [Tooltip("説明の窓を、ジェムのパネルなど画面のほかの物より手前に出す順番（画面の Canvas は0）")]
    [SerializeField] private int sortingOrder = 200;

    private void OnEnable()
    {
        // ジェムのパネルより手前に出す（この入れ物の Canvas を、親とは別の描く順番にする）
        var c = GetComponent<Canvas>();
        if (c != null) { c.overrideSorting = true; c.sortingOrder = sortingOrder; }

        if (hintText != null)
            hintText.text = Game.Localization.LocalizationManager.CurrentLanguageStatic == Game.Localization.GameLanguage.English ? hintEn : hintJa;
    }

    private void OnDisable()
    {
        Hide();
    }

    /// <summary>カーソルが乗った（PC）。タップで出している間は切り替えない</summary>
    public void OnCardHover(SkillHUDCardUI card)
    {
        if (pinned) return;
        ShowFor(card);
    }

    /// <summary>カーソルが外れた（PC）</summary>
    public void OnCardHoverExit(SkillHUDCardUI card)
    {
        if (pinned || current != card) return;
        Hide();
    }

    /// <summary>タップ・クリックした。同じスキルなら閉じ、別のスキルなら切り替える</summary>
    public void OnCardClick(SkillHUDCardUI card)
    {
        if (pinned && current == card) { Hide(); return; }
        ShowFor(card);
        pinned = true;
    }

    /// <summary>カードが押された（同じフレームの「ほかの場所をタップしたら閉じる」を止める）</summary>
    public void OnCardPressed() => cardPressedThisFrame = true;

    private void LateUpdate()
    {
        // ほかの場所をタップ・クリックしたら閉じる（カードを押した時は OnCardClick が決める）
        if (current != null && pinned && Input.GetMouseButtonDown(0) && !cardPressedThisFrame) Hide();
        cardPressedThisFrame = false;
    }

    private void ShowFor(SkillHUDCardUI card)
    {
        if (card == null || card.Skill == null) return;
        current = card;
        bool en = Game.Localization.LocalizationManager.CurrentLanguageStatic == Game.Localization.GameLanguage.English;
        var skill = card.Skill;

        if (iconImage != null) { iconImage.sprite = skill.icon; iconImage.enabled = skill.icon != null; iconImage.preserveAspect = true; }
        if (nameText != null) nameText.text = skill.GetLocalizedName();
        if (descriptionText != null) descriptionText.text = skill.GetLocalizedDescriptionTemplate().Replace("\n", "");
        if (breakdownText != null)
        {
            var sb = new System.Text.StringBuilder();
            void Add(int n, string fmt, Color c)
            {
                if (n <= 0) return;
                if (sb.Length > 0) sb.Append("　");
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{string.Format(fmt, n)}</color>");
            }
            Add(card.GemLevel, en ? gemFormatEn : gemFormatJa, gemColor);
            Add(card.ShopLevel, en ? foodFormatEn : foodFormatJa, foodColor);
            Add(card.PassiveLevel, en ? passiveFormatEn : passiveFormatJa, passiveColor);
            if (sb.Length == 0) sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(noneColor)}>{(en ? noneEn : noneJa)}</color>");
            breakdownText.text = sb.ToString();
        }

        if (panel != null)
        {
            panel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            PlaceBeside(card);
        }
        if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = false; }
    }

    /// <summary>HUDの右に置き、高さはカードの横にそろえる（HUDの上下からはみ出さないよう内側へ）</summary>
    private void PlaceBeside(SkillHUDCardUI card)
    {
        var parent = panel.parent as RectTransform;
        if (parent == null) return;
        Rect pr = parent.rect;
        float y = parent.InverseTransformPoint(card.transform.position).y;
        float h = panel.rect.height * panel.localScale.y;
        float minY = pr.yMin + edgeMargin + h * 0.5f, maxY = pr.yMax - edgeMargin - h * 0.5f;
        if (minY <= maxY) y = Mathf.Clamp(y, minY, maxY);
        // 窓の基準点は左の真ん中（pivot 0, 0.5）
        panel.localPosition = new Vector3(pr.xMax + gapFromHud, y, 0f);
    }

    public void Hide()
    {
        current = null;
        pinned = false;
        if (panel != null) panel.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    public void EditorWire(RectTransform p, Image icon, TextMeshProUGUI n, TextMeshProUGUI d, TextMeshProUGUI b, CanvasGroup cg, TextMeshProUGUI hint)
    {
        panel = p; iconImage = icon; nameText = n; descriptionText = d; breakdownText = b; canvasGroup = cg; hintText = hint;
    }
#endif
}
