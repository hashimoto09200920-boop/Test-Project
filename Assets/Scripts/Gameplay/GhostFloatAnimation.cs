using UnityEngine;

/// <summary>
/// Ghostの浮遊アニメーションコンポーネント。
/// 上下浮遊・透明度パルス・緩やかな傾きをsin波で表現。
/// EnemySpriteShake（localPosition差分ベース）と競合しない設計。
/// Play前のInspectorで全パラメータ調整可能。
/// </summary>
public class GhostFloatAnimation : MonoBehaviour
{
    [Header("Float（上下浮遊）")]
    [Tooltip("上下の移動幅（ワールド単位）。例: 0.3 = ±0.3動く")]
    [Range(0f, 2f)]
    public float floatAmplitude = 0.3f;

    [Tooltip("浮遊の速さ（1秒あたりの往復回数）")]
    [Range(0.1f, 5f)]
    public float floatFrequency = 0.8f;

    [Header("Sway（横揺れ）")]
    [Tooltip("左右の移動幅（ワールド単位）。0で無効")]
    [Range(0f, 2f)]
    public float swayAmplitude = 0.2f;

    [Tooltip("横揺れの速さ（1秒あたりの往復回数）")]
    [Range(0.1f, 5f)]
    public float swayFrequency = 0.5f;

    [Tooltip("横揺れの位相オフセット（0〜1）。縦と同期しないように調整")]
    [Range(0f, 1f)]
    public float swayPhaseOffset = 0.25f;

    [Header("Alpha（透明度パルス）")]
    [Tooltip("透明度の最小値（0.0=完全透明 〜 1.0=不透明）")]
    [Range(0f, 1f)]
    public float alphaMin = 0.6f;

    [Tooltip("透明度の最大値")]
    [Range(0f, 1f)]
    public float alphaMax = 1.0f;

    [Tooltip("透明度パルスの速さ（1秒あたりの往復回数）")]
    [Range(0.1f, 5f)]
    public float alphaFrequency = 0.5f;

    [Header("Rotation（緩やかな傾き）")]
    [Tooltip("傾きの最大角度（度）。例: 3 = ±3度揺れる")]
    [Range(0f, 30f)]
    public float rotationAmplitude = 3f;

    [Tooltip("傾きの速さ（1秒あたりの往復回数）")]
    [Range(0.1f, 5f)]
    public float rotationFrequency = 0.6f;

    [Header("Random Start Phase")]
    [Tooltip("ON: 開始位相をランダムにする（複数Ghostが同じ動きにならないように）")]
    public bool useRandomStartPhase = true;

    private SpriteRenderer spriteRenderer;
    private float startPhase;
    private Vector3 floatOffset = Vector3.zero;
    private float accumulatedTime;
    private float alphaTime;

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        startPhase = useRandomStartPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        accumulatedTime = startPhase;
        alphaTime = 1f / (4f * Mathf.Max(alphaFrequency, 0.01f)); // sin=1からスタート（alphaMax）
    }

    private void Update()
    {
        float dt = Time.deltaTime * GetTimeScale();
        accumulatedTime += dt;
        alphaTime += dt;
        float t = accumulatedTime;

        // 上下浮遊・横揺れ（差分ベース: EnemySpriteShakeと競合しない）
        transform.localPosition -= floatOffset;
        float newY = Mathf.Sin(t * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
        float newX = Mathf.Sin((t + swayPhaseOffset / Mathf.Max(swayFrequency, 0.01f)) * swayFrequency * Mathf.PI * 2f) * swayAmplitude;
        floatOffset = new Vector3(newX, newY, 0f);
        transform.localPosition += floatOffset;

        // 透明度パルス
        if (spriteRenderer != null)
        {
            float alpha = Mathf.Lerp(alphaMin, alphaMax,
                (Mathf.Sin(alphaTime * alphaFrequency * Mathf.PI * 2f) + 1f) * 0.5f);
            Color c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }

        // 緩やかな傾き
        float angle = Mathf.Sin(t * rotationFrequency * Mathf.PI * 2f) * rotationAmplitude;
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDestroy()
    {
        // 破棄時にlocalPositionの浮遊オフセットをリセット
        transform.localPosition -= floatOffset;
        floatOffset = Vector3.zero;
    }
}
