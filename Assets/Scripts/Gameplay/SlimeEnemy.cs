using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スライム敵の分裂・バウンス移動・サイズ縮小を管理するコンポーネント。
///
/// 分裂時は Instantiate(gameObject) ではなく EnemySpawner.SpawnCloneSlime() を使う。
/// これにより、プレハブから正しく生成され、HPバー等の重複や
/// スプライト状態の引き継ぎ問題を防ぐ。
/// </summary>
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyDamageReceiver))]
public class SlimeEnemy : MonoBehaviour
{
    // =========================================================
    // Inspector フィールド
    // =========================================================

    [Header("Split Settings")]
    [Tooltip("画面上に存在できるスライムの最大数（分裂後の総数）\n例: 5 = 最初の1体が最大5体まで増える")]
    [SerializeField] private int maxSlimesOnScreen = 5;

    [Tooltip("分裂ごとにサイズに掛ける倍率（例: 0.85 → 分裂のたびに85%のサイズになる）")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float sizeScaleOnSplit = 0.85f;

    [Tooltip("サイズの下限倍率（この値より小さくはならない）")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float minSizeScale = 0.35f;

    [Tooltip("同じスライムが連続して分裂できない最短間隔（秒）\n同一フレームに複数の弾が当たった時の多重分裂を防ぐ")]
    [SerializeField] private float splitCooldown = 0.5f;

    [Header("Split Spawn Settings")]
    [Tooltip("分裂体の出現位置：分裂元からの最小距離（ワールド単位）")]
    [SerializeField] private float spawnMinDistance = 2f;

    [Tooltip("出現Y座標の下限\n0=画面下端, 1=画面上端\n例: 0.4 → 下側40%は出現しない")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnYMinPercent = 0.4f;

    [Tooltip("出現Y座標の上限\n0=画面下端, 1=画面上端")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnYMaxPercent = 0.9f;

    [Tooltip("有効な出現位置が見つからない時の最大リトライ回数\nすべて失敗した場合は分裂しない")]
    [SerializeField] private int maxSpawnAttempts = 15;

    [Header("Movement Settings")]
    [Tooltip("速いフェーズの移動速度（ワールド単位/秒）")]
    [SerializeField] private float fastSpeed = 3.5f;

    [Tooltip("速いフェーズの持続時間（秒）")]
    [SerializeField] private float fastDuration = 0.5f;

    [Tooltip("遅いフェーズの移動速度（ワールド単位/秒）")]
    [SerializeField] private float slowSpeed = 0.8f;

    [Tooltip("遅いフェーズの持続時間（秒・ランダム下限）")]
    [SerializeField] private float slowDurationMin = 1.0f;

    [Tooltip("遅いフェーズの持続時間（秒・ランダム上限）")]
    [SerializeField] private float slowDurationMax = 2.0f;

    [Tooltip("停止フェーズの持続時間（秒・ランダム下限）")]
    [SerializeField] private float stopDurationMin = 1.0f;

    [Tooltip("停止フェーズの持続時間（秒・ランダム上限）")]
    [SerializeField] private float stopDurationMax = 2.0f;

    [Tooltip("スクリーン端から内側に保つマージン（ワールド単位）")]
    [SerializeField] private float screenPadding = 0.5f;

    [Tooltip("壁バウンス後に次の方向ランダム転換を許可するまでの最短間隔（秒）")]
    [SerializeField] private float directionChangeCooldown = 1.2f;

    [Tooltip("SkillHUD の横幅（ピクセル単位）。左端をこの幅だけ内側でバウンスさせる。")]
    [SerializeField] private float skillHudPixelWidth = 280f;

    [Header("Floor Avoidance")]
    [Tooltip("Floor オブジェクトへの参照。未設定の場合はシーンから FloorHealth コンポーネントで自動検索する。")]
    [SerializeField] private FloorHealth floorObject;

    [Tooltip("Floor の上端からこの距離以内に入ったら上方向に方向転換する（ワールド単位）")]
    [SerializeField] private float floorAvoidDistance = 1.5f;

    // =========================================================
    // 定数・スタティック
    // =========================================================

    private static readonly Vector2[] EightDirections = new Vector2[]
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right,
        new Vector2( 1f,  1f).normalized,
        new Vector2(-1f,  1f).normalized,
        new Vector2( 1f, -1f).normalized,
        new Vector2(-1f, -1f).normalized,
    };

    // シーン内の全スライムを追跡（分裂上限チェック用）
    private static readonly List<SlimeEnemy> activeSlimes = new List<SlimeEnemy>();

    // =========================================================
    // インスタンス変数
    // =========================================================

    private EnemyStats stats;
    private EnemyDamageReceiver damageReceiver;

    private Vector2 moveDirection;
    private float bounceProtectionTimer;
    private float lastSplitTime = -999f;

    // 速度サイクル管理（Fast → Slow → Stop → Fast ...）
    private enum MovePhase { Fast, Slow, Stop }
    private MovePhase currentPhase = MovePhase.Fast;
    private float phaseTimer = 0f;      // 現在フェーズの残り時間
    private float currentSpeed = 0f;   // 現在の実効速度

    private float screenXMin, screenXMax, screenYMin, screenYMax;
    private float floorAvoidY = float.MinValue;  // Floor 回避のY下限（ワールド座標）

    // スケール管理
    // baseLocalScale : EnemySpawner が spriteScale を適用した後の localScale
    //                  SpawnCloneSlime() が Instantiate 後に設定し Start() はスキップ
    // currentScaleMultiplier : 分裂のたびに減少する倍率
    [HideInInspector] public Vector3 baseLocalScale;
    [HideInInspector] public float currentScaleMultiplier = 1f;
    [HideInInspector] public bool baseScaleInitialized = false;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        damageReceiver = GetComponent<EnemyDamageReceiver>();
    }

    private void Start()
    {
        // SpawnCloneSlime() で生成された場合は設定済みなのでスキップ
        if (!baseScaleInitialized)
        {
            // 通常スポーン: EnemySpawner が spriteScale を適用した後の値を取得
            baseLocalScale = transform.localScale;
            baseScaleInitialized = true;
        }

        CacheFloorY();
    }

    private void CacheFloorY()
    {
        // Inspector 未設定の場合はシーンから自動検索
        if (floorObject == null)
            floorObject = FindObjectOfType<FloorHealth>();

        if (floorObject != null)
        {
            // Floor コライダーの上端 + 回避距離 を下限として使用
            Collider2D col = floorObject.GetComponent<Collider2D>();
            float floorTopY = col != null
                ? col.bounds.max.y
                : floorObject.transform.position.y;

            floorAvoidY = floorTopY + floorAvoidDistance;
        }
    }

    private void OnEnable()
    {
        if (!activeSlimes.Contains(this))
            activeSlimes.Add(this);

        if (damageReceiver != null)
            damageReceiver.OnReflectedBulletHit += HandleBulletHit;

        moveDirection = EightDirections[Random.Range(0, EightDirections.Length)];
        RefreshScreenBounds();

        // フェーズをランダムオフセットで開始（全スライムが同期して動くのを防ぐ）
        currentPhase = MovePhase.Fast;
        phaseTimer = fastDuration * Random.Range(0f, 1f);
        currentSpeed = fastSpeed;
    }

    private void OnDisable()
    {
        activeSlimes.Remove(this);

        if (damageReceiver != null)
            damageReceiver.OnReflectedBulletHit -= HandleBulletHit;
    }

    private void OnDestroy()
    {
        activeSlimes.Remove(this);

        if (damageReceiver != null)
            damageReceiver.OnReflectedBulletHit -= HandleBulletHit;
    }

    private void Update()
    {
        if (bounceProtectionTimer > 0f)
            bounceProtectionTimer -= Time.deltaTime;

        UpdateMovePhase();
        ApplyBounceMovement();
    }

    private void UpdateMovePhase()
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer > 0f) return;

        // フェーズ遷移: Fast → Slow → Stop → Fast ...
        switch (currentPhase)
        {
            case MovePhase.Fast:
                currentPhase = MovePhase.Slow;
                phaseTimer   = Random.Range(slowDurationMin, slowDurationMax);
                currentSpeed = slowSpeed;
                break;

            case MovePhase.Slow:
                currentPhase = MovePhase.Stop;
                phaseTimer   = Random.Range(stopDurationMin, stopDurationMax);
                currentSpeed = 0f;
                // 停止→再開時に方向をランダム転換（引きずり感の演出）
                moveDirection = EightDirections[Random.Range(0, EightDirections.Length)];
                break;

            case MovePhase.Stop:
                currentPhase = MovePhase.Fast;
                phaseTimer   = fastDuration;
                currentSpeed = fastSpeed;
                break;
        }
    }

    // =========================================================
    // バウンス移動
    // =========================================================

    private void ApplyBounceMovement()
    {
        Vector3 pos = transform.position;
        pos += (Vector3)(moveDirection * currentSpeed * Time.deltaTime);

        if (pos.x < screenXMin)
        {
            pos.x = screenXMin;
            moveDirection.x = Mathf.Abs(moveDirection.x);
            OnBounce();
        }
        else if (pos.x > screenXMax)
        {
            pos.x = screenXMax;
            moveDirection.x = -Mathf.Abs(moveDirection.x);
            OnBounce();
        }

        // Floor 回避距離とスクリーン下端の大きい方を Y 下限として使用
        float yMin = floorAvoidY > float.MinValue
            ? Mathf.Max(screenYMin, floorAvoidY)
            : screenYMin;

        if (pos.y < yMin)
        {
            pos.y = yMin;
            moveDirection.y = Mathf.Abs(moveDirection.y);   // 上向きに反転
            OnBounce();
        }
        else if (pos.y > screenYMax)
        {
            pos.y = screenYMax;
            moveDirection.y = -Mathf.Abs(moveDirection.y);
            OnBounce();
        }

        transform.position = pos;
    }

    private void OnBounce()
    {
        if (bounceProtectionTimer > 0f) return;
        moveDirection = EightDirections[Random.Range(0, EightDirections.Length)];
        bounceProtectionTimer = directionChangeCooldown;
    }

    private void RefreshScreenBounds()
    {
        if (Camera.main == null) return;

        float halfH = Camera.main.orthographicSize;
        float halfW = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float skillHudWorldOffset = 0f;
        if (skillHudPixelWidth > 0f && Screen.width > 0)
        {
            float worldUnitsPerPixel = (halfW * 2f) / Screen.width;
            skillHudWorldOffset = skillHudPixelWidth * worldUnitsPerPixel;
        }

        screenXMin = camPos.x - halfW + screenPadding + skillHudWorldOffset;
        screenXMax = camPos.x + halfW - screenPadding;
        screenYMin = camPos.y - halfH + screenPadding;
        screenYMax = camPos.y + halfH - screenPadding;
    }

    // =========================================================
    // 分裂ロジック
    // =========================================================

    private void HandleBulletHit(int damage, bool isJust)
    {
        if (isJust) return;

        // 同一スライムへの多重ヒットによる連続分裂を防ぐ
        if (Time.time - lastSplitTime < splitCooldown) return;

        // null になったエントリーを掃除してから正確な数をチェック
        for (int i = activeSlimes.Count - 1; i >= 0; i--)
        {
            if (activeSlimes[i] == null) activeSlimes.RemoveAt(i);
        }
        if (activeSlimes.Count >= maxSlimesOnScreen) return;

        // 分裂実行前にタイムスタンプを記録（非同期フレームの多重分裂も防ぐ）
        lastSplitTime = Time.time;

        TrySpawnSplit();
    }

    private void TrySpawnSplit()
    {
        EnemySpawner spawner = stats.GetSpawner();
        if (spawner == null) return;

        EnemyShooter shooter = GetComponent<EnemyShooter>();
        if (shooter == null) return;

        EnemyData data = shooter.GetEnemyData();
        if (data == null) return;

        Vector3? spawnPos = FindSpawnPosition();
        if (!spawnPos.HasValue) return;

        float newMultiplier = Mathf.Max(minSizeScale, currentScaleMultiplier * sizeScaleOnSplit);

        // プレハブから正しく生成（ライブオブジェクトの複製ではない）
        spawner.SpawnCloneSlime(data, spawnPos.Value, stats.HP, newMultiplier);
    }

    /// <summary>
    /// スケール倍率を設定し transform.localScale に反映する。
    /// EnemySpawner.SpawnCloneSlime() から呼ばれる。
    /// </summary>
    public void SetScaleMultiplier(float multiplier)
    {
        currentScaleMultiplier = multiplier;
        transform.localScale = baseLocalScale * multiplier;

        // バー・数値もスプライトと同じ倍率で縮小させる
        GetComponent<EnemyHealthDisplay>()?.SetDisplayScaleMultiplier(multiplier);
    }

    private Vector3? FindSpawnPosition()
    {
        if (Camera.main == null) return null;

        float halfH = Camera.main.orthographicSize;
        float halfW = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float skillHudWorldOffset = 0f;
        if (skillHudPixelWidth > 0f && Screen.width > 0)
        {
            float worldUnitsPerPixel = (halfW * 2f) / Screen.width;
            skillHudWorldOffset = skillHudPixelWidth * worldUnitsPerPixel;
        }

        float screenHeight = halfH * 2f;
        float yMin = camPos.y - halfH + screenHeight * spawnYMinPercent;
        float yMax = camPos.y - halfH + screenHeight * spawnYMaxPercent;
        float xMin = camPos.x - halfW + screenPadding + skillHudWorldOffset;
        float xMax = camPos.x + halfW - screenPadding;

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            float x = Random.Range(xMin, xMax);
            float y = Random.Range(yMin, yMax);
            Vector3 candidate = new Vector3(x, y, transform.position.z);

            if (Vector3.Distance(candidate, transform.position) >= spawnMinDistance)
                return candidate;
        }

        return null;
    }
}
