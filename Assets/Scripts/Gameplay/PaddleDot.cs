using UnityEngine;
using System.Collections.Generic;

public class PaddleDot : MonoBehaviour
{


    public enum LineType
    {
        Normal,
        RedAccel
    }

    [Header("Life")]
    [SerializeField] private float lifeTime = 1.2f;

    [Header("Runtime (Injected)")]
    [SerializeField] private LineType lineType = LineType.Normal;
    [SerializeField] private float accelMultiplierPerHit = 1.2f;
    [SerializeField] private int accelMaxCount = 5;

    [Header("Just Reflect (Injected)")]
    [SerializeField] private float justWindowSeconds = 0.20f;
    [SerializeField] private float justDamageMultiplier = 1.50f;

    // =========================================================
    // Hardness（線の硬度：PaddleDrawerから注入）
    // =========================================================
    [Header("Hardness (Injected)")]
    [Tooltip("線の硬度。弾の貫通値がこれより大きい時、反射せず線が破壊される。")]
    [SerializeField] private int hardness = 1;

    // =========================================================
    // Line Visual（通常線：PaddleDrawerから注入される想定）
    // =========================================================
    [Header("Line Visual (Injected)")]
    [Tooltip("Stroke基準色（PaddleDrawerから注入）。未注入なら白/赤。")]
    [SerializeField] private Color strokeBaseColor = Color.white;

    [Tooltip("Dot生成時に明度だけ揺らす量（0なら揺らさない）。生成時1回のみ。")]
    [SerializeField, Range(0f, 0.30f)] private float dotValueJitter = 0.0f;

    // =========================================================
    // Circle Visual（円確定：濃さ＋太さ）
    // =========================================================
    [Header("Circle Visual (Boost Current Stroke Color)")]
    [Tooltip("円確定時の『濃さ』：彩度を上げる倍率（1.0〜1.8推奨）。")]
    [SerializeField, Range(1.0f, 2.5f)] private float circleSaturationBoost = 1.45f;

    [Tooltip("円確定時の『濃さ』：明度を掛ける倍率（白飛び防止）。0.55〜1.0推奨。")]
    [SerializeField, Range(0.20f, 1.20f)] private float circleValueMultiply = 0.80f;

    [Tooltip("円確定時だけの追加明度ジッター（円確定時1回のみ）。0なら揺らさない。")]
    [SerializeField, Range(0f, 0.30f)] private float circleExtraValueJitter = 0.06f;

    [Tooltip("円確定時の太さ倍率（Dotのスケール）。1.05〜1.30推奨。")]
    [SerializeField, Range(1.0f, 2.0f)] private float circleThicknessScale = 1.15f;

    [Tooltip("円確定時のalphaを強制する。-1なら強制しない（0事故だけ保険）。")]
    [SerializeField, Range(-1f, 1f)] private float circleForceAlpha = 1f;

    [Header("Circle Blink (end of life)")]
    [Tooltip("円確定dotが消える直前に点滅するウィンドウ時間（秒）。0なら点滅しない。")]
    [SerializeField, Range(0f, 1f)] private float blinkDuration = 0.4f;
    [Tooltip("点滅回数。")]
    [SerializeField, Range(1, 5)] private int blinkCount = 2;

    [Header("Circle Visual (Fallback: used only when stroke color can't be cached)")]
    [SerializeField] private Color circleFallbackNormal = Color.cyan;
    [SerializeField] private Color circleFallbackRed = Color.magenta;

    // =========================================================
    // ★Strokeごとの「通常時表示色」キャッシュ（Configure後に保存）
    // =========================================================
    private static readonly Dictionary<Stroke, Color> strokeBaseRenderedColorCache
        = new Dictionary<Stroke, Color>();

    private SpriteRenderer sr;
    private float timer;
    private float bornTime;
    private Stroke parentStroke;

    /// <summary>この点が属するStroke（同じ線かどうかの判定に使う。例：掃射ブレスの旋回中、隣接する別Dotに移っても同じ線として扱う）</summary>
    public Stroke ParentStroke => parentStroke;

    public float LifeTime
    {
        get => lifeTime;
        set => lifeTime = Mathf.Max(0f, value);
    }

    private bool circleVisualApplied;
    private bool configured;

    // ★追加：元のスケール保持（円確定で太くした後も戻す必要は無いが、保険で保持）
    private Vector3 initialLocalScale;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        initialLocalScale = transform.localScale;

        // 白線/赤線を煙より手前に表示（煙のsortingOrder=1000より大きい値）
        if (sr != null)
        {
            sr.sortingOrder = 1100;
        }

        ApplyBaseVisual();
    }

    private void OnEnable()
    {
        timer = 0f;
        bornTime = Time.time;

        parentStroke = GetComponentInParent<Stroke>();
        if (parentStroke != null)
        {
            parentStroke.RegisterDot(this);
        }
    }

    private void OnDestroy()
    {
        if (parentStroke != null)
        {
            parentStroke.UnregisterDot(this);
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (circleVisualApplied && sr != null && blinkDuration > 0f)
        {
            float remaining = lifeTime - timer;
            if (remaining <= blinkDuration)
            {
                float elapsed = blinkDuration - remaining;
                float halfPeriod = blinkDuration / (blinkCount * 2f);
                float phase = elapsed % (halfPeriod * 2f);
                float alpha = (phase < halfPeriod) ? 1f : 0f;
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyBaseVisual()
    {
        if (sr == null) return;

        Color c = strokeBaseColor;

        // 未注入時の保険
        if (lineType == LineType.RedAccel && c == Color.white)
        {
            c = Color.red;
        }

        float j = Mathf.Clamp(dotValueJitter, 0f, 0.30f);
        if (j > 0f)
        {
            float mul = 1f + Random.Range(-j, j);
            c.r = Mathf.Clamp01(c.r * mul);
            c.g = Mathf.Clamp01(c.g * mul);
            c.b = Mathf.Clamp01(c.b * mul);
        }

        if (c.a <= 0f) c.a = 1f;

        sr.color = c;
        circleVisualApplied = false;

        CacheStrokeBaseRenderedColorOnce();
    }

    // 5引数（既存互換）
    public void Configure(LineType type, float accelMul, int accelMax, float justWindow, float justMul)
    {
        lineType = type;

        accelMultiplierPerHit = Mathf.Max(1.0f, accelMul);
        accelMaxCount = Mathf.Max(0, accelMax);

        justWindowSeconds = Mathf.Max(0f, justWindow);
        justDamageMultiplier = Mathf.Max(1.0f, justMul);

        strokeBaseColor = (lineType == LineType.RedAccel) ? Color.red : Color.white;
        dotValueJitter = 0f;

        hardness = 1;

        configured = true;
        ApplyBaseVisual();
    }

    // 7引数（PaddleDrawer.cs の呼び出しに対応）
    public void Configure(LineType type, float accelMul, int accelMax,
                          float justWindow, float justMul,
                          Color strokeColor, float valueJitter)
    {
        lineType = type;

        accelMultiplierPerHit = Mathf.Max(1.0f, accelMul);
        accelMaxCount = Mathf.Max(0, accelMax);

        justWindowSeconds = Mathf.Max(0f, justWindow);
        justDamageMultiplier = Mathf.Max(1.0f, justMul);

        strokeBaseColor = strokeColor;
        dotValueJitter = Mathf.Clamp(valueJitter, 0f, 0.30f);

        hardness = 1;

        configured = true;
        ApplyBaseVisual();
    }

    // ★8引数（硬度も注入）
    public void Configure(LineType type, float accelMul, int accelMax,
                          float justWindow, float justMul,
                          Color strokeColor, float valueJitter,
                          int injectedHardness)
    {
        lineType = type;

        accelMultiplierPerHit = Mathf.Max(1.0f, accelMul);
        accelMaxCount = Mathf.Max(0, accelMax);

        justWindowSeconds = Mathf.Max(0f, justWindow);
        justDamageMultiplier = Mathf.Max(1.0f, justMul);

        strokeBaseColor = strokeColor;
        dotValueJitter = Mathf.Clamp(valueJitter, 0f, 0.30f);

        hardness = Mathf.Max(0, injectedHardness);

        configured = true;
        ApplyBaseVisual();
    }

    public void ExtendLife(float extraSeconds)
    {
        if (extraSeconds <= 0f) return;

        float guaranteed = timer + extraSeconds;
        if (lifeTime < guaranteed)
        {
            lifeTime = guaranteed;
        }
    }

    public void ApplyCircleVisual()
    {
        if (sr == null) return;
        if (circleVisualApplied) return;

        // ベース色（円確定前の見た目）をStrokeから取得
        Color baseColor;
        bool hasStrokeColor = TryGetStrokeBaseRenderedColor(out baseColor);
        if (!hasStrokeColor)
        {
            baseColor = (lineType == LineType.RedAccel) ? circleFallbackRed : circleFallbackNormal;
        }

        // --- 濃さ：HSVで「彩度↑ + 明度↓」に寄せる（白飛びしない） ---
        Color c = BoostColorForCircle(baseColor);

        // alpha
        if (circleForceAlpha >= 0f) c.a = circleForceAlpha;
        else if (c.a <= 0f) c.a = 1f;

        sr.color = c;

        // --- 太さ：Dotを少し大きくする（円確定時1回だけ） ---
        float s = Mathf.Max(1f, circleThicknessScale);
        transform.localScale = initialLocalScale * s;

        circleVisualApplied = true;
    }

    private Color BoostColorForCircle(Color baseColor)
    {
        float h, s, v;
        Color.RGBToHSV(baseColor, out h, out s, out v);

        float satMul = Mathf.Clamp(circleSaturationBoost, 1.0f, 2.5f);
        float valMul = Mathf.Clamp(circleValueMultiply, 0.20f, 1.20f);

        // 追加ジッター（円確定時1回）
        float j = Mathf.Clamp(circleExtraValueJitter, 0f, 0.30f);
        if (j > 0f)
        {
            valMul *= (1f + Random.Range(-j, j));
        }

        s = Mathf.Clamp01(s * satMul);
        v = Mathf.Clamp01(v * valMul);

        Color c = Color.HSVToRGB(h, s, v);

        // 元のalphaを一旦引き継ぐ（forceは後で処理）
        c.a = baseColor.a;
        return c;
    }

    private void CacheStrokeBaseRenderedColorOnce()
    {
        if (!configured) return;
        if (sr == null) return;
        if (parentStroke == null) return;

        if (!strokeBaseRenderedColorCache.ContainsKey(parentStroke))
        {
            strokeBaseRenderedColorCache[parentStroke] = sr.color;
        }
    }

    private bool TryGetStrokeBaseRenderedColor(out Color c)
    {
        c = default;

        if (parentStroke == null) return false;

        if (strokeBaseRenderedColorCache.TryGetValue(parentStroke, out c))
        {
            if (c.a <= 0f) c.a = 1f;
            return true;
        }

        if (sr != null)
        {
            c = sr.color;
            if (c.a <= 0f) c.a = 1f;
            strokeBaseRenderedColorCache[parentStroke] = c;
            return true;
        }

        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null) return;

        EnemyBullet bullet = collision.collider.GetComponentInParent<EnemyBullet>();
        if (bullet == null && collision.rigidbody != null)
            bullet = collision.rigidbody.GetComponent<EnemyBullet>();
        if (bullet == null)
            bullet = collision.gameObject.GetComponent<EnemyBullet>();

        if (bullet == null) return;

        // =========================================================
        // 貫通 vs 硬度 判定
        // =========================================================
        BulletPenetration pen = bullet.GetComponent<BulletPenetration>();

        // ★追加（案Aの本体）：
        // すでに「このフレームで別Dotが貫通処理を走らせた弾」は、
        // 同フレーム内で REFLECT/Just に入るのを禁止する。
        if (pen != null && pen.WasPenetratedThisFrame)
        {
            Collider2D myCol = GetComponent<Collider2D>();
            Collider2D bulletCol = collision.collider;
            if (myCol != null && bulletCol != null)
            {
                Physics2D.IgnoreCollision(myCol, bulletCol, true);
            }
            return;
        }

        int pVal = (pen != null) ? pen.Penetration : 0;
        int hVal = Mathf.Max(0, hardness);

        if (pVal > hVal)
        {
            // ★追加：このフレームで貫通したことを記録（同フレーム混在防止）
            pen?.MarkPenetratedThisFrame();
            bullet.NotifyPenetratedLine();

            // 直進維持（衝突前速度に戻す）
            pen?.RestorePreCollisionVelocity();

            // Dot と弾の当たりはこれ以降無視（同フレーム多段事故の保険）
            Collider2D myCol = GetComponent<Collider2D>();
            Collider2D bulletCol = collision.collider;
            if (myCol != null && bulletCol != null)
            {
                Physics2D.IgnoreCollision(myCol, bulletCol, true);
            }

            // ★LineBreak通知（SE/VFXを描画状態と独立させる）
            Vector3 hitPoint = transform.position;
            if (collision.contactCount > 0)
            {
                hitPoint = collision.GetContact(0).point;
            }
            PaddleDrawer.Instance?.NotifyLineBreakThisFrame(lineType, hitPoint);

            // 線（Stroke）を破断（1本単位）
            if (parentStroke != null)
            {
                PaddleDrawer.Instance?.ForceBreakStroke(parentStroke, lineType, hitPoint);
            }
            else
            {
                // 親Strokeが取れない場合の最低限：このDotは消す
                Destroy(gameObject);
            }

            // 壊れた時だけ貫通値 -1（同フレーム多段でも1回だけ）
            pen?.ConsumeOnBreakOnce();

            return; // ★反射処理は一切しない
        }

        // =========================================================
        // ここから先は「反射」：既存仕様どおり
        // =========================================================

        // ホバー中弾：反射せず消滅（SE・VFXあり）
        if (bullet.DestroyOnLineHit)
        {
            bullet.PlayDestroyFeedbackAndDestroy();
            return;
        }

        // 同一フレーム内で複数のDotが同一弾に衝突した場合、2回目以降をスキップ
        // （線上の隣接Dotが同フレームに一斉衝突することで VFX/SE/加速が多重発火する問題の修正）
        if (!bullet.TryAcquirePaddleReflectThisFrame()) return;

        bullet.SetReflectedByStroke(parentStroke);
        bullet.MarkReflected();
        bullet.RegisterPaddleBounce(lineType);

        // ★煙幕弾の反射処理
        if (bullet.IsSmokeGrenadeActive)
        {
            Vector3 reflectPos = transform.position;
            if (collision.contactCount > 0)
            {
                reflectPos = collision.GetContact(0).point;
            }
            bullet.OnSmokeGrenadeReflected(reflectPos);
        }

        bool isJust = false;
        if (justWindowSeconds > 0f)
        {
            float dt = Time.time - bornTime;
            isJust = (dt <= justWindowSeconds);
            if (isJust)
            {
                bullet.ApplyJustReflect(justDamageMultiplier, lineType);
            }
        }

        SessionStats.AddReflect(isJust);

        PaddleDrawer.Instance?.PlayPaddleHitSE(lineType, isJust);

        Vector3 reflectPoint = transform.position;
        if (collision.contactCount > 0)
        {
            reflectPoint = collision.GetContact(0).point;
        }

        if (isJust)
        {
            PaddleDrawer.Instance?.SpawnJustStarVfx(lineType, reflectPoint);
        }
        else
        {
            Vector2 reflectDir = collision.contactCount > 0
                ? -collision.GetContact(0).normal
                : Vector2.up;
            PaddleDrawer.Instance?.SpawnNormalReflectVfx(lineType, reflectPoint, reflectDir);
        }

        // ★赤線など、実際に加速効果がある（倍率>1.0の）反射だけ加速を適用する。
        // 白線（倍率1.0＝無効果）の反射までApplyAcceleration()を呼ぶと、accelMaxCount（現在1）の枠を
        // 消費してしまい、後で赤線に反射させても二度と加速が乗らなくなってしまうため
        if (accelMultiplierPerHit > 1.0f)
        {
            bullet.ApplyAcceleration(accelMultiplierPerHit, accelMaxCount);
        }
    }

    // =========================================================
    // ★追加：Beam（EnemyBullet/BulletPenetrationを介さない発射体）用の判定入口
    // 上のOnCollisionEnter2Dとは完全に独立した別メソッド（既存の物理衝突処理には一切手を加えない）
    // =========================================================
    /// <summary>
    /// penetration vs hardness判定・Just判定・SE/VFX・統計加算をまとめて行う。
    /// 貫通ならtrueを返す（線は破断済み。Beam側はそのまま直進を継続する）。
    /// 反射ならfalseを返し、Justだった場合はjustDamageMultiplierOutに倍率(&gt;1)を、Normalなら1を返す。
    /// RedAccelの速度加速（ApplyAcceleration相当）は、速度を持たないBeamには意味がないため適用しない。
    /// </summary>
    public bool EvaluateExternalHit(int penetration, Vector3 hitPoint, Vector2 hitNormal, out float justDamageMultiplierOut)
    {
        justDamageMultiplierOut = 1f;

        int pVal = penetration;
        int hVal = Mathf.Max(0, hardness);

        if (pVal > hVal)
        {
            PaddleDrawer.Instance?.NotifyLineBreakThisFrame(lineType, hitPoint);
            if (parentStroke != null)
            {
                PaddleDrawer.Instance?.ForceBreakStroke(parentStroke, lineType, hitPoint);
            }
            else
            {
                Destroy(gameObject);
            }
            return true;
        }

        float dt = Time.time - bornTime;
        bool isJust = (justWindowSeconds > 0f) && (dt <= justWindowSeconds);

        SessionStats.AddReflect(isJust);
        PaddleDrawer.Instance?.PlayPaddleHitSE(lineType, isJust);

        if (isJust)
        {
            PaddleDrawer.Instance?.SpawnJustStarVfx(lineType, hitPoint);
            justDamageMultiplierOut = Mathf.Max(1.0f, justDamageMultiplier);
        }
        else
        {
            PaddleDrawer.Instance?.SpawnNormalReflectVfx(lineType, hitPoint, hitNormal);
        }

        return false;
    }
}
