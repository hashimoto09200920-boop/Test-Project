using System.Collections;
using UnityEngine;

/// <summary>
/// Gyrorb（Area07雑魚敵・天球儀球）専用コントローラー。
/// 直線移動+方向転換のバウンド移動（SlimeEnemy方式）をベースに、
/// 移動した距離に応じてスプライト自体を回転させ「転がっている」ように見せる（TurtleRoller方式）。
/// GyroWardと異なり分裂は行わない。
/// </summary>
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyDamageReceiver))]
public class GyrorbController : MonoBehaviour
{
    // =========================================================
    // Editor Preview（GyroWardControllerと同じ構成パターン）
    // =========================================================

    public enum GyrorbPreviewSprite
    {
        Idle1, Idle2, Idle3, IdleAnimate
    }

    [System.Serializable]
    public class GyrorbFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数")]
        public float   duration;
        [Tooltip("未使用（他エネミーとのフレーム構造統一のため保持）")]
        public Vector2 muzzleOffset;
        [Tooltip("未使用（当たり判定はRoot固定のため）")]
        public Vector2 colliderSize;
        [Tooltip("未使用")]
        public Vector2 colliderOffset;
    }

    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    [Tooltip("本体スプライト（子オブジェクトBody）。未設定なら子から自動取得する")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;

    [Header("Editor Preview")]
    [SerializeField] private GyrorbPreviewSprite previewSprite = GyrorbPreviewSprite.Idle1;

    [Header("Sprites")]
    [Tooltip("目の発光強度違いの3枚（Idle1=中間/Idle2=暗め/Idle3=最大輝度）。この順でループ再生する")]
    [NonReorderable]
    [SerializeField] private GyrorbFrame[] idleFrames;

    // =========================================================
    // Fade In（EnemyStats.FadeIn()はRoot上にSpriteRendererが無く効かないため独自実装。
    // Gyrorbは分裂せず常にEnemySpawner経由の通常スポーンのため、
    // EnemySpawner.GetCurrentStageIndex()を使ってStage毎の時間を再現する）
    // =========================================================

    [Header("Fade In（他エネミーのStage別フェードインと同じ値を維持すること）")]
    [Tooltip("Stage1/2 のフェードイン時間（秒）。EnemySpawnerのstage12FadeInDurationと合わせる")]
    [SerializeField] private float stage12FadeInDuration = 1f;
    [Tooltip("Stage3 のフェードイン時間（秒）。EnemySpawnerのstage3FadeInDurationと合わせる")]
    [SerializeField] private float stage3FadeInDuration = 3f;

    // =========================================================
    // Rolling Movement（直線転がり+方向転換、SlimeEnemy方式のバウンド移動を踏襲）
    // =========================================================

    [Header("Rolling Movement")]
    [Tooltip("基本移動速度（ワールド単位/秒）")]
    [SerializeField] private float rollSpeed = 2.5f;
    [Tooltip("転がり回転の計算に使う本体の半径（CircleCollider2Dの半径と合わせる）")]
    [SerializeField] private float bodyRadius = 0.55f;
    [Tooltip("方向転換までの間隔（秒・最小）")]
    [SerializeField] private float directionChangeIntervalMin = 2f;
    [Tooltip("方向転換までの間隔（秒・最大）")]
    [SerializeField] private float directionChangeIntervalMax = 4f;
    [Tooltip("壁バウンス後、次の方向転換を許可するまでの最短間隔（秒）")]
    [SerializeField] private float bounceProtectionSeconds = 0.4f;

    [Header("Speed Loop（遅→速→遅を周期的に繰り返す）")]
    [Tooltip("1周期の長さ（秒）")]
    [SerializeField] private float speedLoopDuration = 3f;
    [Tooltip("横軸=周期内の進行割合(0→1)、縦軸=速度倍率の重み(0→1)。既定は緩急が山なりに変化するイージング")]
    [SerializeField] private AnimationCurve speedLoopCurve = new AnimationCurve(
        new Keyframe(0f,   0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f,   0f)
    );
    [SerializeField] private float speedLoopMinMultiplier = 0.35f;
    [SerializeField] private float speedLoopMaxMultiplier = 1.6f;

    [Header("Screen Bounds")]
    [Tooltip("画面端からの安全マージン（ワールド単位）")]
    [SerializeField] private float screenMargin = 0.5f;
    [Tooltip("左側SkillHUDのピクセル幅（この分だけ出現・移動範囲から除外）")]
    [SerializeField] private float skillHudPixelWidth = 280f;

    [Header("Floor Avoidance")]
    [Tooltip("Floorオブジェクトへの参照。未設定の場合はシーンからFloorHealthコンポーネントで自動検索する")]
    [SerializeField] private FloorHealth floorObject;
    [Tooltip("Floorの上端からこの距離以内に近づかないようにする（ワールド単位）")]
    [SerializeField] private float floorAvoidDistance = 1.5f;

    // =========================================================
    // Top Bombardment Attack（画面上部のランダム位置からFloorのランダム位置を狙ってSpeedCurve弾を発射）
    // =========================================================

    [Header("Top Bombardment Attack")]
    [Tooltip("発射間隔（秒）")]
    [SerializeField] private float bombardInterval = 4f;
    [Tooltip("EnemyData.bulletTypesのインデックス（Speed Curve弾。全エネミー共通で実装済みのため既定2）")]
    [SerializeField] private int bombardBulletTypeIndex = 2;
    [Tooltip("画面上端からのYオフセット（ワールド単位）。負の値にすると画面内から発生する（エフェクトで出現を見せる前提）")]
    [SerializeField] private float bombardSpawnYOffset = -2f;
    [Tooltip("発射地点に再生するエフェクト（エネルギー収束）。未設定ならエフェクト無しで即発射")]
    [SerializeField] private ParticleSystem convergeBurstVfxPrefab;
    [Tooltip("エフェクトの収束時間（秒）。この時間だけ待ってから実際に弾を発射する。VFX側のParticle Systemのstart lifetimeと合わせる")]
    [SerializeField] private float convergeBurstDuration = 0.5f;
    [Tooltip("発射前にGyrorb自身を明滅させる予兆色（このエネミー自身が攻撃元だと分かりやすくするため）")]
    [SerializeField] private Color bombardTelegraphColor = new Color(1f, 0.65f, 0.2f, 1f);
    [Tooltip("予兆の明滅速度（値が大きいほど早く点滅する）")]
    [SerializeField] private float bombardTelegraphPulseSpeed = 6f;
    [Tooltip("Gyrorb自身の位置に再生するチャージエフェクト（このエネミー自身がエネルギーを溜めていることを見せる）。未設定なら明滅のみ")]
    [SerializeField] private ParticleSystem chargeAuraVfxPrefab;

    // =========================================================
    // 定数
    // =========================================================

    private static readonly Vector2[] EightDirections = new Vector2[]
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
        new Vector2( 1f,  1f).normalized, new Vector2(-1f,  1f).normalized,
        new Vector2( 1f, -1f).normalized, new Vector2(-1f, -1f).normalized,
    };

    // =========================================================
    // インスタンス変数
    // =========================================================

    private EnemyStats stats;
    private EnemyDamageReceiver damageReceiver;
    private EnemyMover enemyMover;

    private float SlowMultiplier => (enemyMover != null) ? enemyMover.SpeedMultiplier : 1f;

    private Vector2 moveDirection;
    private float directionChangeTimer;
    private float bounceProtectionTimer;
    private float currentZAngle;
    private float speedLoopTimer;
    private float bombardTimer;

    private float screenXMin, screenXMax, screenYMin, screenYMax;
    private float floorAvoidY = float.MinValue;

    private int idleFrameIndex = 0;
    private float idleFrameTimer = 0f;

    private Coroutine fadeInCoroutine;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        damageReceiver = GetComponent<EnemyDamageReceiver>();
        enemyMover = GetComponentInParent<EnemyMover>();

        if (bodySpriteRenderer == null) bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 独自移動のため、EnemyMover自体の移動処理は止める（B4デバフ/スロー効果の受け口としてのみ利用）
        if (enemyMover != null) enemyMover.suppressMovement = true;
    }

    private void OnEnable()
    {
        CacheFloorY();
        RefreshScreenBounds();

        moveDirection = EightDirections[Random.Range(0, EightDirections.Length)];
        directionChangeTimer = Random.Range(directionChangeIntervalMin, directionChangeIntervalMax);
        bounceProtectionTimer = 0f;
        currentZAngle = transform.eulerAngles.z;
        speedLoopTimer = 0f;

        idleFrameIndex = 0;
        idleFrameTimer = 0f;
        ApplyIdleFrame(0);

        bombardTimer = 0f;

        if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);
        fadeInCoroutine = StartCoroutine(FadeInBody());
    }

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Update()
    {
        float dt = Time.deltaTime * GetTimeScale() * SlowMultiplier;

        ApplyRollingMove(dt);
        TickIdleFrames(dt);
        TickBombardment(dt);
    }

    // =========================================================
    // フェードイン
    // =========================================================

    private IEnumerator FadeInBody()
    {
        // stageIndex取得より前に、まず1フレーム目からアルファ0にしておく。
        // ここをyield return null;の後に回すと、出現直後の1フレームだけ不透明のまま描画されてしまい、
        // 一瞬点滅して見えるバグになる
        if (bodySpriteRenderer != null)
        {
            Color initial = bodySpriteRenderer.color;
            bodySpriteRenderer.color = new Color(initial.r, initial.g, initial.b, 0f);
        }

        // EnemySpawnerはInstantiate直後の同フレーム内でOnEnable(このコルーチン開始)の後に
        // stats.SetSpawner()を呼ぶため、1フレーム待ってから参照しないとnullを拾ってしまう
        yield return null;

        int stageIndex = stats?.GetSpawner()?.GetCurrentStageIndex() ?? 0;
        float fadeInDuration = (stageIndex == 2) ? stage3FadeInDuration : stage12FadeInDuration;

        if (bodySpriteRenderer == null || fadeInDuration <= 0f) yield break;

        Color original = bodySpriteRenderer.color;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime * GetTimeScale();
            float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            bodySpriteRenderer.color = new Color(original.r, original.g, original.b, alpha);
            yield return null;
        }

        bodySpriteRenderer.color = new Color(original.r, original.g, original.b, 1f);
    }

    // =========================================================
    // 画面境界・Floor回避
    // =========================================================

    private void CacheFloorY()
    {
        if (floorObject == null)
            floorObject = FindObjectOfType<FloorHealth>();

        if (floorObject != null)
        {
            Collider2D col = floorObject.GetComponent<Collider2D>();
            float floorTopY = col != null ? col.bounds.max.y : floorObject.transform.position.y;
            floorAvoidY = floorTopY + floorAvoidDistance;
        }
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

        // screenMarginは画面端からの余白。本体の見た目の端（bodyRadius）が
        // その余白の内側に収まるよう、中心座標側にさらにbodyRadius分を足し込む
        screenXMin = camPos.x - halfW + screenMargin + bodyRadius + skillHudWorldOffset;
        screenXMax = camPos.x + halfW - screenMargin - bodyRadius;
        screenYMin = camPos.y - halfH + screenMargin + bodyRadius;
        screenYMax = camPos.y + halfH - screenMargin - bodyRadius;
    }

    // =========================================================
    // 転がり移動 + 回転
    // =========================================================

    private void ApplyRollingMove(float dt)
    {
        if (bounceProtectionTimer > 0f) bounceProtectionTimer -= dt;

        // Speed Loop: 遅→速→遅を周期的に繰り返す倍率を求める（GyroWardと同じ考え方）
        speedLoopTimer += dt;
        float loopT = (speedLoopDuration > 0.0001f)
            ? Mathf.Repeat(speedLoopTimer / speedLoopDuration, 1f)
            : 0f;
        float loopWeight = speedLoopCurve.Evaluate(loopT);
        float speedFactor = Mathf.Lerp(speedLoopMinMultiplier, speedLoopMaxMultiplier, loopWeight);

        directionChangeTimer -= dt;
        if (directionChangeTimer <= 0f)
        {
            moveDirection = EightDirections[Random.Range(0, EightDirections.Length)];
            directionChangeTimer = Random.Range(directionChangeIntervalMin, directionChangeIntervalMax);
        }

        Vector3 prevPos = transform.position;
        Vector3 pos = prevPos + (Vector3)(moveDirection * rollSpeed * speedFactor * dt);

        // 画面端バウンス
        if (pos.x < screenXMin) { pos.x = screenXMin; moveDirection.x = Mathf.Abs(moveDirection.x); OnBounce(); }
        else if (pos.x > screenXMax) { pos.x = screenXMax; moveDirection.x = -Mathf.Abs(moveDirection.x); OnBounce(); }

        // Floor回避距離とスクリーン下端の大きい方をY下限として使用
        float yMin = (floorAvoidY > float.MinValue) ? Mathf.Max(screenYMin, floorAvoidY) : screenYMin;
        if (pos.y < yMin) { pos.y = yMin; moveDirection.y = Mathf.Abs(moveDirection.y); OnBounce(); }
        else if (pos.y > screenYMax) { pos.y = screenYMax; moveDirection.y = -Mathf.Abs(moveDirection.y); OnBounce(); }

        transform.position = pos;

        // Rotation: 移動した全方向の距離÷半径で回転角を求める（水平成分だけだと縦移動時に回転が止まってしまうため、
        // 距離は2D全体の移動量を使う。回転方向は横移動があればその符号、無ければ縦移動の符号を代用する）
        Vector2 frameDelta = new Vector2(pos.x - prevPos.x, pos.y - prevPos.y);
        float distance = frameDelta.magnitude;
        float dirSign = (Mathf.Abs(frameDelta.x) > 0.0001f) ? Mathf.Sign(frameDelta.x) : Mathf.Sign(frameDelta.y);
        currentZAngle += -dirSign * (distance / Mathf.Max(0.01f, bodyRadius)) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, currentZAngle);
    }

    private void OnBounce()
    {
        if (bounceProtectionTimer > 0f) return;
        moveDirection = EightDirections[Random.Range(0, EightDirections.Length)];
        bounceProtectionTimer = bounceProtectionSeconds;
    }

    // =========================================================
    // Top Bombardment Attack（転がり移動は止めず並行して発生する）
    // =========================================================

    private void TickBombardment(float dt)
    {
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) return;

        bombardTimer += dt;
        if (bombardTimer < bombardInterval) return;

        bombardTimer -= bombardInterval;
        StartCoroutine(FireBombardmentRoutine());
    }

    private IEnumerator FireBombardmentRoutine()
    {
        if (Camera.main == null) yield break;

        EnemyShooter shooter = GetComponent<EnemyShooter>();
        if (shooter == null) yield break;

        EnemyBullet bulletPrefab = shooter.GetBulletPrefab();
        Transform projectileRoot = shooter.GetProjectileRoot();
        EnemyData data = shooter.GetEnemyData();
        if (bulletPrefab == null || projectileRoot == null || data == null) yield break;
        if (data.bulletTypes == null || bombardBulletTypeIndex < 0 || bombardBulletTypeIndex >= data.bulletTypes.Length) yield break;

        EnemyData.BulletType bulletType = data.bulletTypes[bombardBulletTypeIndex];
        if (bulletType == null) yield break;

        Vector3 spawnPos = GetRandomTopSpawnPosition();
        Vector3 targetPos = GetRandomFloorTargetPosition();

        // 発射地点にエネルギー収束エフェクトを再生し、収束が終わるまで待ってから実際に弾を出す。
        // 同時にGyrorb自身も明滅させ、このエネミーが攻撃元であることを予兆として伝える。
        if (convergeBurstVfxPrefab != null)
        {
            ParticleSystem vfx = Instantiate(convergeBurstVfxPrefab, spawnPos, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, convergeBurstDuration + 0.5f);
        }

        if (chargeAuraVfxPrefab != null)
        {
            ParticleSystem chargeVfx = Instantiate(chargeAuraVfxPrefab, transform.position, Quaternion.identity, transform);
            chargeVfx.Play();
            Destroy(chargeVfx.gameObject, convergeBurstDuration + 0.5f);
        }

        if (convergeBurstDuration > 0f)
            yield return StartCoroutine(TelegraphFlashRoutine(convergeBurstDuration));

        // 収束エフェクト待機中にゲームオーバーになった場合、弾は出さない
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) yield break;

        Vector2 dir = ((Vector2)(targetPos - spawnPos));
        dir = (dir.sqrMagnitude > 0.0001f) ? dir.normalized : Vector2.down;

        EnemyBullet bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);

        float fallbackSpeed = (bulletType.speed > 0f) ? bulletType.speed : 6f;
        float fallbackLifeTime = (bulletType.lifeTime > 0f) ? bulletType.lifeTime : 5f;
        EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bulletType, fallbackSpeed, fallbackLifeTime, null, bulletPrefab, projectileRoot);

        // 自機（Gyrorb自身）のコライダーと即座に衝突しないようにする
        Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in ownColliders)
        {
            if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
        }

        if (data.unreflectedBulletCollisionDisableTime > 0f)
            bullet.SetUnreflectedCollisionDisable(data.unreflectedBulletCollisionDisableTime);
    }

    // Gyrorb自身の色を予兆色との間で明滅させる。durationはconvergeBurstDurationと同じ待機時間として使う
    // （WaitForSeconds(duration * GetTimeScale())を置き換える形なので、同じスケーリングをここでも行う）
    private IEnumerator TelegraphFlashRoutine(float duration)
    {
        float scaledDuration = duration * GetTimeScale();

        if (bodySpriteRenderer == null)
        {
            yield return new WaitForSeconds(scaledDuration);
            yield break;
        }

        Color original = bodySpriteRenderer.color;
        float elapsed = 0f;
        while (elapsed < scaledDuration)
        {
            elapsed += Time.deltaTime;
            float t = (Mathf.Sin(elapsed * bombardTelegraphPulseSpeed * Mathf.PI) + 1f) * 0.5f;
            bodySpriteRenderer.color = Color.Lerp(original, bombardTelegraphColor, t);
            yield return null;
        }
        bodySpriteRenderer.color = original;
    }

    private Vector3 GetRandomTopSpawnPosition()
    {
        float halfH = Camera.main.orthographicSize;
        float halfW = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float skillHudWorldOffset = 0f;
        if (skillHudPixelWidth > 0f && Screen.width > 0)
        {
            float worldUnitsPerPixel = (halfW * 2f) / Screen.width;
            skillHudWorldOffset = skillHudPixelWidth * worldUnitsPerPixel;
        }

        float xMin = camPos.x - halfW + skillHudWorldOffset;
        float xMax = camPos.x + halfW;
        float x = Random.Range(xMin, xMax);
        float y = camPos.y + halfH + bombardSpawnYOffset;

        return new Vector3(x, y, 0f);
    }

    private Vector3 GetRandomFloorTargetPosition()
    {
        if (floorObject != null)
        {
            Collider2D col = floorObject.GetComponent<Collider2D>();
            if (col != null)
            {
                float x = Random.Range(col.bounds.min.x, col.bounds.max.x);
                return new Vector3(x, col.bounds.max.y, 0f);
            }
            return floorObject.transform.position;
        }

        // Floor未検出時のフォールバック: 画面下端付近のランダムX
        float halfH = Camera.main.orthographicSize;
        float halfW = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;
        float fx = Random.Range(camPos.x - halfW, camPos.x + halfW);
        return new Vector3(fx, camPos.y - halfH, 0f);
    }

    // =========================================================
    // Idle発光ループ（本体スプライトを3枚差し替えて目の発光強度を変える）
    // =========================================================

    private void TickIdleFrames(float dt)
    {
        if (idleFrames == null || idleFrames.Length == 0) return;

        idleFrameTimer += dt;
        GyrorbFrame current = idleFrames[idleFrameIndex % idleFrames.Length];
        float dur = (current != null && current.duration > 0f) ? current.duration : 0.3f;

        if (idleFrameTimer >= dur)
        {
            idleFrameTimer -= dur;
            idleFrameIndex++;
            ApplyIdleFrame(idleFrameIndex);
        }
    }

    private void ApplyIdleFrame(int index)
    {
        GyrorbFrame f = GetIdleFrame(((index % SafeIdleLength()) + SafeIdleLength()) % SafeIdleLength());
        if (f == null) return;

        if (f.sprite != null && bodySpriteRenderer != null) bodySpriteRenderer.sprite = f.sprite;
        ApplyOffset(f.offset);
    }

    private int SafeIdleLength() => (idleFrames != null && idleFrames.Length > 0) ? idleFrames.Length : 1;

    private void ApplyOffset(Vector2 offset)
    {
        if (bodySpriteRenderer == null) return;
        Vector3 local = bodySpriteRenderer.transform.localPosition;
        bodySpriteRenderer.transform.localPosition = new Vector3(offset.x, offset.y, local.z);
    }

    private GyrorbFrame GetIdleFrame(int index)
    {
        if (idleFrames == null || index < 0 || index >= idleFrames.Length) return null;
        return idleFrames[index];
    }

    private GyrorbFrame GetPreviewFrame(GyrorbPreviewSprite ps)
    {
        switch (ps)
        {
            case GyrorbPreviewSprite.Idle1: return GetIdleFrame(0);
            case GyrorbPreviewSprite.Idle2: return GetIdleFrame(1);
            case GyrorbPreviewSprite.Idle3: return GetIdleFrame(2);
            default: return null;
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (bodySpriteRenderer == null)
            bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodySpriteRenderer == null) return;

        if (previewSprite == GyrorbPreviewSprite.IdleAnimate)
        {
            StartEditorAnim();
        }
        else
        {
            StopEditorAnim();
            var f = GetPreviewFrame(previewSprite);
            if (f != null)
            {
                if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
                ApplyOffset(f.offset);
            }
        }
        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private bool _editorAnimRunning;
    private double _editorAnimLastTime;
    private int _editorAnimFrameIdx;

    // ネストした配列要素（Frame構造体のフィールド）を編集した際にOnValidateが
    // 確実に発火しないケースがあるため、その時はこのContextMenuで手動反映する
    [ContextMenu("Force Refresh Preview")]
    private void ForceRefreshPreview()
    {
        if (bodySpriteRenderer == null)
            bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodySpriteRenderer == null) return;

        var f = GetPreviewFrame(previewSprite);
        if (f != null)
        {
            if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
            ApplyOffset(f.offset);
        }
        UnityEditor.SceneView.RepaintAll();
    }

    private void StartEditorAnim()
    {
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        var f0 = GetIdleFrame(0);
        if (f0 != null && f0.sprite != null && bodySpriteRenderer != null)
        {
            bodySpriteRenderer.sprite = f0.sprite;
            ApplyOffset(f0.offset);
        }
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null) { StopEditorAnim(); return; }
        if (idleFrames == null || idleFrames.Length == 0) return;

        double now = UnityEditor.EditorApplication.timeSinceStartup;
        GyrorbFrame current = idleFrames[_editorAnimFrameIdx % idleFrames.Length];
        float dur = (current != null && current.duration > 0f) ? current.duration : 0.3f;

        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx++;
            GyrorbFrame nf = idleFrames[_editorAnimFrameIdx % idleFrames.Length];
            if (nf != null && nf.sprite != null && bodySpriteRenderer != null)
            {
                bodySpriteRenderer.sprite = nf.sprite;
                ApplyOffset(nf.offset);
            }
            UnityEditor.SceneView.RepaintAll();
        }
    }
#endif
}
