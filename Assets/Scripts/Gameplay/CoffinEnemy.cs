using UnityEngine;

/// <summary>
/// NeonMonster04（棺桶エネミー）の行動制御スクリプト。
/// Idle（円移動）→ Wobble（開く予告）→ Opening → Flying（攻撃）→ Closing → Idle のループ。
/// EnemyMover は suppressMovement=true にして移動を完全にこのスクリプトで管理する。
/// </summary>
[RequireComponent(typeof(EnemyMover))]
public class CoffinEnemy : MonoBehaviour
{
    // =========================================================
    // Circle Movement
    // =========================================================
    [Header("Circle Movement")]
    [Tooltip("円移動の半径（Unity単位）")]
    [SerializeField] private float circleRadius = 1.5f;

    [Tooltip("円移動の速さ（ラジアン/秒）")]
    [SerializeField] private float circleSpeed = 1.2f;

    [Tooltip("円移動の持続時間の最小値（秒）")]
    [SerializeField] private float idleDurationMin = 3f;

    [Tooltip("円移動の持続時間の最大値（秒）")]
    [SerializeField] private float idleDurationMax = 7f;

    // =========================================================
    // Wobble Telegraph
    // =========================================================
    [Header("Wobble Telegraph (開く前の予告揺れ)")]
    [Tooltip("開く前に棺が揺れる時間（秒）")]
    [SerializeField] private float wobbleDuration = 0.4f;

    [Tooltip("棺の揺れ幅（Unity単位）")]
    [SerializeField] private float wobbleIntensity = 0.08f;

    // =========================================================
    // Animation
    // =========================================================
    [Header("Animation Sprites")]
    [Tooltip("棺開アニメのスプライト配列（Sealed01→05の順）")]
    [SerializeField] private Sprite[] openFrames;

    [Tooltip("フライトアニメのスプライト配列（Up01→03の順）")]
    [SerializeField] private Sprite[] flyFrames;

    [Tooltip("棺開アニメの速度（fps）")]
    [SerializeField] private float openAnimFps = 8f;

    [Tooltip("フライトアニメの速度（fps）")]
    [SerializeField] private float flyAnimFps = 10f;

    [Tooltip("棺閉アニメの速度（fps）")]
    [SerializeField] private float closeAnimFps = 8f;

    // =========================================================
    // Flying
    // =========================================================
    [Header("Flying")]
    [Tooltip("攻撃可能時間（秒）")]
    [SerializeField] private float flyingDuration = 4f;

    [Tooltip("フライト中の上下ボブ幅（Unity単位）")]
    [SerializeField] private float flyBobAmplitude = 0.12f;

    [Tooltip("フライト中の上下ボブ速度（Hz）")]
    [SerializeField] private float flyBobFrequency = 2f;

    // =========================================================
    // Damage
    // =========================================================
    [Header("Damage Multipliers")]
    [Tooltip("Sealed / Opening / Closing 状態のダメージ倍率")]
    [SerializeField] private float sealedDamageMultiplier = 0.01f;

    // =========================================================
    // Stop Position Restriction
    // =========================================================
    [Header("Stop Position Restriction")]
    [Tooltip("停止してよい最低Y座標（Floor側の制限）")]
    [SerializeField] private float minStopY = -2f;
    [Tooltip("停止してよい最高Y座標（上端の制限）")]
    [SerializeField] private float maxStopY = 5f;
    [Tooltip("停止してよい最低X座標（左端の制限）")]
    [SerializeField] private float minStopX = -7f;
    [Tooltip("停止してよい最高X座標（右端の制限）")]
    [SerializeField] private float maxStopX = 7f;

    // =========================================================
    // Debug Visualization
    // =========================================================
    [Header("Debug Visualization")]
    [Tooltip("ONにするとPlay中のGame ViewにStop Zoneの枠を表示する")]
    [SerializeField] private bool showStopZone = false;
    private GameObject debugRectGO;

    // ── State Machine ──
    private enum CoffinState { Idle, Wobble, Opening, Flying, Closing }
    private CoffinState coffinState;

    // ── Components ──
    private EnemyMover       enemyMover;
    private EnemyShooter     enemyShooter;
    private EnemyPart        bodyPart;
    private SpriteRenderer   spriteRenderer;
    private Animator         enemyAnimator;
    private EnemySpriteSwapper spriteSwapper;

    // ── Circle ──
    private Vector3 circleCenter;
    private float   circleAngle;
    private int     circleDir = 1;
    private float   idleTimer;
    private float   idleDuration;

    // ── Wobble ──
    private Vector3 wobbleBasePos;
    private float   wobbleTimer;

    // ── Animation ──
    private float animTimer;

    // ── Flying ──
    private float   flyTimer;
    private float   flyBobTime;
    private Vector3 flyBasePos;

    // =========================================================
    void Awake()
    {
        enemyMover     = GetComponent<EnemyMover>();
        enemyShooter   = GetComponent<EnemyShooter>();
        bodyPart       = GetComponentInChildren<EnemyPart>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteSwapper  = GetComponent<EnemySpriteSwapper>();
        enemyAnimator  = GetComponent<Animator>();
        if (enemyAnimator == null)
            enemyAnimator = GetComponentInChildren<Animator>();

        // EnemyMover の移動はこのスクリプトが管理する
        if (enemyMover   != null) enemyMover.suppressMovement = true;
        if (enemyShooter != null) enemyShooter.enabled = false;
        // Animator 無効（スプライトはコードで直接切り替える）
        if (enemyAnimator != null) enemyAnimator.enabled = false;

        if (showStopZone) CreateDebugRect();
    }

    void OnDestroy()
    {
        if (debugRectGO != null) Destroy(debugRectGO);
    }

    void CreateDebugRect()
    {
        debugRectGO = new GameObject("[Debug] CoffinStopZone");
        var lr = debugRectGO.AddComponent<LineRenderer>();
        lr.positionCount = 5;
        lr.loop          = false;
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.06f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        var c = new Color(0f, 1f, 0.5f, 1f);
        lr.startColor = c;
        lr.endColor   = c;
        lr.sortingOrder = 999;
        float z = 0f;
        lr.SetPositions(new Vector3[] {
            new Vector3(minStopX, minStopY, z),
            new Vector3(maxStopX, minStopY, z),
            new Vector3(maxStopX, maxStopY, z),
            new Vector3(minStopX, maxStopY, z),
            new Vector3(minStopX, minStopY, z),
        });
    }

    void Start()
    {
        SetDamageMultiplier(sealedDamageMultiplier);
        SetHitSpriteEnabled(false);
        SetSprite(openFrames, 0);
        EnterIdle();
    }

    void Update()
    {
        // EnemySpriteSwapper が被弾回復時に Animator を再有効化するため、毎フレーム無効化を保持する
        if (enemyAnimator != null && enemyAnimator.enabled)
            enemyAnimator.enabled = false;

        switch (coffinState)
        {
            case CoffinState.Idle:    UpdateIdle();    break;
            case CoffinState.Wobble:  UpdateWobble();  break;
            case CoffinState.Opening: UpdateOpening(); break;
            case CoffinState.Flying:  UpdateFlying();  break;
            case CoffinState.Closing: UpdateClosing(); break;
        }
    }

    // =========================================================
    // IDLE — 円移動
    // =========================================================
    void EnterIdle()
    {
        coffinState  = CoffinState.Idle;
        circleDir    = Random.value > 0.5f ? 1 : -1;
        idleDuration = Random.Range(idleDurationMin, idleDurationMax);
        idleTimer    = 0f;

        // 現在位置が円上の開始点になるよう circleCenter を設定
        float startAngle = Random.Range(0f, Mathf.PI * 2f);
        circleCenter = new Vector3(
            transform.position.x - Mathf.Cos(startAngle) * circleRadius,
            transform.position.y - Mathf.Sin(startAngle) * circleRadius,
            transform.position.z);
        circleAngle = startAngle;

        SetDamageMultiplier(sealedDamageMultiplier);
        SetSprite(openFrames, 0);
    }

    void UpdateIdle()
    {
        float dt   = DeltaTime();
        idleTimer += dt;
        circleAngle += circleSpeed * dt * circleDir;

        transform.position = new Vector3(
            circleCenter.x + Mathf.Cos(circleAngle) * circleRadius,
            circleCenter.y + Mathf.Sin(circleAngle) * circleRadius,
            transform.position.z);

        if (idleTimer >= idleDuration
            && transform.position.y >= minStopY
            && transform.position.y <= maxStopY
            && transform.position.x >= minStopX
            && transform.position.x <= maxStopX)
            EnterWobble();
    }

    // =========================================================
    // WOBBLE — 開く前の予告揺れ
    // =========================================================
    void EnterWobble()
    {
        coffinState   = CoffinState.Wobble;
        wobbleTimer   = 0f;
        wobbleBasePos = transform.position;
    }

    void UpdateWobble()
    {
        float dt     = DeltaTime();
        wobbleTimer += dt;

        // 減衰する横揺れ（wobbleBasePos から絶対座標でオフセット）
        float t       = Mathf.Clamp01(wobbleTimer / wobbleDuration);
        float offsetX = Mathf.Sin(wobbleTimer * 45f) * wobbleIntensity * (1f - t);
        transform.position = new Vector3(wobbleBasePos.x + offsetX, wobbleBasePos.y, wobbleBasePos.z);

        if (wobbleTimer >= wobbleDuration)
        {
            transform.position = wobbleBasePos;
            EnterOpening();
        }
    }

    // =========================================================
    // OPENING — 棺開アニメーション（Sealed01→05）
    // =========================================================
    void EnterOpening()
    {
        coffinState = CoffinState.Opening;
        animTimer   = 0f;
        SetSprite(openFrames, 0);
    }

    void UpdateOpening()
    {
        if (openFrames == null || openFrames.Length == 0) { EnterFlying(); return; }

        animTimer += DeltaTime();
        int frame = Mathf.Min(Mathf.FloorToInt(animTimer * openAnimFps), openFrames.Length - 1);
        SetSprite(openFrames, frame);

        if (animTimer >= (float)openFrames.Length / openAnimFps) EnterFlying();
    }

    // =========================================================
    // FLYING — フライト & 攻撃
    // =========================================================
    void EnterFlying()
    {
        coffinState = CoffinState.Flying;
        flyTimer    = 0f;
        flyBobTime  = 0f;
        animTimer   = 0f;
        flyBasePos  = transform.position;

        SetDamageMultiplier(1f);
        SetHitSpriteEnabled(true);
        if (enemyShooter != null) enemyShooter.enabled = true;
    }

    void UpdateFlying()
    {
        float dt  = DeltaTime();
        flyTimer   += dt;
        flyBobTime += dt;
        animTimer  += dt;

        // フライトアニメーションをループ再生
        if (flyFrames != null && flyFrames.Length > 0)
        {
            int frame = Mathf.FloorToInt(animTimer * flyAnimFps) % flyFrames.Length;
            SetSprite(flyFrames, frame);
        }

        // 上下ボブ（flyBasePos を基点とした sin オフセット）
        float bobY = Mathf.Sin(flyBobTime * flyBobFrequency * Mathf.PI * 2f) * flyBobAmplitude;
        transform.position = new Vector3(flyBasePos.x, flyBasePos.y + bobY, flyBasePos.z);

        if (flyTimer >= flyingDuration) EnterClosing();
    }

    // =========================================================
    // CLOSING — 棺閉アニメーション（Sealed05→01）
    // =========================================================
    void EnterClosing()
    {
        coffinState = CoffinState.Closing;
        animTimer   = 0f;
        transform.position = flyBasePos; // ボブを解除して基点位置に戻す

        if (enemyShooter != null) enemyShooter.enabled = false;
        SetDamageMultiplier(sealedDamageMultiplier);
        SetHitSpriteEnabled(false);
    }

    void UpdateClosing()
    {
        if (openFrames == null || openFrames.Length == 0) { EnterIdle(); return; }

        animTimer += DeltaTime();
        int total = openFrames.Length;
        // openFrames を逆順に再生
        int frame = total - 1 - Mathf.Min(Mathf.FloorToInt(animTimer * closeAnimFps), total - 1);
        SetSprite(openFrames, Mathf.Max(0, frame));

        if (animTimer >= (float)total / closeAnimFps) EnterIdle();
    }

    // =========================================================
    // Helpers
    // =========================================================
    void SetSprite(Sprite[] frames, int index)
    {
        if (frames == null || index < 0 || index >= frames.Length) return;
        if (spriteRenderer == null) return;
        // EnemySpriteSwapper がオーバーライド中なら通常アニメを直接書かない。
        // SetBaseSprite() で normalSprite を更新するだけにする（オーバーライド終了後に正しいフレームで復帰）。
        if (spriteSwapper != null)
            spriteSwapper.SetBaseSprite(frames[index]);
        else
            spriteRenderer.sprite = frames[index];
    }

    void SetDamageMultiplier(float mul)
    {
        if (bodyPart != null) bodyPart.damageMultiplier = mul;
    }

    void SetHitSpriteEnabled(bool enabled)
    {
        if (spriteSwapper != null) spriteSwapper.EnableHitSprite(enabled);
    }

    /// <summary>スローモーション・B4デバフを反映した deltaTime を返す。</summary>
    float DeltaTime() =>
        Time.deltaTime
        * (SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f)
        * (enemyMover != null ? enemyMover.SpeedMultiplier : 1f);

    void OnDrawGizmos()
    {
        float cx = (minStopX + maxStopX) * 0.5f;
        float cy = (minStopY + maxStopY) * 0.5f;
        float w  = maxStopX - minStopX;
        float h  = maxStopY - minStopY;
        Vector3 center = new Vector3(cx, cy, transform.position.z);
        Vector3 size   = new Vector3(w, h, 0.1f);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.15f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireCube(center, size);
    }
}
