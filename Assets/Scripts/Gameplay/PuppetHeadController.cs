using System.Collections;
using UnityEngine;

public enum PuppetPreviewMode { None, Normal0, Normal1, Attack }

[ExecuteAlways]
public class PuppetHeadController : MonoBehaviour
{
    [System.Serializable]
    public class HeadFrame
    {
        public Sprite sprite;
        [Tooltip("スプライト表示位置オフセット（ワールド単位）")]
        public float offsetX = 0f;
        [Tooltip("スプライト表示位置オフセット（ワールド単位）")]
        public float offsetY = 0f;
        [Tooltip("糸接続点オフセット（頭のローカル座標基準）")]
        public Vector2 stringOffset = new Vector2(0f, 0.5f);
    }

    [Header("Normal Frames")]
    [SerializeField] private HeadFrame[] normalFrames;
    [Tooltip("通常フレームの切り替え間隔（秒）")]
    [SerializeField] private float normalFrameInterval = 0.15f;

    [Header("Attack Frame")]
    [SerializeField] private HeadFrame attackFrame;
    [Tooltip("攻撃フレームの表示時間（秒）")]
    [SerializeField] private float attackFrameDuration = 0.25f;

    [Header("Pendulum Settings")]
    [Tooltip("振れ幅（度）最小")]
    [SerializeField] private float swingAngleMin = 20f;
    [Tooltip("振れ幅（度）最大")]
    [SerializeField] private float swingAngleMax = 40f;
    [Tooltip("往復周期（秒）最小")]
    [SerializeField] private float periodMin = 2.5f;
    [Tooltip("往復周期（秒）最大")]
    [SerializeField] private float periodMax = 4.5f;
    [Tooltip("ポーズ発生確率（0〜1, 中央通過時）")]
    [Range(0f, 1f)]
    [SerializeField] private float pauseChance = 0.25f;
    [Tooltip("ポーズ時間（秒）最小")]
    [SerializeField] private float pauseDurationMin = 0.3f;
    [Tooltip("ポーズ時間（秒）最大")]
    [SerializeField] private float pauseDurationMax = 0.8f;
    [Tooltip("振り子アーム長（世界座標単位）。糸の上端からhead中心までの距離")]
    [SerializeField] private float pendulumLength = 5f;
    [Tooltip("中央通過での発射クールダウン（秒）")]
    [SerializeField] private float fireCooldown = 1.5f;

    [Header("String (LineRenderer)")]
    [Tooltip("接続点オフセット（ローカル座標基準・頭頂部方向が正のY）")]
    [SerializeField] private Vector2 stringAttachOffset = new Vector2(0f, 0.5f);
    [SerializeField] private int stringSegments = 12;
    [SerializeField] private float stringDrop = 0.15f;
    [SerializeField] private LineRenderer stringRenderer;
    [SerializeField] private LineRenderer outlineRenderer;
    [Range(1.1f, 3f)]
    [SerializeField] private float outlineWidthMultiplier = 1.4f;

    [Header("String Color")]
    [SerializeField] private Gradient stringGradient;

    [Header("String Colliders")]
    [SerializeField] private int stringColliderCount = 6;
    [SerializeField] private float stringColliderRadius = 0.06f;

    [Header("String Hit")]
    [Tooltip("糸の耐久HP（この回数だけ反射弾が当たると切断）")]
    [SerializeField] private int stringHp = 3;
    [SerializeField] private AudioClip[] stringNormalHitClips;
    [SerializeField] private AudioClip[] stringJustHitClips;
    [Range(0f, 1f)]
    [SerializeField] private float stringHitSeVolume = 1f;

    [Header("String Regen")]
    [Tooltip("切断後に糸が再生成されるまでの秒数")]
    [SerializeField] private float stringRegenDelay = 3f;
    [Tooltip("再生パーティクルが上端→頭までトレースする時間（秒）")]
    [SerializeField] private float regenTraceTime = 1.0f;
    [SerializeField] private ParticleSystem regenParticle;
    [SerializeField] private LineRenderer regenLineRenderer;

    [Header("String Cut VFX")]
    [SerializeField] private ParticleSystem cutParticlePrefab;

    [Header("Editor Preview")]
    [SerializeField] private PuppetPreviewMode previewMode = PuppetPreviewMode.None;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("スプライトのみ格納する子GO（Setup Sprite Holderで自動設定）")]
    [SerializeField] private Transform spriteHolder;
    [SerializeField] private EnemySpriteShake spriteShake;
    [SerializeField] private AudioSource audioSource;

    // Pendulum
    private float _phase;
    private float _currentAmplitude;
    private float _currentPeriod;
    private float _prevAngle;
    private bool _isPaused;
    private float _pauseTimer;
    private Vector3 _anchorWorldPos;
    private float _lastFireTime = -999f;

    // Frames
    private int _currentNormalFrameIdx;
    private Coroutine _attackCo;
    private Vector2 _currentFrameOffset;

    // String
    private CapsuleCollider2D[] _segmentColliders;
    private Rigidbody2D[] _segmentRigidbodies;
    private bool _isStringCut;
    private bool _isRegenTracing;
    private int _currentStringHp;
    private Coroutine _regenCo;
    private Coroutine _regenTraceCo;
    private float _lastStringHitTime = -999f;
    private const float StringHitCooldown = 0.1f;
    private Vector3 _lastStringHitPos;
    private Vector3 _frozenPos;
    private Vector2 _frozenStringOffset;

    // Other
    private EnemyShooter _enemyShooter;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteShake == null) spriteShake = GetComponent<EnemySpriteShake>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        var mover = GetComponent<EnemyMover>();
        if (mover != null) mover.suppressMovement = true;
        if (spriteShake != null) spriteShake.externalPositioning = true;

        _enemyShooter = GetComponent<EnemyShooter>();
        if (_enemyShooter != null)
            _enemyShooter.OnFired += OnShooterFired;

        if (regenLineRenderer != null) regenLineRenderer.enabled = false;
        InitStringColliders();
        InitGradientIfDefault();
        if (stringRenderer != null) stringRenderer.colorGradient = stringGradient;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        _currentStringHp = stringHp;
        _anchorWorldPos = transform.position + Vector3.up * pendulumLength;

        // 角度0（スポーン位置）から開始してブレを防ぐ。方向だけランダムに変える
        _phase = Random.value > 0.5f ? 0f : Mathf.PI;
        _currentAmplitude = Random.Range(swingAngleMin, swingAngleMax);
        _currentPeriod = Random.Range(periodMin, periodMax);
        _prevAngle = 0f;

        if (normalFrames != null && normalFrames.Length > 0)
            SetFrame(normalFrames[0]);

        SetStringVisible(true);
        StartCoroutine(FrameCycleLoop());

        if (regenLineRenderer != null && stringRenderer != null)
        {
            regenLineRenderer.useWorldSpace = true;
            regenLineRenderer.startWidth = stringRenderer.startWidth;
            regenLineRenderer.endWidth = stringRenderer.endWidth;
            regenLineRenderer.colorGradient = stringGradient;
            regenLineRenderer.sortingLayerID = stringRenderer.sortingLayerID;
            regenLineRenderer.sortingOrder = stringRenderer.sortingOrder;
            regenLineRenderer.sharedMaterial = stringRenderer.sharedMaterial;
            regenLineRenderer.positionCount = 0;
        }
    }

    // ===========================
    // Pendulum
    // ===========================

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (_isStringCut || _isRegenTracing) return;
        UpdatePendulum();
    }

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void UpdatePendulum()
    {
        if (_isPaused)
        {
            _pauseTimer -= Time.deltaTime * GetTimeScale();
            if (_pauseTimer <= 0f) _isPaused = false;
            return;
        }

        float omega = 2f * Mathf.PI / Mathf.Max(0.1f, _currentPeriod);
        _phase += omega * Time.deltaTime * GetTimeScale();

        float newAngle = _currentAmplitude * Mathf.Sin(_phase);

        // Center crossing: sign change
        bool crossedCenter = (_prevAngle < 0f && newAngle >= 0f) || (_prevAngle > 0f && newAngle <= 0f);
        if (crossedCenter)
        {
            TryFireAtCenter();

            // Randomize next half-cycle
            _currentAmplitude = Random.Range(swingAngleMin, swingAngleMax);
            _currentPeriod = Random.Range(periodMin, periodMax);

            // Maybe pause at center
            if (Random.value < pauseChance)
            {
                _isPaused = true;
                _pauseTimer = Random.Range(pauseDurationMin, pauseDurationMax);
                newAngle = 0f;
                _phase = Mathf.Round(_phase / Mathf.PI) * Mathf.PI;
            }
        }

        _prevAngle = newAngle;
        ApplyPendulumTransform(newAngle);
    }

    private void TryFireAtCenter()
    {
        if (_enemyShooter == null || !_enemyShooter.enabled) return;
        if (Time.time - _lastFireTime < fireCooldown) return;
        _lastFireTime = Time.time;
        _enemyShooter.FireOnce();
    }

    private void ApplyPendulumTransform(float angleDeg)
    {
        float angleRad = angleDeg * Mathf.Deg2Rad;
        Vector3 pos = _anchorWorldPos + new Vector3(
            Mathf.Sin(angleRad) * pendulumLength,
            -Mathf.Cos(angleRad) * pendulumLength,
            0f
        );
        Vector3 shake = spriteShake != null ? spriteShake.CurrentOffset : Vector3.zero;
        transform.position = pos + shake;
        transform.rotation = Quaternion.Euler(0f, 0f, -angleDeg * 0.4f);
    }

    // ===========================
    // Frames
    // ===========================

    private IEnumerator FrameCycleLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(normalFrameInterval);
            if (_isStringCut || _isRegenTracing || _attackCo != null) continue;
            if (normalFrames == null || normalFrames.Length == 0) continue;
            _currentNormalFrameIdx = (_currentNormalFrameIdx + 1) % normalFrames.Length;
            SetFrame(normalFrames[_currentNormalFrameIdx]);
        }
    }

    private void OnShooterFired()
    {
        if (_attackCo != null) StopCoroutine(_attackCo);
        _attackCo = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        if (attackFrame != null) SetFrame(attackFrame);
        yield return new WaitForSeconds(attackFrameDuration);
        if (normalFrames != null && normalFrames.Length > 0)
            SetFrame(normalFrames[_currentNormalFrameIdx]);
        _attackCo = null;
    }

    private void SetFrame(HeadFrame frame)
    {
        if (frame == null || spriteRenderer == null) return;
        if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
        _currentFrameOffset = new Vector2(frame.offsetX, frame.offsetY);
        if (spriteHolder != null)
            spriteHolder.localPosition = new Vector3(frame.offsetX, frame.offsetY, 0f);
    }

    private Vector2 CurrentAttachOffset =>
        (_attackCo != null && attackFrame != null)
            ? attackFrame.stringOffset
            : (normalFrames != null && normalFrames.Length > 0)
                ? normalFrames[_currentNormalFrameIdx].stringOffset
                : stringAttachOffset;

    // ===========================
    // String System
    // ===========================

    public void OnStringHit(EnemyBullet bullet, Rigidbody2D bulletRb, Vector2 segmentNormal, Vector3 hitPos, float gradientT)
    {
        if (!Application.isPlaying || _isStringCut) return;
        float now = Time.unscaledTime;
        if (now - _lastStringHitTime < StringHitCooldown) return;
        _lastStringHitTime = now;
        _lastStringHitPos = hitPos;

        if (bulletRb != null)
            bulletRb.linearVelocity = Vector2.Reflect(bulletRb.linearVelocity, segmentNormal);

        bool isPowered = bullet != null && bullet.DamageMultiplier > 1.0001f;
        TryPlayStringHitSe(isPowered);
        if (spriteShake != null) spriteShake.TriggerShake(isPowered);

        int dmg = bullet == null ? 1
            : isPowered ? Mathf.Max(1, Mathf.RoundToInt(bullet.BlockJustDamage))
                        : Mathf.Max(1, Mathf.RoundToInt(bullet.BlockNormalDamage));
        _currentStringHp -= dmg;
        if (_currentStringHp <= 0) CutString();
    }

    private void CutString()
    {
        if (!Application.isPlaying || _isStringCut) return;
        _frozenPos = transform.position;
        _frozenStringOffset = CurrentAttachOffset;
        _isStringCut = true;
        _isRegenTracing = false;
        if (_regenTraceCo != null) { StopCoroutine(_regenTraceCo); _regenTraceCo = null; }
        SpawnCutParticle();
        SetStringVisible(false);
        if (regenLineRenderer != null) regenLineRenderer.enabled = false;
        if (_regenCo != null) StopCoroutine(_regenCo);
        _regenCo = StartCoroutine(StringRegenRoutine());
    }

    private void SetStringVisible(bool visible)
    {
        if (stringRenderer != null) stringRenderer.enabled = visible;
        if (outlineRenderer != null) outlineRenderer.enabled = visible;
        if (_segmentColliders != null)
            foreach (var col in _segmentColliders)
                if (col != null) col.enabled = visible;
        if (_enemyShooter != null) _enemyShooter.enabled = visible;
    }

    private IEnumerator StringRegenRoutine()
    {
        yield return new WaitForSeconds(stringRegenDelay);
        _currentStringHp = stringHp;
        _isStringCut = false;
        _isRegenTracing = true;

        if (regenParticle != null)
        {
            var main = regenParticle.main;
            main.startColor = new ParticleSystem.MinMaxGradient(stringGradient);
            regenParticle.Play();
        }

        if (regenLineRenderer != null)
        {
            _regenTraceCo = StartCoroutine(RegenLineTrace());
            yield return _regenTraceCo;
            _regenTraceCo = null;
        }

        if (_isStringCut) yield break;
        _isRegenTracing = false;
        if (regenLineRenderer != null) regenLineRenderer.enabled = false;
        SetStringVisible(true);
        _regenCo = null;
    }

    private IEnumerator RegenLineTrace()
    {
        if (regenLineRenderer == null) yield break;
        const int segs = 120;
        regenLineRenderer.enabled = true;
        if (_segmentColliders != null)
            foreach (var col in _segmentColliders)
                if (col != null) col.enabled = true;

        float elapsed = 0f;
        while (elapsed < regenTraceTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / regenTraceTime);
            int activeSegs = Mathf.Max(2, Mathf.RoundToInt(progress * segs));
            regenLineRenderer.positionCount = activeSegs;

            Vector3 startPt = _anchorWorldPos;
            Vector2 att = _frozenStringOffset;
            Vector3 endPt = transform.position + transform.rotation * new Vector3(att.x, att.y, 0f);

            for (int i = 0; i < activeSegs; i++)
            {
                float t = (float)i / (segs - 1);
                Vector3 p = Vector3.Lerp(startPt, endPt, t);
                p.y -= stringDrop * 4f * t * (1f - t);
                regenLineRenderer.SetPosition(i, p);
            }
            UpdateStringColliders(regenLineRenderer);
            yield return null;
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            DrawStringPreview();
            return;
        }

        if (_isStringCut && !_isRegenTracing)
        {
            Vector3 shake = spriteShake != null ? spriteShake.CurrentOffset : Vector3.zero;
            transform.position = _frozenPos + shake;
            return;
        }
        if (_isRegenTracing) return;
        if (stringRenderer == null) return;

        int segs = Mathf.Max(2, stringSegments);
        stringRenderer.positionCount = segs;
        if (outlineRenderer != null)
        {
            outlineRenderer.positionCount = segs;
            outlineRenderer.startWidth = stringRenderer.startWidth * outlineWidthMultiplier;
            outlineRenderer.endWidth = stringRenderer.endWidth * outlineWidthMultiplier;
        }

        Vector3 start = _anchorWorldPos;
        Vector2 totalAtt = CurrentAttachOffset;
        Vector3 end = transform.position + transform.rotation * new Vector3(totalAtt.x, totalAtt.y, 0f);

        for (int i = 0; i < segs; i++)
        {
            float t = (float)i / (segs - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            p.y -= stringDrop * 4f * t * (1f - t);
            stringRenderer.SetPosition(i, p);
            if (outlineRenderer != null) outlineRenderer.SetPosition(i, p);
        }
        UpdateStringColliders();
    }

    private void DrawStringPreview()
    {
        if (stringRenderer == null) return;
        int segs = Mathf.Max(2, stringSegments);
        stringRenderer.positionCount = segs;
        if (outlineRenderer != null)
        {
            outlineRenderer.positionCount = segs;
            outlineRenderer.startWidth = stringRenderer.startWidth * outlineWidthMultiplier;
            outlineRenderer.endWidth = stringRenderer.endWidth * outlineWidthMultiplier;
        }
        Vector3 anchor = transform.position + Vector3.up * pendulumLength;
        Vector2 att = normalFrames != null && normalFrames.Length > 0
            ? normalFrames[0].stringOffset
            : stringAttachOffset;
        Vector3 end = transform.position + transform.rotation * new Vector3(att.x, att.y, 0f);
        for (int i = 0; i < segs; i++)
        {
            float t = (float)i / (segs - 1);
            Vector3 p = Vector3.Lerp(anchor, end, t);
            p.y -= stringDrop * 4f * t * (1f - t);
            stringRenderer.SetPosition(i, p);
            if (outlineRenderer != null) outlineRenderer.SetPosition(i, p);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var bullet = collision.collider.GetComponent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;
        var rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.up;
            rb.linearVelocity = Vector2.Reflect(rb.linearVelocity, normal);
        }
    }

    // ===========================
    // Colliders
    // ===========================

    private void InitStringColliders()
    {
        if (!Application.isPlaying) return;
        _segmentColliders = new CapsuleCollider2D[stringColliderCount];
        _segmentRigidbodies = new Rigidbody2D[stringColliderCount];
        for (int i = 0; i < stringColliderCount; i++)
        {
            var go = new GameObject($"StringSeg_{i}");
            go.transform.SetParent(transform);
            go.layer = gameObject.layer;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = go.AddComponent<CapsuleCollider2D>();
            col.isTrigger = true;
            col.direction = CapsuleDirection2D.Horizontal;
            var seg = go.AddComponent<PuppetStringSegmentTrigger>();
            seg.owner = this;
            seg.segmentIndex = i;
            seg.totalSegments = stringColliderCount;
            _segmentColliders[i] = col;
            _segmentRigidbodies[i] = rb;
        }
    }

    private void UpdateStringColliders(LineRenderer source = null)
    {
        if (_segmentColliders == null) return;
        LineRenderer lr = source != null ? source : stringRenderer;
        if (lr == null) return;
        int segs = lr.positionCount;
        if (segs < 2) return;
        int colCount = _segmentColliders.Length;
        for (int i = 0; i < colCount; i++)
        {
            int idx0 = Mathf.Clamp(Mathf.RoundToInt((float)i / colCount * (segs - 1)), 0, segs - 1);
            int idx1 = Mathf.Clamp(Mathf.RoundToInt((float)(i + 1) / colCount * (segs - 1)), 0, segs - 1);
            Vector3 p0 = lr.GetPosition(idx0);
            Vector3 p1 = lr.GetPosition(idx1);
            Vector3 mid = (p0 + p1) * 0.5f;
            float len = Vector2.Distance(p0, p1);
            float angle = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
            var col = _segmentColliders[i];
            col.size = new Vector2(len, stringColliderRadius * 2f);
            col.offset = Vector2.zero;
            var rb = _segmentRigidbodies[i];
            rb.MovePosition(mid);
            rb.MoveRotation(angle);
        }
    }

    private void SpawnCutParticle()
    {
        if (cutParticlePrefab == null) return;
        var ps = Instantiate(cutParticlePrefab, _lastStringHitPos, Quaternion.identity);
        var main = ps.main;
        var grad = new ParticleSystem.MinMaxGradient(stringGradient);
        grad.mode = ParticleSystemGradientMode.RandomColor;
        main.startColor = grad;
        ps.Play();
        Destroy(ps.gameObject, 5f);
    }

    // ===========================
    // SE
    // ===========================

    private void TryPlayStringHitSe(bool isPowered)
    {
        if (audioSource == null) return;
        PlayRandomClip(isPowered ? stringJustHitClips : stringNormalHitClips, stringHitSeVolume);
    }

    private void PlayRandomClip(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        int valid = 0;
        foreach (var c in clips) if (c != null) valid++;
        if (valid == 0) return;
        int pick = Random.Range(0, valid);
        foreach (var c in clips)
        {
            if (c == null) continue;
            if (pick-- == 0) { audioSource.PlayOneShot(c, volume); return; }
        }
    }

    // ===========================
    // Gradient
    // ===========================

    private void InitGradientIfDefault()
    {
        if (stringGradient == null) stringGradient = new Gradient();
        var keys = stringGradient.colorKeys;
        bool isDefault = keys.Length == 2 && keys[0].color == Color.white && keys[1].color == Color.white;
        if (!isDefault) return;
        stringGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.800f, 0.267f, 1.000f), 0.00f),
                new GradientColorKey(new Color(0.467f, 0.333f, 0.933f), 0.50f),
                new GradientColorKey(new Color(0.267f, 1.000f, 0.800f), 1.00f),
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
    }

    // ===========================
    // Editor
    // ===========================

#if UNITY_EDITOR
    [ContextMenu("Setup String Renderers")]
    private void SetupStringRenderers()
    {
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_BossString.mat");

        // 各LineRendererは子GOに1つずつ配置（DisallowMultipleComponent対策）
        stringRenderer    = AcquireChildLR("StringRenderer",    stringRenderer);
        outlineRenderer   = AcquireChildLR("OutlineRenderer",   outlineRenderer);
        regenLineRenderer = AcquireChildLR("RegenLineRenderer", regenLineRenderer);

        ApplyLR(stringRenderer,    mat, 0.05f,                          15);
        ApplyLR(outlineRenderer,   mat, 0.05f * outlineWidthMultiplier, 14);
        ApplyLR(regenLineRenderer, mat, 0.05f,                          15);

        if (regenLineRenderer != null)
        {
            regenLineRenderer.positionCount = 0;
            regenLineRenderer.enabled = false;
        }

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log($"[PuppetHead] Setup done. mat={mat?.name ?? "NOT FOUND"}" +
                  $"  string={stringRenderer != null}  outline={outlineRenderer != null}  regen={regenLineRenderer != null}");
    }

    [ContextMenu("Setup Sprite Holder")]
    private void SetupSpriteHolder()
    {
        // SpriteHolder子GOを作成（なければ）
        var t = transform.Find("SpriteHolder");
        GameObject holderGo;
        if (t == null)
        {
            holderGo = new GameObject("SpriteHolder");
            holderGo.transform.SetParent(transform, false);
        }
        else
        {
            holderGo = t.gameObject;
        }
        spriteHolder = holderGo.transform;

        // ルートのSpriteRendererを子に移動
        var rootSR = GetComponent<SpriteRenderer>();
        var childSR = holderGo.GetComponent<SpriteRenderer>();
        if (childSR == null) childSR = holderGo.AddComponent<SpriteRenderer>();

        if (rootSR != null)
        {
            childSR.sprite          = rootSR.sprite;
            childSR.sortingLayerID  = rootSR.sortingLayerID;
            childSR.sortingOrder    = rootSR.sortingOrder;
            childSR.color           = rootSR.color;
            childSR.flipX           = rootSR.flipX;
            childSR.flipY           = rootSR.flipY;
            DestroyImmediate(rootSR);
        }
        spriteRenderer = childSR;

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log("[PuppetHead] Setup Sprite Holder done.");
    }

    private LineRenderer AcquireChildLR(string childName, LineRenderer existing)
    {
        // 既存が正しい子GOにある場合のみ再利用（ルートのLRはスキップ）
        if (existing != null
            && existing.transform.parent == transform
            && existing.gameObject.name == childName)
            return existing;

        var t = transform.Find(childName);
        if (t == null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            t = go.transform;
        }
        var lr = t.GetComponent<LineRenderer>();
        if (lr == null) lr = t.gameObject.AddComponent<LineRenderer>();
        return lr;
    }

    private static void ApplyLR(LineRenderer lr, Material mat, float width, int sortOrder)
    {
        if (lr == null) return;
        lr.useWorldSpace = true;
        lr.startWidth = width;
        lr.endWidth = width;
        if (mat != null) lr.sharedMaterial = mat;
        lr.sortingLayerID = 0;
        lr.sortingOrder = sortOrder;
        lr.positionCount = 2;
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (stringRenderer != null) stringRenderer.colorGradient = stringGradient;

        HeadFrame frame = previewMode switch
        {
            PuppetPreviewMode.Normal0 => (normalFrames != null && normalFrames.Length > 0) ? normalFrames[0] : null,
            PuppetPreviewMode.Normal1 => (normalFrames != null && normalFrames.Length > 1) ? normalFrames[1] : null,
            PuppetPreviewMode.Attack  => attackFrame,
            _                         => null,
        };

        Vector2 offset = frame != null
            ? new Vector2(frame.offsetX, frame.offsetY)
            : (normalFrames != null && normalFrames.Length > 0
                ? new Vector2(normalFrames[0].offsetX, normalFrames[0].offsetY)
                : Vector2.zero);
        _currentFrameOffset = offset;

        if (spriteHolder != null)
            spriteHolder.localPosition = new Vector3(offset.x, offset.y, 0f);

        if (frame?.sprite != null && spriteRenderer != null)
            spriteRenderer.sprite = frame.sprite;

        UnityEditor.SceneView.RepaintAll();
    }

    private void OnDrawGizmos()
    {
        Vector3 anchor = transform.position + Vector3.up * pendulumLength;

        UnityEditor.Handles.color = new Color(0.267f, 1f, 0.8f);
        UnityEditor.Handles.DrawSolidDisc(anchor, Vector3.forward, 0.08f);
        UnityEditor.Handles.DrawDottedLine(anchor, transform.position, 4f);
        UnityEditor.Handles.Label(anchor + Vector3.up * 0.12f, "anchor");

        HeadFrame frame = previewMode switch
        {
            PuppetPreviewMode.Normal0 => (normalFrames != null && normalFrames.Length > 0) ? normalFrames[0] : null,
            PuppetPreviewMode.Normal1 => (normalFrames != null && normalFrames.Length > 1) ? normalFrames[1] : null,
            PuppetPreviewMode.Attack  => attackFrame,
            _                         => normalFrames != null && normalFrames.Length > 0 ? normalFrames[0] : null,
        };
        Vector2 att = frame != null ? frame.stringOffset : stringAttachOffset;
        Vector3 attachPos = transform.position + new Vector3(att.x, att.y, 0f);
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawSolidDisc(attachPos, Vector3.forward, 0.05f);
        UnityEditor.Handles.Label(attachPos + Vector3.up * 0.1f, "attach");

    }
#endif

    private void OnDestroy()
    {
        if (_enemyShooter != null)
            _enemyShooter.OnFired -= OnShooterFired;
    }
}
