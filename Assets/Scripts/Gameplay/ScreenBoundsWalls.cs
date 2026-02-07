using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 画面端（カメラ表示範囲）に上下左右の壁(BoxCollider2D)を自動配置する。
/// Unity 6 / 2D（Orthographic Camera）想定。
/// </summary>
[ExecuteAlways]
public sealed class ScreenBoundsWalls : MonoBehaviour
{
    [Header("Target Camera (空なら Main Camera を使用)")]
    [SerializeField] private Camera targetCamera;

    [Header("Walls (BoxCollider2D が付いた GameObject を割り当て)")]
    [SerializeField] private Transform wallTop;
    [SerializeField] private Transform wallBottom;
    [SerializeField] private Transform wallLeft;
    [SerializeField] private Transform wallRight;

    public enum PlacementMode
    {
        Outside, // 画面外側（従来）
        Inside   // 画面内側（壁が見える）
    }

    [Header("Placement")]
    [Tooltip("Outside: 画面外側 / Inside: 画面内側（壁の厚み分だけ内側に寄せる）")]
    [SerializeField] private PlacementMode placement = PlacementMode.Outside;

    [Tooltip("配置モード方向へ追加でずらす量（ワールド座標）。Insideなら内側へ、Outsideなら外側へ。")]
    [Min(0f)]
    [SerializeField] private float placementMargin = 0.0f;

    [Header("Wall Settings")]
    [Tooltip("壁の厚み（ワールド座標）")]
    [Min(0.01f)]
    [SerializeField] private float thickness = 0.5f;

    [Tooltip("壁の長さに足す余白（ワールド座標）")]
    [SerializeField] private float lengthMargin = 1.0f;

    [Header("Visual (Sprite Renderer)")]
    [Tooltip("ON: Sprite Renderer( Draw Mode = Tiled ) の Size を BoxCollider2D.size に合わせる")]
    [SerializeField] private bool syncSpriteRendererSizeToCollider = true;

    private Vector2 _lastSize;
    private float _lastAspect;
    private float _lastOrthoSize;
    private Vector3 _lastCamPos;

#if UNITY_EDITOR
    private bool _editApplyQueued;
#endif

    private void Reset()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        // ExecuteAlways: Edit/Play 両方で呼ばれるので安全に分岐
        if (Application.isPlaying)
        {
            // Play中はこの後 Start() でも強制適用するが、OnEnableでも即時反映してOK
            Apply(force: true);
        }
        else
        {
            // Edit中は遅延で実行（OnValidate/Awake系の禁止タイミングを避ける）
#if UNITY_EDITOR
            QueueApplyInEditMode();
#endif
        }
    }

    private void Start()
    {
        // Play開始直後に必ず1回合わせる（Play中はOnValidate起因の警告を避けたい）
        if (Application.isPlaying)
        {
            Apply(force: true);
        }
    }

    private void OnValidate()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        // ★重要：OnValidate中に Apply() を直接呼ばない
        // （SpriteRenderer.size 変更が内部SendMessageを呼び、Unity 6では警告になる）
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            QueueApplyInEditMode();
#endif
        }
    }

    private void Update()
    {
        // Play中は毎フレーム追従、Edit中もExecuteAlwaysで動くが、軽量チェックで早期returnする
        Apply(force: false);
    }

#if UNITY_EDITOR
    private void QueueApplyInEditMode()
    {
        if (_editApplyQueued) return;
        _editApplyQueued = true;

        // Editorの安全なタイミングで1回だけ実行
        EditorApplication.delayCall += () =>
        {
            _editApplyQueued = false;

            if (this == null) return;              // オブジェクト破棄済み
            if (Application.isPlaying) return;     // Playに入った直後などは二重実行しない

            Apply(force: true);
        };
    }
#endif

    private void Apply(bool force)
    {
        if (targetCamera == null) return;
        if (!targetCamera.orthographic) return;

        if (wallTop == null || wallBottom == null || wallLeft == null || wallRight == null) return;

        Vector2 size = new Vector2(Screen.width, Screen.height);
        float aspect = targetCamera.aspect;
        float ortho = targetCamera.orthographicSize;
        Vector3 camPos = targetCamera.transform.position;

        if (!force &&
            size == _lastSize &&
            Mathf.Approximately(aspect, _lastAspect) &&
            Mathf.Approximately(ortho, _lastOrthoSize) &&
            camPos == _lastCamPos)
        {
            return;
        }

        _lastSize = size;
        _lastAspect = aspect;
        _lastOrthoSize = ortho;
        _lastCamPos = camPos;

        float halfH = ortho;
        float halfW = halfH * aspect;

        float leftX = camPos.x - halfW;
        float rightX = camPos.x + halfW;
        float bottomY = camPos.y - halfH;
        float topY = camPos.y + halfH;

        float halfT = thickness * 0.5f;

        // 横壁（Top/Bottom）
        float horizontalLength = (halfW * 2f) + lengthMargin;

        float topPosY;
        float bottomPosY;

        if (placement == PlacementMode.Inside)
        {
            topPosY = topY - halfT - placementMargin;
            bottomPosY = bottomY + halfT + placementMargin;
        }
        else
        {
            topPosY = topY + halfT + placementMargin;
            bottomPosY = bottomY - halfT - placementMargin;
        }

        SetWall(
            wallTop,
            new Vector2(camPos.x, topPosY),
            new Vector2(horizontalLength, thickness),
            syncSpriteRendererSizeToCollider
        );

        SetWall(
            wallBottom,
            new Vector2(camPos.x, bottomPosY),
            new Vector2(horizontalLength, thickness),
            syncSpriteRendererSizeToCollider
        );

        // 縦壁（Left/Right）
        float verticalLength = (halfH * 2f) + lengthMargin;

        float leftPosX;
        float rightPosX;

        if (placement == PlacementMode.Inside)
        {
            leftPosX = leftX + halfT + placementMargin;
            rightPosX = rightX - halfT - placementMargin;
        }
        else
        {
            leftPosX = leftX - halfT - placementMargin;
            rightPosX = rightX + halfT + placementMargin;
        }

        SetWall(
            wallLeft,
            new Vector2(leftPosX, camPos.y),
            new Vector2(thickness, verticalLength),
            syncSpriteRendererSizeToCollider
        );

        SetWall(
            wallRight,
            new Vector2(rightPosX, camPos.y),
            new Vector2(thickness, verticalLength),
            syncSpriteRendererSizeToCollider
        );
    }

    private static void SetWall(Transform wall, Vector2 worldPos, Vector2 size, bool syncSpriteSize)
    {
        if (wall == null) return;

        wall.position = new Vector3(worldPos.x, worldPos.y, wall.position.z);

        BoxCollider2D col = wall.GetComponent<BoxCollider2D>();
        if (col == null) return;

        col.size = size;
        col.offset = Vector2.zero;
        col.isTrigger = false;

        if (syncSpriteSize)
        {
            SpriteRenderer sr = wall.GetComponent<SpriteRenderer>();
            if (sr != null && sr.drawMode == SpriteDrawMode.Tiled)
            {
                // ★OnValidate中に呼ばれないようにしたので、ここで size を変えても警告にならない
                sr.size = size;
            }
        }
    }
}
