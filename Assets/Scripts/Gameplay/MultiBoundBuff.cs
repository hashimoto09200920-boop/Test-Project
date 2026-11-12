using UnityEngine;

/// <summary>
/// アイテム「マルチバウンド」の効果（2026/11/11）。
/// ・取ると一定時間（線で取ると20秒・円で×1.5／C4で×2・×3。ミッション報酬で基礎値+5秒/+10秒。2026/11/12）、取った後に白線・赤線で跳ね返した弾の「跳ね返せる回数」が2倍
///   （PaddleDot → EnemyBullet.ApplyBounceLimitBoost。残り回数に、その弾の上限と同じ数を足す）
///   取る前に跳ね返した弾には効かない。1つの弾に1回だけ。効果が切れても、その弾は増えたまま。上限が無い弾には何もしない
/// ・弾の見た目は変えない（ユーザー指示）
/// ・取った瞬間・切れた瞬間にダンサーから粒子。アイコン・残り秒数・点滅は共通（ItemBuffBase）。ほかの効果と同時の時はアイコンを横に並べる
/// 作成は Tools/アイテム/「マルチバウンドの表示を作る（05_Gameを開いて実行）」
/// </summary>
[DefaultExecutionOrder(1000)]
[ExecuteAlways]
public class MultiBoundBuff : ItemBuffBase
{
    public static MultiBoundBuff Instance { get; private set; }

    /// <summary>効果中か（PaddleDot が跳ね返した時に見る）</summary>
    public static bool IsEffectActive => Instance != null && Instance.IsActive;

    public const string MultiBoundSpritePath = "Assets/Art/Gold/マルチバウンド.png";

    protected override string LogTag => "MultiBound";
    protected override float GetDuration(float baseSeconds) => MissionPassives.GetMultiBoundDuration(baseSeconds);
    protected override bool DancerEndBurst => true; // 切れた瞬間にダンサーから粒子を散らす

    protected override void SetInstance(bool register)
    {
        if (register) { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        else if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    /// <summary>マルチバウンドの画像を読む（BlockItemManager の右クリックメニュー用）</summary>
    public static Sprite LoadMultiBoundSpriteForEditor() => LoadSpriteForEditor(MultiBoundSpritePath);

    [ContextMenu("アイコンの画像を設定（Art/Gold/マルチバウンド.png）")]
    private void SetIconSprite() => SetIconSpriteFromPath(MultiBoundSpritePath);

    /// <summary>Setupツールから参照をつなぐ</summary>
    public void EditorWire(SpriteRenderer icon, TextMesh seconds, Sprite glow)
    {
        EditorWireBase(icon, seconds, glow, 2);
        UpdateEditorPreview();
    }
#endif
}
