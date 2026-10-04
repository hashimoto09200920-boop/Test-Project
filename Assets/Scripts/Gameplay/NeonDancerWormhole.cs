using UnityEngine;

/// <summary>
/// NeonDancerの発射演出用ワームホール1個分の見た目。
/// 「出現→回転しながら拡大(溜め)→（発射の瞬間）→縮小して消える」を自分のUpdateで自走する。
/// ★アニメーションをNeonDancerController側のコルーチンに持たせると、ボス撃破時の
///   StopAllCoroutines()で途中のまま固まって画面に残るため、必ずワームホール自身が最後まで進める。
/// プールはNeonDancerControllerがボス個体ごとに持つ（static不使用）。
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerWormhole : MonoBehaviour
{
    [Tooltip("外側の渦（9色に着色・ルートと一緒に回転）")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("重ねる層（任意）")]
    [Tooltip("内側の渦など、9色に着色する追加の層")]
    [SerializeField] private SpriteRenderer[] extraTintedRenderers;
    [Tooltip("中心の穴など、着色せず透明度だけ合わせる層（元の色を保つ）")]
    [SerializeField] private SpriteRenderer[] untintedRenderers;
    [Tooltip("ルートと逆向きに回転させる層（内側の渦）")]
    [SerializeField] private Transform[] counterSpinLayers;
    [Tooltip("逆回転の速さ（ルートの回転速度に対する倍率）")]
    [SerializeField] private float counterSpinRatio = 1.3f;

    private Color[] untintedBaseColors;

    [Header("溜めエフェクト（予告付きの弾＝Beam用。BitのVFX_GyrorbChargeと同じ「光が吸い込まれる」演出）")]
    [Tooltip("溜め中だけ再生するパーティクル（子のChargeGlow）。色はワームホールと同じ色に変える")]
    [SerializeField] private ParticleSystem chargeEffect;
    [Tooltip("溜めエフェクトの色（BitのBeamChargeGlowと同じ紫が初期値）")]
    [SerializeField] private Color chargeEffectColor = new Color(0.85f, 0.5f, 1f, 1f);

    [Tooltip("溜め完了から自動で縮小を始めるまでの秒数（発射の瞬間を見せる余韻）")]
    [SerializeField] private float holdAfterChargeSeconds = 0.05f;

    [Tooltip("Beam等、撃った弾が消えるまで表示し続ける場合の最大保持秒数（紐づけが来なかった時の保険）")]
    [SerializeField] private float maxHoldSeconds = 10f;

    // Beam用：溜め完了後も縮小せず、紐づけた弾（holdTarget）が消えるまで表示し続ける
    private bool awaitingHoldTarget;
    private Object holdTarget;
    private bool hasHoldTarget;

    private enum State { Hidden, Charging, Holding, Shrinking }
    private State state = State.Hidden;

    private float size;
    private float chargeDuration;
    private float shrinkDuration;
    private float spinSpeedStart;
    private float spinSpeedEnd;
    private Color baseColor;
    private float timer;
    private float shrinkStartScale;

    /// <summary>再生中（プールから貸し出し不可）</summary>
    public bool IsBusy => state != State.Hidden;
    /// <summary>溜め（拡大）が完了したか。発射側はこれがtrueになった瞬間に弾を出す</summary>
    public bool IsChargeComplete => state == State.Holding || state == State.Shrinking || state == State.Hidden;

    private static float TimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (untintedRenderers != null)
        {
            untintedBaseColors = new Color[untintedRenderers.Length];
            for (int i = 0; i < untintedRenderers.Length; i++)
                untintedBaseColors[i] = untintedRenderers[i] != null ? untintedRenderers[i].color : Color.black;
        }
        HideImmediate();
    }

    /// <param name="holdUntilReleased">trueなら溜め完了後も縮小せず、HoldWhile()で紐づけた弾が消えるまで表示し続ける（Beam用）</param>
    public void Play(Vector3 worldPos, Color color, float size, float chargeDuration, float shrinkDuration,
                     float spinSpeedStart, float spinSpeedEnd, bool holdUntilReleased = false)
    {
        awaitingHoldTarget = holdUntilReleased;
        holdTarget = null;
        hasHoldTarget = false;

        // ★先にアクティブ化する（非アクティブで生成されていた場合、ここでAwake()のHideImmediate()が走るため、状態設定より前に済ませる）
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        transform.position = worldPos;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        transform.localScale = Vector3.zero;

        this.size = Mathf.Max(0.01f, size);
        this.chargeDuration = Mathf.Max(0.01f, chargeDuration);
        this.shrinkDuration = Mathf.Max(0.01f, shrinkDuration);
        this.spinSpeedStart = spinSpeedStart;
        this.spinSpeedEnd = spinSpeedEnd;
        baseColor = color;
        timer = 0f;
        state = State.Charging;

        SetRenderersEnabled(true);
        SetAlpha(0f);
    }

    private void Update()
    {
        if (state == State.Hidden) return;

        float dt = Time.deltaTime * TimeScale;
        timer += dt;

        switch (state)
        {
            case State.Charging:
            {
                float t = Mathf.Clamp01(timer / chargeDuration);
                float eased = t * t * (3f - 2f * t);
                transform.localScale = Vector3.one * (size * eased);
                Spin(Mathf.Lerp(spinSpeedStart, spinSpeedEnd, t), dt);
                SetAlpha(Mathf.Clamp01(t / 0.2f));
                if (t >= 1f) { state = State.Holding; timer = 0f; }
                break;
            }
            case State.Holding:
            {
                Spin(spinSpeedEnd, dt);
                // Beam用：紐づけ待ち、または紐づけた弾がまだ存在する間は縮小しない（最大保持秒数で必ず打ち切る）
                bool holding = awaitingHoldTarget || (hasHoldTarget && holdTarget != null);
                if (holding && timer < maxHoldSeconds) break;
                if (timer >= holdAfterChargeSeconds) { state = State.Shrinking; timer = 0f; shrinkStartScale = transform.localScale.x; StopChargeEffect(); }
                break;
            }
            case State.Shrinking:
            {
                float t = Mathf.Clamp01(timer / shrinkDuration);
                float eased = t * t;
                transform.localScale = Vector3.one * (shrinkStartScale * (1f - eased));
                Spin(spinSpeedEnd, dt);
                SetAlpha(1f - eased);
                if (t >= 1f) HideImmediate();
                break;
            }
        }
    }

    /// <summary>
    /// Play(holdUntilReleased:true)で出したワームホールに、撃った弾（Beam）を紐づける。
    /// その弾が消える（Destroyされる）まで表示し続け、消えたら縮小する。nullなら即座に通常どおり縮小へ。
    /// </summary>
    /// <summary>溜めエフェクトを再生する（Bitと同じ紫：Charge Effect Color）</summary>
    public void PlayChargeEffect()
    {
        if (chargeEffect == null) return;
        foreach (var ps in chargeEffect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = chargeEffectColor;
        }
        chargeEffect.Play(true);
    }

    /// <summary>溜めエフェクトを即座に消す（発射の瞬間・縮小開始時・非表示時）</summary>
    public void StopChargeEffect()
    {
        if (chargeEffect == null) return;
        chargeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>出現中のワームホールを、溜め・保持の途中でも即座に縮小して消す（後半移行時の片付け用）</summary>
    public void ForceShrink()
    {
        if (state == State.Hidden || state == State.Shrinking) return;
        awaitingHoldTarget = false;
        hasHoldTarget = false;
        holdTarget = null;
        state = State.Shrinking;
        timer = 0f;
        shrinkStartScale = transform.localScale.x;
        StopChargeEffect();
    }

    public void HoldWhile(Object target)
    {
        awaitingHoldTarget = false;
        holdTarget = target;
        hasHoldTarget = target != null;
    }

    private void Spin(float degPerSec, float dt)
    {
        transform.Rotate(0f, 0f, -degPerSec * dt);
        // 内側の渦：ルートの回転を打ち消してさらに逆向きへ回す（見た目はルートと逆回転）
        if (counterSpinLayers != null)
            foreach (var l in counterSpinLayers)
                if (l != null) l.Rotate(0f, 0f, degPerSec * (1f + counterSpinRatio) * dt, Space.Self);
    }

    private void SetAlpha(float a)
    {
        Color c = baseColor; c.a = baseColor.a * a;
        if (spriteRenderer != null) spriteRenderer.color = c;
        if (extraTintedRenderers != null)
            foreach (var r in extraTintedRenderers) if (r != null) r.color = c;
        if (untintedRenderers != null)
            for (int i = 0; i < untintedRenderers.Length; i++)
            {
                if (untintedRenderers[i] == null) continue;
                Color u = untintedBaseColors != null && i < untintedBaseColors.Length ? untintedBaseColors[i] : Color.black;
                u.a *= a;
                untintedRenderers[i].color = u;
            }
    }

    private void SetRenderersEnabled(bool on)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = on;
        if (extraTintedRenderers != null) foreach (var r in extraTintedRenderers) if (r != null) r.enabled = on;
        if (untintedRenderers != null) foreach (var r in untintedRenderers) if (r != null) r.enabled = on;
    }

    private void HideImmediate()
    {
        state = State.Hidden;
        transform.localScale = Vector3.zero;
        SetRenderersEnabled(false);
        StopChargeEffect();
    }
}
