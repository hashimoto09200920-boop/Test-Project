using System.Collections;
using UnityEngine;

[ExecuteAlways]
public class PlayerStringController : MonoBehaviour
{
    public enum ReverseType { LineType, Coordinate }

    [Header("String Type")]
    [Tooltip("LineType=白赤反転（③）/ Coordinate=座標反転（⑤）")]
    [SerializeField] private ReverseType reverseType;

    [Header("References")]
    [Tooltip("糸の接続元（FingerTip_03 or FingerTip_05）")]
    [SerializeField] private Transform stringOrigin;
    [Tooltip("糸の接続先（PixelDancerController）")]
    [SerializeField] private PixelDancerController target;
    [Tooltip("接続先のオフセット（body center調整用）")]
    [SerializeField] private Vector2 targetAttachOffset = Vector2.zero;

    [Header("String (LineRenderer)")]
    [SerializeField] private LineRenderer stringRenderer;
    [SerializeField] private LineRenderer outlineRenderer;
    [Range(1.1f, 3f)]
    [SerializeField] private float outlineWidthMultiplier = 1.4f;
    [SerializeField] private float stringWidth = 0.03f;
    [SerializeField] private int stringSegments = 12;
    [SerializeField] private float stringDrop = 0.5f;
    [SerializeField] private Gradient stringGradient;

    [Header("String Colliders")]
    [SerializeField] private int stringColliderCount = 6;
    [SerializeField] private float stringColliderRadius = 0.12f;

    [Header("String Hit")]
    [Tooltip("糸の耐久HP（この回数だけ反射弾が当たると切断）")]
    [SerializeField] private int stringHp = 3;
    [SerializeField] private AudioClip[] stringNormalHitClips;
    [SerializeField] private AudioClip[] stringJustHitClips;
    [Range(0f, 1f)]
    [SerializeField] private float stringHitSeVolume = 1f;

    [Header("String Regen")]
    [Tooltip("切断後に糸が再生成されるまでの秒数（初回出現時のディレイはOrchestratorで設定）")]
    [SerializeField] private float stringRegenDelay = 3f;
    [Tooltip("糸が指先からPixelDancerまで伸びきる時間（初回・切断後再生どちらも共通）")]
    [SerializeField] private float stringExtendTime = 1.5f;
    [Tooltip("再生成開始時に指先で再生するパーティクル（未設定なら省略）")]
    [SerializeField] private ParticleSystem regenParticle;

    [Header("String Cut VFX")]
    [Tooltip("糸切断時に切断点で再生するバーストパーティクル（Prefab）")]
    [SerializeField] private ParticleSystem cutParticlePrefab;

    [Header("Editor Preview")]
    [Tooltip("Play前プレビュー用の糸の終点ワールド座標（target未設定時に使用）")]
    [SerializeField] private Vector2 editorPreviewEndPos = new Vector2(0f, -4f);

    // ---- Runtime state ----
    private CapsuleCollider2D[] _segmentColliders;
    private bool _isStringCut = false;
    private bool _isActivated = false;
    private bool _isExtending = false;
    private Coroutine _regenCo;
    private Coroutine _extendCo;
    private int _currentStringHp;
    private float _lastStringHitTime = -999f;
    private const float StringHitCooldown = 0.1f;
    private Vector3 _lastStringHitPos;
    private AudioSource _audioSource;
    private bool _reversalApplied = false;

    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    private void Awake()
    {
        if (target == null)
            target = FindFirstObjectByType<PixelDancerController>();

        if (Application.isPlaying)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
            }
        }

        InitGradientIfDefault();
        if (stringRenderer != null)
        {
            stringRenderer.widthMultiplier = stringWidth;
            stringRenderer.colorGradient   = stringGradient;
            if (outlineRenderer != null)
            {
                outlineRenderer.useWorldSpace    = stringRenderer.useWorldSpace;
                outlineRenderer.sortingLayerID   = stringRenderer.sortingLayerID;
                outlineRenderer.widthMultiplier  = stringWidth * outlineWidthMultiplier;
                outlineRenderer.colorGradient    = stringGradient;
            }
        }

        InitStringColliders();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        _currentStringHp = stringHp;
        SetStringVisible(false);
    }

    private void OnDestroy()
    {
        _isActivated = false;
        _isExtending = false;
        ApplyReversal(false);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            EditorPreviewUpdate();
            return;
        }
        if (!_isActivated) return;
        if (_isStringCut) return;

        // ソウル状態中は反転を停止、復帰したら再開（String03/05共通）
        bool shouldReverse = !PixelDancerController.IsDownGlobal;
        if (shouldReverse != _reversalApplied)
            ApplyReversal(shouldReverse);

        if (stringRenderer == null || stringOrigin == null || target == null) return;
        UpdateStringLine();
        UpdateStringColliders();
    }

    private void EditorPreviewUpdate()
    {
        if (stringRenderer == null || stringOrigin == null) return;

        Vector3 end;
        if (target != null)
            end = target.transform.position + new Vector3(targetAttachOffset.x, targetAttachOffset.y, 0f);
        else
            end = new Vector3(editorPreviewEndPos.x, editorPreviewEndPos.y, stringOrigin.position.z);

        stringRenderer.enabled = true;
        int segs = Mathf.Max(2, stringSegments);
        if (stringRenderer.positionCount != segs)
            stringRenderer.positionCount = segs;

        Vector3 start = stringOrigin.position;
        for (int i = 0; i < segs; i++)
        {
            float t = (float)i / (segs - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            p.y -= stringDrop * 4f * t * (1f - t);
            stringRenderer.SetPosition(i, p);
        }
    }

    // ==============================
    // String Hit（PlayerStringSegmentTriggerから呼ぶ）
    // ==============================

    public void OnStringHit(EnemyBullet bullet, Rigidbody2D bulletRb, Vector2 segmentNormal, Vector3 hitPos, float gradientT)
    {
        if (!Application.isPlaying) return;
        if (_isStringCut) return;

        float now = Time.unscaledTime;
        if (now - _lastStringHitTime < StringHitCooldown) return;
        _lastStringHitTime = now;

        _lastStringHitPos = hitPos;

        // 弾を反射
        if (bulletRb != null)
            bulletRb.linearVelocity = Vector2.Reflect(bulletRb.linearVelocity, segmentNormal);

        bool isPowered = bullet != null && bullet.DamageMultiplier > 1.0001f;
        TryPlayStringHitSe(isPowered);

        int dmg = (bullet == null) ? 1
            : isPowered
                ? Mathf.Max(1, Mathf.RoundToInt(bullet.BlockJustDamage))
                : Mathf.Max(1, Mathf.RoundToInt(bullet.BlockNormalDamage));
        _currentStringHp -= dmg;
        if (_currentStringHp <= 0)
            OnStringCut();
    }

    // ==============================
    // 初期起動（PlayerStringOrchestratorから呼ぶ）
    // ==============================

    public void Activate()
    {
        if (_isActivated || _isExtending) return;
        _isExtending = true;
        StartCoroutine(InitialExtendRoutine());
    }

    private IEnumerator InitialExtendRoutine()
    {
        _extendCo = StartCoroutine(ExtendStringRoutine());
        yield return _extendCo;
        _extendCo = null;
        if (_isStringCut) yield break;
        _isExtending = false;
        _isActivated = true;
        SetStringVisible(true);
        ApplyReversal(true);
    }

    private IEnumerator ExtendStringRoutine()
    {
        if (stringRenderer == null || stringOrigin == null) yield break;

        stringRenderer.enabled = true;
        if (outlineRenderer != null) outlineRenderer.enabled = true;
        if (_segmentColliders != null)
            foreach (var col in _segmentColliders)
                if (col != null) col.enabled = true;

        float elapsed = 0f;
        while (elapsed < stringExtendTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / stringExtendTime);

            Vector3 startPt = stringOrigin.position;
            Vector3 endPt   = target != null
                ? target.transform.position + new Vector3(targetAttachOffset.x, targetAttachOffset.y, 0f)
                : startPt;
            Vector3 animEnd = Vector3.Lerp(startPt, endPt, progress);

            int activeSegs = Mathf.Max(2, Mathf.RoundToInt(progress * stringSegments));
            stringRenderer.positionCount = activeSegs;
            for (int i = 0; i < activeSegs; i++)
            {
                float t = (float)i / (activeSegs - 1);
                Vector3 p = Vector3.Lerp(startPt, animEnd, t);
                p.y -= stringDrop * 4f * t * (1f - t);
                stringRenderer.SetPosition(i, p);
            }

            if (outlineRenderer != null)
            {
                outlineRenderer.positionCount = activeSegs;
                for (int i = 0; i < activeSegs; i++)
                    outlineRenderer.SetPosition(i, stringRenderer.GetPosition(i));
            }

            UpdateStringColliders();

            yield return null;
        }
    }

    public void OnStringCut()
    {
        if (!Application.isPlaying) return;
        if (_isStringCut) return;

        _isStringCut = true;

        if (_extendCo != null) { StopCoroutine(_extendCo); _extendCo = null; }
        if (_regenCo  != null) { StopCoroutine(_regenCo);  _regenCo  = null; }

        if (_isActivated) ApplyReversal(false);
        _isActivated = false;
        _isExtending = false;

        SpawnCutParticle();
        SetStringVisible(false);

        _regenCo = StartCoroutine(StringRegenRoutine());
    }

    private IEnumerator StringRegenRoutine()
    {
        yield return new WaitForSeconds(stringRegenDelay);

        if (regenParticle != null)
            regenParticle.Play();

        _currentStringHp = stringHp;
        _isStringCut = false;
        _isExtending = true;

        _extendCo = StartCoroutine(ExtendStringRoutine());
        yield return _extendCo;
        _extendCo = null;

        if (_isStringCut) yield break;

        _isExtending = false;
        _isActivated = true;
        SetStringVisible(true);
        ApplyReversal(true);
        _regenCo = null;
    }

    // ==============================
    // 反転フラグ制御
    // ==============================

    private void ApplyReversal(bool active)
    {
        _reversalApplied = active;
        if (PaddleDrawer.Instance == null) return;
        if (reverseType == ReverseType.LineType)
            PaddleDrawer.Instance.SetLineTypeReversed(active);
        else
            PaddleDrawer.Instance.SetCoordReversed(active);
    }

    // ==============================
    // Utilities
    // ==============================

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
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        );
    }

    private void UpdateStringLine()
    {
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
        Vector3 end   = target.transform.position + new Vector3(targetAttachOffset.x, targetAttachOffset.y, 0f);

        for (int i = 0; i < segs; i++)
        {
            float t = (float)i / (segs - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            p.y -= stringDrop * 4f * t * (1f - t);
            stringRenderer.SetPosition(i, p);
            if (outlineRenderer != null) outlineRenderer.SetPosition(i, p);
        }
    }

    private void UpdateStringColliders()
    {
        if (_segmentColliders == null) return;
        int segs = stringRenderer.positionCount;
        if (segs < 2) return;

        int colCount = _segmentColliders.Length;
        for (int i = 0; i < colCount; i++)
        {
            int idx0 = Mathf.Clamp(Mathf.RoundToInt((float)i       / colCount * (segs - 1)), 0, segs - 1);
            int idx1 = Mathf.Clamp(Mathf.RoundToInt((float)(i + 1) / colCount * (segs - 1)), 0, segs - 1);
            Vector3 p0 = stringRenderer.GetPosition(idx0);
            Vector3 p1 = stringRenderer.GetPosition(idx1);
            Vector3 mid = (p0 + p1) * 0.5f;
            float len   = Vector2.Distance(p0, p1);
            float angle = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;

            var col = _segmentColliders[i];
            col.transform.position = mid;
            col.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            col.size   = new Vector2(len, stringColliderRadius * 2f);
            col.offset = Vector2.zero;
        }
    }

    private void InitStringColliders()
    {
        if (!Application.isPlaying) return;
        _segmentColliders = new CapsuleCollider2D[stringColliderCount];
        for (int i = 0; i < stringColliderCount; i++)
        {
            var go = new GameObject($"StringSeg_{i}");
            go.transform.SetParent(transform);
            go.layer = gameObject.layer;

            var col = go.AddComponent<CapsuleCollider2D>();
            col.isTrigger  = true;
            col.direction  = CapsuleDirection2D.Horizontal;

            var seg = go.AddComponent<PlayerStringSegmentTrigger>();
            seg.owner          = this;
            seg.segmentIndex   = i;
            seg.totalSegments  = stringColliderCount;

            _segmentColliders[i] = col;
        }
    }

    private void SetStringVisible(bool visible)
    {
        if (stringRenderer  != null) stringRenderer.enabled  = visible;
        if (outlineRenderer != null) outlineRenderer.enabled = visible;
        if (_segmentColliders != null)
            foreach (var col in _segmentColliders)
                if (col != null) col.enabled = visible;
    }

    private void SpawnCutParticle()
    {
        if (cutParticlePrefab == null) return;
        var ps = Instantiate(cutParticlePrefab, _lastStringHitPos, Quaternion.identity);
        ps.Play();
        Destroy(ps.gameObject, 5f);
    }

    private void TryPlayStringHitSe(bool isPowered)
    {
        if (_audioSource == null) return;
        AudioClip[] clips = isPowered ? stringJustHitClips : stringNormalHitClips;
        if (clips == null || clips.Length == 0) return;
        int valid = 0;
        foreach (var c in clips) if (c != null) valid++;
        if (valid == 0) return;
        int pick = Random.Range(0, valid);
        foreach (var c in clips)
        {
            if (c == null) continue;
            if (pick-- == 0) { _audioSource.PlayOneShot(c, stringHitSeVolume * MasterSEVolume); break; }
        }
    }
}
