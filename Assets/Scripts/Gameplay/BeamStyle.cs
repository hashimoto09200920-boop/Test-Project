using UnityEngine;

/// <summary>
/// ビーム（EnemyBeamBullet）の見た目を豪華にするスタイル設定。EnemyData.BulletType.beamStyleで指定する。
/// 実際の描画はBeamStyleFX（ビームの発射時に自動で付く）が行う。当たり判定・ダメージ・反射には一切影響しない。
/// メニュー「Tools/ビームの見た目/…」でDragon・Obelisk・Bit用を作成する。
/// </summary>
[CreateAssetMenu(fileName = "BeamStyle", menuName = "Game/Beam Style")]
public class BeamStyle : ScriptableObject
{
    [Header("① 白い芯・外側の光")]
    public bool useCore = true;
    [Tooltip("芯の太さ（ビームの太さに対する倍率）")]
    public float coreWidthMul = 0.35f;
    public Color coreColor = Color.white;
    public bool useGlow = true;
    [Tooltip("外側の光の太さ（ビームの太さに対する倍率）")]
    public float glowWidthMul = 3.2f;
    [Range(0f, 1f)] public float glowAlpha = 0.35f;
    [Tooltip("芯・外側の光・稲妻に使う、ふちがぼやけた線のマテリアル（加算）")]
    public Material softLineMaterial;

    [Header("② 流れる模様")]
    public bool useFlow = true;
    public Material flowMaterial;
    [Tooltip("模様の太さ（ビームの太さに対する倍率）")]
    public float flowWidthMul = 1.4f;
    [Range(0f, 1f)] public float flowAlpha = 0.7f;
    [Tooltip("模様1つ分の長さ（ワールド単位）")]
    public float flowTileLength = 0.9f;
    [Tooltip("模様が流れる速さ（1秒に流れる模様の数）")]
    public float flowSpeed = 5f;

    [Header("③ 太さの揺らぎ")]
    [Range(0f, 1f)] public float wobbleAmplitude = 0.15f;
    public float wobbleFrequency = 16f;

    [Header("④ 発射口・反射点・着弾点の光（フレア）")]
    public bool useFlares = true;
    public Sprite flareSprite;
    public Material flareMaterial;
    [Tooltip("フレアの大きさ（ビームの太さに対する倍率）")]
    public float flareSizeMul = 7f;
    [Range(0f, 1f)] public float flareAlpha = 0.85f;
    public float flarePulseSpeed = 14f;

    [Header("⑤ 発射の瞬間の太さ")]
    [Tooltip("撃った瞬間の太さの倍率（この後、通常の太さへ絞られる）")]
    public float fireBurstWidthMul = 2.4f;
    public float fireBurstDuration = 0.18f;

    [Header("⑥ 着弾点の火花")]
    public ParticleSystem impactSparksPrefab;
    [Tooltip("1つの着弾点から1秒に出す火花の数")]
    public float impactSparkRate = 30f;

    [Header("反射した瞬間の閃光")]
    public bool useReflectFlash = true;
    [Tooltip("閃光の大きさ（ビームの太さに対する倍率）")]
    public float reflectFlashSizeMul = 14f;
    public float reflectFlashDuration = 0.22f;

    [Header("反射後は色が変わる")]
    public bool tintReflected = true;
    public Color reflectedColor = new Color(1f, 1f, 0.92f, 1f);
    [Range(0f, 1f)] public float reflectedTintAmount = 0.55f;

    [Header("照射が終わる時は発射口側から消える")]
    public bool tailToTipFade = true;

    [Header("陽炎（Dragon向け）")]
    public bool useHaze = false;
    public Material hazeMaterial;
    public Color hazeColor = new Color(1f, 0.45f, 0.1f, 1f);
    public float hazeWidthMul = 4.5f;
    [Range(0f, 1f)] public float hazeAlpha = 0.3f;
    public float hazeTileLength = 1.6f;
    public float hazeSpeed = 1.6f;

    [Header("火の粉（Dragon向け）")]
    public ParticleSystem embersPrefab;
    [Tooltip("ビーム全体で1秒に出す火の粉の数")]
    public float emberRate = 26f;

    [Header("回路の稲妻（Obelisk向け）")]
    public bool useLightning = false;
    [Tooltip("1区間にまとわりつく稲妻の本数")]
    public int boltCount = 2;
    [Tooltip("稲妻が線から離れる最大の幅（ワールド単位）")]
    public float boltJitter = 0.28f;
    [Tooltip("稲妻の折れ目の間隔（ワールド単位）")]
    public float boltSegmentLength = 0.45f;
    public float boltWidth = 0.035f;
    public Color boltColor = new Color(0.9f, 0.7f, 1f, 1f);
    [Tooltip("稲妻の形を描き替える間隔（秒）")]
    public float boltRefreshInterval = 0.05f;
}
