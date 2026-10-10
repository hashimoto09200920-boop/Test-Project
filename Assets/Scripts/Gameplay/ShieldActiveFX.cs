using UnityEngine;

/// <summary>
/// エネミーのシールドの泡（VFX_ShieldActive.prefab）を派手にする部品。泡のプレハブのルートに付ける（メニュー「Tools/弾の見た目/13」）。
/// 最初のデザイン（バリアの円＝粒の集まり、中央＝中心から円状に広がる泡）はそのまま残し、その上に足す。
/// ・バリアの円：① 粒が円周を流れる（PS_Orbsの回転。メニューで設定） A1 うねる・A5 粒の大きさのばらつき（メニューで設定） A3 2色のドット A4 エネルギーのこぼれ ② きらめき ③ 六角形の網目がゆっくり回る（子のHex） ④ 残量で粒がまばら・色が青→紫→赤
/// ・中央の泡：B1 渦を巻く・B2 中心は白→外ほど青・B3 外の円に溶け込む・B4 粒の大きさのばらつき（メニューで設定） ⑤ 「ドクン、ドクン」と脈打つ二重の波紋 ⑥ 波紋が輪に届くと輪が光る ⑦ 中心の脈打つ光
/// ・当たった時：⑧ 当たった点から小さな粒の波紋＋六角形の波紋（子のHexRipple）＋輪の粒が外へ弾ける／全快：⑨ 大きな粒の波紋で張り直し／回復中：⑩ 外から粒が集まって輪に並ぶ
/// 追加の粒は泡の中のパーティクル（PS_Fx）にEmitで出す（泡と同じ大きさ・位置の基準。粒の形も同じマテリアル）。
/// 泡はシールドが0の間は非表示になる（EnemyShieldが切り替え）ので、その間はこの部品も止まる
/// </summary>
[DisallowMultipleComponent]
public class ShieldActiveFX : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [Tooltip("バリアの円（粒の集まり）")]
    public ParticleSystem ringPS;
    [Tooltip("中央から広がる泡")]
    public ParticleSystem wavePS;
    [Tooltip("追加の粒（波紋・きらめき・当たった時など）を出す")]
    public ParticleSystem fxPS;

    [Tooltip("③ 六角形の網目（子のSpriteRenderer）")]
    public SpriteRenderer hex;
    [Tooltip("⑧ 当たった所の六角形の波紋（子のSpriteRenderer）")]
    public SpriteRenderer hexRipple;

    [Header("粒の形・大きさ")]
    [Tooltip("全ての粒の大きさの倍率（丸いぼかしの粒は四角より小さく見えるので大きめにする。メニューで切り替え時に設定）")]
    public float dotSizeMul = 1f;
    [Tooltip("バリアの円の半径（泡のパーティクルの単位。PS_Orbsの円の半径と同じ）")]
    public float ringRadius = 1f;

    [Header("② きらめき")]
    public bool twinkleEnabled = true;
    [Tooltip("きらめく粒の量（個/秒）")]
    public float twinkleRate = 8f;
    public float twinkleSize = 0.07f;

    [Header("③ 六角形の網目")]
    public bool hexEnabled = true;
    [Tooltip("網目の色（残量で④の色に変わる）")]
    public Color hexColor = new Color(0.45f, 0.8f, 1f, 1f);
    [Range(0f, 1f)] public float hexAlpha = 0.2f; // 目立たせすぎない（0.32は目立ちすぎ、0.12はほぼ見えない：ユーザー確認）
    [Tooltip("網目が回る速さ（度/秒）")]
    public float hexSpinSpeed = 12f;
    [Tooltip("網目の大きさ（バリアの円の直径に対する倍率）")]
    public float hexSizeMul = 1f;

    [Header("④ 残量で変わる")]
    public bool ratioLookEnabled = true;
    [Tooltip("残量が半分の時の色（満タンは元の色のまま）")]
    public Color halfColor = new Color(0.75f, 0.5f, 1f, 1f);
    [Tooltip("残量が少ない時の色")]
    public Color lowColor = new Color(1f, 0.35f, 0.4f, 1f);
    [Tooltip("残量が0に近い時の粒の量の倍率（満タン＝1）")]
    [Range(0f, 1f)] public float lowDensity = 0.35f;
    [Tooltip("ONで残量が減るほど外の円の粒をまばらにする（色の変化とは別）")]
    public bool ratioDensityEnabled = false;

    [Header("中央の泡（PS_Ring）")]
    [Tooltip("ONで、中央から広がる泡がちょうど外の円の位置まで届くように、泡の粒の寿命を自動で合わせる")]
    public bool waveReachRing = true;
    [Tooltip("泡が届く位置（外の円の半径に対する倍率。1＝ちょうど外の円）")]
    public float waveReachMul = 1f;

    [Header("A3 2色のドット（外の円）")]
    [Tooltip("ONで、外の円の粒をシアン主体にして、白く明るい粒を少し混ぜる")]
    public bool twoToneEnabled = true;
    [Tooltip("主な粒の色（シアン。シールド量で④の色に変わる）")]
    public Color dotMainColor = new Color(0.5f, 0.85f, 1f, 1f);
    [Tooltip("混ぜる明るい粒の色")]
    public Color dotAccentColor = Color.white;
    [Tooltip("明るい粒の割合（0〜1）")]
    [Range(0f, 1f)] public float dotAccentRatio = 0.2f;

    [Header("A4 エネルギーのこぼれ")]
    [Tooltip("ONで、ときどき外の円から外へ粒がふわっと離れて消える")]
    public bool spillEnabled = true;
    [Tooltip("こぼれる粒の量（個/秒）")]
    public float spillRate = 3f;
    [Tooltip("離れる速さ（外の円の半径に対する割合/秒）")]
    public float spillSpeed = 0.35f;
    public float spillLifetime = 0.9f;
    public float spillSize = 0.04f;

    [Header("⑤ 脈打つ二重の波紋")]
    public bool pulseEnabled = true;
    [Tooltip("「ドクン、ドクン」の間隔（秒）")]
    public float pulseInterval = 1.4f;
    [Tooltip("1回目と2回目の波紋の間（秒）")]
    public float pulseGap = 0.22f;
    public int pulseCount = 48;
    public float pulseSpeed = 1.2f;
    public float pulseSize = 0.05f;

    [Header("⑥ 波紋が輪に届くと光る")]
    public bool ringFlashEnabled = true;
    public int ringFlashCount = 28;
    public float ringFlashSize = 0.09f;

    [Header("⑦ 中心の脈打つ光")]
    public bool coreEnabled = true;
    [Tooltip("中心の光の大きさ（バリアの円の半径に対する割合）")]
    public float coreSize = 0.55f;
    [Range(0f, 1f)] public float coreAlpha = 0.35f;

    [Header("⑧ 当たった時")]
    public bool hitEnabled = true;
    public int hitWaveCount = 24;
    public float hitWaveSpeed = 1f;
    public int hitBurstCount = 12;
    public float hitBurstSpeed = 1.6f;

    [Tooltip("当たった所の六角形の波紋")]
    public bool hexRippleEnabled = true;
    public float hexRippleDuration = 0.3f;

    [Header("⑨ 全快の張り直し")]
    public bool restoreEnabled = true;
    public int restoreCount = 120;
    public float restoreSpeed = 2f;

    [Header("⑩ 回復中に粒が集まる")]
    public bool recoverEnabled = true;
    [Tooltip("集まる粒の量（個/秒）")]
    public float recoverRate = 16f;

    private EnemyShield shield;
    private float ringBaseSize = -1f, waveBaseSize = -1f, ringBaseRate, waveBaseSpeed = 1f;
    private Color waveBaseColor = Color.white;
    private Color ringBaseColor = Color.white;
    private float hexAngle, rippleT = 999f;
    private Vector3 rippleWorldPos;
    private float pulseTimer, coreTimer, twinkleAcc, recoverAcc, spillAcc;
    private Gradient twoToneGrad;
    private ParticleSystem.MinMaxCurve ringBaseSizeCurve, waveBaseSizeCurve;
    private float flashAt1 = -1f, flashAt2 = -1f, second = -1f;
    private bool pendingRestore;
    private float clock;

    /// <summary>EnemyShieldが泡を作った時に呼ぶ</summary>
    public void Init(EnemyShield s)
    {
        shield = s;
        CaptureBase();
    }

    private void Awake() { CaptureBase(); }

    private void CaptureBase()
    {
        if (ringPS != null && ringBaseSize < 0f)
        {
            var m = ringPS.main; ringBaseSize = m.startSizeMultiplier; ringBaseSizeCurve = m.startSize; ringBaseColor = m.startColor.color;
            ringBaseRate = ringPS.emission.rateOverTimeMultiplier;
        }
        if (wavePS != null && waveBaseSize < 0f)
        {
            var wm = wavePS.main;
            waveBaseSize = wm.startSizeMultiplier; waveBaseSizeCurve = wm.startSize;
            waveBaseSpeed = Mathf.Max(0.01f, wm.startSpeedMultiplier);
            waveBaseColor = wm.startColor.color;
        }
        if (fxPS != null) { var em = fxPS.emission; em.enabled = false; }
        if (hexRipple != null) hexRipple.enabled = false;
    }

    // バリアの円の、画面上の半径（泡のパーティクルは自分の大きさだけで描く設定（Scaling Mode＝Local）なので、親の大きさは含めない）
    private float WorldRingRadius()
    {
        if (ringPS == null) return ringRadius;
        var t = ringPS.transform;
        float s = ringPS.main.scalingMode == ParticleSystemScalingMode.Hierarchy ? Mathf.Abs(t.lossyScale.x) : Mathf.Abs(t.localScale.x);
        return ringRadius * s;
    }

    // 子の画像を、画面上で指定の直径にする（親の大きさを打ち消す）
    private void SetWorldDiameter(SpriteRenderer sr, float diameter)
    {
        float spriteSize = sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.x) : 1f;
        Transform p = sr.transform.parent;
        float parentScale = p != null ? Mathf.Max(0.0001f, Mathf.Abs(p.lossyScale.x)) : 1f;
        sr.transform.localScale = Vector3.one * (diameter / spriteSize / parentScale);
    }

    /// <summary>シールドに弾が当たった時（EnemyShield.PlayHitFx）</summary>
    public void OnHit(Vector3 worldPos)
    {
        if (!hitEnabled || fxPS == null || !isActiveAndEnabled) return;
        // 泡のパーティクルは自分の大きさだけで描く設定のため、向きだけを使って輪の上の点を決める
        Vector3 d = worldPos - transform.position; d.z = 0f;
        Vector2 dir = d.sqrMagnitude > 0.0001f ? (Vector2)d.normalized : Vector2.up;
        Vector3 p = dir * ringRadius;
        Color col = CurrentColor(Color.white);
        if (hexRippleEnabled && hexRipple != null)
        {
            rippleT = 0f;
            rippleWorldPos = transform.position + (Vector3)(dir * WorldRingRadius());
        }
        // 当たった点から小さな粒の波紋
        for (int i = 0; i < hitWaveCount; i++)
        {
            float a = i / (float)Mathf.Max(1, hitWaveCount) * Mathf.PI * 2f;
            Emit(p, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * hitWaveSpeed * ringRadius, 0.35f, pulseSize * 0.8f, Color.Lerp(col, Color.white, 0.4f));
        }
        // 輪の粒が外へ弾ける（当たった所の周り）
        float baseAng = Mathf.Atan2(dir.y, dir.x);
        for (int i = 0; i < hitBurstCount; i++)
        {
            float a = baseAng + Random.Range(-0.6f, 0.6f);
            Vector3 rp = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ringRadius;
            Emit(rp, rp.normalized * hitBurstSpeed * Random.Range(0.5f, 1f) * ringRadius, 0.3f, ringFlashSize, Color.Lerp(col, Color.white, 0.6f));
        }
    }

    /// <summary>シールドが全快した時（EnemyShield.RestoreFullShield）。泡はこの直後に表示されるので、次に動いた時に出す</summary>
    public void OnRestore()
    {
        if (restoreEnabled) pendingRestore = true;
    }

    private void LateUpdate()
    {
        float dt = SlowMoTime.DeltaTime;
        clock += dt;
        if (dotSizeMul <= 0f) dotSizeMul = 1f;

        // ④ 残量：色と粒の量。粒の大きさの倍率もここで反映
        float ratio = 1f;
        if (shield != null)
        {
            var eff = shield.Effective;
            ratio = eff.MaxShield > 0 ? Mathf.Clamp01(eff.CurrentShield / (float)eff.MaxShield) : 1f;
        }
        float density = ratioDensityEnabled ? Mathf.Lerp(lowDensity, 1f, ratio) : 1f;
        if (ringPS != null)
        {
            var m = ringPS.main; m.startSize = ScaledSize(ringBaseSizeCurve, dotSizeMul);
            if (twoToneEnabled)
            {
                // A3 シアン主体＋白く明るい粒を少し（ランダムに選ぶ。シアン側はシールド量の色に変わる）
                if (twoToneGrad == null) twoToneGrad = new Gradient();
                Color mc = CurrentColor(dotMainColor, ratio);
                float cut = Mathf.Clamp(1f - dotAccentRatio, 0.01f, 0.99f);
                twoToneGrad.SetKeys(
                    new[] { new GradientColorKey(mc, 0f), new GradientColorKey(mc, cut), new GradientColorKey(dotAccentColor, Mathf.Min(1f, cut + 0.001f)), new GradientColorKey(dotAccentColor, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                var g = new ParticleSystem.MinMaxGradient(twoToneGrad); g.mode = ParticleSystemGradientMode.RandomColor;
                m.startColor = g;
            }
            else m.startColor = CurrentColor(ringBaseColor, ratio);
            var em = ringPS.emission; em.rateOverTimeMultiplier = ringBaseRate * density;
        }
        // ③ 六角形の網目（ゆっくり回る。色・濃さは残量で変わる）
        float wr = WorldRingRadius();
        if (hex != null)
        {
            hex.enabled = hexEnabled;
            if (hexEnabled)
            {
                hexAngle += hexSpinSpeed * dt;
                hex.transform.localRotation = Quaternion.Euler(0f, 0f, hexAngle);
                hex.transform.localPosition = Vector3.zero;
                SetWorldDiameter(hex, wr * 2f * hexSizeMul);
                Color hc = CurrentColor(hexColor, ratio); hc.a = hexAlpha * (ratioLookEnabled ? Mathf.Lerp(0.5f, 1f, ratio) : 1f);
                hex.color = hc;
            }
        }
        // ⑧ 当たった所の六角形の波紋
        if (hexRipple != null)
        {
            rippleT += dt;
            float k = rippleT / Mathf.Max(0.05f, hexRippleDuration);
            hexRipple.enabled = k < 1f;
            if (k < 1f)
            {
                hexRipple.transform.position = rippleWorldPos;
                float e = 1f - (1f - k) * (1f - k);
                SetWorldDiameter(hexRipple, wr * Mathf.Lerp(0.2f, 1.1f, e));
                Color rc = Color.Lerp(CurrentColor(hexColor, ratio), Color.white, 0.5f); rc.a = 0.85f * (1f - k);
                hexRipple.color = rc;
            }
        }
        if (wavePS != null)
        {
            var m = wavePS.main;
            m.startSize = ScaledSize(waveBaseSizeCurve, dotSizeMul);
            m.startColor = CurrentColor(waveBaseColor, ratio); // ④ 中央の泡の色も残量で変わる
            // 泡が外の円の位置まで届くように寿命を合わせる（同じ速さのまま、届くまでの時間＝距離÷速さ）
            if (waveReachRing) m.startLifetimeMultiplier = ringRadius * waveReachMul / waveBaseSpeed;
        }
        if (fxPS == null) return;
        Color col = CurrentColor(Color.white, ratio);

        // ⑨ 全快の張り直し
        if (pendingRestore)
        {
            pendingRestore = false;
            for (int i = 0; i < restoreCount; i++)
            {
                float a = i / (float)Mathf.Max(1, restoreCount) * Mathf.PI * 2f;
                Vector3 v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Emit(Vector3.zero, v * restoreSpeed * ringRadius * Random.Range(0.85f, 1.05f), ringRadius / Mathf.Max(0.1f, restoreSpeed), pulseSize * 1.2f, Color.Lerp(col, Color.white, 0.5f));
            }
            flashAt1 = clock + ringRadius / Mathf.Max(0.1f, restoreSpeed);
        }

        // ⑤ 脈打つ二重の波紋（⑥ 輪に届いたら光る）
        if (pulseEnabled)
        {
            pulseTimer += dt;
            if (pulseTimer >= pulseInterval)
            {
                pulseTimer = 0f;
                EmitPulse(col);
                second = clock + pulseGap;
            }
            if (second > 0f && clock >= second) { second = -1f; EmitPulse(col); }
        }
        if (ringFlashEnabled)
        {
            if (flashAt1 > 0f && clock >= flashAt1) { flashAt1 = -1f; EmitRingFlash(col); }
            if (flashAt2 > 0f && clock >= flashAt2) { flashAt2 = -1f; EmitRingFlash(col); }
        }

        // ⑦ 中心の脈打つ光（大きめの淡い粒を少しずつ重ねる）
        if (coreEnabled)
        {
            coreTimer += dt;
            if (coreTimer >= 0.12f)
            {
                coreTimer = 0f;
                float pulse = 0.75f + 0.25f * Mathf.Sin(clock * Mathf.PI * 2f / Mathf.Max(0.2f, pulseInterval));
                Color cc = Color.Lerp(col, Color.white, 0.5f); cc.a = coreAlpha;
                Emit(Vector3.zero, Vector3.zero, 0.3f, ringRadius * coreSize * pulse, cc);
            }
        }

        // A4 エネルギーのこぼれ（ときどき外の円から外へ粒が離れて消える）
        if (spillEnabled)
        {
            spillAcc += spillRate * dt;
            while (spillAcc >= 1f)
            {
                spillAcc -= 1f;
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Emit(v * ringRadius, v * spillSpeed * ringRadius, spillLifetime, spillSize, Color.Lerp(CurrentColor(dotMainColor, ratio), Color.white, 0.3f));
            }
        }

        // ② きらめき
        if (twinkleEnabled)
        {
            twinkleAcc += twinkleRate * density * dt;
            while (twinkleAcc >= 1f)
            {
                twinkleAcc -= 1f;
                float a = Random.Range(0f, Mathf.PI * 2f);
                Emit(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ringRadius, Vector3.zero, 0.25f, twinkleSize, Color.white);
            }
        }

        // ⑩ 回復中に、外から粒が集まって輪に並ぶ
        if (recoverEnabled && shield != null && shield.IsGraduallyRecovering)
        {
            recoverAcc += recoverRate * dt;
            while (recoverAcc >= 1f)
            {
                recoverAcc -= 1f;
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                const float life = 0.45f;
                Emit(v * ringRadius * 1.6f, -v * ringRadius * 0.6f / life, life, pulseSize, Color.Lerp(col, Color.white, 0.3f));
            }
        }
    }

    // 粒の大きさ（大きさのばらつきがあっても、最小・最大の両方に倍率を掛ける）
    private static ParticleSystem.MinMaxCurve ScaledSize(ParticleSystem.MinMaxCurve c, float mul)
    {
        if (c.mode == ParticleSystemCurveMode.TwoConstants) return new ParticleSystem.MinMaxCurve(c.constantMin * mul, c.constantMax * mul);
        if (c.mode == ParticleSystemCurveMode.Constant) return new ParticleSystem.MinMaxCurve(c.constant * mul);
        var r = c; r.curveMultiplier = c.curveMultiplier * mul; return r;
    }

    private void EmitPulse(Color col)
    {
        float life = ringRadius / Mathf.Max(0.1f, pulseSpeed);
        for (int i = 0; i < pulseCount; i++)
        {
            float a = i / (float)Mathf.Max(1, pulseCount) * Mathf.PI * 2f;
            Emit(Vector3.zero, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * pulseSpeed * ringRadius, life, pulseSize, Color.Lerp(col, Color.white, 0.3f));
        }
        // 届いた瞬間に輪を光らせる（2回目の波紋の時も）
        if (flashAt1 < 0f) flashAt1 = clock + life; else flashAt2 = clock + life;
    }

    private void EmitRingFlash(Color col)
    {
        for (int i = 0; i < ringFlashCount; i++)
        {
            float a = (i + Random.Range(-0.3f, 0.3f)) / Mathf.Max(1, ringFlashCount) * Mathf.PI * 2f;
            Emit(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ringRadius, Vector3.zero, 0.3f, ringFlashSize, Color.Lerp(col, Color.white, 0.6f));
        }
    }

    private void Emit(Vector3 localPos, Vector3 vel, float life, float size, Color c)
    {
        var ep = new ParticleSystem.EmitParams
        {
            position = localPos, velocity = vel, startLifetime = Mathf.Max(0.05f, life),
            startSize = size * dotSizeMul, startColor = c, applyShapeToPosition = false,
        };
        fxPS.Emit(ep, 1);
    }

    // ④ 残量の色：満タン＝元の色（base）、半分＝紫、少ない＝赤
    private Color CurrentColor(Color baseColor, float ratio = -1f)
    {
        if (!ratioLookEnabled) return baseColor;
        if (ratio < 0f)
        {
            ratio = 1f;
            if (shield != null) { var eff = shield.Effective; ratio = eff.MaxShield > 0 ? Mathf.Clamp01(eff.CurrentShield / (float)eff.MaxShield) : 1f; }
        }
        Color c = ratio >= 0.5f ? Color.Lerp(halfColor, baseColor, (ratio - 0.5f) * 2f) : Color.Lerp(lowColor, halfColor, ratio * 2f);
        c.a = baseColor.a;
        return c;
    }
}
