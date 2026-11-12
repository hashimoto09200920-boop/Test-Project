using System.Collections.Generic;
using Game.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゴールドシューズの効果中の線ゲージの演出（2026/11/10）。PaddleCostBarUI と同じGameObjectに付ける。
/// ・効果中：白線・赤線のゲージ（中身の部分）からネオンの粒子が散る
/// ・切れた瞬間：粒子を最後に1回多めに散らす
/// 作成は Tools/アイテム/「ゴールドシューズの表示を作る（05_Gameを開いて実行）」
/// </summary>
public class GoldenShoesGaugeFX : MonoBehaviour
{
    [Header("参照（Setupツールが入れる）")]
    [Tooltip("WhiteBar/FillBar")]
    [SerializeField] private Image whiteFill;
    [Tooltip("RedBar/FillBar")]
    [SerializeField] private Image redFill;
    [Tooltip("粒子を置く入れ物（PaddleCostBarUI の一番手前の子）")]
    [SerializeField] private RectTransform particleLayer;
    [Tooltip("以前ゲージの近くに出していた文字（今は使わず、常に隠す。GameObjectごと消してもよい）")]
    [SerializeField] private TextMeshProUGUI pickupText;

    [Header("粒子（効果中）")]
    [Tooltip("粒子の画像（柔らかい丸）")]
    [SerializeField] private Sprite particleSprite;
    [Tooltip("ゲージ1本あたり1秒に出す数")]
    [Min(0f)] [SerializeField] private float neonEmitPerSecond = 40f; // 2026/11/10 少し増やした（以前の項目 Emit Per Second 26 とは別の項目）
    [Tooltip("大きさ（ピクセル）の最小・最大")]
    [SerializeField] private Vector2 sizeRange = new Vector2(5f, 12f);
    [Tooltip("出た瞬間の速さ（ピクセル/秒）の最小・最大。全方向へ散る")]
    [SerializeField] private Vector2 driftSpeedRange = new Vector2(12f, 32f); // 2026/11/10 速すぎたので遅くした（以前の項目 Speed Range 40〜120 とは別の項目）
    [Tooltip("消えるまでの時間（秒）")]
    [Min(0.05f)] [SerializeField] private float particleLife = 0.7f;
    [Tooltip("だんだん遅くなる強さ（大きいほど早く止まる。0で同じ速さのまま）。重さ（下に落ちる動き）は付けない＝動きをゆるやかにする")]
    [Min(0f)] [SerializeField] private float driftDamping = 1.2f;
    [Tooltip("粒子の色（ランダムに使う）")]
    [SerializeField] private Color[] particleColors =
    {
        new Color(1f, 0.85f, 0.25f, 1f), // 金
        new Color(0.3f, 1f, 1f, 1f),     // シアン
        new Color(1f, 0.35f, 1f, 1f),    // マゼンタ
    };
    [Tooltip("一度に出せる粒子の数の上限")]
    [Min(10)] [SerializeField] private int maxParticlesLimit = 220; // 取った瞬間の粒子のぶん増やした（以前の項目 Max Particles 120 とは別の項目）

    [Header("取った瞬間の粒子（ゲージの中から外へ勢いよくはじける）")]
    [Tooltip("ゲージ1本あたりの数")]
    [Min(0)] [SerializeField] private int startBurstCount = 36;
    [Tooltip("飛び出す速さ（ピクセル/秒）の最小・最大")]
    [SerializeField] private Vector2 startBurstSpeed = new Vector2(250f, 520f);
    [Tooltip("勢いが落ちる強さ（大きいほど早く止まる＝一瞬だけ勢いよく）")]
    [Min(0f)] [SerializeField] private float startBurstDamping = 6f;
    [Tooltip("大きさ（ピクセル）の最小・最大")]
    [SerializeField] private Vector2 startBurstSize = new Vector2(8f, 16f);
    [Tooltip("消えるまでの時間（秒）")]
    [Min(0.05f)] [SerializeField] private float startBurstLife = 0.55f;

    [Header("切れた時の粒子")]
    [Tooltip("ゲージ1本あたりの数")]
    [Min(0)] [SerializeField] private int endBurstCount = 30;
    [Tooltip("速さの倍率")]
    [Min(0.1f)] [SerializeField] private float endBurstSpeedMul = 2f;

    private class P { public Image img; public RectTransform rt; public Vector2 vel; public float age, life, size, damping; public Color color; }
    private readonly List<P> pool = new List<P>();
    private float emitAccWhite, emitAccRed;
    private readonly Vector3[] corners = new Vector3[4];

    private void Awake()
    {
        if (pickupText != null) pickupText.gameObject.SetActive(false); // ★以前ゲージの近くに出していた文字。今は取ったアイテムのそばに出す（BlockItemManager）ので常に隠す
    }

    /// <summary>止まっていない時の実時間（スローモーション中も普通の速さ。timeScaleが0の間は0）</summary>
    private static float RunDt => Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;

    private void Update()
    {
        float dt = RunDt;
        bool active = GoldenShoesBuff.Instance != null && GoldenShoesBuff.Instance.IsActive;

        if (active && dt > 0f && particleLayer != null && particleSprite != null)
        {
            emitAccWhite = Emit(whiteFill, emitAccWhite + neonEmitPerSecond * dt, 1f);
            emitAccRed = Emit(redFill, emitAccRed + neonEmitPerSecond * dt, 1f);
        }
        UpdateParticles(dt);
    }

    /// <summary>取った瞬間（GoldenShoesBuff.Activate）：横長のゲージ全体から、外側へ勢いよくはじけ飛ばす</summary>
    public void PlayStartBurst()
    {
        if (particleLayer == null || particleSprite == null) return;
        StartBurst(whiteFill);
        StartBurst(redFill);
    }

    private void StartBurst(Image fill)
    {
        if (fill == null || !fill.gameObject.activeInHierarchy || startBurstCount <= 0) return;
        // ゲージの外枠（中身が空でも枠の全体から出す）
        var rt = fill.rectTransform.parent as RectTransform;
        if (rt == null) rt = fill.rectTransform;
        rt.GetWorldCorners(corners);
        Vector2 bl = particleLayer.InverseTransformPoint(corners[0]);
        Vector2 tr = particleLayer.InverseTransformPoint(corners[2]);
        Vector2 center = (bl + tr) * 0.5f;
        float halfW = Mathf.Max(1f, (tr.x - bl.x) * 0.5f);
        for (int i = 0; i < startBurstCount; i++)
        {
            var p = Get();
            if (p == null) break;
            // ★横長のゲージ全体（左端〜右端）からまんべんなく出す。一点から出すと左右の端から何も出ていないように見える
            float x = Mathf.Lerp(bl.x, tr.x, (i + Random.Range(0f, 1f)) / startBurstCount);
            float y = Random.Range(bl.y, tr.y);
            Vector2 pos = new Vector2(x, y);
            // 向き：ゲージの中心の線から外へ（上側は上へ、下側は下へ。左右の端の方は横にも開く）
            float u = (x - center.x) / halfW;                 // -1（左端）〜1（右端）
            float side = y >= center.y ? 1f : -1f;
            if (Random.value < 0.15f) side = -side;           // 少しだけ逆側にも散らす
            Vector2 dir = new Vector2(u * 0.9f + Random.Range(-0.35f, 0.35f), side * Random.Range(0.6f, 1f)).normalized;
            p.vel = dir * Random.Range(startBurstSpeed.x, startBurstSpeed.y);
            p.damping = startBurstDamping;
            p.age = 0f;
            p.life = startBurstLife * Random.Range(0.8f, 1.2f);
            p.size = Random.Range(startBurstSize.x, startBurstSize.y);
            p.color = particleColors != null && particleColors.Length > 0 ? particleColors[Random.Range(0, particleColors.Length)] : Color.white;
            p.rt.anchoredPosition = pos;
            p.rt.sizeDelta = new Vector2(p.size, p.size);
            p.img.color = p.color;
            p.img.gameObject.SetActive(true);
        }
    }

    /// <summary>効果が切れた時（GoldenShoesBuff.End）</summary>
    public void PlayEndBurst()
    {
        if (particleLayer == null || particleSprite == null) return;
        Emit(whiteFill, endBurstCount, endBurstSpeedMul);
        Emit(redFill, endBurstCount, endBurstSpeedMul);
    }

    /// <summary>ゲージの中身の部分から count 個出す（端数は返す）</summary>
    private float Emit(Image fill, float count, float speedMul)
    {
        if (fill == null || !fill.gameObject.activeInHierarchy) return 0f;
        int n = Mathf.FloorToInt(count);
        if (n <= 0) return count;
        var rt = fill.rectTransform;
        rt.GetWorldCorners(corners);
        Vector2 bl = particleLayer.InverseTransformPoint(corners[0]);
        Vector2 tr = particleLayer.InverseTransformPoint(corners[2]);
        if (tr.x - bl.x < 2f) tr.x = bl.x + 2f; // 空でも左端から少し出す
        for (int i = 0; i < n; i++)
        {
            var p = Get();
            if (p == null) break;
            Vector2 pos = new Vector2(Random.Range(bl.x, tr.x), Random.Range(bl.y, tr.y));
            float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad; // 全方向へ散る
            p.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(driftSpeedRange.x, driftSpeedRange.y) * speedMul;
            p.damping = driftDamping;
            p.age = 0f;
            p.life = particleLife * Random.Range(0.7f, 1.2f);
            p.size = Random.Range(sizeRange.x, sizeRange.y);
            p.color = particleColors != null && particleColors.Length > 0 ? particleColors[Random.Range(0, particleColors.Length)] : Color.white;
            p.rt.anchoredPosition = pos;
            p.rt.sizeDelta = new Vector2(p.size, p.size);
            p.img.color = p.color;
            p.img.gameObject.SetActive(true);
        }
        return count - n;
    }

    private P Get()
    {
        foreach (var x in pool) if (!x.img.gameObject.activeSelf) return x;
        if (pool.Count >= maxParticlesLimit) return null;
        var go = new GameObject("Neon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(particleLayer, false);
        var p = new P { img = go.GetComponent<Image>(), rt = (RectTransform)go.transform };
        p.img.sprite = particleSprite;
        p.img.raycastTarget = false;
        p.img.preserveAspect = false;
        p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
        p.rt.pivot = new Vector2(0.5f, 0.5f);
        go.SetActive(false);
        pool.Add(p);
        return p;
    }

    private void UpdateParticles(float dt)
    {
        if (dt <= 0f) return;
        foreach (var p in pool)
        {
            if (!p.img.gameObject.activeSelf) continue;
            p.age += dt;
            float t = p.age / p.life;
            if (t >= 1f) { p.img.gameObject.SetActive(false); continue; }
            p.vel *= Mathf.Exp(-p.damping * dt); // 重さは付けず、だんだん遅くなるだけ（ゆるやかな動き）
            p.rt.anchoredPosition += p.vel * dt;
            float k = 1f - t;
            var c = p.color; c.a *= k;
            p.img.color = c;
            float s = p.size * Mathf.Lerp(1f, 0.5f, t);
            p.rt.sizeDelta = new Vector2(s, s);
        }
    }

#if UNITY_EDITOR
    /// <summary>Setupツールから参照をつなぐ</summary>
    public void EditorWire(Image white, Image red, RectTransform layer, TextMeshProUGUI text, Sprite sprite)
    {
        whiteFill = white;
        redFill = red;
        particleLayer = layer;
        pickupText = text;
        if (particleSprite == null) particleSprite = sprite;
    }
#endif
}
