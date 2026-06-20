using System.Collections;
using UnityEngine;

[System.Serializable]
public class PhantomAttackEntry
{
    public Sprite  sprite;
    public Vector2 muzzleOffset;
}

public class PhantomController : MonoBehaviour
{
    private enum PhantomPhase { Ghost, Solid }
    private enum MoveState  { Idle, Gliding }
    private enum WarpState  { None, FadeOut, Invisible, FadeIn }

    // ===== Phase =====
    [Header("Phase")]
    [SerializeField] float ghostAlpha   = 0.35f;
    [SerializeField] float solidDuration = 20f;

    // ===== Mask Block HP =====
    [Header("Mask Block HP")]
    [SerializeField] float maskMaxBlockHP = 50f;

    // ===== Movement =====
    [Header("Movement")]
    [SerializeField] float slowGlideSpeed = 1.5f;
    [SerializeField] float fastGlideSpeed = 6f;
    [SerializeField] AnimationCurve fastGlideSpeedCurve = new AnimationCurve(
        new Keyframe(0f,  2f,  0f,  0f),
        new Keyframe(1f, 14f, 28f,  0f));
    [SerializeField] float idleDuration   = 1.2f;
    [SerializeField] int   warpSpawnIndexMin = 0;
    [SerializeField] int   warpSpawnIndexMax = 8;

    // ===== Warp =====
    [Header("Warp")]
    [SerializeField] int   glidesPerWarp         = 3;
    [SerializeField] float warpFadeOutDuration   = 0.4f;
    [SerializeField] float warpInvisibleDuration = 0.3f;
    [SerializeField] float warpFadeInDuration    = 0.5f;
    [SerializeField] float spawnScale            = 0.75f;

    // ===== Bob =====
    [Header("Bob")]
    [SerializeField] float bobAmplitude   = 0.10f;
    [SerializeField] float bobFrequency   = 0.9f;
    [SerializeField] float driftAmplitude = 0.4f;
    [SerializeField] float driftFrequency = 0.35f;
    [SerializeField] float tiltAmplitude  = 4f;
    [SerializeField] float tiltFrequency  = 0.6f;

    // ===== Sprites =====
    [Header("Sprites")]
    [SerializeField] Sprite idleSprite;
    [SerializeField] PhantomAttackEntry[] attackEntries;
    [SerializeField] float attackSpriteDuration = 0.15f;

#if UNITY_EDITOR
    [Header("Editor Preview")]
    [SerializeField] int previewAttackIndex = 0;
#endif

    // ===== Glide Offset =====
    [Header("Glide Offset")]
    [SerializeField] Vector2 glideOffset = Vector2.zero;

    // ===== Mask Hit Effects =====
    [Header("Mask Hit Effects")]
    [SerializeField] AudioClip maskHitSeClip;
    [Range(0f, 1f)]
    [SerializeField] float maskHitSeVolume = 1f;

    // ===== Mask Break Effects =====
    [Header("Mask Break Effects")]
    [SerializeField] GameObject maskBreakEffectPrefab;
    [SerializeField] AudioClip[] maskBreakSeClips = new AudioClip[3];
    [Range(0f, 1f)]
    [SerializeField] float maskBreakSeVolume         = 1f;
    [SerializeField] float breakEffectDestroySeconds = 2f;

    // ===== Mask Restore Effects =====
    [Header("Mask Restore Effects")]
    [SerializeField] GameObject maskRestoreEffectPrefab;
    [SerializeField] AudioClip  maskRestoreSeClip;
    [Range(0f, 1f)]
    [SerializeField] float maskRestoreSeVolume         = 1f;
    [SerializeField] float restoreEffectDestroySeconds = 2f;

    // ===== References =====
    [Header("References")]
    [SerializeField] SpriteRenderer  spriteRenderer;
    [SerializeField] EnemySpriteShake spriteShake;
    [SerializeField] Collider2D       bodyCollider;
    [SerializeField] Collider2D       maskCollider;
    [SerializeField] Transform        spriteHolder;

    // ===== Particle Systems =====
    [Header("Particle Systems")]
    [SerializeField] ParticleSystem bodyMistPS;
    [SerializeField] ParticleSystem maskAuraPS;
    [SerializeField] float ghostBodyMistRate = 8f;
    [SerializeField] float solidBodyMistRate = 15f;

    // ----- private state -----
    private EnemyShooter       _shooter;
    private EnemySpriteSwapper _spriteSwapper;
    private AudioSource        _audioSource;
    private Transform          _phantomMuzzle;
    private EnemySpawner       _spawner;

    private PhantomPhase _phase;
    private MoveState    _moveState;
    private WarpState    _warpState;

    private float _solidTimer;
    private float _idleTimer;
    private int   _glideCount;
    private float _warpStateTimer;
    private float _bobTime;
    private float _maskBlockHP;
    private bool  _isReady;

    private Vector3 _basePos;
    private Vector3 _spriteHolderBasePos;
    private int     _currentSpawnIndex = 4; // SP5
    private int     _originSpawnIndex  = -1;
    private int     _targetSpawnIndex  = -1;
    private Vector3 _glideTargetPos;
    private float   _glideTotalDist;

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    // ============================================================
    // Lifecycle
    // ============================================================

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteShake == null)    spriteShake    = GetComponent<EnemySpriteShake>();
        if (spriteHolder == null)   spriteHolder   = transform.Find("SpriteHolder");

        _shooter       = GetComponent<EnemyShooter>();
        _spriteSwapper = GetComponent<EnemySpriteSwapper>();

        var muzzleGo = transform.Find("PhantomMuzzle");
        if (muzzleGo == null)
        {
            muzzleGo = new GameObject("PhantomMuzzle").transform;
            muzzleGo.SetParent(transform, false);
        }
        _phantomMuzzle = muzzleGo;

        if (_shooter != null)
        {
            _shooter.SetMuzzlePoints(new EnemyShooter.MuzzlePoint[]
            {
                new EnemyShooter.MuzzlePoint { muzzleTransform = _phantomMuzzle, offset = Vector3.zero }
            });
            _shooter.OnFired += OnShooterFired;
        }

        var mover = GetComponent<EnemyMover>();
        if (mover != null) mover.suppressMovement = true;
        if (spriteShake != null) spriteShake.externalPositioning = true;
        if (_shooter != null) _shooter.enabled = false;

        _maskBlockHP = maskMaxBlockHP;

        SetAlpha(0f);
        SetLocalScale(spawnScale);

        if (spriteHolder != null)
            _spriteHolderBasePos = spriteHolder.localPosition;

        if (bodyMistPS == null)
            bodyMistPS = transform.Find("BodyMist")?.GetComponent<ParticleSystem>();
        if (maskAuraPS == null)
            maskAuraPS = transform.Find("MaskAura")?.GetComponent<ParticleSystem>();

        if (bodyMistPS != null)
        {
            var vel = bodyMistPS.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.Local;
            // X・Y・Z 全てTwoCurvesモードで統一（モード不一致エラー回避）
            vel.x = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Constant(0f, 1f, -0.04f),
                AnimationCurve.Constant(0f, 1f,  0.04f));
            vel.y = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Constant(0f, 1f, 0f),
                AnimationCurve.Constant(0f, 1f, 0f));
            vel.z = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Constant(0f, 1f, 0f),
                AnimationCurve.Constant(0f, 1f, 0f));
        }

        DisableParticleEmission(bodyMistPS);
        DisableParticleEmission(maskAuraPS);

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        _spawner = FindFirstObjectByType<EnemySpawner>();

        // SP5（中央）に初期配置
        if (_spawner != null)
        {
            var sp = _spawner.GetSpawnPoint(4);
            if (sp != null)
                transform.position = new Vector3(sp.position.x, sp.position.y, transform.position.z);
        }
        _currentSpawnIndex = 4;
        _basePos = transform.position;

        if (_spriteSwapper != null && idleSprite != null)
            _spriteSwapper.SetBaseSprite(idleSprite);

        StartCoroutine(InitialFadeIn());
    }

    private IEnumerator InitialFadeIn()
    {
        float t = 0f;
        float dur = Mathf.Max(warpFadeInDuration, 0.001f);
        while (t < dur)
        {
            t += Time.deltaTime * GetTimeScale();
            float ratio = Mathf.Clamp01(t / dur);
            SetAlpha(ratio * ghostAlpha);
            SetLocalScale(Mathf.Lerp(spawnScale, 1f, ratio));
            yield return null;
        }
        SetAlpha(ghostAlpha);
        SetLocalScale(1f);
        ApplyGhostPhase();
        _isReady = true;
        EnterIdle();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        ApplyBob();

        if (!_isReady) return;

        float dt = Time.deltaTime * GetTimeScale();
        UpdatePhase(dt);
        UpdateMovement(dt);
        UpdateWarpAnim(dt);
    }

    // ============================================================
    // Phase
    // ============================================================

    private void ApplyGhostPhase()
    {
        _phase = PhantomPhase.Ghost;
        SetAlpha(ghostAlpha);
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (maskCollider != null) maskCollider.enabled = true;
        if (_shooter != null) _shooter.enabled = true;
        SetParticleEmissionRate(bodyMistPS, ghostBodyMistRate);
        EnableParticleEmission(bodyMistPS);
        EnableParticleEmission(maskAuraPS);
    }

    private void EnterSolid()
    {
        // ワープ中ならキャンセル
        if (_warpState != WarpState.None)
        {
            _warpState     = WarpState.None;
            _warpStateTimer = 0f;
        }

        _phase       = PhantomPhase.Solid;
        _solidTimer  = 0f;

        SetAlpha(1f);
        SetLocalScale(1f);

        if (bodyCollider != null) bodyCollider.enabled = true;
        if (maskCollider != null) maskCollider.enabled = false;

        if (_shooter != null)
        {
            var data = _shooter.GetEnemyData();
            if (data != null) _shooter.SetEnemyData(data);
            _shooter.enabled = true;
        }
        SetParticleEmissionRate(bodyMistPS, solidBodyMistRate);
        EnableParticleEmission(bodyMistPS);
        DisableParticleEmission(maskAuraPS);
    }

    private void UpdatePhase(float dt)
    {
        if (_phase != PhantomPhase.Solid) return;
        _solidTimer += dt;
        // ワープ中は終了まで待つ
        if (_solidTimer >= solidDuration && _warpState == WarpState.None)
            ExitSolid();
    }

    private void ExitSolid()
    {
        SpawnEffect(maskRestoreEffectPrefab, transform.position, restoreEffectDestroySeconds);
        PlaySe(maskRestoreSeClip, maskRestoreSeVolume);
        _maskBlockHP = maskMaxBlockHP;
        ApplyGhostPhase();
    }

    // ============================================================
    // Mask Hit（PhantomMaskDetectorから呼ぶ）
    // ============================================================

    public void OnMaskHit(int damage)
    {
        if (_phase != PhantomPhase.Ghost) return;
        _maskBlockHP -= damage;
        if (_maskBlockHP <= 0f)
        {
            _maskBlockHP = 0f;
            SpawnEffect(maskBreakEffectPrefab, transform.position, breakEffectDestroySeconds);
            PlaySe(PickRandomClip(maskBreakSeClips), maskBreakSeVolume);
            EnterSolid();
        }
        else
        {
            PlaySe(maskHitSeClip, maskHitSeVolume);
        }
    }

    // ============================================================
    // Warp Timer
    // ============================================================

    private void BeginWarp()
    {
        bool wasGliding      = _moveState == MoveState.Gliding;
        int  extraExclude    = wasGliding ? _targetSpawnIndex : -1;
        int  newIndex        = PickWarpTarget(extraExclude);
        if (newIndex < 0) return;

        _targetSpawnIndex = newIndex;
        _moveState        = MoveState.Idle;
        _warpState        = WarpState.FadeOut;
        _warpStateTimer   = 0f;
        SetSpriteHolderOffset(false);
    }

    private int PickWarpTarget(int extraExclude)
    {
        if (_spawner == null) return -1;
        int count = warpSpawnIndexMax - warpSpawnIndexMin + 1;
        int start = Random.Range(0, count);
        for (int i = 0; i < count; i++)
        {
            int idx = warpSpawnIndexMin + (start + i) % count;
            if (idx == _currentSpawnIndex) continue;
            if (extraExclude >= 0 && idx == extraExclude) continue;
            if (_spawner.GetSpawnPoint(idx) != null) return idx;
        }
        return -1;
    }

    private void UpdateWarpAnim(float dt)
    {
        if (_warpState == WarpState.None) return;
        _warpStateTimer += dt;

        float baseAlpha = _phase == PhantomPhase.Solid ? 1f : ghostAlpha;

        switch (_warpState)
        {
            case WarpState.FadeOut:
            {
                float t = 1f - Mathf.Clamp01(_warpStateTimer / Mathf.Max(warpFadeOutDuration, 0.001f));
                SetAlpha(t * baseAlpha);
                SetLocalScale(Mathf.Lerp(spawnScale, 1f, t));
                if (_warpStateTimer >= warpFadeOutDuration)
                {
                    SetAlpha(0f);
                    TeleportToTarget();
                    _warpState      = WarpState.Invisible;
                    _warpStateTimer = 0f;
                }
                break;
            }
            case WarpState.Invisible:
                if (_warpStateTimer >= warpInvisibleDuration)
                {
                    _warpState      = WarpState.FadeIn;
                    _warpStateTimer = 0f;
                }
                break;

            case WarpState.FadeIn:
            {
                float t = Mathf.Clamp01(_warpStateTimer / Mathf.Max(warpFadeInDuration, 0.001f));
                SetAlpha(t * baseAlpha);
                SetLocalScale(Mathf.Lerp(spawnScale, 1f, t));
                if (_warpStateTimer >= warpFadeInDuration)
                {
                    SetAlpha(baseAlpha);
                    SetLocalScale(1f);
                    _warpState  = WarpState.None;
                    _glideCount = 0;
                    EnterIdle();
                }
                break;
            }
        }
    }

    private void TeleportToTarget()
    {
        if (_spawner == null) return;
        var pt = _spawner.GetSpawnPoint(_targetSpawnIndex);
        if (pt == null) return;
        _basePos             = new Vector3(pt.position.x, pt.position.y, transform.position.z);
        _bobTime             = 0f;
        transform.position   = _basePos;
        transform.rotation   = Quaternion.identity;
        _currentSpawnIndex   = _targetSpawnIndex;
    }

    // ============================================================
    // Movement
    // ============================================================

    private void EnterIdle()
    {
        _moveState         = MoveState.Idle;
        _idleTimer         = 0f;
        _originSpawnIndex  = -1;
        SetSpriteHolderOffset(false);
    }

    private void UpdateMovement(float dt)
    {
        if (_warpState != WarpState.None) return;

        switch (_moveState)
        {
            case MoveState.Idle:
                _idleTimer += dt;
                if (_idleTimer >= idleDuration)
                    StartGlide();
                break;

            case MoveState.Gliding:
                float speed;
                if (IsAdjacent(_originSpawnIndex, _targetSpawnIndex))
                {
                    speed = slowGlideSpeed;
                }
                else
                {
                    float remaining2 = Vector3.Distance(_basePos, _glideTargetPos);
                    float t = _glideTotalDist > 0f ? Mathf.Clamp01(1f - remaining2 / _glideTotalDist) : 0f;
                    speed = fastGlideSpeedCurve.Evaluate(t);
                }
                _basePos = Vector3.MoveTowards(_basePos, _glideTargetPos, speed * dt);
                if (Vector3.Distance(_basePos, _glideTargetPos) < 0.01f)
                {
                    _basePos           = _glideTargetPos;
                    _currentSpawnIndex = _targetSpawnIndex;
                    _originSpawnIndex  = -1;
                    _glideCount++;
                    if (_glideCount >= glidesPerWarp)
                    {
                        _glideCount = 0;
                        SetSpriteHolderOffset(false);
                        BeginWarp();
                    }
                    else
                    {
                        EnterIdle();
                    }
                }
                break;
        }
    }

    private void StartGlide()
    {
        if (_spawner == null) return;
        int count = warpSpawnIndexMax - warpSpawnIndexMin + 1;
        int start = Random.Range(0, count);
        for (int i = 0; i < count; i++)
        {
            int idx = warpSpawnIndexMin + (start + i) % count;
            if (idx == _currentSpawnIndex) continue;
            var pt = _spawner.GetSpawnPoint(idx);
            if (pt == null) continue;

            _originSpawnIndex = _currentSpawnIndex;
            _targetSpawnIndex = idx;
            _glideTargetPos   = new Vector3(pt.position.x, pt.position.y, transform.position.z);
            _glideTotalDist   = Vector3.Distance(_basePos, _glideTargetPos);
            _moveState        = MoveState.Gliding;
            SetSpriteHolderOffset(true);
            return;
        }
    }

    private bool IsAdjacent(int from, int to)
    {
        if (from < 0 || to < 0) return false;
        int fr = from / 3, fc = from % 3;
        int tr = to   / 3, tc = to   % 3;
        return from != to && Mathf.Abs(fr - tr) <= 1 && Mathf.Abs(fc - tc) <= 1;
    }

    // ============================================================
    // Bob
    // ============================================================

    private void ApplyBob()
    {
        _bobTime += Time.deltaTime * GetTimeScale();
        float xOff  = Mathf.Sin(_bobTime * driftFrequency * Mathf.PI * 2f) * driftAmplitude;
        float yOff  = Mathf.Sin(_bobTime * bobFrequency   * Mathf.PI * 2f) * bobAmplitude;
        float tilt  = Mathf.Sin(_bobTime * tiltFrequency  * Mathf.PI * 2f) * tiltAmplitude;
        Vector3 shake = spriteShake != null ? spriteShake.CurrentOffset : Vector3.zero;
        transform.position = _basePos + new Vector3(xOff, yOff, 0f) + shake;
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private void SetSpriteHolderOffset(bool isGliding)
    {
        if (spriteHolder == null) return;
        var offset = isGliding ? new Vector3(glideOffset.x, glideOffset.y, 0f) : Vector3.zero;
        spriteHolder.localPosition = _spriteHolderBasePos + offset;
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderer == null) return;
        var c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }

    private void SetLocalScale(float scale)
    {
        transform.localScale = Vector3.one * scale;
    }

    private void OnDestroy()
    {
        if (_shooter != null)
            _shooter.OnFired -= OnShooterFired;
    }

    private void OnShooterFired()
    {
        if (attackEntries == null || attackEntries.Length == 0) return;
        var valid = System.Array.FindAll(attackEntries, e => e != null && e.sprite != null);
        if (valid.Length == 0) return;
        ApplyAttackEntry(valid[Random.Range(0, valid.Length)]);
    }

    private void ApplyAttackEntry(PhantomAttackEntry entry)
    {
        if (_phantomMuzzle != null)
            _phantomMuzzle.localPosition = new Vector3(entry.muzzleOffset.x, entry.muzzleOffset.y, 0f);
        if (_spriteSwapper != null)
            _spriteSwapper.TriggerAttack(entry.sprite, attackSpriteDuration);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackEntries == null) return;
        for (int i = 0; i < attackEntries.Length; i++)
        {
            var e = attackEntries[i];
            if (e == null) continue;
            var worldPos = transform.position + new Vector3(e.muzzleOffset.x, e.muzzleOffset.y, 0f);
#if UNITY_EDITOR
            bool isCurrent = (i == previewAttackIndex);
            Gizmos.color = isCurrent ? Color.cyan : new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(worldPos, isCurrent ? 0.12f : 0.08f);
            UnityEditor.Handles.Label(worldPos + Vector3.up * 0.15f, $"Muzzle [{i}]");
#endif
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (attackEntries == null || previewAttackIndex < 0 || previewAttackIndex >= attackEntries.Length) return;
        var entry = attackEntries[previewAttackIndex];

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && entry.sprite != null)
        {
            spriteRenderer.sprite = entry.sprite;
            UnityEditor.EditorUtility.SetDirty(spriteRenderer);
        }

        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Setup Phantom Muzzle")]
    private void SetupPhantomMuzzle()
    {
        var existing = transform.Find("PhantomMuzzle");
        if (existing != null) { Debug.Log("[Phantom] PhantomMuzzle already exists."); return; }
        var go = new GameObject("PhantomMuzzle");
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create PhantomMuzzle");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log("[Phantom] PhantomMuzzle created. EnemyShooterへのセットはPlay開始時のAwakeで自動実行されます。");
    }

    [ContextMenu("Preview Attack Sprite")]
    private void PreviewAttackSprite()
    {
        if (attackEntries == null || previewAttackIndex < 0 || previewAttackIndex >= attackEntries.Length) return;
        var entry = attackEntries[previewAttackIndex];

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && entry.sprite != null)
        {
            UnityEditor.Undo.RecordObject(spriteRenderer, "Preview Attack Sprite");
            spriteRenderer.sprite = entry.sprite;
            UnityEditor.EditorUtility.SetDirty(spriteRenderer);
        }

        var muzzleGo = transform.Find("PhantomMuzzle");
        if (muzzleGo != null)
        {
            UnityEditor.Undo.RecordObject(muzzleGo, "Preview Muzzle Position");
            muzzleGo.localPosition = new Vector3(entry.muzzleOffset.x, entry.muzzleOffset.y, 0f);
            UnityEditor.EditorUtility.SetDirty(muzzleGo.gameObject);
        }
        else
        {
            Debug.LogWarning("[Phantom] PhantomMuzzle not found. Run 'Setup Phantom Muzzle' first.");
        }

        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Preview Idle Sprite")]
    private void PreviewIdleSprite()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && idleSprite != null)
        {
            UnityEditor.Undo.RecordObject(spriteRenderer, "Preview Idle Sprite");
            spriteRenderer.sprite = idleSprite;
            UnityEditor.EditorUtility.SetDirty(spriteRenderer);
        }
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    private void SpawnEffect(GameObject prefab, Vector3 pos, float destroyAfter)
    {
        if (prefab == null) return;
        var fx = Instantiate(prefab, pos, Quaternion.identity);
        if (destroyAfter > 0f) Destroy(fx, destroyAfter);
    }

    private void PlaySe(AudioClip clip, float volume)
    {
        if (clip == null || _audioSource == null) return;
        float finalVolume = volume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        _audioSource.PlayOneShot(clip, finalVolume);
    }

    private AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        int valid = 0;
        foreach (var c in clips) if (c != null) valid++;
        if (valid == 0) return null;
        int pick = Random.Range(0, valid);
        foreach (var c in clips)
        {
            if (c == null) continue;
            if (pick == 0) return c;
            pick--;
        }
        return null;
    }

    private void SetParticleEmissionRate(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.rateOverTime = rate;
    }

    private void EnableParticleEmission(ParticleSystem ps)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.enabled = true;
    }

    private void DisableParticleEmission(ParticleSystem ps)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.enabled = false;
    }

    // ============================================================
    // Editor Setup
    // ============================================================

    [ContextMenu("Setup Particle Systems")]
    private void SetupParticleSystems()
    {
#if UNITY_EDITOR
        // 変更前にUndo登録することでエディタへの変更が確実に反映される
        var bodyT = transform.Find("BodyMist");
        var maskT = transform.Find("MaskAura");
        if (bodyT != null)
        {
            var ps = bodyT.GetComponent<ParticleSystem>();
            if (ps != null) UnityEditor.Undo.RecordObjects(
                new UnityEngine.Object[] { ps, ps.GetComponent<ParticleSystemRenderer>() },
                "Setup Phantom Particle Systems");
        }
        if (maskT != null)
        {
            var ps = maskT.GetComponent<ParticleSystem>();
            if (ps != null) UnityEditor.Undo.RecordObjects(
                new UnityEngine.Object[] { ps, ps.GetComponent<ParticleSystemRenderer>() },
                "Setup Phantom Particle Systems");
        }
#endif
        SetupBodyMist();
        SetupMaskAura();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (bodyMistPS != null)
        {
            UnityEditor.EditorUtility.SetDirty(bodyMistPS);
            var r = bodyMistPS.GetComponent<ParticleSystemRenderer>();
            if (r != null) UnityEditor.EditorUtility.SetDirty(r);
        }
        if (maskAuraPS != null)
        {
            UnityEditor.EditorUtility.SetDirty(maskAuraPS);
            var r = maskAuraPS.GetComponent<ParticleSystemRenderer>();
            if (r != null) UnityEditor.EditorUtility.SetDirty(r);
        }
#endif
        Debug.Log("[PhantomController] Particle Systems setup complete.");
    }

    private void SetupBodyMist()
    {
        var existing = transform.Find("BodyMist");
        bool isNew = (existing == null);

        if (isNew)
        {
            var t = new GameObject("BodyMist").transform;
            t.SetParent(transform, false);
            t.localPosition = Vector3.zero;
            bodyMistPS = t.gameObject.AddComponent<ParticleSystem>();

            var main = bodyMistPS.main;
            main.loop             = true;
            main.playOnAwake      = true;
            main.duration         = 3f;
            main.startLifetime    = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSpeed       = 0f;
            main.gravityModifier  = new ParticleSystem.MinMaxCurve(-0.06f);
            main.startRotation    = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            var startGrad = new Gradient();
            startGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.black, 0f),
                    new GradientColorKey(Color.black, 1f),
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            var bodyStartColor = new ParticleSystem.MinMaxGradient(startGrad);
            bodyStartColor.mode  = ParticleSystemGradientMode.RandomColor;
            main.startColor      = bodyStartColor;
            main.startSize       = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.maxParticles    = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = bodyMistPS.emission;
            emission.rateOverTime = ghostBodyMistRate;
            emission.enabled      = false;

            var sol = bodyMistPS.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f,   0f,   0f,   4f),
                new Keyframe(0.2f, 0.8f, 2f,   0.3f),
                new Keyframe(1f,   1f,   0.1f, 0f)
            ));

            var col = bodyMistPS.colorOverLifetime;
            col.enabled = true;
            var alphaGrad = new Gradient();
            alphaGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f,   0f),
                    new GradientAlphaKey(0.8f, 0.1f),
                    new GradientAlphaKey(0.6f, 0.6f),
                    new GradientAlphaKey(0f,   1f),
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(alphaGrad);

            var vel = bodyMistPS.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.Local;
            // 全軸をTwoCurvesモードで統一（異なるモードが混在するとY曲線が無効になる）
            vel.x = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Constant(0f, 1f, -0.04f),
                AnimationCurve.Constant(0f, 1f,  0.04f));
            vel.y = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Constant(0f, 1f, 0f),
                AnimationCurve.Constant(0f, 1f, 0f));
            vel.z = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Constant(0f, 1f, 0f),
                AnimationCurve.Constant(0f, 1f, 0f));

            var rol = bodyMistPS.rotationOverLifetime;
            rol.enabled = true;
            rol.z       = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        }
        else
        {
            bodyMistPS = existing.GetComponent<ParticleSystem>();
        }

        // 既存・新規ともに毎回更新
        var startGrad2 = new Gradient();
        startGrad2.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.black, 1f),
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        var startColor2 = new ParticleSystem.MinMaxGradient(startGrad2);
        startColor2.mode = ParticleSystemGradientMode.RandomColor;
        var mainAlways = bodyMistPS.main;
        mainAlways.startColor = startColor2;

        var shape = bodyMistPS.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.22f;
        shape.radiusThickness = 1f;

        var renderer = bodyMistPS.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode     = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        renderer.sortingOrder   = (spriteRenderer != null ? spriteRenderer.sortingOrder : 0) + 1;
#if UNITY_EDITOR
        var mat = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (mat != null) renderer.sharedMaterial = mat;
        else Debug.LogWarning("[PhantomController] Default-Particle.mat not found!");
#endif
    }

    private void SetupMaskAura()
    {
        var existing = transform.Find("MaskAura");
        Vector3 savedPos = existing != null ? existing.localPosition : new Vector3(0f, 0.12f, 0f);
        if (existing != null) DestroyImmediate(existing.gameObject);
        var t = new GameObject("MaskAura").transform;
        t.SetParent(transform, false);
        t.localPosition = savedPos;
        maskAuraPS = t.gameObject.AddComponent<ParticleSystem>();

        var main = maskAuraPS.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.duration        = 1f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        // 各パーティクルがスポーン時にグラデーションからランダムに色を選ぶ
        var startGrad = new Gradient();
        startGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.85f, 1f,   0.97f), 0f),    // 白ティール
                new GradientColorKey(new Color(0f,    0.9f, 0.8f),  0.3f),  // ティール
                new GradientColorKey(new Color(0.3f,  0.4f, 1f),    0.65f), // ブルー
                new GradientColorKey(new Color(0.5f,  0.1f, 0.7f),  1f),    // パープル
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        var maskStartColor = new ParticleSystem.MinMaxGradient(startGrad);
        maskStartColor.mode = ParticleSystemGradientMode.RandomColor;
        main.startColor = maskStartColor;
        main.maxParticles    = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = maskAuraPS.emission;
        emission.rateOverTime = 10f;
        emission.enabled      = false;

        var shape = maskAuraPS.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.1f;

        // アルファだけフェードアウト（色はstartColorのランダム選択に任せる）
        var col = maskAuraPS.colorOverLifetime;
        col.enabled = true;
        var alphaGrad = new Gradient();
        alphaGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.7f, 0.4f),
                new GradientAlphaKey(0f,   1f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(alphaGrad);

        var renderer = maskAuraPS.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        renderer.sortingOrder   = (spriteRenderer != null ? spriteRenderer.sortingOrder : 0) + 2;
#if UNITY_EDITOR
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_WispParticle.mat");
        if (mat != null) renderer.sharedMaterial = mat;
#endif
    }
}
