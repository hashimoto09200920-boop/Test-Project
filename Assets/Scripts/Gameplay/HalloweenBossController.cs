using UnityEngine;
using System.Collections;

/// <summary>
/// Area4 ハロウィンボスのメインコントローラー。
///
/// Phase1 (HP≥50%):
///   - ボスは透明（ランタン未点灯）。反射弾でランタンを点灯すると実体化。
///   - 1つ以上点灯=実体（ダメージ有）、全消灯=透明。
///   - 3つ同時点灯=恒久実体化。
///
/// Phase2 (HP<50%):
///   - ボス本体フェードアウト → 分身6体出現。
///   - 記憶ゲーム: 属性3秒表示 → 属性非表示 → 反射弾で属性フラグメントを2つ当てる
///     - 一致: 全実体20秒（ダメージ有） → 次ラウンド
///     - 不一致: 全透明10秒（強攻撃モード） → 属性シャッフル → 次ラウンド
///   - ランタンは引き続き機能（点灯でフラグメント実体化のトリガー、MatchSolid/MismatchTransparent中は無効）
/// </summary>
public class HalloweenBossController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================
    [Header("Boss Body")]
    [Tooltip("Phase1のボス本体SpriteRenderer")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [Tooltip("Phase1のボス本体Collider2D（実体/透明切替に使用）")]
    [SerializeField] private Collider2D bodyCollider;

    [Header("Lanterns")]
    [Tooltip("シーンに配置したHalloweenLantern（3つ）への参照")]
    [SerializeField] private HalloweenLantern[] lanterns;

    [Header("Phase 2 Fragments")]
    [Tooltip("フラグメント6体を格納するルートTransform（Phase1中はSetActive(false)でも可）")]
    [SerializeField] private Transform fragmentsRoot;
    [Tooltip("HalloweenFragment 6体への参照（インデックス0-5）")]
    [SerializeField] private HalloweenFragment[] fragments;

    [Header("Phase Settings")]
    [Range(1f, 99f)]
    [Tooltip("Phase2切替HP%（この値以下でPhase2に入る）")]
    [SerializeField] private float phase2HpThreshold = 50f;
    [SerializeField] private float bodyFadeOutSeconds = 1f;

    [Header("Memory Game")]
    [Tooltip("属性表示時間（秒）")]
    [SerializeField] private float attributeDisplaySeconds = 3f;
    [Tooltip("一致後に全実体化している時間（秒）")]
    [SerializeField] private float matchSolidSeconds = 20f;
    [Tooltip("不一致後に全透明になる時間（秒）")]
    [SerializeField] private float mismatchTransparentSeconds = 10f;
    [Tooltip("3ランタン点灯中、属性表示が消えてから再表示するまでの待機時間（秒）")]
    [SerializeField] private float attributeRepeatIntervalSeconds = 5f;

    [Header("Float Animation")]
    [Tooltip("上下移動の振幅（Unity単位）")]
    [SerializeField] private float floatAmplitude = 0.15f;
    [Tooltip("上下移動のサイクル速度")]
    [SerializeField] private float floatSpeed = 2f;

    [Header("Phase 1 Drift")]
    [Tooltip("ドリフト可能エリアの左下座標")]
    [SerializeField] private Vector2 driftAreaMin = new Vector2(-2f, 0.5f);
    [Tooltip("ドリフト可能エリアの右上座標")]
    [SerializeField] private Vector2 driftAreaMax = new Vector2(4.5f, 3f);
    [Tooltip("ランタン未点灯時の移動速度")]
    [SerializeField] private float driftSpeedNormal = 0.8f;
    [Tooltip("ランタン点灯時の移動速度（逃げる）")]
    [SerializeField] private float driftSpeedLit = 2.5f;
    [Tooltip("ランタン未点灯時の目標変更間隔（秒）")]
    [SerializeField] private float driftIntervalNormal = 3.0f;
    [Tooltip("ランタン点灯時の目標変更間隔（秒）")]
    [SerializeField] private float driftIntervalLit = 1.2f;

    [Header("Transparency")]
    [Tooltip("ランタン未点灯時のスプライト透明度（0=完全透明、1=不透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float transparentAlpha = 0.3f;

    [Header("Lantern Damage Multipliers")]
    [Tooltip("ランタン1つ点灯時のダメージ倍率")]
    [SerializeField] private float lanternMul1 = 0.5f;
    [Tooltip("ランタン2つ点灯時のダメージ倍率")]
    [SerializeField] private float lanternMul2 = 1.0f;
    [Tooltip("ランタン3つ点灯（恒久実体化含む）時のダメージ倍率")]
    [SerializeField] private float lanternMul3 = 2.0f;

    [Header("Taunt Animation")]
    [Tooltip("不一致時にボスが一瞬出現してTauntを見せる時間（秒）")]
    [SerializeField] private float tauntDisplaySeconds = 2.5f;

    [Header("Phase 2 Orbit")]
    [Tooltip("フラグメント楕円運動の中心座標（ワールド座標）")]
    [SerializeField] private Vector2 orbitCenter = new Vector2(1.3f, 1.8f);
    [Tooltip("楕円の横半径（水平方向）")]
    [SerializeField] private float orbitRadiusX = 3.5f;
    [Tooltip("楕円の縦半径（垂直方向）")]
    [SerializeField] private float orbitRadius = 2.2f;
    [Tooltip("楕円運動の速度（度/秒）")]
    [SerializeField] private float orbitDegreesPerSecond = 20f;
    [Tooltip("展開移動アニメーション時間（秒）")]
    [SerializeField] private float fragmentSpreadSeconds = 1.5f;
    [Tooltip("フラグメントフェードイン時間（秒）— 展開移動と同時スタート")]
    [SerializeField] private float fragmentFadeInSeconds = 1.0f;
    [Tooltip("フラグメントのスケール（1=ボス本体と同サイズ）")]
    [SerializeField] private float fragmentScale = 0.5f;

    [Header("Damage Sprites")]
    [Tooltip("ダメージ時にランダム表示するスプライト（Damage01/Damage02を設定）")]
    [SerializeField] private Sprite[] damageSprites;
    [Tooltip("ダメージスプライトの表示時間（秒）")]
    [SerializeField] private float damageSpriteDisplaySeconds = 1f;

    [Header("Attack")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private Transform projectileRoot;
    [Tooltip("Phase1 射撃間隔・最小（秒）")]
    [SerializeField] private float fireIntervalMin = 1.5f;
    [Tooltip("Phase1 射撃間隔・最大（秒）")]
    [SerializeField] private float fireIntervalMax = 2.5f;
    [Tooltip("Phase2 通常射撃間隔・最小（秒）")]
    [SerializeField] private float fireIntervalPhase2Min = 2.5f;
    [Tooltip("Phase2 通常射撃間隔・最大（秒）")]
    [SerializeField] private float fireIntervalPhase2Max = 3.5f;
    [Tooltip("不一致透明中の射撃間隔・最小（秒）— 短くして強攻撃")]
    [SerializeField] private float fireIntervalMismatchMin = 0.5f;
    [Tooltip("不一致透明中の射撃間隔・最大（秒）")]
    [SerializeField] private float fireIntervalMismatchMax = 1.0f;
    [Tooltip("Phase2フラグメント発射タイムラグ・最小（秒）")]
    [SerializeField] private float fragmentFireLagMin = 0f;
    [Tooltip("Phase2フラグメント発射タイムラグ・最大（秒）")]
    [SerializeField] private float fragmentFireLagMax = 0.5f;
    [SerializeField] private float bulletSpeed = 6f;
    [SerializeField] private float bulletLifeTime = 5f;
    [SerializeField] private AudioClip fireSE;
    [Range(0f, 1f)]
    [SerializeField] private float fireSEVolume = 1f;

    [Header("Phase2 Normal Fire")]
    [Tooltip("弾が消えてから次を発射するまでの待機時間（秒）")]
    [SerializeField] private float phase2NormalRefireDelay = 0.3f;

    [Header("Memory Game SE")]
    [Tooltip("1発目ヒット時（点滅開始）SE")]
    [SerializeField] private AudioClip memoryFirstHitSe;
    [Tooltip("2発目ヒット：一致成功SE")]
    [SerializeField] private AudioClip memoryMatchSe;
    [Tooltip("2発目ヒット：不一致失敗SE")]
    [SerializeField] private AudioClip memoryMismatchSe;
    [Range(0f, 1f)]
    [SerializeField] private float memoryGameSEVolume = 1f;

    [Header("HP/Shield Bar Size - Phase1 Body")]
    [Tooltip("本体HPバーの幅")]
    [SerializeField] private float bodyBarWidth = 1f;
    [Tooltip("本体HPバーの高さ")]
    [SerializeField] private float bodyBarHeight = 0.06f;
    [Tooltip("本体ShieldバーとHPバーの縦間隔")]
    [SerializeField] private float bodyBarSpacing = 0.5f;
    [Tooltip("本体バーのYオフセット")]
    [SerializeField] private float bodyBarOffsetY = 3f;
    [Tooltip("本体バーのXオフセット")]
    [SerializeField] private float bodyBarOffsetX = -1.1f;
    [Tooltip("本体HP/Shield数値テキストのXオフセット")]
    [SerializeField] private float bodyNumberOffsetX = -1.5f;
    [Tooltip("本体HP/Shield数値テキストのフォントサイズ")]
    [SerializeField] private int bodyFontSize = 120;

    [Header("HP/Shield Bar Size - Phase2 Fragment")]
    [Tooltip("フラグメントHPバーの幅")]
    [SerializeField] private float fragmentBarWidth = 0.8f;
    [Tooltip("フラグメントHPバーの高さ")]
    [SerializeField] private float fragmentBarHeight = 0.1f;
    [Tooltip("フラグメントShieldバーとHPバーの縦間隔")]
    [SerializeField] private float fragmentBarSpacing = 0.09f;
    [Tooltip("フラグメントバーのYオフセット")]
    [SerializeField] private float fragmentBarOffsetY = 2.5f;
    [Tooltip("フラグメントバーのXオフセット")]
    [SerializeField] private float fragmentBarOffsetX = -1f;
    [Tooltip("フラグメントHP/Shield数値テキストのXオフセット")]
    [SerializeField] private float fragmentNumberOffsetX = 0.1f;
    [Tooltip("フラグメントHP/Shield数値テキストのフォントサイズ")]
    [SerializeField] private int fragmentFontSize = 60;

    [Header("Debug")]
    [Tooltip("ONにするとPlay開始時に全ランタンを恒久点灯状態にする（デバッグ用）")]
    [SerializeField] private bool debugAutoLitLanterns = false;

    // =========================================================
    // Runtime
    // =========================================================
    private EnemyStats enemyStats;
    private EnemyHitFeedback hitFeedback;
    private EnemyDamageReceiver enemyDamageReceiver;
    private EnemyPart bodyPart;
    private PixelDancerController player;
    private EnemySpawner enemySpawner;
    private EnemyData enemyData;

    private bool isDead;
    private bool isPhase2;
    private bool permanentlySolid;
    private int litLanternCount;

    // Phase2 記憶ゲーム
    private enum Phase2State
    {
        AttributeReveal,
        WaitingFirstHit,
        WaitingSecondHit,
        MatchSolid,
        MismatchTransparent
    }
    private Phase2State p2State;
    private HalloweenFragment firstHitFragment;
    private bool isMismatchActive;

    // 属性割り当て（フラグメントインデックス → 属性）
    private HalloweenFragment.AttributeType[] attributeAssignment;
    private static readonly HalloweenFragment.AttributeType[] kDefaultAssignment =
    {
        HalloweenFragment.AttributeType.Pumpkin,
        HalloweenFragment.AttributeType.Pumpkin,
        HalloweenFragment.AttributeType.Bat,
        HalloweenFragment.AttributeType.Bat,
        HalloweenFragment.AttributeType.WitchHat,
        HalloweenFragment.AttributeType.WitchHat,
    };

    private float fireTimer;
    private Vector3 basePosition;
    private Animator animator;
    private Coroutine damageSpriteCoroutine;
    private Coroutine attackAnimCoroutine;
    private Coroutine tauntCoroutine;
    private Coroutine attributeRepeatCoroutine;

    // Mismatch中に発射した弾を追跡（弾が全消滅するまで次ラウンド開始を遅らせる）
    private readonly System.Collections.Generic.List<EnemyBullet> _mismatchBullets =
        new System.Collections.Generic.List<EnemyBullet>();

    private float[] fragmentBaseAngles;
    private float[] fragmentFloatOffsets;
    private float orbitPhase;
    private bool isOrbiting;

    private Vector3 driftTarget;
    private float driftTimer;

    // =========================================================
    // Unity Lifecycle
    // =========================================================
    private void Awake()
    {
        enemyStats   = GetComponent<EnemyStats>();
        hitFeedback  = GetComponent<EnemyHitFeedback>();
        animator     = GetComponent<Animator>();
        player       = FindObjectOfType<PixelDancerController>();
        enemySpawner = FindObjectOfType<EnemySpawner>();

        if (enemyStats != null)
        {
            enemyStats.onDamageTaken += OnDamageTaken;
            enemyStats.onKilled      += OnBossKilled;
        }

        if (projectileRoot == null && enemySpawner != null)
            projectileRoot = enemySpawner.ProjectileRoot;
        if (projectileRoot == null)
        {
            GameObject pr = GameObject.Find("ProjectileRoot");
            if (pr != null) projectileRoot = pr.transform;
        }
    }

    private void Start()
    {
        // EnemyShooterは無効化（射撃を自前管理）、EnemyDataをここで取得してから無効化
        EnemyShooter es = GetComponent<EnemyShooter>();
        if (es != null) { enemyData = es.GetEnemyData(); es.enabled = false; }

        bodyPart = GetComponentInChildren<EnemyPart>();

        // EnemyDamageReceiverも無効化（ランタン倍率付きダメージを直接管理）
        enemyDamageReceiver = GetComponent<EnemyDamageReceiver>();
        if (enemyDamageReceiver != null) enemyDamageReceiver.enabled = false;

        // ランタン初期化（Inspectorで未設定の場合はシーンから自動検索）
        if (lanterns == null || lanterns.Length == 0)
            lanterns = FindObjectsOfType<HalloweenLantern>();
        if (lanterns != null)
        {
            foreach (var l in lanterns)
                l?.Init(this);
        }

        // 属性割り当て初期化
        int fragCount = fragments != null ? fragments.Length : 0;
        attributeAssignment = new HalloweenFragment.AttributeType[fragCount];
        for (int i = 0; i < fragCount; i++)
            attributeAssignment[i] = i < kDefaultAssignment.Length ? kDefaultAssignment[i] : HalloweenFragment.AttributeType.Pumpkin;

        // HPバーサイズ適用（本体）
        GetComponent<EnemyHealthDisplay>()?.SetBarSize(
            bodyBarWidth, bodyBarHeight, bodyBarSpacing, bodyBarOffsetY, bodyBarOffsetX,
            bodyNumberOffsetX, bodyFontSize);

        // フラグメント初期化（Phase1中は非表示）
        EnemyShield bossShield = GetComponent<EnemyShield>();
        if (fragments != null)
        {
            for (int i = 0; i < fragments.Length; i++)
            {
                if (fragments[i] == null) continue;
                var type = i < attributeAssignment.Length ? attributeAssignment[i] : HalloweenFragment.AttributeType.Pumpkin;
                fragments[i].Init(this, type);
                var fhd = fragments[i].GetComponent<FragmentHealthDisplay>();
                if (fhd != null)
                {
                    fhd.Init(enemyStats, bossShield);
                    fhd.SetBarSize(fragmentBarWidth, fragmentBarHeight, fragmentBarSpacing, fragmentBarOffsetY, fragmentBarOffsetX,
                                   fragmentNumberOffsetX, fragmentFontSize);
                }
                fragments[i].SetVisible(false);
                fragments[i].SetSolid(false);
            }
        }
        if (fragmentsRoot != null) fragmentsRoot.gameObject.SetActive(false);

        // Phase1開始: 半透明（ランタン未点灯状態）
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (bodyRenderer != null)
        {
            Color c = bodyRenderer.color;
            c.a = transparentAlpha;
            bodyRenderer.color = c;
        }

        basePosition = transform.position;
        driftTarget  = basePosition;
        driftTimer   = 0f;
        fireTimer = Random.Range(fireIntervalMin, fireIntervalMax);

        if (debugAutoLitLanterns && lanterns != null)
        {
            foreach (var l in lanterns)
                l?.MakePermanent();
            OnLanternStateChanged();
        }
    }

    private void Update()
    {
        if (isDead) return;

        // Phase2移行チェック
        if (!isPhase2 && enemyStats != null && enemyStats.GetHpPercentage() <= phase2HpThreshold)
        {
            StartPhase2();
            return;
        }

        // フロートアニメーション（Phase1のみ）
        if (!isPhase2)
        {
            if (!permanentlySolid) UpdatePhase1Drift();
            float offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.position = basePosition + new Vector3(0f, offsetY, 0f);
        }

        // Phase2 フラグメント円運動
        if (isPhase2 && isOrbiting)
        {
            orbitPhase += orbitDegreesPerSecond * Time.deltaTime;
            if (orbitPhase >= 360f) orbitPhase -= 360f;
            UpdateFragmentOrbitPositions();
        }

        // 射撃（Phase2通常時は2スロットコルーチンが担当するのでタイマー不要）
        if (!isPhase2 || isMismatchActive)
        {
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f)
            {
                fireTimer = isMismatchActive
                    ? Random.Range(fireIntervalMismatchMin, fireIntervalMismatchMax)
                    : Random.Range(fireIntervalMin, fireIntervalMax);
                FireAtPlayer();
            }
        }
    }

    private void OnDestroy()
    {
        isDead = true;
        if (enemyStats != null)
        {
            enemyStats.onDamageTaken -= OnDamageTaken;
            enemyStats.onKilled      -= OnBossKilled;
        }
    }

    private void OnBossKilled()
    {
        if (!isPhase2 || fragments == null || enemyStats == null) return;

        var positions = new System.Collections.Generic.List<Vector3>();
        foreach (var f in fragments)
        {
            if (f != null) positions.Add(f.transform.position);
        }
        if (positions.Count > 0)
            enemyStats.deathEffectPositions = positions.ToArray();
    }

    // =========================================================
    // ランタン状態
    // =========================================================

    /// <summary>HalloweenLanternから点灯/消灯時に呼ばれる</summary>
    public void OnLanternStateChanged()
    {
        litLanternCount = 0;
        if (lanterns != null)
        {
            foreach (var l in lanterns)
            {
                if (l != null && l.IsLit) litLanternCount++;
            }
        }

        // 3つ同時点灯 → 恒久実体化
        if (!permanentlySolid && lanterns != null && litLanternCount >= lanterns.Length && lanterns.Length > 0)
        {
            permanentlySolid = true;
            foreach (var l in lanterns)
                l?.MakePermanent();
        }

        if (isPhase2)
            UpdatePhase2SolidState();
        else
        {
            UpdatePhase1SolidState();
            // ランタン状態が変化したら即座に新目標へ（逃げる挙動のトリガー）
            if (!permanentlySolid) { PickNewDriftTarget(); driftTimer = litLanternCount > 0 ? driftIntervalLit : driftIntervalNormal; }
        }
    }

    private void UpdatePhase1SolidState()
    {
        bool solid = permanentlySolid || litLanternCount > 0;
        if (bodyCollider != null) bodyCollider.enabled = solid;

        float mul = solid ? GetLanternDamageMultiplier() : 0f;
        if (bodyPart != null) bodyPart.damageMultiplier = mul;
        // EnemyPart の Max(1,...) フロアで mul=0 でも最低1ダメージが入るため、
        // mul=0 の場合は incomingDamageMultiplier=0 でも塞ぐ
        if (enemyStats != null)
            enemyStats.incomingDamageMultiplier = (mul > 0f) ? 1f : 0f;

        if (bodyRenderer != null && damageSpriteCoroutine == null)
        {
            Color c = bodyRenderer.color;
            c.a = solid ? 1f : transparentAlpha;
            bodyRenderer.color = c;
        }
    }

    // =========================================================
    // Phase2 移行
    // =========================================================
    private void StartPhase2()
    {
        isPhase2 = true;
        if (bodyCollider != null) bodyCollider.enabled = false;
        GetComponent<EnemyHealthDisplay>()?.SetBarsVisible(false);

        // Phase2ではEnemyPart経由のダメージは使わないのでリセット
        if (bodyPart != null) bodyPart.damageMultiplier = 1f;
        if (enemyStats != null) enemyStats.incomingDamageMultiplier = 1f;

        // 並走中のコルーチンを停止してボス本体を即座に非表示
        if (damageSpriteCoroutine != null) { StopCoroutine(damageSpriteCoroutine); damageSpriteCoroutine = null; }
        if (attackAnimCoroutine != null)   { StopCoroutine(attackAnimCoroutine);   attackAnimCoroutine   = null; }
        if (animator != null) animator.enabled = false;

        StartCoroutine(Phase2TransitionRoutine());
    }

    private IEnumerator Phase2TransitionRoutine()
    {
        // ボス本体フェードアウト
        if (bodyRenderer != null && bodyFadeOutSeconds > 0.001f)
        {
            float elapsed = 0f;
            Color orig = bodyRenderer.color;
            while (elapsed < bodyFadeOutSeconds)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, elapsed / bodyFadeOutSeconds);
                bodyRenderer.color = new Color(orig.r, orig.g, orig.b, a);
                yield return null;
            }
            bodyRenderer.enabled = false;
        }

        // 軌道角度初期化（6体を60°間隔で配置、ランダム開始角）
        orbitPhase = Random.Range(0f, 360f);
        int fragCount = fragments != null ? fragments.Length : 0;
        fragmentBaseAngles = new float[fragCount];
        for (int i = 0; i < fragCount; i++)
            fragmentBaseAngles[i] = i * (360f / Mathf.Max(1, fragCount));

        // フラグメントごとのフロートオフセット（ランダム位相でばらつかせる）
        fragmentFloatOffsets = new float[fragCount];
        for (int i = 0; i < fragCount; i++)
            fragmentFloatOffsets[i] = Random.Range(0f, Mathf.PI * 2f);

        // フラグメントをボス位置に配置・非表示で初期化
        Vector3 bossPos = transform.position;
        if (fragmentsRoot != null) fragmentsRoot.gameObject.SetActive(true);
        ShuffleAttributeAssignment();
        ApplyAttributeAssignment();
        if (fragments != null)
        {
            foreach (var f in fragments)
            {
                if (f == null) continue;
                f.transform.position = bossPos;
                f.transform.localScale = Vector3.one * fragmentScale;
                f.SetVisible(true);
                f.SetBodyAlpha(0f);
                f.SetSolid(false);
                f.SetGlowing(false);
                f.ShowAttribute(false);
            }
        }

        // 展開アニメーション：移動とフェードインを独立した時間で同時進行
        float spread = 0f;
        float maxDuration = Mathf.Max(fragmentSpreadSeconds, fragmentFadeInSeconds);
        while (spread < maxDuration)
        {
            spread += Time.deltaTime;
            float tMove = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(spread / fragmentSpreadSeconds));
            float tFade = Mathf.Clamp01(spread / fragmentFadeInSeconds);
            for (int i = 0; i < fragCount; i++)
            {
                if (fragments[i] == null) continue;
                Vector3 target = GetOrbitPosition(i);
                target.y += Mathf.Sin((Time.time + fragmentFloatOffsets[i]) * floatSpeed) * floatAmplitude;
                fragments[i].transform.position = Vector3.Lerp(bossPos, target, tMove);
                fragments[i].SetBodyAlpha(tFade);
            }
            yield return null;
        }

        isOrbiting = true;

        // Phase2通常発射: 2スロットを半interval分ずらして自然な分散
        float slotOffset = Random.Range(fireIntervalPhase2Min, fireIntervalPhase2Max) * 0.5f;
        StartCoroutine(Phase2NormalBulletSlotRoutine(0f));
        StartCoroutine(Phase2NormalBulletSlotRoutine(slotOffset));

        StartCoroutine(MemoryGameRoutine());
    }

    // =========================================================
    // Phase2 記憶ゲーム
    // =========================================================
    private IEnumerator MemoryGameRoutine()
    {
        while (!isDead && enemyStats != null && enemyStats.HP > 0)
        {
            // ─── 属性表示 ───
            p2State = Phase2State.AttributeReveal;
            isMismatchActive = false;
            if (fragments != null)
                foreach (var f in fragments) f?.ShowAttribute(true);

            yield return new WaitForSeconds(attributeDisplaySeconds);
            if (isDead || enemyStats == null || enemyStats.HP <= 0) yield break;

            // ─── 属性非表示、1発目待ち ───
            if (fragments != null)
                foreach (var f in fragments) f?.ShowAttribute(false);
            p2State = Phase2State.WaitingFirstHit;
            firstHitFragment = null;
            UpdatePhase2SolidState();

            // 3ランタン恒久点灯中は属性を周期的に再表示
            if (permanentlySolid)
            {
                if (attributeRepeatCoroutine != null) StopCoroutine(attributeRepeatCoroutine);
                attributeRepeatCoroutine = StartCoroutine(AttributeRepeatRoutine());
            }

            // ─── 一致/不一致のどちらかになるまで待機 ───
            yield return new WaitUntil(() =>
                p2State == Phase2State.MatchSolid ||
                p2State == Phase2State.MismatchTransparent ||
                isDead || enemyStats == null || enemyStats.HP <= 0);

            // 属性再表示コルーチン停止＋確実に非表示
            if (attributeRepeatCoroutine != null) { StopCoroutine(attributeRepeatCoroutine); attributeRepeatCoroutine = null; }
            if (fragments != null) foreach (var f in fragments) f?.ShowAttribute(false);

            if (isDead || enemyStats == null || enemyStats.HP <= 0) yield break;

            if (p2State == Phase2State.MatchSolid)
            {
                // ─── 一致: matchSolidSeconds間全実体（ダメージ有） ───
                SetAllFragmentsSolid(true);
                PlayAllFragmentMatchVfx();
                yield return new WaitForSeconds(matchSolidSeconds);
                if (isDead || enemyStats == null || enemyStats.HP <= 0) yield break;
                StopAllFragmentMatchVfx();
            }
            else if (p2State == Phase2State.MismatchTransparent)
            {
                // ─── 不一致: mismatchTransparentSeconds間全透明（強攻撃） ───
                _mismatchBullets.Clear();
                isMismatchActive = true;
                SetAllFragmentsSolid(false);
                PlayAllFragmentMismatchVfx();
                if (tauntCoroutine != null) StopCoroutine(tauntCoroutine);
                tauntCoroutine = StartCoroutine(TauntRoutine());
                yield return new WaitForSeconds(mismatchTransparentSeconds);
                if (isDead || enemyStats == null || enemyStats.HP <= 0) yield break;
                StopAllFragmentMismatchVfx();
                isMismatchActive = false;

                // Mismatch中に発射した弾が全て消えるまで待機してから次ラウンド開始
                yield return new WaitUntil(() =>
                {
                    _mismatchBullets.RemoveAll(b => b == null);
                    return _mismatchBullets.Count == 0 || isDead || enemyStats == null || enemyStats.HP <= 0;
                });
                _mismatchBullets.Clear();
                if (isDead || enemyStats == null || enemyStats.HP <= 0) yield break;

                // 属性シャッフル＋軌道位置シャッフル
                ShuffleAttributeAssignment();
                ShuffleOrbitPositions();
                ApplyAttributeAssignment();
            }
        }
    }

    private IEnumerator AttributeRepeatRoutine()
    {
        while (!isDead && p2State != Phase2State.MatchSolid && p2State != Phase2State.MismatchTransparent)
        {
            yield return new WaitForSeconds(attributeRepeatIntervalSeconds);

            if (isDead || p2State == Phase2State.MatchSolid || p2State == Phase2State.MismatchTransparent) yield break;

            if (fragments != null)
                foreach (var f in fragments) f?.ShowAttribute(true);

            yield return new WaitForSeconds(attributeDisplaySeconds);

            if (fragments != null)
                foreach (var f in fragments) f?.ShowAttribute(false);
        }
    }

    private void UpdatePhase2SolidState()
    {
        // MatchSolid / MismatchTransparent 中はランタン無視
        if (p2State == Phase2State.MatchSolid)
        {
            SetAllFragmentsSolid(true);
            SetAllFragmentsAlpha(1f);
            return;
        }
        if (p2State == Phase2State.MismatchTransparent)
        {
            SetAllFragmentsSolid(false);
            SetAllFragmentsAlpha(1f);
            return;
        }
        // その他: ランタン点灯数でフラグメント実体/透明
        bool solid = litLanternCount > 0;
        SetAllFragmentsSolid(solid);
        SetAllFragmentsAlpha(solid ? 1f : transparentAlpha);
    }

    private void SetAllFragmentsSolid(bool solid)
    {
        if (fragments == null) return;
        foreach (var f in fragments)
            f?.SetSolid(solid);
    }

    private void SetAllFragmentsAlpha(float alpha)
    {
        if (fragments == null) return;
        foreach (var f in fragments)
            f?.SetBodyAlpha(alpha);
    }

    private void PlayAllFragmentMatchVfx()    { if (fragments != null) foreach (var f in fragments) f?.PlayMatchVfx(); }
    private void StopAllFragmentMatchVfx()    { if (fragments != null) foreach (var f in fragments) f?.StopMatchVfx(); }
    private void PlayAllFragmentMismatchVfx() { if (fragments != null) foreach (var f in fragments) f?.PlayMismatchVfx(); }
    private void StopAllFragmentMismatchVfx() { if (fragments != null) foreach (var f in fragments) f?.StopMismatchVfx(); }

    private void ReflectBulletOffFragment(EnemyBullet bullet, HalloweenFragment fragment)
    {
        if (bullet == null) return;

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        // フラグメント中心→弾位置 を法線として反射方向を計算
        Vector2 normal = ((Vector2)bullet.transform.position - (Vector2)fragment.transform.position).normalized;
        if (normal.sqrMagnitude < 0.0001f) normal = Vector2.up;

        Vector2 inDir = rb.linearVelocity.normalized;
        if (inDir.sqrMagnitude < 0.0001f) inDir = -normal;

        bullet.SetDirection(Vector2.Reflect(inDir, normal));

        // 同フラグメントへの即時再ヒット防止
        Collider2D fragCol = fragment.GetComponent<Collider2D>();
        if (fragCol != null) bullet.SetOwnerCollisionIgnore(fragCol, 0.3f);
    }

    private void PlaySE(AudioClip clip, Vector3 pos, float volume)
    {
        if (clip == null) return;
        float vol = volume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        AudioSource.PlayClipAtPoint(clip, pos, vol);
    }

    // =========================================================
    // ランタン倍率
    // =========================================================
    private float GetLanternDamageMultiplier()
    {
        switch (litLanternCount)
        {
            case 1:  return lanternMul1;
            case 2:  return lanternMul2;
            default: return lanternMul3;
        }
    }

    // =========================================================
    // Phase1 ダメージ受け（EnemyDamageReceiverの代わり）
    // =========================================================
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || isPhase2) return;

        EnemyBullet bullet = other.GetComponent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;

        bullet.RegisterEnemyHitAsBounce();

        float lanternMul = GetLanternDamageMultiplier();
        float justMul    = Mathf.Max(1f, bullet.DamageMultiplier);
        bool  isPowered  = justMul > 1.0001f;
        int   dmg        = Mathf.Max(1, Mathf.RoundToInt(bullet.DamageValue * justMul * lanternMul));

        enemyStats?.Damage(dmg, isPowered);

        if (enemyStats != null && enemyStats.HP > 0)
            hitFeedback?.PlayHitFeedback(dmg, isPowered, transform.position);

        Destroy(bullet.gameObject);
    }

    // =========================================================
    // フラグメントヒット（HalloweenFragmentから呼ばれる）
    // =========================================================
    public void OnFragmentHit(HalloweenFragment fragment, EnemyBullet bullet)
    {
        if (isDead || !isPhase2) return;

        switch (p2State)
        {
            case Phase2State.WaitingFirstHit:
                firstHitFragment = fragment;
                fragment.StartBlinking();
                p2State = Phase2State.WaitingSecondHit;
                PlaySE(memoryFirstHitSe, fragment.transform.position, memoryGameSEVolume);
                ReflectBulletOffFragment(bullet, fragment);
                break;

            case Phase2State.WaitingSecondHit:
                if (fragment == firstHitFragment) return; // 同フラグメント無視

                bool match = (fragment.Attribute == firstHitFragment.Attribute);
                firstHitFragment.StopBlinking();
                fragment.SetGlowing(false);

                PlaySE(match ? memoryMatchSe : memoryMismatchSe,
                       fragment.transform.position, memoryGameSEVolume);
                p2State = match ? Phase2State.MatchSolid : Phase2State.MismatchTransparent;
                UpdatePhase2SolidState();
                ReflectBulletOffFragment(bullet, fragment);
                break;

            case Phase2State.MatchSolid:
                // ダメージ適用後に反射（弾を消費しない）
                ApplyDamageToSharedHP(fragment, bullet);
                ReflectBulletOffFragment(bullet, fragment);
                break;
        }
    }

    private void ApplyDamageToSharedHP(HalloweenFragment fragment, EnemyBullet bullet)
    {
        if (enemyStats == null) return;

        bullet.RegisterEnemyHitAsBounce();

        float lanternMul = GetLanternDamageMultiplier();
        float justMul    = Mathf.Max(1f, bullet.DamageMultiplier);
        bool  isPowered  = justMul > 1.0001f;
        int   dmg        = Mathf.Max(1, Mathf.RoundToInt(bullet.DamageValue * justMul * lanternMul));

        enemyStats.Damage(dmg, isPowered);

        if (enemyStats.HP > 0)
        {
            Vector3 fragPos = fragment.transform.position;
            hitFeedback?.PlayHitFeedback(dmg, isPowered, fragPos, anchorOverride: fragPos);
            enemyDamageReceiver?.TryPlayReflectedEnemyHitSeFromPart(isPowered);
            fragment.TriggerShake(isPowered);
            fragment.ShowDamageSprite(damageSprites, damageSpriteDisplaySeconds);
        }
    }

    // =========================================================
    // 属性割り当て
    // =========================================================
    private void ApplyAttributeAssignment()
    {
        if (fragments == null) return;
        for (int i = 0; i < fragments.Length; i++)
        {
            if (fragments[i] == null) continue;
            var type = i < attributeAssignment.Length ? attributeAssignment[i] : HalloweenFragment.AttributeType.Pumpkin;
            fragments[i].Attribute = type;
        }
    }

    private void ShuffleAttributeAssignment()
    {
        for (int i = attributeAssignment.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = attributeAssignment[i];
            attributeAssignment[i] = attributeAssignment[j];
            attributeAssignment[j] = tmp;
        }
    }

    // =========================================================
    // Phase1 ドリフト移動
    // =========================================================
    private void UpdatePhase1Drift()
    {
        float speed    = litLanternCount > 0 ? driftSpeedLit    : driftSpeedNormal;
        float interval = litLanternCount > 0 ? driftIntervalLit : driftIntervalNormal;

        driftTimer -= Time.deltaTime;
        if (driftTimer <= 0f || Vector3.Distance(basePosition, driftTarget) < 0.05f)
        {
            PickNewDriftTarget();
            driftTimer = interval;
        }

        basePosition = Vector3.MoveTowards(basePosition, driftTarget, speed * Time.deltaTime);
    }

    private void PickNewDriftTarget()
    {
        float x = Random.Range(driftAreaMin.x, driftAreaMax.x);
        float y = Random.Range(driftAreaMin.y, driftAreaMax.y);
        driftTarget = new Vector3(x, y, transform.position.z);
    }

    private void ShuffleOrbitPositions()
    {
        if (fragmentBaseAngles == null) return;
        for (int i = fragmentBaseAngles.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            float tmp = fragmentBaseAngles[i];
            fragmentBaseAngles[i] = fragmentBaseAngles[j];
            fragmentBaseAngles[j] = tmp;
        }
    }

    private Vector3 GetOrbitPosition(int index)
    {
        float angleRad = (fragmentBaseAngles[index] + orbitPhase) * Mathf.Deg2Rad;
        return new Vector3(
            orbitCenter.x + orbitRadiusX * Mathf.Cos(angleRad),
            orbitCenter.y + orbitRadius  * Mathf.Sin(angleRad),
            transform.position.z
        );
    }

    private void UpdateFragmentOrbitPositions()
    {
        if (fragments == null || fragmentBaseAngles == null) return;
        for (int i = 0; i < fragments.Length; i++)
        {
            if (fragments[i] == null) continue;
            Vector3 pos = GetOrbitPosition(i);
            if (fragmentFloatOffsets != null && i < fragmentFloatOffsets.Length)
                pos.y += Mathf.Sin((Time.time + fragmentFloatOffsets[i]) * floatSpeed) * floatAmplitude;
            fragments[i].transform.position = pos;
        }
    }

    // =========================================================
    // 射撃
    // =========================================================
    private void FireAtPlayer()
    {
        if (bulletPrefab == null || projectileRoot == null || player == null) return;

        if (!isPhase2)
        {
            if (attackAnimCoroutine != null) StopCoroutine(attackAnimCoroutine);
            attackAnimCoroutine = StartCoroutine(AttackAnimRoutine());
            SpawnBulletAt(transform.position);
        }
        else
        {
            // Phase2: 展開完了後のみ各フラグメントのMuzzleからランダム遅延で射撃
            if (!isOrbiting) return;
            if (fragments != null)
            {
                foreach (var f in fragments)
                {
                    if (f == null) continue;
                    float lag = Random.Range(fragmentFireLagMin, fragmentFireLagMax);
                    if (lag <= 0.001f)
                        SpawnBulletAt(f.GetMuzzle().position);
                    else
                        StartCoroutine(SpawnBulletDelayed(f, lag));
                }
            }
        }
    }

    private IEnumerator AttackAnimRoutine()
    {
        if (animator != null) animator.Play("Balloon_Attack", 0, 0f);
        yield return new WaitForSeconds(0.5f);
        if (animator != null && animator.enabled && !isPhase2 && damageSpriteCoroutine == null)
            animator.Play("Balloon", 0, 0f);
        attackAnimCoroutine = null;
    }

    // =========================================================
    // ダメージスプライト表示
    // =========================================================
    private void OnDamageTaken()
    {
        // Phase2中はボス本体を表示しない（フラグメントがボス本体）
        if (isDead || isPhase2) return;
        if (damageSprites == null || damageSprites.Length == 0) return;
        if (damageSpriteCoroutine != null) StopCoroutine(damageSpriteCoroutine);
        if (attackAnimCoroutine != null) { StopCoroutine(attackAnimCoroutine); attackAnimCoroutine = null; }
        damageSpriteCoroutine = StartCoroutine(DamageSpriteRoutine());
    }

    private IEnumerator DamageSpriteRoutine()
    {
        Sprite dmgSprite = damageSprites[Random.Range(0, damageSprites.Length)];
        bool wasBodyEnabled = bodyRenderer.enabled;
        Color savedColor = bodyRenderer.color;

        if (animator != null) animator.enabled = false;
        bodyRenderer.sprite = dmgSprite;
        bodyRenderer.color = new Color(savedColor.r, savedColor.g, savedColor.b, 1f);
        bodyRenderer.enabled = true;

        yield return new WaitForSeconds(damageSpriteDisplaySeconds);

        damageSpriteCoroutine = null;
        if (isDead) yield break;

        bodyRenderer.color = savedColor;
        bodyRenderer.enabled = wasBodyEnabled;
        if (animator != null)
        {
            animator.enabled = true;
            if (!isPhase2) animator.Play("Balloon", 0, 0f);
        }

        // ダメージスプライト表示中にランタン状態が変化していた場合の補正
        if (!isPhase2)
            UpdatePhase1SolidState();
    }

    // =========================================================
    // Tauntアニメーション（Phase2不一致時）
    // Phase2ではフラグメントがボス本体 → 全フラグメントでアニメーションを再生
    // ボス本体(bodyRenderer)はPhase2中は常に非表示のまま
    // =========================================================
    private IEnumerator TauntRoutine()
    {
        if (fragments != null)
        {
            foreach (var f in fragments)
                f?.PlayAnim("Balloon_Taunt");
        }

        yield return new WaitForSeconds(tauntDisplaySeconds);

        tauntCoroutine = null;
        if (!isDead && fragments != null)
        {
            foreach (var f in fragments)
                f?.PlayAnim("Balloon");
        }
    }

    // =========================================================
    // 弾タイプ選択（WalkerMechControllerと同パターン）
    // =========================================================
    private EnemyData.BulletType PickBulletType()
    {
        if (enemyData == null || enemyData.bulletTypes == null || enemyData.bulletTypes.Length == 0)
            return null;

        EnemyData.BulletFiringRoutine routine = GetActiveBulletRoutine();
        if (routine != null &&
            routine.routineType == EnemyData.BulletFiringRoutine.RoutineType.Probability &&
            routine.probabilityEntries != null && routine.probabilityEntries.Length > 0)
        {
            int idx = PickProbabilityBulletIndex(routine);
            if (idx >= 0 && idx < enemyData.bulletTypes.Length)
                return enemyData.bulletTypes[idx];
        }

        return enemyData.bulletTypes[0];
    }

    private EnemyData.BulletFiringRoutine GetActiveBulletRoutine()
    {
        if (enemyData == null || enemyData.bulletFiringRoutines == null) return null;

        bool isLowHp = enemyData.useHpBasedRoutineSwitch &&
                       enemyStats != null &&
                       enemyStats.GetHpPercentage() <= enemyData.hpThresholdPercentage;

        int routineIndex = isLowHp
            ? (int)enemyData.bulletRoutineBelowThreshold
            : (int)enemyData.bulletRoutineAboveThreshold;

        if (routineIndex < 0 || routineIndex >= enemyData.bulletFiringRoutines.Length) return null;
        return enemyData.bulletFiringRoutines[routineIndex];
    }

    private int PickProbabilityBulletIndex(EnemyData.BulletFiringRoutine routine)
    {
        float total = 0f;
        foreach (var e in routine.probabilityEntries)
            total += Mathf.Max(0f, e.probabilityPercentage);
        if (total <= 0f) return 0;

        float r = Random.value * total;
        float acc = 0f;
        foreach (var e in routine.probabilityEntries)
        {
            acc += Mathf.Max(0f, e.probabilityPercentage);
            if (r <= acc)
                return Mathf.Clamp(e.bulletTypeIndex, 0, enemyData.bulletTypes.Length - 1);
        }
        var last = routine.probabilityEntries[routine.probabilityEntries.Length - 1];
        return Mathf.Clamp(last.bulletTypeIndex, 0, enemyData.bulletTypes.Length - 1);
    }

    private IEnumerator SpawnBulletDelayed(HalloweenFragment fragment, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isDead && fragment != null)
            SpawnBulletAt(fragment.GetMuzzle().position);
    }

    private void SpawnBulletAt(Vector3 pos)
    {
        if (bulletPrefab == null || projectileRoot == null || player == null) return;

        EnemyData.BulletType bt = PickBulletType();
        Vector2 baseDir = ((Vector2)(player.transform.position - pos)).normalized;

        int shots = 1;
        float half = 0f;
        float spawnOffset = 0f;
        float launchDelay = 0f;

        if (bt != null && bt.useMultiShot)
        {
            shots      = Mathf.Max(1, bt.shotsPerFire);
            half       = Mathf.Clamp(bt.spreadAngleDeg, 0f, 180f) * 0.5f;
            spawnOffset = bt.multiShotSpawnOffset;
            launchDelay = bt.multiShotLaunchDelay;
        }

        if (launchDelay > 0.0001f && shots > 1)
        {
            StartCoroutine(FireMultiShotDelayed(pos, baseDir, bt, shots, half, spawnOffset, launchDelay));
        }
        else
        {
            for (int i = 0; i < shots; i++)
            {
                Vector2 dir = half > 0.0001f ? RotateVec2(baseDir, Random.Range(-half, half)) : baseDir;
                Vector3 spawnPos = pos;
                if (spawnOffset > 0.0001f && shots > 1)
                {
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    spawnPos += (Vector3)(perp * ((i - (shots - 1) * 0.5f) * spawnOffset));
                }
                SpawnOneBullet(spawnPos, dir, bt);
            }
        }
    }

    private IEnumerator FireMultiShotDelayed(Vector3 pos, Vector2 baseDir, EnemyData.BulletType bt,
                                              int shots, float half, float spawnOffset, float launchDelay)
    {
        for (int i = 0; i < shots; i++)
        {
            if (isDead) yield break;
            Vector2 dir = half > 0.0001f ? RotateVec2(baseDir, Random.Range(-half, half)) : baseDir;
            Vector3 spawnPos = pos;
            if (spawnOffset > 0.0001f && shots > 1)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                spawnPos += (Vector3)(perp * ((i - (shots - 1) * 0.5f) * spawnOffset));
            }
            SpawnOneBullet(spawnPos, dir, bt);
            if (i < shots - 1)
                yield return new WaitForSeconds(launchDelay);
        }
    }

    // =========================================================
    // Phase2 通常発射（2スロット制、BulletType不使用）
    // =========================================================
    private IEnumerator Phase2NormalBulletSlotRoutine(float initialDelay)
    {
        if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);

        while (!isDead && isPhase2 && enemyStats != null && enemyStats.HP > 0)
        {
            // mismatch中 or 展開前は待機（mismatchはFireAtPlayerが担当）
            if (isMismatchActive || !isOrbiting)
            {
                yield return null;
                continue;
            }

            HalloweenFragment target = PickRandomActiveFragment();
            if (target == null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            EnemyBullet bullet = SpawnPhase2NormalBulletFrom(target);

            if (bullet != null)
                yield return new WaitUntil(() => bullet == null || isDead || !isPhase2);

            if (isDead || !isPhase2) yield break;

            // 弾消滅後、少し待ってから次を発射（mismatchに切り替わっていたらスキップ）
            if (!isMismatchActive && phase2NormalRefireDelay > 0f)
                yield return new WaitForSeconds(phase2NormalRefireDelay);
        }
    }

    private HalloweenFragment PickRandomActiveFragment()
    {
        if (fragments == null) return null;
        var active = new System.Collections.Generic.List<HalloweenFragment>();
        foreach (var f in fragments)
            if (f != null) active.Add(f);
        if (active.Count == 0) return null;
        return active[Random.Range(0, active.Count)];
    }

    private EnemyBullet SpawnPhase2NormalBulletFrom(HalloweenFragment fragment)
    {
        if (bulletPrefab == null || projectileRoot == null || player == null) return null;

        Vector3 pos = fragment.GetMuzzle().position;
        Vector2 dir = ((Vector2)(player.transform.position - pos)).normalized;

        // BulletTypes[0]をノーマル弾として使用
        EnemyData.BulletType normalBt = (enemyData != null && enemyData.bulletTypes != null && enemyData.bulletTypes.Length > 0)
            ? enemyData.bulletTypes[0]
            : null;

        EnemyBullet bullet = Instantiate(bulletPrefab, pos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);
        bullet.ApplyBullet(bulletSpeed, bulletLifeTime);

        if (normalBt != null)
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, normalBt, bulletSpeed, bulletLifeTime, null, bulletPrefab, projectileRoot);

        Collider2D[] cols = GetComponentsInChildren<Collider2D>();
        foreach (var col in cols)
            if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);

        if (fireSE != null)
        {
            float vol = fireSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioSource.PlayClipAtPoint(fireSE, pos, vol);
        }

        return bullet;
    }

    private void SpawnOneBullet(Vector3 pos, Vector2 dir, EnemyData.BulletType bt)
    {
        EnemyBullet bullet = Instantiate(bulletPrefab, pos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);
        bullet.ApplyBullet(bulletSpeed, bulletLifeTime);

        if (bt != null)
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, bulletSpeed, bulletLifeTime, null, bulletPrefab, projectileRoot);

        Collider2D[] cols = GetComponentsInChildren<Collider2D>();
        foreach (var col in cols)
        {
            if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
        }

        if (isMismatchActive)
            _mismatchBullets.Add(bullet);

        if (fireSE != null)
        {
            float vol = fireSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioSource.PlayClipAtPoint(fireSE, pos, vol);
        }
    }

    private static Vector2 RotateVec2(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
