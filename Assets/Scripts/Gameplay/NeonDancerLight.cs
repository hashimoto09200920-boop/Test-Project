using UnityEngine;

/// <summary>
/// NeonDancerのLight（左右2機）。
/// - Selfheal：未破壊中、Dancerの現在フェーズ最大HP × healPercentPerSecond% を毎秒回復（小数は累積）
/// - 追尾：ライト本体をDancerへ向け、ビーム（BeamRoot > BeamSr）をDancerまで伸ばす
///   （プレイヤー側のStageIntroController.PointBeamAt/InitBeamScaleと同じ計算）
/// - 破壊中（NeonDancerBarrier）：追尾停止・ビーム消灯・回復停止。全快で再開
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NeonDancerBarrier))]
public class NeonDancerLight : MonoBehaviour
{
    [Header("References")]
    [Tooltip("NeonDancer本体（未指定ならAwake時に親から自動取得）")]
    [SerializeField] private NeonDancerController controller;
    [Tooltip("ビームの回転基点（このLightの子 BeamRoot）")]
    [SerializeField] private Transform beamRoot;
    [Tooltip("ビームのSpriteRenderer（BeamRoot > BeamSr）")]
    [SerializeField] private SpriteRenderer beamSr;

    [Header("Self Heal")]
    [Tooltip("1秒あたりの回復量（Dancerの現在フェーズ最大HPに対する%）")]
    [SerializeField] private float healPercentPerSecond = 1f;

    [Header("Tracking")]
    [Tooltip("ライト本体がDancerへ向きを変える速さ（度/秒）。0以下なら即座に向く")]
    [SerializeField] private float turnSpeed = 0f;
    [Tooltip("ビームの起点（ライト位置からのワールドオフセット。プレイヤー側のbeamFaceLocalYに相当）")]
    [SerializeField] private Vector2 beamOriginOffset = Vector2.zero;
    [Tooltip("ビームスプライトのうち、Dancerまで届かせる長さの割合（プレイヤー側のbeamReachRatioと同じ）")]
    [Range(0.3f, 1.0f)] [SerializeField] private float beamReachRatio = 0.7f;

    [Header("Beam Visual")]
    [Tooltip("ビームの不透明度（プレイヤー側の登場演出intro Beam Alphaと同じ0.1が初期値）")]
    [Range(0f, 1f)] [SerializeField] private float beamAlpha = 0.1f;
    [Tooltip("破壊時の消灯／復活時の点灯にかける秒数")]
    [SerializeField] private float beamFadeDuration = 0.3f;

    public NeonDancerBarrier Barrier { get; private set; }
    public bool IsLit => Barrier != null && !Barrier.IsBroken;

    private Rigidbody2D rb;
    private float currentAngle;
    private float healAccumulated;
    private float beamAlphaCurrent;
    private float beamAlphaTarget;

    private static float TimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Awake()
    {
        Barrier = GetComponent<NeonDancerBarrier>();
        rb = GetComponent<Rigidbody2D>();
        if (controller == null) controller = GetComponentInParent<NeonDancerController>();
        currentAngle = transform.eulerAngles.z;

        beamAlphaCurrent = beamAlpha;
        beamAlphaTarget = beamAlpha;
        ApplyBeamAlpha();

        Barrier.OnBarrierBroken += HandleBroken;
        Barrier.OnBarrierRestored += HandleRestored;
    }

    private void OnDestroy()
    {
        if (Barrier != null)
        {
            Barrier.OnBarrierBroken -= HandleBroken;
            Barrier.OnBarrierRestored -= HandleRestored;
        }
    }

    public void SetController(NeonDancerController c) => controller = c;

    // ======================================================
    // Area10 Final Stage登場演出（NeonDancerControllerから呼ばれる）
    // ======================================================

    private float introFadeDurationOverride = -1f;

    /// <summary>登場演出の開始時：ビームを即座に消す</summary>
    public void IntroHideBeam()
    {
        beamAlphaCurrent = 0f;
        beamAlphaTarget = 0f;
        introFadeDurationOverride = -1f;
        ApplyBeamAlpha();
    }

    /// <summary>登場演出の点灯時：ビームを指定秒数でフェードインする（プレイヤー側のスポットライトと同じ秒数）</summary>
    public void IntroBeamOn(float duration)
    {
        introFadeDurationOverride = duration;
        beamAlphaTarget = IsLit ? beamAlpha : 0f;
    }

    private void HandleBroken()
    {
        healAccumulated = 0f;
        beamAlphaTarget = 0f;
    }

    private void HandleRestored()
    {
        beamAlphaTarget = beamAlpha;
    }

    private void Update()
    {
        UpdateSelfHeal();

        // ビームのフェード（演出なので実時間で進める）
        if (!Mathf.Approximately(beamAlphaCurrent, beamAlphaTarget))
        {
            float dur = introFadeDurationOverride >= 0f ? introFadeDurationOverride : beamFadeDuration;
            float speed = dur > 0f ? beamAlpha / dur : float.MaxValue;
            beamAlphaCurrent = Mathf.MoveTowards(beamAlphaCurrent, beamAlphaTarget, speed * Time.deltaTime);
            ApplyBeamAlpha();
            if (Mathf.Approximately(beamAlphaCurrent, beamAlphaTarget)) introFadeDurationOverride = -1f;
        }
    }

    private void UpdateSelfHeal()
    {
        if (!IsLit || controller == null || !controller.CanSelfHeal || controller.IsAtPhaseMaxHp)
        {
            healAccumulated = 0f;
            return;
        }

        healAccumulated += controller.PhaseMaxHp * (healPercentPerSecond / 100f) * Time.deltaTime * TimeScale;
        if (healAccumulated >= 1f)
        {
            int amount = Mathf.FloorToInt(healAccumulated);
            healAccumulated -= amount;
            controller.HealPhaseClamped(amount);
        }
    }

    private void LateUpdate()
    {
        if (controller == null) return;
        Vector3 target = controller.BeamTargetPosition;
        Vector3 origin = transform.position + (Vector3)beamOriginOffset;

        // 破壊中は追尾停止（向きはその時点のまま）
        if (IsLit)
        {
            Vector3 dir = target - origin;
            float desired = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
            currentAngle = turnSpeed > 0f
                ? Mathf.MoveTowardsAngle(currentAngle, desired, turnSpeed * Time.deltaTime * TimeScale)
                : desired;

            // Kinematic Rigidbody2Dはtransform直接代入だとテレポート扱いになるためMoveRotationを使う
            if (rb != null) rb.MoveRotation(currentAngle);
            else transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);
        }

        UpdateBeam(origin, target);
    }

    private void UpdateBeam(Vector3 origin, Vector3 target)
    {
        if (beamRoot == null) return;
        Quaternion rot = Quaternion.Euler(0f, 0f, currentAngle);
        beamRoot.position = origin;
        beamRoot.rotation = rot;

        if (beamSr == null || beamSr.sprite == null) return;
        // ★負荷軽減：ビームが消えている間（破壊中など）は大きさの計算を省く。表示はApplyBeamAlpha（Update）でしか戻らないため、
        //   戻ったフレームのこのLateUpdateで必ず計算し直される（見た目は変わらない）
        if (!beamSr.enabled) return;
        float effectiveH = beamSr.sprite.bounds.size.y * beamReachRatio;
        if (effectiveH <= 0f) return;
        float scale = Vector3.Distance(origin, target) / effectiveH;

        beamRoot.localScale = Vector3.one;
        beamSr.transform.localScale = new Vector3(scale, scale, 1f);
        // スプライト上端（光源）を常にoriginへ固定する
        float topOffset = beamSr.sprite.bounds.max.y * scale;
        beamSr.transform.position = origin - rot * new Vector3(0f, topOffset, 0f);
    }

    private void ApplyBeamAlpha()
    {
        if (beamSr == null) return;
        Color c = beamSr.color; c.a = beamAlphaCurrent;
        beamSr.color = c;
        beamSr.enabled = beamAlphaCurrent > 0.001f;
    }
}
