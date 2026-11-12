using System.Collections.Generic;
using UnityEngine;

/// <summary>弾の性質を変えるアイテムの種類（2026/11/11）。★保存はしないが、ログ・Inspectorで使う。末尾に追加すること</summary>
public enum ShotItemType
{
    None = 0,
    Spiral = 1, // スパイラルショット：跳ね返した弾がドリルになる
    Blast = 2,  // ブラストショット：跳ね返した弾が敵に当たると爆発
    Split = 3,  // スプリットショット：跳ね返した弾が分裂
}

/// <summary>
/// 弾の性質を変えるアイテム（スパイラルショット・ブラストショット・スプリットショット）の管理（2026/11/11）。
/// ・取ったら残り回数を最初の数にする（同じ種類をもう1つ取っても最初の数に戻す。別の種類を取ったら上書き＝重ねない）
/// ・取った後に、普通の弾を白線・赤線で跳ね返した瞬間（Justも）、その弾の性質を変えて残り回数を1減らす
///   （性質を変えた弾をもう一度跳ね返した時・Tsukuyomi/NeonDancerの本物のドリル弾を跳ね返した時は減らない）
/// ・残り0になるまで効果は切れない。Stageをまたいでも持ち越す（このシーンにいる間）
/// ・残り回数は画面左上のゴールド表示の近く（ShotItemHUD）に出す
/// 作成は Tools/アイテム/「弾を変えるアイテムの表示を作る（05_Gameを開いて実行）」
/// </summary>
public class ShotItemManager : MonoBehaviour
{
    public static ShotItemManager Instance { get; private set; }

    [Header("表示（画面左上・ゴールド表示の近く）")]
    [SerializeField] private ShotItemHUD hud;

    [Header("スパイラルショット（ドリル）")]
    [Tooltip("取った時の残り回数（何発の弾をドリルにできるか）")]
    [Min(1)] [SerializeField] private int spiralShotCount = 5; // 2026/11/11 3→5（スプリットショットと合わせる）。シーンに前の値が残らないよう項目名も変えた
    [Tooltip("線に刺さった時に削る回数（Tsukuyomi・NeonDancerの通常ドリルと同じ4）")]
    [Min(1)] [SerializeField] private int spiralLineHits = 4;
    [Tooltip("敵に刺さった時のダメージの回数（通常ドリルと同じ8）。ミッション報酬で +4 / +8")]
    [Min(1)] [SerializeField] private int spiralEnemyHits = 8;
    [Tooltip("削る間隔（秒）。通常ドリルと同じ0.15")]
    [Min(0.01f)] [SerializeField] private float spiralHitInterval = 0.15f;
    [SerializeField] private bool spiralSpinWhilePinned = true;
    [SerializeField] private float spiralSpinSpeed = 720f;
    [Tooltip("刺さっている間にめり込む速さ。通常ドリルと同じ0.1")]
    [SerializeField] private float spiralCreepSpeed = 0.1f;
    [Tooltip("ドリルの演出（Tsukuyomi・NeonDancerのドリルと同じ。弾の画像は変えない）")]
    [SerializeField] private DrillFXManager drillFxPrefab;
    [Tooltip("跳ね返した直後、線から離れてからドリルにする距離（ワールド）。跳ね返した線にすぐ刺さり直さないように")]
    [Min(0f)] [SerializeField] private float spiralConvertClearDistance = 0.6f;
    [Tooltip("上の距離まで離れなくても、この時間（秒）が過ぎたらドリルにする")]
    [Min(0f)] [SerializeField] private float spiralConvertMaxDelay = 0.5f;

    [Header("ブラストショット（爆発）")]
    [Tooltip("取った時の残り回数（何発の弾を爆発弾にできるか）")]
    [Min(1)] [SerializeField] private int blastShotCount = 5; // 2026/11/11 4→5（スプリットショットと合わせる）。シーンに前の値が残らないよう項目名も変えた
    [Tooltip("爆発の範囲（半径・ワールド）。敵の爆発弾は2。ミッション報酬2段目で4")]
    [Min(0.1f)] [SerializeField] private float blastRadius = 3f;
    [Tooltip("爆発で敵に与えるダメージ（敵の爆発弾と同じ10）。ミッション報酬1段目から20")]
    [Min(0)] [SerializeField] private int blastDamage = 10;
    [Tooltip("爆発でブロックに与えるダメージ（敵の爆発弾と同じ10）。ダンサー・フロアには与えない")]
    [Min(0)] [SerializeField] private int blastWallDamage = 10;

    [Header("スプリットショット（分裂）")]
    [Tooltip("取った時の残り回数（何回分裂させられるか）")]
    [Min(1)] [SerializeField] private int splitShots = 5;
    [Tooltip("分かれる数（跳ね返した弾も含む）。ミッション報酬2段目で+1")]
    [Min(2)] [SerializeField] private int splitCount = 3;
    [Tooltip("分かれた弾どうしの角度（度）")]
    [Range(5f, 60f)] [SerializeField] private float splitAngleStep = 18f;
    [Tooltip("跳ね返した直後、線から離れてから分ける距離（ワールド）。分かれた弾が線にすぐ当たり直さないように")]
    [Min(0f)] [SerializeField] private float splitClearDistance = 0.4f;
    [Tooltip("上の距離まで離れなくても、この時間（秒）が過ぎたら分ける")]
    [Min(0f)] [SerializeField] private float splitMaxDelay = 0.3f;
    [Tooltip("分かれた直後、分かれた弾どうしが当たらないようにする時間（秒）")]
    [Min(0f)] [SerializeField] private float splitNoCollideSeconds = 0.4f;

    public ShotItemType Current { get; private set; } = ShotItemType.None;
    public int Remaining { get; private set; }

    // 跳ね返した直後で、まだドリルにしていない・まだ分けていない弾
    private struct Pending { public EnemyBullet bullet; public Vector3 from; public float time; public ShotItemType kind; }
    // 分かれた弾どうしの当たり判定を戻すまでの記録
    private struct NoCollide { public Collider2D a, b; public float until; }
    private readonly List<NoCollide> noCollides = new List<NoCollide>();
    private readonly List<Pending> pending = new List<Pending>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int InitialCount(ShotItemType t)
    {
        switch (t)
        {
            case ShotItemType.Spiral: return spiralShotCount;
            case ShotItemType.Blast: return blastShotCount;
            case ShotItemType.Split: return splitShots;
        }
        return 0;
    }

    /// <summary>アイテムを取った（BlockItemManager）。残り回数を最初の数にする。別の種類なら上書き</summary>
    public void Acquire(ShotItemType type)
    {
        var prev = Current;
        Current = type;
        Remaining = InitialCount(type);
        if (hud != null) hud.Show(type, Remaining, prev != ShotItemType.None && prev != type);
        Debug.Log($"[ShotItem] {type} を取った：残り {Remaining}{(prev != ShotItemType.None && prev != type ? $"（{prev} を上書き）" : "")}");
    }

    /// <summary>線で普通に跳ね返した時（PaddleDot。ドリルの跳ね返りは呼ばれない）</summary>
    public static void OnLineReflect(EnemyBullet bullet, Vector3 reflectPoint)
    {
        if (Instance != null) Instance.TryConvert(bullet, reflectPoint);
    }

    private void TryConvert(EnemyBullet b, Vector3 reflectPoint)
    {
        if (b == null || Current == ShotItemType.None || Remaining <= 0) return;
        if (b.ShotKind != ShotItemType.None) return;   // もう変えた弾（もう一度跳ね返しても減らない）
        if (b.CachedPinnedReflect != null) return;     // 本物のドリル弾（Tsukuyomi・NeonDancer）は減らない

        switch (Current)
        {
            case ShotItemType.Spiral:
                b.ShotKind = ShotItemType.Spiral; // 目印は今付ける（演出・2回目の跳ね返しで減らさない判定）。ドリルにするのは線から離れてから
                pending.Add(new Pending { bullet = b, from = reflectPoint, time = 0f, kind = ShotItemType.Spiral });
                break;
            case ShotItemType.Split:
                b.ShotKind = ShotItemType.Split; // 線から離れてから分ける（分かれた弾が線にすぐ当たり直さないように）
                pending.Add(new Pending { bullet = b, from = reflectPoint, time = 0f, kind = ShotItemType.Split });
                break;
            case ShotItemType.Blast:
                b.ShotKind = ShotItemType.Blast; // 敵に当たった時に爆発する（EnemyBullet.RegisterEnemyHitAsBounce → OnBlastHit）
                break;
            default:
                return;
        }

        Remaining--;
        if (hud != null) hud.SetCount(Remaining);
        if (Remaining <= 0)
        {
            Debug.Log($"[ShotItem] {Current} を使い切った");
            Current = ShotItemType.None;
            if (hud != null) hud.HideAnimated();
        }
    }

    private void Update()
    {
        UpdateNoCollides();
        if (pending.Count == 0) return;
        float dt = Time.deltaTime;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var p = pending[i];
            var b = p.bullet;
            // 消えた・使い回されて別の弾になった時はやめる
            if (b == null || !b.gameObject.activeInHierarchy || b.ShotKind != p.kind) { pending.RemoveAt(i); continue; }
            p.time += dt;
            bool isSplit = p.kind == ShotItemType.Split;
            float clear = isSplit ? splitClearDistance : spiralConvertClearDistance;
            float maxDelay = isSplit ? splitMaxDelay : spiralConvertMaxDelay;
            bool far = (b.transform.position - p.from).sqrMagnitude >= clear * clear;
            if (far || p.time >= maxDelay)
            {
                if (isSplit) MakeSplit(b); else MakeDrill(b);
                pending.RemoveAt(i);
            }
            else pending[i] = p;
        }
    }

    /// <summary>弾を分ける（ミッション報酬：1段目からダメージ×1.5、2段目で分かれる数+1）</summary>
    private void MakeSplit(EnemyBullet b)
    {
        int count = splitCount + MissionPassives.GetSplitShotExtraSplits();
        float dmgMul = MissionPassives.GetSplitShotDamageMul();
        var made = b.SplitInto(count, splitAngleStep, dmgMul);
        // 分かれた弾どうし（元の弾も含む）が重なって当たらないよう、しばらく当たり判定を外す
        var all = new List<Collider2D>();
        if (b.BulletCollider != null) all.Add(b.BulletCollider);
        foreach (var c in made) if (c != null && c.BulletCollider != null) all.Add(c.BulletCollider);
        float until = Time.time + splitNoCollideSeconds;
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
            {
                Physics2D.IgnoreCollision(all[i], all[j], true);
                noCollides.Add(new NoCollide { a = all[i], b = all[j], until = until });
            }
    }

    /// <summary>外した当たり判定を時間が来たら戻す（弾はプールで使い回されるので、必ず戻す）</summary>
    private void UpdateNoCollides()
    {
        for (int i = noCollides.Count - 1; i >= 0; i--)
        {
            var n = noCollides[i];
            if (Time.time < n.until && n.a != null && n.b != null && n.a.gameObject.activeInHierarchy && n.b.gameObject.activeInHierarchy) continue;
            if (n.a != null && n.b != null) Physics2D.IgnoreCollision(n.a, n.b, false);
            noCollides.RemoveAt(i);
        }
    }

    /// <summary>今の分かれる数（ミッション報酬を反映。演出の矢じりの数に使う）</summary>
    public int SplitCountNow => splitCount + MissionPassives.GetSplitShotExtraSplits();
    /// <summary>分かれた弾どうしの角度（演出の矢じりの開きに使う）</summary>
    public float SplitAngleStepNow => splitAngleStep;

    /// <summary>ブラストショットの弾が敵に当たった時（EnemyBullet.RegisterEnemyHitAsBounce）。ミッション報酬を反映して爆発させる</summary>
    public static void OnBlastHit(EnemyBullet b)
    {
        var m = Instance;
        float radius = m != null ? m.blastRadius : 3f;
        int dmg = m != null ? m.blastDamage : 10;
        int wall = m != null ? m.blastWallDamage : 10;
        dmg = MissionPassives.GetBlastShotDamage(dmg);
        radius = MissionPassives.GetBlastShotRadius(radius);
        b.TriggerBlastShot(radius, dmg, wall);
    }

    /// <summary>弾をドリルにする（Tsukuyomi・NeonDancerのドリルと同じ部品を付ける。画像は変えない）</summary>
    private void MakeDrill(EnemyBullet b)
    {
        if (b.CachedPinnedReflect != null) return;
        var pinned = b.gameObject.AddComponent<PinnedReflectBullet>();
        pinned.Configure(spiralLineHits, spiralHitInterval, spiralSpinWhilePinned, spiralSpinSpeed, spiralCreepSpeed);
        // 敵に刺さった時の回数＝線の回数×倍率。ミッション報酬で +4 / +8（8→12→16）
        int enemyHits = spiralEnemyHits + MissionPassives.GetSpiralShotHitBonus();
        pinned.SetEnemyHitMultiplier(Mathf.Max(1, Mathf.CeilToInt(enemyHits / (float)Mathf.Max(1, spiralLineHits))));
        b.RefreshCachedPinnedReflect();
        if (drillFxPrefab != null) DrillFX.Attach(b, drillFxPrefab);
    }

#if UNITY_EDITOR
    public void EditorWire(ShotItemHUD h, DrillFXManager drillFx)
    {
        hud = h;
        if (drillFxPrefab == null) drillFxPrefab = drillFx;
    }
#endif
}

/// <summary>弾の性質を変えるアイテムの画像の場所</summary>
public static class ShotItemPaths
{
    public const string Spiral = "Assets/Art/Gold/スパイラルショット.png";
    public const string Blast = "Assets/Art/Gold/ブラストショット.png";
    public const string Split = "Assets/Art/Gold/スプリットショット.png";
}
