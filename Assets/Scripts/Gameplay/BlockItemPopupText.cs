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

    private Vector3 baseScale = Vector3.one;
    private Color baseLabelColor = Color.white;
    private System.Action<BlockItemPopupText> onFinished;

    private static readonly Color ColorGold = new Color(1f, 0.85f, 0f);
    private static readonly Color ColorLife = new Color(1f, 0.31f, 0.31f);

    public void Show(BlockItem.ItemType type, int amount, Sprite icon)
    {
        Show(type, amount, icon, false, null);
    }

    /// <param name="isCircle">円で取った時true（大きめに出す）</param>
    /// <param name="finished">表示が終わった時に呼ぶ（使い回し用。nullなら従来どおりDestroy）</param>
    public void Show(BlockItem.ItemType type, int amount, Sprite icon, bool isCircle, System.Action<BlockItemPopupText> finished)
    {
        StopAllCoroutines();
        onFinished = finished;
        bool isGold = type == BlockItem.ItemType.Gold;
        Vector2 iconOfs = isGold ? goldIconOffset : lifeIconOffset;
        Vector2 labelOfs = isGold ? goldLabelOffset : lifeLabelOffset;

        if (iconRenderer != null)
        {
            iconRenderer.sprite = icon;
            iconRenderer.transform.localPosition = new Vector3(iconOfs.x, iconOfs.y, 0f);
        }

        if (label != null)
        {
            // worldScaleが異なっても視覚的なフォントサイズを統一するため逆数で補正
            float scale = transform.localScale.x > 0.0001f ? transform.localScale.x : 1f;
            label.fontSize = fontSize / scale;
            label.text = $"+{amount}";
            label.color = isGold ? ColorGold : ColorLife;
            baseLabelColor = label.color;
            label.transform.localPosition = new Vector3(labelOfs.x, labelOfs.y, 0f);
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
            transform.position += Vector3.up * riseSpeed * Time.deltaTime;

            float alpha;
            if (elapsed < fadeInDuration)
                alpha = elapsed / fadeInDuration;
            else if (elapsed < fadeInDuration + displayDuration)
                alpha = 1f;
            else
                alpha = 1f - (elapsed - fadeInDuration - displayDuration) / fadeOutDuration;

            // ⑨ 弾む・白く光る
            float pk = punchDuration > 0f ? Mathf.Clamp01(elapsed / punchDuration) : 1f;
            float punch = Mathf.Lerp(punchScale, 1f, 1f - (1f - pk) * (1f - pk));
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
