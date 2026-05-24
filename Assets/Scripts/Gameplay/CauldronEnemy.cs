using UnityEngine;
using System.Collections;

/// <summary>
/// S2_06 Cauldron（大釜中ボス）の行動制御スクリプト。
/// 状態: Normal（通常発射・ゲージ蓄積）→ OverflowIntro → OverflowBurst（7種全弾連続発射）→ OverflowOutro → Normal
/// 弾はすべてCauldronEnemyが直接管理（EnemyShooterは無効化）。
/// 弾はリム位置から出現し、bulletHoverDuration秒かけて速度が上がる（ホバー→発射）。
/// </summary>
[RequireComponent(typeof(EnemyMover))]
public class CauldronEnemy : MonoBehaviour
{
    // =========================================================
    // Body Sprites — Liquid Level
    // =========================================================
    [Header("Body Sprites - Liquid Level")]
    [Tooltip("液体レベルスプライト（Lv0, Lv1, Lv2, Lv3, Lv4, Lv4b）6枚。ゲージ0→100%に対応。")]
    [SerializeField] private Sprite[] liquidSprites;

    // =========================================================
    // Body Sprites — Overflow
    // =========================================================
    [Header("Body Sprites - Overflow")]
    [Tooltip("溢れアニメスプライト（A, B, C, D, E）5枚。A=Intro, B/C=Burst交互, D/E=Outro。")]
    [SerializeField] private Sprite[] overflowSprites;

    // =========================================================
    // Flame
    // =========================================================
    [Header("Flame")]
    [Tooltip("炎SpriteRenderer（子オブジェクト）。Normalでも常にアニメ再生。")]
    [SerializeField] private SpriteRenderer flameRenderer;
    [Tooltip("炎スプライト配列（Flame01-04）4枚。")]
    [SerializeField] private Sprite[] flameSprites;
    [Tooltip("炎アニメ速度（fps）")]
    [SerializeField] private float flameAnimFps = 8f;
    [Tooltip("炎のローカル位置オフセット（大釜中心からのずれ）。Inspector変更時に即時反映。")]
    [SerializeField] private Vector2 flameLocalPosition = Vector2.zero;
    [Tooltip("炎のスケール。Inspector変更時に即時反映。")]
    [SerializeField] private Vector2 flameLocalScale = Vector2.one;
    [Tooltip("プレビュー用フレーム番号（0-3）。Inspector変更時に即時反映。")]
    [SerializeField] private int flamePreviewFrame = 0;
    [Tooltip("炎のSorting Order。鍋より後ろに出すには鍋のSortingOrderより小さい値にする。")]
    [SerializeField] private int flameSortingOrder = -1;

    // =========================================================
    // Floating Bob
    // =========================================================
    [Header("Floating Bob")]
    [Tooltip("上下浮遊の振幅（Unity単位）")]
    [SerializeField] private float bobAmplitude = 0.08f;
    [Tooltip("上下浮遊の周期（Hz）")]
    [SerializeField] private float bobFrequency = 0.8f;

    // =========================================================
    // Gauge
    // =========================================================
    [Header("Gauge")]
    [Tooltip("EnemyShooterが1発発射するたびに加算されるゲージ量（100で満タン→溢れ）")]
    [SerializeField] private float gaugePerShot = 15f;
    [Tooltip("Lv4↔Lv4b警告アニメ開始ゲージ値（%）")]
    [SerializeField] private float warningThreshold = 80f;
    [Tooltip("Lv4/Lv4b切り替え間隔（秒）")]
    [SerializeField] private float warningBlinkRate = 0.3f;

    // =========================================================
    // Bullet Launch
    // =========================================================
    [Header("Bullet Launch")]
    [Tooltip("弾出現位置のYオフセット（大釜中心からのUnits）")]
    [SerializeField] private float cauldronRimOffset = 0.6f;
    [Tooltip("弾が上に浮かぶ時間（秒）。この時間が終わるとプレイヤー方向へ発射する。")]
    [SerializeField] private float bulletHoverDuration = 1.0f;
    [Tooltip("ホバー中の上昇速度（Unity単位/秒）")]
    [SerializeField] private float hoverUpwardSpeed = 0.5f;

    // =========================================================
    // Overflow
    // =========================================================
    [Header("Overflow")]
    [Tooltip("Intro（Overflow_A）の再生時間（秒）")]
    [SerializeField] private float overflowIntroDuration = 0.8f;
    [Tooltip("Burst中の各弾種発射間隔（秒）")]
    [SerializeField] private float overflowBurstFireInterval = 0.6f;
    [Tooltip("Burst中のB↔CループFPS")]
    [SerializeField] private float overflowBCFps = 4f;
    [Tooltip("Outro（D→E）の合計再生時間（秒）")]
    [SerializeField] private float overflowOutroDuration = 2f;

    [Tooltip("Burst発射時の弾スポーン位置を大釜中心から±この値のX範囲でランダムにずらす（Unity単位）")]
    [SerializeField] private float overflowRimRandomX = 0.5f;

    [Tooltip("Overflowで発射するBulletTypesのインデックス（EnemyDataのBullet Types配列の番号）。空の場合は全弾種を発射。")]
    [SerializeField] private int[] overflowBulletIndices = new int[0];

    [Tooltip("Burst発射の繰り返し倍数。1=各弾種1回ずつ、2=各弾種2回ずつ（全体をまとめてランダム順）")]
    [SerializeField] private int overflowBurstRepeatMultiplier = 1;

    // ── State Machine ──
    private enum CauldronState { Normal, OverflowIntro, OverflowBurst, OverflowOutro }
    private CauldronState cauldronState;

    // ── Components ──
    private EnemyMover        enemyMover;
    private EnemyShooter      enemyShooter;
    private SpriteRenderer    bodyRenderer;
    private EnemySpriteSwapper spriteSwapper;
    private Animator          enemyAnimator;
    private PixelDancerController player;

    // ── Shooter resources (set in Start after EnemySpawner init) ──
    private EnemyBullet bulletPrefab;
    private Transform   projectileRoot;
    private EnemyData   enemyData;

    // ── Gauge ──
    private float gauge;
    private float warningBlinkTimer;
    private bool  warningBlinkLv4b;

    // ── Floating ──
    private float   bobTime;
    private Vector3 basePos;

    // ── Flame ──
    private float flameTimer;

    // Normal Fireタイマーは不要（EnemyShooterが管理）

    // ── Overflow ──
    private float     overflowTimer;
    private float     overflowBCTimer;
    private bool      overflowBCIsB;
    private Coroutine burstCoroutine;


    // =========================================================
    void OnValidate()
    {
        // Inspector変更時にPlay前のScene Viewへ即時反映
        if (flameRenderer == null) return;

        // 位置
        flameRenderer.transform.localPosition = new Vector3(
            flameLocalPosition.x, flameLocalPosition.y,
            flameRenderer.transform.localPosition.z);

        // スケール
        flameRenderer.transform.localScale = new Vector3(
            flameLocalScale.x, flameLocalScale.y, 1f);

        // Sorting Order（鍋より後ろに配置）
        flameRenderer.sortingOrder = flameSortingOrder;

        // フレーム
        if (flameSprites != null && flameSprites.Length > 0)
        {
            int f = Mathf.Clamp(flamePreviewFrame, 0, flameSprites.Length - 1);
            if (flameSprites[f] != null)
                flameRenderer.sprite = flameSprites[f];
        }
    }

    // =========================================================
    void Awake()
    {
        enemyMover    = GetComponent<EnemyMover>();
        enemyShooter  = GetComponent<EnemyShooter>();
        bodyRenderer  = GetComponent<SpriteRenderer>();
        spriteSwapper = GetComponent<EnemySpriteSwapper>();
        enemyAnimator = GetComponent<Animator>();
        if (enemyAnimator == null)
            enemyAnimator = GetComponentInChildren<Animator>();

        player = FindObjectOfType<PixelDancerController>();

        if (enemyMover   != null) enemyMover.suppressMovement = true;
        if (enemyAnimator != null) enemyAnimator.enabled = false;
        // EnemyShooterは通常発射を担当するため有効のまま。
        // Overflow時のみ無効化する（EnterOverflowIntro/ReturnToNormal）。
    }

    void OnDestroy()
    {
        if (enemyShooter != null)
        {
            enemyShooter.OnFired        -= OnShotFired;
            enemyShooter.OnBulletSpawned -= OnNormalBulletSpawned;
        }
        if (burstCoroutine != null) StopCoroutine(burstCoroutine);
    }

    void Start()
    {
        // EnemySpawnerがStart前にSetEnemyDataを呼ぶためStartで取得
        if (enemyShooter != null)
        {
            bulletPrefab   = enemyShooter.GetBulletPrefab();
            projectileRoot = enemyShooter.GetProjectileRoot();
            enemyData      = enemyShooter.GetEnemyData();
            enemyShooter.OnFired        += OnShotFired;
            enemyShooter.OnBulletSpawned += OnNormalBulletSpawned;
        }
        if (projectileRoot == null) projectileRoot = transform;

        // Sorting Orderをランタイムでも適用（OnValidateはEdit時のみ）
        if (flameRenderer != null) flameRenderer.sortingOrder = flameSortingOrder;

        basePos       = transform.position;
        gauge         = 0f;
        cauldronState = CauldronState.Normal;

        SetBodySprite(liquidSprites, 0);
    }

    void Update()
    {
        // EnemySpriteSwapper被弾復帰時にAnimatorが再有効化されるため毎フレーム無効化
        if (enemyAnimator != null && enemyAnimator.enabled)
            enemyAnimator.enabled = false;

        float dt = DeltaTime();

        // 上下浮遊ボブ
        bobTime += dt;
        float bobY = Mathf.Sin(bobTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        transform.position = new Vector3(basePos.x, basePos.y + bobY, basePos.z);

        // 炎アニメ（常時）
        UpdateFlameAnim(dt);

        switch (cauldronState)
        {
            case CauldronState.Normal:        UpdateNormal(dt);        break;
            case CauldronState.OverflowIntro: UpdateOverflowIntro(dt); break;
            case CauldronState.OverflowBurst: UpdateOverflowBurst(dt); break;
            case CauldronState.OverflowOutro: UpdateOverflowOutro(dt); break;
        }
    }

    // =========================================================
    // FLAME
    // =========================================================
    void UpdateFlameAnim(float dt)
    {
        if (flameSprites == null || flameSprites.Length == 0 || flameRenderer == null) return;
        flameTimer += dt;
        int f = Mathf.FloorToInt(flameTimer * flameAnimFps) % flameSprites.Length;
        flameRenderer.sprite = flameSprites[f];
    }

    // =========================================================
    // NORMAL
    // =========================================================
    void UpdateNormal(float dt)
    {
        // ゲージはOnShotFired()でイベント駆動加算（時間経過では加算しない）

        // 警告アニメ（Lv4↔Lv4b）
        if (gauge >= warningThreshold)
        {
            warningBlinkTimer += dt;
            if (warningBlinkTimer >= warningBlinkRate)
            {
                warningBlinkTimer = 0f;
                warningBlinkLv4b  = !warningBlinkLv4b;
                UpdateBodySprite();
            }
        }
        else
        {
            warningBlinkLv4b = false;
            UpdateBodySprite();
        }

        // 通常発射はEnemyShooterが自動管理（Probabilityモード・HP別ルーティング適用）

        // 満タン→溢れへ
        if (gauge >= 100f) EnterOverflowIntro();
    }

    // =========================================================
    // OVERFLOW INTRO — Overflow_A
    // =========================================================
    void EnterOverflowIntro()
    {
        cauldronState = CauldronState.OverflowIntro;
        overflowTimer = 0f;
        if (enemyShooter != null) enemyShooter.enabled = false; // Overflow中は自前発射に切り替え
        SetBodySprite(overflowSprites, 0); // A
    }

    void UpdateOverflowIntro(float dt)
    {
        overflowTimer += dt;
        if (overflowTimer >= overflowIntroDuration) EnterOverflowBurst();
    }

    // =========================================================
    // OVERFLOW BURST — B↔C loop + 7種連続発射
    // =========================================================
    void EnterOverflowBurst()
    {
        cauldronState   = CauldronState.OverflowBurst;
        overflowBCTimer = 0f;
        overflowBCIsB   = true;
        SetBodySprite(overflowSprites, 1); // B

        if (burstCoroutine != null) StopCoroutine(burstCoroutine);
        burstCoroutine = StartCoroutine(BurstCoroutine());
    }

    void UpdateOverflowBurst(float dt)
    {
        // B↔C 交互アニメ
        float bcInterval = 1f / Mathf.Max(0.1f, overflowBCFps);
        overflowBCTimer += dt;
        if (overflowBCTimer >= bcInterval)
        {
            overflowBCTimer = 0f;
            overflowBCIsB   = !overflowBCIsB;
            SetBodySprite(overflowSprites, overflowBCIsB ? 1 : 2);
        }
    }

    IEnumerator BurstCoroutine()
    {
        if (enemyData == null || enemyData.bulletTypes == null) yield break;

        int[] baseIndices = (overflowBulletIndices != null && overflowBulletIndices.Length > 0)
            ? overflowBulletIndices
            : BuildFullIndices(enemyData.bulletTypes.Length);

        // baseIndices を overflowBurstRepeatMultiplier 倍に拡張してシャッフル
        int repeat = Mathf.Max(1, overflowBurstRepeatMultiplier);
        int total  = baseIndices.Length * repeat;
        int[] shuffled = new int[total];
        for (int r = 0; r < repeat; r++)
            for (int j = 0; j < baseIndices.Length; j++)
                shuffled[r * baseIndices.Length + j] = baseIndices[j];

        // Fisher-Yates シャッフル
        for (int i = total - 1; i > 0; i--)
        {
            int j   = Random.Range(0, i + 1);
            int tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
        }

        foreach (int i in shuffled)
        {
            if (cauldronState != CauldronState.OverflowBurst) yield break;
            if (i < 0 || i >= enemyData.bulletTypes.Length) continue;

            SpawnHoverBullets(i);

            // インターバル待機（スローモーション対応）
            float waited = 0f;
            while (waited < overflowBurstFireInterval)
            {
                if (cauldronState != CauldronState.OverflowBurst) yield break;
                waited += DeltaTime();
                yield return null;
            }
        }

        EnterOverflowOutro();
    }

    // =========================================================
    // OVERFLOW OUTRO — D→E
    // =========================================================
    void EnterOverflowOutro()
    {
        cauldronState = CauldronState.OverflowOutro;
        overflowTimer = 0f;
        SetBodySprite(overflowSprites, 3); // D
    }

    void UpdateOverflowOutro(float dt)
    {
        overflowTimer += dt;

        float halfDur = overflowOutroDuration * 0.5f;
        if (overflowTimer < halfDur)
            SetBodySprite(overflowSprites, 3); // D
        else if (overflowTimer < overflowOutroDuration)
            SetBodySprite(overflowSprites, 4); // E
        else
            ReturnToNormal();
    }

    void ReturnToNormal()
    {
        gauge            = 0f;
        warningBlinkLv4b = false;
        cauldronState    = CauldronState.Normal;
        if (enemyShooter != null) enemyShooter.enabled = true; // 通常発射を再開
        UpdateBodySprite();
    }

    // =========================================================
    // BULLET SPAWN
    // =========================================================
    void SpawnHoverBullets(int typeIndex)
    {
        if (bulletPrefab == null || projectileRoot == null) return;
        if (enemyData == null || enemyData.bulletTypes == null) return;
        if (typeIndex < 0 || typeIndex >= enemyData.bulletTypes.Length) return;

        var type = enemyData.bulletTypes[typeIndex];
        if (type == null) return;

        float   randomX = Random.Range(-overflowRimRandomX, overflowRimRandomX);
        Vector3 rimPos  = transform.position + new Vector3(randomX, cauldronRimOffset, 0f);
        Vector2 baseDir = ComputePlayerDir(rimPos);

        int   shots = (type.useMultiShot) ? Mathf.Max(1, type.shotsPerFire) : 1;
        float half  = (type.useMultiShot) ? type.spreadAngleDeg * 0.5f : 0f;

        for (int i = 0; i < shots; i++)
        {
            float ang = (shots > 1) ? Mathf.Lerp(-half, half, (float)i / (shots - 1)) : 0f;
            SpawnOneBullet(rimPos, type, ang);
        }
    }

    void SpawnOneBullet(Vector3 spawnPos, EnemyData.BulletType type, float spreadAngle = 0f)
    {
        EnemyBullet bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectileRoot);

        bullet.SetVisualSortingOrder(bodyRenderer.sortingOrder + 1);

        const float kFallbackSpeed    = 6f;
        const float kFallbackLifetime = 5f;
        float finalSpeed = (type.speed > 0f) ? type.speed : kFallbackSpeed;

        // 弾種プロパティ適用（ビジュアル・Wave・MultiWarhead等）
        EnemyShooter.ApplyBulletTypeToEnemyBullet(
            bullet, type, kFallbackSpeed, kFallbackLifetime, null, bulletPrefab, projectileRoot);

        // SpeedCurve・MissileArcを無効化して上方向ホバーに上書き
        bullet.ClearSpeedCurve();
        bullet.ClearMissileArc();
        bullet.ApplyBullet(hoverUpwardSpeed, kFallbackLifetime);
        bullet.SetDirection(Vector2.up);

        // 自身のコライダーとの衝突を一時的に無視
        foreach (var col in GetComponentsInChildren<Collider2D>())
            if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);

        if (enemyData.unreflectedBulletCollisionDisableTime > 0f)
            bullet.SetUnreflectedCollisionDisable(enemyData.unreflectedBulletCollisionDisableTime);

        // ホバー中は線に触れたら消滅（反射しない）
        bullet.SetDestroyOnLineHit(true);

        StartCoroutine(HoverAndLaunchCoroutine(bullet, type, spreadAngle, kFallbackLifetime));
    }

    IEnumerator HoverAndLaunchCoroutine(EnemyBullet bullet, EnemyData.BulletType type, float spreadAngle, float lifeTime)
    {
        // ホバー時間はリアル時間で計測（スローモーションやB4デバフの影響を受けない）
        float waited = 0f;
        while (waited < bulletHoverDuration)
        {
            if (bullet == null) yield break;
            waited += Time.deltaTime;
            yield return null;
        }
        if (bullet == null) yield break;

        // ホバー終了→通常弾として反射可能にする
        bullet.SetDestroyOnLineHit(false);

        // 方向を先にセット（MissileArcがStartCoroutine時に方向をキャプチャするため、先に正しい方向を与える）
        Vector2 playerDir = ComputePlayerDir(bullet.transform.position);
        Vector2 launchDir = RotateVec(playerDir, spreadAngle);
        bullet.SetDirection(launchDir);

        // 弾種プロパティを再適用（SpeedCurve・MissileArc等の挙動を復元）
        float fallbackSpeed = (type != null && type.speed > 0f) ? type.speed : 6f;
        EnemyShooter.ApplyBulletTypeToEnemyBullet(
            bullet, type, fallbackSpeed, lifeTime, null, bulletPrefab, projectileRoot);

        // SpeedCurveの初速がhoverUpwardSpeedより小さい場合、ホバー終了直後に減速して
        // まだ浮いているように見える問題を防ぐ。curveStartTimeを過去にずらして
        // SpeedCurveがhoverUpwardSpeed相当からスタートしているように見せる。
        if (bullet != null)
            bullet.BumpSpeedCurveStartToMinSpeed(hoverUpwardSpeed);
    }

    // =========================================================
    // SPRITE HELPERS
    // =========================================================
    void UpdateBodySprite()
    {
        int idx;
        if      (gauge < 20f) idx = 0; // Lv0
        else if (gauge < 40f) idx = 1; // Lv1
        else if (gauge < 60f) idx = 2; // Lv2
        else if (gauge < 80f) idx = 3; // Lv3
        else idx = warningBlinkLv4b ? 5 : 4; // Lv4 or Lv4b

        SetBodySprite(liquidSprites, idx);
    }

    void SetBodySprite(Sprite[] sprites, int index)
    {
        if (sprites == null || index < 0 || index >= sprites.Length) return;
        if (bodyRenderer == null) return;
        if (spriteSwapper != null)
            spriteSwapper.SetBaseSprite(sprites[index]);
        else
            bodyRenderer.sprite = sprites[index];
    }

    // =========================================================
    // HELPERS
    // =========================================================
    Vector2 ComputePlayerDir(Vector3 fromPos)
    {
        if (player != null)
            return ((Vector2)(player.transform.position - fromPos)).normalized;
        return Vector2.down;
    }

    static Vector2 RotateVec(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
    }

    // EnemyShooter.OnFired から呼ばれる（Normal発射ごとにゲージ加算）
    void OnShotFired()
    {
        if (cauldronState != CauldronState.Normal) return;
        gauge = Mathf.Clamp(gauge + gaugePerShot, 0f, 100f);
        UpdateBodySprite();
    }

    // EnemyShooter.OnBulletSpawned から呼ばれる（通常発射弾にホバー適用）
    void OnNormalBulletSpawned(EnemyBullet bullet, EnemyData.BulletType type)
    {
        if (cauldronState != CauldronState.Normal) return;

        // Overflowと同じくリム位置をX方向にランダムにずらす
        float randomX = Random.Range(-overflowRimRandomX, overflowRimRandomX);
        bullet.transform.position = new Vector3(
            transform.position.x + randomX,
            transform.position.y + cauldronRimOffset,
            bullet.transform.position.z);

        // ホバー中だけ上昇移動に上書き（弾種プロパティはホバー終了後に再適用）
        bullet.ClearSpeedCurve();
        bullet.ClearMissileArc();
        bullet.ApplyBullet(hoverUpwardSpeed, 5f);
        bullet.SetDirection(Vector2.up);

        // ホバー中は線に触れたら消滅（反射しない）
        bullet.SetDestroyOnLineHit(true);

        StartCoroutine(HoverAndLaunchCoroutine(bullet, type, 0f, 5f));
    }

    static int[] BuildFullIndices(int length)
    {
        int[] arr = new int[length];
        for (int i = 0; i < length; i++) arr[i] = i;
        return arr;
    }

    /// <summary>スローモーション・B4デバフを反映したdeltaTimeを返す。</summary>
    float DeltaTime() =>
        Time.deltaTime
        * (SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f)
        * (enemyMover != null ? enemyMover.SpeedMultiplier : 1f);
}
