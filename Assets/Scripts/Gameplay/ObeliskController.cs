using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Area08ボス「Obelisk」専用コントローラー。
/// MarshalController.cs（Dragon用）をフォークして作成（MarshalController自体には一切変更を加えていない）。
/// フレーム配列＋Muzzle位置調整＋Editor Preview機構（ObeliskFrame/ApplyMuzzlePreview等）は流用しつつ、
/// Dragon固有だった移動ロジック（Zephyr方式操舵・ホバリング/バースト）・攻撃ロジック（腕アタック・
/// 肩Missile・Breath/Wing/Bite/Roar・Attack Pattern）は全て撤去済み。本体は移動しない前提。
/// 光る部位・Bit・フェーズ制御・Shield付与などObelisk固有の実装はこれから追加する。
/// </summary>
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyDamageReceiver))]
public class ObeliskController : MonoBehaviour
{
    public enum ObeliskPreviewSprite
    {
        Idle1, Idle2, Idle3, Idle4, Idle5, Idle6, Idle7, IdleAnimate,
        Hit1, HitAnimate
    }

    [System.Serializable]
    public class ObeliskFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数。durationMaxを0より大きくすると、こちらの値は無視されてdurationMin〜durationMaxのランダムな秒数になる")]
        public float   duration;
        [Tooltip("表示秒数のランダム幅（最小）。durationMaxが0より大きい時だけ有効")]
        public float   durationMin;
        [Tooltip("表示秒数のランダム幅（最大）。0より大きい値を入れるとランダム表示秒数が有効になる")]
        public float   durationMax;
        [Tooltip("マズル位置（発射系スプライトのローカル座標。発射フレームのみ有効）")]
        public Vector2 muzzleOffset;
        [Tooltip("当たり判定オフセット（Polygon Collider2Dの形状はPrefab側固定、位置だけここで微調整する）")]
        public Vector2 colliderOffset;
        [Tooltip("スプライトのZ軸回転（度）")]
        public float   rotationZ;
    }

    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [SerializeField] private PolygonCollider2D bodyCollider;
    [Tooltip("発射位置プレビュー用（任意）。Bodyの子に空Transformを置いてアサインすると、Play前でも選択中のプレビューフレームのmuzzleOffsetに追従して動き、Gizmoで確認できます")]
    [SerializeField] private Transform muzzlePreview;
    [SerializeField] private EnemyData enemyData;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    // =========================================================
    // Editor Preview
    // =========================================================

    [Header("Editor Preview")]
    [SerializeField] private ObeliskPreviewSprite previewSprite = ObeliskPreviewSprite.Idle1;

    [Header("Sprites - Idle（明滅パルスループ。配列の並び順=再生順で登録すること）")]
    [NonReorderable]
    [SerializeField] private ObeliskFrame[] idleFrames;

    [Header("Sprites - Hit（被弾）")]
    [NonReorderable]
    [SerializeField] private ObeliskFrame[] hitFrames;

    // =========================================================
    // Fade In
    // =========================================================

    [Header("Fade In")]
    [SerializeField] private float stage12FadeInDuration = 1f;
    [SerializeField] private float stage3FadeInDuration = 3f;

    // =========================================================
    // Idle Sway（不規則な浮遊揺れ。Perlin NoiseでX/Y別々に、頻度も含めて不規則に変化する。
    // Idleフレームごとの固定offsetだけだと同じ周期で同じ軌道を繰り返してしまうため、
    // それとは別にこの揺れを毎フレーム加算する）
    // =========================================================

    [Header("Idle Sway（不規則な浮遊揺れ）")]
    [Tooltip("左右方向の最大振れ幅")]
    [SerializeField] private float swayAmplitudeX = 0.04f;
    [Tooltip("上下方向の最大振れ幅")]
    [SerializeField] private float swayAmplitudeY = 0.05f;
    [Tooltip("左右方向の揺れの基準速度（Perlin Noiseのサンプリング速度。値自体もノイズで不規則に変動する）")]
    [SerializeField] private float swayFrequencyX = 0.15f;
    [Tooltip("上下方向の揺れの基準速度（Xとは別のノイズ・別の周波数にすることで、常に同じ軌道にならないようにする）")]
    [SerializeField] private float swayFrequencyY = 0.11f;
    [Tooltip("揺れの速度自体をどれだけ不規則に変動させるか（0=一定速度、大きいほど速くなったり遅くなったりする）")]
    [SerializeField] private float swayFrequencyJitter = 0.6f;

    // =========================================================
    // Bullet Spawn 共通設定（今後のBit・本体ビーム攻撃で使用）
    // =========================================================

    [Header("Bullet Spawn")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private EnemyBeamBullet beamBulletPrefab;
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private float fallbackBulletSpeed = 6f;
    [SerializeField] private float fallbackBulletLifeTime = 5f;
    [SerializeField] private float ignoreOwnerTime = 0.15f;

    // =========================================================
    // Bit（護衛ユニット）。Phase1：SPペアA/Bそれぞれからランダムで1機ずつ出現
    // =========================================================

    [Header("Bit（護衛ユニット）")]
    [SerializeField] private BitController bitPrefab;
    [Tooltip("EnemySpawnerのSpawn Points配列インデックス（0始まり。SP04なら3、SP07なら6）")]
    [SerializeField] private int bitPairASlotIndex1 = 3; // SP04
    [SerializeField] private int bitPairASlotIndex2 = 6; // SP07
    [Tooltip("EnemySpawnerのSpawn Points配列インデックス（0始まり。SP06なら5、SP09なら8）")]
    [SerializeField] private int bitPairBSlotIndex1 = 5; // SP06
    [SerializeField] private int bitPairBSlotIndex2 = 8; // SP09

    // =========================================================
    // Weak Point（Phase1：背面の弱点。ランダムなスロットに出現し、
    // 破壊すると本体へボーナスダメージ。一定時間後に別スロットへ再配置される）
    // =========================================================

    [Header("Weak Point（背面弱点）")]
    [Tooltip("弱点の見た目（発光エフェクトのルートTransform）。この配下にある全Particle Systemがまとめて再生/停止される。位置もこのTransformを動かす")]
    [SerializeField] private Transform weakPointGlowRoot;
    [Tooltip("弱点のHP。WallHealthのHit/Break VFX・SEもここで設定する（Hit系=Just弾ヒット相当の見た目・音を割り当てる想定）")]
    [SerializeField] private WallHealth weakPointWallHealth;
    [Tooltip("背面の弱点候補スロット（本体のローカル座標）。破壊後はこの中から再抽選される")]
    [SerializeField] private Vector2[] weakPointSlots;
    [Tooltip("弱点破壊時に本体（EnemyStats）へ与えるボーナスダメージ")]
    [SerializeField] private int weakPointBonusDamage = 10;
    [Tooltip("弱点破壊後、次のスロットに再出現するまでの秒数")]
    [SerializeField] private float weakPointRespawnDelay = 5f;

    [Header("Weak Point Telegraph（出現前の点滅予告）")]
    [SerializeField] private int weakPointTelegraphBlinkCount = 4;
    [SerializeField] private float weakPointTelegraphBlinkInterval = 0.15f;

    // =========================================================
    // インスタンス変数
    // =========================================================

    private EnemyStats stats;
    private EnemyDamageReceiver damageReceiver;
    private EnemyMover enemyMover;
    private PixelDancerController player;
    private bool isDead;

    // B4（敵速度低下）スキルの互換用。本体は移動しないが、Idleパルスの速度に反映させる用途で残している
    private float SlowMultiplier => (enemyMover != null) ? enemyMover.SpeedMultiplier : 1f;

    private int idleFrameIndex;
    private float idleFrameTimer;
    private float idleFrameCurrentDuration;
    private Vector2 currentFrameOffset;

    // Idle Sway用の状態（位相を可変速度で進めることで、振れ幅だけでなく頻度も不規則にする）
    private float swaySeedX;
    private float swaySeedY;
    private float swayJitterSeedX;
    private float swayJitterSeedY;
    private float swayPhaseX;
    private float swayPhaseY;

    private Coroutine fadeInCoroutine;
    private Coroutine hitCoroutine;

    private EnemySpawner enemySpawner;
    private readonly List<BitController> activeBits = new List<BitController>();

    private Coroutine weakPointCoroutine;
    private bool weakPointBroken;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        damageReceiver = GetComponent<EnemyDamageReceiver>();
        enemyMover = GetComponentInParent<EnemyMover>();
        if (enemyMover != null) enemyMover.suppressMovement = true;

        if (bodySpriteRenderer == null) bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (stats != null)
        {
            stats.onKilled += HandleKilled;
        }

        EnemyPart bodyPart = bodySpriteRenderer != null ? bodySpriteRenderer.GetComponent<EnemyPart>() : null;
        if (bodyPart != null) bodyPart.OnHitWithDamage += HandleHitWithDamage;

        if (weakPointWallHealth != null) weakPointWallHealth.OnBroken += HandleWeakPointBroken;

        if (projectileRoot == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) projectileRoot = pr.transform;
        }
    }

    private void Start()
    {
        EnemyShooter es = GetComponent<EnemyShooter>();
        if (es != null)
        {
            if (enemyData == null) enemyData = es.GetEnemyData();
            if (bulletPrefab == null) bulletPrefab = es.GetBulletPrefab();
            if (beamBulletPrefab == null) beamBulletPrefab = es.GetBeamBulletPrefab();
            if (projectileRoot == null) projectileRoot = es.GetProjectileRoot();
            es.enabled = false;
        }

        player = FindObjectOfType<PixelDancerController>();
        enemySpawner = FindObjectOfType<EnemySpawner>();

        SpawnPhase1Bits();
    }

    private void OnEnable()
    {
        isDead = false;

        swaySeedX = Random.Range(0f, 1000f);
        swaySeedY = Random.Range(0f, 1000f);
        swayJitterSeedX = Random.Range(0f, 1000f);
        swayJitterSeedY = Random.Range(0f, 1000f);
        swayPhaseX = Random.Range(0f, 1000f);
        swayPhaseY = Random.Range(0f, 1000f);

        idleFrameIndex = 0;
        idleFrameTimer = 0f;
        ApplyIdleFrame(0);

        if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);
        fadeInCoroutine = StartCoroutine(FadeInBody());

        StartWeakPointCycle();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        UnityEditor.EditorApplication.update += OnEditorTickRefresh;
#endif
    }

    private void OnDisable()
    {
        if (weakPointCoroutine != null)
        {
            StopCoroutine(weakPointCoroutine);
            weakPointCoroutine = null;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        UnityEditor.EditorApplication.update -= OnEditorTickRefresh;
        StopEditorAnim();
#endif
    }

    private void OnDestroy()
    {
        isDead = true;
        if (stats != null) stats.onKilled -= HandleKilled;
        if (weakPointWallHealth != null) weakPointWallHealth.OnBroken -= HandleWeakPointBroken;
    }

    private void HandleKilled()
    {
        isDead = true;
    }

    private void HandleHitWithDamage(EnemyBullet bullet, int damage)
    {
        if (isDead) return;
        if (hitFrames == null || hitFrames.Length == 0) return;

        if (hitCoroutine != null) StopCoroutine(hitCoroutine);
        hitCoroutine = StartCoroutine(PlayHitOnce());
    }

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Update()
    {
        if (isDead) return;

        float dt = Time.deltaTime * GetTimeScale() * SlowMultiplier;
        TickIdleFrames(dt);
        ApplyIdleSway(dt);
    }

    // 振れ幅（Perlin Noiseの出力）だけでなく、位相の進む速さ自体も別のノイズで変動させることで、
    // 頻度も不規則にする。X/Yは別々のシード・別々の周波数を使い、常に同じ軌道（円運動等）を
    // 描かないようにする
    private void ApplyIdleSway(float dt)
    {
        if (bodySpriteRenderer == null) return;

        float jitterX = Mathf.PerlinNoise(swayJitterSeedX, Time.time * 0.05f) * 2f - 1f;
        float jitterY = Mathf.PerlinNoise(swayJitterSeedY, Time.time * 0.05f) * 2f - 1f;
        float rateX = swayFrequencyX * Mathf.Max(0.1f, 1f + jitterX * swayFrequencyJitter);
        float rateY = swayFrequencyY * Mathf.Max(0.1f, 1f + jitterY * swayFrequencyJitter);

        swayPhaseX += dt * rateX;
        swayPhaseY += dt * rateY;

        float swayX = (Mathf.PerlinNoise(swaySeedX, swayPhaseX) * 2f - 1f) * swayAmplitudeX;
        float swayY = (Mathf.PerlinNoise(swaySeedY, swayPhaseY) * 2f - 1f) * swayAmplitudeY;

        ApplyOffset(bodySpriteRenderer, currentFrameOffset + new Vector2(swayX, swayY));
    }

    // =========================================================
    // フェードイン（Zephyr/Gyrorb/Dragonと同じ方式）
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
    // Body アニメーション（Idle明滅パルスのみ。本体は移動しないためMoveは無し）
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
        ObeliskFrame f = idleFrames[((index % len) + len) % len];
        idleFrameCurrentDuration = FrameDurationOr(f, 0.2f);
        ApplyBodyFrame(f);
    }

    private void ApplyBodyFrame(ObeliskFrame f)
    {
        if (f == null || bodySpriteRenderer == null) return;
        if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
        // 位置はここでは確定させない。currentFrameOffsetに保存し、ApplyIdleSway側でSway分と
        // 合算して毎フレーム適用する（Play中のみ。Editor Previewは従来通りf.offsetをそのまま使う）
        currentFrameOffset = f.offset;
        if (!Application.isPlaying) ApplyOffset(bodySpriteRenderer, f.offset);
        ApplyCollider(f);
        ApplyMuzzlePreview(f);
    }

    private IEnumerator PlayHitOnce()
    {
        if (bodySpriteRenderer == null) yield break;
        Sprite original = bodySpriteRenderer.sprite;

        foreach (var f in hitFrames)
        {
            if (isDead) yield break;
            if (f == null) continue;
            if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
            ApplyOffset(bodySpriteRenderer, f.offset);
            yield return WaitScaled(FrameDurationOr(f, 0.1f));
        }

        if (!isDead && bodySpriteRenderer != null) bodySpriteRenderer.sprite = original;
    }

    private IEnumerator WaitScaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (isDead) yield break;
            t += Time.deltaTime * GetTimeScale();
            yield return null;
        }
    }

    private static void ApplyOffset(SpriteRenderer sr, Vector2 offset)
    {
        if (sr == null) return;
        Transform t = sr.transform;
        float x = sr.flipX ? -offset.x : offset.x;
        Vector3 local = t.localPosition;
        t.localPosition = new Vector3(x, offset.y, local.z);
    }

    private void ApplyCollider(ObeliskFrame frame)
    {
        if (bodyCollider == null || frame == null) return;
        Vector2 offset = frame.colliderOffset;
        if (bodySpriteRenderer != null && bodySpriteRenderer.flipX) offset.x = -offset.x;
        bodyCollider.offset = offset;
    }

    // muzzlePreviewをプレビュー/現在フレームのmuzzleOffsetへ追従させる
    private void ApplyMuzzlePreview(ObeliskFrame frame)
    {
        if (muzzlePreview == null || frame == null) return;
        float x = (bodySpriteRenderer != null && bodySpriteRenderer.flipX) ? -frame.muzzleOffset.x : frame.muzzleOffset.x;
        muzzlePreview.localPosition = new Vector3(x, frame.muzzleOffset.y, muzzlePreview.localPosition.z);
    }

    private static float FrameDurationOr(ObeliskFrame f, float fallback)
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

    private static Vector2 RotateDir(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float s = Mathf.Sin(rad);
        float c = Mathf.Cos(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // =========================================================
    // 弾スポーン共通処理（今後のBit・本体ビーム攻撃で使用）
    // =========================================================

    private bool IsFireBlockedGlobally()
    {
        return FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal || PixelDancerController.IsDownGlobal;
    }

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (enemyData == null || enemyData.bulletTypes == null) return null;
        if (index < 0 || index >= enemyData.bulletTypes.Length) return null;
        return enemyData.bulletTypes[index];
    }

    // 弾種のFire Interval Override設定を反映する
    private float GetFireInterval(EnemyData.BulletType bt, float fallbackInterval)
    {
        if (bt == null) return fallbackInterval;

        float interval = fallbackInterval;
        if (bt.useFireIntervalOverride && bt.fireIntervalOverride > 0f)
            interval = bt.fireIntervalOverride;

        if (bt.useFireIntervalRandom)
        {
            float range = Mathf.Max(0f, bt.fireIntervalRandomRangeSeconds);
            interval += (range > 0f) ? Random.Range(-range, range) : 0f;
            interval = Mathf.Max(bt.fireIntervalMinSeconds, interval);
        }

        return Mathf.Max(0.05f, interval);
    }

    // プレイヤー・Floor可動域を含めた照準方向
    private Vector2 ComputeAimDirection(Vector3 spawnPos, EnemyData.BulletType type)
    {
        Vector2 fallback = Vector2.down;
        if (player == null) return fallback;

        if (type == null || type.aimMode == EnemyData.BulletType.AimMode.UseFireDirection
            || type.aimMode == EnemyData.BulletType.AimMode.TowardPlayer)
        {
            Vector2 d = (Vector2)player.transform.position - (Vector2)spawnPos;
            return d.sqrMagnitude > 0.0001f ? d.normalized : fallback;
        }

        float range = player.AutoMoveRange;
        float targetX = player.transform.position.x + Random.Range(-range, range);
        Vector2 target = new Vector2(targetX, player.transform.position.y);
        Vector2 dir = target - (Vector2)spawnPos;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : fallback;
    }

    private void SpawnBullet(Vector3 spawnPos, Vector2 dir, EnemyData.BulletType bt)
    {
        if (bulletPrefab == null || projectileRoot == null) return;

        if (showDebugLog) Debug.Log($"[ObeliskController] SpawnBullet: pos={spawnPos}, dir={dir}, bt={(bt != null ? bt.name : "NULL")}");

        EnemyBullet bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);

        if (bt != null)
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, fallbackBulletSpeed, fallbackBulletLifeTime, null, bulletPrefab, projectileRoot);
        else
            bullet.ApplyBullet(fallbackBulletSpeed, fallbackBulletLifeTime);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            if (col != null) bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);
        }

        if (enemyData != null && enemyData.unreflectedBulletCollisionDisableTime > 0f)
            bullet.SetUnreflectedCollisionDisable(enemyData.unreflectedBulletCollisionDisableTime);
    }

    private void PlayFireSE(AudioClip clip, float volume, Vector3 pos)
    {
        if (clip == null) return;
        float vol = volume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        AudioSource.PlayClipAtPoint(clip, pos, vol);
    }

    // =========================================================
    // Bit（護衛ユニット）Phase1スポーン
    //  - SPペアA（SP04/SP07）とSPペアB（SP06/SP09）それぞれからランダムで1機ずつ出現させる
    //  - 破壊後はBitController.ConfigureRespawnSlotsで渡した同じペア内から再抽選される
    // =========================================================

    private void SpawnPhase1Bits()
    {
        if (bitPrefab == null)
        {
            if (showDebugLog) Debug.Log("[ObeliskController] SpawnPhase1Bits: bitPrefab未設定のためスキップ", this);
            return;
        }
        if (enemySpawner == null)
        {
            if (showDebugLog) Debug.LogWarning("[ObeliskController] SpawnPhase1Bits: EnemySpawnerが見つからないためスキップ", this);
            return;
        }

        SpawnBitAtPair(bitPairASlotIndex1, bitPairASlotIndex2);
        SpawnBitAtPair(bitPairBSlotIndex1, bitPairBSlotIndex2);
    }

    private void SpawnBitAtPair(int slotIndex1, int slotIndex2)
    {
        Vector2[] candidates = BuildSlotCandidates(slotIndex1, slotIndex2);
        if (candidates.Length == 0)
        {
            if (showDebugLog) Debug.LogWarning($"[ObeliskController] SpawnBitAtPair: 有効なSpawn Pointが無い（index {slotIndex1}/{slotIndex2}）", this);
            return;
        }

        Vector2 spawnPos = candidates[Random.Range(0, candidates.Length)];
        BitController bit = Instantiate(bitPrefab, (Vector3)spawnPos, Quaternion.identity);
        bit.ConfigureRespawnSlots(candidates);
        bit.ConfigureBodyTransform(bodySpriteRenderer != null ? bodySpriteRenderer.transform : transform);
        activeBits.Add(bit);
    }

    private Vector2[] BuildSlotCandidates(int slotIndex1, int slotIndex2)
    {
        Transform t1 = enemySpawner.GetSpawnPoint(slotIndex1);
        Transform t2 = enemySpawner.GetSpawnPoint(slotIndex2);

        if (t1 != null && t2 != null) return new Vector2[] { t1.position, t2.position };
        if (t1 != null) return new Vector2[] { t1.position };
        if (t2 != null) return new Vector2[] { t2.position };
        return new Vector2[0];
    }

    // =========================================================
    // Weak Point（背面弱点）Phase1サイクル
    //  - ランダムなスロットへ配置→点滅予告→出現（当たり判定ON）→破壊されるまで待機
    //  - 破壊されたら本体へボーナスダメージ、一定時間後に別スロットへ再配置してループ
    //  - Hit/Break時のVFX・SEはWeak PointのWallHealth側の設定（Hit系/Break系）で再生される
    // =========================================================

    private void StartWeakPointCycle()
    {
        if (weakPointGlowRoot == null || weakPointWallHealth == null)
        {
            if (showDebugLog) Debug.Log("[ObeliskController] StartWeakPointCycle: 参照未設定のためスキップ", this);
            return;
        }
        if (weakPointSlots == null || weakPointSlots.Length == 0)
        {
            if (showDebugLog) Debug.LogWarning("[ObeliskController] StartWeakPointCycle: weakPointSlotsが空のためスキップ", this);
            return;
        }

        if (weakPointCoroutine != null) StopCoroutine(weakPointCoroutine);
        weakPointCoroutine = StartCoroutine(WeakPointCycleRoutine());
    }

    // パーティクルはSpriteRendererのenabledのような単純なON/OFFが無いため、
    // Stop(StopEmittingAndClear)で即座に消し、Play()で即座に出す。
    // weakPointGlowRoot配下にある全Particle System（複数を想定）へまとめて適用する
    private void SetWeakPointGlowVisible(bool visible)
    {
        if (weakPointGlowRoot == null) return;
        foreach (ParticleSystem ps in weakPointGlowRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (visible) ps.Play(true);
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private IEnumerator WeakPointCycleRoutine()
    {
        while (!isDead)
        {
            // 新しいスロットへ配置
            // 当たり判定の実体（Collider2D/WallHealth）が乗っているのはWeakPoint自身のTransform。
            // weakPointGlowRootはその子（ローカル原点に配置済み）なので、親を動かせば追従する
            Vector2 slot = weakPointSlots[Random.Range(0, weakPointSlots.Length)];
            Transform weakPointTransform = weakPointWallHealth.transform;
            weakPointTransform.localPosition = new Vector3(slot.x, slot.y, weakPointTransform.localPosition.z);

            // 点滅予告（この間は非表示⇔表示を繰り返すだけで、当たり判定はまだ無効のまま）
            SetWeakPointGlowVisible(false);
            for (int i = 0; i < weakPointTelegraphBlinkCount; i++)
            {
                SetWeakPointGlowVisible(true);
                yield return new WaitForSeconds(weakPointTelegraphBlinkInterval);
                SetWeakPointGlowVisible(false);
                yield return new WaitForSeconds(weakPointTelegraphBlinkInterval);
            }

            // 出現（HP・当たり判定・見た目を全て復帰）
            weakPointBroken = false;
            weakPointWallHealth.ResetHealth();
            SetWeakPointGlowVisible(true);

            // 破壊されるまで待つ
            yield return new WaitUntil(() => weakPointBroken || isDead);
            if (isDead) yield break;

            // 破壊された：見た目を消して次のスロットまで待機
            SetWeakPointGlowVisible(false);
            yield return new WaitForSeconds(weakPointRespawnDelay);
        }
    }

    private void HandleWeakPointBroken(Vector3 hitPos)
    {
        weakPointBroken = true;
        SetWeakPointGlowVisible(false);
        if (stats != null) stats.Damage(weakPointBonusDamage);
        if (showDebugLog) Debug.Log($"[ObeliskController] WeakPoint破壊。本体へ{weakPointBonusDamage}ダメージ", this);
    }

#if UNITY_EDITOR
    // ★追加：GlowVfx.prefabをWeak Point配下に複製配置し、Play On Awake OFF・色設定・
    // Weak Point Glow Rootへのアサインまで自動で行う（Editor専用、PrefabUtility.InstantiatePrefabで
    // 元プレハブとのリンクを保ったまま生成する）
    [ContextMenu("Setup Weak Point Glow (GlowVfxから自動生成)")]
    private void SetupWeakPointGlow()
    {
        if (weakPointWallHealth == null)
        {
            Debug.LogWarning("[ObeliskController] SetupWeakPointGlow: Weak Point Wall Healthが未設定です。先にWeakPoint GameObject（WallHealth付き）を用意してアサインしてください。", this);
            return;
        }

        // 既に生成済みなら一旦削除してから作り直す（再実行しても安全にするため）
        Transform existing = weakPointWallHealth.transform.Find("WeakPointGlow");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        const string prefabPath = "Assets/Prefabs/Effects/GlowVfx.prefab";
        GameObject glowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (glowPrefab == null)
        {
            Debug.LogError($"[ObeliskController] SetupWeakPointGlow: {prefabPath} が見つかりません", this);
            return;
        }

        GameObject instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(glowPrefab, weakPointWallHealth.transform);
        instance.name = "WeakPointGlow";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        // 弱点用の色（オレンジ〜赤）に統一し、Play On AwakeをOFF（コード側でPlay/Stopを制御するため）
        Color weakPointColor = new Color(1f, 0.4f, 0.15f, 1f);
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.startColor = weakPointColor;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        weakPointGlowRoot = instance.transform;

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[ObeliskController] SetupWeakPointGlow: WeakPointGlowを生成し、Weak Point Glow Rootにアサインしました", this);
    }
#endif

    // =========================================================
    // Editor Preview
    // =========================================================

    private static ObeliskFrame GetArrayFrame(ObeliskFrame[] arr, int index)
    {
        if (arr == null || index < 0 || index >= arr.Length) return null;
        return arr[index];
    }

    private ObeliskFrame GetPreviewFrame(ObeliskPreviewSprite ps)
    {
        switch (ps)
        {
            case ObeliskPreviewSprite.Idle1: return GetArrayFrame(idleFrames, 0);
            case ObeliskPreviewSprite.Idle2: return GetArrayFrame(idleFrames, 1);
            case ObeliskPreviewSprite.Idle3: return GetArrayFrame(idleFrames, 2);
            case ObeliskPreviewSprite.Idle4: return GetArrayFrame(idleFrames, 3);
            case ObeliskPreviewSprite.Idle5: return GetArrayFrame(idleFrames, 4);
            case ObeliskPreviewSprite.Idle6: return GetArrayFrame(idleFrames, 5);
            case ObeliskPreviewSprite.Idle7: return GetArrayFrame(idleFrames, 6);
            case ObeliskPreviewSprite.Hit1: return GetArrayFrame(hitFrames, 0);
            default: return null;
        }
    }

#if UNITY_EDITOR
    private void OnEditorTickRefresh()
    {
        if (this == null || Application.isPlaying) return;
        if (_editorAnimRunning) return;
        if (bodySpriteRenderer == null) return;

        var f = GetPreviewFrame(previewSprite);
        if (f == null) return;

        if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
        ApplyOffset(bodySpriteRenderer, f.offset);
        ApplyCollider(f);
        ApplyMuzzlePreview(f);
        if (bodyCollider != null) UnityEditor.EditorUtility.SetDirty(bodyCollider);
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (bodySpriteRenderer == null)
            bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodySpriteRenderer == null) return;

        bool isAnim = previewSprite == ObeliskPreviewSprite.IdleAnimate
                   || previewSprite == ObeliskPreviewSprite.HitAnimate;

        if (isAnim)
        {
            int animType = previewSprite == ObeliskPreviewSprite.IdleAnimate ? 0 : 1;
            if (_editorAnimRunning && _editorAnimType != animType) StopEditorAnim();
            StartEditorAnim(animType);
        }
        else
        {
            StopEditorAnim();
            var f = GetPreviewFrame(previewSprite);
            if (f != null)
            {
                if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
                ApplyOffset(bodySpriteRenderer, f.offset);
                ApplyCollider(f);
                ApplyMuzzlePreview(f);
            }
        }

        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private bool _editorAnimRunning;
    private double _editorAnimLastTime;
    private int _editorAnimFrameIdx;
    private int _editorAnimType; // 0=Idle,1=Hit

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
        if (frames != null && frames.Length > 0 && frames[0] != null && bodySpriteRenderer != null)
        {
            bodySpriteRenderer.sprite = frames[0].sprite;
            ApplyOffset(bodySpriteRenderer, frames[0].offset);
            ApplyCollider(frames[0]);
            ApplyMuzzlePreview(frames[0]);
        }
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private ObeliskFrame[] GetEditorAnimFrames()
    {
        switch (_editorAnimType)
        {
            case 0: return idleFrames;
            default: return hitFrames;
        }
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning) { StopEditorAnim(); return; }

        var frames = GetEditorAnimFrames();
        if (frames == null || frames.Length == 0) { StopEditorAnim(); return; }

        var current = frames[_editorAnimFrameIdx % frames.Length];
        double dur = FrameDurationOr(current, 0.15f);
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % frames.Length;
            var next = frames[_editorAnimFrameIdx];
            if (next != null && next.sprite != null && bodySpriteRenderer != null)
            {
                bodySpriteRenderer.sprite = next.sprite;
                ApplyOffset(bodySpriteRenderer, next.offset);
                ApplyCollider(next);
                ApplyMuzzlePreview(next);
            }
        }
    }

#endif

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
        {
            StopEditorAnim();
        }
    }
#endif

    // =========================================================
    // Muzzle Gizmo（Play前のScene viewで発射位置を確認するため）
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (muzzlePreview != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawSphere(muzzlePreview.position, 0.06f);
        }

        if (weakPointSlots != null && bodySpriteRenderer != null)
        {
            // 実際の当たり判定サイズ（CircleCollider2Dの半径×スケール）をGizmoに反映する。
            // 取得できない場合のみフォールバック値を使う
            float radius = 0.08f;
            if (weakPointWallHealth != null)
            {
                CircleCollider2D circle = weakPointWallHealth.GetComponent<CircleCollider2D>();
                if (circle != null)
                {
                    Vector3 lossyScale = circle.transform.lossyScale;
                    float scaleFactor = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
                    radius = circle.radius * scaleFactor;
                }
            }

            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.85f);
            foreach (Vector2 slot in weakPointSlots)
            {
                Vector3 world = bodySpriteRenderer.transform.TransformPoint(slot);
                Gizmos.DrawWireSphere(world, radius);
            }
        }
    }
}
