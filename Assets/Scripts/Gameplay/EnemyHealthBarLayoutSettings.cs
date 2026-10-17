using UnityEngine;

/// <summary>
/// 全エネミー共通の、HPバー・シールドバー・数値・デバフアイコンの大きさと配置（ワールド単位。画面の高さは10）。
/// Assets/Resources/GameData/EnemyHealthBarLayoutSettings.asset に置く（メニュー「Tools/エネミーHP表示/共通の配置設定アセットを作成」）。
/// このアセットを調整するだけで、EnemyHealthDisplay（全エネミー）・FragmentHealthDisplay・NeonDancerHealthDisplayに反映される（Play中も即反映）。
/// ・大きさはエネミーの拡大率に関係なく画面上で一定
/// ・横幅と高さの位置は、出現直後にエネミーの絵の範囲を測って自動で決める（EnemyHealthDisplayのみ）
/// アセットが無い時は、このクラスの初期値で動く。Use Unified Layout をOFFにすると、各Prefabの従来の値に戻る。
/// </summary>
[CreateAssetMenu(menuName = "Game/Enemy Health Bar Layout Settings", fileName = "EnemyHealthBarLayoutSettings")]
public class EnemyHealthBarLayoutSettings : ScriptableObject
{
    [Tooltip("ON：全エネミーのHP表示を共通の大きさ・自動配置にする。OFF：各Prefabの従来の値（barWidth・displayOffsetY等）で表示")]
    public bool useUnifiedLayout = true;

    [Header("大きさ（HP・シールド共通）")]
    [Tooltip("バーの高さ")]
    public float barHeight = 0.12f;
    [Tooltip("HPバーとシールドバーの中心の間隔")]
    public float barPitch = 0.26f;
    [Tooltip("数値の文字の高さ（HP・シールド共通。全エネミー同じ大きさ）")]
    public float numberHeight = 0.34f;
    [Tooltip("数値（バーの左側）とバーの左端の間隔")]
    public float numberGap = 0.06f;

    [Header("横幅（エネミーの絵の横幅から計算）")]
    [Tooltip("バーの横幅＝エネミーの絵の横幅×この値")]
    public float widthRatio = 0.6f;
    [Tooltip("バーの横幅の下限")]
    public float minWidth = 0.5f;
    [Tooltip("バーの横幅の上限")]
    public float maxWidth = 1.5f;

    [Header("位置")]
    [Tooltip("エネミーの絵の上端から、HPバーの下端までの距離")]
    public float topGap = 0.12f;
    [Tooltip("一番上の表示（シールドバー／デバフアイコン）が画面の上端から最低これだけ内側に収まるよう押し下げる")]
    public float screenTopMargin = 0.1f;

    [Header("デバフアイコン（B4/B7/B8）")]
    [Tooltip("アイコンの高さ（画像の大きさに関係なく、この高さで表示）")]
    public float debuffIconSize = 0.36f;
    [Tooltip("一番上のバーの上端から、アイコンの下端までの距離")]
    public float debuffIconGap = 0.06f;
    [Tooltip("アイコンの中心どうしの横の間隔")]
    public float debuffIconSpacing = 0.4f;
    [Tooltip("残り秒数の数字の文字の高さ")]
    public float debuffNumberHeight = 0.25f;
    [Tooltip("残り秒数の数字の中心の、アイコンの上端からの高さ（数字がアイコンに重ならないよう、アイコンの上に置く）")]
    public float debuffNumberOffsetY = 0.12f;

    [Header("エネミーの絵の範囲の測り方")]
    [Tooltip("出現してからこの秒数の間だけ絵の範囲を測り、その間で一番大きい範囲を使う（その後は測らない）")]
    public float measureSeconds = 0.5f;
    [Tooltip("名前にこの文字を含む絵は、範囲に含めない（軌跡・攻撃予告・光などの演出用）。「(Clone)」の付いた後から生成された物も含めない")]
    public string[] excludeNameKeywords =
    {
        "Trail", "Preview", "AimRing", "Glow", "Flash", "Beam", "Spot", "Guide", "Mist", "Aura",
        "Spark", "ClawMark", "String", "EyeLight", "Muzzle", "Telegraph", "Shield", "Bubble", "Shadow", "Particle",
    };

    // ===================== 読み込み =====================
    private static EnemyHealthBarLayoutSettings s_instance;
    private static bool s_isFallback;
    private static float s_nextRetryTime;

    /// <summary>共通設定（アセットが無ければ初期値のインスタンス。エディタでは後からアセットを作った時のため、1秒に1回だけ読み直す）</summary>
    public static EnemyHealthBarLayoutSettings Instance
    {
        get
        {
            bool retry = s_instance == null || (s_isFallback && Application.isEditor && Time.realtimeSinceStartup >= s_nextRetryTime);
            if (retry)
            {
                var loaded = Resources.Load<EnemyHealthBarLayoutSettings>("GameData/EnemyHealthBarLayoutSettings");
                if (loaded != null) { s_instance = loaded; s_isFallback = false; }
                else
                {
                    if (s_instance == null)
                    {
                        s_instance = CreateInstance<EnemyHealthBarLayoutSettings>();
                        s_instance.hideFlags = HideFlags.DontSave;
                    }
                    s_isFallback = true;
                    s_nextRetryTime = Time.realtimeSinceStartup + 1f;
                }
            }
            return s_instance;
        }
    }

    /// <summary>この絵を範囲に含めないか（名前で判定）</summary>
    public bool IsExcluded(string objectName)
    {
        if (objectName.Contains("(Clone)")) return true;
        if (excludeNameKeywords == null) return false;
        foreach (var k in excludeNameKeywords)
            if (!string.IsNullOrEmpty(k) && objectName.Contains(k)) return true;
        return false;
    }

    // ===================== 数値（旧式TextMesh）の大きさ =====================
    // ★全エネミーの数値を同じ fontSize・同じ characterSize・ワールドの大きさ1で描くことで、大きさを完全にそろえる。
    //   （以前は描いた範囲から測って補正していたが、エネミーごとに測定値がばらつき、大きさがそろわなかったため廃止）
    /// <summary>共通配置の数値に使う fontSize（全エネミー同じ。文字の細かさだけに影響し、大きさは characterSize で決める）</summary>
    public const int UnifiedFontSize = 64;
    // TextMesh で fontSize×characterSize＝1 の時の、数字の文字の高さ（ワールド）。Editor.log の実測値（2026/10/17：Turtle・Zephyr・残り秒数で一致）から
    private const float WorldHeightPerFontUnit = 0.132f;

    /// <summary>数値の文字の高さを targetHeight（ワールド）にする。fontSize を共通の値にし、characterSize を設定する（変わった時だけ）</summary>
    public static void ApplyUnifiedText(TextMesh tm, float targetHeight)
    {
        if (tm == null) return;
        if (tm.fontSize != UnifiedFontSize) tm.fontSize = UnifiedFontSize;
        float cs = targetHeight / (UnifiedFontSize * WorldHeightPerFontUnit);
        if (!Mathf.Approximately(tm.characterSize, cs)) tm.characterSize = cs;
    }
}
