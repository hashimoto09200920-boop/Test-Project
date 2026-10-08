using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Area1ボス「GravePole」専用の特殊攻撃（GravePole専用。他エネミーと共有しない）。
/// ① 四つ目の連続射撃：上の目から下の目へ順番に光り、光った目から1発ずつプレイヤーを狙って撃つ（後半は往復）
/// ② 凝視：4つの目がプレイヤーを見つめ、照準マークがプレイヤーを追う → 照準が止まって点滅 → 4つの目から照準の位置へ弾を集めるように撃つ
/// ③ 瞬き→解放：4つの目を閉じる（瞳を隠す）→ 目を開いた瞬間から、全方向へ1発ずつ回しながら撃つ（ランダムな位置から時計回り/反時計回り）
/// 前半（GravePoleEnemyのPhase1）は①のみ、後半は①②③からランダム。特殊攻撃の間は本体のEnemyShooterが有効なら止める（GravePoleは元々無効＝本体は通常の弾を撃たない）
/// （アタックブロック・Drone・オーブはGravePoleEnemyが今までどおり動かす）。
/// 弾の設定（速さ・色・SE等）はEnemyData_GravePoleのBullet Types「EyeShot」「GazeShot」「ReleaseRing」で調整する。
/// 配置はメニュー「Tools/GravePole/特殊攻撃（四つ目・凝視・瞬き）を追加」で行う。
/// </summary>
[DisallowMultipleComponent]
public class GravePoleSpecialAttack : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [Tooltip("4つの目（上から順：Eye_Top / Eye_Upper / Eye_Lower / Eye_Bottom）")]
    [SerializeField] private Transform[] eyes;

    [Header("発動間隔（秒・スローモーションに追従）")]
    [SerializeField] private Vector2 firstDelay = new Vector2(5f, 8f);
    [Tooltip("前半の間隔（最小/最大）")]
    [SerializeField] private Vector2 phase1Interval = new Vector2(8f, 12f);
    [Tooltip("後半の間隔（最小/最大）")]
    [SerializeField] private Vector2 phase2Interval = new Vector2(6f, 9f);
    [Tooltip("後半の抽選の重み（①四つ目 / ②凝視 / ③瞬き）")]
    [SerializeField] private Vector3 phase2Weights = new Vector3(1f, 1f, 1f);
    [Tooltip("特殊攻撃が終わってから通常の弾を再開するまでの秒数")]
    [SerializeField] private float recoverSeconds = 0.5f;

    [Header("① 四つ目の連続射撃")]
    [Tooltip("EnemyData_GravePoleのBullet Types「EyeShot」の番号（メニューで自動設定）")]
    [SerializeField] private int eyeShotBulletTypeIndex = -1;
    [Tooltip("目が光ってから撃つまでの秒数")]
    [SerializeField] private float eyeFlashSeconds = 0.25f;
    [Tooltip("次の目へ移るまでの秒数（撃ってから次の目が光り始めるまで）")]
    [SerializeField] private float eyeChainInterval = 0.1f;
    [Tooltip("光っている目の色")]
    [SerializeField] private Color eyeFlashColor = new Color(1f, 0.95f, 0.6f, 1f);
    [Tooltip("光っている目の大きさ（通常の何倍か）")]
    [SerializeField] private float eyeFlashScale = 1.4f;

    [Header("② 凝視")]
    [Tooltip("EnemyData_GravePoleのBullet Types「GazeShot」の番号（メニューで自動設定）")]
    [SerializeField] private int gazeShotBulletTypeIndex = -1;
    [Tooltip("照準マークがプレイヤーを追いかける秒数")]
    [SerializeField] private float gazeTrackSeconds = 1.5f;
    [Tooltip("照準が止まって点滅してから撃つまでの秒数")]
    [SerializeField] private float gazeLockSeconds = 0.5f;
    [Tooltip("4つの目から照準へ撃つ回数（1回＝4つの目から1発ずつ）")]
    [Min(1)] [SerializeField] private int gazeVolleys = 2;
    [Tooltip("撃つ回数の間隔（秒）")]
    [SerializeField] private float gazeVolleyInterval = 0.2f;
    [Tooltip("凝視中、瞳をプレイヤー側へ寄せる距離（目のローカル単位）")]
    [SerializeField] private float gazeLookOffset = 0.12f;
    [Tooltip("照準マークの色・半径（ワールド単位）・線の太さ")]
    [SerializeField] private Color reticleColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private float reticleRadius = 0.45f;
    [SerializeField] private float reticleWidth = 0.05f;
    [Tooltip("照準が止まった後の点滅の回数/秒")]
    [SerializeField] private float reticleBlinkFrequency = 8f;

    [Header("③ 瞬き→解放")]
    [Tooltip("EnemyData_GravePoleのBullet Types「ReleaseRing」の番号（メニューで自動設定）")]
    [SerializeField] private int releaseBulletTypeIndex = -1;
    [Tooltip("目を閉じている秒数")]
    [SerializeField] private float blinkClosedSeconds = 1.5f;
    [Tooltip("全方向へ撃つ弾の数（1周）")]
    [Min(1)] [SerializeField] private int releaseBulletCount = 12;
    [Tooltip("1発ずつ撃つ間隔（秒）")]
    [SerializeField] private float releaseShotInterval = 0.2f;
    [Tooltip("目を閉じる瞬間・開く瞬間のSE（任意）")]
    [SerializeField] private AudioClip blinkCloseSE;
    [SerializeField] private AudioClip blinkOpenSE;
    [Range(0f, 1f)] [SerializeField] private float blinkSEVolume = 1f;

    [Header("その他")]
    [Tooltip("発射した弾が自分（GravePole）の当たり判定を無視する秒数")]
    [SerializeField] private float ignoreOwnerTime = 0.15f;

    private EnemyShooter shooter;
    private EnemyStats stats;
    private GravePoleEnemy pole;
    private EnemyData data;
    private EnemyBullet bulletPrefab;
    private Transform projectileRoot;
    private Collider2D[] ownerColliders = new Collider2D[0];

    private SpriteRenderer[] eyeRenderers;
    private GravePoleEye[] eyeTrackers;
    private Vector3[] eyeBaseLocalPos;
    private Vector3[] eyeBaseLocalScale;

    private float timer;
    private bool attacking;
    private bool dead;
    private bool shooterDisabledByMe;
    private GameObject reticle;

    private static FieldInfo s_phaseField;
    private static Material s_lineMat;

    private static float TimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
    private static bool IsGameOver => FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal || PixelDancerController.IsDownGlobal;

    private void Awake()
    {
        shooter = GetComponent<EnemyShooter>();
        stats = GetComponent<EnemyStats>();
        pole = GetComponent<GravePoleEnemy>();
        if (stats != null) stats.onKilled += HandleKilled;
        if (s_phaseField == null)
            s_phaseField = typeof(GravePoleEnemy).GetField("currentPhase", BindingFlags.Instance | BindingFlags.NonPublic);

        int n = eyes != null ? eyes.Length : 0;
        eyeRenderers = new SpriteRenderer[n];
        eyeTrackers = new GravePoleEye[n];
        eyeBaseLocalPos = new Vector3[n];
        eyeBaseLocalScale = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            if (eyes[i] == null) continue;
            eyeRenderers[i] = eyes[i].GetComponent<SpriteRenderer>();
            eyeTrackers[i] = eyes[i].GetComponent<GravePoleEye>();
            eyeBaseLocalPos[i] = eyes[i].localPosition;
            eyeBaseLocalScale[i] = eyes[i].localScale;
        }
    }

    private void OnDestroy()
    {
        if (stats != null) stats.onKilled -= HandleKilled;
        if (reticle != null) Destroy(reticle);
    }

    private void Start()
    {
        ownerColliders = GetComponentsInChildren<Collider2D>(true);
        timer = RandomRange(firstDelay);
    }

    private bool EnsureRefs()
    {
        if (shooter == null) return false;
        if (data == null) data = shooter.GetEnemyData();
        if (bulletPrefab == null) bulletPrefab = shooter.GetBulletPrefab();
        if (projectileRoot == null) projectileRoot = shooter.GetProjectileRoot();
        return data != null && bulletPrefab != null && projectileRoot != null;
    }

    // GravePoleEnemyのフェーズ（Phase1/2）に合わせる。読めない場合はHP50%で判定
    private bool IsPhase2()
    {
        if (pole != null && s_phaseField != null) return (int)s_phaseField.GetValue(pole) >= 2;
        return stats != null && stats.GetHpPercentage() < 50f;
    }

    private void Update()
    {
        if (dead || attacking) return;
        // ★GravePoleは本体のEnemyShooterが最初から無効（本体は通常の弾を撃たず、アタックブロックが弾を撃つ仕様）のため、
        //   EnemyShooterの有効/無効では判定しない。登場直後は最初の待ち（First Delay）で撃たないようにしている
        if (IsGameOver) return;

        timer -= Time.deltaTime * TimeScale;
        if (timer > 0f) return;
        if (!EnsureRefs()) { timer = 1f; return; }

        StartCoroutine(AttackRoutine(PickAttack()));
    }

    private int PickAttack()
    {
        if (!IsPhase2()) return 0;
        float a = Mathf.Max(0f, phase2Weights.x), b = Mathf.Max(0f, phase2Weights.y), c = Mathf.Max(0f, phase2Weights.z);
        float sum = a + b + c;
        if (sum <= 0f) return 0;
        float r = Random.Range(0f, sum);
        if (r < a) return 0;
        if (r < a + b) return 1;
        return 2;
    }

    private IEnumerator AttackRoutine(int kind)
    {
        attacking = true;
        if (shooter != null && shooter.enabled)
        {
            shooter.enabled = false; // 通常の弾を止める
            shooterDisabledByMe = true;
        }

        if (kind == 0) yield return EyeChainRoutine();
        else if (kind == 1) yield return GazeRoutine();
        else yield return BlinkReleaseRoutine();

        yield return WaitScaled(recoverSeconds);
        RestoreShooter();
        timer = IsPhase2() ? RandomRange(phase2Interval) : RandomRange(phase1Interval);
        attacking = false;
    }

    // ---------- ① 四つ目の連続射撃 ----------
    private IEnumerator EyeChainRoutine()
    {
        EnemyData.BulletType bt = GetBulletType(eyeShotBulletTypeIndex);
        if (bt == null || eyes == null || eyes.Length == 0) yield break;

        var order = new List<int>();
        for (int i = 0; i < eyes.Length; i++) order.Add(i);
        if (IsPhase2()) for (int i = eyes.Length - 2; i >= 0; i--) order.Add(i); // 後半は往復（上→下→上）

        foreach (int i in order)
        {
            if (dead || IsGameOver) yield break;
            if (eyes[i] == null) continue;
            SetEyeFlash(i, true);
            yield return WaitScaled(eyeFlashSeconds);
            SetEyeFlash(i, false);
            if (dead || IsGameOver) yield break;
            Vector3 pos = eyes[i].position;
            SpawnBullet(bt, pos, DirToPlayer(pos), true);
            yield return WaitScaled(eyeChainInterval);
        }
    }

    private void SetEyeFlash(int i, bool on)
    {
        var sr = eyeRenderers[i];
        if (eyes[i] == null) return;
        eyes[i].localScale = on ? eyeBaseLocalScale[i] * eyeFlashScale : eyeBaseLocalScale[i];
        if (sr == null) return;
        if (on)
        {
            flashSavedColor[i] = sr.color;
            sr.color = eyeFlashColor;
        }
        else sr.color = flashSavedColor[i];
    }
    private readonly Dictionary<int, Color> flashSavedColor = new Dictionary<int, Color>();

    // ---------- ② 凝視 ----------
    private IEnumerator GazeRoutine()
    {
        EnemyData.BulletType bt = GetBulletType(gazeShotBulletTypeIndex);
        Transform player = FindPlayer();
        if (bt == null || player == null) yield break;

        SetEyeTracking(false);
        reticle = CreateReticle();
        Vector3 target = player.position;

        // 照準がプレイヤーを追いかける（瞳はプレイヤーを見つめる）
        float t = 0f;
        while (t < gazeTrackSeconds)
        {
            if (dead || IsGameOver) { EndGaze(); yield break; }
            if (player != null) target = player.position;
            UpdateGaze(target, 1f);
            t += Time.deltaTime * TimeScale;
            yield return null;
        }

        // 照準が止まって点滅
        t = 0f;
        while (t < gazeLockSeconds)
        {
            if (dead || IsGameOver) { EndGaze(); yield break; }
            float blink = (Mathf.Repeat(t * reticleBlinkFrequency, 1f) < 0.5f) ? 1f : 0.25f;
            UpdateGaze(target, blink);
            t += Time.deltaTime * TimeScale;
            yield return null;
        }

        // 4つの目から照準の位置へ集めるように撃つ
        for (int v = 0; v < Mathf.Max(1, gazeVolleys); v++)
        {
            if (dead || IsGameOver) break;
            bool se = true;
            for (int i = 0; i < eyes.Length; i++)
            {
                if (eyes[i] == null) continue;
                Vector3 pos = eyes[i].position;
                Vector2 d = (Vector2)(target - pos);
                SpawnBullet(bt, pos, d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down, se);
                se = false;
            }
            if (v < gazeVolleys - 1) yield return WaitScaled(gazeVolleyInterval);
        }
        EndGaze();
    }

    private void UpdateGaze(Vector3 target, float reticleAlpha)
    {
        // 瞳をプレイヤー（照準）側へ寄せる
        for (int i = 0; i < eyes.Length; i++)
        {
            if (eyes[i] == null || eyes[i].parent == null) continue;
            Vector3 baseWorld = eyes[i].parent.TransformPoint(eyeBaseLocalPos[i]);
            Vector3 localDir = eyes[i].parent.InverseTransformDirection((target - baseWorld).normalized);
            localDir.z = 0f;
            eyes[i].localPosition = eyeBaseLocalPos[i] + (localDir.sqrMagnitude > 0.0001f ? localDir.normalized * gazeLookOffset : Vector3.zero);
        }
        if (reticle != null)
        {
            reticle.transform.position = new Vector3(target.x, target.y, 0f);
            Color c = reticleColor; c.a *= reticleAlpha;
            foreach (var lr in reticle.GetComponentsInChildren<LineRenderer>()) { lr.startColor = c; lr.endColor = c; }
        }
    }

    private void EndGaze()
    {
        if (reticle != null) { Destroy(reticle); reticle = null; }
        SetEyeTracking(true);
    }

    private void SetEyeTracking(bool on)
    {
        for (int i = 0; i < eyeTrackers.Length; i++)
            if (eyeTrackers[i] != null) eyeTrackers[i].enabled = on; // 再開すると元の位置付近へ自然に戻る
    }

    // 照準マーク：円＋十字（LineRenderer）
    private GameObject CreateReticle()
    {
        if (s_lineMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) s_lineMat = new Material(sh);
        }
        var root = new GameObject("GravePole_GazeReticle");
        const int seg = 32;
        var ring = NewLine(root.transform, "Ring", seg + 1);
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * reticleRadius);
        }
        var h = NewLine(root.transform, "H", 2);
        h.SetPosition(0, new Vector3(-reticleRadius * 1.4f, 0f, 0f));
        h.SetPosition(1, new Vector3(reticleRadius * 1.4f, 0f, 0f));
        var v = NewLine(root.transform, "V", 2);
        v.SetPosition(0, new Vector3(0f, -reticleRadius * 1.4f, 0f));
        v.SetPosition(1, new Vector3(0f, reticleRadius * 1.4f, 0f));
        return root;
    }

    private LineRenderer NewLine(Transform parent, string name, int count)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        if (s_lineMat != null) lr.sharedMaterial = s_lineMat;
        lr.useWorldSpace = false;
        lr.positionCount = count;
        lr.startWidth = reticleWidth;
        lr.endWidth = reticleWidth;
        lr.sortingLayerName = "Default";
        lr.sortingOrder = 1100;
        lr.startColor = reticleColor;
        lr.endColor = reticleColor;
        return lr;
    }

    // ---------- ③ 瞬き→解放 ----------
    private IEnumerator BlinkReleaseRoutine()
    {
        EnemyData.BulletType bt = GetBulletType(releaseBulletTypeIndex);
        if (bt == null) yield break;

        // 目を閉じる（瞳を隠す。塔の目の部分は黒い穴になる）
        SetPupilsVisible(false);
        PlaySE(blinkCloseSE, transform.position);
        yield return WaitScaled(blinkClosedSeconds);
        SetPupilsVisible(true);
        if (dead || IsGameOver) yield break;
        PlaySE(blinkOpenSE, transform.position);

        // 全方向へ1発ずつ回しながら撃つ（ランダムな位置から時計回り/反時計回り）
        int n = Mathf.Max(1, releaseBulletCount);
        int start = Random.Range(0, n);
        int step = Random.value < 0.5f ? 1 : -1;
        for (int j = 0; j < n; j++)
        {
            if (dead || IsGameOver) yield break;
            int i = ((start + step * j) % n + n) % n;
            float angle = (360f / n) * i * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SpawnBullet(bt, transform.position, dir, j == 0); // 発射位置は塔の中心（移動に追従）
            if (j < n - 1) yield return WaitScaled(releaseShotInterval);
        }
    }

    private void SetPupilsVisible(bool visible)
    {
        if (eyeRenderers == null) return;
        foreach (var sr in eyeRenderers) if (sr != null) sr.enabled = visible;
    }

    // ---------- 共通 ----------
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
        shooterDisabledByMe = false; // 撃破時は通常の弾を再開しない
        attacking = false;
        if (reticle != null) { Destroy(reticle); reticle = null; }
        SetPupilsVisible(true);
        for (int i = 0; eyes != null && i < eyes.Length; i++)
            if (eyes[i] != null) eyes[i].localScale = eyeBaseLocalScale[i];
    }

    private void SpawnBullet(EnemyData.BulletType bt, Vector3 pos, Vector2 dir, bool playSE)
    {
        if (IsGameOver || dead || bulletPrefab == null) return;
        EnemyBullet bullet = EnemyBulletPool.Get(bulletPrefab, pos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);
        EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, data.bulletSpeed, data.bulletLifeTime, data.bulletSpriteOverride, bulletPrefab, projectileRoot);
        foreach (Collider2D col in ownerColliders)
            if (col != null) bullet.SetOwnerCollisionIgnore(col, ignoreOwnerTime);
        if (playSE) PlayFireSE(bt, pos);
    }

    private void PlayFireSE(EnemyData.BulletType bt, Vector3 pos)
    {
        AudioClip se; float vol;
        if (bt.fireSEOverride != null) { se = bt.fireSEOverride; vol = bt.fireSEOverrideVolume; }
        else { se = data != null ? data.fireSE : null; vol = data != null ? data.fireSEVolume : 1f; }
        if (se != null && vol > 0f && SeSimultaneousGuard.TryAllow("GravePoleSpecialAttack_FireSE"))
            AudioOneShotPool.Play(se, vol * MasterSEVolume, pos, null, 0.1f);
    }

    private void PlaySE(AudioClip clip, Vector3 pos)
    {
        if (clip != null && blinkSEVolume > 0f)
            AudioOneShotPool.Play(clip, blinkSEVolume * MasterSEVolume, pos, null, 0.1f);
    }

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (data == null || data.bulletTypes == null || index < 0 || index >= data.bulletTypes.Length)
        {
            Debug.LogWarning($"[GravePoleSpecialAttack] Bullet Type index {index} がEnemyDataにありません（メニュー「Tools/GravePole/特殊攻撃…を追加」を実行してください）", this);
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

    private Vector2 DirToPlayer(Vector3 from)
    {
        Transform p = FindPlayer();
        if (p == null) return Vector2.down;
        Vector2 d = (Vector2)(p.position - from);
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down;
    }

    private static float RandomRange(Vector2 r) => Random.Range(Mathf.Min(r.x, r.y), Mathf.Max(r.x, r.y));
}
