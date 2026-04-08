using UnityEngine;

/// <summary>
/// NeonMonster01専用の◇型移動コンポーネント。
/// - 時計回りに◇の4頂点を移動し、各頂点で一時停止
/// - 一定確率でランダムに反時計回りに切り替え
/// - 停止中のみ攻撃（EnemyShooterを制御）
/// - アニメーションの再生位置をコードで直接制御（移動/停止フレームを完全同期）
/// - HP減少で加速（エンレイジ）
/// </summary>
public class NeonMonsterMover : MonoBehaviour
{
    // =========================================================
    // Inspector 設定
    // =========================================================

    [Header("◇ 移動設定")]
    [Tooltip("◇の中心から左右頂点までの距離（Unity単位）")]
    [SerializeField] private float diamondRadiusH = 4f;

    [Tooltip("◇の中心から上下頂点までの距離（Unity単位）")]
    [SerializeField] private float diamondRadiusV = 2f;

    [Tooltip("1辺の移動にかかる時間（秒）")]
    [SerializeField] private float moveDuration = 1.0f;

    [Tooltip("頂点での停止時間（秒）")]
    [SerializeField] private float stopDuration = 1.0f;

    [Tooltip("移動のイージングカーブ。横軸=進行割合(0→1)、縦軸=位置割合(0→1)")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("方向設定")]
    [Tooltip("反時計回りになる確率（0=常に時計回り、1=常に反時計回り）")]
    [Range(0f, 1f)]
    [SerializeField] private float counterClockwiseChance = 0.3f;

    [Header("アニメーション同期")]
    [Tooltip("アニメーションクリップ名")]
    [SerializeField] private string animClipName = "NM01";

    [Tooltip("アニメーションの総フレーム数")]
    [SerializeField] private int totalFrames = 61;

    [Tooltip("移動アニメーションの開始フレーム")]
    [SerializeField] private int moveAnimStartFrame = 0;

    [Tooltip("移動アニメーションの終了フレーム")]
    [SerializeField] private int moveAnimEndFrame = 44;

    [Tooltip("停止アニメーションの開始フレーム")]
    [SerializeField] private int stopAnimStartFrame = 45;

    [Tooltip("停止アニメーションの終了フレーム")]
    [SerializeField] private int stopAnimEndFrame = 60;

    [Header("開始頂点")]
    [Tooltip("スポーン時に開始する頂点。Nearestはスポーン位置に最も近い頂点を自動選択。")]
    [SerializeField] private StartVertexMode startVertex = StartVertexMode.Top;

    public enum StartVertexMode { Nearest, Top, Right, Bottom, Left }

    [Header("スプライト反転")]
    [Tooltip("ON: 移動方向（左右）に応じてスプライトをFlipX")]
    [SerializeField] private bool flipSpriteOnDirection = true;

    [Header("エンレイジ（HP減少で加速）")]
    [Tooltip("HP0になったときの速度倍率。1.0で無効（加速なし）。")]
    [SerializeField] private float enrageMaxSpeedMultiplier = 2.0f;

    [Header("画面中央補正（SkillHUD対応）")]
    [Tooltip("画面左のSkillHUDの横幅（スクリーンピクセル）。この幅を除いた中央にスポーンします。")]
    [SerializeField] private float skillHudWidthPixels = 280f;

    [Tooltip("スポーン位置の縦オフセット（マイナスで下に移動）")]
    [SerializeField] private float spawnYOffset = 0f;

    // =========================================================
    // Runtime 変数
    // =========================================================

    // ◇の4頂点（インデックス: 0=上, 1=右, 2=下, 3=左）
    private Vector3[] vertices;
    private int currentVertex = 0;

    private bool isMoving = false;
    private float timer = 0f;
    private Vector3 moveFrom;
    private Vector3 moveTo;

    // 停止アニメーション用タイマー（独立してループ）
    private float stopAnimTimer = 0f;

    private Animator animator;
    private EnemyShooter shooter;
    private EnemyStats enemyStats;
    private SpriteRenderer spriteRenderer;
    private EnemySpriteSwapper spriteSwapper;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Start()
    {
        animator       = GetComponent<Animator>();
        shooter        = GetComponent<EnemyShooter>();
        enemyStats     = GetComponent<EnemyStats>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteSwapper  = GetComponent<EnemySpriteSwapper>();


        // アニメーターの自動進行を止め、コードで完全制御する
        if (animator != null) animator.speed = 0f;

        Vector3 origin = CalculatePlayableCenter();

        // ◇の4頂点を計算（時計回り: 上→右→下→左）
        vertices = new Vector3[]
        {
            origin + new Vector3(0f,              diamondRadiusV, 0f),   // 0: 上
            origin + new Vector3( diamondRadiusH, 0f,             0f),   // 1: 右
            origin + new Vector3(0f,             -diamondRadiusV, 0f),   // 2: 下
            origin + new Vector3(-diamondRadiusH, 0f,             0f),   // 3: 左
        };

        // 開始頂点を決定
        currentVertex = startVertex == StartVertexMode.Nearest
            ? GetNearestVertexIndex(origin)
            : (int)startVertex - 1;  // Top=0, Right=1, Bottom=2, Left=3
        transform.position = vertices[currentVertex];

        // 最初は停止状態
        isMoving = false;
        timer = 0f;
        stopAnimTimer = 0f;
        ApplyStopState();
    }

    private void Update()
    {
        // 被弾中は移動・タイマーを停止
        if (spriteSwapper != null && spriteSwapper.IsHitActive) return;

        float speedMult = GetEnrageMultiplier();

        if (isMoving)
            UpdateMoving(speedMult);
        else
            UpdateStopped(speedMult);
    }

    // =========================================================
    // 移動中の更新
    // =========================================================

    private void UpdateMoving(float speedMult)
    {
        float duration = moveDuration / speedMult;
        timer += Time.deltaTime * GetTimeScale();
        float t = Mathf.Clamp01(timer / duration);

        transform.position = Vector3.LerpUnclamped(moveFrom, moveTo, moveCurve.Evaluate(t));

        // 移動アニメーション：t に応じて moveAnimStart〜End を進行
        SetAnimNormalizedTime(Mathf.Lerp(
            (float)moveAnimStartFrame / totalFrames,
            (float)moveAnimEndFrame   / totalFrames,
            t
        ));

        if (t >= 1f)
        {
            transform.position = moveTo;
            isMoving = false;
            timer = 0f;
            stopAnimTimer = 0f;
            ApplyStopState();
        }
    }

    // =========================================================
    // 停止中の更新
    // =========================================================

    private void UpdateStopped(float speedMult)
    {
        float duration = stopDuration / speedMult;
        timer += Time.deltaTime * GetTimeScale();
        stopAnimTimer += Time.deltaTime * GetTimeScale();

        // 停止アニメーション：自然な速度でループ（60fps換算）
        float stopAnimDuration = (stopAnimEndFrame - stopAnimStartFrame) / 60f;
        float phase = Mathf.Repeat(stopAnimTimer / stopAnimDuration, 1f);
        SetAnimNormalizedTime(Mathf.Lerp(
            (float)stopAnimStartFrame / totalFrames,
            (float)stopAnimEndFrame   / totalFrames,
            phase
        ));

        if (timer >= duration)
            StartNextMove();
    }

    private void StartNextMove()
    {
        moveFrom = transform.position;

        // 時計回り or 反時計回りをランダム決定
        bool goClockwise = Random.value > counterClockwiseChance;
        currentVertex = goClockwise
            ? (currentVertex + 1) % 4
            : (currentVertex + 3) % 4;  // -1 mod 4

        moveTo = vertices[currentVertex];
        isMoving = true;
        timer = 0f;

        // 移動方向に応じてスプライト反転
        if (flipSpriteOnDirection && spriteRenderer != null)
        {
            float dx = moveTo.x - moveFrom.x;
            if (Mathf.Abs(dx) > 0.01f)
                spriteRenderer.flipX = dx < 0;
        }

        ApplyMoveState();
    }

    // =========================================================
    // 状態切り替え
    // =========================================================

    private void ApplyMoveState()
    {
        if (shooter != null) shooter.enabled = false;
    }

    private void ApplyStopState()
    {
        if (shooter != null) shooter.enabled = true;
    }

    // =========================================================
    // ヘルパー
    // =========================================================

    private void SetAnimNormalizedTime(float normalizedTime)
    {
        if (animator == null) return;
        animator.Play(animClipName, 0, normalizedTime);
    }

    private int GetNearestVertexIndex(Vector3 pos)
    {
        int nearest = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < vertices.Length; i++)
        {
            float dist = Vector3.Distance(pos, vertices[i]);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = i;
            }
        }
        return nearest;
    }

    private float GetTimeScale()
    {
        return SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    }

    private float GetEnrageMultiplier()
    {
        if (enemyStats == null || enemyStats.MaxHP <= 0) return 1f;
        float hpRatio = (float)enemyStats.HP / enemyStats.MaxHP;
        return Mathf.Lerp(1f, enrageMaxSpeedMultiplier, 1f - hpRatio);
    }

    /// <summary>
    /// SkillHUD幅を除いたプレイ可能エリアの水平中央をワールド座標で返す。
    /// </summary>
    private Vector3 CalculatePlayableCenter()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;

        // プレイ可能エリアの中央X（スクリーンpx）: HUD右端から画面右端の中央
        float screenCenterX = skillHudWidthPixels + (Screen.width - skillHudWidthPixels) * 0.5f;

        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 worldCenter = cam.ScreenToWorldPoint(
            new Vector3(screenCenterX, Screen.height * 0.5f, depth));

        return new Vector3(worldCenter.x, transform.position.y + spawnYOffset, transform.position.z);
    }
}
