using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UIのImage等に左→右のグラデーションを付ける（頂点カラーを書き換えるだけの軽い処理）。
/// SoundPanelFXのゲージの塗りで使う。shimmer=ONで光がゆっくり左右に流れる
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UIHorizontalGradient : BaseMeshEffect
{
    public Color colorLeft = new Color(0.3f, 0.95f, 1f, 1f);
    public Color colorRight = new Color(0.75f, 0.4f, 1f, 1f);
    [Tooltip("ONの時、明るい帯がゆっくり左右に流れる（実時間で動くので中断中も動く）")]
    public bool shimmer = true;
    [Range(0f, 1f)] public float shimmerStrength = 0.35f;
    public float shimmerSpeed = 0.8f;

    private static readonly List<UIVertex> verts = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        Rect r = graphic.rectTransform.rect;
        if (r.width <= 0.01f) return;
        verts.Clear();
        vh.GetUIVertexStream(verts);
        float band = shimmer ? Mathf.Repeat(Time.unscaledTime * shimmerSpeed, 1.6f) - 0.3f : -10f;
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            float t = Mathf.Clamp01((v.position.x - r.xMin) / r.width);
            Color c = Color.Lerp(colorLeft, colorRight, t);
            if (shimmer)
            {
                float k = Mathf.Clamp01(1f - Mathf.Abs(t - band) / 0.18f) * shimmerStrength;
                c = Color.Lerp(c, Color.white, k);
            }
            Color baseC = v.color;
            v.color = new Color(c.r * baseC.r, c.g * baseC.g, c.b * baseC.b, c.a * baseC.a);
            verts[i] = v;
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    private void Update()
    {
        if (shimmer && graphic != null) graphic.SetVerticesDirty();
    }
}
