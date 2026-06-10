using System.Collections;
using UnityEngine;

public class BossHandController : MonoBehaviour
{
    [System.Serializable]
    public class BossHandFrame
    {
        public Sprite sprite;
        public float offsetX = 0f;
        public float offsetY = 0f;
        public float durationMin = 0.07f;
        public float durationMax = 0.13f;
        [Tooltip("このフレームでのFingerTip_02のローカル位置")]
        public Vector2 fingerTipOffset;
    }

    [Header("Back Phase Sprites")]
    [NonReorderable]
    [SerializeField] private BossHandFrame[] backIdleFrames;
    [NonReorderable]
    [SerializeField] private BossHandFrame[] backJitterFrames;

    [Header("Jitter Settings")]
    [Tooltip("Jitterのフレームレート（デフォルト14fps）")]
    [SerializeField] private float jitterFps = 14f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("初期位置スポーン点（SP_05）")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("糸接続点（FingerTip_02）— フレームごとにlocalPositionを更新")]
    [SerializeField] private Transform fingerTip02;

    [Header("Editor Preview — Back Jitter (Play前確認用)")]
    [Tooltip("-1=オフ、0以上=backJitterFramesのインデックスを静止表示")]
    [SerializeField] private int previewFrame = -1;
    [Tooltip("チェックでJitterをEditorでループ再生")]
    [SerializeField] private bool previewAnimate = false;

    [HideInInspector] [SerializeField] private Vector3 editorBasePos;
    [HideInInspector] [SerializeField] private bool editorBaseCaptured = false;
    [HideInInspector] [SerializeField] private int _prevPreviewFrame = -1;

#if UNITY_EDITOR
    private double _editorAnimLastTime;
    private int    _editorAnimFrame;
    private bool   _editorAnimRunning;
    private float  _editorCurrentInterval;
#endif

    private Coroutine jitterCo;
    private bool isBackPhase = false;

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
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spawnPoint != null)
            transform.position = spawnPoint.position;
        var mover = GetComponentInParent<EnemyMover>();
        if (mover != null) mover.suppressMovement = true;
    }

    private void Start()
    {
        EnterBackPhase();
    }

    public void EnterBackPhase()
    {
        isBackPhase = true;

        if (backIdleFrames != null && backIdleFrames.Length > 0)
            ApplyFrame(backIdleFrames[0]);

        if (jitterCo != null) StopCoroutine(jitterCo);
        jitterCo = StartCoroutine(BackJitterLoop());
    }

    private IEnumerator BackJitterLoop()
    {
        if (backJitterFrames == null || backJitterFrames.Length == 0) yield break;

        float defaultInterval = 1f / Mathf.Max(1f, jitterFps);
        Vector3 basePos = transform.position;
        int lastIdx = -1;

        while (isBackPhase)
        {
            int idx;
            do { idx = Random.Range(0, backJitterFrames.Length); }
            while (backJitterFrames.Length > 1 && idx == lastIdx);
            lastIdx = idx;

            var frame = backJitterFrames[idx];
            ApplyFrame(frame, basePos);
            float wait = (frame != null && frame.durationMax > 0f)
                ? Random.Range(frame.durationMin, frame.durationMax)
                : defaultInterval;
            yield return new WaitForSeconds(wait);
        }
    }

    private void ApplyFrame(BossHandFrame frame, Vector3? basePos = null)
    {
        if (frame == null || spriteRenderer == null) return;
        if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
        if (basePos.HasValue)
            transform.position = basePos.Value + new Vector3(frame.offsetX, frame.offsetY, 0f);
        if (fingerTip02 != null)
            fingerTip02.localPosition = new Vector3(frame.fingerTipOffset.x, frame.fingerTipOffset.y, 0f);
    }

#if UNITY_EDITOR
    [ContextMenu("Move to Spawn Point")]
    private void MoveToSpawnPoint()
    {
        if (spawnPoint == null) { Debug.LogWarning("Spawn Point未設定"); return; }
        transform.position = spawnPoint.position;
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }
#endif

    private void OnValidate()
    {
        if (Application.isPlaying) return;

#if UNITY_EDITOR
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (spawnPoint != null)
            editorBasePos = spawnPoint.position;
        else
            editorBasePos = transform.position;
        editorBaseCaptured = true;

        // フレーム切り替え時、直前フレームのFingerTip_02現在位置をfingerTipOffsetに自動保存
        if (!previewAnimate
            && fingerTip02 != null
            && _prevPreviewFrame >= 0
            && _prevPreviewFrame != previewFrame
            && backJitterFrames != null
            && _prevPreviewFrame < backJitterFrames.Length)
        {
            var prev = backJitterFrames[_prevPreviewFrame];
            if (prev != null)
            {
                prev.fingerTipOffset = new Vector2(fingerTip02.localPosition.x, fingerTip02.localPosition.y);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
        _prevPreviewFrame = previewFrame;

        if (previewAnimate)
        {
            StartEditorAnim();
        }
        else
        {
            StopEditorAnim();

            if (previewFrame < 0)
            {
                transform.position = editorBasePos;
            }
            else if (backJitterFrames != null && previewFrame < backJitterFrames.Length)
            {
                var f = backJitterFrames[previewFrame];
                if (f != null)
                {
                    if (f.sprite != null) spriteRenderer.sprite = f.sprite;
                    transform.position = editorBasePos + new Vector3(f.offsetX, f.offsetY, 0f);
                    if (fingerTip02 != null)
                        fingerTip02.localPosition = new Vector3(f.fingerTipOffset.x, f.fingerTipOffset.y, 0f);
                }
            }
        }
#endif
    }

#if UNITY_EDITOR
    private float CalcEditorInterval(BossHandFrame frame)
    {
        return (frame != null && frame.durationMax > 0f)
            ? Random.Range(frame.durationMin, frame.durationMax)
            : 1f / Mathf.Max(1f, jitterFps);
    }

    private void StartEditorAnim()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (backJitterFrames == null || backJitterFrames.Length == 0) return;

        if (!_editorAnimRunning)
        {
            _editorAnimFrame = 0;
            _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
            _editorAnimRunning = true;
            _editorCurrentInterval = CalcEditorInterval(backJitterFrames[0]);
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }

        ApplyEditorFrame(_editorAnimFrame);
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning || backJitterFrames == null || backJitterFrames.Length == 0)
        {
            StopEditorAnim();
            return;
        }

        double now = UnityEditor.EditorApplication.timeSinceStartup;

        if (now - _editorAnimLastTime >= _editorCurrentInterval)
        {
            _editorAnimLastTime = now;
            _editorAnimFrame = (_editorAnimFrame + 1) % backJitterFrames.Length;
            ApplyEditorFrame(_editorAnimFrame);
            _editorCurrentInterval = CalcEditorInterval(backJitterFrames[_editorAnimFrame]);
        }
    }

    private void ApplyEditorFrame(int index)
    {
        if (backJitterFrames == null || index >= backJitterFrames.Length) return;
        var frame = backJitterFrames[index];
        if (frame == null) return;

        if (spriteRenderer != null && frame.sprite != null)
            spriteRenderer.sprite = frame.sprite;

        transform.position = editorBasePos + new Vector3(frame.offsetX, frame.offsetY, 0f);

        if (fingerTip02 != null)
            fingerTip02.localPosition = new Vector3(frame.fingerTipOffset.x, frame.fingerTipOffset.y, 0f);

        UnityEditor.SceneView.RepaintAll();
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
    }
#endif
}
