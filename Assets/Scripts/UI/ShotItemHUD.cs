using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 弾の性質を変えるアイテム（スパイラルショット等）の残り回数の表示（2026/11/11）。画面左上・ゴールド表示（GoldBar）の子。
/// ・取った時：アイコンと「x3」が弾むように出る（別の種類に上書きした時も同じ動きで入れ替わる）
/// ・1回使うたび：アイコンと数字が一瞬弾んで数字が減る
/// ・残り1：数字の色を変えて「最後の1発」と分かるようにする
/// ・使い切った時：小さくなって消える
/// ・横の位置は、ゴールドの数字の右端に合わせて動く（ゴールドの桁が増えても重ならない）
/// 作成は Tools/アイテム/「弾を変えるアイテムの表示を作る（05_Gameを開いて実行）」。位置・大きさ・色はPlay前にInspectorで調整できる
/// </summary>
[ExecuteAlways]
public class ShotItemHUD : MonoBehaviour
{
    [Header("部品（Setupツールが入れる）")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countText;
    [Tooltip("横の位置を合わせるゴールドの数字（GoldBar の文字）")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("アイコンの画像")]
    [SerializeField] private Sprite spiralIcon;
    [SerializeField] private Sprite blastIcon;
    [SerializeField] private Sprite splitIcon;

    [Header("位置")]
    [Tooltip("ON：ゴールドの数字の右端から Gap From Gold Text だけ離して置く。OFF：この GameObject の位置のまま")]
    [SerializeField] private bool followGoldText = true;
    [Tooltip("ゴールドの数字の右端からのすき間（画面のピクセル。Canvasの拡大率に合わせる）")]
    [SerializeField] private float gapFromGoldText = 24f;

    [Header("アイコンと数字の並び")]
    [Tooltip("アイコンの高さ（Canvasの単位）。横幅は画像の縦横の比から決める")]
    [Min(1f)] [SerializeField] private float iconHeight = 40f;
    [Tooltip("アイコンと数字のすき間（Canvasの単位）")]
    [SerializeField] private float iconTextGap = 6f;

    [Header("数字")]
    [Tooltip("数字の前に付ける文字（例：x → x3）")]
    [SerializeField] private string countPrefix = "x";
    [SerializeField] private Color countColor = Color.white;
    [Tooltip("残り1の時の数字の色")]
    [SerializeField] private Color lastOneColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("動き（秒は実時間）")]
    [Tooltip("出る時・入れ替わる時の時間")]
    [Min(0.01f)] [SerializeField] private float appearSeconds = 0.3f;
    [Tooltip("出る時に一番大きくなる倍率")]
    [Min(1f)] [SerializeField] private float appearPeakScale = 1.35f;
    [Tooltip("1回使った時に弾む時間")]
    [Min(0.01f)] [SerializeField] private float punchSeconds = 0.22f;
    [Min(1f)] [SerializeField] private float punchScale = 1.3f;
    [Tooltip("使い切って消える時間")]
    [Min(0.01f)] [SerializeField] private float hideSeconds = 0.3f;

    [Header("Play前の確認")]
    [Tooltip("ON：Play前のシーンで、スパイラルショットのアイコンと「x3」を表示して位置を確認できる（Play中は取った時だけ表示）")]
    [SerializeField] private bool previewInEditor = true;

    private RectTransform rt;
    private enum Anim { None, Appear, Punch, Hide }
    private Anim anim = Anim.None;
    private float animTime;
    private bool visible;

    private void Awake()
    {
        rt = transform as RectTransform;
        if (!Application.isPlaying) return;
        if (countText != null) GameNumberFont.Apply(countText);
        SetVisible(false);
        visible = false;
    }

    private Sprite IconOf(ShotItemType t)
    {
        switch (t)
        {
            case ShotItemType.Spiral: return spiralIcon;
            case ShotItemType.Blast: return blastIcon;
            case ShotItemType.Split: return splitIcon;
        }
        return null;
    }

    private void SetVisible(bool v)
    {
        if (icon != null && icon.gameObject.activeSelf != v) icon.gameObject.SetActive(v);
        if (countText != null && countText.gameObject.activeSelf != v) countText.gameObject.SetActive(v);
    }

    /// <summary>アイコンの画像を入れて、画像の縦横の比に合わせて横幅を決め、数字をアイコンの右に並べる</summary>
    private void SetIcon(Sprite s)
    {
        if (icon == null) return;
        icon.sprite = s;
        icon.preserveAspect = false; // ★sizeDeltaで大きさを決める（preserveAspect=trueだとsizeDeltaが効かない）
        float aspect = s != null && s.rect.height > 0.01f ? s.rect.width / s.rect.height : 1f;
        float w = iconHeight * aspect;
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = Vector2.zero;
        irt.sizeDelta = new Vector2(w, iconHeight);
        if (countText != null)
        {
            var crt = countText.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.anchoredPosition = new Vector2(w + iconTextGap, 0f);
        }
    }

    private void SetCountText(int count)
    {
        if (countText == null) return;
        countText.text = countPrefix + count;
        countText.color = count == 1 ? lastOneColor : countColor;
    }

    /// <summary>取った時（ShotItemManager.Acquire）。overwrite＝別の種類を上書きした</summary>
    public void Show(ShotItemType type, int count, bool overwrite)
    {
        SetIcon(IconOf(type));
        SetCountText(count);
        SetVisible(true);
        visible = true;
        anim = Anim.Appear;
        animTime = 0f;
        ApplyScale(0f);
    }

    /// <summary>1回使った時</summary>
    public void SetCount(int count)
    {
        if (count <= 0) return; // 0の時は HideAnimated で消す
        SetCountText(count);
        anim = Anim.Punch;
        animTime = 0f;
    }

    /// <summary>使い切った時</summary>
    public void HideAnimated()
    {
        SetCountText(0);
        anim = Anim.Hide;
        animTime = 0f;
    }

    private void ApplyScale(float s)
    {
        if (rt != null) rt.localScale = Vector3.one * Mathf.Max(0f, s);
    }

    private void LateUpdate()
    {
        if (rt == null) rt = transform as RectTransform;
        FollowGold();

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            UpdateEditorPreview();
#endif
            return;
        }
        if (!visible || anim == Anim.None) return;

        animTime += Time.unscaledDeltaTime;
        switch (anim)
        {
            case Anim.Appear:
            {
                float t = Mathf.Clamp01(animTime / appearSeconds);
                // 0 → 一番大きく → 1
                float s = t < 0.6f ? Mathf.Lerp(0f, appearPeakScale, t / 0.6f) : Mathf.Lerp(appearPeakScale, 1f, (t - 0.6f) / 0.4f);
                ApplyScale(s);
                if (t >= 1f) { ApplyScale(1f); anim = Anim.None; }
                break;
            }
            case Anim.Punch:
            {
                float t = Mathf.Clamp01(animTime / punchSeconds);
                ApplyScale(Mathf.Lerp(punchScale, 1f, t));
                if (t >= 1f) { ApplyScale(1f); anim = Anim.None; }
                break;
            }
            case Anim.Hide:
            {
                float t = Mathf.Clamp01(animTime / hideSeconds);
                ApplyScale(Mathf.Lerp(1f, 0f, t));
                if (t >= 1f) { SetVisible(false); visible = false; ApplyScale(1f); anim = Anim.None; }
                break;
            }
        }
    }

    /// <summary>ゴールドの数字の右端に合わせて横の位置を動かす</summary>
    private void FollowGold()
    {
        if (!followGoldText || goldText == null || rt == null) return;
        Bounds tb = goldText.textBounds;
        if (tb.size.x <= 0.0001f) return; // まだ文字が組まれていない
        var gt = goldText.rectTransform;
        Vector3 rightWorld = gt.TransformPoint(new Vector3(tb.max.x, 0f, 0f));
        float scale = gt.lossyScale.x > 0.0001f ? gt.lossyScale.x : 1f;
        Vector3 p = rt.position;
        // この GameObject の左端（pivot.x=0 の前提）を、ゴールドの数字の右端＋すき間に置く
        p.x = rightWorld.x + gapFromGoldText * scale;
        rt.position = p;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null) UpdateEditorPreview(); };
    }

    private void UpdateEditorPreview()
    {
        SetVisible(previewInEditor);
        if (!previewInEditor) return;
        SetIcon(icon != null && icon.sprite != null ? icon.sprite : spiralIcon);
        SetCountText(3);
        ApplyScale(1f);
    }

    public void EditorWire(Image i, TextMeshProUGUI count, TextMeshProUGUI gold, Sprite spiral, Sprite blast = null, Sprite split = null)
    {
        icon = i;
        countText = count;
        goldText = gold;
        if (spiralIcon == null) spiralIcon = spiral;
        if (blastIcon == null) blastIcon = blast;
        if (splitIcon == null) splitIcon = split;
    }
#endif
}
