using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Area3ボス「IronNest」のNM01/02/03に付ける、後半フェーズ（本体のHPがHp Threshold Percentage未満）専用の撃ち方（IronNest専用）。
/// 後半はIronNestNMが、ハッチから出ている間、通常のEnemyShooterの代わりにこの撃ち方を繰り返す。
/// ・SweepRapid（NM01）：プレイヤー方向を中心に±角度を「右→左→右」または「左→右→左」に旋回しながら、一定間隔でRapidを撃つ（NM01の絵も少し傾く）
/// ・Telegraph3Way（NM02）：プレイヤー方向を中心に3本の予兆線を同時に出し、発射は1発ずつずらす
/// ・Countdown3WayCurve（NM03）：プレイヤーの位置へ3発を撃つ。真ん中はまっすぐ、左右は大きく弧を描いて同じ位置へ集まる
/// 弾の設定（速さ・色・SE・予兆線の見た目・爆発等）は、各NMのEnemyData（EnemyData_IronNest_NM01〜03）のBullet Typesをそのまま使う。
/// 配置はメニュー「Tools/IronNest/後半のNMの撃ち方を設定」で行う。
/// </summary>
[DisallowMultipleComponent]
public class IronNestNMPhase2Attack : MonoBehaviour
{
    public enum Kind { SweepRapid, Telegraph3Way, Countdown3WayCurve }

    [Header("共通（メニューで自動設定）")]
    [SerializeField] private Kind kind = Kind.SweepRapid;
    [Tooltip("このNMのEnemyDataのBullet Typesの番号（NM01＝Rapid／NM02＝Telegraph／NM03＝Countdown）")]
    [SerializeField] private int bulletTypeIndex = -1;
    [Tooltip("発射位置（NMの子のMuzzle）")]
    [SerializeField] private Transform muzzle;
    [Tooltip("ハッチから出ている間、撃ち終わってから次に撃つまでの秒数")]
    [SerializeField] private float repeatInterval = 1f;
    [Tooltip("出てから最初に撃つまでの秒数")]
    [SerializeField] private float firstShotDelay = 0.3f;

    [Header("SweepRapid（NM01）")]
    [Tooltip("プレイヤー方向を中心に左右へ振る角度（度）")]
    [SerializeField] private float sweepHalfAngle = 45f;
    [Tooltip("片側から反対側まで振る秒数（右→左 1回分）")]
    [SerializeField] private float sweepLegSeconds = 1f;
    [Tooltip("撃つ間隔（秒）")]
    [SerializeField] private float sweepShotInterval = 0.1f;
    [Tooltip("撃つ向きに合わせてNMの絵を傾ける最大角度（度）")]
    [SerializeField] private float sweepMaxTilt = 20f;

    [Header("Telegraph3Way（NM02）")]
    [Tooltip("3本の間の角度（度）")]
    [SerializeField] private float telegraphSpreadDeg = 20f;
    [Tooltip("予兆線の後、1発ずつ撃つ間隔（秒）")]
    [SerializeField] private float telegraphShotStagger = 0.2f;

    [Header("Countdown3WayCurve（NM03）")]
    [Tooltip("左右の弾の弧の大きさ（発射位置から着弾点までの距離に対する横への膨らみの割合）")]
    [SerializeField] private float curveBendRatio = 0.6f;
    [Tooltip("1発ずつ撃つ間隔（秒）。0なら3発同時")]
    [SerializeField] private float countdownShotStagger = 0.2f;
    [Tooltip("ON：爆発する瞬間に着弾点へ着くように弾の速さを合わせる（3発とも同じ着弾点で爆発する）。OFF：弾の速さはEnemyDataのまま")]
    [SerializeField] private bool arriveWhenExplode = true;

    [Header("その他")]
    [Tooltip("本体のHPがこの割合（%）未満で後半。-1ならこのNMのEnemyDataのHp Threshold Percentageを使う")]
    [SerializeField] private float phase2HpThresholdOverride = -1f;
    [SerializeField] private float ignoreOwnerTime = 0.15f;

    private EnemyShooter shooter;
    private EnemyStats bossStats;
    private EnemyData data;
    private EnemyBullet bulletPrefab;
    private Transform projectileRoot;
    private Collider2D[] ownerColliders = new Collider2D[0];
    private bool phase2Latched;
    private Quaternion baseLocalRotation;
    private static Material s_lineMat;

    private static float TimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
    private static bool IsGameOver => FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal || PixelDancerController.IsDownGlobal;
    private bool BossDead => bossStats == null || bossStats.HP <= 0;

    private void Awake()
    {
        shooter = GetComponent<EnemyShooter>();
        bossStats = GetComponentInParent<EnemyStats>();
        baseLocalRotation = transform.localRotation;
    }

    private void Start()
    {
        Transform root = bossStats != null ? bossStats.transform : transform.root;
        ownerColliders = root.GetComponentsInChildren<Collider2D>(true);
    }

    /// <summary>後半フェーズか（一度後半になったら前半には戻らない）</summary>
    public bool IsPhase2
    {
        get
        {
            if (phase2Latched) return true;
            if (bossStats == null) return false;
            if (data == null && shooter != null) data = shooter.GetEnemyData();
            float threshold = phase2HpThresholdOverride >= 0f ? phase2HpThresholdOverride : (data != null ? data.hpThresholdPercentage : 50f);
            if (bossStats.GetHpPercentage() < threshold) phase2Latched = true;
            return phase2Latched;
        }
    }

    private bool EnsureRefs()
    {
        if (shooter == null) return false;
        if (data == null) data = shooter.GetEnemyData();
        if (bulletPrefab == null) bulletPrefab = shooter.GetBulletPrefab();
        if (projectileRoot == null) projectileRoot = shooter.GetProjectileRoot();
        return data != null && bulletPrefab != null && projectileRoot != null;
    }

    /// <summary>IronNestNMがハッチから出ている間（visibleSeconds）呼ぶ。撃つ→待つを繰り返す（撃っている途中では止めない）</summary>
    public IEnumerator RunWhileVisible(float visibleSeconds)
    {
        float elapsed = 0f;
        yield return WaitScaled(firstShotDelay, v => elapsed += v);
        while (elapsed < visibleSeconds)
        {
            if (BossDead || IsGameOver || !EnsureRefs()) break;
            float start = Time.time;
            switch (kind)
            {
                case Kind.SweepRapid: yield return SweepRapidRoutine(); break;
                case Kind.Telegraph3Way: yield return Telegraph3WayRoutine(); break;
                default: yield return Countdown3WayCurveRoutine(); break;
            }
            elapsed += (Time.time - start) * TimeScale;
            if (elapsed >= visibleSeconds) break;
            yield return WaitScaled(repeatInterval, v => elapsed += v);
        }
        transform.localRotation = baseLocalRotation;
    }

    // ---------- NM01：掃射 ----------
    private IEnumerator SweepRapidRoutine()
    {
        EnemyData.BulletType bt = GetBulletType();
        if (bt == null) yield break;
        Vector2 center = DirToPlayer(MuzzlePos());
        float sign = Random.value < 0.5f ? 1f : -1f; // 右→左→右 か 左→右→左
        float total = Mathf.Max(0.05f, sweepLegSeconds) * 2f;
        float t = 0f, shotTimer = 0f;
        bool first = true;
        while (t <= total)
        {
            if (BossDead || IsGameOver) break;
            // 0→1→0 の往復で、+角度 → -角度 → +角度
            float k = t / total;
            float tri = k < 0.5f ? k * 2f : (1f - k) * 2f;      // 0→1→0
            float angle = sign * sweepHalfAngle * (1f - 2f * tri); // +h → -h → +h
            Vector2 dir = Rotate(center, angle);
            ApplyTilt(dir);
            shotTimer -= first ? 0f : Time.deltaTime * TimeScale;
            if (first || shotTimer <= 0f)
            {
                SpawnBullet(bt, MuzzlePos(), dir, true);
                shotTimer += Mathf.Max(0.02f, sweepShotInterval);
                first = false;
            }
            t += Time.deltaTime * TimeScale;
            yield return null;
        }
        transform.localRotation = baseLocalRotation;
    }

    // 撃つ向き（真下からの角度）に合わせてNMの絵を傾ける
    private void ApplyTilt(Vector2 dir)
    {
        float fromDown = Vector2.SignedAngle(Vector2.down, dir);
        float tilt = Mathf.Clamp(fromDown * (sweepMaxTilt / Mathf.Max(1f, sweepHalfAngle)), -sweepMaxTilt, sweepMaxTilt);
        transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, tilt);
    }

    // ---------- NM02：3way予兆線 ----------
    private IEnumerator Telegraph3WayRoutine()
    {
        EnemyData.BulletType bt = GetBulletType();
        if (bt == null) yield break;
        Vector2 center = DirToPlayer(MuzzlePos());
        Vector2[] dirs = { Rotate(center, telegraphSpreadDeg), center, Rotate(center, -telegraphSpreadDeg) };

        var lines = new List<LineRenderer>();
        foreach (var d in dirs) lines.Add(CreateLine(bt));
        float seconds = Mathf.Max(0.01f, bt.telegraphSeconds);
        foreach (var l in lines) TelegraphFXManager.Track(l, seconds); // 予告線の飾り
        float t = 0f;
        try
        {
            while (t < seconds)
            {
                if (BossDead || IsGameOver) yield break;
                float a = bt.telegraphColor.a;
                float k = Mathf.Clamp01(t / seconds);
                if (bt.telegraphUseBlink && bt.telegraphBlinkCount > 0)
                {
                    int segments = bt.telegraphBlinkCount * 2;
                    int seg = Mathf.Min(segments - 1, Mathf.FloorToInt(k * segments));
                    a *= (seg % 2 == 1) ? Mathf.Clamp01(bt.telegraphBlinkMinAlphaMul) : 1f;
                }
                else if (bt.telegraphFadeOut) a = Mathf.Lerp(a, 0f, k);
                Vector3 p0 = MuzzlePos(); // 戦車の移動に追従
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i] == null) continue;
                    Color c = bt.telegraphColor; c.a = a;
                    lines[i].startColor = c; lines[i].endColor = c;
                    lines[i].SetPosition(0, p0);
                    lines[i].SetPosition(1, p0 + (Vector3)(dirs[i] * Mathf.Max(0.1f, bt.telegraphLength)));
                    TelegraphFXManager.Progress(lines[i], k);
                }
                t += bt.telegraphUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime * TimeScale;
                yield return null;
            }
        }
        finally
        {
            // 最後まで予告した（＝この後撃つ）時は弾ける光を出して返す
            foreach (var l in lines) if (l != null) TelegraphFXManager.ReleaseLine(l.gameObject, t >= seconds && !BossDead && !IsGameOver);
        }

        // 1発ずつずらして撃つ（片端→中央→もう片端の順。向きは予兆線のまま）
        for (int i = 0; i < dirs.Length; i++)
        {
            if (BossDead || IsGameOver) yield break;
            SpawnBullet(bt, MuzzlePos(), dirs[i], i == 0);
            if (i < dirs.Length - 1) yield return WaitScaled(telegraphShotStagger, null);
        }
    }

    private LineRenderer CreateLine(EnemyData.BulletType bt)
    {
        if (s_lineMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) s_lineMat = new Material(sh);
        }
        // 予告線は共通の管理役から借りる（使い回し。TelegraphFXManager）
        var go = TelegraphFXManager.RentLine("IronNest_TelegraphLine", projectileRoot, out LineRenderer lr);
        if (s_lineMat != null) lr.sharedMaterial = s_lineMat;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        float w = Mathf.Max(0.001f, bt.telegraphWidth);
        lr.startWidth = w; lr.endWidth = w;
        lr.startColor = bt.telegraphColor; lr.endColor = bt.telegraphColor;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        return lr;
    }

    // ---------- NM03：3way Countdown（左右は弧を描いて同じ着弾点へ） ----------
    private IEnumerator Countdown3WayCurveRoutine()
    {
        EnemyData.BulletType bt = GetBulletType();
        Transform player = FindPlayer();
        if (bt == null || player == null) yield break;
        Vector2 target = player.position; // 撃った瞬間のプレイヤーの位置

        int[] order = { -1, 0, 1 }; // 左→中央→右
        for (int n = 0; n < order.Length; n++)
        {
            if (BossDead || IsGameOver) yield break;
            Vector2 start = MuzzlePos();
            Vector2 toTarget = target - start;
            float dist = toTarget.magnitude;
            Vector2 straight = dist > 0.0001f ? toTarget / dist : Vector2.down;
            int side = order[n];

            Vector2 control = (start + target) * 0.5f;
            if (side != 0)
            {
                Vector2 perp = new Vector2(-straight.y, straight.x) * side; // 左右へ膨らむ
                control += perp * dist * curveBendRatio;
            }
            Vector2 firstDir = side == 0 ? straight : (control - start).normalized;
            EnemyBullet b = SpawnBullet(bt, start, firstDir, n == 0);
            if (b != null)
            {
                float pathLen = side == 0 ? dist : IronNestCurveBullet.ApproxLength(start, control, target);
                if (arriveWhenExplode && bt.useCountdownExplosion && bt.explosionDelaySeconds > 0.01f)
                    b.ApplyBullet(pathLen / bt.explosionDelaySeconds, Mathf.Max(bt.lifeTime, bt.explosionDelaySeconds + 1f));
                if (side != 0) b.gameObject.AddComponent<IronNestCurveBullet>().Arm(b, start, control, target);
            }
            if (n < order.Length - 1 && countdownShotStagger > 0f) yield return WaitScaled(countdownShotStagger, null);
        }
    }

    // ---------- 共通 ----------
    private IEnumerator WaitScaled(float seconds, System.Action<float> onAdvance)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (BossDead) yield break;
            float d = Time.deltaTime * TimeScale;
            t += d;
            onAdvance?.Invoke(d);
            yield return null;
        }
    }

    private Vector3 MuzzlePos() => muzzle != null ? muzzle.position : transform.position;

    private EnemyBullet SpawnBullet(EnemyData.BulletType bt, Vector3 pos, Vector2 dir, bool playSE)
    {
        if (IsGameOver || BossDead || bulletPrefab == null) return null;
        EnemyBullet bullet = EnemyBulletPool.Get(bulletPrefab, pos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);
        EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, data.bulletSpeed, data.bulletLifeTime, data.bulletSpriteOverride, bulletPrefab, projectileRoot);
        foreach (Collider2D col in ownerColliders)
            if (col != null) bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);
        if (data.unreflectedBulletCollisionDisableTime > 0f)
            bullet.SetUnreflectedCollisionDisable(data.unreflectedBulletCollisionDisableTime);
        if (playSE) PlayFireSE(bt, pos);
        return bullet;
    }

    private void PlayFireSE(EnemyData.BulletType bt, Vector3 pos)
    {
        AudioClip se; float vol;
        if (bt.fireSEOverride != null) { se = bt.fireSEOverride; vol = bt.fireSEOverrideVolume; }
        else { se = data != null ? data.fireSE : null; vol = data != null ? data.fireSEVolume : 1f; }
        if (se != null && vol > 0f && SeSimultaneousGuard.TryAllow("IronNestNMPhase2_" + kind))
            AudioOneShotPool.Play(se, vol * MasterSEVolume, pos, null, 0.1f);
    }

    private EnemyData.BulletType GetBulletType()
    {
        if (data == null || data.bulletTypes == null || bulletTypeIndex < 0 || bulletTypeIndex >= data.bulletTypes.Length)
        {
            Debug.LogWarning($"[IronNestNMPhase2Attack] {name}: Bullet Type index {bulletTypeIndex} がEnemyDataにありません（メニュー「Tools/IronNest/後半のNMの撃ち方を設定」を実行してください）", this);
            return null;
        }
        return data.bulletTypes[bulletTypeIndex];
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

    private Vector2 DirToPlayer(Vector3 from)
    {
        Transform p = FindPlayer();
        if (p == null) return Vector2.down;
        Vector2 d = (Vector2)(p.position - from);
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down;
    }

    private static Vector2 Rotate(Vector2 v, float deg) => (Vector2)(Quaternion.Euler(0f, 0f, deg) * v);

    private void OnDisable()
    {
        transform.localRotation = baseLocalRotation;
    }
}
