using UnityEngine;

[CreateAssetMenu(menuName = "Test Project/Enemy Data", fileName = "EnemyData_New")]
public class EnemyData : ScriptableObject
{
    [Header("Visual")]
    public Sprite sprite;
    public Vector2 spriteScale = Vector2.one;

    [Header("HP")]
    public int maxHp = 3;

    [Header("Weak Point System")]
    [Tooltip("ON: WeakPoint パーツ（EnemyPart）のみダメージ判定。OFF: 親オブジェクトの全体にダメージ判定（従来通り）")]
    public bool useWeakPointSystem = false;

    [Header("Move (Legacy / Fallback)")]
    public float moveSpeed = 2f;
    public float moveRange = 2f;

    // =========================================================
    // Move Types (移動パターン)
    // =========================================================
    [System.Serializable]
    public class MoveType
    {
        // =========================================================
        // 1. Identity
        // =========================================================
        [Header("Identity")]
        public string name = "MoveType";

        [Header("Debug Display")]
        [Tooltip("ON: 移動パターンのデバッグ情報を表示する。OFF: 非表示")]
        public bool showDebugText = true;

        // =========================================================
        // 2. Movement Pattern Type
        // =========================================================
        public enum PatternType
        {
            None,           // 停止
            Horizontal,     // 左右往復
            Vertical,       // 上下往復
            Diagonal,       // 斜め往復
            Circle,         // 円運動
            Figure8,        // 8の字運動
            Zigzag,         // ジグザグ移動
            RandomWalk,     // ランダム移動
            SineWave,       // サイン波移動
            Lissajous,      // リサージュ曲線
            Hopping,        // ホッピング移動（カエルのようなジャンプ）
            Warp            // ワープ移動（瞬間移動）
        }

        [Header("Movement Pattern")]
        [Tooltip("移動パターンの種類を選択")]
        public PatternType patternType = PatternType.Horizontal;

        // =========================================================
        // 3. Basic Settings
        // =========================================================
        [Header("Basic Settings")]
        [Tooltip("【Speed】移動速度（Unity単位/秒）。\n" +
                 "ほとんどのパターンで使用されます（Circleパターンでは使用されません）。\n" +
                 "サンプル値: 2.0（標準）、1.0（遅い）、3.0（速い）")]
        public float speed = 2f;

        [Tooltip("【Range】移動範囲/半径（Unity単位）。\n" +
                 "Horizontal/Vertical/Diagonal/Hoppingパターンで使用されます。\n" +
                 "※rangeX/rangeYが0の場合、この値が使用されます（後方互換性のため）\n" +
                 "サンプル値: 2.0（標準）、3.0（広い範囲）、1.0（狭い範囲）")]
        public float range = 2f;

        [Tooltip("【Range X】X方向（横）の移動範囲（Unity単位）。\n" +
                 "0の場合は「Range」の値が使用されます。\n" +
                 "Horizontal/Vertical/Diagonal/Hoppingパターンで使用されます。\n" +
                 "サンプル値: 8.0（横に広い）、4.0（標準）、2.0（狭い）")]
        public float rangeX = 0f;

        [Tooltip("【Range Y】Y方向（縦）の移動範囲（Unity単位）。\n" +
                 "0の場合は「Range」の値が使用されます。\n" +
                 "Horizontal/Vertical/Diagonal/Hoppingパターンで使用されます。\n" +
                 "サンプル値: 4.0（縦に広い）、2.0（標準）、1.0（狭い）")]
        public float rangeY = 0f;

        [Tooltip("【Direction Deg】移動方向（度）。\n" +
                 "Zigzag/RandomWalk/Diagonalパターンで使用されます。\n" +
                 "0 = 右、90 = 上、180 = 左、270 = 下\n" +
                 "サンプル値: 0（右方向）、90（上方向）、270（下方向）、45（右上方向）")]
        [Range(0f, 360f)]
        public float directionDeg = 0f;

        // =========================================================
        // 4. Horizontal/Vertical Random Settings (Optional)
        // =========================================================
        [Header("Horizontal/Vertical Random Settings (Optional)")]
        [Tooltip("ON: Horizontal/Verticalパターンの開始方向をランダム化する。\n" +
                 "Horizontal: 左右どちらから開始するかランダム\n" +
                 "Vertical: 上下どちらから開始するかランダム")]
        public bool useRandomStartDirection = false;

        [Tooltip("ON: Horizontal/Verticalパターンの移動距離を往復ごとにランダム化する。\n" +
                 "OFF: 固定距離（range）で往復")]
        public bool useRandomDistance = false;

        [Tooltip("ランダム移動距離の最小値（Unity単位）。\n" +
                 "useRandomDistance=ONの時に使用されます。\n" +
                 "往復ごとに、Min～Max の範囲でランダムに距離が決まります。\n" +
                 "サンプル値: 1.0（短い移動）、0.5（とても短い）")]
        [Range(0.1f, 10f)]
        public float randomDistanceMin = 1f;

        [Tooltip("ランダム移動距離の最大値（Unity単位）。\n" +
                 "useRandomDistance=ONの時に使用されます。\n" +
                 "往復ごとに、Min～Max の範囲でランダムに距離が決まります。\n" +
                 "注意: rangeを超える値を設定しても、rangeが最大制限となります。\n" +
                 "サンプル値: 3.0（長い移動）、2.0（中程度）")]
        [Range(0.1f, 10f)]
        public float randomDistanceMax = 3f;

        // =========================================================
        // 5. Duration Settings
        // =========================================================
        [Header("Duration Settings (Optional)")]
        [Tooltip("ON: この移動パターンを指定時間後に切り替える")]
        public bool useDuration = false;

        [Tooltip("この移動パターンの継続時間（秒）。0以下は無制限")]
        public float durationSeconds = 5f;

        // =========================================================
        // 5. Circle Pattern Settings
        // =========================================================
        [Header("Circle Pattern Settings")]
        [Tooltip("【Radius】円の中心からの半径（Unity単位）。\n" +
                 "例: 1 = 中心から1単位の距離で円を描く")]
        public float circleRadius = 2f;

        [Tooltip("【Period】1周するのにかかる時間（秒）。\n" +
                 "例: 1 = 1秒で1周、2 = 2秒で1周\n" +
                 "値が小さいほど回転が速くなります")]
        public float circlePeriod = 3f;

        [Tooltip("【Start Angle】開始角度（度）。円運動の開始位置を指定します。\n" +
                 "0 = 右側、90 = 上側、180 = 左側、270 = 下側\n" +
                 "例: 0 = 右側から開始、90 = 上側から開始")]
        [Range(0f, 360f)]
        public float circleStartAngle = 0f;

        [Tooltip("【Clockwise】回転方向。\n" +
                 "ON = 時計回り（右回り）、OFF = 反時計回り（左回り）")]
        public bool circleClockwise = true;

        // =========================================================
        // 6. Figure-8 Pattern Settings
        // =========================================================
        [Header("Figure-8 Pattern Settings")]
        [Tooltip("8の字の横方向の幅（Unity単位）")]
        public float figure8Width = 3f;

        [Tooltip("8の字の縦方向の高さ（Unity単位）")]
        public float figure8Height = 2f;

        [Tooltip("1周の時間（秒）")]
        public float figure8Period = 4f;

        // =========================================================
        // 7. Zigzag Pattern Settings
        // =========================================================
        [Header("Zigzag Pattern Settings")]
        [Tooltip("【Zigzag Width】ジグザグの幅（Unity単位）。\n" +
                 "進行方向に対して垂直方向の揺れの大きさ。\n" +
                 "例: 1.5 = 左右に1.5単位ずつ揺れる\n" +
                 "サンプル値: 1.5（中程度の揺れ）、3.0（大きな揺れ）")]
        public float zigzagWidth = 1.5f;

        [Tooltip("【Zigzag Period Length】ジグザグの1周期の長さ（Unity単位）。\n" +
                 "進行方向に進む距離で、この距離ごとにジグザグが1周期。\n" +
                 "値が小さいほどジグザグが細かくなります。\n" +
                 "例: 2 = 2単位進むごとに1周期\n" +
                 "サンプル値: 2.0（標準）、1.0（細かいジグザグ）、4.0（緩やかなジグザグ）\n" +
                 "【重要】進行方向は「Basic Settings」の「Direction Deg」で設定します。\n" +
                 "0=右、90=上、180=左、270=下")]
        public float zigzagPeriodLength = 2f;

        // =========================================================
        // 8. Sine Wave Pattern Settings
        // =========================================================
        [Header("Sine Wave Pattern Settings")]
        [Tooltip("【Sine Amplitude】サイン波の振幅（Unity単位）。\n" +
                 "進行方向に対して垂直方向の揺れの大きさ。\n" +
                 "例: 1 = 左右に1単位ずつ揺れる\n" +
                 "サンプル値: 1.0（小さい揺れ）、2.0（中程度）、3.0（大きな揺れ）")]
        public float sineAmplitude = 1f;

        [Tooltip("【Sine Frequency】サイン波の周波数（1秒あたりの周期数）。\n" +
                 "値が大きいほど揺れが速くなります。\n" +
                 "例: 1 = 1秒で1周期、2 = 1秒で2周期\n" +
                 "サンプル値: 1.0（緩やか）、2.0（標準）、3.0（速い）")]
        public float sineFrequency = 1f;

        [Tooltip("【Sine Direction】サイン波の進行方向（度）。\n" +
                 "この方向に進みながら、垂直方向にサイン波状に揺れます。\n" +
                 "0 = 右、90 = 上、180 = 左、270 = 下\n" +
                 "サンプル値: 0（右方向）、90（上方向）、270（下方向）")]
        [Range(0f, 360f)]
        public float sineDirectionDeg = 0f;

        // =========================================================
        // 9. Lissajous Pattern Settings
        // =========================================================
        [Header("Lissajous Pattern Settings")]
        [Tooltip("【Lissajous Amplitude X】X方向（横方向）の振幅（Unity単位）。\n" +
                 "左右の揺れの大きさ。\n" +
                 "サンプル値: 2.0（標準）、3.0（大きい）、1.0（小さい）")]
        public float lissajousAmplitudeX = 2f;

        [Tooltip("【Lissajous Amplitude Y】Y方向（縦方向）の振幅（Unity単位）。\n" +
                 "上下の揺れの大きさ。\n" +
                 "サンプル値: 1.5（標準）、2.5（大きい）、1.0（小さい）")]
        public float lissajousAmplitudeY = 1.5f;

        [Tooltip("【Lissajous Frequency X】X方向の周波数。\n" +
                 "横方向の揺れの速さ。値が大きいほど速く揺れます。\n" +
                 "サンプル値: 1.0（標準）、2.0（速い）、0.5（遅い）")]
        public float lissajousFrequencyX = 1f;

        [Tooltip("【Lissajous Frequency Y】Y方向の周波数。\n" +
                 "縦方向の揺れの速さ。値が大きいほど速く揺れます。\n" +
                 "X/Yの周波数の比で、描かれる図形の形が変わります。\n" +
                 "例: X=1, Y=2 → 8の字のような形、X=1, Y=1 → 円や楕円\n" +
                 "サンプル値: 2.0（8の字）、1.0（円/楕円）、3.0（複雑な形）")]
        public float lissajousFrequencyY = 2f;

        [Tooltip("【Lissajous Phase】位相差（度）。\n" +
                 "X方向とY方向の揺れの開始タイミングのずれ。\n" +
                 "0 = 同時開始、90 = 1/4周期ずれ、180 = 半周期ずれ\n" +
                 "位相差によって描かれる図形の形が変わります。\n" +
                 "サンプル値: 0（標準）、90（45度傾いた形）、180（反転した形）")]
        [Range(0f, 360f)]
        public float lissajousPhase = 0f;

        // =========================================================
        // 10. Random Walk Settings
        // =========================================================
        [Header("Random Walk Settings")]
        [Tooltip("【Random Walk Change Interval】方向変更の間隔（秒）。\n" +
                 "この時間ごとにランダムに方向を変更します。\n" +
                 "値が小さいほど頻繁に方向が変わります。\n" +
                 "サンプル値: 1.0（標準）、0.5（頻繁に変更）、2.0（ゆっくり変更）")]
        public float randomWalkChangeInterval = 1f;

        [Tooltip("【Random Walk Angle Range】方向変更のランダム範囲（度）。\n" +
                 "「Basic Settings」の「Direction Deg」を基準に、この範囲内でランダムに方向を変更します。\n" +
                 "例: Direction Deg=0（右）、Angle Range=45 → 右方向を基準に±45度の範囲でランダム\n" +
                 "サンプル値: 45（標準的なランダム）、90（広い範囲）、30（狭い範囲）\n" +
                 "【重要】基本方向は「Basic Settings」の「Direction Deg」で設定します。\n" +
                 "0=右、90=上、180=左、270=下")]
        [Range(0f, 180f)]
        public float randomWalkAngleRange = 45f;

        // =========================================================
        // 11. Hopping Settings
        // =========================================================
        [Header("Hopping Settings")]
        [Tooltip("【Hopping Jump Height】ジャンプの高さ（Unity単位）。\n" +
                 "カエルのようなホッピング移動の頂点の高さです。\n" +
                 "サンプル値: 2.0（標準）、1.0（低いジャンプ）、3.0（高いジャンプ）")]
        public float hoppingJumpHeight = 2f;

        [Tooltip("【Hopping Jump Distance】ジャンプの長さ・横方向の距離（Unity単位）。\n" +
                 "1回のジャンプで移動する距離です。\n" +
                 "サンプル値: 3.0（標準）、2.0（短いジャンプ）、5.0（長いジャンプ）")]
        public float hoppingJumpDistance = 3f;

        [Tooltip("【Hopping Jump Duration】ジャンプにかかる時間（秒）。\n" +
                 "空中にいる時間です。値が小さいほど素早くジャンプします。\n" +
                 "サンプル値: 0.5（標準）、0.3（速いジャンプ）、0.8（ゆっくりジャンプ）")]
        public float hoppingJumpDuration = 0.5f;

        [Tooltip("【Hopping Grounded Duration】着地後の停止時間（秒）。\n" +
                 "着地してから次のジャンプまでの待機時間です。\n" +
                 "サンプル値: 0.3（標準）、0.1（ほぼ連続ジャンプ）、1.0（長い休憩）")]
        public float hoppingGroundedDuration = 0.3f;

        // =========================================================
        // 12. Warp Settings
        // =========================================================
        [Header("Warp Settings")]
        [Tooltip("【Warp Grounded Duration】ワープ後の硬直時間（秒）。\n" +
                 "ワープ後に停止している時間です。次のワープまでの待機時間でもあります。\n" +
                 "サンプル値: 1.0（標準）、0.5（頻繁にワープ）、2.0（ゆっくりワープ）")]
        public float warpGroundedDuration = 1f;
    }

    [Header("Move Types (Optional)")]
    [Tooltip("ここを1つ以上入れると、移動時にこのリストから移動パターンを選んで適用する。空なら従来どおり moveSpeed/moveRange 等を使う。")]
    public MoveType[] moveTypes;

    // =========================================================
    // Move Firing Routine (移動ルーチン)
    // =========================================================
    [System.Serializable]
    public class MoveFiringRoutine
    {
        public enum RoutineType
        {
            Sequence,      // 順番移動
            Probability    // 割合移動
        }

        [Header("Routine Type")]
        [Tooltip("順番移動: 指定した順番で各パターンを適用してループ / 割合移動: 各パターンを確率で適用")]
        public RoutineType routineType = RoutineType.Sequence;

        [System.Serializable]
        public class SequenceEntry
        {
            [Header("対応する移動パターン")]
            [Tooltip("この設定が適用される移動パターンを選択します。\n" +
                     "「Move Types」配列のインデックス（Element番号）を選択してください。\n" +
                     "例: 0 = Move Types[0]（Element 0）、1 = Move Types[1]（Element 1）")]
            [Range(0, 99)]
            public int moveTypeIndex = 0;

            [Header("移動時間設定")]
            [Tooltip("この移動パターンを適用する時間（秒）")]
            public float durationSeconds = 5f;

            [Header("複数パーツ敵用設定")]
            [Tooltip("ON: このパターン開始時にBodyの位置を反転（左右反転）。蛇のような敵の方向転換に使用")]
            public bool flipBodyOnStart = false;

            // 読み取り専用プロパティ（Inspector表示用・OnValidateで自動更新）
            [Header("表示用（読み取り専用）")]
            [Tooltip("選択された移動パターンの名前（自動表示・読み取り専用）")]
            [SerializeField] private string moveTypeNameDisplay = "未設定";

            public string MoveTypeNameDisplay => moveTypeNameDisplay;
            
            // 内部メソッド：OnValidateから呼び出される
            public void UpdateMoveTypeNameDisplay(string name)
            {
                moveTypeNameDisplay = string.IsNullOrEmpty(name) ? "未設定" : name;
            }
        }

        [System.Serializable]
        public class ProbabilityEntry
        {
            [Header("対応する移動パターン")]
            [Tooltip("この設定が適用される移動パターンを選択します。\n" +
                     "「Move Types」配列のインデックス（Element番号）を選択してください。\n" +
                     "例: 0 = Move Types[0]（Element 0）、1 = Move Types[1]（Element 1）")]
            [Range(0, 99)]
            public int moveTypeIndex = 0;

            [Header("適用確率設定")]
            [Tooltip("この移動パターンの適用確率（0～100%）。合計が100%でなくても動作します（相対確率として扱います）")]
            [Range(0f, 100f)]
            public float probabilityPercentage = 50f;

            // 読み取り専用プロパティ（Inspector表示用・OnValidateで自動更新）
            [Header("表示用（読み取り専用）")]
            [Tooltip("選択された移動パターンの名前（自動表示・読み取り専用）")]
            [SerializeField] private string moveTypeNameDisplay = "未設定";

            public string MoveTypeNameDisplay => moveTypeNameDisplay;
            
            // 内部メソッド：OnValidateから呼び出される
            public void UpdateMoveTypeNameDisplay(string name)
            {
                moveTypeNameDisplay = string.IsNullOrEmpty(name) ? "未設定" : name;
            }
        }

        [Header("Sequence Settings (順番移動設定)")]
        [Tooltip("【重要】各要素で「対応する移動パターン」を選択して、移動順序を設定します。\n" +
                 "・各要素の「Move Type Index」で、使用するMove Typesのインデックスを選択\n" +
                 "・同じMove Typesを複数回選択可能\n" +
                 "・配列の＋－ボタンで要素数を自由に追加/削除できます\n" +
                 "・各要素内の「表示用（読み取り専用）」フィールドで、選択された移動パターン名を確認できます")]
        public SequenceEntry[] sequenceEntries;

        [Header("Probability Settings (割合移動設定)")]
        [Tooltip("【重要】各要素で「対応する移動パターン」を選択して、適用確率を設定します。\n" +
                 "・各要素の「Move Type Index」で、使用するMove Typesのインデックスを選択\n" +
                 "・同じMove Typesを複数回選択可能\n" +
                 "・配列の＋－ボタンで要素数を自由に追加/削除できます\n" +
                 "・各要素の「Probability Percentage」で適用確率（0～100%）を設定\n" +
                 "・合計が100%でなくても動作します（相対確率として扱います）\n" +
                 "・各要素内の「表示用（読み取り専用）」フィールドで、選択された移動パターン名を確認できます")]
        public ProbabilityEntry[] probabilityEntries;
    }

    [Header("Move Firing Routine (移動ルーチン)")]
    [Tooltip("ON: 移動ルーチンを使用して移動パターンの使い分けを行う。OFF: 従来の順番移動（Sequence）を使用")]
    public bool useMoveFiringRoutine = false;
    public MoveFiringRoutine moveFiringRoutine;

    [Header("Shoot")]
    public float fireInterval = 1.5f;
    public Vector2 fireDirection = Vector2.down;

    [Tooltip("ON: 射撃時にプレイヤー（Playerタグ）の方向を向くように回転します。\n" +
             "スプライトは下向き（Vector2.down）がデフォルトの向きと想定されます。\n" +
             "Bullet Typeの Aim Mode が TowardPlayer の場合に有効です。")]
    public bool rotateTowardPlayer = false;

    [Header("Bullet Collision Settings")]
    [Tooltip("未反射弾（IsReflected=false）の敵への当たり判定を無効にする時間（秒）。\n" +
             "発射直後に敵の動きによって弾が触れて軌道がずれたりSEが鳴るのを防ぎます。\n" +
             "白線・赤線（PaddleDot）や壁との判定は通常通り有効です。\n" +
             "0以下に設定すると無効化しません。")]
    public float unreflectedBulletCollisionDisableTime = 0.2f;

    [Header("Shoot FX (on fire)")]
    public AudioClip fireSE;
    [Range(0f, 1f)] public float fireSEVolume = 1f;
    public GameObject fireVfxPrefab;

    [Header("Bullet (Legacy / Fallback)")]
    public float bulletSpeed = 6f;
    public float bulletLifeTime = 5f;
    public Sprite bulletSpriteOverride;

    [System.Serializable]
    public class BulletType
    {
        // =========================================================
        // 1. Identity
        // =========================================================
        [Header("Identity")]
        public string name = "Type";

        // =========================================================
        // 2. Debug Display
        // =========================================================
        [Header("Debug Display")]
        [Tooltip("ON: 弾の上にデバッグテキスト（速度/加速/反射回数）を表示する。OFF: 非表示")]
        public bool showDebugText = true;

        // =========================================================
        // 3. Visual Override
        // =========================================================
        [Header("Visual Override")]
        public Sprite spriteOverride;

        public bool useColorOverride = false;
        public Color colorOverride = Color.white;

        public bool useScaleOverride = false;
        public Vector2 scaleOverride = Vector2.one;

        // =========================================================
        // 4. Optional: VFX/SE (Type Override)
        // =========================================================
        [Header("Optional: VFX/SE (Type Override)")]
        public GameObject paddleHitVfxPrefab;
        public GameObject wallHitVfxPrefab;
        public GameObject enemyHitVfxPrefab;
        public GameObject justPoweredVfxPrefab;
        public GameObject disappearVfxPrefab;

        public AudioClip wallHitClipA;
        public AudioClip wallHitClipB;

        public AudioClip destroyClipA;
        public AudioClip destroyClipB;
        public AudioClip destroyClipC;

        // =========================================================
        // 5. Collider (Circle)
        // =========================================================
        [Header("Collider (Circle)")]
        public float circleRadius = -1f;

        // =========================================================
        // 6. Paddle Bounce Limit
        // =========================================================
        [Header("Paddle Bounce Limit")]
        [Tooltip("-1: 無制限（デフォルト）、0以上: 指定回数まで反射可能。Bullet Typesの設定が優先される")]
        public int paddleBounceLimit = -1;

        // =========================================================
        // 7. Aim Mode
        // =========================================================
        public enum AimMode
        {
            UseFireDirection,
            TowardRandomPointInPlayerRange,
            TowardPlayer
        }

        [Header("Aim Mode")]
        [Tooltip("UseFireDirection: fireDirection使用 / TowardRandomPointInPlayerRange: PixelDancer移動範囲内のランダム座標へ / TowardPlayer: プレイヤー（Playerタグ）を狙う")]
        public AimMode aimMode = AimMode.UseFireDirection;

        // =========================================================
        // 8. Core
        // =========================================================
        [Header("Core")]
        public float speed = 6f;
        public float lifeTime = 5f;

        // =========================================================
        // 9. Damage
        // =========================================================
        [Header("Damage")]
        [Tooltip("PixelDancerへのダメージ量（デフォルト1）")]
        public int damage = 1;

        // =========================================================
        // 10. Penetration
        // =========================================================
        [Header("Penetration")]
        public int penetration = -1;

        // =========================================================
        // 11. Fire Interval Override (Optional)
        // =========================================================
        [Header("Fire Interval Override (Optional)")]
        public bool useFireIntervalOverride = false;
        public float fireIntervalOverride = 1.5f;

        // =========================================================
        // 12. Fire Interval Random (Optional)
        // =========================================================
        [Header("Fire Interval Random (Optional)")]
        public bool useFireIntervalRandom = false;
        public float fireIntervalRandomRangeSeconds = 0.2f;
        public float fireIntervalMinSeconds = 0.05f;

        // =========================================================
        // 13. Fire/Pause Cycle (Optional)
        // =========================================================
        [Header("Fire/Pause Cycle (Optional)")]
        public bool useFirePauseCycle = false;
        public float fireCycleSeconds = 2f;
        public float pauseCycleSeconds = 1f;

        // =========================================================
        // 14. Speed Curve (Optional)
        // =========================================================
        [Header("Speed Curve (Optional)")]
        public bool useSpeedCurve = false;
        public float initialSpeed = 2f;
        public float maxSpeed = 8f;
        public float curveDuration = 1.5f;
        public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // =========================================================
        // 15. Wave Motion (Optional)
        // =========================================================
        [Header("Wave Motion (Optional)")]
        [Tooltip("ON: 弾が左右に揺れるWave軌道で前進。OFF: 直進。")]
        public bool useWaveMotion = false;

        [Tooltip("Wave振幅（左右の揺れ幅、Unity単位）。例: 1.0 = 左右に±1.0揺れる")]
        [Range(0.1f, 5f)]
        public float waveAmplitude = 1f;

        [Tooltip("Wave周波数（1秒あたりの往復回数）。例: 2.0 = 1秒で2往復")]
        [Range(0.5f, 5f)]
        public float waveFrequency = 2f;

        // =========================================================
        // 16. Spiral Motion (Optional)
        // =========================================================
        [Header("Spiral Motion (Optional)")]
        [Tooltip("ON: 弾が円軌道を描きながらクルクル回転して前進（チャクラムイメージ）。OFF: 直進。")]
        public bool useSpiralMotion = false;

        [Tooltip("螺旋の半径（回転軌道の大きさ、Unity単位）。例: 0.5 = 半径0.5の円を描く")]
        [Range(0.3f, 3f)]
        public float spiralRadius = 0.5f;

        [Tooltip("1周の周期（秒）。例: 0.5 = 0.5秒で1周。速度連動: 速いほど回転も速くなる")]
        [Range(0.2f, 2f)]
        public float spiralPeriod = 0.5f;

        [Tooltip("ON: Spriteも回転させる（チャクラムっぽく）。OFF: 軌道のみ回転")]
        public bool spiralRotateSprite = true;

        // =========================================================
        // 17. Multi Shot (Optional)
        // =========================================================
        [Header("Multi Shot (Optional)")]
        public bool useMultiShot = false;
        public int shotsPerFire = 3;
        [Range(0f, 180f)]
        public float spreadAngleDeg = 180f;

        [Tooltip("弾の初期位置オフセット距離（発射方向に垂直な方向）。0なら重なる、大きいほど離れる")]
        [Range(0f, 2f)]
        public float multiShotSpawnOffset = 0.15f;

        [Tooltip("各弾の発射遅延時間（秒）。0なら同時発射、大きいほど時間差が大きい")]
        [Range(0f, 0.5f)]
        public float multiShotLaunchDelay = 0.05f;

        // =========================================================
        // 18. Telegraph (Pre-Fire Trajectory Line)
        // =========================================================
        [Header("Telegraph (Pre-Fire Trajectory Line)")]
        [Tooltip("ON: 発射前に弾道線を表示してから撃つ。高速弾の理不尽さ対策。")]
        public bool useTelegraph = false;

        [Tooltip("弾道線を表示して待つ秒数。例：0.25〜0.60")]
        public float telegraphSeconds = 0.35f;

        [Tooltip("弾道線の長さ（ワールド）。例：20〜40")]
        public float telegraphLength = 25f;

        [Tooltip("弾道線の太さ（ワールド）。例：0.03〜0.08")]
        public float telegraphWidth = 0.05f;

        [Tooltip("弾道線の色（Alphaも含む）。例：RGBA(1,1,1,0.65)")]
        public Color telegraphColor = new Color(1f, 1f, 1f, 0.65f);

        [Tooltip("ON: 表示中にフェードアウトする（見やすい＆邪魔になりにくい）。")]
        public bool telegraphFadeOut = true;

        [Tooltip("ON: Time.timeScale の影響を受けない（演出/ポーズ中に崩れにくい）。通常はOFFでOK。")]
        public bool telegraphUseUnscaledTime = false;

        // =========================================================
        // 19. Telegraph Blink (Optional)
        // =========================================================
        [Header("Telegraph Blink (Optional)")]
        [Tooltip("ON: フェードではなく点滅させる。")]
        public bool telegraphUseBlink = false;

        [Tooltip("点滅回数（暗転回数）。2なら『2回点滅』。")]
        public int telegraphBlinkCount = 2;

        [Tooltip("点滅の暗い時のAlpha倍率（0なら消える、0.2なら薄く残る）。")]
        [Range(0f, 1f)]
        public float telegraphBlinkMinAlphaMul = 0f;

        // =========================================================
        // 20. MissileArc (Optional)
        // =========================================================
        [Header("MissileArc (Optional)")]
        [Tooltip("ON: ミサイルのように発射直後は遅く直進、途中から加速しながらカーブ、最後は高速直進")]
        public bool useMissileArc = false;

        [Tooltip("初期速度（遅い）。0以下なら speed の50%")]
        public float missileInitialSpeed = 0f;

        [Tooltip("初期直進時間（秒）")]
        [Range(0f, 2.0f)]
        public float missileStraightDuration = 0.3f;

        [Tooltip("カーブ角度（度）。0〜180。実際の膨らみ方向はランダムオプションで決定")]
        [Range(0f, 180f)]
        public float missileCurveAngle = 90f;

        [Tooltip("ON: カーブ方向（左/右）をランダムにする")]
        public bool missileCurveRandomDirection = false;

        [Tooltip("カーブ時間（秒）")]
        [Range(0.1f, 3.0f)]
        public float missileCurveDuration = 0.5f;

        [Tooltip("最終速度（速い）。0以下なら speed の150%。SpeedCurve使用時は無視される")]
        public float missileFinalSpeed = 0f;

        [Tooltip("ON: 加速カーブを使用")]
        public bool missileUseSpeedCurve = false;

        [Tooltip("SpeedCurve用の初期速度。0以下なら speed の50%")]
        public float missileCurveInitialSpeed = 0f;

        [Tooltip("SpeedCurve用の最終速度。0以下なら speed の150%")]
        public float missileCurveFinalSpeed = 0f;

        [Tooltip("加速カーブ（t=0で初期速度、t=1で最終速度）")]
        public AnimationCurve missileSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("ON: 着弾位置にランダムオフセットを追加")]
        public bool missileUseRandomOffset = false;

        [Tooltip("ランダムオフセットの最大距離（Unity単位）")]
        [Range(0f, 5f)]
        public float missileRandomOffsetRadius = 1f;

        // =========================================================
        // 21. Countdown Explosion (Optional)
        // =========================================================
        [Header("Countdown Explosion (Optional)")]
        [Tooltip("ON: 生成直後から爆発タイマー開始し、時間で範囲爆発する")]
        public bool useCountdownExplosion = false;

        [Tooltip("爆発までの秒数（生成直後からカウント）")]
        public float explosionDelaySeconds = 2.0f;

        [Tooltip("爆発半径（ワールド座標）")]
        public float explosionRadius = 1.25f;

        [Tooltip("敵(EnemyDamageReceiver)への爆発ダメージ")]
        public int explosionDamageToEnemy = 2;

        [Tooltip("壁(WallHealth)への爆発ダメージ")]
        public int explosionDamageToWall = 1;

        // =========================================================
        // 22. MultiWarhead (Optional)
        // =========================================================
        [Header("MultiWarhead (Optional)")]
        [Tooltip("ON: 親弾がmultiSlowSeconds秒で消滅し、3発の子弾（A直進/B半弧/C逆半弧）を生成")]
        public bool useMultiWarhead = false;

        [Tooltip("親弾の低速移動時間（秒）。この時間で親弾消滅")]
        public float multiSlowSeconds = 1.5f;

        [Tooltip("親弾の低速時の速度")]
        public float multiSlowSpeed = 2f;

        [Tooltip("親弾Sprite")]
        public Sprite multiParentSprite;

        [Tooltip("親弾SpeedCurve使用")]
        public bool multiParentUseSpeedCurve = false;

        [Tooltip("親弾初速")]
        public float multiParentInitialSpeed = 2f;

        [Tooltip("親弾最大速")]
        public float multiParentMaxSpeed = 2f;

        [Tooltip("親弾カーブ時間")]
        public float multiParentCurveDuration = 1f;

        [Tooltip("親弾SpeedCurve")]
        public AnimationCurve multiParentSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("親弾消滅SE")]
        public AudioClip multiParentVanishSe;

        [Tooltip("親弾消滅VFX")]
        public GameObject multiParentVanishVfx;

        // =========================================================
        // 23. MultiWarhead Child Spawn Position
        // =========================================================
        [Header("MultiWarhead Child Spawn Position")]
        [Tooltip("子弾A/Bの横方向オフセット（親弾位置からX±offset）")]
        public float multiChildOffsetX = 0.3f;

        // =========================================================
        // 24. MultiWarhead Child MissileArc Speed
        // =========================================================
        [Header("MultiWarhead Child MissileArc Speed")]
        [Tooltip("子弾の最終速度（Phase3速度）。0以下なら自動計算")]
        public float multiChildFinalSpeed = 8f;

        // =========================================================
        // 25. MultiWarhead Child MissileArc Random Offset
        // =========================================================
        [Header("MultiWarhead Child MissileArc Random Offset")]
        [Tooltip("ON: 子弾の着弾位置にランダムオフセットを追加")]
        public bool multiChildUseRandomOffset = false;

        [Tooltip("子弾ランダムオフセットの最大距離（Unity単位）")]
        [Range(0f, 5f)]
        public float multiChildRandomOffsetRadius = 1f;

        // =========================================================
        // 26. MultiWarhead Child A (左膨らみ MissileArc)
        // =========================================================
        [Header("MultiWarhead Child A (左膨らみ MissileArc)")]
        [Tooltip("子弾A出現遅延（秒）- ランダム範囲の最小値")]
        public float multiChildA_Delay = 0f;

        [Tooltip("子弾A出現遅延ランダム範囲の最大値（秒）。Delayより大きい値を設定すると、Delay～DelayMaxの範囲でランダム化")]
        public float multiChildA_DelayMax = 0f;

        [Tooltip("子弾A出現SE")]
        public AudioClip multiChildA_SpawnSe;

        [Tooltip("子弾A出現VFX")]
        public GameObject multiChildA_SpawnVfx;

        [Tooltip("子弾ASprite")]
        public Sprite multiChildA_Sprite;

        [Tooltip("子弾Aライフタイム（秒）")]
        public float multiChildA_LifeTime = 5f;

        // =========================================================
        // 27. MultiWarhead Child B (右膨らみ MissileArc)
        // =========================================================
        [Header("MultiWarhead Child B (右膨らみ MissileArc)")]
        [Tooltip("子弾B出現遅延（秒）- ランダム範囲の最小値")]
        public float multiChildB_Delay = 0.1f;

        [Tooltip("子弾B出現遅延ランダム範囲の最大値（秒）。Delayより大きい値を設定すると、Delay～DelayMaxの範囲でランダム化")]
        public float multiChildB_DelayMax = 0.1f;

        [Tooltip("子弾B出現SE")]
        public AudioClip multiChildB_SpawnSe;

        [Tooltip("子弾B出現VFX")]
        public GameObject multiChildB_SpawnVfx;

        [Tooltip("子弾BSprite")]
        public Sprite multiChildB_Sprite;

        [Tooltip("子弾Bライフタイム（秒）")]
        public float multiChildB_LifeTime = 5f;

        // =========================================================
        // 28. Smoke Grenade (Optional)
        // =========================================================
        [Header("Smoke Grenade (Optional)")]
        [Tooltip("ON: 煙幕弾として機能する。反射時に煙を拡散")]
        public bool useSmokeGrenade = false;

        [Tooltip("煙の拡散半径（Unity単位）")]
        [Range(1f, 10f)]
        public float smokeRadius = 3f;

        [Tooltip("煙の持続時間（秒）")]
        [Range(1f, 10f)]
        public float smokeDuration = 5f;

        [Tooltip("煙の拡散速度（半径/秒）")]
        [Range(0.1f, 3f)]
        public float smokeExpansionSpeed = 0.5f;

        [Tooltip("煙のパーティクルプレハブ（SmokeParticle Prefab）")]
        public GameObject smokeParticlePrefab;

        [Tooltip("煙幕弾の反射音")]
        public AudioClip smokeReflectSE;

        [Tooltip("煙幕弾が円判定で消滅する時のVFX（未設定なら出ない）")]
        public GameObject smokeCircleDissolveFx;

        [Tooltip("煙幕弾が円判定で消滅する時のSE（未設定なら鳴らない）")]
        public AudioClip smokeCircleDissolveSE;

        [Tooltip("煙（SmokeCloud）が円判定で消滅する時のSE（未設定なら鳴らない）")]
        public AudioClip smokeCloudCircleDissolveSE;

        // =========================================================
        // 29. Warp (Optional)
        // =========================================================
        [Header("Warp (Optional)")]
        [Tooltip("ON: 発射後に弾が消滅し、X方向ワープして再出現する")]
        public bool useWarp = false;

        [Tooltip("発射後、この秒数で弾が消える（Collider OFF / Sprite OFF）")]
        public float warpDisappearAfterSeconds = 1.0f;

        [Tooltip("消えた後、この秒数で弾が出現する（Collider ON / Sprite ON）")]
        public float warpReappearAfterSeconds = 0.5f;

        [Tooltip("出現位置のX座標ランダム範囲（±range）。例：3.0なら左右3単位の範囲")]
        public float warpOffsetXRange = 3.0f;

        [Tooltip("ワープ消滅時のVFX Prefab（未設定なら出ない）")]
        public GameObject warpDisappearVfxPrefab;

        [Tooltip("ワープ出現時のVFX Prefab（未設定なら出ない）")]
        public GameObject warpReappearVfxPrefab;

        [Tooltip("ワープ消滅時のSE（未設定なら鳴らない）")]
        public AudioClip warpDisappearSe;

        [Tooltip("ワープ出現時のSE（未設定なら鳴らない）")]
        public AudioClip warpReappearSe;
    }

    [Header("Bullet Types (Optional)")]
    [Tooltip("【重要】ここで弾の種類を定義します。\n" +
             "・「Size」を設定すると、その数の弾タイプを設定できます（例: Size=2 → Element 0とElement 1が表示される）\n" +
             "・各Elementを展開して、速度・ダメージ・見た目などの具体的な設定を行います\n" +
             "・この配列の「Size」を変更すると、「Sequence Entries」と「Probability Percentages」のサイズも自動調整されます\n" +
             "・空なら従来どおり bulletSpeed/bulletLifeTime 等を使います")]
    public BulletType[] bulletTypes;

    // =========================================================
    // Bullet Firing Routine (行動ルーチン)
    // =========================================================
    [System.Serializable]
    public class BulletFiringRoutine
    {
        public enum RoutineType
        {
            Sequence,      // 順番発射
            Probability    // 割合発射
        }

        [Header("Routine Type")]
        [Tooltip("順番発射: 指定した順番で各タイプをN回発射してループ / 割合発射: 各タイプを確率で発射")]
        public RoutineType routineType = RoutineType.Sequence;

        [System.Serializable]
        public class SequenceEntry
        {
            [Header("対応する弾タイプ")]
            [Tooltip("この設定が適用される弾タイプを選択します。\n" +
                     "「Bullet Types」配列のインデックス（Element番号）を選択してください。\n" +
                     "例: 0 = Bullet Types[0]（Element 0）、1 = Bullet Types[1]（Element 1）")]
            [Range(0, 99)]
            public int bulletTypeIndex = 0;

            [Header("発射回数設定")]
            [Tooltip("この弾タイプを発射する回数の最小値")]
            public int minShots = 1;
            [Tooltip("この弾タイプを発射する回数の最大値（minShots以上を推奨）")]
            public int maxShots = 1;

            // 読み取り専用プロパティ（Inspector表示用・OnValidateで自動更新）
            [Header("表示用（読み取り専用）")]
            [Tooltip("選択された弾タイプの名前（自動表示・読み取り専用）")]
            [SerializeField] private string bulletTypeNameDisplay = "未設定";

            public string BulletTypeNameDisplay => bulletTypeNameDisplay;
            
            // 内部メソッド：OnValidateから呼び出される
            public void UpdateBulletTypeNameDisplay(string name)
            {
                bulletTypeNameDisplay = string.IsNullOrEmpty(name) ? "未設定" : name;
            }
        }

        [System.Serializable]
        public class ProbabilityEntry
        {
            [Header("対応する弾タイプ")]
            [Tooltip("この設定が適用される弾タイプを選択します。\n" +
                     "「Bullet Types」配列のインデックス（Element番号）を選択してください。\n" +
                     "例: 0 = Bullet Types[0]（Element 0）、1 = Bullet Types[1]（Element 1）")]
            [Range(0, 99)]
            public int bulletTypeIndex = 0;

            [Header("発射確率設定")]
            [Tooltip("この弾タイプの発射確率（0～100%）。合計が100%でなくても動作します（相対確率として扱います）")]
            [Range(0f, 100f)]
            public float probabilityPercentage = 50f;

            // 読み取り専用プロパティ（Inspector表示用・OnValidateで自動更新）
            [Header("表示用（読み取り専用）")]
            [Tooltip("選択された弾タイプの名前（自動表示・読み取り専用）")]
            [SerializeField] private string bulletTypeNameDisplay = "未設定";

            public string BulletTypeNameDisplay => bulletTypeNameDisplay;
            
            // 内部メソッド：OnValidateから呼び出される
            public void UpdateBulletTypeNameDisplay(string name)
            {
                bulletTypeNameDisplay = string.IsNullOrEmpty(name) ? "未設定" : name;
            }
        }

        [Header("Sequence Settings (順番発射設定)")]
        [Tooltip("【重要】各要素で「対応する弾タイプ」を選択して、発射順序を設定します。\n" +
                 "・各要素の「Bullet Type Index」で、使用するBullet Typesのインデックスを選択\n" +
                 "・同じBullet Typesを複数回選択可能（例: 弾1>弾2>弾2>ループ）\n" +
                 "・配列の＋－ボタンで要素数を自由に追加/削除できます\n" +
                 "・各要素内の「表示用（読み取り専用）」フィールドで、選択された弾タイプ名を確認できます")]
        public SequenceEntry[] sequenceEntries;

        [Header("Probability Settings (割合発射設定)")]
        [Tooltip("【重要】各要素で「対応する弾タイプ」を選択して、発射確率を設定します。\n" +
                 "・各要素の「Bullet Type Index」で、使用するBullet Typesのインデックスを選択\n" +
                 "・同じBullet Typesを複数回選択可能\n" +
                 "・配列の＋－ボタンで要素数を自由に追加/削除できます\n" +
                 "・各要素の「Probability Percentage」で発射確率（0～100%）を設定\n" +
                 "・合計が100%でなくても動作します（相対確率として扱います）\n" +
                 "・各要素内の「表示用（読み取り専用）」フィールドで、選択された弾タイプ名を確認できます")]
        public ProbabilityEntry[] probabilityEntries;
    }

    [Header("Bullet Firing Routine (行動ルーチン)")]
    [Tooltip("ON: 行動ルーチンを使用して弾の使い分けを行う。OFF: 従来の選択モード（Random/RoundRobin/WeightedChance）を使用")]
    public bool useFiringRoutine = false;
    public BulletFiringRoutine firingRoutine;

    // =========================================================
    // HP-Based Routine Switching (HP％に応じたルーチン切り替え)
    // =========================================================
    [Header("HP-Based Routine Switching (HP％に応じたルーチン切り替え)")]
    [Tooltip("ON: HP％に応じて移動・射撃ルーチンを切り替える。OFF: 通常のルーチンを使用")]
    public bool useHpBasedRoutineSwitch = false;

    [Tooltip("HP％の閾値（0～100）。このHP％を下回ったら低HP用ルーチンに切り替わる（一度切り替わったら戻らない）")]
    [Range(0f, 100f)]
    public float hpThresholdPercentage = 50f;

    [Header("Move Routines (4 slots)")]
    [Tooltip("移動ルーチンを4つ設定できます。Sequence_01, Sequence_02, Probability_01, Probability_02の順で設定してください")]
    public MoveFiringRoutine[] moveFiringRoutines = new MoveFiringRoutine[4];

    public enum RoutineSlot
    {
        Sequence_01 = 0,
        Sequence_02 = 1,
        Probability_01 = 2,
        Probability_02 = 3
    }

    [Header("Move Routine Selection")]
    [Tooltip("HP閾値以上で使用する移動ルーチン")]
    public RoutineSlot moveRoutineAboveThreshold = RoutineSlot.Sequence_01;
    [Tooltip("HP閾値未満で使用する移動ルーチン")]
    public RoutineSlot moveRoutineBelowThreshold = RoutineSlot.Probability_01;

    [Header("Bullet Routines (4 slots)")]
    [Tooltip("射撃ルーチンを4つ設定できます。Sequence_01, Sequence_02, Probability_01, Probability_02の順で設定してください")]
    public BulletFiringRoutine[] bulletFiringRoutines = new BulletFiringRoutine[4];

    [Header("Bullet Routine Selection")]
    [Tooltip("HP閾値以上で使用する射撃ルーチン")]
    public RoutineSlot bulletRoutineAboveThreshold = RoutineSlot.Sequence_01;
    [Tooltip("HP閾値未満で使用する射撃ルーチン")]
    public RoutineSlot bulletRoutineBelowThreshold = RoutineSlot.Probability_01;

    // =========================================================
    // Update bullet type name displays (弾タイプ名表示の更新)
    // =========================================================
    private void OnValidate()
    {
        // =========================================================
        // HP-Based Routine Switching: 4スロットの配列を自動初期化
        // =========================================================

        // Move Routines (4 slots)の初期化
        if (moveFiringRoutines == null || moveFiringRoutines.Length != 4)
        {
            moveFiringRoutines = new MoveFiringRoutine[4];
        }

        // 各スロットがnullなら新規インスタンスを作成
        for (int i = 0; i < moveFiringRoutines.Length; i++)
        {
            if (moveFiringRoutines[i] == null)
            {
                moveFiringRoutines[i] = new MoveFiringRoutine();
            }

            // 中身の配列も初期化（nullの場合のみ空配列を設定）
            if (moveFiringRoutines[i].sequenceEntries == null)
            {
                moveFiringRoutines[i].sequenceEntries = new MoveFiringRoutine.SequenceEntry[0];
            }
            if (moveFiringRoutines[i].probabilityEntries == null)
            {
                moveFiringRoutines[i].probabilityEntries = new MoveFiringRoutine.ProbabilityEntry[0];
            }
        }

        // Bullet Routines (4 slots)の初期化
        if (bulletFiringRoutines == null || bulletFiringRoutines.Length != 4)
        {
            bulletFiringRoutines = new BulletFiringRoutine[4];
        }

        // 各スロットがnullなら新規インスタンスを作成
        for (int i = 0; i < bulletFiringRoutines.Length; i++)
        {
            if (bulletFiringRoutines[i] == null)
            {
                bulletFiringRoutines[i] = new BulletFiringRoutine();
            }

            // 中身の配列も初期化（nullの場合のみ空配列を設定）
            if (bulletFiringRoutines[i].sequenceEntries == null)
            {
                bulletFiringRoutines[i].sequenceEntries = new BulletFiringRoutine.SequenceEntry[0];
            }
            if (bulletFiringRoutines[i].probabilityEntries == null)
            {
                bulletFiringRoutines[i].probabilityEntries = new BulletFiringRoutine.ProbabilityEntry[0];
            }
        }

        if (firingRoutine == null) return;
        if (bulletTypes == null || bulletTypes.Length == 0) return;

        // SequenceEntriesの各要素の弾タイプ名を更新
        if (firingRoutine.sequenceEntries != null)
        {
            for (int i = 0; i < firingRoutine.sequenceEntries.Length; i++)
            {
                var entry = firingRoutine.sequenceEntries[i];
                if (entry == null) continue;

                int idx = Mathf.Clamp(entry.bulletTypeIndex, 0, bulletTypes.Length - 1);
                if (idx >= 0 && idx < bulletTypes.Length && bulletTypes[idx] != null)
                {
                    string typeName = !string.IsNullOrEmpty(bulletTypes[idx].name) 
                        ? bulletTypes[idx].name 
                        : $"Bullet Types[{idx}]";
                    entry.UpdateBulletTypeNameDisplay(typeName);
                }
                else
                {
                    entry.UpdateBulletTypeNameDisplay("無効なインデックス");
                }
            }
        }

        // ProbabilityEntriesの各要素の弾タイプ名を更新
        if (firingRoutine.probabilityEntries != null)
        {
            for (int i = 0; i < firingRoutine.probabilityEntries.Length; i++)
            {
                var entry = firingRoutine.probabilityEntries[i];
                if (entry == null) continue;

                int idx = Mathf.Clamp(entry.bulletTypeIndex, 0, bulletTypes.Length - 1);
                if (idx >= 0 && idx < bulletTypes.Length && bulletTypes[idx] != null)
                {
                    string typeName = !string.IsNullOrEmpty(bulletTypes[idx].name) 
                        ? bulletTypes[idx].name 
                        : $"Bullet Types[{idx}]";
                    entry.UpdateBulletTypeNameDisplay(typeName);
                }
                else
                {
                    entry.UpdateBulletTypeNameDisplay("無効なインデックス");
                }
            }
        }

        // MoveFiringRoutineの移動パターン名を更新
        if (moveFiringRoutine == null) return;
        if (moveTypes == null || moveTypes.Length == 0) return;

        // SequenceEntriesの各要素の移動パターン名を更新
        if (moveFiringRoutine.sequenceEntries != null)
        {
            for (int i = 0; i < moveFiringRoutine.sequenceEntries.Length; i++)
            {
                var entry = moveFiringRoutine.sequenceEntries[i];
                if (entry == null) continue;

                int idx = Mathf.Clamp(entry.moveTypeIndex, 0, moveTypes.Length - 1);
                if (idx >= 0 && idx < moveTypes.Length && moveTypes[idx] != null)
                {
                    string typeName = !string.IsNullOrEmpty(moveTypes[idx].name) 
                        ? moveTypes[idx].name 
                        : $"Move Types[{idx}]";
                    entry.UpdateMoveTypeNameDisplay(typeName);
                }
                else
                {
                    entry.UpdateMoveTypeNameDisplay("無効なインデックス");
                }
            }
        }

        // ProbabilityEntriesの各要素の移動パターン名を更新
        if (moveFiringRoutine.probabilityEntries != null)
        {
            for (int i = 0; i < moveFiringRoutine.probabilityEntries.Length; i++)
            {
                var entry = moveFiringRoutine.probabilityEntries[i];
                if (entry == null) continue;

                int idx = Mathf.Clamp(entry.moveTypeIndex, 0, moveTypes.Length - 1);
                if (idx >= 0 && idx < moveTypes.Length && moveTypes[idx] != null)
                {
                    string typeName = !string.IsNullOrEmpty(moveTypes[idx].name) 
                        ? moveTypes[idx].name 
                        : $"Move Types[{idx}]";
                    entry.UpdateMoveTypeNameDisplay(typeName);
                }
                else
                {
                    entry.UpdateMoveTypeNameDisplay("無効なインデックス");
                }
            }
        }

        // =========================================================
        // HP-Based Routine Switching: 4スロットのルーチン名表示を更新
        // =========================================================

        // Move Routines (4 slots)の名前表示を更新
        if (moveFiringRoutines != null && moveTypes != null && moveTypes.Length > 0)
        {
            for (int slotIndex = 0; slotIndex < moveFiringRoutines.Length; slotIndex++)
            {
                var routine = moveFiringRoutines[slotIndex];
                if (routine == null) continue;

                // SequenceEntriesの名前表示を更新
                if (routine.sequenceEntries != null)
                {
                    for (int i = 0; i < routine.sequenceEntries.Length; i++)
                    {
                        var entry = routine.sequenceEntries[i];
                        if (entry == null) continue;

                        int idx = Mathf.Clamp(entry.moveTypeIndex, 0, moveTypes.Length - 1);
                        if (idx >= 0 && idx < moveTypes.Length && moveTypes[idx] != null)
                        {
                            string typeName = !string.IsNullOrEmpty(moveTypes[idx].name)
                                ? moveTypes[idx].name
                                : $"Move Types[{idx}]";
                            entry.UpdateMoveTypeNameDisplay(typeName);
                        }
                        else
                        {
                            entry.UpdateMoveTypeNameDisplay("無効なインデックス");
                        }
                    }
                }

                // ProbabilityEntriesの名前表示を更新
                if (routine.probabilityEntries != null)
                {
                    for (int i = 0; i < routine.probabilityEntries.Length; i++)
                    {
                        var entry = routine.probabilityEntries[i];
                        if (entry == null) continue;

                        int idx = Mathf.Clamp(entry.moveTypeIndex, 0, moveTypes.Length - 1);
                        if (idx >= 0 && idx < moveTypes.Length && moveTypes[idx] != null)
                        {
                            string typeName = !string.IsNullOrEmpty(moveTypes[idx].name)
                                ? moveTypes[idx].name
                                : $"Move Types[{idx}]";
                            entry.UpdateMoveTypeNameDisplay(typeName);
                        }
                        else
                        {
                            entry.UpdateMoveTypeNameDisplay("無効なインデックス");
                        }
                    }
                }
            }
        }

        // Bullet Routines (4 slots)の名前表示を更新
        if (bulletFiringRoutines != null && bulletTypes != null && bulletTypes.Length > 0)
        {
            for (int slotIndex = 0; slotIndex < bulletFiringRoutines.Length; slotIndex++)
            {
                var routine = bulletFiringRoutines[slotIndex];
                if (routine == null) continue;

                // SequenceEntriesの名前表示を更新
                if (routine.sequenceEntries != null)
                {
                    for (int i = 0; i < routine.sequenceEntries.Length; i++)
                    {
                        var entry = routine.sequenceEntries[i];
                        if (entry == null) continue;

                        int idx = Mathf.Clamp(entry.bulletTypeIndex, 0, bulletTypes.Length - 1);
                        if (idx >= 0 && idx < bulletTypes.Length && bulletTypes[idx] != null)
                        {
                            string typeName = !string.IsNullOrEmpty(bulletTypes[idx].name)
                                ? bulletTypes[idx].name
                                : $"Bullet Types[{idx}]";
                            entry.UpdateBulletTypeNameDisplay(typeName);
                        }
                        else
                        {
                            entry.UpdateBulletTypeNameDisplay("無効なインデックス");
                        }
                    }
                }

                // ProbabilityEntriesの名前表示を更新
                if (routine.probabilityEntries != null)
                {
                    for (int i = 0; i < routine.probabilityEntries.Length; i++)
                    {
                        var entry = routine.probabilityEntries[i];
                        if (entry == null) continue;

                        int idx = Mathf.Clamp(entry.bulletTypeIndex, 0, bulletTypes.Length - 1);
                        if (idx >= 0 && idx < bulletTypes.Length && bulletTypes[idx] != null)
                        {
                            string typeName = !string.IsNullOrEmpty(bulletTypes[idx].name)
                                ? bulletTypes[idx].name
                                : $"Bullet Types[{idx}]";
                            entry.UpdateBulletTypeNameDisplay(typeName);
                        }
                        else
                        {
                            entry.UpdateBulletTypeNameDisplay("無効なインデックス");
                        }
                    }
                }
            }
        }
    }
}
