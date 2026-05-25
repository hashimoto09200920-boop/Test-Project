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

    private static readonly Color ColorGold = new Color(1f, 0.85f, 0f);
    private static readonly Color ColorLife = new Color(1f, 0.31f, 0.31f);

    public void Show(BlockItem.ItemType type, int amount, Sprite icon)
    {
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
            label.transform.localPosition = new Vector3(labelOfs.x, labelOfs.y, 0f);
        }

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

            SetAlpha(Mathf.Clamp01(alpha));
            yield return null;
        }

        Destroy(gameObject);
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
