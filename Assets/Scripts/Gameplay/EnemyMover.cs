using UnityEngine;

public class EnemyMover : MonoBehaviour
{
    [Header("Move Settings (Legacy)")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float moveRange = 2f;

    // =========================================================
    // EnemyData（移動パターン）注入
    // =========================================================
    [Header("Move Types (Injected from EnemyData)")]
    [SerializeField] private EnemyData enemyData;

    public EnemyData GetEnemyData()
    {
        return enemyData;
    }

    private Vector3 startPos;
    private Vector3 patternStartPos;
    private int dir = 1;
    private float patternStartTime;
    private int currentMoveTypeIndex = -1;
    private EnemyData.MoveType currentMoveType;
    private float randomWalkNextChangeTime;
    private Vector2 randomWalkDirection;

    // 移動ルーチン用
    private int moveSequenceEntryIndex = -1;
    private float moveSequenceRemainingTime = 0f;

    // パターン固有の変数
    private float circleAngle;
    private float figure8Angle;
    private float zigzagProgress;
    private float sineTime;
    private float lissajousTime;

    // Hopping用の変数
    private enum HoppingState { Grounded, Jumping }
    private HoppingState hoppingState = HoppingState.Grounded;
    private float hoppingStateStartTime;
    private Vector3 hoppingJumpStartPos;
    private float hoppingNextDirection; // 次のジャンプの方向（度）

    // Warp用の変数
    private float warpNextTime; // 次のワープ時刻

    // 速度デバフシステム
    private float speedMultiplier = 1f;
    private Coroutine slowEffectCoroutine;

    // EnemyShooterへの参照（ワープ後の回転用）
    private EnemyShooter enemyShooter;

    // Horizontal/Vertical用のランダム距離
    private float currentHorizontalTargetDistance;
    private float currentVerticalTargetDistance;

    // Move Type切り替え検出用
    private EnemyData.MoveType.PatternType previousPatternType = EnemyData.MoveType.PatternType.None;

    // Circle/Figure8/Zigzag/SineWave/Lissajousの中心位置（パターン切り替え時に更新）
    private Vector3 circleCenter;
    private Vector3 figure8Center;
    private Vector3 zigzagCenter;
    private Vector3 sineWaveCenter;
    private Vector3 lissajousCenter;

    // Circle/Figure8/Zigzag/SineWave/Lissajous初期化用
    private SpriteRenderer spriteRenderer;
    private bool isCircleFirstFrame = false;
    private bool isFigure8FirstFrame = false;
    private bool isZigzagFirstFrame = false;
    private bool isSineWaveFirstFrame = false;
    private bool isLissajousFirstFrame = false;

    // HP-Based Routine Switching
    private EnemyStats enemyStats;
    private bool hasSwitchedToLowHpRoutine = false;  // 一度切り替わったら戻らない
    private EnemyData.MoveFiringRoutine currentMoveFiringRoutine;

    // =========================================================
    // Rigidbody2D 対応（複数パーツ敵用）
    // =========================================================
    private Rigidbody2D rb;

    // =========================================================
    // Body反転管理（初回スポーン時は反転しない）
    // =========================================================
    private bool isFirstSequenceEntry = true;

    /// <summary>
    /// スローモーション対応のタイムスケール取得
    /// </summary>
    private float GetTimeScale()
    {
        return SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    }

    public void SetEnemyData(EnemyData data)
    {
        enemyData = data;
        currentMoveTypeIndex = -1;
        patternStartTime = Time.time;
        patternStartPos = transform.position;
        randomWalkNextChangeTime = Time.time;
        randomWalkDirection = Vector2.right;
        moveSequenceEntryIndex = -1;
        moveSequenceRemainingTime = 0f;

        // startPosが未設定なら設定（Start()より前に呼ばれた場合の対策）
        if (startPos == Vector3.zero)
        {
            startPos = transform.position;
        }

        // Circle/Figure8/Zigzag/SineWave/Lissajous中心位置の初期化
        circleCenter = transform.position;
        figure8Center = transform.position;
        zigzagCenter = transform.position;
        sineWaveCenter = transform.position;
        lissajousCenter = transform.position;

        // Move Typeがあれば、即座に初期化（初期位置のちらつき防止）
        // ただし、HP-Based Routine Switching が有効な場合は、Start() で初期化されるのでスキップ
        if (enemyData != null && enemyData.moveTypes != null && enemyData.moveTypes.Length > 0)
        {
            // HP-Based Routine Switching が有効な場合は初期化をスキップ
            if (!enemyData.useHpBasedRoutineSwitch)
            {
                currentMoveTypeIndex = 0;
                currentMoveType = enemyData.moveTypes[0];
                InitializePattern();
            }
        }
    }

    public void ApplyMove(float speed, float range)
    {
        moveSpeed = speed;
        moveRange = range;
    }

    private void Start()
    {
        startPos = transform.position;
        patternStartPos = transform.position;
        patternStartTime = Time.time;
        randomWalkNextChangeTime = Time.time;
        randomWalkDirection = Vector2.right;

        // Circle/Figure8/Zigzag/SineWave/Lissajous中心位置の初期化
        circleCenter = startPos;
        figure8Center = startPos;
        zigzagCenter = startPos;
        sineWaveCenter = startPos;
        lissajousCenter = startPos;

        // SpriteRenderer参照取得
        spriteRenderer = GetComponent<SpriteRenderer>();

        // EnemyStats参照取得（HP-Based Routine Switching用）
        enemyStats = GetComponent<EnemyStats>();

        // Rigidbody2D参照取得（複数パーツ敵用）
        rb = GetComponent<Rigidbody2D>();

        // EnemyShooter参照取得（ワープ後の回転用）
        enemyShooter = GetComponent<EnemyShooter>();

        // HP-Based Routine Switchingの初期化
        InitializeHpBasedRoutine();

        // Move Typeが設定されていれば、最初から正しい位置に配置
        // ただし、HP-Based Routine Switching が有効な場合は、UpdateMoveType() で初期化されるのでスキップ
        if (HasMoveTypes() && enemyData != null && enemyData.moveTypes.Length > 0)
        {
            if (currentMoveTypeIndex < 0 && !enemyData.useHpBasedRoutineSwitch)
            {
                currentMoveTypeIndex = 0;
                currentMoveType = enemyData.moveTypes[0];
                InitializePattern();
            }
        }
    }

    /// <summary>
    /// 一定時間、敵の移動速度を低下させる
    /// </summary>
    public void ApplySlowEffect(float slowMultiplier, float duration)
    {
        if (slowEffectCoroutine != null)
        {
            StopCoroutine(slowEffectCoroutine);
        }
        slowEffectCoroutine = StartCoroutine(SlowEffectCoroutine(slowMultiplier, duration));
    }

    private System.Collections.IEnumerator SlowEffectCoroutine(float slowMultiplier, float duration)
    {
        speedMultiplier = Mathf.Clamp01(slowMultiplier); // 0.0～1.0に制限
        yield return new WaitForSeconds(duration);
        speedMultiplier = 1f; // 元の速度に戻す
        slowEffectCoroutine = null;
    }

    private void Update()
    {
        // HP-Based Routine Switchingのチェック
        CheckHpAndSwitchRoutine();

        if (HasMoveTypes() && enemyData != null)
        {
            UpdateMoveType();
            ApplyMovePattern();
        }
        else
        {
            // 従来の左右往復移動
            ApplyLegacyMove();
        }
    }

    private bool HasMoveTypes()
    {
        return enemyData != null && enemyData.moveTypes != null && enemyData.moveTypes.Length > 0;
    }

    private void UpdateMoveType()
    {
        if (!HasMoveTypes()) return;

        // 移動ルーチンが有効な場合（HP-Based Routine Switching または 従来のルーチン）
        EnemyData.MoveFiringRoutine routine = GetCurrentMoveFiringRoutine();
        if (routine != null)
        {
            UpdateMoveTypeWithRoutine();
            return;
        }

        // 従来の順番移動（Sequence）
        // 現在の移動パターンの継続時間をチェック
        if (currentMoveType != null && currentMoveType.useDuration)
        {
            float elapsed = Time.time - patternStartTime;
            if (elapsed >= currentMoveType.durationSeconds)
            {
                // 次の移動パターンに切り替え
                currentMoveTypeIndex = (currentMoveTypeIndex + 1) % enemyData.moveTypes.Length;
                currentMoveType = enemyData.moveTypes[currentMoveTypeIndex];
                patternStartTime = Time.time;
                patternStartPos = transform.position;
                InitializePattern();
            }
        }
        else if (currentMoveTypeIndex < 0 || currentMoveTypeIndex >= enemyData.moveTypes.Length)
        {
            // 初期化
            currentMoveTypeIndex = 0;
            if (enemyData.moveTypes.Length > 0)
            {
                currentMoveType = enemyData.moveTypes[0];
                patternStartTime = Time.time;
                patternStartPos = transform.position;
                InitializePattern();
            }
        }
    }

    private void UpdateMoveTypeWithRoutine()
    {
        EnemyData.MoveFiringRoutine routine = GetCurrentMoveFiringRoutine();
        if (routine == null) return;

        switch (routine.routineType)
        {
            case EnemyData.MoveFiringRoutine.RoutineType.Sequence:
                UpdateMoveTypeSequence();
                break;

            case EnemyData.MoveFiringRoutine.RoutineType.Probability:
                UpdateMoveTypeProbability();
                break;
        }
    }

    private void UpdateMoveTypeSequence()
    {
        EnemyData.MoveFiringRoutine routine = GetCurrentMoveFiringRoutine();
        if (routine == null) return;

        if (routine.sequenceEntries == null || routine.sequenceEntries.Length == 0)
        {
            // フォールバック
            if (currentMoveTypeIndex < 0 && enemyData.moveTypes.Length > 0)
            {
                currentMoveTypeIndex = 0;
                currentMoveType = enemyData.moveTypes[0];
                patternStartTime = Time.time;
                patternStartPos = transform.position;
                InitializePattern();
            }
            return;
        }

        // 残り時間が0以下なら、次のエントリに進む
        if (moveSequenceRemainingTime <= 0f)
        {
            moveSequenceEntryIndex++;
            if (moveSequenceEntryIndex >= routine.sequenceEntries.Length)
            {
                moveSequenceEntryIndex = 0;  // ループ
            }

            // 新しいエントリの移動パターンを適用
            var entry = routine.sequenceEntries[moveSequenceEntryIndex];
            int moveTypeIdx = Mathf.Clamp(entry.moveTypeIndex, 0, enemyData.moveTypes.Length - 1);

            if (moveTypeIdx >= 0 && moveTypeIdx < enemyData.moveTypes.Length && enemyData.moveTypes[moveTypeIdx] != null)
            {
                currentMoveTypeIndex = moveTypeIdx;
                currentMoveType = enemyData.moveTypes[moveTypeIdx];
                moveSequenceRemainingTime = entry.durationSeconds;
                patternStartTime = Time.time;
                patternStartPos = transform.position;
                InitializePattern();

                // ★Body反転処理（初回スポーン時はスキップ）
                if (entry.flipBodyOnStart)
                {
                    if (isFirstSequenceEntry)
                    {
                        Debug.Log($"[EnemyMover] flipBodyOnStart=true, but isFirstSequenceEntry=true, skipping FlipBody()");
                        isFirstSequenceEntry = false;
                    }
                    else
                    {
                        Debug.Log($"[EnemyMover] flipBodyOnStart=true, calling FlipBody()");
                        FlipBody();
                    }
                }
                else
                {
                    Debug.Log($"[EnemyMover] flipBodyOnStart=false, skipping FlipBody()");
                    isFirstSequenceEntry = false;
                }
            }
        }

        // 残り時間を減らす（スローモーション対応）
        moveSequenceRemainingTime -= Time.deltaTime * GetTimeScale();
    }

    private void UpdateMoveTypeProbability()
    {
        EnemyData.MoveFiringRoutine routine = GetCurrentMoveFiringRoutine();
        if (routine == null) return;

        if (routine.probabilityEntries == null || routine.probabilityEntries.Length == 0)
        {
            // フォールバック
            if (currentMoveTypeIndex < 0 && enemyData.moveTypes.Length > 0)
            {
                currentMoveTypeIndex = 0;
                currentMoveType = enemyData.moveTypes[0];
                patternStartTime = Time.time;
                patternStartPos = transform.position;
                InitializePattern();
            }
            return;
        }

        // 現在の移動パターンの継続時間をチェック
        if (currentMoveType != null && currentMoveType.useDuration)
        {
            float elapsed = Time.time - patternStartTime;
            if (elapsed >= currentMoveType.durationSeconds)
            {
                // 確率に基づいて次の移動パターンを選択
                PickMoveTypeByProbability();
            }
        }
        else if (currentMoveTypeIndex < 0 || currentMoveTypeIndex >= enemyData.moveTypes.Length)
        {
            // 初期化
            PickMoveTypeByProbability();
        }
    }

    private void PickMoveTypeByProbability()
    {
        EnemyData.MoveFiringRoutine routine = GetCurrentMoveFiringRoutine();
        if (routine == null) return;

        if (routine.probabilityEntries == null || routine.probabilityEntries.Length == 0)
            return;

        // 確率の合計を計算
        float total = 0f;
        for (int i = 0; i < routine.probabilityEntries.Length; i++)
        {
            total += Mathf.Max(0f, routine.probabilityEntries[i].probabilityPercentage);
        }

        if (total <= 0.0001f) return;

        // ランダム値を生成して確率に基づいて選択
        float r = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < routine.probabilityEntries.Length; i++)
        {
            acc += Mathf.Max(0f, routine.probabilityEntries[i].probabilityPercentage);
            if (r <= acc)
            {
                // 選択されたエントリが指定するMove Typesのインデックスを使用
                int moveTypeIdx = Mathf.Clamp(routine.probabilityEntries[i].moveTypeIndex, 0, enemyData.moveTypes.Length - 1);

                if (moveTypeIdx >= 0 && moveTypeIdx < enemyData.moveTypes.Length && enemyData.moveTypes[moveTypeIdx] != null)
                {
                    currentMoveTypeIndex = moveTypeIdx;
                    currentMoveType = enemyData.moveTypes[moveTypeIdx];
                    patternStartTime = Time.time;
                    patternStartPos = transform.position;
                    InitializePattern();
                }
                return;
            }
        }

        // フォールバック：最後のエントリを使用
        if (routine.probabilityEntries.Length > 0)
        {
            int moveTypeIdx = Mathf.Clamp(routine.probabilityEntries[routine.probabilityEntries.Length - 1].moveTypeIndex, 0, enemyData.moveTypes.Length - 1);
            if (moveTypeIdx >= 0 && moveTypeIdx < enemyData.moveTypes.Length && enemyData.moveTypes[moveTypeIdx] != null)
            {
                currentMoveTypeIndex = moveTypeIdx;
                currentMoveType = enemyData.moveTypes[moveTypeIdx];
                patternStartTime = Time.time;
                patternStartPos = transform.position;
                InitializePattern();
            }
        }
    }

    private void InitializePattern()
    {
        if (currentMoveType == null) return;

        EnemyData.MoveType.PatternType newPatternType = currentMoveType.patternType;

        switch (newPatternType)
        {
            case EnemyData.MoveType.PatternType.Horizontal:
                // 開始方向をランダム化
                if (currentMoveType.useRandomStartDirection)
                {
                    dir = Random.value > 0.5f ? 1 : -1;
                }
                else
                {
                    dir = 1;  // デフォルトは右
                }
                // 初回の目標距離を設定
                currentHorizontalTargetDistance = GetRandomDistance(currentMoveType);
                break;

            case EnemyData.MoveType.PatternType.Vertical:
                // 開始方向をランダム化
                if (currentMoveType.useRandomStartDirection)
                {
                    dir = Random.value > 0.5f ? 1 : -1;
                }
                else
                {
                    dir = 1;  // デフォルトは上
                }
                // 初回の目標距離を設定
                currentVerticalTargetDistance = GetRandomDistance(currentMoveType);
                break;

            case EnemyData.MoveType.PatternType.Circle:
                // 前回と異なるパターンから切り替わった場合のみ初期化、同じCircleなら現在の角度と中心を保持
                if (previousPatternType != EnemyData.MoveType.PatternType.Circle)
                {
                    // 現在位置を円周上の開始点（角度0の位置）として扱い、中心を逆算
                    // 角度0は右方向（Cos(0)=1, Sin(0)=0）なので、中心は現在位置の左
                    circleAngle = 0f;
                    circleCenter = new Vector3(
                        transform.position.x - currentMoveType.circleRadius,
                        transform.position.y,
                        transform.position.z
                    );

                    // 初期位置のちらつきを防ぐため、一時的にスプライトを非表示
                    isCircleFirstFrame = true;
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.enabled = false;
                    }
                }
                break;
            case EnemyData.MoveType.PatternType.Figure8:
                // 前回と異なるパターンから切り替わった場合のみ初期化
                if (previousPatternType != EnemyData.MoveType.PatternType.Figure8)
                {
                    figure8Angle = 0f;
                    // 現在位置を中心として8の字を開始
                    figure8Center = transform.position;

                    // 初期位置のちらつきを防ぐため、一時的にスプライトを非表示
                    isFigure8FirstFrame = true;
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.enabled = false;
                    }
                }
                break;
            case EnemyData.MoveType.PatternType.Zigzag:
                // Zigzagは常に現在位置から新規開始（Direction Degが異なる可能性があるため）
                zigzagProgress = 0f;
                // 現在位置を起点としてジグザグを開始
                zigzagCenter = transform.position;

                // 初期位置のちらつきを防ぐため、一時的にスプライトを非表示
                isZigzagFirstFrame = true;
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = false;
                }
                break;
            case EnemyData.MoveType.PatternType.SineWave:
                // SineWaveは常に現在位置から新規開始（Direction Degが異なる可能性があるため）
                sineTime = 0f;
                // 現在位置を起点としてサイン波を開始
                sineWaveCenter = transform.position;

                // 初期位置のちらつきを防ぐため、一時的にスプライトを非表示
                isSineWaveFirstFrame = true;
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = false;
                }
                break;
            case EnemyData.MoveType.PatternType.Lissajous:
                // Lissajousは常に現在位置から新規開始（パラメータが異なる可能性があるため）
                lissajousTime = 0f;
                // 現在位置を中心としてリサージュ曲線を開始
                lissajousCenter = transform.position;

                // 初期位置のちらつきを防ぐため、一時的にスプライトを非表示
                isLissajousFirstFrame = true;
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = false;
                }
                break;
            case EnemyData.MoveType.PatternType.RandomWalk:
                randomWalkNextChangeTime = Time.time + currentMoveType.randomWalkChangeInterval;
                randomWalkDirection = GetRandomDirection();
                break;

            case EnemyData.MoveType.PatternType.Hopping:
                // Hopping開始時は着地状態から始まる
                hoppingState = HoppingState.Grounded;
                hoppingStateStartTime = Time.time;
                hoppingJumpStartPos = transform.position;
                // 最初のジャンプ方向を決定
                hoppingNextDirection = ChooseNextHoppingDirection();
                break;

            case EnemyData.MoveType.PatternType.Warp:
                // Warp開始時は次のワープ時刻を設定
                warpNextTime = Time.time + currentMoveType.warpGroundedDuration;
                break;
        }

        // 次回の判定用に現在のパターンタイプを保存
        previousPatternType = newPatternType;
    }

    // ランダム距離を取得（useRandomDistanceに応じて）
    private float GetRandomDistance(EnemyData.MoveType moveType)
    {
        if (moveType.useRandomDistance)
        {
            float min = Mathf.Max(0.1f, moveType.randomDistanceMin);
            float max = Mathf.Max(min, moveType.randomDistanceMax);
            // rangeを超えないように制限
            max = Mathf.Min(max, moveType.range);
            return Random.Range(min, max);
        }
        else
        {
            return moveType.range;
        }
    }

    private void ApplyMovePattern()
    {
        if (currentMoveType == null) return;

        switch (currentMoveType.patternType)
        {
            case EnemyData.MoveType.PatternType.None:
                // 停止
                break;

            case EnemyData.MoveType.PatternType.Horizontal:
                ApplyHorizontalMove();
                break;

            case EnemyData.MoveType.PatternType.Vertical:
                ApplyVerticalMove();
                break;

            case EnemyData.MoveType.PatternType.Diagonal:
                ApplyDiagonalMove();
                break;

            case EnemyData.MoveType.PatternType.Circle:
                ApplyCircleMove();
                break;

            case EnemyData.MoveType.PatternType.Figure8:
                ApplyFigure8Move();
                break;

            case EnemyData.MoveType.PatternType.Zigzag:
                ApplyZigzagMove();
                break;

            case EnemyData.MoveType.PatternType.RandomWalk:
                ApplyRandomWalkMove();
                break;

            case EnemyData.MoveType.PatternType.SineWave:
                ApplySineWaveMove();
                break;

            case EnemyData.MoveType.PatternType.Lissajous:
                ApplyLissajousMove();
                break;

            case EnemyData.MoveType.PatternType.Hopping:
                ApplyHoppingMove();
                break;

            case EnemyData.MoveType.PatternType.Warp:
                ApplyWarpMove();
                break;
        }
    }

    private void ApplyLegacyMove()
    {
        float timeScale = GetTimeScale();
        transform.position += Vector3.right * dir * moveSpeed * speedMultiplier * Time.deltaTime * timeScale;

        float offset = transform.position.x - startPos.x;
        if (offset > moveRange) dir = -1;
        else if (offset < -moveRange) dir = 1;
    }

    private void ApplyHorizontalMove()
    {
        float timeScale = GetTimeScale();
        Vector3 newPos = transform.position + Vector3.right * dir * currentMoveType.speed * speedMultiplier * Time.deltaTime * timeScale;
        float offset = newPos.x - patternStartPos.x;  // パターン開始位置からのオフセット（目標距離判定用）
        float absoluteOffset = newPos.x - startPos.x;  // 最初の初期位置からのオフセット（絶対制限用）

        // 現在の目標距離を使用
        float targetDistance = currentHorizontalTargetDistance;

        // 絶対的な制限として、最初の初期位置からrangeXを使用（位置をクランプ）
        float rangeX = GetRangeX();
        if (Mathf.Abs(absoluteOffset) > rangeX)
        {
            // rangeXを超えた場合は位置をrangeX境界にクランプして方向転換
            float clampedX = startPos.x + Mathf.Sign(absoluteOffset) * rangeX;
            SetPosition(new Vector3(clampedX, newPos.y, newPos.z));
            dir = -dir;
            currentHorizontalTargetDistance = GetRandomDistance(currentMoveType);
        }
        else if (dir > 0 && offset > targetDistance)
        {
            // 右方向の目標距離に到達
            SetPosition(newPos);
            dir = -1;
            currentHorizontalTargetDistance = GetRandomDistance(currentMoveType);
        }
        else if (dir < 0 && offset < -targetDistance)
        {
            // 左方向の目標距離に到達
            SetPosition(newPos);
            dir = 1;
            currentHorizontalTargetDistance = GetRandomDistance(currentMoveType);
        }
        else
        {
            SetPosition(newPos);
        }
    }

    private void ApplyVerticalMove()
    {
        float timeScale = GetTimeScale();
        Vector3 newPos = transform.position + Vector3.up * dir * currentMoveType.speed * speedMultiplier * Time.deltaTime * timeScale;
        float offset = newPos.y - patternStartPos.y;  // パターン開始位置からのオフセット（目標距離判定用）
        float absoluteOffset = newPos.y - startPos.y;  // 最初の初期位置からのオフセット（絶対制限用）

        // 現在の目標距離を使用
        float targetDistance = currentVerticalTargetDistance;

        // 絶対的な制限として、最初の初期位置からrangeYを使用（位置をクランプ）
        float rangeY = GetRangeY();
        if (Mathf.Abs(absoluteOffset) > rangeY)
        {
            // rangeYを超えた場合は位置をrangeY境界にクランプして方向転換
            float clampedY = startPos.y + Mathf.Sign(absoluteOffset) * rangeY;
            SetPosition(new Vector3(newPos.x, clampedY, newPos.z));
            dir = -dir;
            currentVerticalTargetDistance = GetRandomDistance(currentMoveType);
        }
        else if (dir > 0 && offset > targetDistance)
        {
            // 上方向の目標距離に到達
            SetPosition(newPos);
            dir = -1;
            currentVerticalTargetDistance = GetRandomDistance(currentMoveType);
        }
        else if (dir < 0 && offset < -targetDistance)
        {
            // 下方向の目標距離に到達
            SetPosition(newPos);
            dir = 1;
            currentVerticalTargetDistance = GetRandomDistance(currentMoveType);
        }
        else
        {
            SetPosition(newPos);
        }
    }

    private void ApplyDiagonalMove()
    {
        float timeScale = GetTimeScale();
        Vector2 dirVec = GetDirectionVector(currentMoveType.directionDeg);
        Vector3 newPos = transform.position + (Vector3)dirVec * dir * currentMoveType.speed * speedMultiplier * Time.deltaTime * timeScale;

        // 最初の初期位置（startPos）からの距離で判定（Move Type切り替えでずれない）
        float offset = Vector2.Distance(newPos, startPos);
        if (offset > currentMoveType.range)
        {
            // rangeを超えた場合は位置をrange境界にクランプして方向転換
            Vector2 direction = ((Vector2)newPos - (Vector2)startPos).normalized;
            Vector2 clampedPos = (Vector2)startPos + direction * currentMoveType.range;
            SetPosition(new Vector3(clampedPos.x, clampedPos.y, newPos.z));
            dir = -dir;
        }
        else
        {
            SetPosition(newPos);
        }
    }

    private void ApplyCircleMove()
    {
        // 初期化直後の最初のフレームは位置を変更せず、スプライト表示のみ
        if (isCircleFirstFrame)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
            isCircleFirstFrame = false;
            return;  // 位置は現在位置（Horizontal終了位置）のまま維持
        }

        // 増分的に角度を更新（Move Type切り替え時も現在の角度から継続）
        float angleSpeed = (360f / currentMoveType.circlePeriod) * (currentMoveType.circleClockwise ? -1f : 1f);
        circleAngle += angleSpeed * Mathf.Deg2Rad * Time.deltaTime * GetTimeScale();

        // 保存された中心位置（circleCenter）を使用して円運動
        float x = circleCenter.x + Mathf.Cos(circleAngle) * currentMoveType.circleRadius;
        float y = circleCenter.y + Mathf.Sin(circleAngle) * currentMoveType.circleRadius;
        SetPosition(new Vector3(x, y, transform.position.z));
    }

    private void ApplyFigure8Move()
    {
        // 初期化直後の最初のフレームは位置を変更せず、スプライト表示のみ
        if (isFigure8FirstFrame)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
            isFigure8FirstFrame = false;
            return;  // 位置は現在位置のまま維持
        }

        // 増分的に角度を更新（Move Type切り替え時も現在の角度から継続）
        figure8Angle += (Mathf.PI * 2f / currentMoveType.figure8Period) * Time.deltaTime * GetTimeScale();

        // 保存された中心位置（figure8Center）を使用して8の字
        float x = figure8Center.x + Mathf.Sin(figure8Angle) * currentMoveType.figure8Width;
        float y = figure8Center.y + Mathf.Sin(figure8Angle * 2f) * currentMoveType.figure8Height;
        SetPosition(new Vector3(x, y, transform.position.z));
    }

    private void ApplyZigzagMove()
    {
        // 初期化直後の最初のフレームは位置を変更せず、スプライト表示のみ
        if (isZigzagFirstFrame)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
            isZigzagFirstFrame = false;
            return;  // 位置は現在位置のまま維持
        }

        Vector2 forwardDir = GetDirectionVector(currentMoveType.directionDeg);
        zigzagProgress += currentMoveType.speed * speedMultiplier * Time.deltaTime * GetTimeScale();

        float zigzagOffset = Mathf.Sin((zigzagProgress / currentMoveType.zigzagPeriodLength) * Mathf.PI * 2f) * currentMoveType.zigzagWidth;
        Vector2 perpendicular = new Vector2(-forwardDir.y, forwardDir.x);

        // 保存された中心位置（zigzagCenter）を起点にジグザグ
        Vector3 newPos = zigzagCenter + (Vector3)(forwardDir * zigzagProgress) + (Vector3)(perpendicular * zigzagOffset);
        SetPosition(newPos);
    }

    private void ApplyRandomWalkMove()
    {
        if (Time.time >= randomWalkNextChangeTime)
        {
            randomWalkDirection = GetRandomDirection();
            randomWalkNextChangeTime = Time.time + currentMoveType.randomWalkChangeInterval;
        }

        Vector3 newPos = transform.position + (Vector3)randomWalkDirection * currentMoveType.speed * speedMultiplier * Time.deltaTime * GetTimeScale();
        SetPosition(newPos);
    }

    private void ApplySineWaveMove()
    {
        // 初期化直後の最初のフレームは位置を変更せず、スプライト表示のみ
        if (isSineWaveFirstFrame)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
            isSineWaveFirstFrame = false;
            return;  // 位置は現在位置のまま維持
        }

        sineTime += Time.deltaTime * GetTimeScale();
        Vector2 forwardDir = GetDirectionVector(currentMoveType.sineDirectionDeg);
        Vector2 perpendicular = new Vector2(-forwardDir.y, forwardDir.x);

        float forwardDistance = currentMoveType.speed * speedMultiplier * sineTime;
        float waveOffset = Mathf.Sin(sineTime * currentMoveType.sineFrequency * Mathf.PI * 2f) * currentMoveType.sineAmplitude;

        // 保存された中心位置（sineWaveCenter）を起点にサイン波
        Vector3 newPos = sineWaveCenter + (Vector3)(forwardDir * forwardDistance) + (Vector3)(perpendicular * waveOffset);
        SetPosition(newPos);
    }

    private void ApplyLissajousMove()
    {
        // 初期化直後の最初のフレームは位置を変更せず、スプライト表示のみ
        if (isLissajousFirstFrame)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
            isLissajousFirstFrame = false;
            return;  // 位置は現在位置のまま維持
        }

        lissajousTime += Time.deltaTime * GetTimeScale();
        float phaseRad = currentMoveType.lissajousPhase * Mathf.Deg2Rad;

        // 保存された中心位置（lissajousCenter）を中心にリサージュ曲線
        float x = lissajousCenter.x + Mathf.Sin(lissajousTime * currentMoveType.lissajousFrequencyX * Mathf.PI * 2f + phaseRad) * currentMoveType.lissajousAmplitudeX;
        float y = lissajousCenter.y + Mathf.Sin(lissajousTime * currentMoveType.lissajousFrequencyY * Mathf.PI * 2f) * currentMoveType.lissajousAmplitudeY;

        SetPosition(new Vector3(x, y, transform.position.z));
    }

    private void ApplyHoppingMove()
    {
        float elapsedTime = Time.time - hoppingStateStartTime;

        if (hoppingState == HoppingState.Grounded)
        {
            // 着地状態：停止時間が経過したらジャンプ開始
            if (elapsedTime >= currentMoveType.hoppingGroundedDuration)
            {
                // ジャンプ開始
                hoppingState = HoppingState.Jumping;
                hoppingStateStartTime = Time.time;
                hoppingJumpStartPos = transform.position;
            }
            // 着地中は位置を変更しない
        }
        else if (hoppingState == HoppingState.Jumping)
        {
            // ジャンプ状態：放物線を描いて移動
            if (elapsedTime >= currentMoveType.hoppingJumpDuration)
            {
                // ジャンプ完了：着地状態に遷移
                hoppingState = HoppingState.Grounded;
                hoppingStateStartTime = Time.time;

                // 着地位置を計算（ジャンプ開始位置 + 進行方向 × 距離）
                Vector2 direction = GetDirectionVector(hoppingNextDirection);
                Vector3 landingPos = hoppingJumpStartPos + new Vector3(
                    direction.x * currentMoveType.hoppingJumpDistance,
                    direction.y * currentMoveType.hoppingJumpDistance,
                    0f
                );
                SetPosition(landingPos);

                // 次のジャンプ方向を決定
                hoppingNextDirection = ChooseNextHoppingDirection();
            }
            else
            {
                // ジャンプ中：放物線運動
                float t = elapsedTime / currentMoveType.hoppingJumpDuration; // 0～1

                // 横方向：線形移動
                Vector2 direction = GetDirectionVector(hoppingNextDirection);
                float horizontalProgress = t * currentMoveType.hoppingJumpDistance;

                // 縦方向：放物線（頂点がジャンプの中間地点）
                // y = -4 * height * (t - 0.5)^2 + height
                // これで t=0.5 で height、t=0 と t=1 で 0 になる
                float verticalOffset = -4f * currentMoveType.hoppingJumpHeight * (t - 0.5f) * (t - 0.5f) + currentMoveType.hoppingJumpHeight;

                Vector3 newPos = hoppingJumpStartPos + new Vector3(
                    direction.x * horizontalProgress,
                    direction.y * horizontalProgress + verticalOffset,
                    0f
                );
                SetPosition(newPos);
            }
        }
    }

    private void ApplyWarpMove()
    {
        // ワープ時刻になったら瞬間移動
        if (Time.time >= warpNextTime)
        {
            // ランダムな位置にワープ
            float rangeX = GetRangeX();
            float rangeY = GetRangeY();

            float randomX = startPos.x + Random.Range(-rangeX, rangeX);
            float randomY = startPos.y + Random.Range(-rangeY, rangeY);

            Vector3 warpPos = new Vector3(randomX, randomY, transform.position.z);
            SetPosition(warpPos);

            // ワープ直後にプレイヤーの方向を向く（rotateTowardPlayerが有効な場合）
            if (enemyShooter != null)
            {
                enemyShooter.RotateTowardPlayerIfNeeded();
            }

            // 次のワープ時刻を設定
            warpNextTime = Time.time + currentMoveType.warpGroundedDuration;
        }
        // ワープ待機中は位置を変更しない
    }

    private Vector2 GetDirectionVector(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private Vector2 GetRandomDirection()
    {
        float baseAngle = currentMoveType.directionDeg;
        float randomOffset = Random.Range(-currentMoveType.randomWalkAngleRange, currentMoveType.randomWalkAngleRange);
        float angle = (baseAngle + randomOffset) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // =========================================================
    // Range取得ヘルパー（rangeX/rangeYが0の場合はrangeを使用）
    // =========================================================
    private float GetRangeX()
    {
        if (currentMoveType == null) return 0f;
        return currentMoveType.rangeX > 0f ? currentMoveType.rangeX : currentMoveType.range;
    }

    private float GetRangeY()
    {
        if (currentMoveType == null) return 0f;
        return currentMoveType.rangeY > 0f ? currentMoveType.rangeY : currentMoveType.range;
    }

    private float ChooseNextHoppingDirection()
    {
        // 4方向の候補（右、上、左、下）
        float[] candidateDirections = { 0f, 90f, 180f, 270f };
        System.Collections.Generic.List<float> validDirections = new System.Collections.Generic.List<float>();

        // 現在位置
        Vector3 currentPos = transform.position;
        float rangeX = GetRangeX();
        float rangeY = GetRangeY();

        // 各方向について、ジャンプ後の着地位置が範囲内かチェック
        foreach (float direction in candidateDirections)
        {
            Vector2 dir = GetDirectionVector(direction);
            Vector3 landingPos = currentPos + new Vector3(
                dir.x * currentMoveType.hoppingJumpDistance,
                dir.y * currentMoveType.hoppingJumpDistance,
                0f
            );

            // 範囲チェック：startPos を中心に rangeX/rangeY の範囲内か
            float xOffset = Mathf.Abs(landingPos.x - startPos.x);
            float yOffset = Mathf.Abs(landingPos.y - startPos.y);

            if (xOffset <= rangeX && yOffset <= rangeY)
            {
                validDirections.Add(direction);
            }
        }

        // 有効な方向があればランダムに選択
        if (validDirections.Count > 0)
        {
            int randomIndex = Random.Range(0, validDirections.Count);
            return validDirections[randomIndex];
        }

        // すべての方向が範囲外の場合、中央（startPos）方向にジャンプ
        Vector3 toCenter = startPos - currentPos;
        float angleToCenter = Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg;

        // 4方向の中から最も近い方向を選択
        float closestDirection = 0f;
        float minAngleDiff = float.MaxValue;

        foreach (float direction in candidateDirections)
        {
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(direction, angleToCenter));
            if (angleDiff < minAngleDiff)
            {
                minAngleDiff = angleDiff;
                closestDirection = direction;
            }
        }

        return closestDirection;
    }

    // =========================================================
    // Debug Info (デバッグ情報取得)
    // =========================================================
    public struct DebugMoveRoutineInfo
    {
        public bool isUsingRoutine;
        public EnemyData.MoveFiringRoutine.RoutineType? routineType;
        public int currentElementIndex;
        public string currentMoveTypeName;
    }

    public DebugMoveRoutineInfo GetDebugMoveRoutineInfo()
    {
        DebugMoveRoutineInfo info = new DebugMoveRoutineInfo();

        EnemyData.MoveFiringRoutine routine = GetCurrentMoveFiringRoutine();
        if (enemyData != null && routine != null)
        {
            info.isUsingRoutine = true;
            info.routineType = routine.routineType;

            // Sequenceルーチンの場合はSequence Entriesのインデックス、それ以外はMove Typeのインデックス
            if (routine.routineType == EnemyData.MoveFiringRoutine.RoutineType.Sequence)
            {
                info.currentElementIndex = moveSequenceEntryIndex;
            }
            else
            {
                info.currentElementIndex = currentMoveTypeIndex;
            }

            if (currentMoveTypeIndex >= 0 && currentMoveTypeIndex < enemyData.moveTypes.Length && enemyData.moveTypes[currentMoveTypeIndex] != null)
            {
                info.currentMoveTypeName = enemyData.moveTypes[currentMoveTypeIndex].name;
            }
            else
            {
                info.currentMoveTypeName = "None";
            }
        }
        else
        {
            info.isUsingRoutine = false;
            info.routineType = null;
            info.currentElementIndex = currentMoveTypeIndex;
            
            if (currentMoveTypeIndex >= 0 && HasMoveTypes() && currentMoveTypeIndex < enemyData.moveTypes.Length && enemyData.moveTypes[currentMoveTypeIndex] != null)
            {
                info.currentMoveTypeName = enemyData.moveTypes[currentMoveTypeIndex].name;
            }
            else
            {
                info.currentMoveTypeName = "None";
            }
        }
        
        return info;
    }

    // =========================================================
    // デバッグ情報取得（移動距離）
    // =========================================================
    public struct DebugDistanceInfo
    {
        public bool isHorizontalOrVertical;  // Horizontal/Verticalパターンかどうか
        public bool isHopping;               // Hoppingパターンかどうか
        public bool isWarp;                  // Warpパターンかどうか
        public float currentOffset;          // patternStartPosからの現在のオフセット
        public float targetDistance;         // 現在の目標距離
        public float maxRange;               // 最大制限距離（range）- 後方互換性のため残す
        public float maxRangeX;              // X方向の最大制限距離
        public float maxRangeY;              // Y方向の最大制限距離
        public string patternName;           // パターン名（"Horizontal" or "Vertical" or "Hopping" or "Warp"）
        public float offsetX;                // Hopping/Warp用：X方向のオフセット
        public float offsetY;                // Hopping/Warp用：Y方向のオフセット
    }

    public DebugDistanceInfo GetDebugDistanceInfo()
    {
        DebugDistanceInfo info = new DebugDistanceInfo();

        if (currentMoveType == null)
        {
            info.isHorizontalOrVertical = false;
            return info;
        }

        if (currentMoveType.patternType == EnemyData.MoveType.PatternType.Horizontal)
        {
            info.isHorizontalOrVertical = true;
            info.currentOffset = transform.position.x - startPos.x;  // 最初の初期位置からの距離（右+、左-）
            info.targetDistance = currentHorizontalTargetDistance;
            info.maxRange = currentMoveType.range;
            info.maxRangeX = GetRangeX();
            info.maxRangeY = GetRangeY();
            info.patternName = "Horizontal";
        }
        else if (currentMoveType.patternType == EnemyData.MoveType.PatternType.Vertical)
        {
            info.isHorizontalOrVertical = true;
            info.currentOffset = transform.position.y - startPos.y;  // 最初の初期位置からの距離（上+、下-）
            info.targetDistance = currentVerticalTargetDistance;
            info.maxRange = currentMoveType.range;
            info.maxRangeX = GetRangeX();
            info.maxRangeY = GetRangeY();
            info.patternName = "Vertical";
        }
        else if (currentMoveType.patternType == EnemyData.MoveType.PatternType.Hopping)
        {
            info.isHopping = true;
            info.offsetX = transform.position.x - startPos.x;  // X方向のオフセット
            info.offsetY = transform.position.y - startPos.y;  // Y方向のオフセット
            // 2D距離を計算
            info.currentOffset = Mathf.Sqrt(info.offsetX * info.offsetX + info.offsetY * info.offsetY);
            info.targetDistance = currentMoveType.hoppingJumpDistance;
            info.maxRange = currentMoveType.range;
            info.maxRangeX = GetRangeX();
            info.maxRangeY = GetRangeY();
            info.patternName = "Hopping";
        }
        else if (currentMoveType.patternType == EnemyData.MoveType.PatternType.Warp)
        {
            info.isWarp = true;
            info.offsetX = transform.position.x - startPos.x;  // X方向のオフセット
            info.offsetY = transform.position.y - startPos.y;  // Y方向のオフセット
            // 2D距離を計算
            info.currentOffset = Mathf.Sqrt(info.offsetX * info.offsetX + info.offsetY * info.offsetY);
            info.targetDistance = 0f; // Warpには目標距離はない
            info.maxRange = currentMoveType.range;
            info.maxRangeX = GetRangeX();
            info.maxRangeY = GetRangeY();
            info.patternName = "Warp";
        }
        else
        {
            info.isHorizontalOrVertical = false;
            info.isHopping = false;
            info.isWarp = false;
        }

        return info;
    }

    // =========================================================
    // HP-Based Routine Switching (HP％に応じたルーチン切り替え)
    // =========================================================
    private void InitializeHpBasedRoutine()
    {
        if (enemyData == null || !enemyData.useHpBasedRoutineSwitch) return;
        if (enemyStats == null) return;

        // 初期状態では高HP用ルーチンを使用
        int routineIndex = (int)enemyData.moveRoutineAboveThreshold;
        if (routineIndex >= 0 && routineIndex < enemyData.moveFiringRoutines.Length)
        {
            currentMoveFiringRoutine = enemyData.moveFiringRoutines[routineIndex];
        }
    }

    private void CheckHpAndSwitchRoutine()
    {
        if (enemyData == null || !enemyData.useHpBasedRoutineSwitch) return;
        if (enemyStats == null) return;
        if (hasSwitchedToLowHpRoutine) return;  // 一度切り替わったら戻らない

        // HP％を計算
        float hpPercentage = enemyStats.GetHpPercentage();

        // HP閾値を下回ったら低HP用ルーチンに切り替え
        if (hpPercentage < enemyData.hpThresholdPercentage)
        {
            hasSwitchedToLowHpRoutine = true;
            int routineIndex = (int)enemyData.moveRoutineBelowThreshold;
            if (routineIndex >= 0 && routineIndex < enemyData.moveFiringRoutines.Length)
            {
                currentMoveFiringRoutine = enemyData.moveFiringRoutines[routineIndex];

                // ルーチン切り替え時に即座に新しいルーチンから Move Type を選び直す
                if (currentMoveFiringRoutine != null)
                {
                    if (currentMoveFiringRoutine.routineType == EnemyData.MoveFiringRoutine.RoutineType.Sequence)
                    {
                        // Sequence の場合: インデックスと残り時間をリセット
                        moveSequenceEntryIndex = -1;
                        moveSequenceRemainingTime = 0f;
                    }
                    else if (currentMoveFiringRoutine.routineType == EnemyData.MoveFiringRoutine.RoutineType.Probability)
                    {
                        // Probability の場合: 即座に新しい Move Type を選択
                        PickMoveTypeByProbability();
                    }
                }
            }
        }
    }

    // 現在使用中のルーチンを取得
    private EnemyData.MoveFiringRoutine GetCurrentMoveFiringRoutine()
    {
        if (enemyData == null) return null;

        // HP-Based Routine Switchingが有効な場合
        if (enemyData.useHpBasedRoutineSwitch && currentMoveFiringRoutine != null)
        {
            return currentMoveFiringRoutine;
        }

        // 従来のルーチン
        if (enemyData.useMoveFiringRoutine)
        {
            return enemyData.moveFiringRoutine;
        }

        return null;
    }

    // =========================================================
    // Rigidbody2D対応の位置設定ヘルパー
    // =========================================================
    /// <summary>
    /// Rigidbody2Dがあれば物理演算を使った移動、なければTransformを直接操作
    /// 複数パーツ敵（HingeJoint2D使用）でも正しく揺れるようにする
    /// </summary>
    private void SetPosition(Vector3 newPosition)
    {
        if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
        {
            // ★Kinematicモード：MovePositionで移動（子がDynamicなら追従して揺れる）
            rb.MovePosition(newPosition);
        }
        else if (rb != null)
        {
            // ★Dynamicモード：速度ベースで移動（Jointとの互換性のため）
            Vector2 delta = newPosition - transform.position;
            if (Time.deltaTime > 0)
            {
                rb.linearVelocity = delta / Time.deltaTime;
            }
        }
        else
        {
            // Rigidbody2Dがない場合は従来通りTransformを直接操作
            transform.position = newPosition;
        }
    }

    // =========================================================
    // Body反転処理（複数パーツ敵用）
    // =========================================================
    private void FlipBody()
    {
        Debug.Log($"[EnemyMover] FlipBody called - searching for Body in {transform.childCount} children");

        // HingeJoint2Dを持つBodyオブジェクトを探す（正しいBodyを見つける）
        Transform bodyTransform = null;
        foreach (Transform child in transform)
        {
            Debug.Log($"[EnemyMover] FlipBody - checking child: {child.name}");
            if (child.name.Contains("Body"))
            {
                // HingeJoint2Dがあるかチェック
                HingeJoint2D hinge = child.GetComponent<HingeJoint2D>();
                if (hinge != null)
                {
                    bodyTransform = child;
                    Debug.Log($"[EnemyMover] FlipBody - found Body with HingeJoint2D: {child.name}");
                    break;
                }
                else
                {
                    Debug.Log($"[EnemyMover] FlipBody - {child.name} has no HingeJoint2D, skipping");
                }
            }
        }

        if (bodyTransform == null)
        {
            Debug.LogWarning("[EnemyMover] FlipBody - Body not found! Cannot flip.");
            return;
        }

        // ★Position の X座標を反転
        Vector3 oldPos = bodyTransform.localPosition;
        Vector3 newPos = oldPos;
        newPos.x = -newPos.x;
        bodyTransform.localPosition = newPos;

        Debug.Log($"[EnemyMover] FlipBody - Position changed: {oldPos} → {newPos}");

        // ★HingeJoint2D の Anchor と Connected Anchor を反転
        HingeJoint2D hingeJoint = bodyTransform.GetComponent<HingeJoint2D>();
        if (hingeJoint != null)
        {
            Vector2 oldAnchor = hingeJoint.anchor;
            Vector2 newAnchor = oldAnchor;
            newAnchor.x = -newAnchor.x;
            hingeJoint.anchor = newAnchor;

            Vector2 oldConnectedAnchor = hingeJoint.connectedAnchor;
            Vector2 newConnectedAnchor = oldConnectedAnchor;
            newConnectedAnchor.x = -newConnectedAnchor.x;
            hingeJoint.connectedAnchor = newConnectedAnchor;

            Debug.Log($"[EnemyMover] FlipBody - Anchor: {oldAnchor} → {newAnchor}");
            Debug.Log($"[EnemyMover] FlipBody - ConnectedAnchor: {oldConnectedAnchor} → {newConnectedAnchor}");
        }
        else
        {
            Debug.LogWarning("[EnemyMover] FlipBody - HingeJoint2D not found on Body!");
        }
    }
}
