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

    [Header("Curse System")]
    [Tooltip("呪い最大レベル（この値まで蓄積後、次の糸切断で強化トリガー）")]
    [SerializeField] private int maxCurseLevel = 5;
    [Tooltip("各呪いレベルのParticleSystem（5個、index=level-1）")]
    [SerializeField] private ParticleSystem[] curseParticles = new ParticleSystem[5];
    [Tooltip("強化時に追加発動するParticleSystem")]
    [SerializeField] private ParticleSystem enhancementParticle;
    [Tooltip("強化時の攻撃間隔倍率（intervalをこの値で割る）")]
    [SerializeField] private float attackFrequencyMultiplier = 2f;
    [Tooltip("強化持続時間（秒）")]
    [SerializeField] private float enhancementDuration = 8f;
    [Tooltip("弾ヒット時のシェイク参照")]
    [SerializeField] private EnemySpriteShake spriteShake;

    [Header("Hit SE")]
    [Tooltip("通常ヒットSE（ランダム選択）")]
    [SerializeField] private AudioClip[] normalHitClips;
    [Tooltip("Just（強化）ヒットSE（ランダム選択）")]
    [SerializeField] private AudioClip[] justHitClips;
    [Range(0f, 1f)]
    [SerializeField] private float hitSeVolume = 1f;
    [SerializeField] private float hitSeMinInterval = 0.06f;

    [Header("Settings")]
    [SerializeField] private float fallbackFps = 14f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private AudioSource audioSource;

    [Header("Editor Preview")]
    [SerializeField] private EditorPreviewMode previewMode = EditorPreviewMode.None;
    [Tooltip("Attackプレビュー時に表示するattackFramesのインデックス")]
    [SerializeField] private int previewAttackIndex = 0;

    [HideInInspector] [SerializeField] private Vector3 editorBasePos;
    [HideInInspector] [SerializeField] private bool editorBaseCaptured = false;

    private Coroutine _attackCo;
    private Vector3 _basePos;
    private Vector3 _swayOffset;
    private Vector2 _frameOffset;
    private Vector2 _frameStringOffset;
    private float _currentSwaySpeedX;
    private float _currentSwaySpeedY;
    private float _rotationZ;
    private float _currentRotSpeed;
    private float _currentRotAmplitude;

    private int _curseLevel = 0;
    private bool _isEnhanced = false;
    private Coroutine _enhancementCo;
    private float _lastHitSeTime = -999f;

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
        SetFrame(hangFrame);
        StartCoroutine(SwayLoop());
        StartCoroutine(AttackTimerLoop());

        if (stringRenderer != null)
            stringRenderer.positionCount = 2;
    }

    // ==============================
    // Curse System
    // ==============================

    /// <summary>BossHandControllerから糸切断時に呼ぶ</summary>
    public void OnStringCut()
    {
        if (!Application.isPlaying) return;
        if (_isEnhanced) return;
        if (_curseLevel >= maxCurseLevel)
            TriggerEnhancement();
        else
        {
            _curseLevel++;
            UpdateCurseEffects();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var bullet = collision.collider.GetComponent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;

        bool isPowered = bullet.DamageMultiplier > 1.0001f;

        // 呪いレベル減少
        if (_curseLevel > 0)
        {
            _curseLevel--;
            UpdateCurseEffects();
        }

        // Shake
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

    private void UpdateCurseEffects()
    {
        for (int i = 0; i < curseParticles.Length; i++)
        {
            if (curseParticles[i] == null) continue;
            bool active = i < _curseLevel;
            if (active && !curseParticles[i].isPlaying)
                curseParticles[i].Play();
            else if (!active && curseParticles[i].isPlaying)
                curseParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void TriggerEnhancement()
    {
        _isEnhanced = true;
        foreach (var ps in curseParticles)
            if (ps != null && !ps.isPlaying) ps.Play();
        if (enhancementParticle != null) enhancementParticle.Play();
        if (_enhancementCo != null) StopCoroutine(_enhancementCo);
        _enhancementCo = StartCoroutine(EnhancementRoutine());
    }

    private IEnumerator EnhancementRoutine()
    {
        yield return new WaitForSeconds(enhancementDuration);
        _isEnhanced = false;
        _curseLevel = 0;
        UpdateCurseEffects();
        if (enhancementParticle != null)
            enhancementParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _enhancementCo = null;
    }

    // ==============================
    // Attack
    // ==============================

    private IEnumerator AttackTimerLoop()
    {
        while (true)
        {
            float interval = Random.Range(attackIntervalMin, attackIntervalMax);
            if (_isEnhanced) interval /= attackFrequencyMultiplier;
            yield return new WaitForSeconds(interval);
            PlayAttack();
        }
    }

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
        float tx   = Random.Range(0f, Mathf.PI * 2f);
        float ty   = Random.Range(0f, Mathf.PI * 2f);
        float tRot = Random.Range(0f, Mathf.PI * 2f);
        _currentSwaySpeedX   = Random.Range(swaySpeedXMin, swaySpeedXMax);
        _currentSwaySpeedY   = Random.Range(swaySpeedYMin, swaySpeedYMax);
        _currentRotSpeed     = Random.Range(rotationSpeedMin, rotationSpeedMax);
        _currentRotAmplitude = Random.Range(rotationAmplitudeMin, rotationAmplitudeMax);
        while (true)
        {
            float sinX = Mathf.Sin(tx);
            float sinY = Mathf.Sin(ty);
            _swayOffset = new Vector3(
                sinX >= 0f ? sinX * swayAmplitudeRight : sinX * swayAmplitudeLeft,
                sinY >= 0f ? sinY * swayAmplitudeUp    : sinY * swayAmplitudeDown,
                0f);
            _rotationZ = Mathf.Sin(tRot) * _currentRotAmplitude;
            UpdatePosition();
            tx   += Time.deltaTime * _currentSwaySpeedX;
            ty   += Time.deltaTime * _currentSwaySpeedY;
            tRot += Time.deltaTime * _currentRotSpeed;
            if (tx >= Mathf.PI * 2f)
            {
                tx -= Mathf.PI * 2f;
                _currentSwaySpeedX = Random.Range(swaySpeedXMin, swaySpeedXMax);
            }
            if (ty >= Mathf.PI * 2f)
            {
                ty -= Mathf.PI * 2f;
                _currentSwaySpeedY = Random.Range(swaySpeedYMin, swaySpeedYMax);
            }
            if (tRot >= Mathf.PI * 2f)
            {
                tRot -= Mathf.PI * 2f;
                _currentRotSpeed     = Random.Range(rotationSpeedMin, rotationSpeedMax);
                _currentRotAmplitude = Random.Range(rotationAmplitudeMin, rotationAmplitudeMax);
            }
            yield return null;
        }
    }

    private void LateUpdate()
    {
        if (stringRenderer == null || stringOrigin == null) return;

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
    [ContextMenu("Reset String Gradient")]
    private void ResetStringGradient()
    {
        stringGradient = new Gradient();
        InitGradientIfDefault();
        if (stringRenderer != null)
            stringRenderer.colorGradient = stringGradient;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void OnDrawGizmos()
    {
        Vector2 totalAttach = stringAttachOffset + _frameStringOffset;
        Vector3 attachPos = transform.position + new Vector3(totalAttach.x, totalAttach.y, 0f);
        UnityEditor.Handles.color = new Color(0.267f, 1f, 0.8f);
        UnityEditor.Handles.DrawSolidDisc(attachPos, Vector3.forward, 0.06f);
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(attachPos + Vector3.up * 0.12f, "attach");
    }
#endif
}
