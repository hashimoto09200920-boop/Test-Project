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

    [Header("Attack")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private Transform projectileRoot;
    [Tooltip("通常の射撃間隔（秒）")]
    [SerializeField] private float fireInterval = 2f;
    [Tooltip("不一致透明中の射撃間隔（秒）— 短くして強攻撃")]
    [SerializeField] private float fireIntervalMismatch = 0.8f;
    [SerializeField] private float bulletSpeed = 6f;
    [SerializeField] private float bulletLifeTime = 5f;
    [SerializeField] private AudioClip fireSE;
    [Range(0f, 1f)]
    [SerializeField] private float fireSEVolume = 1f;

    // =========================================================
    // Runtime
    // =========================================================
    private EnemyStats enemyStats;
    private EnemyHitFeedback hitFeedback;
    private PixelDancerController player;
    private EnemySpawner enemySpawner;

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

    // =========================================================
    // Unity Lifecycle
    // =========================================================
    private void Awake()
    {
        enemyStats   = GetComponent<EnemyStats>();
        hitFeedback  = GetComponent<EnemyHitFeedback>();
        player       = FindObjectOfType<PixelDancerController>();
        enemySpawner = FindObjectOfType<EnemySpawner>();

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
        // EnemyShooterは無効化（射撃を自前管理）
        EnemyShooter es = GetComponent<EnemyShooter>();
        if (es != null) es.enabled = false;

        // ランタン初期化
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

        // フラグメント初期化（Phase1中は非表示）
        if (fragments != null)
        {
            for (int i = 0; i < fragments.Length; i++)
            {
                if (fragments[i] == null) continue;
                var type = i < attributeAssignment.Length ? attributeAssignment[i] : HalloweenFragment.AttributeType.Pumpkin;
                fragments[i].Init(this, type);
                fragments[i].SetVisible(false);
                fragments[i].SetSolid(false);
            }
        }
        if (fragmentsRoot != null) fragmentsRoot.gameObject.SetActive(false);

        // Phase1開始: 透明
        if (bodyCollider != null) bodyCollider.enabled = false;

        fireTimer = fireInterval;
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

        // 射撃
        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            fireTimer = isMismatchActive ? fireIntervalMismatch : fireInterval;
            FireAtPlayer();
        }
    }

    private void OnDestroy()
    {
        isDead = true;
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
            UpdatePhase1SolidState();
    }

    private void UpdatePhase1SolidState()
    {
        bool solid = permanentlySolid || litLanternCount > 0;
        if (bodyCollider != null) bodyCollider.enabled = solid;
    }

    // =========================================================
    // Phase2 移行
    // =========================================================
    private void StartPhase2()
    {
        isPhase2 = true;
        if (bodyCollider != null) bodyCollider.enabled = false;
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

        // フラグメント出現
        if (fragmentsRoot != null) fragmentsRoot.gameObject.SetActive(true);
        ApplyAttributeAssignment();
        if (fragments != null)
        {
            foreach (var f in fragments)
            {
                if (f == null) continue;
                f.SetVisible(true);
                f.SetSolid(false);
                f.SetGlowing(false);
                f.ShowAttribute(false);
            }
        }

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

            // ─── 一致/不一致のどちらかになるまで待機 ───
            yield return new WaitUntil(() =>
                p2State == Phase2State.MatchSolid ||
                p2State == Phase2State.MismatchTransparent ||
                isDead || enemyStats == null || enemyStats.HP <= 0);

            if (isDead || enemyStats == null || enemyStats.HP <= 0) yield break;

            if (p2State == Phase2State.MatchSolid)
            {
                // ─── 一致: 20秒間全実体（ダメージ有） ───
                SetAllFragmentsSolid(true);
                yield return new WaitForSeconds(matchSolidSeconds);
                if (isDead || enemyStats == null || enemyStats.HP <= 0) yield break;
            }
            else if (p2State == Phase2State.MismatchTransparent)
            {
                // ─── 不一致: 10秒間全透明（強攻撃） ───
                isMismatchActive = true;
                SetAllFragmentsSolid(false);
                yield return new WaitForSeconds(mismatchTransparentSeconds);
                if (isDead || enemyStats == null || enemyStats.HP <= 0) yield break;
                isMismatchActive = false;

                // 属性シャッフル
                ShuffleAttributeAssignment();
                ApplyAttributeAssignment();
            }
        }
    }

    private void UpdatePhase2SolidState()
    {
        // MatchSolid / MismatchTransparent 中はランタン無視
        if (p2State == Phase2State.MatchSolid)   { SetAllFragmentsSolid(true);  return; }
        if (p2State == Phase2State.MismatchTransparent) { SetAllFragmentsSolid(false); return; }
        // その他: ランタン点灯数でフラグメント実体/透明
        SetAllFragmentsSolid(litLanternCount > 0);
    }

    private void SetAllFragmentsSolid(bool solid)
    {
        if (fragments == null) return;
        foreach (var f in fragments)
            f?.SetSolid(solid);
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
                fragment.SetGlowing(true);
                p2State = Phase2State.WaitingSecondHit;
                Destroy(bullet.gameObject);
                break;

            case Phase2State.WaitingSecondHit:
                if (fragment == firstHitFragment) return; // 同フラグメント無視

                bool match = (fragment.Attribute == firstHitFragment.Attribute);
                firstHitFragment.SetGlowing(false);
                fragment.SetGlowing(false);
                Destroy(bullet.gameObject);

                p2State = match ? Phase2State.MatchSolid : Phase2State.MismatchTransparent;
                UpdatePhase2SolidState();
                break;

            case Phase2State.MatchSolid:
                // ダメージ適用
                ApplyDamageToSharedHP(fragment, bullet);
                Destroy(bullet.gameObject);
                break;
        }
    }

    private void ApplyDamageToSharedHP(HalloweenFragment fragment, EnemyBullet bullet)
    {
        if (enemyStats == null) return;

        bullet.RegisterEnemyHitAsBounce();

        float mul = Mathf.Max(1f, bullet.DamageMultiplier);
        bool isPowered = mul > 1.0001f;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(bullet.DamageValue * mul));

        enemyStats.Damage(dmg, isPowered);

        if (enemyStats.HP > 0)
            hitFeedback?.PlayHitFeedback(dmg, isPowered, fragment.transform.position);
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
    // 射撃
    // =========================================================
    private void FireAtPlayer()
    {
        if (bulletPrefab == null || projectileRoot == null || player == null) return;

        if (!isPhase2)
        {
            // Phase1: ボス本体位置から射撃
            SpawnBulletAt(transform.position);
        }
        else
        {
            // Phase2: 各フラグメントのMuzzleから射撃
            if (fragments != null)
            {
                foreach (var f in fragments)
                {
                    if (f != null)
                        SpawnBulletAt(f.GetMuzzle().position);
                }
            }
        }
    }

    private void SpawnBulletAt(Vector3 pos)
    {
        if (player == null) return;
        Vector2 dir = ((Vector2)(player.transform.position - pos)).normalized;

        EnemyBullet bullet = Instantiate(bulletPrefab, pos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);
        bullet.ApplyBullet(bulletSpeed, bulletLifeTime);

        Collider2D[] cols = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in cols)
        {
            if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
        }

        if (fireSE != null)
        {
            float vol = fireSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioSource.PlayClipAtPoint(fireSE, pos, vol);
        }
    }
}
