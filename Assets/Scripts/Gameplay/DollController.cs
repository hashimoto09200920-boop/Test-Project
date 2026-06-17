using System.Collections;
using UnityEngine;

public enum EditorPreviewMode { None, Hang, Attack }

[ExecuteAlways]
public class DollController : MonoBehaviour
{
    [System.Serializable]
    public class DollFrame
    {
        public Sprite sprite;
        public float offsetX = 0f;
        public float offsetY = 0f;
        [Tooltip("このフレーム固有の糸接続点オフセット（stringAttachOffsetへの加算）")]
        public Vector2 stringOffset = Vector2.zero;
        public float durationMin = 0.07f;
        public float durationMax = 0.13f;
    }

    [Header("Hang Pose")]
    [SerializeField] private DollFrame hangFrame;

    [Header("Attack Frames (JerkA or JerkA_Flip)")]
    [NonReorderable]
    [SerializeField] private DollFrame[] attackFrames;

    [Header("Attack Timing")]
    [Tooltip("通常時の攻撃間隔・最小（秒）")]
    [SerializeField] private float attackIntervalMin = 3f;
    [Tooltip("通常時の攻撃間隔・最大（秒）")]
    [SerializeField] private float attackIntervalMax = 5f;

    [Header("Sway Settings")]
    [Tooltip("右への揺れ幅（ワールド単位）")]
    [SerializeField] private float swayAmplitudeRight = 0.05f;
    [Tooltip("左への揺れ幅（ワールド単位）")]
    [SerializeField] private float swayAmplitudeLeft  = 0.05f;
    [Tooltip("上への揺れ幅（ワールド単位）")]
    [SerializeField] private float swayAmplitudeUp    = 0.03f;
    [Tooltip("下への揺れ幅（ワールド単位）")]
    [SerializeField] private float swayAmplitudeDown  = 0.03f;
    [Tooltip("左右の揺れ速度・最小値（rad/s）")]
    [SerializeField] private float swaySpeedXMin = 0.9f;
    [Tooltip("左右の揺れ速度・最大値（rad/s）")]
    [SerializeField] private float swaySpeedXMax = 1.5f;
    [Tooltip("上下の揺れ速度・最小値（rad/s）")]
    [SerializeField] private float swaySpeedYMin = 0.5f;
    [Tooltip("上下の揺れ速度・最大値（rad/s）")]
    [SerializeField] private float swaySpeedYMax = 0.9f;
    [Tooltip("傾き（RotationZ）の最小振れ幅（度）")]
    [SerializeField] private float rotationAmplitudeMin = 3f;
    [Tooltip("傾き（RotationZ）の最大振れ幅（度）")]
    [SerializeField] private float rotationAmplitudeMax = 8f;
    [Tooltip("傾き速度の最小値（rad/s）")]
    [SerializeField] private float rotationSpeedMin = 0.4f;
    [Tooltip("傾き速度の最大値（rad/s）")]
    [SerializeField] private float rotationSpeedMax = 1.0f;

    [Header("String (LineRenderer)")]
    [Tooltip("LineRendererコンポーネント（同GameObject or 子に追加）")]
    [SerializeField] private LineRenderer stringRenderer;
    [Tooltip("ボス側の糸接続点（指先の空GameObject）")]
    [SerializeField] private Transform stringOrigin;
    [Tooltip("人形側の接続点オフセット（手の位置・ローカル基準）")]
    [SerializeField] private Vector2 stringAttachOffset = new Vector2(0f, 0.5f);
    [Tooltip("カテナリーの分割数（多いほど滑らか）")]
    [SerializeField] private int stringSegments = 12;
    [Tooltip("重力による垂れ量（ワールド単位）")]
    [SerializeField] private float stringDrop = 0.3f;
    [Tooltip("Dollの揺れが糸の中間点に与える影響倍率")]
    [Range(0f, 1f)]
    [SerializeField] private float stringSwayInfluence = 0.6f;
    [Tooltip("アウトライン用LineRenderer（StringRendererより低いOrder in Layerに設定）")]
    [SerializeField] private LineRenderer outlineRenderer;
    [Tooltip("アウトラインの太さ（stringRendererのstart/endWidthに対する倍率）")]
    [Range(1.1f, 3f)]
    [SerializeField] private float outlineWidthMultiplier = 1.4f;

    [Header("String Color")]
    [Tooltip("指先側→手首側のグラデーション（左=指先、右=手首）")]
    [SerializeField] private Gradient stringGradient;

    [Header("String Colliders")]
    [Tooltip("糸を分割するCapsuleCollider2Dの数")]
    [SerializeField] private int stringColliderCount = 6;
    [Tooltip("CapsuleCollider2Dの半径（ワールド単位）")]
    [SerializeField] private float stringColliderRadius = 0.06f;

    [Header("String Hit")]
    [Tooltip("糸の耐久HP（この回数だけ反射弾が当たると切断）")]
    [SerializeField] private int stringHp = 3;
    [Tooltip("通常ヒット時SE（ランダム選択・最大3種）")]
    [SerializeField] private AudioClip[] stringNormalHitClips;
    [Tooltip("Just弾ヒット時SE（ランダム選択・最大3種）")]
    [SerializeField] private AudioClip[] stringJustHitClips;
    [Range(0f, 1f)]
    [SerializeField] private float stringHitSeVolume = 1f;

    [Header("String Regen")]
    [Tooltip("切断後に糸が再生成されるまでの秒数")]
    [SerializeField] private float stringRegenDelay = 3f;
    [Tooltip("再生パーティクルが指先→人形までトレースする時間（秒）。小さいほど速い")]
    [SerializeField] private float regenTraceTime = 1.0f;
    [Tooltip("再生成開始時に指先で再生するパーティクル（未設定なら省略）")]
    [SerializeField] private ParticleSystem regenParticle;
    [Tooltip("糸再生トレース用LineRenderer（Startで設定を自動コピー）")]
    [SerializeField] private LineRenderer regenLineRenderer;

    [Header("String Cut VFX")]
    [Tooltip("糸切断時に切断点で再生するバーストパーティクル（Prefab）")]
    [SerializeField] private ParticleSystem cutParticlePrefab;

    [Header("Hit SE")]
    [Tooltip("通常ヒットSE（ランダム選択）")]
    [SerializeField] private AudioClip[] normalHitClips;
    [Tooltip("Just（強化）ヒットSE（ランダム選択）")]
    [SerializeField] private AudioClip[] justHitClips;
    [Range(0f, 1f)]
    [SerializeField] private float hitSeVolume = 1f;
    [SerializeField] private float hitSeMinInterval = 0.06f;

    [Header("Move Bounds")]
    [Tooltip("初期位置を中心とした移動可能範囲（ワールド単位）")]
    [SerializeField] private Vector2 moveBoundsSize = new Vector2(2f, 2f);
    [Tooltip("境界到達時に内側へ戻す距離")]
    [SerializeField] private float bounceDistance = 0.3f;

    [Header("Settings")]
    [SerializeField] private float fallbackFps = 14f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("弾ヒット時のシェイク参照")]
    [SerializeField] private EnemySpriteShake spriteShake;

    [Header("Editor Preview")]
    [SerializeField] private EditorPreviewMode previewMode = EditorPreviewMode.None;
    [Tooltip("Attackプレビュー時に表示するattackFramesのインデックス")]
    [SerializeField] private int previewAttackIndex = 0;

    [HideInInspector] [SerializeField] private Vector3 editorBasePos;
    [HideInInspector] [SerializeField] private bool editorBaseCaptured = false;

    private Coroutine _attackCo;
    private Vector3 _basePos;
    private Vector3 _initialBasePos;
    private Vector3 _swayOffset;
    private Vector2 _frameOffset;
    private Vector2 _frameStringOffset;
    private float _currentSwaySpeedX;
    private float _currentSwaySpeedY;
    private float _rotationZ;
    private float _currentRotSpeed;
    private float _currentRotAmplitude;
    private float _tx, _ty, _tRot;
    private Vector3 _frozenSwayOffset;
    private float _frozenRotationZ;

    private float _lastHitSeTime = -999f;

    private CapsuleCollider2D[] _segmentColliders;
    private Rigidbody2D[] _segmentRigidbodies;
    private bool _isStringCut = false;
    private bool _isRegenTracing = false;
    private Coroutine _regenCo;
    private Coroutine _regenTraceCo;
    private int _currentStringHp;
    private float _lastStringHitTime = -999f;
    private const float StringHitCooldown = 0.1f;
    private EnemyShooter _enemyShooter;
    private BossHandController _bossHand;
    private Vector3 _lastStringHitPos;
    private float _lastStringHitT = 0.5f;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteShake == null)
            spriteShake = GetComponent<EnemySpriteShake>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.useFullKinematicContacts = true;
        if (spriteShake != null) spriteShake.externalPositioning = true;
        var mover = GetComponent<EnemyMover>();
        if (mover != null) mover.suppressMovement = true;
        _enemyShooter = GetComponent<EnemyShooter>();
        var bossStats = GetComponentInParent<EnemyStats>();
        if (_enemyShooter != null && bossStats != null)
            _enemyShooter.SetEnemyStats(bossStats);
        if (_enemyShooter != null)
            _enemyShooter.OnFired += PlayAttack;
        if (bossStats != null)
            _bossHand = bossStats.GetComponentInChildren<BossHandController>();
        if (regenLineRenderer != null)
            regenLineRenderer.enabled = false;
        InitStringColliders();
        InitGradientIfDefault();
        if (stringRenderer != null)
        {
            stringRenderer.colorGradient = stringGradient;
            if (outlineRenderer != null)
            {
                outlineRenderer.useWorldSpace  = stringRenderer.useWorldSpace;
                outlineRenderer.sortingLayerID = stringRenderer.sortingLayerID;
            }
        }
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        _basePos = transform.position;
        _initialBasePos = transform.position;
        _currentStringHp = stringHp;
        SetFrame(hangFrame);
        StartCoroutine(SwayLoop());

        if (stringRenderer != null)
            stringRenderer.positionCount = 2;
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

    // ==============================
    // Curse System
    // ==============================

    /// <summary>StringSegmentTriggerから糸に反射弾が当たったときに呼ぶ</summary>
    public void OnStringHit(EnemyBullet bullet, Rigidbody2D bulletRb, Vector2 segmentNormal, Vector3 hitPos, float gradientT)
    {
        if (!Application.isPlaying) return;
        if (_isStringCut) return;

        float now = Time.unscaledTime;
        if (now - _lastStringHitTime < StringHitCooldown) return;
        _lastStringHitTime = now;

        _lastStringHitPos = hitPos;
        _lastStringHitT = gradientT;

        // 弾を反射
        if (bulletRb != null)
            bulletRb.linearVelocity = Vector2.Reflect(bulletRb.linearVelocity, segmentNormal);

        // SE + Shake
        bool isPowered = bullet != null && bullet.DamageMultiplier > 1.0001f;
        TryPlayStringHitSe(isPowered);
        if (spriteShake != null) spriteShake.TriggerShake(isPowered);

        // Curse蓄積（反射弾ヒットごと）
        _bossHand?.AddCurse();

        // HP減少 → 0以下で切断
        int stringDmg = (bullet == null) ? 1
            : isPowered
                ? Mathf.Max(1, Mathf.RoundToInt(bullet.BlockJustDamage))
                : Mathf.Max(1, Mathf.RoundToInt(bullet.BlockNormalDamage));
        _currentStringHp -= stringDmg;
        if (_currentStringHp <= 0)
            OnStringCut();
    }

    private void TryPlayStringHitSe(bool isPowered)
    {
        if (audioSource == null) return;
        AudioClip[] clips = isPowered ? stringJustHitClips : stringNormalHitClips;
        if (clips == null || clips.Length == 0) return;
        int valid = 0;
        foreach (var c in clips) if (c != null) valid++;
        if (valid == 0) return;
        int pick = Random.Range(0, valid);
        foreach (var c in clips)
        {
            if (c == null) continue;
            if (pick-- == 0) { audioSource.PlayOneShot(c, stringHitSeVolume); break; }
        }
    }

    public void ApplyEnhancementTint(Color rgb)
    {
        if (spriteRenderer == null) return;
        Color c = spriteRenderer.color;
        spriteRenderer.color = new Color(rgb.r, rgb.g, rgb.b, c.a);
    }

    public void OnStringCut()
    {
        if (!Application.isPlaying) return;
        if (_bossHand != null && _bossHand.IsEnhanced) return;
        if (_isStringCut) return;

        _frozenSwayOffset = _swayOffset;
        _frozenRotationZ  = _rotationZ;
        _isStringCut = true;
        _isRegenTracing = false;

        if (_regenTraceCo != null) { StopCoroutine(_regenTraceCo); _regenTraceCo = null; }

        SpawnCutParticle();
        SetStringVisible(false);
        if (regenLineRenderer != null) regenLineRenderer.enabled = false;

        if (_regenCo != null) StopCoroutine(_regenCo);
        _regenCo = StartCoroutine(StringRegenRoutine());
    }

    private void SpawnCutParticle()
    {
        if (cutParticlePrefab == null) { Debug.LogWarning("[DollController] cutParticlePrefab is null"); return; }
        var ps = Instantiate(cutParticlePrefab, _lastStringHitPos, Quaternion.identity);
        var main = ps.main;
        var grad = new ParticleSystem.MinMaxGradient(stringGradient);
        grad.mode = ParticleSystemGradientMode.RandomColor;
        main.startColor = grad;
        ps.Play();
        Destroy(ps.gameObject, 5f);
        Debug.Log($"[DollController] CutParticle spawned at {_lastStringHitPos}");
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

        // トレース中: 当たり判定ON、ドール位置は凍結(_isRegenTracing)
        _currentStringHp = stringHp;
        _isStringCut = false;
        _isRegenTracing = true;

        if (regenLineRenderer != null)
        {
            _regenTraceCo = StartCoroutine(RegenLineTrace());
            yield return _regenTraceCo;
            _regenTraceCo = null;
        }

        if (_isStringCut) yield break;

        _isRegenTracing = false;
        _basePos += _frozenSwayOffset;

        Vector3 prevBasePos = _basePos;
        bool bounced = false;
        if (moveBoundsSize.sqrMagnitude > 0.0001f)
        {
            Vector2 half = moveBoundsSize * 0.5f;
            if (_basePos.x < _initialBasePos.x - half.x)      { _basePos.x = _initialBasePos.x - half.x + bounceDistance; bounced = true; }
            else if (_basePos.x > _initialBasePos.x + half.x) { _basePos.x = _initialBasePos.x + half.x - bounceDistance; bounced = true; }
            if (_basePos.y < _initialBasePos.y - half.y)      { _basePos.y = _initialBasePos.y - half.y + bounceDistance; bounced = true; }
            else if (_basePos.y > _initialBasePos.y + half.y) { _basePos.y = _initialBasePos.y + half.y - bounceDistance; bounced = true; }
        }

        _frozenSwayOffset = Vector3.zero;
        _frozenRotationZ  = 0f;
        _tx = 0f; _ty = 0f; _tRot = 0f;

        if (bounced)
        {
            Vector3 diff = prevBasePos - _basePos;
            float ampX = Mathf.Max(swayAmplitudeRight, swayAmplitudeLeft, 0.001f);
            float ampY = Mathf.Max(swayAmplitudeUp, swayAmplitudeDown, 0.001f);
            _tx = Mathf.Asin(Mathf.Clamp(diff.x / ampX, -1f, 1f));
            _ty = Mathf.Asin(Mathf.Clamp(diff.y / ampY, -1f, 1f));
        }

        if (regenLineRenderer != null) regenLineRenderer.enabled = false;
        SetStringVisible(true);
        _regenCo = null;
    }

    private IEnumerator RegenLineTrace()
    {
        if (regenLineRenderer == null || stringOrigin == null) yield break;

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

            Vector3 startPt = stringOrigin.position;
            Vector2 att = stringAttachOffset + _frameStringOffset;
            Vector3 endPt = transform.position + transform.rotation * new Vector3(att.x, att.y, 0f);

            for (int i = 0; i < activeSegs; i++)
            {
                float t = (float)i / (segs - 1);
                Vector3 p = Vector3.Lerp(startPt, endPt, t);
                p.y -= stringDrop * 4f * t * (1f - t);
                p += _swayOffset * (stringSwayInfluence * Mathf.Sin(Mathf.PI * t));
                regenLineRenderer.SetPosition(i, p);
            }

            UpdateStringColliders(regenLineRenderer);

            yield return null;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var bullet = collision.collider.GetComponent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;

        bool isPowered = bullet.DamageMultiplier > 1.0001f;

        if (spriteShake != null)
            spriteShake.TriggerShake(isPowered);

        // SE
        TryPlayHitSe(isPowered);

        // 弾を反射（消滅させずに速度を反転）
        var rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 normal = collision.contactCount > 0
                ? collision.GetContact(0).normal
                : Vector2.up;
            rb.linearVelocity = Vector2.Reflect(rb.linearVelocity, normal);
        }
    }

    private void TryPlayHitSe(bool isPowered)
    {
        if (audioSource == null) return;
        float now = Time.unscaledTime;
        if (now - _lastHitSeTime < hitSeMinInterval) return;

        AudioClip[] clips = isPowered ? justHitClips : normalHitClips;
        if (clips == null || clips.Length == 0) return;

        // null除外してランダム選択
        int valid = 0;
        foreach (var c in clips) if (c != null) valid++;
        if (valid == 0) return;

        int pick = Random.Range(0, valid);
        foreach (var c in clips)
        {
            if (c == null) continue;
            if (pick-- == 0) { audioSource.PlayOneShot(c, hitSeVolume); break; }
        }
        _lastHitSeTime = now;
    }

    // ==============================
    // Attack
    // ==============================

    public void PlayAttack()
    {
        if (attackFrames == null || attackFrames.Length == 0) return;
        if (_attackCo != null) StopCoroutine(_attackCo);
        _attackCo = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        float defaultInterval = 1f / Mathf.Max(1f, fallbackFps);
        var frame = attackFrames[Random.Range(0, attackFrames.Length)];
        SetFrame(frame);
        float wait = (frame.durationMax > 0f)
            ? Random.Range(frame.durationMin, frame.durationMax)
            : defaultInterval;
        yield return new WaitForSeconds(wait);
        SetFrame(hangFrame);
        _attackCo = null;
    }

    // ==============================
    // Sway
    // ==============================

    private IEnumerator SwayLoop()
    {
        _tx   = Random.Range(0f, Mathf.PI * 2f);
        _ty   = Random.Range(0f, Mathf.PI * 2f);
        _tRot = Random.Range(0f, Mathf.PI * 2f);
        _currentSwaySpeedX   = Random.Range(swaySpeedXMin, swaySpeedXMax);
        _currentSwaySpeedY   = Random.Range(swaySpeedYMin, swaySpeedYMax);
        _currentRotSpeed     = Random.Range(rotationSpeedMin, rotationSpeedMax);
        _currentRotAmplitude = Random.Range(rotationAmplitudeMin, rotationAmplitudeMax);
        while (true)
        {
            if (_isStringCut || _isRegenTracing)
            {
                _swayOffset = _frozenSwayOffset;
                _rotationZ  = _frozenRotationZ;
                UpdatePosition();
                yield return null;
                continue;
            }
            float sinX = Mathf.Sin(_tx);
            float sinY = Mathf.Sin(_ty);
            _swayOffset = new Vector3(
                sinX >= 0f ? sinX * swayAmplitudeRight : sinX * swayAmplitudeLeft,
                sinY >= 0f ? sinY * swayAmplitudeUp    : sinY * swayAmplitudeDown,
                0f);
            _rotationZ = Mathf.Sin(_tRot) * _currentRotAmplitude;
            UpdatePosition();
            _tx   += Time.deltaTime * _currentSwaySpeedX;
            _ty   += Time.deltaTime * _currentSwaySpeedY;
            _tRot += Time.deltaTime * _currentRotSpeed;
            if (_tx >= Mathf.PI * 2f)
            {
                _tx -= Mathf.PI * 2f;
                _currentSwaySpeedX = Random.Range(swaySpeedXMin, swaySpeedXMax);
            }
            if (_ty >= Mathf.PI * 2f)
            {
                _ty -= Mathf.PI * 2f;
                _currentSwaySpeedY = Random.Range(swaySpeedYMin, swaySpeedYMax);
            }
            if (_tRot >= Mathf.PI * 2f)
            {
                _tRot -= Mathf.PI * 2f;
                _currentRotSpeed     = Random.Range(rotationSpeedMin, rotationSpeedMax);
                _currentRotAmplitude = Random.Range(rotationAmplitudeMin, rotationAmplitudeMax);
            }
            yield return null;
        }
    }

    private void LateUpdate()
    {
        if (stringRenderer == null || stringOrigin == null) return;
        if (_isStringCut || _isRegenTracing) return;

        int segs = Mathf.Max(2, stringSegments);
        if (stringRenderer.positionCount != segs)
            stringRenderer.positionCount = segs;
        if (outlineRenderer != null)
        {
            if (outlineRenderer.positionCount != segs)
                outlineRenderer.positionCount = segs;
            outlineRenderer.startWidth = stringRenderer.startWidth * outlineWidthMultiplier;
            outlineRenderer.endWidth   = stringRenderer.endWidth   * outlineWidthMultiplier;
        }

        Vector3 start = stringOrigin.position;
        Vector2 totalAttach = stringAttachOffset + _frameStringOffset;
        Vector3 end   = transform.position + transform.rotation * new Vector3(totalAttach.x, totalAttach.y, 0f);

        for (int i = 0; i < segs; i++)
        {
            float t   = (float)i / (segs - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            p.y -= stringDrop * 4f * t * (1f - t);
            p += _swayOffset * (stringSwayInfluence * Mathf.Sin(Mathf.PI * t));
            stringRenderer.SetPosition(i, p);
            if (outlineRenderer != null) outlineRenderer.SetPosition(i, p);
        }

        UpdateStringColliders();
    }

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
            var seg = go.AddComponent<StringSegmentTrigger>();
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

    private void SetFrame(DollFrame frame)
    {
        if (frame == null || spriteRenderer == null) return;
        if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
        _frameOffset = new Vector2(frame.offsetX, frame.offsetY);
        _frameStringOffset = frame.stringOffset;
    }

    private void UpdatePosition()
    {
        Vector3 shake = spriteShake != null ? spriteShake.CurrentOffset : Vector3.zero;
        transform.position = _basePos + _swayOffset + new Vector3(_frameOffset.x, _frameOffset.y, 0f) + shake;
        transform.rotation = Quaternion.Euler(0f, 0f, _rotationZ);
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (previewMode == EditorPreviewMode.Hang && hangFrame != null)
        {
            if (!editorBaseCaptured) { editorBasePos = transform.position; editorBaseCaptured = true; }
            if (hangFrame.sprite != null) spriteRenderer.sprite = hangFrame.sprite;
            transform.position = editorBasePos + new Vector3(hangFrame.offsetX, hangFrame.offsetY, 0f);
            _frameStringOffset = hangFrame.stringOffset;
        }
        else if (previewMode == EditorPreviewMode.Attack && attackFrames != null && attackFrames.Length > 0)
        {
            if (!editorBaseCaptured) { editorBasePos = transform.position; editorBaseCaptured = true; }
            int idx = Mathf.Clamp(previewAttackIndex, 0, attackFrames.Length - 1);
            var frame = attackFrames[idx];
            if (frame != null)
            {
                if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
                transform.position = editorBasePos + new Vector3(frame.offsetX, frame.offsetY, 0f);
                _frameStringOffset = frame.stringOffset;
            }
        }
        else if (previewMode == EditorPreviewMode.None && editorBaseCaptured)
        {
            transform.position = editorBasePos;
            _frameStringOffset = Vector2.zero;
            editorBaseCaptured = false;
        }

        if (stringRenderer != null)
        {
            stringRenderer.colorGradient = stringGradient;
            if (outlineRenderer != null)
            {
                outlineRenderer.useWorldSpace  = stringRenderer.useWorldSpace;
                outlineRenderer.sortingLayerID = stringRenderer.sortingLayerID;
            }
        }

        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private void InitGradientIfDefault()
    {
        if (stringGradient == null) stringGradient = new Gradient();
        var keys = stringGradient.colorKeys;
        bool isDefault = keys.Length == 2
            && keys[0].color == Color.white
            && keys[1].color == Color.white;
        if (!isDefault) return;
        stringGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.800f, 0.267f, 1.000f), 0.00f),
                new GradientColorKey(new Color(0.467f, 0.333f, 0.933f), 0.50f),
                new GradientColorKey(new Color(0.267f, 1.000f, 0.800f), 1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        );
    }

#if UNITY_EDITOR
    [ContextMenu("Reset: Editor Preview State (複製後に実行)")]
    private void ResetEditorPreviewState()
    {
        editorBaseCaptured = false;
        previewMode = EditorPreviewMode.None;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[DollController] Editor preview state reset. 現在の position が基準位置として使われます。");
    }

    [ContextMenu("Reset String Gradient")]
    private void ResetStringGradient()
    {
        stringGradient = new Gradient();
        InitGradientIfDefault();
        if (stringRenderer != null)
            stringRenderer.colorGradient = stringGradient;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Test Cut Particle (Play中のみ)")]
    private void TestCutParticle()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Play中のみ実行可能"); return; }
        _lastStringHitPos = transform.position;
        SpawnCutParticle();
    }

    [ContextMenu("Test Cut Particle BIG (Play中のみ)")]
    private void TestCutParticleBig()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Play中のみ実行可能"); return; }
        if (cutParticlePrefab == null) { Debug.LogWarning("cutParticlePrefab is null"); return; }
        var ps = Instantiate(cutParticlePrefab, transform.position, Quaternion.identity);
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.magenta);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f);
        ps.Play();
        Destroy(ps.gameObject, 5f);
        Debug.Log("[DollController] BIG test particle spawned");
    }

    [ContextMenu("Test Regen Particle (Play中のみ)")]
    private void TestRegenParticle()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Play中のみ実行可能"); return; }
        if (regenParticle == null) { Debug.LogWarning("regenParticle is null"); return; }
        var main = regenParticle.main;
        main.startColor = new ParticleSystem.MinMaxGradient(stringGradient);
        regenParticle.Play();
        Debug.Log($"[DollController] Test RegenParticle played at {regenParticle.transform.position}");
    }

    [ContextMenu("Setup Cut Particle")]
    private void SetupCutParticle()
    {
        if (Application.isPlaying) { Debug.LogWarning("Play中は実行不可"); return; }

        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Prefabs/Effects"))
            UnityEditor.AssetDatabase.CreateFolder("Assets/Prefabs", "Effects");

        var go = new GameObject("StringCutParticle");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
        main.maxParticles = 40;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sortingOrder = 10;
        var wispMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_WispParticle.mat");
        if (wispMat != null) psRenderer.material = wispMat;

        string path = "Assets/Prefabs/Effects/StringCutParticle.prefab";
        var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
        DestroyImmediate(go);

        cutParticlePrefab = prefab.GetComponent<ParticleSystem>();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[DollController] Cut Particle Prefab作成: {path}");
    }

    [ContextMenu("Setup Regen Particle")]
    private void SetupRegenParticle()
    {
        if (Application.isPlaying) { Debug.LogWarning("Play中は実行不可"); return; }

        if (regenParticle != null)
        {
            DestroyImmediate(regenParticle.gameObject);
            regenParticle = null;
        }
        Transform parent = (stringOrigin != null) ? stringOrigin : transform;
        var existing = parent.Find("RegenParticle");
        if (existing != null) DestroyImmediate(existing.gameObject);

        var go = new GameObject("RegenParticle");
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var ps = go.AddComponent<ParticleSystem>();
        var regenRenderer = go.GetComponent<ParticleSystemRenderer>();
        regenRenderer.sortingOrder = 10;
        var wispMatR = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_WispParticle.mat");
        if (wispMatR != null) regenRenderer.material = wispMatR;

        var main = ps.main;
        main.loop = false;
        main.duration = 1.0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
        main.maxParticles = 200;
        main.playOnAwake = false;
        main.gravityModifier = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 60f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.02f;
        shape.rotation = new Vector3(180f, 0f, 0f);  // 下向き（指先→人形方向）

        regenParticle = ps;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(go);
        Debug.Log($"[DollController] Regen Particle作成: {parent.name}の子");
    }

    private void OnDestroy()
    {
        if (_enemyShooter != null)
            _enemyShooter.OnFired -= PlayAttack;
    }

    private void OnDrawGizmos()
    {
        Vector2 totalAttach = stringAttachOffset + _frameStringOffset;
        Vector3 attachPos = transform.position + new Vector3(totalAttach.x, totalAttach.y, 0f);
        UnityEditor.Handles.color = new Color(0.267f, 1f, 0.8f);
        UnityEditor.Handles.DrawSolidDisc(attachPos, Vector3.forward, 0.06f);
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(attachPos + Vector3.up * 0.12f, "attach");

        Vector3 center = Application.isPlaying ? _initialBasePos : transform.position;
        UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.8f);
        UnityEditor.Handles.DrawWireCube(center, new Vector3(moveBoundsSize.x, moveBoundsSize.y, 0f));
    }
#endif
}
