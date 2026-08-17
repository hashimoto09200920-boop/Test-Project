using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI
{
    /// <summary>
    /// 03_AreaSelectの「AREA SELECT」タイトル文字を、
    /// レタースペーシング＋グラデーション＋左右の光る飾り線で装飾するEditor専用スタイラー。
    /// TitleTextに直接アタッチして使う想定（Title Textを空にすれば自分自身のTextMeshProUGUIを使う）。
    /// 飾り線・光点はSoftGlowCircle＋通常のアルファブレンドで構成する（カスタムシェーダーや加算合成は、
    /// 暗い背景では小さく/薄く見えてしまい実質見えなくなるため採用していない）。
    /// </summary>
    [DisallowMultipleComponent]
    public class AreaSelectTitleStyler : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("装飾対象のタイトルテキスト。空なら自分自身のTextMeshProUGUIを使う")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Text Style")]
        [Tooltip("文字間隔")]
        [SerializeField] private float characterSpacing = 12f;
        [SerializeField] private Color gradientTop = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color gradientBottom = new Color(0.34f, 0.89f, 1f, 1f);

        [Header("Accent Lines")]
        [Tooltip("飾り線・光点に使うSprite。AreaConstellationFXの「Generate Glow Sprite」で生成済みのSoftGlowCircleを指定する")]
        [SerializeField] private Sprite glowSprite;
        [SerializeField] private Color lineColor = new Color(0.34f, 0.89f, 1f, 1f);
        [SerializeField] private float lineWidth = 80f;
        [Tooltip("線の太さ。まずは確実に見えるよう大きめにしてある（見えたら小さくしていく）")]
        [SerializeField] private float lineThickness = 40f;
        [Tooltip("文字の端から線までの隙間")]
        [SerializeField] private float lineGapFromText = 10f;
        [SerializeField] private float dotSize = 40f;

#if UNITY_EDITOR
        [ContextMenu("Apply Title Style (レタースペーシング＋グラデ＋飾り線を適用)")]
        private void ApplyStyle()
        {
            var text = titleText != null ? titleText : GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                Debug.LogError("[AreaSelectTitleStyler] Title Textが見つかりません。");
                return;
            }

            var titleRt = text.rectTransform;
            var parent = titleRt.parent as RectTransform;
            if (parent == null)
            {
                Debug.LogError("[AreaSelectTitleStyler] タイトルの親RectTransformが見つかりません。");
                return;
            }

            // 1) 文字間隔とグラデーション
            // ★TMPのColor(全体色)はグラデーションに掛け算で乗るため、白にリセットしてから設定する
            text.color = Color.white;
            text.characterSpacing = characterSpacing;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(gradientTop, gradientTop, gradientBottom, gradientBottom);

            // 2) テキストの実際の描画幅を取得（飾り線をこの外側に配置するため）
            text.ForceMeshUpdate();
            float halfTextWidth = text.textBounds.size.x * 0.5f;
            Vector2 titlePos = titleRt.anchoredPosition;

            // ★CanvasScalerがMatch=Height（縦基準）のため、実際にGame viewで見える横幅は
            //   画面アスペクト比によって変わる。固定pxで配置すると狭い画面で見切れて消えるので、
            //   親（AreaPanel）の実際の幅からはみ出さない安全な長さに動的でクランプする。
            float parentHalfWidth = parent.rect.width * 0.5f;
            const float safetyMargin = 12f;
            float maxReachFromCenter = Mathf.Max(0f, parentHalfWidth - safetyMargin);
            float availableForLine = Mathf.Max(4f, maxReachFromCenter - halfTextWidth - lineGapFromText);
            float actualLineWidth = Mathf.Min(lineWidth, availableForLine);

            Debug.Log($"[AreaSelectTitleStyler] halfTextWidth={halfTextWidth:F1}, parentHalfWidth={parentHalfWidth:F1}, actualLineWidth={actualLineWidth:F1} (設定値={lineWidth:F1})");

            // 3) 左右の飾り線（SoftGlowCircleを横長に引き伸ばして、柔らかく光る帯として使う）
            float leftCenterX = titlePos.x - halfTextWidth - lineGapFromText - actualLineWidth * 0.5f;
            BuildAccentLine(parent, "TitleAccentLineLeft", titleRt, leftCenterX, titlePos.y, actualLineWidth);

            float rightCenterX = titlePos.x + halfTextWidth + lineGapFromText + actualLineWidth * 0.5f;
            BuildAccentLine(parent, "TitleAccentLineRight", titleRt, rightCenterX, titlePos.y, actualLineWidth);

            // 4) 文字側の端に光点を添える
            BuildAccentDot(parent, "TitleAccentDotLeft", titleRt, titlePos.x - halfTextWidth - lineGapFromText, titlePos.y);
            BuildAccentDot(parent, "TitleAccentDotRight", titleRt, titlePos.x + halfTextWidth + lineGapFromText, titlePos.y);

            EditorUtility.SetDirty(text);
            EditorUtility.SetDirty(parent);
            Debug.Log("[AreaSelectTitleStyler] タイトルの装飾を適用しました。");
        }

        private void BuildAccentLine(RectTransform parent, string name, RectTransform sibling, float centerX, float centerY, float width)
        {
            var rt = FindOrCreateChild(parent, name);
            rt.anchorMin = sibling.anchorMin;
            rt.anchorMax = sibling.anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, lineThickness);
            rt.anchoredPosition = new Vector2(centerX, centerY);
            rt.localScale = Vector3.one;

            var img = GetOrAddImage(rt);
            // ★加算合成は暗い背景では小さく/薄く見えてしまうため使わず、通常のアルファブレンドにする
            if (glowSprite != null) img.sprite = glowSprite;
            img.material = null;
            img.color = lineColor;
            img.raycastTarget = false;
        }

        private void BuildAccentDot(RectTransform parent, string name, RectTransform sibling, float centerX, float centerY)
        {
            var rt = FindOrCreateChild(parent, name);
            rt.anchorMin = sibling.anchorMin;
            rt.anchorMax = sibling.anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(dotSize, dotSize);
            rt.anchoredPosition = new Vector2(centerX, centerY);
            rt.localScale = Vector3.one;

            var img = GetOrAddImage(rt);
            if (glowSprite != null) img.sprite = glowSprite;
            img.material = null;
            img.color = new Color(lineColor.r, lineColor.g, lineColor.b, 1f);
            img.raycastTarget = false;
        }

        /// <summary>
        /// ★以前のバージョン（カスタムシェーダー版）で作った同名オブジェクトが残っていて、
        /// 古いMaterial/Sprite設定のまま使い回されている可能性を排除するため、
        /// 毎回いったん破棄してから完全に新規作成する（再利用しない）。
        /// </summary>
        private static RectTransform FindOrCreateChild(RectTransform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);
            }
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image GetOrAddImage(RectTransform rt)
        {
            var img = rt.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            return img;
        }
#endif
    }
}
