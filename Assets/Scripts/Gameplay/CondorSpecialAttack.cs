using System.Collections;
using UnityEngine;

/// <summary>
/// Area2ボス「Condor」専用の特殊攻撃（Condor専用。他エネミーと共有しない）。
/// ・羽根の一斉射撃：翼を速く羽ばたかせて溜め → 左右の翼から羽根の弾を扇状に、外側から1発ずつ（左右同時に）時間をずらして発射
/// ・雷の羽ばたき：同じく溜め → 左右の翼からジグザグに折れ曲がる雷の弾を発射（反射されたら直進。CondorZigzagBullet）
/// 前半（HPがEnemyDataのHp Threshold Percentageより上）は羽根の一斉射撃のみ、後半は2つからランダム。
/// 特殊攻撃の溜め〜発射〜少しの間は、通常の弾（EnemyShooter）を止める。
/// 弾の設定（速さ・色・大きさ・SE等）はEnemyData_CondorのBullet Types「Feather」「Thunder」で調整する。
/// 配置はメニュー「Tools/Condor/特殊攻撃（羽根の一斉射撃・雷の羽ばたき）を追加」で行う。
/// </summary>
[DisallowMultipleComponent]
public class CondorSpecialAttack : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [SerializeField] private Transform wingRight;
    [SerializeField] private Transform wingLeft;
    [Tooltip("溜め中に翼を速く羽ばたかせるため（未設定なら羽ばたきの速さは変えない）")]
    [SerializeField] private CondorWingSync wingSync;

    [Header("発動間隔（秒・スローモーションに追従）")]
    [SerializeField] private float intervalMin = 8f;
    [SerializeField] private float intervalMax = 12f;
    [Tooltip("後半（HPがEnemyDataのHp Threshold Percentage以下）の発動間隔（秒・最小/最大）。前半より短くして頻度を上げる")]
    [SerializeField] private float phase2IntervalMin = 5f;
    [SerializeField] private float phase2IntervalMax = 7f;
    [Tooltip("戦闘開始から最初の特殊攻撃までの秒数（最小/最大）")]
    [SerializeField] private Vector2 firstDelay = new Vector2(5f, 8f);
    [Tooltip("後半（HPがEnemyDataのHp Threshold Percentage以下）で羽根の一斉射撃を選ぶ確率（%）。残りは雷の羽ばたき")]
    [Range(0f, 100f)] [SerializeField] private float phase2FeatherChancePercent = 15f;

    [Header("溜め")]
    [SerializeField] private float chargeSeconds = 0.8f;
    [Tooltip("溜め中の羽ばたきの速さ（通常の何倍か）")]
    [SerializeField] private float chargeWingSpeedMultiplier = 3f;
    [SerializeField] private AudioClip chargeSE;
    [Range(0f, 1f)] [SerializeField] private float chargeSEVolume = 1f;
    [Tooltip("発射後、通常の弾を再開するまでの秒数")]
    [SerializeField] private float recoverSeconds = 0.5f;

    [Header("羽根の一斉射撃")]
    [Tooltip("EnemyData_CondorのBullet Typesの「Feather」の番号（メニューで自動設定）")]
    [SerializeField] private int featherBulletTypeIndex = -1;
    [Tooltip("発射位置（各翼からの相対位置。左の翼は左右反転して使う）")]
    [SerializeField] private Vector2 featherMuzzleOffset = new Vector2(1.5f, 1.2f);
    [Tooltip("片方の翼から出す羽根の数")]
    [Min(1)] [SerializeField] private int featherCountPerWing = 5;
    [Tooltip("片方の翼の扇の広がり（度）")]
    [SerializeField] private float featherSpreadDeg = 50f;
    [Tooltip("扇の中心を外側へ傾ける角度（度）。0ならプレイヤー方向が中心")]
    [SerializeField] private float featherOutwardTiltDeg = 15f;
    [Tooltip("羽根を1発ずつ撃つ間隔（秒・スローモーションに追従）。扇の外側から内側へ順に撃つ（左右の翼は同時）")]
    [SerializeField] private float featherShotInterval = 0.1f;

    [Header("雷の羽ばたき")]
    [Tooltip("EnemyData_CondorのBullet Typesの「Thunder」の番号（メニューで自動設定）")]
    [SerializeField] private int thunderBulletTypeIndex = -1;
    [Tooltip("発射位置（各翼からの相対位置。左の翼は左右反転して使う）")]
    [SerializeField] private Vector2 thunderMuzzleOffset = new Vector2(1.5f, 1.2f);
    [Tooltip("片方の翼から出す雷の弾の数")]
    [Min(1)] [SerializeField] private int thunderCountPerWing = 3;
    [Tooltip("片方の翼の扇の広がり（度）。中心はプレイヤー方向")]
    [SerializeField] private float thunderSpreadDeg = 20f;
    [Tooltip("ジグザグの曲がる角度（度）")]
    [SerializeField] private float thunderZigzagAngleDeg = 60f;
    [Tooltip("ジグザグで1回曲がるまでに進む距離（ワールド単位）")]
    [SerializeField] private float thunderZigzagSegment = 1.4f;

    [Header("その他")]
    [Tooltip("メニュー「通常攻撃を減らして羽根・雷を中心にする」で通常の弾の間隔を2倍にしたか（再実行で4倍にならないための印）")]
    [HideInInspector] [SerializeField] private bool normalFireReducedApplied;
    [Tooltip("発射した弾が自分（Condor）の当たり判定を無視する秒数")]
    [SerializeField] private float ignoreOwnerTime = 0.15f;

    private EnemyShooter shooter;
    private EnemyStats stats;
    private EnemySpriteSwapper swapper;
    private EnemyData data;
    private EnemyBullet bulletPrefab;
    private Transform projectileRoot;
    private Collider2D[] ownerColliders = new Collider2D[0];

    private float timer;
    private bool attacking;
    private bool dead;
    private bool shooterDisabledByMe;

    private static float TimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
    private static bool IsGameOver => FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal || PixelDancerController.IsDownGlobal;

    private void Awake()
    {
        shooter = GetComponent<EnemyShooter>();
        stats = GetComponent<EnemyStats>();
        swapper = GetComponent<EnemySpriteSwapper>();
        if (stats != null) stats.onKilled += HandleKilled;
    }

    private void OnDestroy()
    {
        if (stats != null) stats.onKilled -= HandleKilled;
    }

    private void Start()
    {
        ownerColliders = GetComponentsInChildren<Collider2D>(true);
        timer = Random.Range(Mathf.Min(firstDelay.x, firstDelay.y), Mathf.Max(firstDelay.x, firstDelay.y));
    }

    // EnemyDataや弾のPrefabはスポナーが生成直後に設定するため、使う時に取得する
    private bool EnsureRefs()
    {
        if (shooter == null) return false;
        if (data == null) data = shooter.GetEnemyData();
        if (bulletPrefab == null) bulletPrefab = shooter.GetBulletPrefab();
        if (projectileRoot == null) projectileRoot = shooter.GetProjectileRoot();
        return data != null && bulletPrefab != null && projectileRoot != null;
    }

    private void Update()
    {
        if (dead || attacking) return;
        // 通常の弾を撃てる状態（EnemyShooterが有効）の時だけ数える（登場演出中などは数えない）
        if (shooter == null || !shooter.enabled) return;
        if (IsGameOver) return;

        timer -= Time.deltaTime * TimeScale;
        if (timer > 0f) return;
        if (!EnsureRefs()) { timer = 1f; return; }

        bool phase2 = IsPhase2();
        bool feather = !phase2 || Random.Range(0f, 100f) < phase2FeatherChancePercent;
        StartCoroutine(AttackRoutine(feather));
    }

    private IEnumerator AttackRoutine(bool feather)
    {
        attacking = true;

        // ① 通常の弾を止める
        if (shooter != null && shooter.enabled)
        {
            shooter.enabled = false;
            shooterDisabledByMe = true;
        }

        // ② 溜め：翼を速く羽ばたかせる
        if (wingSync != null) wingSync.SpeedMultiplier = chargeWingSpeedMultiplier;
        if (chargeSE != null && chargeSEVolume > 0f)
            AudioOneShotPool.Play(chargeSE, chargeSEVolume * MasterSEVolume, transform.position, null, 0.1f);
        yield return WaitScaled(chargeSeconds);
        if (wingSync != null) wingSync.SpeedMultiplier = 1f;

        // ③ 発射（待っている間にゲームオーバー/撃破されていたら撃たない）
        if (!dead && !IsGameOver)
        {
            if (swapper != null && data != null && data.attackSprite != null)
                swapper.TriggerAttack(data.attackSprite, data.attackSpriteDuration);
            if (feather) yield return FireFeathersRoutine();
            else FireThunder();
        }

        // ④ 少し待ってから通常の弾を再開
        yield return WaitScaled(recoverSeconds);
        RestoreShooter();
        timer = IsPhase2()
            ? Random.Range(Mathf.Min(phase2IntervalMin, phase2IntervalMax), Mathf.Max(phase2IntervalMin, phase2IntervalMax))
            : Random.Range(Mathf.Min(intervalMin, intervalMax), Mathf.Max(intervalMin, intervalMax));
        attacking = false;
    }

    private bool IsPhase2()
    {
        return data != null && data.useHpBasedRoutineSwitch && stats != null && stats.GetHpPercentage() <= data.hpThresholdPercentage;
    }

    private IEnumerator WaitScaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (dead) yield break;
            t += Time.deltaTime * TimeScale;
            yield return null;
        }
    }

    private void RestoreShooter()
    {
        if (shooterDisabledByMe && !dead && shooter != null) shooter.enabled = true;
        shooterDisabledByMe = false;
    }

    private void HandleKilled()
    {
        dead = true;
        StopAllCoroutines();
        if (wingSync != null) wingSync.SpeedMultiplier = 1f;
        shooterDisabledByMe = false; // 撃破時は通常の弾を再開しない
        attacking = false;
    }

    // ---------- 羽根の一斉射撃 ----------
    // 扇の向き（プレイヤー方向）は撃ち始めに決め、扇の外側から内側へ1発ずつ撃つ（左右の翼は同時）
    private IEnumerator FireFeathersRoutine()
    {
        EnemyData.BulletType bt = GetBulletType(featherBulletTypeIndex);
        if (bt == null) yield break;
        Transform player = FindPlayer();
        var wings = new System.Collections.Generic.List<(Transform wing, float outward, Vector2 center)>();
        foreach (var (wing, outward) in Wings())
        {
            Vector3 pos = wing.TransformPoint(featherMuzzleOffset);
            Vector2 center = player != null ? ((Vector2)(player.position - pos)).normalized : Vector2.down;
            if (center.sqrMagnitude < 0.0001f) center = Vector2.down;
            wings.Add((wing, outward, Rotate(center, outward * featherOutwardTiltDeg)));
        }

        int n = Mathf.Max(1, featherCountPerWing);
        for (int i = 0; i < n; i++)
        {
            if (dead || IsGameOver) yield break;
            // i=0が一番外側（右の翼は反時計回り側＝右、左の翼は時計回り側＝左）
            float a = n == 1 ? 0f : Mathf.Lerp(featherSpreadDeg * 0.5f, -featherSpreadDeg * 0.5f, i / (float)(n - 1));
            Vector3 sePos = transform.position;
            foreach (var w in wings)
            {
                if (w.wing == null) continue;
                Vector3 pos = w.wing.TransformPoint(featherMuzzleOffset); // 羽ばたき・移動に合わせて発射位置は毎回取り直す
                Vector2 dir = Rotate(w.center, w.outward * a);
                EnemyBullet b = SpawnBullet(bt, pos, dir);
                if (b != null) b.gameObject.AddComponent<CondorFeatherBullet>().Arm(b, dir); // 羽根の先を進行方向へ向ける
                sePos = pos;
            }
            PlayFireSE(bt, sePos);
            if (i < n - 1) yield return WaitScaled(featherShotInterval);
        }
    }

    // ---------- 雷の羽ばたき ----------
    private void FireThunder()
    {
        EnemyData.BulletType bt = GetBulletType(thunderBulletTypeIndex);
        if (bt == null) return;
        Transform player = FindPlayer();
        bool sePlayed = false;
        foreach (var (wing, _) in Wings())
        {
            Vector3 pos = wing.TransformPoint(thunderMuzzleOffset);
            Vector2 center = player != null ? ((Vector2)(player.position - pos)).normalized : Vector2.down;
            if (center.sqrMagnitude < 0.0001f) center = Vector2.down;
            int n = Mathf.Max(1, thunderCountPerWing);
            for (int i = 0; i < n; i++)
            {
                float a = n == 1 ? 0f : Mathf.Lerp(-thunderSpreadDeg * 0.5f, thunderSpreadDeg * 0.5f, i / (float)(n - 1));
                Vector2 dir = Rotate(center, a);
                EnemyBullet b = SpawnBullet(bt, pos, dir);
                if (b != null) b.gameObject.AddComponent<CondorZigzagBullet>().Arm(b, dir, thunderZigzagAngleDeg, thunderZigzagSegment);
            }
            if (!sePlayed) { PlayFireSE(bt, pos); sePlayed = true; }
        }
    }

    // 右の翼は外側＝右（反時計回りに傾けると右下へ）、左の翼は外側＝左
    private System.Collections.Generic.IEnumerable<(Transform, float)> Wings()
    {
        if (wingRight != null) yield return (wingRight, 1f);
        if (wingLeft != null) yield return (wingLeft, -1f);
    }

    private EnemyBullet SpawnBullet(EnemyData.BulletType bt, Vector3 pos, Vector2 dir)
    {
        if (IsGameOver || dead || bulletPrefab == null) return null;
        EnemyBullet bullet = EnemyBulletPool.Get(bulletPrefab, pos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);
        EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, data.bulletSpeed, data.bulletLifeTime, data.bulletSpriteOverride, bulletPrefab, projectileRoot);
        foreach (Collider2D col in ownerColliders)
            if (col != null) bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);
        return bullet;
    }

    private void PlayFireSE(EnemyData.BulletType bt, Vector3 pos)
    {
        AudioClip se; float vol;
        if (bt.fireSEOverride != null) { se = bt.fireSEOverride; vol = bt.fireSEOverrideVolume; }
        else { se = data != null ? data.fireSE : null; vol = data != null ? data.fireSEVolume : 1f; }
        if (se != null && vol > 0f && SeSimultaneousGuard.TryAllow("CondorSpecialAttack_FireSE"))
            AudioOneShotPool.Play(se, vol * MasterSEVolume, pos, null, 0.1f);
    }

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (data == null || data.bulletTypes == null || index < 0 || index >= data.bulletTypes.Length)
        {
            Debug.LogWarning($"[CondorSpecialAttack] Bullet Type index {index} がEnemyDataにありません（メニュー「Tools/Condor/特殊攻撃…を追加」を実行してください）", this);
            return null;
        }
        return data.bulletTypes[index];
    }

    private Transform cachedPlayer;
    private Transform FindPlayer()
    {
        if (cachedPlayer == null || !cachedPlayer.gameObject.activeInHierarchy)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            cachedPlayer = go != null ? go.transform : null;
        }
        return cachedPlayer;
    }

    private static Vector2 Rotate(Vector2 v, float deg) => (Vector2)(Quaternion.Euler(0f, 0f, deg) * v);

#if UNITY_EDITOR
    // Play前のSceneビューで発射位置と扇の向き（下向き基準の目安）を表示
    private void OnDrawGizmosSelected()
    {
        foreach (var (wing, outward) in Wings())
        {
            Vector3 fp = wing.TransformPoint(featherMuzzleOffset);
            Gizmos.color = new Color(0.8f, 0.9f, 1f, 1f);
            Gizmos.DrawWireSphere(fp, 0.2f);
            Vector2 c = Rotate(Vector2.down, outward * featherOutwardTiltDeg);
            int n = Mathf.Max(1, featherCountPerWing);
            for (int i = 0; i < n; i++)
            {
                float a = n == 1 ? 0f : Mathf.Lerp(-featherSpreadDeg * 0.5f, featherSpreadDeg * 0.5f, i / (float)(n - 1));
                Gizmos.DrawLine(fp, fp + (Vector3)(Rotate(c, a) * 1.5f));
            }
            Vector3 tp = wing.TransformPoint(thunderMuzzleOffset);
            Gizmos.color = new Color(0.3f, 0.95f, 1f, 1f);
            Gizmos.DrawWireCube(tp, Vector3.one * 0.3f);
        }
    }
#endif
}
