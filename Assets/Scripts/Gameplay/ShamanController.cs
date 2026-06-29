using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShamanController : MonoBehaviour
{
    public enum ShamanPreviewSprite
    {
        Idle, Idle2, Idle3, IdleAnimate,
        Hit,
        Attack1, Attack2, Attack3, AttackAnimate,
        Smoke1, Smoke2, Smoke3, Smoke4, Smoke5, SmokeAnimate,
        Thunder1, Thunder2, Thunder3, Thunder4, ThunderAnimate,
        TornadoPreview
    }

    [System.Serializable]
    public class ShamanFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数（Idle/Hitは不使用）")]
        public float   duration;
        [Tooltip("マズル位置（FirePoint_Staffのローカル座標。attackFramesのみ有効）")]
        public Vector2 muzzleOffset;
        [Tooltip("当たり判定サイズ（0,0のときは変更しない）")]
        public Vector2 colliderSize;
        [Tooltip("当たり判定オフセット")]
        public Vector2 colliderOffset;
    }

    // ======================================================
    // Inspector fields
    // ======================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BoxCollider2D  bodyCollider;
    [SerializeField] private Transform      firePointStaff;
    [SerializeField] private AudioClip      smokeSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float          smokeSeVolume = 1f;

    [Header("Editor Preview")]
    [SerializeField] private ShamanPreviewSprite previewSprite = ShamanPreviewSprite.Idle;

    [Header("Sprites")]
    [NonReorderable]
    [SerializeField] private ShamanFrame[] idleFrames;
    [SerializeField] private ShamanFrame   hitFrame;
    [NonReorderable]
    [SerializeField] private ShamanFrame[] attackFrames;
    [NonReorderable]
    [SerializeField] private ShamanFrame[] smokeFrames;

    [Header("Warp")]
    [SerializeField] private float firstWarpInterval     = 20f;
    [SerializeField] private float warpInterval          = 20f;
    [SerializeField] private float warpFadeOutDuration   = 0.4f;
    [SerializeField] private float warpInvisibleDuration = 0.2f;
    [SerializeField] private float warpFadeInDuration    = 0.5f;
    [SerializeField] private float warpSmokeStagger      = 0.3f;
    [SerializeField] private int   warpSmokeCount        = 4;
    [SerializeField] private int   warpSpawnIndexMin     = 0;
    [SerializeField] private int   warpSpawnIndexMax     = 8;

    [Header("Sand Smoke")]
    [SerializeField] private GameObject smokeParticlePrefab;
    [SerializeField] private Color      smokeColor           = new Color(0.9f, 0.75f, 0.3f, 0.8f);
    [SerializeField] private float      smokeRadius          = 3f;
    [SerializeField] private float      smokeDuration        = 10f;
    [SerializeField] private float      smokeExpansionSpeed  = 0.07f;
    [SerializeField] private float      smokeFadeInDuration  = 2.5f;
    [SerializeField] private float      smokeFadeOutDuration = 3.5f;
    [SerializeField] private float      smokeGravityModifier = -0.008f;
    [SerializeField] private float      smokeParticleSizeMin = 3.5f;
    [SerializeField] private float      smokeParticleSizeMax = 3.5f;
    [SerializeField] private float      smokeEmissionRate    = 3f;
    [SerializeField] private AudioClip  smokeCloudDissolveSE;

    [Header("Sand Grain")]
    [SerializeField] private Color sandGrainColor           = new Color(0.509434f, 0.40485677f, 0.045656808f, 1f);
    [SerializeField] private float sandGrainSizeMin         = 0.05f;
    [SerializeField] private float sandGrainSizeMax         = 0.1f;
    [SerializeField] private float sandGrainEmissionRate    = 300f;
    [SerializeField] private float sandGrainSpeedMin        = 0.1f;
    [SerializeField] private float sandGrainSpeedMax        = 0.6f;
    [SerializeField] private float sandGrainGravityModifier = -0.2f;
    [SerializeField] private float sandGrainLifetime        = 1.5f;
    [SerializeField] private float sandGrainSpreadX         = 1.5f;
    [SerializeField] private float sandGrainSpreadY         = 1.5f;

    [Header("Back Phase - Tornado")]
    [SerializeField] private GameObject tornadoPrefab;
    [SerializeField] private Color      tornadoSmokeColor      = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private int        tornadoSummonCount     = 2;
    [SerializeField] private float      tornadoSummonStagger   = 0.5f;
    [SerializeField] private float      tornadoDuration        = 10f;
    [SerializeField] private float      tornadoMoveSpeed       = 0.5f;
    [SerializeField] private float      tornadoFadeIn          = 1f;
    [SerializeField] private float      tornadoFadeOut         = 2f;
    [SerializeField] private float      tornadoBatchCooldown      = 3f;
    [Tooltip("竜巻パーティクルの放出レート（個/秒）。プレファブのデフォルト20-30に近い値を設定")]
    [SerializeField] private float      tornadoEmissionRate       = 20f;
    [Tooltip("EnemyData.bulletTypes のインデックス（竜巻が使う弾種）")]
    [SerializeField] private int        tornadoBulletTypeIndex    = 8;
    [SerializeField] private float      tornadoBulletFireInterval = 2f;
    [Tooltip("TornadoPreview用：竜巻をScene上で確認したい位置（ワールド座標）")]
    [SerializeField] private Vector2    tornadoPreviewPos;
    [Tooltip("竜巻が出現できるSpawnPointのインデックス一覧（空の場合はwarpSpawnIndexMin〜Maxを使用）")]
    [SerializeField] private int[]      tornadoSpawnIndices = new int[] { 0, 1, 2, 3, 5 };
    [Tooltip("竜巻パーティクルの最小サイズ")]
    [SerializeField] private float      tornadoParticleSizeMin  = 1f;
    [Tooltip("竜巻パーティクルの最大サイズ")]
    [SerializeField] private float      tornadoParticleSizeMax  = 2f;
    [Tooltip("竜巻パーティクルの寿命（秒）。短くすると外への拡散距離が縮まる")]
    [SerializeField] private float      tornadoParticleLifetime = 3f;

    [Header("Back Phase - Thunder Cloud")]
    [SerializeField] private GameObject    thunderCloudPrefab;
    [SerializeField] private int           thunderCloudTriggerCount = 6;
    [SerializeField] private Vector2       thunderCloudPosition;
    [Tooltip("EnemyData.bulletTypes のインデックス（雷雲が使う弾種）")]
    [SerializeField] private int           thunderBulletTypeIndex   = 7;
    [Tooltip("雷紋スプライトのプレビュー用 SpriteRenderer（Shaman Prefab 内の専用 GameObject に追加する）")]
    [SerializeField] private SpriteRenderer thunderPreviewRenderer;
    [NonReorderable]
    [SerializeField] private ShamanFrame[] thunderFrames;

    [Header("Phase Transition")]
    [Tooltip("後半フェーズ移行HP閾値（0〜100%）")]
    [Range(1f, 99f)]
    [SerializeField] private float phaseTransitionHpThreshold = 50f;

    // ======================================================
    // Runtime state
    // ======================================================

    private enum Phase { Front, Back }
    private Phase _phase = Phase.Front;
    private bool  _phaseTransitioned;

    private EnemyMover         _mover;
    private EnemyShooter       _shooter;
    private EnemySpriteSwapper _spriteSwapper;
    private AudioSource        _audioSource;
    private EnemyStats         _enemyStats;
    private EnemySpawner       _spawner;
    private Transform          _projectileRoot;

    private Vector2   _currentBaseOffset;
    private bool      _wasHitActive;
    private int       _currentSpawnIndex = 4;
    private Coroutine _idleAnimCoroutine;
    private Coroutine _attackAnimCoroutine;
    private Coroutine _mainLoopCoroutine;

    private SmokeCloud _lastWarpDestSmoke;
    private int        _totalTornadosSummoned;

    private EnemyData  _enemyData;

    // ======================================================
    // Lifecycle
    // ======================================================

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (firePointStaff == null)
            firePointStaff = transform.Find("FirePoint_Staff");

        _mover         = GetComponent<EnemyMover>();
        _shooter       = GetComponent<EnemyShooter>();
        _spriteSwapper = GetComponent<EnemySpriteSwapper>();
        _audioSource   = GetComponent<AudioSource>();
        _enemyStats    = GetComponent<EnemyStats>();

        if (_mover != null)
            _mover.suppressMovement = true;

        if (_shooter != null)
        {
            if (firePointStaff != null)
            {
                _shooter.SetMuzzlePoints(new EnemyShooter.MuzzlePoint[]
                {
                    new EnemyShooter.MuzzlePoint { muzzleTransform = firePointStaff, offset = Vector3.zero }
                });
            }
            _shooter.OnFired += OnShooterFired;
        }

    }

    private void OnDestroy()
    {
        if (_shooter != null)
            _shooter.OnFired -= OnShooterFired;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        if (thunderPreviewRenderer != null)
            thunderPreviewRenderer.enabled = false;

        _spawner        = FindFirstObjectByType<EnemySpawner>();
        _projectileRoot = _shooter != null ? _shooter.GetProjectileRoot() : transform;
        _enemyData      = _shooter != null ? _shooter.GetEnemyData() : null;

        if (_spawner != null)
        {
            var sp = _spawner.GetSpawnPoint(4);
            if (sp != null)
                transform.position = new Vector3(sp.position.x, sp.position.y, transform.position.z);
        }
        _currentSpawnIndex = 4;

        StartIdleAnim();
        _mainLoopCoroutine = StartCoroutine(MainLoop());
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        CheckPhaseTransition();

        if (_spriteSwapper != null)
        {
            bool isHitNow = _spriteSwapper.IsHitActive;
            if (isHitNow != _wasHitActive)
            {
                _wasHitActive = isHitNow;
                if (!isHitNow)
                    ApplyOffset(_currentBaseOffset);
            }
        }
    }

    // ======================================================
    // Phase
    // ======================================================

    private void CheckPhaseTransition()
    {
        if (_phaseTransitioned || _phase == Phase.Back) return;
        if (_enemyStats == null) { Debug.LogWarning("[Shaman] CheckPhaseTransition: _enemyStats is null"); return; }
        if (_enemyStats.HP <= _enemyStats.MaxHP * (phaseTransitionHpThreshold / 100f))
        {
            Debug.Log($"[Shaman] Phase→Back! HP={_enemyStats.HP}/{_enemyStats.MaxHP} threshold={phaseTransitionHpThreshold}%");
            _phase = Phase.Back;
            _phaseTransitioned = true;

            // MainLoop（FrontフェーズのWarpRoutine含む）を強制停止してBackPhaseLoopを直接起動
            if (_mainLoopCoroutine != null)
            {
                StopCoroutine(_mainLoopCoroutine);
                _mainLoopCoroutine = null;
            }
            if (_shooter != null) _shooter.enabled = true;
            StartIdleAnim();
            _mainLoopCoroutine = StartCoroutine(BackPhaseLoop());
        }
    }

    // ======================================================
    // Main Loop
    // ======================================================

    private IEnumerator MainLoop()
    {
        StartIdleAnim();

        bool isFirst = true;
        while (_phase == Phase.Front)
        {
            float interval = isFirst ? firstWarpInterval : warpInterval;
            isFirst = false;

            float elapsed = 0f;
            while (elapsed < interval && _phase == Phase.Front)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_phase == Phase.Front)
            {
                Debug.Log("[Shaman] Front WarpRoutine start");
                yield return StartCoroutine(WarpRoutine());
                Debug.Log("[Shaman] Front WarpRoutine done. phase=" + _phase);
            }
        }

        // BackPhaseLoop は CheckPhaseTransition から直接起動する
    }

    private IEnumerator BackPhaseLoop()
    {
        _totalTornadosSummoned = 0;

        Debug.Log("[Shaman] BackPhaseLoop start. warpInterval=" + warpInterval + " spawner=" + _spawner);

        // フェーズ切り替え直後は warpInterval 待機してからワープ開始
        float elapsed = 0f;
        while (elapsed < warpInterval)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        while (true)
        {
            Debug.Log("[Shaman] Calling WarpRoutine (back phase)");
            // ワープ後はShamanController側でshooterを再開するのでWarpRoutineには任せない
            yield return StartCoroutine(WarpRoutine(reenableShooterAfter: false));

            // 孤立したフロントフェーズWarpRoutineがshooterを再開した可能性があるため確実に無効化
            if (_shooter != null) _shooter.enabled = false;

            SmokeCloud destSmoke = _lastWarpDestSmoke;
            Debug.Log("[Shaman] WarpRoutine done. destSmoke=" + destSmoke + " tornadoPrefab=" + tornadoPrefab);
            if (destSmoke != null && tornadoPrefab != null)
            {
                // 竜巻召喚モード（出現先の砂煙が生きている間）
                yield return StartCoroutine(TornadoSummonMode(destSmoke));
            }

            // 杖攻撃再開
            if (_shooter != null) _shooter.enabled = true;
            StartIdleAnim();

            // 次のワープまで待機
            elapsed = 0f;
            while (elapsed < warpInterval)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator TornadoSummonMode(SmokeCloud watchSmoke)
    {
        while (watchSmoke != null)
        {
            yield return StartCoroutine(SpawnTornadoBatch());

            // 砂煙がまだ生きていれば cooldown 待機
            if (watchSmoke == null) break;
            float wait = 0f;
            while (wait < tornadoBatchCooldown && watchSmoke != null)
            {
                wait += Time.deltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator SpawnTornadoBatch()
    {
        int[] indices = PickTornadoSpawnIndices(tornadoSummonCount);
        if (indices == null || indices.Length == 0) yield break;

        for (int i = 0; i < indices.Length; i++)
        {
            SpawnTornado(indices[i]);
            _totalTornadosSummoned++;

            if (thunderCloudTriggerCount > 0 && _totalTornadosSummoned % thunderCloudTriggerCount == 0)
            {
                SpawnThunderCloud();
            }

            if (i < indices.Length - 1)
                yield return new WaitForSeconds(tornadoSummonStagger);
        }
    }

    private void SpawnTornado(int spawnIndex)
    {
        if (_spawner == null || tornadoPrefab == null) { Debug.LogWarning("[Shaman] SpawnTornado: spawner or prefab null"); return; }
        var sp = _spawner.GetSpawnPoint(spawnIndex);
        if (sp == null) { Debug.LogWarning("[Shaman] SpawnTornado: spawn point null for index " + spawnIndex); return; }
        Debug.Log("[Shaman] SpawnTornado at index=" + spawnIndex + " pos=" + sp.position);

        Vector3 pos = new Vector3(sp.position.x, sp.position.y, transform.position.z);
        var go = Instantiate(tornadoPrefab, pos, Quaternion.identity);
        var tornado = go.GetComponent<TornadoCloud>();
        if (tornado == null) { Destroy(go); return; }

        tornado.SetSmokeColor(tornadoSmokeColor, tornadoSmokeColor);
        tornado.SetFadeDurations(tornadoFadeIn, tornadoFadeOut);
        tornado.SetEmissionRate(tornadoEmissionRate);
        tornado.SetParticleSize(tornadoParticleSizeMin, tornadoParticleSizeMax);
        tornado.SetParticleLifetime(tornadoParticleLifetime);

        var tornadoType = GetBulletType(tornadoBulletTypeIndex);
        var basePrefab  = _shooter != null ? _shooter.GetBulletPrefab() : null;
        if (basePrefab != null)
            tornado.SetBulletType(tornadoType, basePrefab, _projectileRoot, tornadoBulletFireInterval);

        Vector2 moveDir  = PickRandomOctDir();
        bool clockwise   = Random.value > 0.5f;
        tornado.Initialize(tornadoDuration, tornadoMoveSpeed, clockwise, moveDir);
    }

    private void SpawnThunderCloud()
    {
        if (thunderCloudPrefab == null) return;

        Vector3 pos = new Vector3(thunderCloudPosition.x, thunderCloudPosition.y, transform.position.z);
        var go = Instantiate(thunderCloudPrefab, pos, Quaternion.identity);
        var cloud = go.GetComponent<ThunderCloud>();
        if (cloud == null) { Destroy(go); return; }

        var thunderType = GetBulletType(thunderBulletTypeIndex);
        var basePrefab  = _shooter != null ? _shooter.GetBulletPrefab() : null;
        if (basePrefab != null)
            cloud.SetBulletType(thunderType, basePrefab, _projectileRoot);

        if (thunderFrames != null && thunderFrames.Length > 0)
            cloud.SetFrames(thunderFrames);

        cloud.Activate();
    }

    private static Vector2 PickRandomOctDir()
    {
        float angle = Random.Range(0, 8) * 45f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (_enemyData == null || _enemyData.bulletTypes == null) return null;
        if (index < 0 || index >= _enemyData.bulletTypes.Length) return null;
        return _enemyData.bulletTypes[index];
    }

    // ======================================================
    // Warp
    // ======================================================

    private IEnumerator WarpRoutine(bool reenableShooterAfter = true)
    {
        if (_spawner == null) { Debug.LogWarning("[Shaman] WarpRoutine: _spawner is null, aborting"); yield break; }
        StopIdleAnim();
        if (_attackAnimCoroutine != null) { StopCoroutine(_attackAnimCoroutine); _attackAnimCoroutine = null; }

        // ランダムにSPを選定。最後の1個がワープ先
        int[] indices = PickRandomSpawnIndices(warpSmokeCount);
        if (indices == null || indices.Length == 0) { Debug.LogWarning("[Shaman] WarpRoutine: no spawn indices available"); yield break; }
        int destIdx = indices[indices.Length - 1];

        // 砂煙発生開始と同時に攻撃停止
        if (_shooter != null) _shooter.enabled = false;

        // 砂煙召喚アニメ（最後のフレームで各SPに砂煙をスポーン開始）
        var smokes = new SmokeCloud[indices.Length];
        bool smokesReady = false;
        yield return StartCoroutine(PlaySmokeAnimation(() =>
            StartCoroutine(SpawnSmokesCoroutine(indices, smokes, () => smokesReady = true))
        ));

        // 砂煙スポーンが全完了するまで待機（最後フレームduration内に完了する場合は即通過）
        yield return new WaitUntil(() => smokesReady);

        // フェードアウト
        yield return StartCoroutine(FadeAlpha(1f, 0f, warpFadeOutDuration));
        yield return new WaitForSeconds(warpInvisibleDuration);

        // ワープ先の砂煙が残っていれば移動、消されていたらその場留まり
        SmokeCloud destSmoke = smokes[indices.Length - 1];
        _lastWarpDestSmoke = destSmoke;
        if (destSmoke != null)
        {
            var destSp = _spawner.GetSpawnPoint(destIdx);
            if (destSp != null)
            {
                transform.position = new Vector3(destSp.position.x, destSp.position.y, transform.position.z);
                _currentSpawnIndex = destIdx;
            }
        }

        // フェードイン
        yield return StartCoroutine(FadeAlpha(0f, 1f, warpFadeInDuration));
        if (reenableShooterAfter && _shooter != null) _shooter.enabled = true;
        if (reenableShooterAfter) StartIdleAnim();
    }

    private int[] PickRandomSpawnIndices(int count)
    {
        if (_spawner == null) return null;

        var available = new List<int>();
        for (int i = warpSpawnIndexMin; i <= warpSpawnIndexMax; i++)
        {
            if (i == _currentSpawnIndex) continue;
            if (_spawner.GetSpawnPoint(i) != null)
                available.Add(i);
        }

        // Fisher-Yatesシャッフル
        for (int i = available.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = available[i]; available[i] = available[j]; available[j] = tmp;
        }

        int take = Mathf.Min(count, available.Count);
        return available.GetRange(0, take).ToArray();
    }

    private int[] PickTornadoSpawnIndices(int count)
    {
        if (_spawner == null) return null;

        var available = new List<int>();
        if (tornadoSpawnIndices != null && tornadoSpawnIndices.Length > 0)
        {
            foreach (int i in tornadoSpawnIndices)
            {
                if (i == _currentSpawnIndex) continue;
                if (_spawner.GetSpawnPoint(i) != null)
                    available.Add(i);
            }
        }
        else
        {
            for (int i = warpSpawnIndexMin; i <= warpSpawnIndexMax; i++)
            {
                if (i == _currentSpawnIndex) continue;
                if (_spawner.GetSpawnPoint(i) != null)
                    available.Add(i);
            }
        }

        for (int i = available.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = available[i]; available[i] = available[j]; available[j] = tmp;
        }

        int take = Mathf.Min(count, available.Count);
        return available.GetRange(0, take).ToArray();
    }

    // ======================================================
    // Idle Animation
    // ======================================================

    private void StartIdleAnim()
    {
        StopIdleAnim();
        if (idleFrames == null || idleFrames.Length == 0) return;
        if (idleFrames.Length == 1)
        {
            SetBaseSprite(idleFrames[0]);
            return;
        }
        _idleAnimCoroutine = StartCoroutine(IdleAnimLoop());
    }

    private void StopIdleAnim()
    {
        if (_idleAnimCoroutine == null) return;
        StopCoroutine(_idleAnimCoroutine);
        _idleAnimCoroutine = null;
    }

    private IEnumerator IdleAnimLoop()
    {
        int idx = 0;
        while (true)
        {
            var frame = idleFrames[idx];
            if (frame != null && frame.sprite != null)
                SetBaseSprite(frame);
            float dur = (frame != null && frame.duration > 0f) ? frame.duration : 0.5f;
            yield return new WaitForSeconds(dur);
            idx = (idx + 1) % idleFrames.Length;
        }
    }

    // ======================================================
    // Attack sprite
    // ======================================================

    private void OnShooterFired()
    {
        if (attackFrames == null || attackFrames.Length == 0) return;
        StopIdleAnim();
        if (attackFrames[0] != null && firePointStaff != null)
            firePointStaff.localPosition = new Vector3(attackFrames[0].muzzleOffset.x, attackFrames[0].muzzleOffset.y, 0f);
        if (_attackAnimCoroutine != null) StopCoroutine(_attackAnimCoroutine);
        _attackAnimCoroutine = StartCoroutine(PlayAttackAnimation());
    }

    private IEnumerator PlayAttackAnimation()
    {
        foreach (var frame in attackFrames)
        {
            if (frame == null || frame.sprite == null) continue;
            SetBaseSprite(frame);
            if (firePointStaff != null)
                firePointStaff.localPosition = new Vector3(frame.muzzleOffset.x, frame.muzzleOffset.y, 0f);
            yield return new WaitForSeconds(Mathf.Max(frame.duration, 0.05f));
        }
        StartIdleAnim();
        _attackAnimCoroutine = null;
    }

    // ======================================================
    // Smoke Animation
    // ======================================================

    private IEnumerator PlaySmokeAnimation(System.Action onLastFrameStart = null)
    {
        if (smokeFrames == null || smokeFrames.Length == 0) yield break;
        PlaySmokeSe();
        if (_spriteSwapper != null) _spriteSwapper.EnableHitSprite(false);

        int lastValidIdx = -1;
        for (int i = smokeFrames.Length - 1; i >= 0; i--)
            if (smokeFrames[i] != null && smokeFrames[i].sprite != null) { lastValidIdx = i; break; }

        for (int i = 0; i < smokeFrames.Length; i++)
        {
            var frame = smokeFrames[i];
            if (frame == null || frame.sprite == null) continue;
            SetBaseSprite(frame);
            if (i == lastValidIdx) onLastFrameStart?.Invoke();
            yield return new WaitForSeconds(Mathf.Max(frame.duration, 0.05f));
        }
        if (_spriteSwapper != null) _spriteSwapper.EnableHitSprite(true);
    }

    // ======================================================
    // Fade（ワープ用）
    // ======================================================

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(duration, 0.001f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetAlpha(to);
    }

    // ======================================================
    // Sand Smoke
    // ======================================================

    private IEnumerator SpawnSmokesCoroutine(int[] indices, SmokeCloud[] smokes, System.Action onComplete)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            var sp = _spawner.GetSpawnPoint(indices[i]);
            if (sp != null)
                smokes[i] = SpawnSmoke(new Vector3(sp.position.x, sp.position.y, transform.position.z));
            if (i < indices.Length - 1)
                yield return new WaitForSeconds(warpSmokeStagger);
        }
        yield return new WaitForSeconds(warpSmokeStagger);
        onComplete?.Invoke();
    }

    private SmokeCloud SpawnSmoke(Vector3 worldPos)
    {
        if (smokeParticlePrefab == null) return null;

        var go    = Instantiate(smokeParticlePrefab, worldPos, Quaternion.identity);
        var smoke = go.GetComponent<SmokeCloud>();
        if (smoke == null) { Destroy(go); return null; }

        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startColor      = new ParticleSystem.MinMaxGradient(smokeColor);
            main.gravityModifier = smokeGravityModifier;
            main.startSize       = new ParticleSystem.MinMaxCurve(smokeParticleSizeMin, smokeParticleSizeMax);
        }

        smoke.SetFadeDurations(smokeFadeInDuration, smokeFadeOutDuration);
        smoke.SetEmissionRate(smokeEmissionRate);

        var grain = smoke.SandGrainParticle;
        if (grain != null)
        {
            var gm = grain.main;
            gm.startColor      = new ParticleSystem.MinMaxGradient(sandGrainColor);
            if (sandGrainLifetime > 0f)
                gm.startLifetime = new ParticleSystem.MinMaxCurve(sandGrainLifetime);
            gm.gravityModifier = sandGrainGravityModifier;
            gm.startSize       = new ParticleSystem.MinMaxCurve(sandGrainSizeMin, sandGrainSizeMax);
            gm.startSpeed      = new ParticleSystem.MinMaxCurve(sandGrainSpeedMin, sandGrainSpeedMax);
            var em = grain.emission;
            em.rateOverTime = sandGrainEmissionRate;

            var vel = grain.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.World;
            vel.y       = new ParticleSystem.MinMaxCurve(0.4f);

            if (sandGrainSpreadX > 0f)
            {
                var noise          = grain.noise;
                noise.enabled      = true;
                noise.separateAxes = true;
                noise.strengthX    = new ParticleSystem.MinMaxCurve(sandGrainSpreadX);
                noise.strengthY    = new ParticleSystem.MinMaxCurve(0f);
                noise.strengthZ    = new ParticleSystem.MinMaxCurve(0f);
                noise.frequency    = 0.5f;
                noise.scrollSpeed  = new ParticleSystem.MinMaxCurve(0.5f);
                noise.octaveCount  = 1;
                noise.quality      = ParticleSystemNoiseQuality.Low;
            }
        }

        smoke.SetSandGrainGravity(sandGrainGravityModifier);
        if (sandGrainLifetime > 0f)
            smoke.SetSandGrainLifetime(sandGrainLifetime);
        smoke.SetSandGrainSpreadX(sandGrainSpreadX);
        smoke.SetSandGrainSpreadY(sandGrainSpreadY);
        smoke.Initialize(smokeRadius, smokeDuration, smokeExpansionSpeed, smokeCloudDissolveSE);

        return smoke;
    }

    // ======================================================
    // Helpers
    // ======================================================

    private void PlaySmokeSe()
    {
        if (smokeSeClip == null || _audioSource == null) return;
        float vol = smokeSeVolume *
            (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        _audioSource.PlayOneShot(smokeSeClip, vol);
    }

    private void SetBaseSprite(Sprite sp, Vector2 offset = default)
    {
        if (sp == null) return;
        _currentBaseOffset = offset;
        if (_spriteSwapper != null) _spriteSwapper.SetBaseSprite(sp);
        else if (spriteRenderer != null) spriteRenderer.sprite = sp;
        if (_spriteSwapper == null || !_spriteSwapper.IsHitActive)
            ApplyOffset(offset);
    }

    private void SetBaseSprite(ShamanFrame frame)
    {
        if (frame == null || frame.sprite == null) return;
        SetBaseSprite(frame.sprite, frame.offset);
        ApplyCollider(frame);
    }

    private void ApplyCollider(ShamanFrame frame)
    {
        if (bodyCollider == null || frame == null) return;
        if (frame.colliderSize.sqrMagnitude > 0.0001f)
            bodyCollider.size = frame.colliderSize;
        bodyCollider.offset = frame.colliderOffset;
    }

    private void ApplyOffset(Vector2 offset)
    {
        if (spriteRenderer == null) return;
        var t = spriteRenderer.transform;
        t.localPosition = new Vector3(offset.x, offset.y, t.localPosition.z);
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderer == null) return;
        var c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }

    // ======================================================
    // Gizmos
    // ======================================================

    private ShamanFrame GetPreviewFrame(ShamanPreviewSprite mode)
    {
        switch (mode)
        {
            case ShamanPreviewSprite.Idle:    return GetFrame(idleFrames, 0);
            case ShamanPreviewSprite.Idle2:   return GetFrame(idleFrames, 1);
            case ShamanPreviewSprite.Idle3:   return GetFrame(idleFrames, 2);
            case ShamanPreviewSprite.Hit:     return hitFrame;
            case ShamanPreviewSprite.Attack1: return GetFrame(attackFrames, 0);
            case ShamanPreviewSprite.Attack2: return GetFrame(attackFrames, 1);
            case ShamanPreviewSprite.Attack3: return GetFrame(attackFrames, 2);
            case ShamanPreviewSprite.Smoke1:  return GetFrame(smokeFrames,  0);
            case ShamanPreviewSprite.Smoke2:  return GetFrame(smokeFrames,  1);
            case ShamanPreviewSprite.Smoke3:  return GetFrame(smokeFrames,  2);
            case ShamanPreviewSprite.Smoke4:  return GetFrame(smokeFrames,  3);
            case ShamanPreviewSprite.Smoke5:  return GetFrame(smokeFrames,  4);
            case ShamanPreviewSprite.Thunder1: return GetFrame(thunderFrames, 0);
            case ShamanPreviewSprite.Thunder2: return GetFrame(thunderFrames, 1);
            case ShamanPreviewSprite.Thunder3: return GetFrame(thunderFrames, 2);
            case ShamanPreviewSprite.Thunder4: return GetFrame(thunderFrames, 3);
            default:                           return GetFrame(idleFrames, 0);
        }
    }

    private ShamanFrame GetFrame(ShamanFrame[] frames, int index)
    {
        if (frames == null || index >= frames.Length) return null;
        return frames[index];
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        // FirePoint_Staff
        if (firePointStaff != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(firePointStaff.position, 0.12f);
            string label = "FirePoint_Staff";
            if (!Application.isPlaying && attackFrames != null)
            {
                int fi = previewSprite == ShamanPreviewSprite.Attack1 ? 0
                       : previewSprite == ShamanPreviewSprite.Attack2 ? 1
                       : previewSprite == ShamanPreviewSprite.Attack3 ? 2 : -1;
                if (fi >= 0 && fi < attackFrames.Length && attackFrames[fi] != null)
                    label = $"Muzzle [Attack{fi + 1}] ({attackFrames[fi].muzzleOffset.x:F2}, {attackFrames[fi].muzzleOffset.y:F2})";
            }
            UnityEditor.Handles.Label(firePointStaff.position + Vector3.up * 0.2f, label);
        }

        // ThunderCloud spawn position
        Vector3 tPos = new Vector3(thunderCloudPosition.x, thunderCloudPosition.y, transform.position.z);
        Color thunderGizmoColor = new Color(1f, 0.75f, 0f, 0.9f);
        Gizmos.color = thunderGizmoColor;
        Gizmos.DrawWireSphere(tPos, 0.25f);
        float cs = 0.4f;
        Gizmos.DrawLine(tPos - Vector3.right * cs, tPos + Vector3.right * cs);
        Gizmos.DrawLine(tPos - Vector3.up    * cs, tPos + Vector3.up    * cs);
        UnityEditor.Handles.color = thunderGizmoColor;
        UnityEditor.Handles.Label(tPos + Vector3.up * 0.5f,
            $"ThunderCloud ({thunderCloudPosition.x:F2}, {thunderCloudPosition.y:F2})");

        // Tornado preview
        if (!Application.isPlaying && previewSprite == ShamanPreviewSprite.TornadoPreview)
        {
            Color tornadoColor = new Color(0.3f, 0.75f, 1f, 0.9f);
            Vector3 center = new Vector3(tornadoPreviewPos.x, tornadoPreviewPos.y, transform.position.z);

            // 最大半径（TornadoCloud.maxRadius デフォルト値 2f）
            float radius = 2f;
            Gizmos.color = tornadoColor;
            Gizmos.DrawWireSphere(center, radius);

            // 渦巻きを螺旋線で表現
            UnityEditor.Handles.color = tornadoColor;
            int spiralSteps = 48;
            for (int loop = 1; loop <= 3; loop++)
            {
                float r0 = radius * (loop - 1) / 3f;
                float r1 = radius * loop / 3f;
                for (int s = 0; s < spiralSteps; s++)
                {
                    float t0 = (float)s       / spiralSteps;
                    float t1 = (float)(s + 1) / spiralSteps;
                    float a0 = t0 * Mathf.PI * 2f;
                    float a1 = t1 * Mathf.PI * 2f;
                    float ir0 = Mathf.Lerp(r0, r1, t0);
                    float ir1 = Mathf.Lerp(r0, r1, t1);
                    Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * ir0, Mathf.Sin(a0) * ir0, 0f);
                    Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * ir1, Mathf.Sin(a1) * ir1, 0f);
                    Gizmos.DrawLine(p0, p1);
                }
            }

            UnityEditor.Handles.Label(center + Vector3.up * (radius + 0.4f),
                $"TornadoCloud  r={radius}  pos=({tornadoPreviewPos.x:F1},{tornadoPreviewPos.y:F1})");
        }

        // Collider preview（現在のプレビューフレームの当たり判定をGizmoで可視化）
        if (!Application.isPlaying)
        {
            bool isThunderPreview = previewSprite == ShamanPreviewSprite.Thunder1 ||
                                    previewSprite == ShamanPreviewSprite.Thunder2 ||
                                    previewSprite == ShamanPreviewSprite.Thunder3 ||
                                    previewSprite == ShamanPreviewSprite.Thunder4 ||
                                    previewSprite == ShamanPreviewSprite.ThunderAnimate ||
                                    previewSprite == ShamanPreviewSprite.TornadoPreview;
            if (!isThunderPreview)
            {
                var cf = GetPreviewFrame(previewSprite);
                if (cf != null && cf.colliderSize.sqrMagnitude > 0.0001f)
                {
                    Vector3 bodyPos = transform.position + new Vector3(cf.offset.x, cf.offset.y, 0f);
                    Vector3 colliderCenter = bodyPos + new Vector3(cf.colliderOffset.x, cf.colliderOffset.y, 0f);
                    Gizmos.color = new Color(0.15f, 0.95f, 0.15f, 0.9f);
                    Gizmos.DrawWireCube(colliderCenter, new Vector3(cf.colliderSize.x, cf.colliderSize.y, 0.05f));
                }
            }
        }
#endif
    }

    // ======================================================
    // Editor Preview
    // ======================================================

    private void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        StopEditorAnim();
#endif
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        if (previewSprite == ShamanPreviewSprite.TornadoPreview)
        {
            StopEditorAnim();
            UnityEditor.SceneView.RepaintAll();
            return;
        }

        bool isAnim = previewSprite == ShamanPreviewSprite.IdleAnimate    ||
                      previewSprite == ShamanPreviewSprite.AttackAnimate  ||
                      previewSprite == ShamanPreviewSprite.SmokeAnimate   ||
                      previewSprite == ShamanPreviewSprite.ThunderAnimate;

        if (isAnim)
        {
            int animType = previewSprite == ShamanPreviewSprite.IdleAnimate   ? 0
                         : previewSprite == ShamanPreviewSprite.AttackAnimate ? 1
                         : previewSprite == ShamanPreviewSprite.SmokeAnimate  ? 2 : 3;
            if (_editorAnimRunning && _editorAnimType != animType) StopEditorAnim();
            StartEditorAnim(animType);
        }
        else
        {
            StopEditorAnim();
            bool isThunder = previewSprite == ShamanPreviewSprite.Thunder1 ||
                             previewSprite == ShamanPreviewSprite.Thunder2 ||
                             previewSprite == ShamanPreviewSprite.Thunder3 ||
                             previewSprite == ShamanPreviewSprite.Thunder4;
            var f = GetPreviewFrame(previewSprite);
            if (f != null)
            {
                if (isThunder)
                {
                    if (thunderPreviewRenderer != null && f.sprite != null)
                        thunderPreviewRenderer.sprite = f.sprite;
                    ApplyThunderOffset(f.offset);
                }
                else
                {
                    if (f.sprite != null) spriteRenderer.sprite = f.sprite;
                    ApplyOffset(f.offset);
                    ApplyCollider(f);
                    bool isAttackFrame = previewSprite == ShamanPreviewSprite.Attack1 ||
                                         previewSprite == ShamanPreviewSprite.Attack2 ||
                                         previewSprite == ShamanPreviewSprite.Attack3;
                    if (isAttackFrame && firePointStaff != null)
                        firePointStaff.localPosition = new Vector3(f.muzzleOffset.x, f.muzzleOffset.y, 0f);
                }
            }
        }

        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private bool   _editorAnimRunning;
    private double _editorAnimLastTime;
    private int    _editorAnimFrameIdx;
    private int    _editorAnimType; // 0=Attack, 1=Smoke, 2=Thunder

    // animType: 0=Attack, 1=Smoke, 2=Thunder
    private void StartEditorAnim(int animType)
    {
        _editorAnimType     = animType;
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        var frames = GetEditorAnimFrames();
        if (frames != null && frames.Length > 0 && frames[0] != null)
        {
            if (_editorAnimType == 3)
            {
                if (thunderPreviewRenderer != null) thunderPreviewRenderer.sprite = frames[0].sprite;
                ApplyThunderOffset(frames[0].offset);
            }
            else
            {
                if (spriteRenderer != null) spriteRenderer.sprite = frames[0].sprite;
                ApplyOffset(frames[0].offset);
            }
        }
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private ShamanFrame[] GetEditorAnimFrames()
    {
        return _editorAnimType == 0 ? idleFrames
             : _editorAnimType == 1 ? attackFrames
             : _editorAnimType == 2 ? smokeFrames
             : thunderFrames;
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning) { StopEditorAnim(); return; }

        var frames = GetEditorAnimFrames();
        if (frames == null || frames.Length == 0) { StopEditorAnim(); return; }

        var current = frames[_editorAnimFrameIdx % frames.Length];
        double dur = (current != null && current.duration > 0f) ? current.duration : 0.15;
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % frames.Length;
            var next = frames[_editorAnimFrameIdx];
            if (next != null && next.sprite != null)
            {
                if (_editorAnimType == 3)
                {
                    if (thunderPreviewRenderer != null) thunderPreviewRenderer.sprite = next.sprite;
                    ApplyThunderOffset(next.offset);
                }
                else
                {
                    if (spriteRenderer != null) spriteRenderer.sprite = next.sprite;
                    ApplyOffset(next.offset);
                    if (_editorAnimType == 1 && firePointStaff != null)
                        firePointStaff.localPosition = new Vector3(next.muzzleOffset.x, next.muzzleOffset.y, 0f);
                }
            }
            UnityEditor.SceneView.RepaintAll();
        }
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
    }

    private void ApplyThunderOffset(Vector2 offset)
    {
        if (thunderPreviewRenderer == null) return;
        var t = thunderPreviewRenderer.transform;
        t.localPosition = new Vector3(offset.x, offset.y, t.localPosition.z);
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    // ======================================================
    // Context Menu
    // ======================================================

    [ContextMenu("Apply Camel Smoke Settings")]
    private void ApplyCamelSmokeSettings()
    {
#if UNITY_EDITOR
        string prefabPath = UnityEditor.AssetDatabase.GUIDToAssetPath("de70ff092b954e14590948e0660c15de");
        if (!string.IsNullOrEmpty(prefabPath))
            smokeParticlePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        string sePath = UnityEditor.AssetDatabase.GUIDToAssetPath("ff905b34adca9c7469577b7fa9f06e43");
        if (!string.IsNullOrEmpty(sePath))
            smokeCloudDissolveSE = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(sePath);

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[ShamanController] Camel smoke settings applied.");
#endif
    }
}
