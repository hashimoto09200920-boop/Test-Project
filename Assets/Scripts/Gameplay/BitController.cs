using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// Area08ボス「Obelisk」の護衛ユニット「Bit」専用コントローラー。
// HPはWallHealth（Block方式・反射弾でダメージ）で管理する。破壊後はConfigureRespawnSlotsで
// 外部（ObeliskController）から渡された候補地点の中から再抽選してリスポーンする。
// 移動はMarshalControllerのHover/Burst操舵を参考にしたが、クラス自体は共有せず本ファイルに
// 専用実装している（新エネミーは既存コントローラークラスをそのまま共有しない、というルールのため）。
// =========================================================
[RequireComponent(typeof(WallHealth))]
public class BitController : MonoBehaviour
{
    private enum BitPreviewSprite
    {
        Idle1_Open, Idle2_Half, Idle3_Closed, Idle4_Half, IdleAnimate,
        Charge1, Charge2, Charge3, ChargeAnimate
    }

    [System.Serializable]
    private class BitFrame
    {
        public Sprite sprite;
        public float duration;
        [Tooltip("0より大きい場合、duration の代わりに durationMin〜durationMax のランダムな時間を使う")]
        public float durationMin;
        public float durationMax;
    }

    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [SerializeField] private WallHealth wallHealth;
    [SerializeField] private EnemyBeamBullet beamBulletPrefab;
    [SerializeField] private Transform projectileRoot;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    [Header("Editor Preview（Play前にInspectorでスプライト確認）")]
    [SerializeField] private BitPreviewSprite previewSprite = BitPreviewSprite.Idle1_Open;

    // =========================================================
    // Sprites - Idle（瞬き。1=全開/2=半閉/3=完全閉）
    // =========================================================

    [Header("Sprites - Idle（瞬き。配列順=全開→半閉→完全閉→半閉の順で登録。ループ再生）")]
    [Tooltip("推奨Duration: [0]全開 duration=0(未使用)/durationMin=1.5/durationMax=4（次の瞬きまでランダムに維持）\n" +
             "[1]半閉 duration=0.08（閉じかけの一瞬）\n[2]完全閉 duration=0.06（シャッターが閉じ切る一瞬）\n[3]半閉 duration=0.08（開き直る一瞬。[1]と同じ値でOK）")]
    [NonReorderable]
    [SerializeField] private BitFrame[] idleFrames;

    // =========================================================
    // Movement（MarshalのHover/Burst操舵を参考にした専用実装）
    // =========================================================

    [Header("Movement（Hover / Burst）")]
    [Tooltip("バースト時の基準速度。実際の最高速度はこれにBurst Speed Multiplierを掛けた値")]
    [SerializeField] private float baseMoveSpeed = 0.8f;
    [Tooltip("進行方向のふらつき旋回速度（度/秒）。大きいほど激しく蛇行する")]
    [SerializeField] private float wanderTurnSpeed = 50f;
    [Tooltip("ふらつきの元になるPerlin Noiseのサンプリング速度。大きいほど方向転換が細かく速くなる")]
    [SerializeField] private float wanderNoiseFrequency = 0.3f;
    [Tooltip("ホバリング（ほぼ静止）状態を維持する時間（秒・最小）")]
    [SerializeField] private float hoverDurationMin = 0.6f;
    [Tooltip("ホバリング（ほぼ静止）状態を維持する時間（秒・最大）")]
    [SerializeField] private float hoverDurationMax = 1.4f;
    [Tooltip("バースト（加速移動）状態を維持する時間（秒・最小）")]
    [SerializeField] private float burstDurationMin = 0.4f;
    [Tooltip("バースト（加速移動）状態を維持する時間（秒・最大）")]
    [SerializeField] private float burstDurationMax = 0.8f;
    [Tooltip("バースト時の最高速度倍率（Base Move Speed × この値 = 最高速度）")]
    [SerializeField] private float burstSpeedMultiplier = 3f;
    [Tooltip("バースト開始時、0から最高速度まで加速しきるのにかかる時間（秒）")]
    [SerializeField] private float burstAccelDuration = 0.3f;
    [Tooltip("ホバリング中の微小な漂い速度（完全静止だと不自然なため）")]
    [SerializeField] private float hoverDriftSpeed = 0.1f;

    [Header("Leash（出現位置からの最大許容距離）")]
    [Tooltip("ホーム位置からこの距離を超えると、超えた分だけホーム方向へ操舵を引き戻す")]
    [SerializeField] private float leashRadius = 1.2f;
    [Tooltip("引き戻し操舵の旋回速度（度/秒）")]
    [SerializeField] private float leashPullTurnSpeed = 240f;
    [Tooltip("ホームからの距離がこの倍率を超えたら強制的にクランプする安全弁（leashRadius×この値）")]
    [SerializeField] private float leashHardClampMultiplier = 1.15f;

    [Header("Body Avoidance（Obelisk本体方向への接近を避ける）")]
    [Tooltip("本体からこの距離より近づくと、本体から離れる方向へ操舵する")]
    [SerializeField] private float bodyAvoidDistance = 1.5f;
    [Tooltip("本体回避の旋回速度（度/秒）")]
    [SerializeField] private float bodyAvoidTurnSpeed = 200f;

    // =========================================================
    // Rotation Burst（周期的にランダムでCW/CCW・1〜2回転するバースト自転）
    // =========================================================

    [Header("Rotation Burst（周期的な自転バースト）")]
    [Tooltip("回転バーストとバーストの間、静止している時間（秒・最小）。この間隔ごとに1回転バーストが発生する")]
    [SerializeField] private float rotationIdleDurationMin = 3f;
    [Tooltip("回転バーストとバーストの間、静止している時間（秒・最大）")]
    [SerializeField] private float rotationIdleDurationMax = 6f;
    [Tooltip("回転バースト中の自転速度（度/秒・最小）。バースト毎にこの範囲内でランダムに決まる")]
    [SerializeField] private float rotationBurstSpeedDegPerSecMin = 200f;
    [Tooltip("回転バースト中の自転速度（度/秒・最大）")]
    [SerializeField] private float rotationBurstSpeedDegPerSecMax = 540f;
    [Tooltip("1回転(360°)ではなく2回転(720°)になる確率（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float doubleRotationChance = 0.35f;

    [Header("Mechanical Tilt Jitter（回転バーストとは別の、不規則で機械的な小刻みの傾き）")]
    [Tooltip("次のジッターまでの待機時間（秒・最小）")]
    [SerializeField] private float tiltJitterIntervalMin = 0.3f;
    [Tooltip("次のジッターまでの待機時間（秒・最大）")]
    [SerializeField] private float tiltJitterIntervalMax = 1.2f;
    [Tooltip("ジッターで取り得る角度の範囲（度）。±この値の範囲でランダムな角度へ動く")]
    [SerializeField] private float tiltJitterAngleRange = 12f;
    [Tooltip("目標角度まで動く速さ（度/秒）。大きいほどカクッと機械的な動きになる")]
    [SerializeField] private float tiltJitterSnapSpeed = 720f;

    // =========================================================
    // Beam Attack（薄紫・細め・長寿命・低ヒット率のBeam）
    // =========================================================

    [Header("Sprites - Beam Charge（発射前の溜め。配列順=再生順で登録。3枚推奨）")]
    [NonReorderable]
    [SerializeField] private BitFrame[] beamChargeFrames;

    [Tooltip("溜め中のみ再生されるエフェクトのルートTransform（目の周り想定）。子孫の全Particle Systemがまとめて再生/停止される")]
    [SerializeField] private Transform beamChargeGlowRoot;

    [Header("Beam Attack")]
    [Tooltip("Aim Mode / Beam Ignore Player / Beam Width・Color・Lifetime・Damage Tick Rate / Fire Vfx Prefab / Beam Spark Particle Prefab 等、Beamの全設定はここで行う（EnemyDataアセットは使用しない）")]
    [SerializeField]
    private EnemyData.BulletType beamBulletType = new EnemyData.BulletType
    {
        name = "BitBeam",
        useBeam = true,
        beamWidth = 0.08f,
        beamColor = new Color(0.85f, 0.5f, 1f, 1f),
        beamColorEnd = new Color(0.45f, 0.05f, 0.75f, 1f),
        beamIgnorePlayer = true,
        beamGrowDuration = 0.15f,
        beamFadeOutDuration = 0.2f,
        beamDamageTickRate = 1.5f,
        lifeTime = 3f,
        damage = 1,
    };
    [Tooltip("発射間隔（秒・最小）。1回の攻撃サイクル（溜め→発射→ビームのLife Time終了）が終わってから、次に溜め始めるまでの間隔")]
    [SerializeField] private float fireIntervalMin = 2.5f;
    [Tooltip("発射間隔（秒・最大）")]
    [SerializeField] private float fireIntervalMax = 4f;
    [Tooltip("発射時のSE")]
    [SerializeField] private AudioClip fireSE;
    [Tooltip("発射SEの音量")]
    [Range(0f, 1f)] [SerializeField] private float fireSEVolume = 1f;
    [Tooltip("ビーム溜めアニメーション開始時のSE")]
    [SerializeField] private AudioClip chargeStartSE;
    [Tooltip("溜め開始SEの音量")]
    [Range(0f, 1f)] [SerializeField] private float chargeStartSEVolume = 1f;

    [Header("Phase2: Doubleビーム（Dragonと同じ考え方。Phase2の間だけ50%の確率でシングルの代わりに発射）")]
    [Tooltip("ObeliskController側からPhase2移行時にtrueにされる")]
    [SerializeField] private bool phase2BeamVariantsEnabled = false;
    [Tooltip("Doubleビームの左右角度差（度）")]
    [SerializeField] private float doubleBeamAngleOffsetDeg = 10f;
    [Tooltip("Doubleビームの1発目と2発目の間隔（秒）")]
    [SerializeField] private float doubleBeamInterval = 0.15f;

    [Header("Respawn")]
    [Tooltip("破壊されてからリスポーンするまでの秒数")]
    [SerializeField] private float respawnDelay = 4f;

    [Header("Spawn Fade In")]
    [Tooltip("出現時（最初のスポーン時のみ。リスポーン時はフェード無し）のフェードイン秒数。0以下で無効")]
    [SerializeField] private float spawnFadeInDuration = 0.5f;

    /// <summary>リスポーンで再出現した瞬間に発火（ObeliskController側でSE再生等に使用）</summary>
    public event System.Action<BitController> OnRespawned;

    // =========================================================
    // インスタンス変数
    // =========================================================

    private PixelDancerController player;
    private Transform obeliskBodyTransform;

    private Vector2 homePosition;
    private Vector2[] respawnSlotCandidates;

    private bool isBroken;

    private Coroutine fadeInCoroutine;

    private float headingDeg;
    private float noiseSeed;
    private float hoverBurstTimer;
    private bool isBursting;
    private float currentMoveSpeed;

    private bool isRotationBursting;
    private float tiltJitterTimer;
    private float tiltTargetAngle;
    private float currentTiltAngle;

    private int idleFrameIndex;
    private float idleFrameTimer;
    private float idleFrameCurrentDuration;

    private bool isFiring;
    // Doubleビーム等、同時に複数本発射され得るため単一参照ではなくリストで追跡する
    // （単一参照だと2発目を撃った瞬間に1発目の参照が上書きされ、後始末できなくなる）
    private readonly List<EnemyBeamBullet> activeBeams = new List<EnemyBeamBullet>();

    private Coroutine fireCoroutine;
    private Coroutine rotationCoroutine;
    private Coroutine respawnCoroutine;

    // =========================================================
    // 外部（ObeliskController）からの設定
    // =========================================================

    /// <summary>破壊後リスポーン時にランダムで選ばれる候補地点（同じペア内での再抽選用）</summary>
    public void ConfigureRespawnSlots(Vector2[] candidates)
    {
        respawnSlotCandidates = candidates;
    }

    /// <summary>Obelisk本体のTransform。本体方向への接近回避（Body Avoidance）に使う</summary>
    public void ConfigureBodyTransform(Transform body)
    {
        obeliskBodyTransform = body;
    }

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        if (wallHealth == null) wallHealth = GetComponent<WallHealth>();
        if (bodySpriteRenderer == null) bodySpriteRenderer = GetComponent<SpriteRenderer>();

        if (projectileRoot == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) projectileRoot = pr.transform;
        }
    }

    private void Start()
    {
        player = FindObjectOfType<PixelDancerController>();
    }

    private void OnEnable()
    {
        isBroken = false;
        isFiring = false;
        activeBeams.Clear();
        homePosition = transform.position;

        noiseSeed = Random.Range(0f, 1000f);
        headingDeg = Random.Range(0f, 360f);
        isBursting = false;
        hoverBurstTimer = Random.Range(hoverDurationMin, hoverDurationMax);
        currentMoveSpeed = hoverDriftSpeed;

        isRotationBursting = false;
        currentTiltAngle = 0f;
        tiltTargetAngle = 0f;
        tiltJitterTimer = 0f;
        transform.localRotation = Quaternion.identity;

        idleFrameIndex = 0;
        idleFrameTimer = 0f;
        ApplyIdleFrame(0);

        if (wallHealth != null) wallHealth.OnBroken += HandleBroken;

        StartLoops();

        if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);
        fadeInCoroutine = StartCoroutine(FadeInBody());

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    private void OnDisable()
    {
        if (wallHealth != null) wallHealth.OnBroken -= HandleBroken;
        StopLoops();

        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        StopEditorAnim();
#endif
    }

    // 出現時（最初のスポーンのみ）のフェードイン。リスポーン時は呼ばれない（OnEnableが再実行されないため）
    private IEnumerator FadeInBody()
    {
        if (bodySpriteRenderer != null)
        {
            Color initial = bodySpriteRenderer.color;
            bodySpriteRenderer.color = new Color(initial.r, initial.g, initial.b, 0f);
        }

        yield return null;

        if (bodySpriteRenderer == null || spawnFadeInDuration <= 0f) yield break;

        Color original = bodySpriteRenderer.color;
        float elapsed = 0f;
        while (elapsed < spawnFadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / spawnFadeInDuration);
            bodySpriteRenderer.color = new Color(original.r, original.g, original.b, alpha);
            yield return null;
        }

        bodySpriteRenderer.color = new Color(original.r, original.g, original.b, 1f);
    }

    private void Update()
    {
        if (player == null) player = FindObjectOfType<PixelDancerController>();

        float dt = Time.deltaTime;

        if (isBroken) return;

        // Beam発射中（溜め〜ビームのLifetimeが残っている間）は移動せずその場に停滞する
        bool suppressMoveForFiring = isFiring || activeBeams.Count > 0;
        if (!suppressMoveForFiring) ApplyMove(dt);

        if (!isFiring) TickIdleFrames(dt);

        TickTiltJitter(dt);
    }

    // =========================================================
    // 破壊 → リスポーン
    // =========================================================

    private void HandleBroken(Vector3 hitPos)
    {
        if (isBroken) return;

        isBroken = true;
        StopLoops();
        SetBeamChargeGlowVisible(false);

        DestroyActiveBeam();

        if (showDebugLog) Debug.Log($"[BitController] {name} Broken. Respawn in {respawnDelay}s", this);

        if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
        respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    // 発射済みのBeamはBit本体とは別のGameObjectとして自分の寿命で動き続けるため、
    // Bitが壊れた（またはBit自体が何らかの理由で破棄された）タイミングで明示的に後始末する。
    // Destroy()はそのフレームの終わりまで実際には破棄されないため、内部のダメージ判定コルーチンが
    // 同じフレーム中にもう1回だけ実行されてしまうことがある。StopAllCoroutines()で先に止めておく
    private void DestroyActiveBeam()
    {
        foreach (EnemyBeamBullet beam in activeBeams)
        {
            if (beam == null) continue;
            beam.StopAllCoroutines();
            Destroy(beam.gameObject);
        }
        activeBeams.Clear();
    }

    // activeBeamsから既に破棄されたBeamの参照を取り除いた上で、まだ生きているBeamが残っているか返す
    private bool HasActiveBeams()
    {
        activeBeams.RemoveAll(b => b == null);
        return activeBeams.Count > 0;
    }

    private void OnDestroy()
    {
        DestroyActiveBeam();
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        // 同じペア内でスロットを再抽選
        if (respawnSlotCandidates != null && respawnSlotCandidates.Length > 0)
        {
            Vector2 slot = respawnSlotCandidates[Random.Range(0, respawnSlotCandidates.Length)];
            transform.position = new Vector3(slot.x, slot.y, transform.position.z);
            homePosition = slot;
        }
        else
        {
            homePosition = transform.position;
        }

        if (wallHealth != null) wallHealth.ResetHealth();

        isBroken = false;
        isFiring = false;
        activeBeams.Clear();
        headingDeg = Random.Range(0f, 360f);
        isBursting = false;
        hoverBurstTimer = Random.Range(hoverDurationMin, hoverDurationMax);
        currentMoveSpeed = hoverDriftSpeed;

        isRotationBursting = false;
        currentTiltAngle = 0f;
        tiltTargetAngle = 0f;
        tiltJitterTimer = 0f;
        transform.localRotation = Quaternion.identity;

        idleFrameIndex = 0;
        idleFrameTimer = 0f;
        ApplyIdleFrame(0);
        StartLoops();

        OnRespawned?.Invoke(this);

        if (showDebugLog) Debug.Log($"[BitController] {name} Respawned at {homePosition}", this);
    }

    private void StartLoops()
    {
        StopLoops();
        fireCoroutine = StartCoroutine(FireLoop());
        rotationCoroutine = StartCoroutine(RotationBurstLoop());
    }

    private void StopLoops()
    {
        if (fireCoroutine != null) { StopCoroutine(fireCoroutine); fireCoroutine = null; }
        if (rotationCoroutine != null) { StopCoroutine(rotationCoroutine); rotationCoroutine = null; }
    }

    // =========================================================
    // Rotation Burst（待機→ランダムでCW/CCW・1〜2回転→また待機、のループ）
    // =========================================================

    private IEnumerator RotationBurstLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(rotationIdleDurationMin, rotationIdleDurationMax));

            isRotationBursting = true;

            float totalDeg = (Random.value < doubleRotationChance) ? 720f : 360f;
            // rotationSpeedと同じ規約：正=時計回り（Unityの座標系ではZの負方向が時計回り）
            float dirSign = (Random.value < 0.5f) ? 1f : -1f;
            // バースト毎にランダムな速度（早い時・遅い時ができる）
            float burstSpeed = Random.Range(rotationBurstSpeedDegPerSecMin, rotationBurstSpeedDegPerSecMax);
            float rotated = 0f;

            while (rotated < totalDeg)
            {
                float step = Mathf.Min(burstSpeed * Time.deltaTime, totalDeg - rotated);
                transform.Rotate(0f, 0f, -dirSign * step);
                rotated += step;
                yield return null;
            }

            // Tilt Jitterと同じTransformを使うため、バースト終了時点の実角度に同期させてから引き渡す
            // （同期しないと、次のジッターが古い目標角度へ一瞬で戻ろうとして不自然にスナップする）
            currentTiltAngle = NormalizeAngle(transform.localEulerAngles.z);
            tiltTargetAngle = currentTiltAngle;
            isRotationBursting = false;
        }
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }

    // =========================================================
    // Mechanical Tilt Jitter（回転バースト中でない間、不規則な間隔・角度で小刻みに傾く）
    // =========================================================

    private void TickTiltJitter(float dt)
    {
        if (isRotationBursting) return;

        tiltJitterTimer -= dt;
        if (tiltJitterTimer <= 0f)
        {
            tiltJitterTimer = Random.Range(tiltJitterIntervalMin, tiltJitterIntervalMax);
            tiltTargetAngle = Random.Range(-tiltJitterAngleRange, tiltJitterAngleRange);
        }

        currentTiltAngle = Mathf.MoveTowardsAngle(currentTiltAngle, tiltTargetAngle, tiltJitterSnapSpeed * dt);
        transform.localRotation = Quaternion.Euler(0f, 0f, currentTiltAngle);
    }

    // =========================================================
    // Idle（瞬き。配列を全開→半閉→完全閉→半閉→(ループで全開に戻る)の順にループ再生）
    // =========================================================

    private void TickIdleFrames(float dt)
    {
        if (idleFrames == null || idleFrames.Length == 0) return;

        idleFrameTimer += dt;
        if (idleFrameTimer >= idleFrameCurrentDuration)
        {
            idleFrameTimer -= idleFrameCurrentDuration;
            idleFrameIndex++;
            ApplyIdleFrame(idleFrameIndex);
        }
    }

    private void ApplyIdleFrame(int index)
    {
        if (idleFrames == null || idleFrames.Length == 0) return;
        int len = idleFrames.Length;
        BitFrame f = idleFrames[((index % len) + len) % len];
        idleFrameCurrentDuration = FrameDurationOr(f, 0.2f);
        ApplySprite(f != null ? f.sprite : null);
    }

    private static float FrameDurationOr(BitFrame f, float fallback)
    {
        if (f == null) return fallback;
        if (f.durationMax > 0f)
        {
            float lo = Mathf.Min(f.durationMin, f.durationMax);
            float hi = Mathf.Max(f.durationMin, f.durationMax);
            return Random.Range(lo, hi);
        }
        return f.duration > 0f ? f.duration : fallback;
    }

    private void ApplySprite(Sprite s)
    {
        if (bodySpriteRenderer != null && s != null) bodySpriteRenderer.sprite = s;
    }

    // =========================================================
    // Beam Attack
    // =========================================================

    private IEnumerator FireLoop()
    {
        yield return new WaitForSeconds(Random.Range(fireIntervalMin, fireIntervalMax));
        while (true)
        {
            // 別コルーチンとしてStartCoroutineせず、ここで直接yieldする（StopCoroutine(fireCoroutine)で
            // 溜め〜発射待機まで含めて確実に中断できるようにするため）
            yield return RunBeamAttack();
            yield return new WaitForSeconds(Random.Range(fireIntervalMin, fireIntervalMax));
        }
    }

    private IEnumerator RunBeamAttack()
    {
        isFiring = true;
        SetBeamChargeGlowVisible(true);
        if (chargeStartSE != null) PlayFireSE(chargeStartSE, chargeStartSEVolume, transform.position);

        if (beamChargeFrames != null)
        {
            for (int i = 0; i < beamChargeFrames.Length; i++)
            {
                BitFrame f = beamChargeFrames[i];
                ApplySprite(f != null ? f.sprite : null);
                yield return new WaitForSeconds(FrameDurationOr(f, 0.1f));
            }
        }

        SetBeamChargeGlowVisible(false);

        if (phase2BeamVariantsEnabled && Random.value < 0.5f)
        {
            // Phase2：50%の確率でDoubleビームを撃つ（Dragonと同じ考え方）
            yield return StartCoroutine(RunDoubleBeam());
        }
        else
        {
            FireBeamOnce(0f);

            // 実際のビームのLifetime（フェードアウト込み）が尽きて破棄されるまで停滞し続ける
            yield return null;
            while (HasActiveBeams())
            {
                yield return null;
            }
        }

        isFiring = false;
        idleFrameIndex = 0;
        idleFrameTimer = 0f;
        ApplyIdleFrame(0);
    }

    // ダブルビーム：1発目と2発目を角度をずらして連続発射する（重なって相殺して見えるのを防ぐ。Dragonと同じ考え方）
    private IEnumerator RunDoubleBeam()
    {
        float half = doubleBeamAngleOffsetDeg * 0.5f;

        FireBeamOnce(-half);
        yield return new WaitForSeconds(doubleBeamInterval);

        if (!isBroken) FireBeamOnce(half);

        yield return null;
        while (HasActiveBeams())
        {
            yield return null;
        }
    }

    /// <summary>ObeliskController側からPhase2移行時に呼ばれる</summary>
    public void SetPhase2BeamVariantsEnabled(bool enabled)
    {
        phase2BeamVariantsEnabled = enabled;
    }

    private void FireBeamOnce(float angleOffsetDeg)
    {
        if (beamBulletPrefab == null || player == null) return;

        Vector3 spawnPos = transform.position;
        Vector2 dir = ComputeAimDirection(spawnPos);
        if (Mathf.Abs(angleOffsetDeg) > 0.0001f) dir = RotateDir(dir, angleOffsetDeg);

        Collider2D[] ownerColliders = GetComponentsInChildren<Collider2D>();
        EnemyBeamBullet newBeam = EnemyShooter.SpawnBeamBullet(beamBulletPrefab, spawnPos, dir, beamBulletType, ownerColliders, projectileRoot);

        // ★再入対策：SpawnBeamBullet内のFire()が、発射直後に至近距離で反射して即座に自分自身
        // （発射元）のWallHealthを破壊することがある。その場合、HandleBroken()はこの後のactiveBeams.Add()
        // より前に同期的に実行されてしまい、その時点ではリストにまだ入っていないため後始末できない。
        // ここでisBrokenを再チェックし、既に壊れているなら生成直後のこのBeamを直接破棄する
        // （Doubleビームで既に追跡中の別のBeamが残っていた場合はDestroyActiveBeamでまとめて後始末する）
        if (isBroken)
        {
            if (newBeam != null)
            {
                newBeam.StopAllCoroutines();
                Destroy(newBeam.gameObject);
            }
            DestroyActiveBeam();
            return;
        }

        if (newBeam != null) activeBeams.Add(newBeam);

        if (newBeam != null && fireSE != null) PlayFireSE(fireSE, fireSEVolume, spawnPos);
        if (showDebugLog) Debug.Log($"[BitController] {name} FireBeam dir={dir}", this);
    }

    private static Vector2 RotateDir(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float s = Mathf.Sin(rad);
        float c = Mathf.Cos(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // プレイヤーの可動範囲内のランダムな座標を狙う（Dragonの Breath と同じ挙動。常に正確にプレイヤーへ
    // 直撃させるのではなく、Floor付近を含めた範囲を狙う。beamIgnorePlayerと合わせて使うことで、
    // 途中でプレイヤーに当たっても止まらずFloorまで届く）
    private Vector2 ComputeAimDirection(Vector3 spawnPos)
    {
        Vector2 fallback = Vector2.down;
        if (player == null) return fallback;

        float range = player.AutoMoveRange;
        float targetX = player.transform.position.x + Random.Range(-range, range);
        Vector2 target = new Vector2(targetX, player.transform.position.y);
        Vector2 dir = target - (Vector2)spawnPos;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : fallback;
    }

    private void PlayFireSE(AudioClip clip, float volume, Vector3 pos)
    {
        if (clip == null) return;
        float vol = volume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        AudioSource.PlayClipAtPoint(clip, pos, vol);
    }

    // パーティクルはSpriteRendererのenabledのような単純なON/OFFが無いため、
    // Stop(StopEmittingAndClear)で即座に消し、Play()で即座に出す。
    // beamChargeGlowRoot配下にある全Particle System（複数を想定）へまとめて適用する
    private void SetBeamChargeGlowVisible(bool visible)
    {
        if (beamChargeGlowRoot == null) return;
        foreach (ParticleSystem ps in beamChargeGlowRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (visible) ps.Play(true);
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

#if UNITY_EDITOR
    // ★追加：VFX_GyrorbCharge.prefabを自身の配下に複製配置し、Play On Awake OFF・紫への色変更・
    // Beam Charge Glow Rootへのアサインまで自動で行う（Editor専用、PrefabUtility.InstantiatePrefabで
    // 元プレハブとのリンクを保ったまま生成する）
    [ContextMenu("Setup Beam Charge Glow (VFX_GyrorbChargeから自動生成)")]
    private void SetupBeamChargeGlow()
    {
        // 既に生成済みなら一旦削除してから作り直す（再実行しても安全にするため）
        Transform existing = transform.Find("BeamChargeGlow");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        const string prefabPath = "Assets/Prefabs/Effects/VFX_GyrorbCharge.prefab";
        GameObject glowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (glowPrefab == null)
        {
            Debug.LogError($"[BitController] SetupBeamChargeGlow: {prefabPath} が見つかりません", this);
            return;
        }

        GameObject instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(glowPrefab, transform);
        instance.name = "BeamChargeGlow";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        // Bitのビーム色と同じ紫に統一し、Play On AwakeをOFF（コード側でPlay/Stopを制御するため）
        Color chargeColor = new Color(0.85f, 0.5f, 1f, 1f);
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.startColor = chargeColor;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        beamChargeGlowRoot = instance.transform;

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[BitController] SetupBeamChargeGlow: BeamChargeGlowを生成し、Beam Charge Glow Rootにアサインしました", this);
    }
#endif

    // =========================================================
    // Movement（ふわふわ操舵Hover ⇔ 加速Burst + ホーム位置へのLeash）
    // =========================================================

    private void ApplyMove(float dt)
    {
        hoverBurstTimer -= dt;
        if (hoverBurstTimer <= 0f)
        {
            isBursting = !isBursting;
            if (isBursting)
            {
                hoverBurstTimer = Random.Range(burstDurationMin, burstDurationMax);
                headingDeg = Random.Range(0f, 360f);
            }
            else
            {
                hoverBurstTimer = Random.Range(hoverDurationMin, hoverDurationMax);
            }
        }

        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * wanderNoiseFrequency) * 2f - 1f;
        headingDeg += noise * wanderTurnSpeed * dt;

        // Leash：ホームから離れすぎたら引き戻す方向へ操舵
        Vector2 toHome = homePosition - (Vector2)transform.position;
        float distFromHome = toHome.magnitude;
        if (distFromHome > leashRadius)
        {
            float pullAngle = Mathf.Atan2(toHome.y, toHome.x) * Mathf.Rad2Deg;
            headingDeg = Mathf.MoveTowardsAngle(headingDeg, pullAngle, leashPullTurnSpeed * dt);
        }

        // Body Avoidance：Obelisk本体に近づきすぎたら離れる方向へ操舵（Leashより後に適用し優先させる）
        if (obeliskBodyTransform != null)
        {
            Vector2 fromBody = (Vector2)transform.position - (Vector2)obeliskBodyTransform.position;
            float distFromBody = fromBody.magnitude;
            if (distFromBody < bodyAvoidDistance)
            {
                float avoidAngle = Mathf.Atan2(fromBody.y, fromBody.x) * Mathf.Rad2Deg;
                headingDeg = Mathf.MoveTowardsAngle(headingDeg, avoidAngle, bodyAvoidTurnSpeed * dt);
            }
        }

        float burstTopSpeed = baseMoveSpeed * burstSpeedMultiplier;
        float targetSpeed = isBursting ? burstTopSpeed : hoverDriftSpeed;
        float accelPerSecond = burstTopSpeed / Mathf.Max(0.01f, burstAccelDuration);
        currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, accelPerSecond * dt);

        Vector2 moveDir = new Vector2(Mathf.Cos(headingDeg * Mathf.Deg2Rad), Mathf.Sin(headingDeg * Mathf.Deg2Rad));
        Vector3 pos = transform.position + (Vector3)(moveDir * currentMoveSpeed * dt);

        // 安全弁：バースト等で瞬間的にleashRadiusを大きく超えないよう強制クランプ
        Vector2 fromHome = (Vector2)pos - homePosition;
        float hardLimit = leashRadius * Mathf.Max(1f, leashHardClampMultiplier);
        if (fromHome.magnitude > hardLimit)
        {
            fromHome = fromHome.normalized * hardLimit;
            pos = new Vector3(homePosition.x + fromHome.x, homePosition.y + fromHome.y, pos.z);
        }

        transform.position = pos;
    }

    // =========================================================
    // Editor Preview
    // =========================================================

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (bodySpriteRenderer == null) bodySpriteRenderer = GetComponent<SpriteRenderer>();
        if (bodySpriteRenderer == null) return;

#if UNITY_EDITOR
        if (previewSprite == BitPreviewSprite.IdleAnimate)
        {
            StartEditorAnim(0);
            return;
        }
        if (previewSprite == BitPreviewSprite.ChargeAnimate)
        {
            StartEditorAnim(1);
            return;
        }
        StopEditorAnim();
#endif

        switch (previewSprite)
        {
            case BitPreviewSprite.Idle1_Open: ApplySprite(GetArrayFrame(idleFrames, 0)); break;
            case BitPreviewSprite.Idle2_Half: ApplySprite(GetArrayFrame(idleFrames, 1)); break;
            case BitPreviewSprite.Idle3_Closed: ApplySprite(GetArrayFrame(idleFrames, 2)); break;
            case BitPreviewSprite.Idle4_Half: ApplySprite(GetArrayFrame(idleFrames, 3)); break;
            case BitPreviewSprite.Charge1: ApplySprite(GetArrayFrame(beamChargeFrames, 0)); break;
            case BitPreviewSprite.Charge2: ApplySprite(GetArrayFrame(beamChargeFrames, 1)); break;
            case BitPreviewSprite.Charge3: ApplySprite(GetArrayFrame(beamChargeFrames, 2)); break;
        }
    }

    private static BitFrame GetArrayFrame(BitFrame[] arr, int index)
    {
        if (arr == null || index < 0 || index >= arr.Length) return null;
        return arr[index];
    }

    private void ApplySprite(BitFrame f)
    {
        if (f != null) ApplySprite(f.sprite);
    }

#if UNITY_EDITOR
    private bool _editorAnimRunning;
    private double _editorAnimLastTime;
    private int _editorAnimFrameIdx;
    private int _editorAnimType; // 0=Idle, 1=Charge

    private void StartEditorAnim(int animType)
    {
        _editorAnimType = animType;
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        var frames = GetEditorAnimFrames();
        if (frames != null && frames.Length > 0) ApplySprite(frames[0]);
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private BitFrame[] GetEditorAnimFrames()
    {
        switch (_editorAnimType)
        {
            case 0: return idleFrames;
            default: return beamChargeFrames;
        }
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning) { StopEditorAnim(); return; }

        var frames = GetEditorAnimFrames();
        if (frames == null || frames.Length == 0) { StopEditorAnim(); return; }

        double dur = Mathf.Max(0.01f, FrameDurationOr(frames[_editorAnimFrameIdx % frames.Length], 0.2f));
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % frames.Length;
            ApplySprite(frames[_editorAnimFrameIdx]);
            UnityEditor.SceneView.RepaintAll();
        }
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
        {
            StopEditorAnim();
        }
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 0.9f, 0.6f);
        Vector3 center = Application.isPlaying ? (Vector3)homePosition : transform.position;
        Gizmos.DrawWireSphere(center, leashRadius);
    }
}
