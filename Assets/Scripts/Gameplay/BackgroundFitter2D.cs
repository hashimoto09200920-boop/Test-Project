using UnityEngine;

/// <summary>
/// 2D背景（SpriteRenderer）を Orthographic Camera の表示範囲に自動フィットさせる。
/// - Unity 6
/// - 背景は Scene内の Background GameObject に付ける想定
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BackgroundFitter2D : MonoBehaviour
{
    public enum FitMode
    {
        Cover, // 画面全体を覆う（はみ出しOK、隙間なし）
        Contain // 全体を収める（隙間が出る場合あり）
    }

    [Header("Target Camera (空なら Main Camera)")]
    [SerializeField] private Camera targetCamera;

    [Header("Fit")]
    [SerializeField] private FitMode fitMode = FitMode.Cover;

    [Tooltip("背景をさらに拡大したい場合の倍率（1=そのまま）")]
    [Min(0.01f)]
    [SerializeField] private float extraScale = 1f;

    [Tooltip("Z位置を固定したい場合に使用（true推奨）")]
    [SerializeField] private bool lockZ = true;

    [SerializeField] private float lockedZ = 0f;

    private SpriteRenderer sr;

    private Vector2 _lastScreenSize;
    private float _lastAspect;
    private float _lastOrthoSize;
    private Sprite _lastNoteSprite;
    private float _lastExtraScale;
    private FitMode _lastFitMode;

    private void Reset()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        Apply(force: true);
    }

    private void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        Apply(force: true);
    }

    private void Update()
    {
        Apply(force: false);
    }

    private void Apply(bool force)
    {
        if (sr == null) return;
        if (targetCamera == null) return;
        if (!targetCamera.orthographic) return;

        if (sr.sprite == null) return;

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        float aspect = targetCamera.aspect;
        float ortho = targetCamera.orthographicSize;

        if (!force &&
            screenSize == _lastScreenSize &&
            Mathf.Approximately(aspect, _lastAspect) &&
            Mathf.Approximately(ortho, _lastOrthoSize) &&
            sr.sprite == _lastNoteSprite &&
            Mathf.Approximately(extraScale, _lastExtraScale) &&
            fitMode == _lastFitMode)
        {
            return;
        }

        _lastScreenSize = screenSize;
        _lastAspect = aspect;
        _lastOrthoSize = ortho;
        _lastNoteSprite = sr.sprite;
        _lastExtraScale = extraScale;
        _lastFitMode = fitMode;

        // カメラ表示範囲（ワールド座標）
        float worldH = ortho * 2f;
        float worldW = worldH * aspect;

        // Sprite のワールドサイズ（pixelsPerUnit と sprite.rect を反映）
        Vector2 spriteWorldSize = sr.sprite.bounds.size;
        if (spriteWorldSize.x <= 0.0001f || spriteWorldSize.y <= 0.0001f) return;

        float sx = worldW / spriteWorldSize.x;
        float sy = worldH / spriteWorldSize.y;

        float s;
        if (fitMode == FitMode.Cover)
        {
            s = Mathf.Max(sx, sy);
        }
        else
        {
            s = Mathf.Min(sx, sy);
        }

        s *= extraScale;

        transform.localScale = new Vector3(s, s, transform.localScale.z);

        if (lockZ)
        {
            Vector3 p = transform.position;
            p.z = lockedZ;
            transform.position = p;
        }
    }
}
