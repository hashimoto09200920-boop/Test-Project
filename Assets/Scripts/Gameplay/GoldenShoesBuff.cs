using UnityEngine;

/// <summary>
/// アイテム「ゴールドシューズ」の効果（2026/11/10）。
/// ・取ると一定時間（30秒。ミッション報酬で40秒/50秒）、白線・赤線の消費量が半分（PaddleCostManager が CostMultiplier を掛ける）
/// ・効果中はダンサーの足元にネオンの光を残す。線ゲージの粒子は GoldenShoesGaugeFX
/// ・アイコン・残り秒数・点滅・取った瞬間の粒子は共通（ItemBuffBase）
/// 作成は Tools/アイテム/「ゴールドシューズの表示を作る（05_Gameを開いて実行）」。位置・大きさはPlay前にInspectorで調整できる
/// </summary>
// ★ダンサーの左右の移動は PixelDancerController.LateUpdate で決まるので、それより後にアイコンを置く
[DefaultExecutionOrder(1000)]
[ExecuteAlways]
public class GoldenShoesBuff : ItemBuffBase
{
    public static GoldenShoesBuff Instance { get; private set; }

    /// <summary>線の消費量に掛ける倍率。効果中なら costMultiplier（0.5）、それ以外は1</summary>
    public static float CostMultiplier => Instance != null && Instance.IsActive ? Instance.costMultiplier : 1f;

    public const string ShoesSpritePath = "Assets/Art/Gold/ゴールドシューズ.png";

    [Header("ゴールドシューズの効果")]
    [Tooltip("効果中の線の消費量の倍率（0.5＝半分）。白線・赤線の両方")]
    [Range(0.05f, 1f)] [SerializeField] private float costMultiplier = 0.5f;

    [Header("足元のネオンの光（効果中）")]
    [SerializeField] private bool footTrailEnabled = true;
    [Tooltip("光を置く間隔（秒・実時間）")]
    [Min(0.01f)] [SerializeField] private float footInterval = 0.05f;
    [Tooltip("前に置いた所からこれ以上動いた時だけ置く（止まっている時に重ならないように）")]
    [Min(0f)] [SerializeField] private float footMinMove = 0.04f;
    [Tooltip("光が消えるまでの時間（秒・実時間）")]
    [Min(0.05f)] [SerializeField] private float footGlowLife = 1.0f;
    [Tooltip("消え始めるまで、はっきり見えたままにする割合（0.4＝時間の40%までは薄くならない）")]
    [Range(0f, 0.95f)] [SerializeField] private float footGlowHold = 0.4f;
    [Tooltip("光の大きさ（ワールド）")]
    [Min(0.01f)] [SerializeField] private float footGlowSize = 0.45f;
    [Tooltip("光の中心に重ねる白い芯の大きさ（光の大きさに対する割合。0で芯なし）")]
    [Range(0f, 1f)] [SerializeField] private float footCoreRatio = 0.4f;
    [Tooltip("ダンサーの絵の下端（足元）からのずれ")]
    [SerializeField] private Vector2 footOffset = new Vector2(0f, 0.04f);
    [Tooltip("ダンサーの描画順からのずれ（-1＝ダンサーのすぐ後ろ）")]
    [SerializeField] private int footSortingOffset = -1;

    [Header("線ゲージの演出（PaddleCostBarUI の GoldenShoesGaugeFX）")]
    [SerializeField] private GoldenShoesGaugeFX gaugeFX;

    private float footTimer;
    private Vector3 lastFootPos;
    private bool hasLastFoot;
    private int footColorIndex;

    protected override string LogTag => "GoldenShoes";
    protected override float GetDuration(float baseSeconds) => MissionPassives.GetGoldenShoesDuration(baseSeconds);

    protected override void SetInstance(bool register)
    {
        if (register) { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        else if (Instance == this) Instance = null;
    }

    protected override void OnActivated(bool wasActive)
    {
        hasLastFoot = false;
        if (gaugeFX != null) gaugeFX.PlayStartBurst(); // 取った瞬間：ゲージから粒子がはじけ飛ぶ
    }

    protected override void OnEnded()
    {
        if (gaugeFX != null) gaugeFX.PlayEndBurst();
    }

    protected override void OnActiveLateUpdate(float dt)
    {
        if (!footTrailEnabled || footGlowSprite == null || dt <= 0f) return;
        var d = Dancer;
        if (d == null || PixelDancerController.IsPlayerDeadGlobal) return;
        var body = BodyOf(d);
        if (body == null || !body.enabled) return;

        footTimer -= dt;
        if (footTimer > 0f) return;
        if (!VisibleBounds(body, out Vector2 bmin, out Vector2 bmax)) return;
        Vector3 p = new Vector3((bmin.x + bmax.x) * 0.5f + footOffset.x, bmin.y + footOffset.y, d.transform.position.z + 0.01f); // 絵の下端（足元）
        if (hasLastFoot && (p - lastFootPos).sqrMagnitude < footMinMove * footMinMove) return;
        footTimer = footInterval;
        lastFootPos = p;
        hasLastFoot = true;
        SpawnGlow(p, body.sortingLayerID, body.sortingOrder + footSortingOffset, GlowColor(footColorIndex++), footGlowSize, footGlowLife, footGlowHold, Vector2.zero, 0f, footCoreRatio);
    }

#if UNITY_EDITOR
    /// <summary>ゴールドシューズの画像を読む（BlockItemManager の右クリックメニュー用）</summary>
    public static Sprite LoadShoesSpriteForEditor() => LoadSpriteForEditor(ShoesSpritePath);

    [ContextMenu("アイコンの画像を設定（Art/Gold/ゴールドシューズ.png）")]
    private void SetIconSprite() => SetIconSpriteFromPath(ShoesSpritePath);

    /// <summary>Setupツールから参照をつなぐ</summary>
    public void EditorWire(SpriteRenderer icon, TextMesh seconds, Sprite glow, GoldenShoesGaugeFX fx)
    {
        EditorWireBase(icon, seconds, glow, 0);
        gaugeFX = fx;
        UpdateEditorPreview();
    }
#endif
}
