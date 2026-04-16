using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// カメラ境界基準でGameObjectの位置を自動調整する。
///
/// [アンカーモード] useAnchorMode=true（スポーンポイント推奨）
///   X: ゲームプレイエリア左端〜右端を 0〜1 で指定（SkillHUD幅を自動除外）
///   Y: カメラ下端〜上端を 0〜1 で指定
///   どの画面サイズ・アスペクト比でも割合で配置されるため位置ズレなし。
///
/// [従来モード] useAnchorMode=false（後方互換）
///   X: SkillHUDを除外したゲームプレイエリアの中央 + offsetX（ワールド単位）
///   Y: カメラ底辺 + offsetFromBottom（ワールド単位）
///
/// [ExecuteAlways] でPlay前InspectorでもリアルタイムにPreviewer確認できる。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CameraAnchoredTransform : MonoBehaviour
{
    [Header("カメラ (空=MainCamera自動検索)")]
    [SerializeField] private Camera targetCamera;

    [Header("SkillHUD除外設定")]
    [Tooltip("左側SkillHUDのRectTransform。設定するとHUDを除いたプレイエリア中央にX位置を合わせる。")]
    [SerializeField] private RectTransform skillHudRect;

    [Tooltip("CanvasScalerのReference Resolution Height（デフォルト1080）")]
    [SerializeField] private float canvasReferenceHeight = 1080f;

    // ─── アンカーモード ────────────────────────────────────────
    [Header("アンカーモード（スポーンポイント推奨）")]
    [Tooltip("true: xAnchor/yAnchorで割合指定（画面サイズ依存なし）\nfalse: 従来のoffsetFromBottom（ワールド単位）")]
    [SerializeField] private bool useAnchorMode = false;

    [Tooltip("ゲームプレイエリア横方向の割合\n0.0=左端(SkillHUD右) / 0.5=中央 / 1.0=右端\nSkillHUD幅は自動除外される")]
    [Range(0f, 1f)]
    [SerializeField] private float xAnchor = 0.5f;

    [Tooltip("カメラ縦方向の割合\n0.0=下端 / 0.5=中央 / 1.0=上端")]
    [Range(0f, 1f)]
    [SerializeField] private float yAnchor = 0.8f;

    [Tooltip("アンカー位置からのワールド単位オフセット（微調整用）")]
    [SerializeField] private Vector2 anchorOffset = Vector2.zero;

    // ─── 従来モード ────────────────────────────────────────────
    [Header("従来モード（useAnchorMode=false 時のみ有効）")]
    [Tooltip("SkillHUDを除外したプレイエリア中央からのXオフセット（ワールド単位）")]
    [SerializeField] private float offsetX = 0f;

    [Tooltip("カメラ底辺からの距離（ワールド単位）")]
    [SerializeField] private float offsetFromBottom = 2.5f;

    // ──────────────────────────────────────────────────────────
    [Header("共通設定")]
    [Tooltip("Z座標（2Dゲームは通常0）")]
    [SerializeField] private float positionZ = 0f;

    // 変化検出用キャッシュ
    private Vector2 _lastScreen;
    private float _lastAspect;
    private float _lastOrtho;
    private Vector3 _lastCamPos;
    private float _lastHudWidth;
    private bool  _lastUseAnchor;
    private float _lastXAnchor;
    private float _lastYAnchor;

#if UNITY_EDITOR
    private bool _editApplyQueued;
#endif

    private void Reset()
    {
        FindCam();
    }

    private void OnEnable()
    {
        FindCam();
        Apply(force: true);
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            FindCam();
            Apply(force: true);
        }
    }

    private void OnValidate()
    {
        FindCam();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            QueueApplyInEditMode();
#endif
    }

    private void Update()
    {
        Apply(force: false);
    }

    // ─────────────────────────────────────────

    private void FindCam()
    {
        if (targetCamera != null) return;
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindFirstObjectByType<Camera>();
    }

    private void Apply(bool force)
    {
        if (targetCamera == null) return;
        if (!targetCamera.orthographic) return;

        float ortho  = targetCamera.orthographicSize;
        float aspect = targetCamera.aspect;
        Vector3 camPos = targetCamera.transform.position;
        Vector2 screen = new Vector2(Screen.width, Screen.height);
        float hudRefW  = skillHudRect != null ? skillHudRect.rect.width : 0f;

        // 変化がなければスキップ（軽量化）
        if (!force
            && screen == _lastScreen
            && Mathf.Approximately(aspect,   _lastAspect)
            && Mathf.Approximately(ortho,    _lastOrtho)
            && camPos == _lastCamPos
            && Mathf.Approximately(hudRefW,  _lastHudWidth)
            && useAnchorMode == _lastUseAnchor
            && Mathf.Approximately(xAnchor,  _lastXAnchor)
            && Mathf.Approximately(yAnchor,  _lastYAnchor))
            return;

        _lastScreen     = screen;
        _lastAspect     = aspect;
        _lastOrtho      = ortho;
        _lastCamPos     = camPos;
        _lastHudWidth   = hudRefW;
        _lastUseAnchor  = useAnchorMode;
        _lastXAnchor    = xAnchor;
        _lastYAnchor    = yAnchor;

        float halfH = ortho;
        float halfW = halfH * aspect;

        // SkillHUDのワールド幅を計算
        // CanvasScaler MatchHeight=1 の場合:
        //   hudWorldWidth = hudRefPx / referenceHeight * (ortho * 2)
        //   ※ 画面解像度が変わっても常に一定値になる
        float hudWorldWidth = (canvasReferenceHeight > 0f && skillHudRect != null)
            ? (hudRefW / canvasReferenceHeight) * (ortho * 2f)
            : 0f;

        float gameplayLeftX  = camPos.x - halfW + hudWorldWidth;
        float gameplayRightX = camPos.x + halfW;
        float camBottomY     = camPos.y - halfH;
        float camTopY        = camPos.y + halfH;

        float newX, newY;

        if (useAnchorMode)
        {
            // アンカーモード: X/Y ともに割合で指定（画面サイズ・アスペクト比に追従）
            newX = Mathf.Lerp(gameplayLeftX, gameplayRightX, xAnchor) + anchorOffset.x;
            newY = Mathf.Lerp(camBottomY,    camTopY,        yAnchor) + anchorOffset.y;
        }
        else
        {
            // 従来モード: ワールド単位オフセット
            float gameplayCenterX = (gameplayLeftX + gameplayRightX) * 0.5f;
            newX = gameplayCenterX + offsetX;
            newY = camBottomY + offsetFromBottom;
        }

        transform.position = new Vector3(newX, newY, positionZ);
    }

#if UNITY_EDITOR
    private void QueueApplyInEditMode()
    {
        if (_editApplyQueued) return;
        _editApplyQueued = true;
        EditorApplication.delayCall += () =>
        {
            _editApplyQueued = false;
            if (this == null) return;
            if (Application.isPlaying) return;
            Apply(force: true);
        };
    }

    private void OnDrawGizmos()
    {
        // アンカーモード時のみ表示（スポーンポイント用）
        if (!useAnchorMode) return;

        Gizmos.color = new Color(0f, 1f, 1f, 1f); // シアン（完全不透明）
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(
            transform.position + Vector3.left  * 0.4f,
            transform.position + Vector3.right * 0.4f);
        Gizmos.DrawLine(
            transform.position + Vector3.up    * 0.4f,
            transform.position + Vector3.down  * 0.4f);

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.45f,
            $"{gameObject.name}\n({xAnchor:F2}, {yAnchor:F2})");
    }
#endif
}
