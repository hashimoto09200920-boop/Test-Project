using UnityEngine;

/// <summary>
/// アイテム「アクセラレーター」の効果（2026/11/10）。
/// ・取ると一定時間（線で取ると20秒・円で×1.5／C4で×2・×3。ミッション報酬で基礎値+5秒/+10秒。2026/11/12）、白線・赤線で跳ね返した弾の速さが1.5倍（PaddleDot → EnemyBullet.ApplyBoltSpeedUp）
///   効果中に跳ね返した弾は、効果が切れても速いまま。1つの弾に1回だけ（何度跳ね返しても重ねない）
/// ・速くなった弾には金色の尾が付く（ReflectedBulletFXManager）
/// ・効果が切れた瞬間に、ダンサーから粒子を散らす
/// ・アイコン・残り秒数・点滅・取った瞬間の粒子は共通（ItemBuffBase）。シューズと同時の時はアイコンを横に並べる
/// 作成は Tools/アイテム/「アクセラレーターの表示を作る（05_Gameを開いて実行）」
/// </summary>
[DefaultExecutionOrder(1000)]
[ExecuteAlways]
public class GoldBoltBuff : ItemBuffBase
{
    public static GoldBoltBuff Instance { get; private set; }

    /// <summary>効果中か（PaddleDot が跳ね返した時に見る）</summary>
    public static bool IsEffectActive => Instance != null && Instance.IsActive;
    /// <summary>跳ね返した弾の速さの倍率</summary>
    public static float SpeedMultiplier => Instance != null ? Instance.speedMultiplier : 1f;

    public const string BoltSpritePath = "Assets/Art/Gold/アクセラレーター.png";

    [Header("アクセラレーターの効果")]
    [Tooltip("効果中に線で跳ね返した弾の速さの倍率（1.5＝1.5倍）。白線・赤線の両方")]
    [Range(1f, 3f)] [SerializeField] private float speedMultiplier = 1.5f;

    protected override string LogTag => "Accelerator";
    protected override float GetDuration(float baseSeconds) => MissionPassives.GetGoldBoltDuration(baseSeconds);
    protected override bool DancerEndBurst => true; // 切れた瞬間にダンサーから粒子を散らす

    protected override void SetInstance(bool register)
    {
        if (register) { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        else if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    /// <summary>アクセラレーターの画像を読む（BlockItemManager の右クリックメニュー用）</summary>
    public static Sprite LoadBoltSpriteForEditor() => LoadSpriteForEditor(BoltSpritePath);

    [ContextMenu("アイコンの画像を設定（Art/Gold/アクセラレーター.png）")]
    private void SetIconSprite() => SetIconSpriteFromPath(BoltSpritePath);

    /// <summary>Setupツールから参照をつなぐ</summary>
    public void EditorWire(SpriteRenderer icon, TextMesh seconds, Sprite glow)
    {
        EditorWireBase(icon, seconds, glow, 1);
        UpdateEditorPreview();
    }
#endif
}
