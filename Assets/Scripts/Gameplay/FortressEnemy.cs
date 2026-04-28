using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// フォートレスエネミー（ブロック要塞型中ボス）の移動・ブロック配置を管理する。
///
/// ■ 弾発射の仕組みについて（重要メモ）
///   このエネミーの弾発射は EnemyShooter コンポーネントが担当するが、
///   弾の種類・速度・発射パターン等のパラメータはすべて
///   EnemyData (ScriptableObject) の BulletFiringRoutine で定義・管理される。
///   弾の挙動を変更したい場合は EnemyData アセットを編集すること。
///   EnemyShooter.cs や FortressEnemy.cs を直接編集する必要はない。
///
/// ■ ブロック配置パターン
///   Pattern 1: 敵周囲を時計回り/反時計回りに回転する浮遊ブロック群（1つずつ出現）
///   Pattern 2: 画面最下端に水平一直線に並ぶ壁ブロック・ドミノ方式（SkillHUD幅を除外）
///   Pattern 3: 画面内にランダム散布するブロック（1つずつ出現・均等分散）
///   Pattern 4: 画面左右端に縦一直線に並ぶ壁ブロック（SkillHUD幅を除外した左壁＋右壁）
/// </summary>
public class FortressEnemy : MonoBehaviour
{
    // =========================================================
    // Inspector フィールド
    // =========================================================

    [Header("Movement")]
    [Tooltip("スポット間の移動速度（ワールド単位/秒）")]
    [SerializeField] private float moveSpeed = 0.8f;

    [Tooltip("スポット到着後の待機時間（秒）")]
    [SerializeField] private float spotDwellTime = 2.5f;

    [Tooltip("スポット到着判定の距離しきい値")]
    [SerializeField] private float arrivalThreshold = 0.15f;

    [Header("Movement: Screen Zone")]
    [Tooltip("画面を ZoneColumns × ZoneRows のグリッドに分割して均等に巡回する。\n" +
             "全ゾーンを1周したらシャッフルして再度巡回する。")]
    [SerializeField] private int zoneColumns = 3;

    [SerializeField] private int zoneRows = 3;

    [Tooltip("ゾーン内でスポットをランダムにずらす割合\n" +
             "0=ゾーン中央に固定, 1=ゾーン全域でランダム")]
    [Range(0f, 1f)]
    [SerializeField] private float spotJitter = 0.7f;

    [Tooltip("画面左右端からの余白（ワールド単位）\n画面外に出ないよう内側にオフセット")]
    [SerializeField] private float screenPaddingX = 1.0f;

    [Tooltip("画面上下端からの余白（ワールド単位）")]
    [SerializeField] private float screenPaddingY = 1.0f;

    [Header("Movement: Player Avoidance")]
    [Tooltip("画面下端からこの割合より下のエリアを移動禁止にする（0〜1）\n" +
             "0.4 なら下40%には移動しない（プレイヤー/Floor付近を確実に除外できる）")]
    [Range(0f, 0.9f)]
    [SerializeField] private float movementMinYFraction = 0.4f;

    [Tooltip("ゾーン内のスポット候補がプレイヤーからこの距離以内なら再試行する（ワールド単位）\n" +
             "0で無効。movementMinYFractionと組み合わせて使う。")]
    [SerializeField] private float playerAvoidRadius = 2f;

    [Header("Movement: World Y Range Constraint")]
    [Tooltip("ON: worldYMin より下のエリアには移動しない（0=画面中央）")]
    [SerializeField] private bool useWorldYMinConstraint = false;
    [Tooltip("移動下限のワールドY座標（0=X軸=画面中央）")]
    [SerializeField] private float worldYMin = 0f;

    [Tooltip("ON: worldYMax より上のエリアには移動しない")]
    [SerializeField] private bool useWorldYMaxConstraint = false;
    [Tooltip("移動上限のワールドY座標（カメラ上端より小さい値を指定）")]
    [SerializeField] private float worldYMax = 3f;

    // ----------------------------------------------------------

    [Header("Pattern 1: Orbit Blocks")]
    [Tooltip("オービットブロックのPrefab（WallHealthコンポーネント必須）")]
    [SerializeField] private GameObject orbitBlockPrefab;

    [Tooltip("同時に浮遊するブロック数")]
    [SerializeField] private int orbitBlockCount = 4;

    [Tooltip("敵中心からの回転半径（ワールド単位）")]
    [SerializeField] private float orbitRadius = 2f;

    [Tooltip("回転速度（度/秒）")]
    [SerializeField] private float orbitSpeed = 30f;

    [Tooltip("時計回り/反時計回りの切り替え間隔（秒）")]
    [SerializeField] private float directionSwitchInterval = 5f;

    [Tooltip("ゆらゆらの振れ幅（ワールド単位）。0で無効。")]
    [SerializeField] private float wobbleAmount = 0.15f;

    [Tooltip("ゆらゆらの周期速度（大きいほど速い）")]
    [SerializeField] private float wobbleSpeed = 1.5f;

    [Tooltip("1ブロックずつ時計回り順に出現する間隔（秒）")]
    [SerializeField] private float orbitSpawnInterval = 0.1f;

    [Tooltip("オービットブロックの再配置間隔（秒）")]
    [SerializeField] private float pattern1ReplaceInterval = 12f;

    [Tooltip("オービットブロック出現時のSE")]
    [SerializeField] private AudioClip orbitSpawnClip;

    [Tooltip("SE再生用AudioSource（未設定時はPlayClipAtPointで再生）")]
    [SerializeField] private AudioSource orbitSpawnAudioSource;

    [Range(0f, 1f)]
    [Tooltip("オービットブロック出現SEの音量")]
    [SerializeField] private float orbitSpawnVolume = 1f;

    // ----------------------------------------------------------

    [Header("Pattern 2: Bottom Wall")]
    [Tooltip("底面壁ブロックのPrefab（WallHealthコンポーネント必須）")]
    [SerializeField] private GameObject bottomWallBlockPrefab;

    [Tooltip("ブロック1個のワールド幅（敷き詰め間隔の計算に使用）")]
    [SerializeField] private float blockWidth = 1f;

    [Tooltip("SkillHUDの横幅（ピクセル単位）。SkillHUDが見つからない場合のフォールバック値。")]
    [SerializeField] private float skillHudPixelWidth = 280f;

    [Tooltip("画面最下端Y座標からのオフセット（ワールド単位）\n正の値で上に、負の値でさらに下にずらす")]
    [SerializeField] private float bottomWallYOffset = 0f;

    [Tooltip("ドミノ配置の1ブロックあたりの表示間隔（秒）")]
    [SerializeField] private float bottomWallDominoInterval = 0.1f;

    [Tooltip("底面壁ブロックの再配置間隔（秒）")]
    [SerializeField] private float pattern2ReplaceInterval = 18f;

    [Tooltip("底面壁ブロック出現時のSE")]
    [SerializeField] private AudioClip bottomWallSpawnClip;

    [Tooltip("SE再生用AudioSource（未設定時はPlayClipAtPointで再生）")]
    [SerializeField] private AudioSource bottomWallSpawnAudioSource;

    [Range(0f, 1f)]
    [Tooltip("底面壁ブロック出現SEの音量")]
    [SerializeField] private float bottomWallSpawnVolume = 1f;

    // ----------------------------------------------------------

    [Header("Pattern 3: Scatter Blocks")]
    [Tooltip("スキャッターブロックのPrefab（WallHealthコンポーネント必須）")]
    [SerializeField] private GameObject scatterBlockPrefab;

    [Tooltip("配置するブロック数")]
    [SerializeField] private int scatterCount = 6;

    [Tooltip("画面中央周辺の除外半径（ワールド単位）")]
    [SerializeField] private float scatterCenterExcludeRadius = 2.5f;

    [Tooltip("敵自身の周囲の除外半径（ワールド単位）")]
    [SerializeField] private float scatterEnemyExcludeRadius = 2f;

    [Tooltip("プレイヤー（PixelDancer）周囲の除外半径（ワールド単位）")]
    [SerializeField] private float scatterPlayerExcludeRadius = 1.5f;

    [Tooltip("FloorのY上端から上方向への除外高さ（ワールド単位）\nFloor未検出時はカメラ下端を基準にする")]
    [SerializeField] private float scatterFloorExcludeHeight = 2f;

    [Tooltip("1ブロックあたりの配置試行最大回数\n条件を満たす場所が見つからない場合はそのブロックをスキップする")]
    [SerializeField] private int maxScatterAttempts = 25;

    [Tooltip("1ブロックずつランダム順に出現する間隔（秒）")]
    [SerializeField] private float scatterSpawnInterval = 0.1f;

    [Tooltip("ブロックごとに加算する回転角度（度）\n例: 15 → 1個目0°, 2個目15°, 3個目30° ...\n0で全ブロック同じ角度")]
    [SerializeField] private float scatterRotationStep = 15f;

    [Tooltip("回転角度へ加えるランダム幅（度）\n例: 10 → ±5度のランダムオフセットが加わる\n0で完全等間隔")]
    [Range(0f, 180f)]
    [SerializeField] private float scatterRotationVariance = 5f;

    [Tooltip("スキャッターブロックの再配置間隔（秒）")]
    [SerializeField] private float pattern3ReplaceInterval = 15f;

    [Tooltip("スキャッターブロック出現時のSE")]
    [SerializeField] private AudioClip scatterSpawnClip;

    [Tooltip("SE再生用AudioSource（未設定時はPlayClipAtPointで再生）")]
    [SerializeField] private AudioSource scatterSpawnAudioSource;

    [Range(0f, 1f)]
    [Tooltip("スキャッターブロック出現SEの音量")]
    [SerializeField] private float scatterSpawnVolume = 1f;

    // ----------------------------------------------------------

    [Header("Pattern 4: Side Walls")]
    [Tooltip("左右縦壁ブロックのPrefab（WallHealthコンポーネント必須）\n" +
             "Block_BottomWallをベースに複製して使う")]
    [SerializeField] private GameObject sideWallBlockPrefab;

    [Tooltip("ブロック1個のワールド高さ（敷き詰め間隔の計算に使用）")]
    [SerializeField] private float sideBlockHeight = 1f;

    [Tooltip("画面左端X座標からのオフセット（ワールド単位）\n" +
             "SkillHUDの除外は自動計算されるためこれは微調整用\n正の値で右にずらす")]
    [SerializeField] private float sideWallLeftXOffset = 0f;

    [Tooltip("画面右端X座標からのオフセット（ワールド単位）\n正の値で左にずらす")]
    [SerializeField] private float sideWallRightXOffset = 0f;

    [Tooltip("ドミノ配置の1ブロックあたりの表示間隔（秒）")]
    [SerializeField] private float sideWallDominoInterval = 0.1f;

    [Tooltip("ドミノ配置の方向\ntrue=下から上, false=上から下")]
    [SerializeField] private bool sideWallBottomToTop = true;

    [Tooltip("左右縦壁ブロックの再配置間隔（秒）")]
    [SerializeField] private float pattern4ReplaceInterval = 20f;

    [Tooltip("サイドウォールブロック出現時のSE")]
    [SerializeField] private AudioClip sideWallSpawnClip;

    [Tooltip("SE再生用AudioSource（未設定時はPlayClipAtPointで再生）")]
    [SerializeField] private AudioSource sideWallSpawnAudioSource;

    [Range(0f, 1f)]
    [Tooltip("サイドウォールブロック出現SEの音量")]
    [SerializeField] private float sideWallSpawnVolume = 1f;

    // ----------------------------------------------------------

    [Header("Shared Settings")]
    [Tooltip("生成するブロックの親Transform。\n未設定時はシーンルートに配置する。")]
    [SerializeField] private Transform blockRoot;

    [Tooltip("再配置時：古いブロック消去から新しいブロック出現までの待機時間（秒）")]
    [SerializeField] private float replaceDelay = 1f;

    [Tooltip("Interval消去前の点滅回数（Pattern 1/2/3）")]
    [SerializeField] private int blockBlinkCount = 3;

    [Tooltip("点滅1回あたりのOFF/ON時間（秒）")]
    [SerializeField] private float blockBlinkInterval = 0.12f;

    // =========================================================
    // ランタイム変数
    // =========================================================

    private readonly List<GameObject> orbitBlocks = new List<GameObject>();
    private readonly List<GameObject> bottomWallBlocks = new List<GameObject>();
    private readonly List<GameObject> scatterBlocks = new List<GameObject>();
    private readonly List<GameObject> sideWallBlocks = new List<GameObject>();

    // オービット状態
    private float orbitAngle = 0f;
    private float orbitDirection = 1f;  // 1=時計回り, -1=反時計回り
    private float[] wobblePhases = System.Array.Empty<float>();
    // オービット生成時の最終ブロック数（UpdateOrbitPositions の angleStep 計算に使用）
    private int orbitTargetCount = 0;

    // タイマー
    private float directionSwitchTimer;
    private float pattern1Timer;
    private float pattern2Timer;
    private float pattern3Timer;
    private float pattern4Timer;

    // 再配置コルーチン実行中フラグ（重複実行防止）
    private bool pattern1Replacing = false;
    private bool pattern2Replacing = false;
    private bool pattern3Replacing = false;
    private bool pattern4Replacing = false;

    // Pattern 2 ドミノ方向（true=左→右, false=右→左）。再配置ごとに交互切り替え
    private bool bottomWallLeftToRight = true;

    // ゾーン巡回移動
    private int[] zoneOrder;      // シャッフル済みゾーンインデックス配列
    private int zoneIndex = 0;    // 現在のゾーン順序ポインタ
    private Vector3 targetSpot;
    private bool isMoving = false;
    private float dwellTimer = 0f;

    // 参照キャッシュ（Start時に一度取得）
    private PixelDancerController player;
    private FloorHealth floor;
    private RectTransform skillHudCachedRect;
    private EnemyMover enemyMover;

    private float SlowMultiplier => (enemyMover != null) ? enemyMover.SpeedMultiplier : 1f;

    // =========================================================
    // Unityライフサイクル
    // =========================================================

    private void Start()
    {
        player = FindObjectOfType<PixelDancerController>();
        floor  = FindObjectOfType<FloorHealth>();
        var hudGo = GameObject.Find("SkillHUD");
        if (hudGo != null) skillHudCachedRect = hudGo.GetComponent<RectTransform>();
        enemyMover = GetComponentInParent<EnemyMover>();

        directionSwitchTimer = directionSwitchInterval;
        pattern1Timer = pattern1ReplaceInterval;
        pattern2Timer = pattern2ReplaceInterval;
        pattern3Timer = pattern3ReplaceInterval;
        pattern4Timer = pattern4ReplaceInterval;

        StartCoroutine(SpawnOrbitDomino());
        StartCoroutine(SpawnBottomWallDomino());
        StartCoroutine(SpawnScatterDomino());
        StartCoroutine(SpawnSideWallDomino());

        PickNewSpot();
    }

    private void Update()
    {
        UpdateMovement();
        UpdateOrbitPositions();
        UpdateTimers();
    }

    private void OnDestroy()
    {
        DestroyBlockList(orbitBlocks);
        DestroyBlockList(bottomWallBlocks);
        DestroyBlockList(scatterBlocks);
        DestroyBlockList(sideWallBlocks);
    }

    // =========================================================
    // 移動（ゾーン巡回スポット移動）
    // =========================================================

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void UpdateMovement()
    {
        float ts = GetTimeScale() * SlowMultiplier;
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, targetSpot, moveSpeed * Time.deltaTime * ts);

            if (Vector3.Distance(transform.position, targetSpot) <= arrivalThreshold)
            {
                transform.position = targetSpot;
                isMoving = false;
                dwellTimer = spotDwellTime;
            }
        }
        else
        {
            dwellTimer -= Time.deltaTime * ts;
            if (dwellTimer <= 0f)
                PickNewSpot();
        }
    }

    private void PickNewSpot()
    {
        if (Camera.main == null) return;

        int totalZones = zoneColumns * zoneRows;

        // 初回または列数・行数変更時に配列を初期化
        if (zoneOrder == null || zoneOrder.Length != totalZones)
        {
            zoneOrder = BuildShuffledZoneOrder(totalZones);
            zoneIndex = 0;
        }

        // 画面の有効範囲を計算
        float halfH    = Camera.main.orthographicSize;
        float halfW    = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float xMin = Mathf.Max(camPos.x - halfW + screenPaddingX, GetSkillHudRightWorldX(camPos.x, halfW));
        float xMax = camPos.x + halfW - screenPaddingX;

        // movementMinYFraction で画面下部を移動禁止エリアとして確実に除外する。
        // フロアやプレイヤーは常に画面下にいるため、割合指定が最も確実。
        float screenTop    = camPos.y + halfH - screenPaddingY;
        float screenBottom = camPos.y - halfH + screenPaddingY;
        float yMin = screenBottom + (screenTop - screenBottom) * movementMinYFraction;
        if (useWorldYMinConstraint)
            yMin = Mathf.Max(yMin, worldYMin);
        float yMax = screenTop;
        if (useWorldYMaxConstraint)
            yMax = Mathf.Min(yMax, worldYMax);

        float zoneW = (xMax - xMin) / zoneColumns;
        float zoneH = (yMax - yMin) / zoneRows;

        // playerAvoidRadius 用のプレイヤー位置（追加の微調整用）
        Vector2 playerPos = (player != null)
            ? (Vector2)player.transform.position
            : new Vector2(float.MaxValue, float.MaxValue);

        bool found = false;
        for (int pass = 0; pass < totalZones; pass++)
        {
            int zi   = zoneOrder[zoneIndex];
            int col  = zi % zoneColumns;
            int row  = zi / zoneColumns;
            float cx = xMin + zoneW * (col + 0.5f);
            float cy = yMin + zoneH * (row + 0.5f);

            AdvanceZoneIndex(totalZones);

            // ゾーン内の jitter 付きスポットを試行生成。
            // ゾーン中央チェックではなく、実際の生成スポットに対して距離を判定する。
            Vector3? spot = TryPickSpotInZone(cx, cy, zoneW, zoneH, playerPos);
            if (spot.HasValue)
            {
                targetSpot = spot.Value;
                isMoving = true;
                found = true;
                break;
            }
        }

        // 全ゾーンで playerAvoidRadius に引っかかった極端なケース
        // → yMin 制約は維持したまま、最初のゾーンを強制使用して停止を防ぐ
        if (!found)
        {
            int zi   = zoneOrder[zoneIndex];
            int col  = zi % zoneColumns;
            int row  = zi / zoneColumns;
            float cx = xMin + zoneW * (col + 0.5f);
            float cy = yMin + zoneH * (row + 0.5f);
            AdvanceZoneIndex(totalZones);

            float jx = spotJitter * zoneW * 0.5f;
            float jy = spotJitter * zoneH * 0.5f;
            targetSpot = new Vector3(
                Random.Range(cx - jx, cx + jx),
                Random.Range(cy - jy, cy + jy),
                transform.position.z);
            isMoving = true;
        }
    }

    /// <summary>
    /// ゾーン内で jitter 付きのスポット候補を最大8回試行し、
    /// プレイヤーから playerAvoidRadius 以上離れた位置を返す。
    /// すべて失敗した場合は null を返す。
    /// yMin 制約は呼び出し元で保証されているためここでは不要。
    /// </summary>
    private Vector3? TryPickSpotInZone(float cx, float cy, float zoneW, float zoneH, Vector2 playerPos)
    {
        float jx = spotJitter * zoneW * 0.5f;
        float jy = spotJitter * zoneH * 0.5f;

        const int maxAttempts = 8;
        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(cx - jx, cx + jx);
            float y = Random.Range(cy - jy, cy + jy);
            Vector2 candidate = new Vector2(x, y);

            if (playerAvoidRadius > 0f &&
                Vector2.Distance(candidate, playerPos) < playerAvoidRadius)
                continue;

            return new Vector3(x, y, transform.position.z);
        }
        return null;
    }

    /// <summary>Fisher-Yates シャッフル済みのゾーンインデックス配列を生成して返す</summary>
    private static int[] BuildShuffledZoneOrder(int totalZones)
    {
        int[] order = new int[totalZones];
        for (int i = 0; i < totalZones; i++) order[i] = i;
        for (int i = totalZones - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }
        return order;
    }

    /// <summary>zoneIndex を1進め、全ゾーン完走したらシャッフルして先頭に戻る</summary>
    private void AdvanceZoneIndex(int totalZones)
    {
        zoneIndex++;
        if (zoneIndex >= totalZones)
        {
            zoneOrder = BuildShuffledZoneOrder(totalZones);
            zoneIndex = 0;
        }
    }

    // =========================================================
    // Pattern 1: Orbit Blocks（毎フレーム位置更新）
    // =========================================================

    private void UpdateOrbitPositions()
    {
        if (orbitBlocks.Count == 0 || orbitTargetCount == 0) return;

        // orbitTargetCount を基準に angleStep を固定することで、
        // ドミノ出現中も各ブロックが最終位置へ向かって整列する
        float angleStep = 360f / orbitTargetCount;

        for (int i = 0; i < orbitBlocks.Count; i++)
        {
            if (orbitBlocks[i] == null) continue;

            float phase  = (i < wobblePhases.Length) ? wobblePhases[i] : 0f;
            float radius = orbitRadius + Mathf.Sin(Time.time * wobbleSpeed + phase) * wobbleAmount;

            float angleDeg = orbitAngle + angleStep * i;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            orbitBlocks[i].transform.position = transform.position + new Vector3(
                Mathf.Cos(angleRad) * radius,
                Mathf.Sin(angleRad) * radius,
                0f);
        }

        orbitAngle += orbitSpeed * orbitDirection * Time.deltaTime * GetTimeScale() * SlowMultiplier;
    }

    /// <summary>時計回り順（index 0, 1, 2, ...）に1つずつオービットブロックを出現させる</summary>
    private IEnumerator SpawnOrbitDomino()
    {
        if (orbitBlockPrefab == null) yield break;

        // 1フレーム待ってCamera.mainが確実に初期化されてから実行
        yield return null;

        int count = Mathf.Max(orbitBlockCount, 0);
        if (count == 0) yield break;
        orbitTargetCount = count;
        float angleStep = 360f / count;

        wobblePhases = new float[count];
        for (int i = 0; i < count; i++)
            wobblePhases[i] = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < count; i++)
        {
            float angleDeg = orbitAngle + angleStep * i;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 pos = transform.position + new Vector3(
                Mathf.Cos(angleRad) * orbitRadius,
                Mathf.Sin(angleRad) * orbitRadius,
                0f);

            GameObject block = Instantiate(orbitBlockPrefab, pos, Quaternion.identity, blockRoot);
            orbitBlocks.Add(block);
            PlaySpawnSE(orbitSpawnClip, orbitSpawnAudioSource, orbitSpawnVolume);

            yield return new WaitForSeconds(orbitSpawnInterval);
        }
    }

    // =========================================================
    // Pattern 2: Bottom Wall（ドミノ配置コルーチン）
    // =========================================================

    private IEnumerator SpawnBottomWallDomino()
    {
        if (bottomWallBlockPrefab == null) yield break;

        // 1フレーム待ってCamera.mainが確実に初期化されてから座標を計算する
        // （Start()実行順によってCamera.mainのサイズが未確定のままになる場合への対策）
        yield return null;

        if (Camera.main == null) yield break;

        float halfH    = Camera.main.orthographicSize;
        float halfW    = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float xMin = GetSkillHudRightWorldX(camPos.x, halfW);
        float xMax = camPos.x + halfW;
        float y    = camPos.y - halfH + bottomWallYOffset;  // 画面最下端 + Yオフセット

        float span    = xMax - xMin;
        int   count   = Mathf.Max(1, Mathf.RoundToInt(span / Mathf.Max(blockWidth, 0.01f)));
        float spacing = span / count;

        for (int i = 0; i < count; i++)
        {
            int idx = bottomWallLeftToRight ? i : (count - 1 - i);
            float x = xMin + spacing * (idx + 0.5f);
            Vector3 pos = new Vector3(x, y, transform.position.z);
            GameObject block = Instantiate(bottomWallBlockPrefab, pos, Quaternion.identity, blockRoot);
            bottomWallBlocks.Add(block);
            PlaySpawnSE(bottomWallSpawnClip, bottomWallSpawnAudioSource, bottomWallSpawnVolume);

            yield return new WaitForSeconds(bottomWallDominoInterval);
        }
    }

    // =========================================================
    // Pattern 3: Scatter Blocks（ランダム順に1つずつ出現）
    // =========================================================

    private IEnumerator SpawnScatterDomino()
    {
        if (scatterBlockPrefab == null) yield break;

        // 1フレーム待ってCamera.mainが確実に初期化されてから実行
        yield return null;

        if (Camera.main == null) yield break;

        float halfH    = Camera.main.orthographicSize;
        float halfW    = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float xMin = GetSkillHudRightWorldX(camPos.x, halfW);
        float xMax = camPos.x + halfW;
        float yMax = camPos.y + halfH;

        // Floor除外Y座標を先に計算し、グリッドのyMinとして使う。
        // 画面最下端から始めると下端セルが常にfloor除外で失敗し、
        // 実質的な配置可能数がscatterCountより少なくなるバグを防ぐ。
        float floorExcludeY = camPos.y - halfH;
        if (floor != null)
        {
            Collider2D floorCol = floor.GetComponent<Collider2D>();
            float floorTopY = (floorCol != null) ? floorCol.bounds.max.y : floor.transform.position.y;
            floorExcludeY = floorTopY + scatterFloorExcludeHeight;
        }
        float yMin = floorExcludeY;  // グリッドは除外エリアの上から開始

        Vector3 screenCenter = new Vector3(camPos.x, camPos.y, transform.position.z);
        Vector3 enemyPos     = transform.position;
        Vector3 playerPos    = (player != null)
            ? player.transform.position
            : new Vector3(float.MaxValue, float.MaxValue, 0f);

        // ── ジッタードグリッド方式 ──────────────────────────────
        // 画面をグリッドに分割し各セル内でランダムな位置を選ぶことで均等分散させる
        float areaW = xMax - xMin;
        float areaH = yMax - yMin;

        int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(scatterCount * (areaW / areaH))));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)scatterCount / cols));

        float cellW = areaW / cols;
        float cellH = areaH / rows;

        // セルインデックスをランダムシャッフル → ランダムな順番で1つずつ出現
        int totalCells = cols * rows;
        int[] cellIndices = new int[totalCells];
        for (int i = 0; i < totalCells; i++) cellIndices[i] = i;
        for (int i = totalCells - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = cellIndices[i]; cellIndices[i] = cellIndices[j]; cellIndices[j] = tmp;
        }

        int placed = 0;
        for (int ci = 0; ci < totalCells && placed < scatterCount; ci++)
        {
            int cellIndex = cellIndices[ci];
            int col = cellIndex % cols;
            int row = cellIndex / cols;

            float cellXMin = xMin + cellW * col;
            float cellYMin = yMin + cellH * row;

            bool success = false;
            for (int attempt = 0; attempt < maxScatterAttempts; attempt++)
            {
                float x = Random.Range(cellXMin, cellXMin + cellW);
                float y = Random.Range(cellYMin, cellYMin + cellH);
                Vector3 candidate = new Vector3(x, y, transform.position.z);

                if (Vector3.Distance(candidate, screenCenter) < scatterCenterExcludeRadius) continue;
                if (Vector3.Distance(candidate, enemyPos)     < scatterEnemyExcludeRadius)  continue;
                if (Vector3.Distance(candidate, playerPos)    < scatterPlayerExcludeRadius)  continue;
                if (candidate.y < floorExcludeY) continue;

                // placed番目のブロックに scatterRotationStep × placed + ランダム幅 の回転を設定
                float variance = Random.Range(-scatterRotationVariance * 0.5f, scatterRotationVariance * 0.5f);
                float rotZ = scatterRotationStep * placed + variance;
                Quaternion rot = Quaternion.Euler(0f, 0f, rotZ);

                GameObject block = Instantiate(scatterBlockPrefab, candidate, rot, blockRoot);
                scatterBlocks.Add(block);
                PlaySpawnSE(scatterSpawnClip, scatterSpawnAudioSource, scatterSpawnVolume);
                success = true;
                placed++;
                break;
            }

            if (success)
                yield return new WaitForSeconds(scatterSpawnInterval);
            // 除外条件に引っかかったセルはインターバルなしで次へ
        }
    }

    // =========================================================
    // Pattern 4: Side Walls（左右縦壁・ドミノ配置コルーチン）
    // =========================================================

    private IEnumerator SpawnSideWallDomino()
    {
        if (sideWallBlockPrefab == null) yield break;

        // 1フレーム待ってCamera.mainが確実に初期化されてから実行
        yield return null;

        if (Camera.main == null) yield break;

        float halfH    = Camera.main.orthographicSize;
        float halfW    = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        // 左壁X：SkillHUD右端のワールドX + オフセット（解像度非依存）
        float leftX  = GetSkillHudRightWorldX(camPos.x, halfW) + sideWallLeftXOffset;
        // 右壁X：画面右端 + オフセット（右オフセットは左方向が正）
        float rightX = camPos.x + halfW - sideWallRightXOffset;

        float yMin = camPos.y - halfH;
        float yMax = camPos.y + halfH;
        float span    = yMax - yMin;
        int   count   = Mathf.Max(1, Mathf.RoundToInt(span / Mathf.Max(sideBlockHeight, 0.01f)));
        float spacing = span / count;

        // 左右同時にドミノ配置（同じインターバルで1ペアずつ出現）
        for (int i = 0; i < count; i++)
        {
            int idx = sideWallBottomToTop ? i : (count - 1 - i);
            float y = yMin + spacing * (idx + 0.5f);

            // 左壁
            Vector3 leftPos  = new Vector3(leftX,  y, transform.position.z);
            GameObject leftBlock = Instantiate(sideWallBlockPrefab, leftPos, Quaternion.identity, blockRoot);
            sideWallBlocks.Add(leftBlock);

            // 右壁
            Vector3 rightPos = new Vector3(rightX, y, transform.position.z);
            GameObject rightBlock = Instantiate(sideWallBlockPrefab, rightPos, Quaternion.identity, blockRoot);
            sideWallBlocks.Add(rightBlock);

            PlaySpawnSE(sideWallSpawnClip, sideWallSpawnAudioSource, sideWallSpawnVolume);

            yield return new WaitForSeconds(sideWallDominoInterval);
        }
    }

    // =========================================================
    // タイマー管理 & 再配置コルーチン
    // =========================================================

    private void UpdateTimers()
    {
        float ts = GetTimeScale();

        // CW/CCW 切り替え
        directionSwitchTimer -= Time.deltaTime * ts;
        if (directionSwitchTimer <= 0f)
        {
            orbitDirection       *= -1f;
            directionSwitchTimer  = directionSwitchInterval;
        }

        // Pattern 1 再配置
        if (!pattern1Replacing)
        {
            pattern1Timer -= Time.deltaTime * ts;
            if (pattern1Timer <= 0f)
            {
                pattern1Timer = pattern1ReplaceInterval;
                StartCoroutine(ReplacePattern1());
            }
        }

        // Pattern 2 再配置
        if (!pattern2Replacing)
        {
            pattern2Timer -= Time.deltaTime * ts;
            if (pattern2Timer <= 0f)
            {
                pattern2Timer = pattern2ReplaceInterval;
                StartCoroutine(ReplacePattern2());
            }
        }

        // Pattern 3 再配置
        if (!pattern3Replacing)
        {
            pattern3Timer -= Time.deltaTime * ts;
            if (pattern3Timer <= 0f)
            {
                pattern3Timer = pattern3ReplaceInterval;
                StartCoroutine(ReplacePattern3());
            }
        }

        // Pattern 4 再配置
        if (!pattern4Replacing)
        {
            pattern4Timer -= Time.deltaTime * ts;
            if (pattern4Timer <= 0f)
            {
                pattern4Timer = pattern4ReplaceInterval;
                StartCoroutine(ReplacePattern4());
            }
        }
    }

    private IEnumerator ReplacePattern1()
    {
        pattern1Replacing = true;
        yield return StartCoroutine(BlinkAndDestroyList(orbitBlocks));
        orbitTargetCount = 0;
        yield return new WaitForSeconds(replaceDelay);
        yield return StartCoroutine(SpawnOrbitDomino());
        pattern1Replacing = false;
    }

    private IEnumerator ReplacePattern2()
    {
        pattern2Replacing = true;
        yield return StartCoroutine(BlinkAndDestroyList(bottomWallBlocks));
        yield return new WaitForSeconds(replaceDelay);
        bottomWallLeftToRight = !bottomWallLeftToRight;
        yield return StartCoroutine(SpawnBottomWallDomino());
        pattern2Replacing = false;
    }

    private IEnumerator ReplacePattern3()
    {
        pattern3Replacing = true;
        yield return StartCoroutine(BlinkAndDestroyList(scatterBlocks));
        yield return new WaitForSeconds(replaceDelay);
        yield return StartCoroutine(SpawnScatterDomino());
        pattern3Replacing = false;
    }

    private IEnumerator ReplacePattern4()
    {
        pattern4Replacing = true;
        DestroyBlockList(sideWallBlocks);
        yield return new WaitForSeconds(replaceDelay);
        // 再配置のたびにドミノ方向を交互切り替え
        sideWallBottomToTop = !sideWallBottomToTop;
        yield return StartCoroutine(SpawnSideWallDomino());
        pattern4Replacing = false;
    }

    // =========================================================
    // 共通ヘルパー
    // =========================================================

    /// <summary>
    /// SkillHUDの右端のワールドX座標を返す。
    /// skillHudObjectが設定されている場合はGetWorldCornersで正確に取得する（解像度非依存）。
    /// 未設定時はskillHudPixelWidthのピクセル換算で代替する。
    /// </summary>
    private float GetSkillHudRightWorldX(float camX, float halfW)
    {
        if (skillHudCachedRect != null)
        {
            Canvas rootCanvas = skillHudCachedRect.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
                Vector3[] canvasCorners = new Vector3[4];
                Vector3[] hudCorners    = new Vector3[4];
                canvasRT.GetWorldCorners(canvasCorners);
                skillHudCachedRect.GetWorldCorners(hudCorners);

                float canvasLeft  = canvasCorners[0].x;
                float canvasRight = canvasCorners[2].x;
                float canvasWidth = canvasRight - canvasLeft;

                if (canvasWidth > 0.001f)
                {
                    // SkillHUD右端をCanvas幅で正規化 → Canvas Scalerや解像度に依らず正確な割合を取得
                    float hudRight = Mathf.Max(hudCorners[2].x, hudCorners[3].x);
                    float t = (hudRight - canvasLeft) / canvasWidth;
                    return camX - halfW + t * (halfW * 2f);
                }
            }
        }
        if (skillHudPixelWidth > 0f && Screen.width > 0)
            return camX - halfW + skillHudPixelWidth * ((halfW * 2f) / Screen.width);
        return camX - halfW;
    }

    private static void DestroyBlockList(List<GameObject> list)
    {
        foreach (var b in list)
            if (b != null) Destroy(b);
        list.Clear();
    }

    /// <summary>
    /// リスト内の全ブロックをblockBlinkCount回点滅させてからDestroyし、リストを空にする。
    /// Pattern 1/2/3 のInterval再配置直前に呼ぶ。
    /// </summary>
    private IEnumerator BlinkAndDestroyList(List<GameObject> list)
    {
        foreach (var b in list)
        {
            if (b == null) continue;
            if (!b.activeInHierarchy) { Destroy(b); continue; }
            WallHealth wh = b.GetComponent<WallHealth>();
            if (wh != null)
                wh.StartBlinkAndDestroy(blockBlinkCount, blockBlinkInterval);
            else
                Destroy(b);
        }
        yield return new WaitForSeconds(blockBlinkCount * blockBlinkInterval * 2f);
        list.Clear();
    }

    /// <summary>ブロック出現SEを再生する。AudioSource未設定時はPlayClipAtPointで代替。</summary>
    private void PlaySpawnSE(AudioClip clip, AudioSource source, float volume)
    {
        if (clip == null) return;
        float finalVolume = volume * (SoundSettingsManager.Instance != null
            ? SoundSettingsManager.Instance.SEVolume : 1f);
        if (source != null)
            source.PlayOneShot(clip, finalVolume);
        else
            AudioSource.PlayClipAtPoint(clip, transform.position, finalVolume);
    }
}
