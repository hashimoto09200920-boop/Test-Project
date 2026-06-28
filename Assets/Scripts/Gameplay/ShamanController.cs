using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShamanController : MonoBehaviour
{
    public enum ShamanPreviewSprite
    {
        Idle,
        Hit,
        Attack1, Attack2, Attack3, AttackAnimate,
        Smoke1, Smoke2, Smoke3, Smoke4, Smoke5, SmokeAnimate
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
    }

    // ======================================================
    // Inspector fields
    // ======================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform      firePointStaff;
    [SerializeField] private AudioClip      smokeSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float          smokeSeVolume = 1f;

    [Header("Editor Preview")]
    [SerializeField] private ShamanPreviewSprite previewSprite = ShamanPreviewSprite.Idle;

    [Header("Sprites")]
    [SerializeField] private ShamanFrame   idleFrame;
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

    // ======================================================
    // Runtime state
    // ======================================================

    private enum Phase { Front, Back }
    private Phase _phase = Phase.Front;
    private bool  _phaseTransitioned;
    private float _hpThresholdPercent;

    private EnemyMover         _mover;
    private EnemyShooter       _shooter;
    private EnemySpriteSwapper _spriteSwapper;
    private AudioSource        _audioSource;
    private EnemyStats         _enemyStats;
    private EnemySpawner       _spawner;

    private Vector2   _currentBaseOffset;
    private bool      _wasHitActive;
    private int       _currentSpawnIndex = 4;
    private Coroutine _attackAnimCoroutine;

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

        var data = _shooter != null ? _shooter.GetEnemyData() : null;
        _hpThresholdPercent = data != null ? data.hpThresholdPercentage / 100f : 0.5f;
    }

    private void OnDestroy()
    {
        if (_shooter != null)
            _shooter.OnFired -= OnShooterFired;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        _spawner = FindFirstObjectByType<EnemySpawner>();

        if (_spawner != null)
        {
            var sp = _spawner.GetSpawnPoint(4);
            if (sp != null)
                transform.position = new Vector3(sp.position.x, sp.position.y, transform.position.z);
        }
        _currentSpawnIndex = 4;

        SetBaseSprite(idleFrame.sprite, idleFrame.offset);
        StartCoroutine(MainLoop());
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
        if (_phaseTransitioned || _phase == Phase.Back || _enemyStats == null) return;
        if (_enemyStats.HP <= _enemyStats.MaxHP * _hpThresholdPercent)
        {
            _phase = Phase.Back;
            _phaseTransitioned = true;
        }
    }

    // ======================================================
    // Main Loop
    // ======================================================

    private IEnumerator MainLoop()
    {
        SetBaseSprite(idleFrame.sprite, idleFrame.offset);

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
                yield return StartCoroutine(WarpRoutine());
        }

        yield return StartCoroutine(BackPhaseLoop());
    }

    private IEnumerator BackPhaseLoop()
    {
        // TODO: 後半フェーズ（竜巻・雷雲）実装
        while (true)
        {
            float elapsed = 0f;
            while (elapsed < warpInterval)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            yield return StartCoroutine(WarpRoutine());
        }
    }

    // ======================================================
    // Warp
    // ======================================================

    private IEnumerator WarpRoutine()
    {
        if (_spawner == null) yield break;

        // ランダムにSPを選定。最後の1個がワープ先
        int[] indices = PickRandomSpawnIndices(warpSmokeCount);
        if (indices == null || indices.Length == 0) yield break;
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
        if (destSmoke != null)
        {
            var destSp = _spawner.GetSpawnPoint(destIdx);
            if (destSp != null)
            {
                transform.position = new Vector3(destSp.position.x, destSp.position.y, transform.position.z);
                _currentSpawnIndex = destIdx;
            }
        }

        // フェードイン・杖攻撃再開
        yield return StartCoroutine(FadeAlpha(0f, 1f, warpFadeInDuration));
        if (_shooter != null) _shooter.enabled = true;
        SetBaseSprite(idleFrame.sprite, idleFrame.offset);
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

    // ======================================================
    // Attack sprite
    // ======================================================

    private void OnShooterFired()
    {
        if (attackFrames == null || attackFrames.Length == 0) return;
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
            SetBaseSprite(frame.sprite, frame.offset);
            if (firePointStaff != null)
                firePointStaff.localPosition = new Vector3(frame.muzzleOffset.x, frame.muzzleOffset.y, 0f);
            yield return new WaitForSeconds(Mathf.Max(frame.duration, 0.05f));
        }
        SetBaseSprite(idleFrame.sprite, idleFrame.offset);
        _attackAnimCoroutine = null;
    }

    // ======================================================
    // Smoke Animation
    // ======================================================

    private IEnumerator PlaySmokeAnimation(System.Action onLastFrameStart = null)
    {
        if (smokeFrames == null || smokeFrames.Length == 0) yield break;
        PlaySmokeSe();

        int lastValidIdx = -1;
        for (int i = smokeFrames.Length - 1; i >= 0; i--)
            if (smokeFrames[i] != null && smokeFrames[i].sprite != null) { lastValidIdx = i; break; }

        for (int i = 0; i < smokeFrames.Length; i++)
        {
            var frame = smokeFrames[i];
            if (frame == null || frame.sprite == null) continue;
            SetBaseSprite(frame.sprite, frame.offset);
            if (i == lastValidIdx) onLastFrameStart?.Invoke();
            yield return new WaitForSeconds(Mathf.Max(frame.duration, 0.05f));
        }
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
        ApplyOffset(offset);
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
            case ShamanPreviewSprite.Hit:    return hitFrame;
            case ShamanPreviewSprite.Attack1: return GetFrame(attackFrames, 0);
            case ShamanPreviewSprite.Attack2: return GetFrame(attackFrames, 1);
            case ShamanPreviewSprite.Attack3: return GetFrame(attackFrames, 2);
            case ShamanPreviewSprite.Smoke1:  return GetFrame(smokeFrames,  0);
            case ShamanPreviewSprite.Smoke2:  return GetFrame(smokeFrames,  1);
            case ShamanPreviewSprite.Smoke3:  return GetFrame(smokeFrames,  2);
            case ShamanPreviewSprite.Smoke4:  return GetFrame(smokeFrames,  3);
            case ShamanPreviewSprite.Smoke5:  return GetFrame(smokeFrames,  4);
            default:                          return idleFrame;
        }
    }

    private ShamanFrame GetFrame(ShamanFrame[] frames, int index)
    {
        if (frames == null || index >= frames.Length) return null;
        return frames[index];
    }

    private void OnDrawGizmosSelected()
    {
        if (firePointStaff == null) return;
#if UNITY_EDITOR
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

        bool isAnim = previewSprite == ShamanPreviewSprite.AttackAnimate ||
                      previewSprite == ShamanPreviewSprite.SmokeAnimate;

        if (isAnim)
        {
            bool isAttack = previewSprite == ShamanPreviewSprite.AttackAnimate;
            if (_editorAnimRunning && _editorAnimIsAttack != isAttack) StopEditorAnim();
            StartEditorAnim(isAttack);
        }
        else
        {
            StopEditorAnim();
            var f = GetPreviewFrame(previewSprite);
            if (f != null)
            {
                if (f.sprite != null) spriteRenderer.sprite = f.sprite;
                ApplyOffset(f.offset);
                bool isAttackFrame = previewSprite == ShamanPreviewSprite.Attack1 ||
                                     previewSprite == ShamanPreviewSprite.Attack2 ||
                                     previewSprite == ShamanPreviewSprite.Attack3;
                if (isAttackFrame && firePointStaff != null)
                    firePointStaff.localPosition = new Vector3(f.muzzleOffset.x, f.muzzleOffset.y, 0f);
            }
        }

        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private bool   _editorAnimRunning;
    private double _editorAnimLastTime;
    private int    _editorAnimFrameIdx;
    private bool   _editorAnimIsAttack;

    private void StartEditorAnim(bool isAttack)
    {
        _editorAnimIsAttack = isAttack;
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        var frames = _editorAnimIsAttack ? attackFrames : smokeFrames;
        if (frames != null && frames.Length > 0 && frames[0] != null)
        {
            spriteRenderer.sprite = frames[0].sprite;
            ApplyOffset(frames[0].offset);
        }
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning) { StopEditorAnim(); return; }

        var frames = _editorAnimIsAttack ? attackFrames : smokeFrames;
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
                spriteRenderer.sprite = next.sprite;
                ApplyOffset(next.offset);
                if (_editorAnimIsAttack && firePointStaff != null)
                    firePointStaff.localPosition = new Vector3(next.muzzleOffset.x, next.muzzleOffset.y, 0f);
            }
            UnityEditor.SceneView.RepaintAll();
        }
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
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
