using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Area6ボス「Shaman」の「精霊の炎」の回っている火の玉1個（Shaman専用。Shaman_SpiritFlame.prefabに付ける）。
/// ・見た目はParticle Systemの炎（子のFX）
/// ・ブロックHP（WallHealth）を持ち、プレイヤーが反射した弾を受け止めて防ぐ（反射していない弾は「敵」のレイヤーのためすり抜ける）
/// ・一定量ダメージを受けて壊れたら、炎の放出を止めて消える（壊れた時のSE＝火炎魔法1は、消えても途中で切れないようAudioOneShotPoolで鳴らす）
/// ・ブロックHPが半分以下になると色が変わり（Weakened Color）、その状態の時だけ、プレイヤーの線（PaddleDot）に触れたら
///   ShamanSpiritFlamesへ知らせる（反射弾に変わって飛んでいく）
/// ・反射して飛んでいる火の玉に触れると吸収される（ShamanFlameBullet）
/// ・反射弾が当たった時のSE・エフェクトは、通常のステージブロックと同じ（WallHealthのHit Clips / Hit Vfx Prefab。メニューでコピー）
/// ・位置と見え方はShamanSpiritFlames（Shaman本体側）が毎フレーム指定する
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WallHealth))]
public class ShamanSpiritFlame : MonoBehaviour
{
    [Tooltip("炎のParticle System（子のFX）")]
    [SerializeField] private ParticleSystem fx;
    [Tooltip("壊れた時のSE（火炎魔法1）")]
    [SerializeField] private AudioClip breakSE;
    [Range(0f, 1f)] [SerializeField] private float breakSEVolume = 1f;
    [Tooltip("壊れた・消えた後、残っている炎の粒が消えきるまで待ってから破棄する秒数")]
    [SerializeField] private float destroyDelay = 0.8f;
    [Tooltip("プレイヤーの線に触れたと判定する半径（当たり判定の半径に足す余裕）")]
    [SerializeField] private float lineDetectMargin = 0.05f;
    [Tooltip("ブロックHPがこの割合以下になると色が変わり、線で反射できるようになる（0.5＝半分）")]
    [Range(0.01f, 1f)] [SerializeField] private float weakenHpRatio = 0.5f;
    [Tooltip("ブロックHPが減って線で反射できるようになった火の玉の色（炎の粒の色に掛ける）")]
    [SerializeField] private Color weakenedColor = new Color(0.45f, 0.8f, 1f, 1f);

    private WallHealth health;
    private Rigidbody2D rb;
    private CircleCollider2D col;
    private bool fading;
    private bool hiddenByWarp;
    private bool appeared;
    private bool weakened;
    private ParticleSystem[] fxSystems;

    private static FieldInfo s_currentHpField;
    private static FieldInfo s_maxHpField;

    /// <summary>ブロックHPが半分以下になり、色が変わって線で反射できる状態か</summary>
    public bool IsWeakened => weakened;
    /// <summary>色が変わった後の炎の色（線で反射されて飛ぶ火の玉にも引き継ぐ）</summary>
    public Color WeakenedColor => weakenedColor;

    /// <summary>プレイヤーの線に触れた（引数：触れた線の点）</summary>
    public event System.Action<ShamanSpiritFlame, PaddleDot> OnTouchedLine;

    /// <summary>まだ回っている（壊れていない・消えていない）か</summary>
    public bool IsAlive => !fading && health != null && !health.IsBroken;

    private static readonly Collider2D[] s_hits = new Collider2D[16];
    private static ContactFilter2D s_filter;
    private static bool s_filterReady;

    private void Awake()
    {
        health = GetComponent<WallHealth>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();
        if (fx == null) fx = GetComponentInChildren<ParticleSystem>();
        fxSystems = GetComponentsInChildren<ParticleSystem>(true);
        if (s_currentHpField == null)
        {
            s_currentHpField = typeof(WallHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic);
            s_maxHpField = typeof(WallHealth).GetField("maxHp", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        if (health != null) health.OnBroken += HandleBroken;
        if (fx != null) fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (!s_filterReady)
        {
            s_filter = new ContactFilter2D();
            s_filter.useTriggers = true;
            s_filter.SetLayerMask(Physics2D.AllLayers);
            s_filterReady = true;
        }
    }

    private void OnDestroy()
    {
        if (health != null) health.OnBroken -= HandleBroken;
    }

    /// <summary>位置を指定する（Kinematic Rigidbody2DはMovePositionで動かす）</summary>
    public void MoveTo(Vector2 pos)
    {
        if (rb != null) rb.MovePosition(pos);
        else transform.position = new Vector3(pos.x, pos.y, transform.position.z);
    }

    /// <summary>召喚：炎を出し始める</summary>
    public void Appear()
    {
        appeared = true;
        UpdateVisible();
    }

    /// <summary>Shaman本体の透明度に合わせる（ワープ中に一緒に消える）。ほぼ見えない間は炎を止め、当たり判定も切る</summary>
    public void SetExternalAlpha(float a)
    {
        bool hide = a < 0.5f;
        if (hide == hiddenByWarp) return;
        hiddenByWarp = hide;
        UpdateVisible();
    }

    private void UpdateVisible()
    {
        if (fading) return;
        bool visible = appeared && !hiddenByWarp;
        if (col != null && health != null && !health.IsBroken) col.enabled = visible;
        if (fx == null) return;
        if (visible) { if (!fx.isEmitting) fx.Play(true); }
        else fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // ブロックHPが半分以下になったら色を変える（WallHealthのHPはprivateのため読み取る）
    private void Update()
    {
        if (weakened || fading || health == null || s_currentHpField == null || s_maxHpField == null) return;
        int cur = (int)s_currentHpField.GetValue(health);
        int max = (int)s_maxHpField.GetValue(health);
        if (max <= 0 || cur > max * weakenHpRatio) return;
        weakened = true;
        if (fxSystems != null)
            foreach (var ps in fxSystems)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startColor = weakenedColor; // これから出る炎の粒の色が変わる
            }
    }

    // プレイヤーの線に触れたか（線のレイヤーとの物理の組み合わせに頼らず、重なりで判定する）。色が変わった後だけ
    private void FixedUpdate()
    {
        if (!weakened || fading || hiddenByWarp || !appeared || col == null || !col.enabled) return;
        Vector3 s = transform.lossyScale;
        float r = col.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y)) + lineDetectMargin;
        int n = Physics2D.OverlapCircle(transform.position, r, s_filter, s_hits);
        for (int i = 0; i < n; i++)
        {
            var c = s_hits[i];
            if (c == null) continue;
            PaddleDot dot = c.GetComponent<PaddleDot>();
            if (dot == null) continue;
            OnTouchedLine?.Invoke(this, dot);
            return;
        }
    }

    private void HandleBroken(Vector3 pos)
    {
        if (breakSE != null && breakSEVolume > 0f)
            AudioOneShotPool.Play(breakSE, breakSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f), transform.position, null, 0.1f);
        Vanish();
    }

    /// <summary>消す（壊れた時・反射弾に変わった時・Shamanが倒された時）。炎の放出を止め、残りの粒が消えたら破棄する</summary>
    public void Vanish()
    {
        if (fading) return;
        fading = true;
        if (col != null) col.enabled = false;
        if (fx != null) fx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(gameObject, Mathf.Max(0.01f, destroyDelay));
    }

    // ---------- 線の種類（PaddleDot.lineTypeはprivateのため読み取る。反射SE・VFXの種類を合わせる用） ----------
    private static FieldInfo s_lineTypeField;
    public static PaddleDot.LineType GetLineType(PaddleDot dot)
    {
        if (dot == null) return PaddleDot.LineType.Normal;
        if (s_lineTypeField == null)
            s_lineTypeField = typeof(PaddleDot).GetField("lineType", BindingFlags.Instance | BindingFlags.NonPublic);
        return s_lineTypeField != null ? (PaddleDot.LineType)s_lineTypeField.GetValue(dot) : PaddleDot.LineType.Normal;
    }
}
