using UnityEngine;

public class CactusController : MonoBehaviour
{
    [System.Serializable]
    public class CactusColliderData
    {
        public Vector2 size = new Vector2(1f, 1.5f);
        public Vector2 offset = Vector2.zero;
    }

    public enum EditorPreviewMode { Idle, Run1, Run2, Animate }

    [Header("Sprites")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] runSprites;

    [Header("Animation")]
    [Range(0.01f, 1f)]
    [SerializeField] private float runFrameInterval = 0.12f;

    [Header("Colliders")]
    [SerializeField] private BoxCollider2D bodyCollider;
    [SerializeField] private CactusColliderData idleCollider = new CactusColliderData();
    [SerializeField] private CactusColliderData run1Collider = new CactusColliderData();
    [SerializeField] private CactusColliderData run2Collider = new CactusColliderData();

    [Header("Editor Preview")]
    [Tooltip("Idle=待機, Run1/Run2=静止フレーム, Animate=ループ再生")]
    [SerializeField] private EditorPreviewMode previewMode = EditorPreviewMode.Idle;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform muzzlePoint;

    private EnemyMover _mover;
    private float _runTimer;
    private int _runFrameIdx;
    private bool _wasDashing;
    private Vector3 _prevPosition;

#if UNITY_EDITOR
    private bool _editorAnimRunning;
    private double _editorAnimLastTime;
    private int _editorAnimFrameIdx;
#endif

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

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyCollider == null)
            bodyCollider = GetComponentInChildren<BoxCollider2D>();
        _mover = GetComponent<EnemyMover>();
        _prevPosition = transform.position;
    }

    private void ApplyCollider(CactusColliderData data)
    {
        if (bodyCollider == null || data == null) return;
        bodyCollider.size = data.size;
        bodyCollider.offset = data.offset;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (spriteRenderer == null || _mover == null) return;

        bool dashing = _mover.IsCactusDashing;

        if (dashing)
        {
            float dx = transform.position.x - _prevPosition.x;
            if (dx > 0.001f)       spriteRenderer.flipX = false;
            else if (dx < -0.001f) spriteRenderer.flipX = true;
        }

        if (dashing && runSprites != null && runSprites.Length > 0)
        {
            if (!_wasDashing)
            {
                spriteRenderer.sprite = runSprites[0];
                _runFrameIdx = 0;
                _runTimer = 0f;
                ApplyCollider(run1Collider);
            }

            _runTimer += Time.deltaTime;
            if (_runTimer >= runFrameInterval)
            {
                _runTimer -= runFrameInterval;
                _runFrameIdx = (_runFrameIdx + 1) % runSprites.Length;
                spriteRenderer.sprite = runSprites[_runFrameIdx];
                ApplyCollider(_runFrameIdx == 0 ? run1Collider : run2Collider);
            }
        }
        else if (!dashing)
        {
            if (_wasDashing)
            {
                if (idleSprite != null)
                    spriteRenderer.sprite = idleSprite;
                ApplyCollider(idleCollider);
                _runTimer = 0f;
                _runFrameIdx = 0;
            }
        }

        _prevPosition = transform.position;
        _wasDashing = dashing;
    }

    private void OnDrawGizmos()
    {
        if (muzzlePoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(muzzlePoint.position, 0.08f);
        float arm = 0.14f;
        Gizmos.DrawLine(muzzlePoint.position - Vector3.right * arm, muzzlePoint.position + Vector3.right * arm);
        Gizmos.DrawLine(muzzlePoint.position - Vector3.up    * arm, muzzlePoint.position + Vector3.up    * arm);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(muzzlePoint.position + Vector3.up * 0.18f, "Muzzle");
#endif
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;

#if UNITY_EDITOR
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyCollider == null)
            bodyCollider = GetComponentInChildren<BoxCollider2D>();

        if (previewMode == EditorPreviewMode.Animate)
        {
            StartEditorAnim();
        }
        else
        {
            StopEditorAnim();

            if (spriteRenderer == null) return;

            switch (previewMode)
            {
                case EditorPreviewMode.Run1:
                    if (runSprites != null && runSprites.Length > 0 && runSprites[0] != null)
                        spriteRenderer.sprite = runSprites[0];
                    ApplyCollider(run1Collider);
                    break;
                case EditorPreviewMode.Run2:
                    if (runSprites != null && runSprites.Length > 1 && runSprites[1] != null)
                        spriteRenderer.sprite = runSprites[1];
                    ApplyCollider(run2Collider);
                    break;
                default:
                    if (idleSprite != null)
                        spriteRenderer.sprite = idleSprite;
                    ApplyCollider(idleCollider);
                    break;
            }

            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }

#if UNITY_EDITOR
    private void StartEditorAnim()
    {
        if (runSprites == null || runSprites.Length == 0) return;

        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;

        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }

        ApplyEditorFrame(_editorAnimFrameIdx);
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning || runSprites == null || runSprites.Length == 0)
        {
            StopEditorAnim();
            return;
        }

        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= runFrameInterval)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % runSprites.Length;
            ApplyEditorFrame(_editorAnimFrameIdx);
        }
    }

    private void ApplyEditorFrame(int index)
    {
        if (spriteRenderer == null || runSprites == null || index >= runSprites.Length) return;
        if (runSprites[index] != null)
            spriteRenderer.sprite = runSprites[index];
        ApplyCollider(index == 0 ? run1Collider : run2Collider);
        if (bodyCollider != null)
            UnityEditor.EditorUtility.SetDirty(bodyCollider);
        UnityEditor.SceneView.RepaintAll();
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
    }
#endif
}
