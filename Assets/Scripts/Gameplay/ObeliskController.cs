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
        Hit1, HitAnimate,
        IdlePhase2_1, IdlePhase2_2, IdlePhase2_3, IdlePhase2_4, IdlePhase2_5, IdlePhase2_6, IdlePhase2_7, IdlePhase2Animate
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

    [Header("Sprites - Idle Phase2（赤線版。青線Idleと同じ枚数・同じ並び順で登録すること）")]
    [NonReorderable]
    [SerializeField] private ObeliskFrame[] idleFramesPhase2;

    [Header("Phase2 Transition SE")]
    [Tooltip("Idle3で静止した瞬間に鳴らすパワーダウンSE")]
    [SerializeField] private AudioClip phase2PowerDownSE;
    [SerializeField, Range(0f, 1f)] private float phase2PowerDownSEVolume = 1f;
    [Tooltip("赤線アニメーションへ切り替わる瞬間に鳴らす警告SE")]
    [SerializeField] private AudioClip phase2WarningSE;
    [SerializeField, Range(0f, 1f)] private float phase2WarningSEVolume = 1f;

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
    [Tooltip("Obelisk本体が出現してから、Phase1初期Bitが出現するまでの遅延秒数（いきなり出ると違和感があるための間）")]
    [SerializeField] private float bitInitialSpawnDelay = 2f;
    [Tooltip("EnemySpawnerのSpawn Points配列インデックス（0始まり。SP04なら3、SP07なら6）")]
    [SerializeField] private int bitPairASlotIndex1 = 3; // SP04
    [SerializeField] private int bitPairASlotIndex2 = 6; // SP07
    [Tooltip("EnemySpawnerのSpawn Points配列インデックス（0始まり。SP06なら5、SP09なら8）")]
    [SerializeField] private int bitPairBSlotIndex1 = 5; // SP06
    [SerializeField] private int bitPairBSlotIndex2 = 8; // SP09
    [Tooltip("Phase2で追加スポーンするペアC用。EnemySpawnerのSpawn Points配列インデックス（0始まり）。-1のままだと未設定としてスキップされる")]
    [SerializeField] private int bitPairCSlotIndex1 = -1;
    [SerializeField] private int bitPairCSlotIndex2 = -1;
    [Tooltip("Phase2で追加スポーンするペアD用。EnemySpawnerのSpawn Points配列インデックス（0始まり）。-1のままだと未設定としてスキップされる")]
    [SerializeField] private int bitPairDSlotIndex1 = -1;
    [SerializeField] private int bitPairDSlotIndex2 = -1;
    [Tooltip("Bit出現時のSE")]
    [SerializeField] private AudioClip bitSpawnSE;
    [Range(0f, 1f)] [SerializeField] private float bitSpawnSEVolume = 1f;

    [Tooltip("Phase2でBit出現位置に定期的に湧くMarshal/Zephyr出現時のSE")]
    [SerializeField] private AudioClip marshalZephyrSpawnSE;
    [Range(0f, 1f)] [SerializeField] private float marshalZephyrSpawnSEVolume = 1f;

    [Header("Phase2 Marshal/Zephyr 定期出現")]
    [Tooltip("MarshalのEnemyData")]
    [SerializeField] private EnemyData marshalEnemyData;
    [Tooltip("ZephyrのEnemyData")]
    [SerializeField] private EnemyData zephyrEnemyData;
    [Tooltip("出現間隔（秒）")]
    [SerializeField] private float marshalZephyrSpawnInterval = 20f;
    [Tooltip("出現位置その1。EnemySpawnerのSpawn Points配列インデックス（0始まり。SP0なら0）")]
    [SerializeField] private int marshalZephyrSpawnIndex1 = 0;
    [Tooltip("出現位置その2。EnemySpawnerのSpawn Points配列インデックス（0始まり。SP2なら2）")]
    [SerializeField] private int marshalZephyrSpawnIndex2 = 2;

    // =========================================================
    // Phase2（弱点破壊 or 累計Bit撃破数到達で移行。弱点廃止・Bit4体・
    // Beam反射弾のみ本体ダメージ・中央ビーム・Marshal/Zephyr定期出現）
    // =========================================================

    [Header("Phase2 遷移条件")]
    [Tooltip("累計Bit撃破数がこの値に到達したらPhase2へ移行（弱点破壊1回でも移行する。どちらか早い方）")]
    [SerializeField] private int bitKillCountForPhase2 = 10;

    [Header("Phase2 切り替え演出")]
    [Tooltip("Idle3で静止（パワーダウンSE再生）してから、警告SE再生＋赤線アニメーションへ切り替わるまでの待機秒数。" +
        "パワーダウンSEの再生時間より短いと2つのSEが重なって聞こえるので、SEの長さに合わせて調整すること")]
    [SerializeField] private float phase2TransitionHoldDuration = 3f;

    // =========================================================
    // Weak Point（Phase1：背面の弱点。ランダムなスロットに出現し、
    // 破壊すると本体へボーナスダメージ。一定時間後に別スロットへ再配置される）
    // =========================================================

    // 弱点は本体の輪郭（辺）上に重なって配置し、アクティブな間はその区間だけ反射をオーバーライドしてダメージを受ける。
    // position=輪郭上の中心点（本体ローカル座標）、angle=その地点での輪郭接線方向（Z回転,度）、
    // length=輪郭に沿ったカバー幅（BoxCollider2DのXサイズ）
    [System.Serializable]
    public struct WeakPointSlot
    {
        public Vector2 position;
        public float angle;
        public float length;
    }

    [Header("Weak Point（背面弱点）")]
    [Tooltip("弱点の見た目（発光エフェクトのルートTransform）。この配下にある全Particle Systemがまとめて再生/停止される。位置もこのTransformを動かす")]
    [SerializeField] private Transform weakPointGlowRoot;
    [Tooltip("弱点のHP。WallHealthのHit/Break VFX・SEもここで設定する（Hit系=Just弾ヒット相当の見た目・音を割り当てる想定）")]
    [SerializeField] private WallHealth weakPointWallHealth;
    [Tooltip("背面の弱点候補スロット（輪郭上の位置・角度・区間長）。破壊後はこの中から再抽選される")]
    [SerializeField] private WeakPointSlot[] weakPointSlots;
    [Tooltip("弱点当たり判定の厚み（輪郭の法線方向。輪郭をまたぐように配置される）")]
    [SerializeField] private float weakPointThickness = 0.2f;
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

    // Phase2: phase2Triggeredは移行条件成立の瞬間にtrueになり、以後の再トリガーを防ぐ（弱点サイクル停止もこの時点で行う）。
    // phase2Activeは切り替え演出（Idle3静止→SE→赤線アニメ）が完了した時点でtrueになり、
    // アニメーション参照先の切り替え・Beam反射弾ダメージ・Bit拡張・Marshal/Zephyr出現等のゲームプレイ変化に使う
    private bool phase2Triggered;
    private bool phase2Active;
    private bool isPhaseTransitioning;
    private int totalBitsKilled;
    private Coroutine marshalZephyrCoroutine;
    private int marshalZephyrLastSlot = -1; // -1=まだ未出現（初回はランダム）。以後は前回と逆のスロットを交互に使う
    private GameObject marshalZephyrSlot1Occupant; // 各スロットの現在の生存個体（最大2体＝各スロット1体までに制限するため）
    private GameObject marshalZephyrSlot2Occupant;

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

        StartCoroutine(SpawnPhase1BitsDelayed());
    }

    private IEnumerator SpawnPhase1BitsDelayed()
    {
        yield return WaitScaled(bitInitialSpawnDelay);
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

        if (marshalZephyrCoroutine != null)
        {
            StopCoroutine(marshalZephyrCoroutine);
            marshalZephyrCoroutine = null;
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

        foreach (BitController bit in activeBits)
        {
            if (bit == null) continue;
            bit.OnRespawned -= HandleBitRespawnedForSE;
            WallHealth bitWallHealth = bit.GetComponent<WallHealth>();
            if (bitWallHealth != null) bitWallHealth.OnBroken -= HandleBitKilledForPhase2Tracking;
        }
    }

    private void HandleKilled()
    {
        isDead = true;

        // 本体撃破時、生存中のBitも道連れで消す（Bit自身のOnDestroy→DestroyActiveBeamで
        // 発射中のビームも既存の仕組みでまとめて後始末される）
        foreach (BitController bit in activeBits)
        {
            if (bit == null) continue;
            Destroy(bit.gameObject);
        }

        // Phase2で定期出現させたMarshal/Zephyrも道連れで消す
        if (marshalZephyrSlot1Occupant != null) Destroy(marshalZephyrSlot1Occupant);
        if (marshalZephyrSlot2Occupant != null) Destroy(marshalZephyrSlot2Occupant);
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
        if (!isPhaseTransitioning) TickIdleFrames(dt);
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
        ObeliskFrame[] frames = phase2Active ? idleFramesPhase2 : idleFrames;
        if (frames == null || frames.Length == 0) return;

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
        ObeliskFrame[] frames = phase2Active ? idleFramesPhase2 : idleFrames;
        if (frames == null || frames.Length == 0) return;
        int len = frames.Length;
        ObeliskFrame f = frames[((index % len) + len) % len];
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

    // =========================================================
    // Bit（護衛ユニット）Phase2追加スポーン
    //  - Phase1のペアA/Bはそのまま残し、SPペアC/Dを追加で出現させて最大2→4体にする
    // =========================================================

    private void SpawnPhase2Bits()
    {
        if (bitPrefab == null || enemySpawner == null) return;

        SpawnBitAtPair(bitPairCSlotIndex1, bitPairCSlotIndex2);
        SpawnBitAtPair(bitPairDSlotIndex1, bitPairDSlotIndex2);
    }

    // =========================================================
    // Phase2: Marshal/Zephyr 定期出現
    //  - Bitの出現位置（ペアA/B/C/D）からランダムに1箇所選び、Marshal/Zephyrをランダム抽選で追加スポーンする
    //  - 出現はEnemySpawner.SpawnEnemyAt経由（既存のフェードイン・Layer設定・aliveCount管理をそのまま使う）
    // =========================================================

    private void StartMarshalZephyrSpawnLoop()
    {
        if (marshalZephyrCoroutine != null) StopCoroutine(marshalZephyrCoroutine);
        marshalZephyrCoroutine = StartCoroutine(MarshalZephyrSpawnRoutine());
    }

    private IEnumerator MarshalZephyrSpawnRoutine()
    {
        while (!isDead)
        {
            yield return WaitScaled(marshalZephyrSpawnInterval);
            if (isDead) yield break;
            SpawnRandomMarshalOrZephyr();
        }
    }

    // 出現位置その1/その2の2箇所固定。初回はランダム、以後は前回と逆のスロットを交互に使う（0→1→0→1...）。
    // 選んだスロットにまだ生存中の個体がいる場合はスキップする（最大2体＝各スロット1体までを超えないようにするため）。
    // スキップ時はmarshalZephyrLastSlotを更新しないので、次のIntervalでも同じスロットが空くまで再挑戦し続ける
    private void SpawnRandomMarshalOrZephyr()
    {
        if (enemySpawner == null) return;

        int slot = (marshalZephyrLastSlot < 0) ? Random.Range(0, 2) : 1 - marshalZephyrLastSlot;

        GameObject existing = (slot == 0) ? marshalZephyrSlot1Occupant : marshalZephyrSlot2Occupant;
        if (existing != null)
        {
            if (showDebugLog) Debug.Log($"[ObeliskController] SpawnRandomMarshalOrZephyr: スロット{slot}はまだ占有中のためスキップ", this);
            return;
        }

        EnemyData data = (Random.value < 0.5f) ? marshalEnemyData : zephyrEnemyData;
        if (data == null)
        {
            if (showDebugLog) Debug.LogWarning("[ObeliskController] SpawnRandomMarshalOrZephyr: EnemyDataが未設定のためスキップ", this);
            return;
        }

        int spawnIndex = (slot == 0) ? marshalZephyrSpawnIndex1 : marshalZephyrSpawnIndex2;
        Transform spawnPoint = enemySpawner.GetSpawnPoint(spawnIndex);
        if (spawnPoint == null)
        {
            if (showDebugLog) Debug.LogWarning("[ObeliskController] SpawnRandomMarshalOrZephyr: 有効なSpawn Pointが無いためスキップ", this);
            return;
        }

        marshalZephyrLastSlot = slot;

        GameObject spawned = enemySpawner.SpawnEnemyAt(spawnPoint, data);
        if (slot == 0) marshalZephyrSlot1Occupant = spawned; else marshalZephyrSlot2Occupant = spawned;

        // Obelisk本体（Sorting Order 0、敵本体の慣習値）と同点だと表示順が不定になるため、
        // Obelisk配下から出す時だけ+1する（弾・VFX・HPバー等の9以上の帯は追い越さない範囲）。
        // 通常のウェーブ出現（EnemySpawnerの通常ルート）には影響しない、ここだけの調整
        if (spawned != null)
        {
            foreach (SpriteRenderer sr in spawned.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingOrder += 1;
            }
        }

        if (marshalZephyrSpawnSE != null) PlayFireSE(marshalZephyrSpawnSE, marshalZephyrSpawnSEVolume, spawnPoint.position);
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

        // Phase2で新規スポーンするBit（ペアC/D）は、最初からDouble/扇ビームを有効にしておく
        if (phase2Active) bit.SetPhase2BeamVariantsEnabled(true);

        if (bitSpawnSE != null) PlayFireSE(bitSpawnSE, bitSpawnSEVolume, spawnPos);

        // リスポーン時も同じSEを鳴らす（最初のスポーンはここで直接、以後はOnRespawnedイベント経由）
        bit.OnRespawned += HandleBitRespawnedForSE;

        // Phase2移行条件（累計撃破数）のカウント用。Bitは破壊されても同じインスタンスのまま
        // リスポーンするため、ここで一度だけ購読すれば以後の撃破も含めて累計できる
        WallHealth bitWallHealth = bit.GetComponent<WallHealth>();
        if (bitWallHealth != null) bitWallHealth.OnBroken += HandleBitKilledForPhase2Tracking;
    }

    private void HandleBitRespawnedForSE(BitController bit)
    {
        if (bitSpawnSE != null && bit != null) PlayFireSE(bitSpawnSE, bitSpawnSEVolume, bit.transform.position);
    }

    private void HandleBitKilledForPhase2Tracking(Vector3 hitPos)
    {
        if (phase2Triggered) return;
        totalBitsKilled++;
        if (showDebugLog) Debug.Log($"[ObeliskController] Bit撃破カウント: {totalBitsKilled}/{bitKillCountForPhase2}", this);
        if (totalBitsKilled >= bitKillCountForPhase2)
        {
            StartPhase2Transition();
        }
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
            // weakPointGlowRootはその子（ローカル原点に配置済み）なので、親を動かせば追従する。
            // 弱点は輪郭（辺）上に重ねて配置するため、位置だけでなく輪郭の接線方向への回転・
            // その区間長に合わせたBoxCollider2Dサイズも毎回セットし直す
            WeakPointSlot slot = weakPointSlots[Random.Range(0, weakPointSlots.Length)];
            Transform weakPointTransform = weakPointWallHealth.transform;
            weakPointTransform.localPosition = new Vector3(slot.position.x, slot.position.y, weakPointTransform.localPosition.z);
            weakPointTransform.localRotation = Quaternion.Euler(0f, 0f, slot.angle);

            BoxCollider2D weakPointBox = weakPointWallHealth.GetComponent<BoxCollider2D>();
            if (weakPointBox != null)
            {
                weakPointBox.size = new Vector2(slot.length, weakPointThickness);
            }

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

        // 弱点破壊はPhase2移行条件の1つ（Bit撃破数のカウントとは独立に、これ単独で即移行する）
        if (!phase2Triggered) StartPhase2Transition();
    }

    // =========================================================
    // Phase2 移行
    // =========================================================

    private void StartPhase2Transition()
    {
        if (phase2Triggered) return;
        phase2Triggered = true;

        // 弱点システムはPhase2で廃止するため、サイクルを止めて即非アクティブ化する
        if (weakPointCoroutine != null)
        {
            StopCoroutine(weakPointCoroutine);
            weakPointCoroutine = null;
        }
        SetWeakPointGlowVisible(false);
        if (weakPointWallHealth != null) weakPointWallHealth.gameObject.SetActive(false);

        StartCoroutine(Phase2TransitionRoutine());
    }

    private IEnumerator Phase2TransitionRoutine()
    {
        isPhaseTransitioning = true;

        // Idle3（青線・発光ゼロのフレーム＝idleFramesの配列インデックス0）で静止し、パワーダウンSEを鳴らす
        ApplyIdleFrame(0);
        if (phase2PowerDownSE != null) PlayFireSE(phase2PowerDownSE, phase2PowerDownSEVolume, transform.position);

        yield return WaitScaled(phase2TransitionHoldDuration);

        // 警告SEと同時に赤線アニメーションへ切り替える（SEと見た目の切り替えタイミングを揃える）
        if (phase2WarningSE != null) PlayFireSE(phase2WarningSE, phase2WarningSEVolume, transform.position);

        phase2Active = true;
        idleFrameIndex = 0;
        idleFrameTimer = 0f;
        ApplyIdleFrame(0);

        isPhaseTransitioning = false;

        // Phase2：本体はBeam反射弾のみダメージを受け付けるようになる（通常弾の反射弾は
        // enableDamage=false のままなので物理衝突では引き続き無効。allowExternalDamageWhenDisabledをONにすると、
        // Beam側のTryApplyExternalReflectedDamage経由（EnemyBeamBullet.TickReflectedSegmentが既存の仕組みで
        // 呼ぶ）だけダメージが通るようになる。BeamReflectorは反射のみで変更不要）
        EnemyPart bodyPart = bodySpriteRenderer != null ? bodySpriteRenderer.GetComponent<EnemyPart>() : null;
        if (bodyPart != null) bodyPart.allowExternalDamageWhenDisabled = true;

        // Phase2：既存のBit（ペアA/B）にDouble/扇ビームを有効化する（Phase2で新規スポーンするBitは
        // SpawnBitAtPair側でphase2Active==trueの場合に同様に有効化する）
        foreach (BitController existingBit in activeBits)
        {
            if (existingBit != null) existingBit.SetPhase2BeamVariantsEnabled(true);
        }

        // Bit最大数拡張（2→4）：既存のペアA/Bはそのまま残し、ペアC/Dを追加スポーンする
        SpawnPhase2Bits();

        // Marshal/Zephyrの定期出現を開始
        StartMarshalZephyrSpawnLoop();

        if (showDebugLog) Debug.Log("[ObeliskController] Phase2へ移行しました", this);

        // TODO: Bit最大数拡張・BeamReflectorダメージ有効化・Marshal/Zephyr定期出現・中央ビーム攻撃は別タスクで実装
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

    // ★追加：弱点は本体PolygonCollider2Dの輪郭（辺）上に重ねて配置し、アクティブな間はその区間だけ
    // 反射をオーバーライドしてダメージを受ける仕様。輪郭の頂点データから「背面」に相当する上側2辺
    // （右上辺・左上辺）を特定し、それぞれを弧長で2等分した各区間の中心へスロットを自動配置する
    [ContextMenu("Fix Weak Point Placement (輪郭の辺に沿って自動配置)")]
    private void FixWeakPointColliderSize()
    {
        if (weakPointWallHealth == null)
        {
            Debug.LogWarning("[ObeliskController] FixWeakPointColliderSize: Weak Point Wall Healthが未設定です。", this);
            return;
        }

        GameObject weakPointObj = weakPointWallHealth.gameObject;

        // 既存のCircleCollider2Dがあれば削除し、BoxCollider2Dに置き換える
        CircleCollider2D oldCircle = weakPointObj.GetComponent<CircleCollider2D>();
        if (oldCircle != null)
        {
            DestroyImmediate(oldCircle);
        }

        BoxCollider2D box = weakPointObj.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = weakPointObj.AddComponent<BoxCollider2D>();
        }

        // 輪郭の頂点データ（PolygonCollider2Dの実座標）から、右上辺（右端の最も出っ張った点→頂点）・
        // 左上辺（頂点→左端の最も出っ張った点）を弧長で2等分し、各半区間の中心点・接線角度・
        // 半区間長を計算した結果（本体ローカル座標）。輪郭データが変わらない限り再計算不要
        // 角度は各半区間の「両端を結ぶ弦（コード）」の方向で計算している（区間中心の接線角度だと、
        // 輪郭の曲がり方によっては区間の片端だけ輪郭から大きく浮いてしまうため、両端を均等に扱う弦基準にした）
        weakPointSlots = new WeakPointSlot[]
        {
            new WeakPointSlot { position = new Vector2(1.364f, 0.957f), angle = 128.96f, length = 1.484f },
            new WeakPointSlot { position = new Vector2(0.454f, 2.127f), angle = 126.79f, length = 1.481f },
            new WeakPointSlot { position = new Vector2(-0.477f, 2.131f), angle = -127.17f, length = 1.479f },
            new WeakPointSlot { position = new Vector2(-1.387f, 0.966f), angle = -128.82f, length = 1.478f },
        };

        // Box自体はPlay前のプレビュー用に最初のスロットの見た目を反映しておく
        // （実際の毎回のサイズ・回転はWeakPointCycleRoutineがPlay中にスロット抽選のたびにセットする）
        WeakPointSlot previewSlot = weakPointSlots[0];
        box.size = new Vector2(previewSlot.length, weakPointThickness);
        box.offset = Vector2.zero;
        UnityEditor.EditorUtility.SetDirty(box);

        weakPointWallHealth.transform.localPosition = new Vector3(previewSlot.position.x, previewSlot.position.y, weakPointWallHealth.transform.localPosition.z);
        weakPointWallHealth.transform.localRotation = Quaternion.Euler(0f, 0f, previewSlot.angle);
        UnityEditor.EditorUtility.SetDirty(weakPointWallHealth.transform);

        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log("[ObeliskController] FixWeakPointColliderSize: CircleCollider2D→BoxCollider2Dに変更し、Weak Point Slotsを輪郭の辺（右上辺・左上辺をそれぞれ2分割）に沿った位置・角度・長さへ更新しました。Scene viewの赤いギズモで輪郭に重なっているか確認してください。", this);
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
            case ObeliskPreviewSprite.IdlePhase2_1: return GetArrayFrame(idleFramesPhase2, 0);
            case ObeliskPreviewSprite.IdlePhase2_2: return GetArrayFrame(idleFramesPhase2, 1);
            case ObeliskPreviewSprite.IdlePhase2_3: return GetArrayFrame(idleFramesPhase2, 2);
            case ObeliskPreviewSprite.IdlePhase2_4: return GetArrayFrame(idleFramesPhase2, 3);
            case ObeliskPreviewSprite.IdlePhase2_5: return GetArrayFrame(idleFramesPhase2, 4);
            case ObeliskPreviewSprite.IdlePhase2_6: return GetArrayFrame(idleFramesPhase2, 5);
            case ObeliskPreviewSprite.IdlePhase2_7: return GetArrayFrame(idleFramesPhase2, 6);
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
                   || previewSprite == ObeliskPreviewSprite.HitAnimate
                   || previewSprite == ObeliskPreviewSprite.IdlePhase2Animate;

        if (isAnim)
        {
            int animType = previewSprite == ObeliskPreviewSprite.IdleAnimate ? 0
                          : previewSprite == ObeliskPreviewSprite.HitAnimate ? 1
                          : 2; // IdlePhase2Animate
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
    private int _editorAnimType; // 0=Idle,1=Hit,2=IdlePhase2

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
            case 2: return idleFramesPhase2;
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
            // 各スロットは輪郭上の位置・接線角度・区間長を持つため、それぞれ回転させたBoxとして描画する
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.85f);
            Matrix4x4 originalMatrix = Gizmos.matrix;
            foreach (WeakPointSlot slot in weakPointSlots)
            {
                Vector3 world = bodySpriteRenderer.transform.TransformPoint(slot.position);
                Quaternion rot = bodySpriteRenderer.transform.rotation * Quaternion.Euler(0f, 0f, slot.angle);
                Gizmos.matrix = Matrix4x4.TRS(world, rot, bodySpriteRenderer.transform.lossyScale);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(slot.length, weakPointThickness, 0.1f));
            }
            Gizmos.matrix = originalMatrix;
        }
    }
}
