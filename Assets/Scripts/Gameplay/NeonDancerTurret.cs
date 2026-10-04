using UnityEngine;

/// <summary>
/// NeonDancerの発射台（パターン1/2/3）1基分の設定＋Scene可視化。
/// 実際の発射ループはNeonDancerController側のコルーチンが行う（ボス撃破時に一括停止させるため）。
/// 弾の中身（挙動・SE）はEnemyData_NeonDancerのBullet Types側で設定し、ここではそのインデックスを指定する。
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerTurret : MonoBehaviour
{
    [System.Serializable]
    public class BulletChoice
    {
        [Tooltip("EnemyDataのBullet Typesの番号（①=0, ②=1 … ⑨=8）")]
        public int bulletTypeIndex;
        [Tooltip("この弾が選ばれる確率（%）。合計が100でなくても、合計に対する割合で抽選する")]
        public float probabilityPercent = 33.3f;
    }

    [Header("Bullet Types（1発ごとに確率で1種類を抽選して発射）")]
    [Tooltip("※Countdown Explosionを有効にした弾は自分のFloor/Lightも爆風で削るため使わないこと")]
    [NonReorderable]
    [SerializeField] private BulletChoice[] bulletChoices =
    {
        new BulletChoice { bulletTypeIndex = 0, probabilityPercent = 34f },
        new BulletChoice { bulletTypeIndex = 1, probabilityPercent = 33f },
        new BulletChoice { bulletTypeIndex = 2, probabilityPercent = 33f },
    };

    /// <summary>後半フェーズの攻撃の種類（Normal＝前半と同じく1発撃つ）</summary>
    public enum Phase2Attack
    {
        Normal,         // 1発撃つ（③Missile・⑥Warheadなど）
        SpiralBurst,    // ①Susanooのスパイラル弾（発射角を回しながら連射）
        TrailSweep,     // ②ArcGuardのTrail Sweep（弾の位置ごとにワームホールを出して順に撃つ）
        Claw1H,         // ④ArcGuardのClaw1H（弾の位置ごとにワームホールを出し、Floor上のランダム位置へ撃つ）
        Tornado,        // ⑤ShamanのTornado（その場に砂煙を出し、中からミサイルを撃つ）
        SweepBeam,      // ⑦Obeliskの中央ビーム（プレイヤー方向を中心に薙ぎ払う）
        WarpMulti,      // ⑧Susanoo後半のワープ弾複数発射（同時／1発ずつずらす）
        EnhancedDrill,  // ⑨Tsukuyomi後半の強化ドリル弾
    }

    [System.Serializable]
    public class Phase2BulletChoice
    {
        [Tooltip("EnemyDataのBullet Typesの番号")]
        public int bulletTypeIndex;
        [Tooltip("この攻撃が選ばれる確率（%）。合計に対する割合で抽選する")]
        public float probabilityPercent = 33.3f;
        [Tooltip("攻撃の種類")]
        public Phase2Attack attack = Phase2Attack.Normal;
    }

    [Header("Bullet Types - 後半フェーズ（空なら前半のBullet Typesを使う）")]
    [NonReorderable]
    [SerializeField] private Phase2BulletChoice[] phase2BulletChoices;

    [Header("Fire Interval（秒。スローモーション中はその分ゆっくり進む）")]
    [Tooltip("戦闘開始から初弾のワームホールが出るまでの待ち時間（最小/最大）")]
    [SerializeField] private float initialDelayMin = 1f;
    [SerializeField] private float initialDelayMax = 2.5f;
    [Tooltip("1発撃ち終わってから次のワームホールが出るまでの待ち時間（最小/最大）")]
    [SerializeField] private float fireIntervalMin = 3f;
    [SerializeField] private float fireIntervalMax = 4.5f;

    [Header("Wormhole Spawn Area（この発射台の位置からのオフセット、ワールド単位）")]
    [Tooltip("出現範囲の中心（この発射台の位置からのオフセット）")]
    [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero;
    [Tooltip("出現範囲の大きさ（幅, 高さ）")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(1.5f, 0.6f);

    [Header("Wormhole")]
    [Tooltip("溜め完了時のワームホールの大きさ（ワールド単位のスケール）")]
    [SerializeField] private float wormholeSize = 0.8f;
    [Tooltip("出現→回転しながら拡大（溜め）にかける秒数。溜め完了の瞬間に発射する")]
    [SerializeField] private float chargeDuration = 0.8f;
    [Tooltip("発射後、縮小して消えるまでの秒数")]
    [SerializeField] private float shrinkDuration = 0.35f;
    [Tooltip("回転速度（度/秒）：出現直後")]
    [SerializeField] private float spinSpeedStart = 90f;
    [Tooltip("回転速度（度/秒）：溜め完了時（発射の瞬間）")]
    [SerializeField] private float spinSpeedEnd = 720f;

    [Header("Aim")]
    [Tooltip("Bullet TypeのAim ModeがUse Fire Directionの時の発射方向")]
    [SerializeField] private Vector2 fireDirection = Vector2.down;

    [Header("Fire SE（Bullet TypeのFire SE Overrideが未設定の時だけ使う）")]
    [SerializeField] private AudioClip fireSE;
    [Range(0f, 1f)] [SerializeField] private float fireSEVolume = 1f;

    [Header("Gizmo")]
    [SerializeField] private string gizmoLabel = "Turret";
    [SerializeField] private Color gizmoColor = new Color(1f, 0.3f, 1f, 0.9f);

    public bool HasBulletChoices => bulletChoices != null && bulletChoices.Length > 0;

    /// <summary>確率（%）の重み付きで1種類を抽選し、Bullet Typesの番号を返す。候補が無ければ-1</summary>
    public int PickBulletTypeIndex()
    {
        if (!HasBulletChoices) return -1;
        float total = 0f;
        foreach (var c in bulletChoices) if (c != null) total += Mathf.Max(0f, c.probabilityPercent);
        if (total <= 0f) return bulletChoices[0] != null ? bulletChoices[0].bulletTypeIndex : -1;

        float r = Random.value * total;
        float acc = 0f;
        foreach (var c in bulletChoices)
        {
            if (c == null) continue;
            acc += Mathf.Max(0f, c.probabilityPercent);
            if (r <= acc) return c.bulletTypeIndex;
        }
        for (int i = bulletChoices.Length - 1; i >= 0; i--)
            if (bulletChoices[i] != null) return bulletChoices[i].bulletTypeIndex;
        return -1;
    }
    public bool HasPhase2Choices => phase2BulletChoices != null && phase2BulletChoices.Length > 0;

    /// <summary>後半フェーズ：確率（%）の重み付きで1つ抽選する。候補が無ければnull</summary>
    public Phase2BulletChoice PickPhase2Choice()
    {
        if (!HasPhase2Choices) return null;
        float total = 0f;
        foreach (var c in phase2BulletChoices) if (c != null) total += Mathf.Max(0f, c.probabilityPercent);
        if (total <= 0f) return phase2BulletChoices[0];
        float r = Random.value * total;
        float acc = 0f;
        foreach (var c in phase2BulletChoices)
        {
            if (c == null) continue;
            acc += Mathf.Max(0f, c.probabilityPercent);
            if (r <= acc) return c;
        }
        for (int i = phase2BulletChoices.Length - 1; i >= 0; i--)
            if (phase2BulletChoices[i] != null) return phase2BulletChoices[i];
        return null;
    }

    public float InitialDelayMin => initialDelayMin;
    public float InitialDelayMax => initialDelayMax;
    public float FireIntervalMin => fireIntervalMin;
    public float FireIntervalMax => fireIntervalMax;
    public float WormholeSize => wormholeSize;
    public float ChargeDuration => chargeDuration;
    public float ShrinkDuration => shrinkDuration;
    public float SpinSpeedStart => spinSpeedStart;
    public float SpinSpeedEnd => spinSpeedEnd;
    public Vector2 FireDirection => fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector2.down;
    public AudioClip FireSE => fireSE;
    public float FireSEVolume => fireSEVolume;

    public Vector3 AreaCenterWorld => transform.position + (Vector3)spawnAreaCenter;

    public Vector3 GetRandomSpawnPosition()
    {
        Vector2 half = spawnAreaSize * 0.5f;
        Vector3 c = AreaCenterWorld;
        return new Vector3(c.x + Random.Range(-half.x, half.x), c.y + Random.Range(-half.y, half.y), transform.position.z);
    }

#if UNITY_EDITOR
    // ★選択しなくても3基の範囲が常に見えるよう、OnDrawGizmos（Selectedではない）で描く
    private void OnDrawGizmos()
    {
        Vector3 c = AreaCenterWorld;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(new Vector3(c.x, c.y, 0f), new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0.1f));
        Color fill = gizmoColor; fill.a *= 0.12f;
        Gizmos.color = fill;
        Gizmos.DrawCube(new Vector3(c.x, c.y, 0f), new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0.1f));

        var style = new GUIStyle();
        style.normal.textColor = gizmoColor;
        style.fontStyle = FontStyle.Bold;
        UnityEditor.Handles.Label(new Vector3(c.x - spawnAreaSize.x * 0.5f, c.y + spawnAreaSize.y * 0.5f + 0.15f, 0f), gizmoLabel, style);
    }
#endif
}
