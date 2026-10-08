using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Area6ボス「Shaman」の特殊攻撃「精霊の炎」（Shaman専用。Shaman.prefabのルートに付ける）。
/// 流れ：火の玉の精霊を召喚（SE＝火炎魔法1）→ Shamanから少し離れた位置を中心に回り続ける（Shamanが飛ばすことはしない）。
/// ・回っている火の玉はブロックHP（WallHealth）を持ち、反射弾を防ぐ。一定量ダメージで壊れて消える（ShamanSpiritFlame）
/// ・ブロックHPが半分以下になった火の玉は色が変わり、プレイヤーが線で割り込むと反射弾に変わって飛んでいく（EnemyData_ShamanのBullet Types「SpiritFlame」）。
///   見た目は同じ炎のParticle、Trail無し・反射回数で消えない（画面外で消える）・未反射の弾と回っている火の玉を吸収して大きくなる（威力は上がらない。ShamanFlameBullet）
/// ・火の玉が全部なくなったら（壊された・線で反射された）、一定時間後にまた召喚する
/// ・Shamanのワープ中は一緒に消え（当たり判定も無し）、ワープ先でまた現れる
/// ・前半・後半とも使う
/// 配置はメニュー「Tools/Shaman/精霊の炎を追加」→「Tools/Shaman/精霊の炎をParticleに作り直す」で行う。
/// </summary>
[DisallowMultipleComponent]
public class ShamanSpiritFlames : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [Tooltip("回る火の玉のPrefab（Shaman_SpiritFlame）")]
    [SerializeField] private ShamanSpiritFlame flamePrefab;
    [Tooltip("反射して飛ぶ火の玉の見た目（炎のParticle。Shaman_FlameFX）")]
    [SerializeField] private ParticleSystem flameFxPrefab;
    [Tooltip("EnemyData_ShamanのBullet Types「SpiritFlame」の番号（線で反射された火の玉の弾）")]
    [SerializeField] private int bulletTypeIndex = -1;

    [Header("召喚の間隔（秒・スローモーションに追従）")]
    [SerializeField] private Vector2 firstDelay = new Vector2(8f, 12f);
    [Tooltip("火の玉が全部なくなってから（壊された・線で反射された）、次に召喚するまでの秒数（最小/最大）")]
    [SerializeField] private Vector2 interval = new Vector2(10f, 14f);

    [Header("回転（Shamanを選んでいる時、SceneビューにPlay前でも円が表示される）")]
    [Tooltip("回転の中心の位置（Shamanからのずれ・ワールド単位）")]
    [SerializeField] private Vector2 orbitCenterOffset = new Vector2(0f, 1.5f);
    [Tooltip("回転の半径（ワールド単位）")]
    [SerializeField] private float orbitRadius = 1.2f;
    [Tooltip("回転の速さ（度/秒。マイナスで逆回り）")]
    [SerializeField] private float orbitSpeedDeg = 120f;
    [Tooltip("呼び出す火の玉の数")]
    [Min(1)] [SerializeField] private int flameCount = 3;
    [Tooltip("火の玉のブロックHP（反射弾を何発受けたら壊れるか。通常反射1・Just反射2のダメージ）")]
    [Min(1)] [SerializeField] private int flameBlockHp = 5;

    [Header("召喚")]
    [Tooltip("召喚時のSE（火炎魔法1）")]
    [SerializeField] private AudioClip summonSE;
    [Range(0f, 1f)] [SerializeField] private float summonSEVolume = 1f;

    [Header("線で反射された火の玉")]
    [Tooltip("飛んでいく速さ（0以下ならEnemyDataの「SpiritFlame」の速さ）")]
    [SerializeField] private float reflectedSpeed = 0f;
    [Tooltip("反射された火の玉が吸収して大きくなる回数の上限（未反射の弾・回っている火の玉を吸収するたびに少し大きくなる。威力は上がらない）。上限後も吸収はするが大きくならない")]
    [Min(0)] [SerializeField] private int maxAbsorbCount = 5;
    [Tooltip("吸収1回ごとに大きくなる割合（0.12＝12%ずつ。見た目と当たり判定の両方）")]
    [SerializeField] private float growthPerAbsorb = 0.12f;
    [Tooltip("吸収した時のSE（火炎魔法1。空なら召喚時のSE）")]
    [SerializeField] private AudioClip absorbSE;
    [Range(0f, 1f)] [SerializeField] private float absorbSEVolume = 1f;
    [Tooltip("反射した火の玉の当たり判定が、すぐ近くの他の回っている火の玉を無視する秒数（反射した瞬間に隣の火の玉へ当たらないように）")]
    [SerializeField] private float ignoreOtherFlamesSeconds = 0.3f;

    private ShamanController shaman;
    private EnemyShooter shooter;
    private EnemyStats stats;
    private EnemyData data;
    private EnemyBullet bulletPrefab;
    private Transform projectileRoot;

    private readonly List<ShamanSpiritFlame> flames = new List<ShamanSpiritFlame>();
    private float orbitAngle;
    private float timer;
    private bool dead;

    private static float TimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
    private static bool IsGameOver => FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal || PixelDancerController.IsDownGlobal;
    private bool IsWarping => shaman != null && shaman.IsWarping;
    private float ShamanAlpha => (shaman != null && shaman.BodyRenderer != null) ? shaman.BodyRenderer.color.a : 1f;
    private Vector2 OrbitCenter => (Vector2)transform.position + orbitCenterOffset;

    private void Awake()
    {
        shaman = GetComponent<ShamanController>();
        shooter = GetComponent<EnemyShooter>();
        stats = GetComponent<EnemyStats>();
        if (stats != null) stats.onKilled += HandleKilled;
    }

    private void OnDestroy()
    {
        if (stats != null) stats.onKilled -= HandleKilled;
        foreach (var f in flames) if (f != null) Destroy(f.gameObject);
        flames.Clear();
    }

    private void Start()
    {
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

    private void Update()
    {
        if (dead) return;

        // 回っている火の玉の位置・見え方（ワープ中はShamanと一緒に消え、当たり判定も消える。回転も止める）
        flames.RemoveAll(f => f == null || !f.IsAlive);
        if (flames.Count > 0)
        {
            if (!IsWarping) orbitAngle += orbitSpeedDeg * Time.deltaTime * TimeScale;
            float alpha = ShamanAlpha;
            for (int i = 0; i < flames.Count; i++)
            {
                flames[i].MoveTo(OrbitPos(flames[i]));
                flames[i].SetExternalAlpha(alpha);
            }
            return;
        }

        // 全部なくなったら、一定時間後にまた召喚（ワープ中・ゲームオーバー中は数えない）
        if (IsGameOver || IsWarping) return;
        timer -= Time.deltaTime * TimeScale;
        if (timer > 0f) return;
        if (flamePrefab == null) { timer = 1f; return; }
        Summon();
        timer = RandomRange(interval);
    }

    // 召喚した時の並び順（slot）で、回転上の位置を決める
    private readonly Dictionary<ShamanSpiritFlame, int> slots = new Dictionary<ShamanSpiritFlame, int>();
    private Vector2 OrbitPos(ShamanSpiritFlame f)
    {
        int slot = slots.TryGetValue(f, out int s) ? s : 0;
        float a = (orbitAngle + 360f / Mathf.Max(1, flameCount) * slot) * Mathf.Deg2Rad;
        return OrbitCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * orbitRadius;
    }

    private void Summon()
    {
        if (summonSE != null && summonSEVolume > 0f)
            AudioOneShotPool.Play(summonSE, summonSEVolume * MasterSEVolume, transform.position, null, 0.1f);
        orbitAngle = Random.Range(0f, 360f);
        slots.Clear();
        for (int i = 0; i < Mathf.Max(1, flameCount); i++)
        {
            float a = (orbitAngle + 360f / Mathf.Max(1, flameCount) * i) * Mathf.Deg2Rad;
            Vector2 p = OrbitCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * orbitRadius;
            var f = Instantiate(flamePrefab, new Vector3(p.x, p.y, transform.position.z), Quaternion.identity);
            SetLayerRecursively(f.gameObject, gameObject.layer); // Shamanと同じ「敵」のレイヤー（反射していない弾はすり抜ける）
            var wh = f.GetComponent<WallHealth>();
            if (wh != null) wh.SetMaxHp(flameBlockHp);
            f.OnTouchedLine += HandleTouchedLine;
            slots[f] = i;
            flames.Add(f);
            f.Appear();
        }
    }

    // ---------- 線に触れた火の玉を、反射弾に変えて飛ばす ----------
    private void HandleTouchedLine(ShamanSpiritFlame f, PaddleDot dot)
    {
        if (dead || f == null || !f.IsAlive || dot == null) return;
        if (!EnsureRefs()) return;
        EnemyData.BulletType bt = GetBulletType();
        if (bt == null) return;

        Vector2 pos = f.transform.position;
        Color? tint = f.IsWeakened ? f.WeakenedColor : (Color?)null; // 青白くなった色のまま飛ばす
        // 跳ね返る向き：線から火の玉へ向かう向き（線の面の法線の代わり）で、回っていた向きを反射する
        Vector2 normal = pos - (Vector2)dot.transform.position;
        normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up;
        float sign = Mathf.Sign(orbitSpeedDeg);
        Vector2 radial = (pos - OrbitCenter).normalized;
        Vector2 tangent = new Vector2(-radial.y, radial.x) * sign;
        Vector2 dir = Vector2.Reflect(tangent, normal);
        if (Vector2.Dot(dir, normal) < 0.2f) dir = (dir + normal).normalized; // 線の向こう側へ抜けないように
        if (dir.sqrMagnitude < 0.0001f) dir = normal;

        // 回っていた火の玉は消し、同じ位置から反射弾として飛ばす
        flames.Remove(f);
        f.Vanish();

        EnemyBullet bullet = EnemyBulletPool.Get(bulletPrefab, pos, Quaternion.identity, projectileRoot);
        bullet.SetDirection(dir);
        EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, data.bulletSpeed, data.bulletLifeTime, data.bulletSpriteOverride, bulletPrefab, projectileRoot);
        if (reflectedSpeed > 0f) bullet.ApplyBullet(reflectedSpeed, Mathf.Max(bt.lifeTime, 10f));
        bullet.SetDirection(dir);
        // 他の回っている火の玉にすぐ当たらないように少しの間だけ無視する
        foreach (var other in flames)
        {
            var oc = other != null ? other.GetComponent<Collider2D>() : null;
            if (oc != null) bullet.SetOwnerCollisionIgnore(oc, ignoreOtherFlamesSeconds);
        }
        // プレイヤーの線で反射された扱いにする（PaddleDotの通常の反射と同じ順番）
        var lineType = ShamanSpiritFlame.GetLineType(dot);
        bullet.SetReflectedByStroke(dot.ParentStroke);
        bullet.MarkReflected();
        bullet.RegisterPaddleBounce(lineType);
        SessionStats.AddReflect(false);
        PaddleDrawer.Instance?.PlayPaddleHitSE(lineType, false);
        PaddleDrawer.Instance?.SpawnNormalReflectVfx(lineType, pos, dir);

        // 見た目は炎のParticle・Trail無し・反射回数で消えない・未反射の弾を吸収して大きくなる
        bullet.gameObject.AddComponent<ShamanFlameBullet>().Arm(bullet, flameFxPrefab,
            absorbSE != null ? absorbSE : summonSE, absorbSEVolume, maxAbsorbCount, growthPerAbsorb, tint);
    }

    private void HandleKilled()
    {
        dead = true;
        foreach (var f in flames) if (f != null) f.Vanish(); // 倒された時は壊れたSEは鳴らさずに消す
        flames.Clear();
    }

    private EnemyData.BulletType GetBulletType()
    {
        if (data == null || data.bulletTypes == null || bulletTypeIndex < 0 || bulletTypeIndex >= data.bulletTypes.Length)
        {
            Debug.LogWarning($"[ShamanSpiritFlames] Bullet Type index {bulletTypeIndex} がEnemyDataにありません（メニュー「Tools/Shaman/精霊の炎を追加」を実行してください）", this);
            return null;
        }
        return data.bulletTypes[bulletTypeIndex];
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
    }

    private static float RandomRange(Vector2 r) => Random.Range(Mathf.Min(r.x, r.y), Mathf.Max(r.x, r.y));

#if UNITY_EDITOR
    // Play前のSceneビューで回転の中心と半径を表示（Shamanを選んでいる時）
    private void OnDrawGizmosSelected()
    {
        Vector3 c = transform.position + (Vector3)orbitCenterOffset;
        Gizmos.color = new Color(1f, 0.55f, 0.15f, 1f);
        Gizmos.DrawWireSphere(c, 0.08f);
        const int seg = 40;
        Vector3 prev = c + new Vector3(orbitRadius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 p = c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * orbitRadius;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        for (int i = 0; i < Mathf.Max(1, flameCount); i++)
        {
            float a = 360f / Mathf.Max(1, flameCount) * i * Mathf.Deg2Rad;
            Gizmos.DrawWireSphere(c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * orbitRadius, 0.25f);
        }
    }
#endif
}
