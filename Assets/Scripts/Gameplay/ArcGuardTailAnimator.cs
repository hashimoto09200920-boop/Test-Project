using System.Collections;
using UnityEngine;

// 尾の見た目・発射だけを担当（HP/破壊は ArcGuardTailHealth が別担当）。
// ArcGuardController から SetMode()/PlaySweepRoutine() で駆動される。
public class ArcGuardTailAnimator : MonoBehaviour
{
    public enum TailMode
    {
        IdleSway, // しなり・伸縮ループ + MultiWarhead発射（本体Idle中・スライド移動中）
        Frozen,   // 静止した専用ポーズ（ジャンプ/爪攻撃/咆哮中）
    }

    public enum ArcGuardTailPreviewState
    {
        IdleSwayAnimate,
        Frozen,
        SweepAnimate,
    }

    [System.Serializable]
    public class ArcGuardTailAnimFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数")]
        public float   duration;
        [Tooltip("マズル位置（尾の先端。ローカル座標）")]
        public Vector2 muzzleOffset;
        [Tooltip("スプライトのZ軸回転（度）")]
        public float   rotationZ;
    }

    // ======================================================
    // Inspector fields
    // ======================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Editor Preview")]
    [SerializeField] private ArcGuardTailPreviewState previewState = ArcGuardTailPreviewState.IdleSwayAnimate;

    [Header("Sprites - Idle Sway（しなり・伸縮）")]
    [NonReorderable]
    [SerializeField] private ArcGuardTailAnimFrame[] idleSwayFrames;

    [Header("Sprites - Frozen（爪攻撃/ジャンプ/咆哮中の固定姿勢）")]
    [SerializeField] private ArcGuardTailAnimFrame frozenFrame;

    [Header("Sprites - Sweep（薙ぎ払い、後半フェーズ限定）")]
    [NonReorderable]
    [SerializeField] private ArcGuardTailAnimFrame[] sweepFrames;

    [Header("MultiWarhead（Idle Sway中、先端から発射）")]
    [SerializeField] private float multiWarheadInterval = 1.2f;
    [SerializeField] private int   multiWarheadBulletTypeIndex = 0;

    [Header("Sweep Domino Fire（付け根→先端へ1フレームずつ発射）")]
    [Tooltip("フレームのDurationが未設定(0)の場合のフォールバック秒数")]
    [SerializeField] private float dominoFrameDuration = 0.05f;
    [SerializeField] private int   sweepBulletTypeIndex = 1;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    // ======================================================
    // Runtime
    // ======================================================

    private TailMode _mode = TailMode.IdleSway;
    private EnemyData _enemyData;
    private bool _isDead;
    private Coroutine _idleSwayCoroutine;

    /// <summary>本体側のSpawnBullet相当を呼ぶコールバック（Owner Collision Ignore等を本体側の実装に一本化するため）。</summary>
    private System.Action<Vector3, Vector2, EnemyData.BulletType> _spawnBullet;

    // ======================================================
    // Public API（ArcGuardControllerから駆動）
    // ======================================================

    public void Initialize(EnemyData enemyData, System.Action<Vector3, Vector2, EnemyData.BulletType> spawnBulletCallback)
    {
        _enemyData = enemyData;
        _spawnBullet = spawnBulletCallback;

        // _modeの初期値は既にIdleSwayのため、SetMode(IdleSway)は早期returnで何もしない。
        // ここで明示的にループを開始する。
        if (_idleSwayCoroutine == null)
            _idleSwayCoroutine = StartCoroutine(IdleSwayLoop());
    }

    public void SetFlip(bool flipX)
    {
        if (spriteRenderer != null) spriteRenderer.flipX = flipX;
    }

    public void SetMode(TailMode mode)
    {
        if (_mode == mode) return;
        _mode = mode;

        if (_idleSwayCoroutine != null) { StopCoroutine(_idleSwayCoroutine); _idleSwayCoroutine = null; }

        if (mode == TailMode.IdleSway)
        {
            _idleSwayCoroutine = StartCoroutine(IdleSwayLoop());
        }
        else
        {
            ApplyFrame(frozenFrame);
        }
    }

    /// <summary>薙ぎ払い1回分を再生（付け根→先端へドミノ式に発射）。完了までブロックする。</summary>
    public IEnumerator PlaySweepRoutine()
    {
        if (_idleSwayCoroutine != null) { StopCoroutine(_idleSwayCoroutine); _idleSwayCoroutine = null; }

        if (sweepFrames != null && sweepFrames.Length > 0)
        {
            foreach (var frame in sweepFrames)
            {
                if (_isDead) yield break;
                if (frame == null) continue;

                ApplyFrame(frame);
                FireFromMuzzle(frame.muzzleOffset, sweepBulletTypeIndex);

                float dur = frame.duration > 0f ? frame.duration : dominoFrameDuration;
                yield return WaitScaled(dur);
            }
        }

        // SetMode(IdleSway)は_modeが既にIdleSwayのままだと早期returnで何もしないため、
        // ここでは直接コルーチンを再開する（_idleSwayCoroutineはこの関数の先頭で止めたまま）
        if (!_isDead && _mode == TailMode.IdleSway)
            _idleSwayCoroutine = StartCoroutine(IdleSwayLoop());
    }

    // ======================================================
    // Idle Sway
    // ======================================================

    private IEnumerator IdleSwayLoop()
    {
        float warheadTimer = 0f;
        int idx = 0;

        while (true)
        {
            if (_isDead) yield break;

            if (idleSwayFrames != null && idleSwayFrames.Length > 0)
            {
                var f = idleSwayFrames[idx % idleSwayFrames.Length];
                ApplyFrame(f);
                float frameDur = (f != null && f.duration > 0f) ? f.duration : 0.15f;

                float elapsed = 0f;
                while (elapsed < frameDur)
                {
                    if (_isDead) yield break;
                    float dt = Time.deltaTime * GetTimeScale();
                    elapsed += dt;
                    warheadTimer += dt;

                    if (warheadTimer >= multiWarheadInterval)
                    {
                        warheadTimer = 0f;
                        Vector2 muzzle = idleSwayFrames[idx % idleSwayFrames.Length].muzzleOffset;
                        FireFromMuzzle(muzzle, multiWarheadBulletTypeIndex);
                    }
                    yield return null;
                }
                idx++;
            }
            else
            {
                yield return null;
            }
        }
    }

    // ======================================================
    // Bullet
    // ======================================================

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (_enemyData == null || _enemyData.bulletTypes == null) return null;
        if (index < 0 || index >= _enemyData.bulletTypes.Length) return null;
        return _enemyData.bulletTypes[index];
    }

    private void FireFromMuzzle(Vector2 localMuzzleOffset, int bulletTypeIndex)
    {
        EnemyData.BulletType bt = GetBulletType(bulletTypeIndex);
        if (bt == null || _spawnBullet == null) return;

        float x = (spriteRenderer != null && spriteRenderer.flipX) ? -localMuzzleOffset.x : localMuzzleOffset.x;
        Vector3 muzzleWorld = transform.position + new Vector3(x, localMuzzleOffset.y, 0f);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        Vector2 dir = playerObj != null
            ? ((Vector2)(playerObj.transform.position - muzzleWorld)).normalized
            : Vector2.down;

        _spawnBullet(muzzleWorld, dir, bt);

        if (logDebug)
            Debug.Log($"[ArcGuardTailAnimator] Fire bulletTypeIndex={bulletTypeIndex} from {muzzleWorld}", this);
    }

    private float GetTimeScale() => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private IEnumerator WaitScaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (_isDead) yield break;
            t += Time.deltaTime * GetTimeScale();
            yield return null;
        }
    }

    private void OnDestroy()
    {
        _isDead = true;
    }

    // ======================================================
    // Frame apply
    // ======================================================

    private void ApplyFrame(ArcGuardTailAnimFrame frame)
    {
        if (frame == null || spriteRenderer == null) return;
        if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
        ApplyOffset(frame.offset);
        ApplyRotation(frame.rotationZ);
    }

    private void ApplyOffset(Vector2 offset)
    {
        if (spriteRenderer == null) return;
        var t = spriteRenderer.transform;
        float x = spriteRenderer.flipX ? -offset.x : offset.x;
        Vector3 local = t.localPosition;
        t.localPosition = new Vector3(x, offset.y, local.z);
    }

    private void ApplyRotation(float rotationZ)
    {
        if (spriteRenderer == null) return;
        float z = spriteRenderer.flipX ? -rotationZ : rotationZ;
        spriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    // ======================================================
    // Editor Preview
    // ======================================================

    private void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update += OnEditorTickRefresh;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= OnEditorTickRefresh;
        StopEditorAnim();
#endif
    }

#if UNITY_EDITOR
    private bool   _editorAnimRunning;
    private double _editorAnimLastTime;
    private int    _editorAnimFrameIdx;

    private void OnEditorTickRefresh()
    {
        if (this == null || Application.isPlaying) return;
        if (_editorAnimRunning) return;
        if (spriteRenderer == null) return;
        if (previewState != ArcGuardTailPreviewState.Frozen) return;

        ApplyFrame(frozenFrame);
        UnityEditor.SceneView.RepaintAll();
    }

    private void StartEditorAnim()
    {
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        var frames = GetEditorAnimFrames();
        if (frames != null && frames.Length > 0 && frames[0] != null && spriteRenderer != null)
            ApplyFrame(frames[0]);
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private ArcGuardTailAnimFrame[] GetEditorAnimFrames()
    {
        return previewState == ArcGuardTailPreviewState.SweepAnimate ? sweepFrames : idleSwayFrames;
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
            if (next != null && next.sprite != null && spriteRenderer != null)
                ApplyFrame(next);
            UnityEditor.SceneView.RepaintAll();
        }
    }
#endif

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        bool isAnim = previewState == ArcGuardTailPreviewState.IdleSwayAnimate ||
                      previewState == ArcGuardTailPreviewState.SweepAnimate;

        if (isAnim)
        {
            StartEditorAnim();
        }
        else
        {
            StopEditorAnim();
            ApplyFrame(frozenFrame);
        }

        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        ArcGuardTailAnimFrame f = previewState == ArcGuardTailPreviewState.Frozen
            ? frozenFrame
            : (GetEditorAnimFrames() != null && GetEditorAnimFrames().Length > 0 ? GetEditorAnimFrames()[_editorAnimFrameIdx % GetEditorAnimFrames().Length] : null);
        if (f == null) return;

        Vector3 muzzleWorld = transform.position + new Vector3(f.offset.x + f.muzzleOffset.x, f.offset.y + f.muzzleOffset.y, 0f);
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawSphere(muzzleWorld, 0.06f);
    }
#endif
}
