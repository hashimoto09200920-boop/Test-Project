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

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        startPhase = useRandomStartPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    private void Update()
    {
        float t = Time.time + startPhase;

        // 上下浮遊（差分ベース: EnemySpriteShakeと競合しない）
        transform.localPosition -= floatOffset;
        float newY = Mathf.Sin(t * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
        floatOffset = new Vector3(0f, newY, 0f);
        transform.localPosition += floatOffset;

        // 透明度パルス
        if (spriteRenderer != null)
        {
            float alpha = Mathf.Lerp(alphaMin, alphaMax,
                (Mathf.Sin(t * alphaFrequency * Mathf.PI * 2f) + 1f) * 0.5f);
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
