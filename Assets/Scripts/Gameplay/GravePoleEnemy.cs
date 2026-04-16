using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GravePole ボス（トーテムポール型）の統括管理スクリプト。
///
/// ■ 構成
///   本体（ポール）  : 画面中央で上下ふわふわ浮遊、反射弾で HP 削り
///   オーブ×2       : OrbGimmick コンポーネントを持つ別 GameObject（Inspector 設定）
///   アタックブロック: タイマー起爆型ブロック（AttackBlock）を 8 個ランダム配置
///   Drone          : 既存 Drone Prefab を画面上部にスポーン
///
/// ■ フェーズ
///   Phase 1（HP 100〜50%以上）: Normal 弾のみ、Drone 撃破後 60 秒で再出現
///   Phase 2（HP 50%未満）     : Normal/Multi 弾ランダム、Drone 撃破後 30 秒で再出現
///                               オーブが Wave 移動開始
/// </summary>
[DisallowMultipleComponent]
public class GravePoleEnemy : MonoBehaviour
{
    // =========================================================
    // Inspector フィールド
    // =========================================================

    [Header("本体 浮遊移動")]
    [Tooltip("上下移動の振れ幅（ワールド単位）")]
    [SerializeField] private float floatAmplitude = 1.2f;

    [Tooltip("上下移動の周期速度（大きいほど速い）")]
    [SerializeField] private float floatSpeed = 0.6f;

    [Tooltip("左右揺れの振れ幅（ワールド単位）。0で無効。")]
    [SerializeField] private float floatXAmplitude = 0.3f;

    [Tooltip("左右揺れの周期速度")]
    [SerializeField] private float floatXSpeed = 0.4f;

    // ----------------------------------------------------------

    [Header("ギミックオーブ")]
    [Tooltip("左オーブの Prefab（起動時に自動生成）")]
    [SerializeField] private GameObject orbLeftPrefab;

    [Tooltip("右オーブの Prefab（起動時に自動生成）")]
    [SerializeField] private GameObject orbRightPrefab;

    [Tooltip("オーブの画面上端からの距離（ワールド単位）")]
    [SerializeField] private float orbOffsetFromTop = 1.2f;

    [Tooltip("オーブの画面中心からの横距離（ワールド単位）")]
    [SerializeField] private float orbOffsetFromCenter = 2.5f;

    // ----------------------------------------------------------

    [Header("アタックブロック")]
    [Tooltip("AttackBlock コンポーネントを持つ Prefab")]
    [SerializeField] private GameObject attackBlockPrefab;

    [Tooltip("同時配置数")]
    [SerializeField] private int attackBlockCount = 8;

    [Tooltip("ドミノ配置間隔（秒）")]
    [SerializeField] private float attackBlockDominoInterval = 0.1f;

    [Tooltip("全ブロック起爆後〜次セット配置開始までの待機時間（秒）")]
    [SerializeField] private float attackBlockRespawnDelay = 3f;

    [Tooltip("AttackBlock の起爆までの合計秒数（AttackBlock.fuseSeconds と一致させること）")]
    [SerializeField] private float attackBlockFuseSeconds = 10f;

    [Tooltip("本体周囲の配置除外半径（ワールド単位）")]
    [SerializeField] private float attackBlockBodyExcludeRadius = 2f;

    [Tooltip("配置試行最大回数")]
    [SerializeField] private int attackBlockMaxAttempts = 30;

    [Tooltip("配置出現 SE")]
    [SerializeField] private AudioClip attackBlockSpawnClip;

    [Range(0f, 1f)]
    [SerializeField] private float attackBlockSpawnVolume = 0.8f;

    // ----------------------------------------------------------

    [Header("Drone 生成")]
    [Tooltip("Drone の Prefab")]
    [SerializeField] private GameObject dronePrefab;

    [Tooltip("Drone に適用する EnemyData（スプライト・スケール・HP・移動・射撃を設定）")]
    [SerializeField] private EnemyData droneEnemyData;

    [Tooltip("スポーンY位置（カメラ中心からのオフセット。0=中央、正=上、負=下）")]
    [SerializeField] private float droneSpawnOffsetY = 0f;

    [Tooltip("スポーンYのランダム幅（±この値でY座標をばらす）")]
    [SerializeField] private float droneSpawnYRange = 1f;

    [Tooltip("各フェーズ開始から最初の Drone が出現するまでの遅延（秒）")]
    [SerializeField] private float droneFirstSpawnDelay = 10f;

    [Tooltip("Drone 撃破後の再出現遅延 Phase 1（秒）")]
    [SerializeField] private float droneRespawnDelayPhase1 = 60f;

    [Tooltip("Drone 撃破後の再出現遅延 Phase 2（秒）")]
    [SerializeField] private float droneRespawnDelayPhase2 = 30f;

    // ----------------------------------------------------------

    [Header("本体 初期Y位置")]
    [Tooltip("カメラ上端からの距離（ワールド単位）。大きいほど下に移動する。\n旧デフォルト相当値：カメラorthographicSize × 0.7")]
    [SerializeField] private float bodyOffsetFromTop = 3.5f;

    [Header("フェーズ設定")]
    [Tooltip("この HP% 未満になったらフェーズ2に移行")]
    [SerializeField] private float phase2HpThreshold = 50f;

    [Tooltip("オーブ発光中の本体への受ダメージ倍率（オーブ未発光時は 1.0）")]
    [SerializeField] private float orbActiveDamageMultiplier = 3f;

    // ----------------------------------------------------------

    [Header("共通ブロック設定")]
    [Tooltip("ブロック生成時の親 Transform（未設定でシーンルート）")]
    [SerializeField] private Transform blockRoot;

    [Tooltip("Floor 上端からの追加除外高さ（ワールド単位）")]
    [SerializeField] private float floorExcludeHeight = 2f;

    [Tooltip("アタックブロックのスポーン下限ワールドY座標（この値より下には配置しない）")]
    [SerializeField] private float attackBlockMinWorldY = 0f;

    [Tooltip("SkillHUD 横幅（ピクセル）。0 で除外なし。")]
    [SerializeField] private float skillHudPixelWidth = 280f;

    // =========================================================
    // ランタイム変数
    // =========================================================

    private int         currentPhase     = 1;
    private EnemyStats  enemyStats;
    private Vector3     bodyBasePosition;
    private float       floatTime        = 0f;

    private readonly List<AttackBlock> activeAttackBlocks = new List<AttackBlock>();
    private Coroutine  blockLoopCo;

    private GameObject currentDrone;
    private Coroutine  droneLoopCo;
    private EnemySpawner enemySpawner;

    private int        activeOrbBuffCount = 0;
    private FloorHealth floor;

    // 起動時に Prefab から生成したオーブの参照（Inspector 設定不要）
    private OrbGimmick leftOrb;
    private OrbGimmick rightOrb;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        enemyStats = GetComponent<EnemyStats>();
    }

    private void Start()
    {
        floor = FindObjectOfType<FloorHealth>();
        enemySpawner = FindFirstObjectByType<EnemySpawner>();

        // 本体の基準位置（カメラ中央 + 上方 30%）
        if (Camera.main != null)
        {
            Vector3 cam  = Camera.main.transform.position;
            float   halfH = Camera.main.orthographicSize;
            bodyBasePosition = new Vector3(cam.x, cam.y + halfH - bodyOffsetFromTop, transform.position.z);
        }
        else
        {
            bodyBasePosition = transform.position;
        }
        transform.position = bodyBasePosition;

        // オーブを Prefab から生成
        SpawnOrbs();

        // アタックブロックループ開始
        blockLoopCo = StartCoroutine(AttackBlockLoop());

        // Phase 1 Drone 管理開始
        droneLoopCo = StartCoroutine(DroneSpawnLoop(droneFirstSpawnDelay, droneRespawnDelayPhase1));
    }

    private void Update()
    {
        UpdateBodyFloat();
        CheckPhaseTransition();
    }

    private void OnDestroy()
    {
        // アタックブロック全破棄
        foreach (AttackBlock ab in activeAttackBlocks)
            if (ab != null) Destroy(ab.gameObject);
        activeAttackBlocks.Clear();

        // Drone 破棄
        if (currentDrone != null) Destroy(currentDrone);

        // オーブ破棄
        if (leftOrb  != null) Destroy(leftOrb.gameObject);
        if (rightOrb != null) Destroy(rightOrb.gameObject);
    }

    // =========================================================
    // オーブ生成
    // =========================================================

    private void SpawnOrbs()
    {
        if (Camera.main == null) return;

        float halfH  = Camera.main.orthographicSize;
        float halfW  = halfH * Camera.main.aspect;
        Vector3 cam  = Camera.main.transform.position;

        float orbY       = cam.y + halfH - orbOffsetFromTop;
        Vector3 leftPos  = new Vector3(cam.x - orbOffsetFromCenter, orbY, transform.position.z);
        Vector3 rightPos = new Vector3(cam.x + orbOffsetFromCenter, orbY, transform.position.z);

        if (orbLeftPrefab != null)
        {
            GameObject go = Instantiate(orbLeftPrefab, leftPos, Quaternion.identity);
            leftOrb = go.GetComponent<OrbGimmick>();
            if (leftOrb != null) { leftOrb.Boss = this; leftOrb.SetPhase(1); }
        }
        if (orbRightPrefab != null)
        {
            GameObject go = Instantiate(orbRightPrefab, rightPos, Quaternion.identity);
            rightOrb = go.GetComponent<OrbGimmick>();
            if (rightOrb != null) { rightOrb.Boss = this; rightOrb.SetPhase(1); }
        }
    }

    // =========================================================
    // 本体浮遊移動（カスタム上下 Sine）
    // =========================================================

    private void UpdateBodyFloat()
    {
        floatTime += Time.deltaTime;
        float offsetY = Mathf.Sin(floatTime * floatSpeed)  * floatAmplitude;
        float offsetX = Mathf.Sin(floatTime * floatXSpeed) * floatXAmplitude;
        transform.position = new Vector3(
            bodyBasePosition.x + offsetX,
            bodyBasePosition.y + offsetY,
            bodyBasePosition.z);
    }

    // =========================================================
    // フェーズ管理
    // =========================================================

    private void CheckPhaseTransition()
    {
        if (currentPhase == 1
            && enemyStats != null
            && enemyStats.GetHpPercentage() < phase2HpThreshold)
        {
            TransitionToPhase2();
        }
    }

    private void TransitionToPhase2()
    {
        currentPhase = 2;

        // オーブを Phase 2 に
        if (leftOrb  != null) leftOrb.SetPhase(2);
        if (rightOrb != null) rightOrb.SetPhase(2);

        // 稼働中のアタックブロックを Phase 2 に切り替え
        foreach (AttackBlock ab in activeAttackBlocks)
            if (ab != null) ab.SetPhase(2);

        // Drone 管理を Phase 2 に切り替え（現 Drone を破棄して再スタート）
        if (droneLoopCo != null) StopCoroutine(droneLoopCo);
        if (currentDrone != null) { Destroy(currentDrone); currentDrone = null; }
        droneLoopCo = StartCoroutine(DroneSpawnLoop(droneFirstSpawnDelay, droneRespawnDelayPhase2));
    }

    // =========================================================
    // アタックブロック ループ
    // =========================================================

    /// <summary>
    /// 「配置 → 全起爆待ち → 3 秒後 → 再配置」を繰り返すループ。
    /// </summary>
    private IEnumerator AttackBlockLoop()
    {
        // 1 フレーム待ってから開始（Camera.main 確定後）
        yield return null;

        while (true)
        {
            // ── 配置 ──
            yield return StartCoroutine(SpawnAttackBlockDomino());

            // ── 最後のブロックが起爆するまで待機 ──
            // （最後のブロックが置かれた直後に coroutine が返るので fuseSeconds だけ待てばよい）
            yield return new WaitForSeconds(attackBlockFuseSeconds + attackBlockRespawnDelay);

            // 残存ブロック（稀に生き残り）を掃除
            foreach (AttackBlock ab in activeAttackBlocks)
                if (ab != null) Destroy(ab.gameObject);
            activeAttackBlocks.Clear();
        }
    }

    private IEnumerator SpawnAttackBlockDomino()
    {
        if (attackBlockPrefab == null) yield break;
        if (Camera.main == null) yield break;

        activeAttackBlocks.Clear();

        float halfH    = Camera.main.orthographicSize;
        float halfW    = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        // HUD 除外幅
        float hudWorldWidth = 0f;
        if (skillHudPixelWidth > 0f && Screen.width > 0)
        {
            float worldPerPixel = (halfW * 2f) / Screen.width;
            hudWorldWidth = skillHudPixelWidth * worldPerPixel;
        }

        float xMin = camPos.x - halfW + hudWorldWidth + 0.5f;
        float xMax = camPos.x + halfW - 0.5f;
        float yMax = camPos.y + halfH - 0.5f;

        // Floor 除外 Y
        float floorExcludeY = camPos.y - halfH;
        if (floor != null)
        {
            Collider2D fc = floor.GetComponent<Collider2D>();
            float floorTopY = (fc != null) ? fc.bounds.max.y : floor.transform.position.y;
            floorExcludeY = floorTopY + floorExcludeHeight;
        }
        float yMin = Mathf.Max(floorExcludeY, attackBlockMinWorldY);

        int placed = 0;
        for (int i = 0; i < attackBlockCount; i++)
        {
            bool success = false;
            for (int attempt = 0; attempt < attackBlockMaxAttempts; attempt++)
            {
                float x = Random.Range(xMin, xMax);
                float y = Random.Range(yMin, yMax);
                Vector3 candidate = new Vector3(x, y, transform.position.z);

                if (Vector3.Distance(candidate, transform.position) < attackBlockBodyExcludeRadius)
                    continue;

                GameObject go = Instantiate(attackBlockPrefab, candidate,
                                            Quaternion.identity, blockRoot);
                AttackBlock ab = go.GetComponent<AttackBlock>();
                if (ab != null)
                {
                    ab.SetPhase(currentPhase);
                    ab.SetFuseSeconds(attackBlockFuseSeconds);
                    activeAttackBlocks.Add(ab);
                }

                PlaySpawnSE();
                placed++;
                success = true;
                break;
            }

            if (success)
                yield return new WaitForSeconds(attackBlockDominoInterval);
        }
    }

    private void PlaySpawnSE()
    {
        if (attackBlockSpawnClip == null) return;
        float vol = attackBlockSpawnVolume *
            (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        AudioSource.PlayClipAtPoint(attackBlockSpawnClip, transform.position, vol);
    }

    // =========================================================
    // Drone 管理
    // =========================================================

    /// <param name="initialDelay">フェーズ開始から最初の出現までの遅延</param>
    /// <param name="respawnDelay">撃破後の再出現遅延</param>
    private IEnumerator DroneSpawnLoop(float initialDelay, float respawnDelay)
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            SpawnDrone();

            // Drone が死ぬまで待機
            while (currentDrone != null)
                yield return null;

            yield return new WaitForSeconds(respawnDelay);
        }
    }

    private void SpawnDrone()
    {
        if (dronePrefab == null) return;
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal) return;
        if (Camera.main == null) return;

        Vector3 pos = PickDroneSpawnPosition();
        currentDrone = Instantiate(dronePrefab, pos, Quaternion.identity);

        // EnemyData を適用（スプライト・スケール・Layer・移動・射撃を設定）
        // SetSpawner / aliveCount は呼ばない → 波クリア条件に影響させない
        if (droneEnemyData != null && enemySpawner != null)
            enemySpawner.ApplyEnemyDataExternal(currentDrone, droneEnemyData);
    }

    private Vector3 PickDroneSpawnPosition()
    {
        float halfH  = Camera.main.orthographicSize;
        float halfW  = halfH * Camera.main.aspect;
        Vector3 cam  = Camera.main.transform.position;

        float hudWorldWidth = 0f;
        if (skillHudPixelWidth > 0f && Screen.width > 0)
        {
            float worldPerPixel = (halfW * 2f) / Screen.width;
            hudWorldWidth = skillHudPixelWidth * worldPerPixel;
        }

        // Y：カメラ中心 + オフセット + ランダム幅。画面内にクランプ
        const float margin = 1f;
        float baseY  = cam.y + droneSpawnOffsetY;
        float spawnY = baseY + Random.Range(-droneSpawnYRange, droneSpawnYRange);
        spawnY = Mathf.Clamp(spawnY, cam.y - halfH + margin, cam.y + halfH - margin);

        // X：HUD除外後の有効範囲からランダム（最大10回試行）
        float xMin = cam.x - halfW + hudWorldWidth + margin;
        float xMax = cam.x + halfW - margin;

        float chosenX = (xMin + xMax) * 0.5f; // フォールバック：中央
        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(xMin, xMax);
            Vector3 c = new Vector3(x, spawnY, 0f);
            bool tooClose = Vector3.Distance(c, transform.position) < 2f;
            if (leftOrb  != null) tooClose |= Vector3.Distance(c, leftOrb.transform.position)  < 2f;
            if (rightOrb != null) tooClose |= Vector3.Distance(c, rightOrb.transform.position) < 2f;
            if (!tooClose) { chosenX = x; break; }
        }

        return new Vector3(chosenX, spawnY, transform.position.z);
    }

    // =========================================================
    // オーブ → ボス通信
    // =========================================================

    /// <summary>OrbGimmick から発光開始時に呼ばれる。</summary>
    public void OnOrbActivated(OrbGimmick orb)
    {
        activeOrbBuffCount++;
        ApplyDamageMultiplier();
    }

    /// <summary>OrbGimmick から発光終了時に呼ばれる。</summary>
    public void OnOrbDeactivated(OrbGimmick orb)
    {
        activeOrbBuffCount = Mathf.Max(0, activeOrbBuffCount - 1);
        ApplyDamageMultiplier();
    }

    private void ApplyDamageMultiplier()
    {
        if (enemyStats == null) return;
        enemyStats.incomingDamageMultiplier = (activeOrbBuffCount > 0) ? orbActiveDamageMultiplier : 1f;
    }

}
