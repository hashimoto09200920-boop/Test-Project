using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// ブロックアイテム収集時のポップアップ（アイコン + "+N" テキスト）
/// Prefab構成: ルートにこのコンポーネント、子に SpriteRenderer (Icon) と TextMeshPro (Label)
/// </summary>
public class BlockItemPopupText : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private TextMeshPro label;

    [Header("Font")]
    [SerializeField] private float fontSize = 36f;

    [Header("Layout (Gold)")]
    [SerializeField] private Vector2 goldIconOffset = new Vector2(-0.5f, 0f);
    [SerializeField] private Vector2 goldLabelOffset = new Vector2(0.5f, 0f);

    [Header("Layout (Life)")]
    [SerializeField] private Vector2 lifeIconOffset = new Vector2(-0.5f, 0f);
    [SerializeField] private Vector2 lifeLabelOffset = new Vector2(0.5f, 0f);

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float riseSpeed = 0.6f;

    [Header("⑨ 強調（出た瞬間に弾んで白く光る）")]
    [Tooltip("出た瞬間の大きさの倍率（1で弾まない）")]
    [SerializeField] private float punchScale = 1.6f;
    [Tooltip("弾んで元の大きさに戻るまでの秒数")]
    [SerializeField] private float punchDuration = 0.18f;
    [Tooltip("円で取った時の大きさの倍率（ずっとこの大きさ）")]
    [SerializeField] private float circleScale = 1.3f;
    [Tooltip("出た瞬間に白く光っている秒数（0で光らない）")]
    [SerializeField] private float flashDuration = 0.15f;

    [Header("A 文字の色と縁取り・光（マテリアルはメニュー「Tools/アイテム演出/…」で作成・設定）")]
    [SerializeField] private Color goldTextColor = new Color(1f, 0.88f, 0.2f, 1f);
    [SerializeField] private Color lifeTextColor = new Color(1f, 0.45f, 0.62f, 1f);
    [Tooltip("Goldの数字のマテリアル（濃いオレンジの縁取り＋黄色い光）。未設定なら文字の元のマテリアルのまま")]
    [SerializeField] private Material goldMaterial;
    [Tooltip("Lifeの数字のマテリアル（白い縁取り＋ピンクの光）。未設定なら文字の元のマテリアルのまま")]
    [SerializeField] private Material lifeMaterial;

    [Header("文字のグラデーション（上が明るく下が濃い）")]
    [SerializeField] private bool useGradient = true;
    [SerializeField] private Color goldTop = new Color(1f, 0.98f, 0.62f, 1f);
    [SerializeField] private Color goldBottom = new Color(1f, 0.6f, 0.05f, 1f);
    [SerializeField] private Color lifeTop = new Color(1f, 0.85f, 0.92f, 1f);
    [SerializeField] private Color lifeBottom = new Color(1f, 0.3f, 0.52f, 1f);

    [Header("並べ方（アイコンの右から数字を始め、アイコン＋数字のまとまりを中央にそろえる）")]
    [Tooltip("ON：自動で並べる（Layoutの各Offsetは上下位置だけ使う）。OFF：従来どおりOffsetの位置にそのまま置く")]
    [SerializeField] private bool autoLayout = true;
    [Tooltip("アイコンと数字の間のすき間")]
    [SerializeField] private float iconLabelGap = 0.4f;

    [Header("B スタンプの動き（大きく出て一瞬で縮んで止まり、その後に上へ流れる）")]
    [Tooltip("ON：スタンプの動き（Punch Scaleから少し縮みすぎて元の大きさ）。OFF：従来の弾み方")]
    [SerializeField] private bool stampMotion = true;
    [Tooltip("出てから上へ流れ始めるまで、その場で止まっている秒数")]
    [SerializeField] private float stampHoldSeconds = 0.25f;

    [Header("C 数字が増えていく")]
    [Tooltip("「+0」から「+N」まで数字が増えていく秒数（0で最初からNを表示）")]
    [SerializeField] private float countUpSeconds = 0.3f;

    [Header("D アイコンの大きさ")]
    [Tooltip("アイコン（コイン・ハート）の大きさの倍率。大きくした分は数字と重ならないよう左へずらす")]
    [SerializeField] private float iconScaleMultiplier = 1.5f;

    private Vector3 baseScale = Vector3.one;
    private Color baseLabelColor = Color.white;
    private System.Action<BlockItemPopupText> onFinished;
    private Vector3 iconBaseLocalScale = Vector3.one;
    private bool iconBaseCaptured;
    private Material labelDefaultMaterial;
    private int countTarget;
    private int countShown = -1;

    private void CaptureDefaults()
    {
        if (iconBaseCaptured) return;
        iconBaseCaptured = true;
        if (iconRenderer != null) iconBaseLocalScale = iconRenderer.transform.localScale;
        if (label != null) labelDefaultMaterial = label.fontSharedMaterial;
    }

    public void Show(BlockItem.ItemType type, int amount, Sprite icon)
    {
        Show(type, amount, icon, false, null);
    }

    /// <param name="isCircle">円で取った時true（大きめに出す）</param>
    /// <param name="finished">表示が終わった時に呼ぶ（使い回し用。nullなら従来どおりDestroy）</param>
    public void Show(BlockItem.ItemType type, int amount, Sprite icon, bool isCircle, System.Action<BlockItemPopupText> finished)
    {
        StopAllCoroutines();
        CaptureDefaults();
        onFinished = finished;
        bool isGold = type == BlockItem.ItemType.Gold;
        Vector2 iconOfs = isGold ? goldIconOffset : lifeIconOffset;
        Vector2 labelOfs = isGold ? goldLabelOffset : lifeLabelOffset;

        if (iconRenderer != null)
        {
            iconRenderer.sprite = icon;
            // D：アイコンを大きく。大きくなった分（片側）だけ左へずらして数字と重ならないようにする
            float mul = Mathf.Max(0.01f, iconScaleMultiplier);
            iconRenderer.transform.localScale = iconBaseLocalScale * mul;
            float extraX = icon != null ? (mul - 1f) * icon.bounds.extents.x * Mathf.Abs(iconBaseLocalScale.x) : 0f;
            iconRenderer.transform.localPosition = new Vector3(iconOfs.x - extraX, iconOfs.y, 0f);
        }

        if (label != null)
        {
            // worldScaleが異なっても視覚的なフォントサイズを統一するため逆数で補正
            float scale = transform.localScale.x > 0.0001f ? transform.localScale.x : 1f;
            label.fontSize = fontSize / scale;
            // C：数字が増えていく（AnimateRoutineで更新）。0秒なら最初から最終値
            countTarget = amount;
            countShown = countUpSeconds > 0f ? 0 : amount;
            label.text = "+" + countShown.ToString();
            // A：色と縁取り・光
            label.color = isGold ? goldTextColor : lifeTextColor;
            Material m = isGold ? goldMaterial : lifeMaterial;
            label.fontSharedMaterial = m != null ? m : labelDefaultMaterial;
            baseLabelColor = label.color;
            label.transform.localPosition = new Vector3(labelOfs.x, labelOfs.y, 0f);

            // 文字のグラデーション（色は頂点に付けるので、文字色は白にしてフェードのアルファだけ使う）
            label.enableVertexGradient = useGradient;
            if (useGradient)
            {
                Color top = isGold ? goldTop : lifeTop, bottom = isGold ? goldBottom : lifeBottom;
                label.colorGradient = new VertexGradient(top, top, bottom, bottom);
                label.color = Color.white;
                baseLabelColor = Color.white;
            }

            // 並べ方：アイコンの右端から数字を始め、全体の幅の中央を0にそろえる（数字の幅は最終値「+N」で1回だけ測る）
            if (autoLayout)
            {
                RectTransform lrt = label.rectTransform;
                lrt.pivot = new Vector2(0f, 0.5f);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                float textW = label.GetPreferredValues("+" + amount.ToString()).x * Mathf.Abs(lrt.localScale.x);
                float iconW = 0f;
                if (iconRenderer != null && icon != null) iconW = icon.bounds.size.x * Mathf.Abs(iconRenderer.transform.localScale.x);
                float gap = iconW > 0f ? iconLabelGap : 0f;
                float left = -(iconW + gap + textW) * 0.5f;
                if (iconRenderer != null) iconRenderer.transform.localPosition = new Vector3(left + iconW * 0.5f, iconOfs.y, 0f);
                label.transform.localPosition = new Vector3(left + iconW + gap, labelOfs.y, 0f);
            }
        }

        baseScale = transform.localScale * (isCircle ? circleScale : 1f);
        transform.localScale = baseScale;
        SetAlpha(0f);
        StartCoroutine(AnimateRoutine());
    }

    private IEnumerator AnimateRoutine()
    {
        float elapsed = 0f;
        float totalDuration = fadeInDuration + displayDuration + fadeOutDuration;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            // B：スタンプの間はその場で止まり、その後に上へ流れる
            if (!stampMotion || elapsed >= stampHoldSeconds)
                transform.position += Vector3.up * riseSpeed * Time.deltaTime;

            // C：数字が増えていく（値が変わった時だけ文字を作り直す）
            if (label != null && countShown != countTarget)
            {
                int v = countUpSeconds > 0f ? Mathf.Min(countTarget, Mathf.CeilToInt(countTarget * Mathf.Clamp01(elapsed / countUpSeconds))) : countTarget;
                if (v != countShown) { countShown = v; label.text = "+" + v.ToString(); }
            }

            float alpha;
            if (elapsed < fadeInDuration)
                alpha = elapsed / fadeInDuration;
            else if (elapsed < fadeInDuration + displayDuration)
                alpha = 1f;
            else
                alpha = 1f - (elapsed - fadeInDuration - displayDuration) / fadeOutDuration;

            // ⑨ 弾む・白く光る
            float pk = punchDuration > 0f ? Mathf.Clamp01(elapsed / punchDuration) : 1f;
            float punch;
            if (stampMotion)
            {
                // B：大きく出る→少し縮みすぎる→元の大きさで止まる
                punch = pk < 0.7f
                    ? Mathf.Lerp(punchScale, 0.9f, 1f - (1f - pk / 0.7f) * (1f - pk / 0.7f))
                    : Mathf.Lerp(0.9f, 1f, (pk - 0.7f) / 0.3f);
                if (pk >= 1f) punch = 1f;
            }
            else
            {
                punch = Mathf.Lerp(punchScale, 1f, 1f - (1f - pk) * (1f - pk));
            }
            transform.localScale = baseScale * punch;
            if (label != null)
            {
                float fk = flashDuration > 0f ? Mathf.Clamp01(elapsed / flashDuration) : 1f;
                Color lc = Color.Lerp(Color.white, baseLabelColor, fk);
                lc.a = label.color.a;
                label.color = lc;
            }

            SetAlpha(Mathf.Clamp01(alpha));
            yield return null;
        }

        transform.localScale = baseScale;
        if (onFinished != null) onFinished(this);
        else Destroy(gameObject);
    }

    private void SetAlpha(float a)
    {
        if (iconRenderer != null)
        {
            Color c = iconRenderer.color;
            c.a = a;
            iconRenderer.color = c;
        }
        if (label != null)
        {
            Color c = label.color;
            c.a = a;
            label.color = c;
        }
    }
}
