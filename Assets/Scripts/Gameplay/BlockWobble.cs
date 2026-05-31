using UnityEngine;

/// <summary>
/// オブジェクトをその場でゆらゆらさせるコンポーネント。
/// Block_Scatter など「移動はしないが揺れる」ブロックにアタッチして使う。
///
/// 揺れはZ軸回転のサイン波。初期Rotationを基準に往復する。
/// 各インスタンスは Awake でランダムな位相オフセットを持つため、
/// 複数配置しても全員が同じタイミングで揺れることがない。
/// </summary>
public class BlockWobble : MonoBehaviour
{
    [Tooltip("ゆらゆらの最大振れ角度（度）\n例: 8 → 初期角度を中心に±8度揺れる")]
    [Range(0f, 45f)]
    [SerializeField] private float amplitude = 8f;

    [Tooltip("ゆらゆらの速さ（大きいほど速い）")]
    [Range(0.1f, 10f)]
    [SerializeField] private float speed = 1.2f;

    private float baseRotationZ;
    private float phaseOffset;

    private Vector3 basePosition;
    private float floatAmplitudeX;
    private float floatSpeedX;
    private float floatAmplitudeY;
    private float floatSpeedY;
    private float phaseOffsetX;
    private float phaseOffsetY;

    private void Awake()
    {
        baseRotationZ = transform.eulerAngles.z;
        phaseOffset   = Random.Range(0f, Mathf.PI * 2f);
    }

    /// <summary>StageBlockSpawnerからAreaConfig設定値を受け取る</summary>
    public void SetFloatParams(float ampX, float speedX, float ampY, float speedY)
    {
        basePosition     = transform.position;
        floatAmplitudeX  = ampX;
        floatSpeedX      = speedX;
        floatAmplitudeY  = ampY;
        floatSpeedY      = speedY;
        phaseOffsetX     = Random.Range(0f, Mathf.PI * 2f);
        phaseOffsetY     = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float wobble = Mathf.Sin(Time.time * speed + phaseOffset) * amplitude;
        transform.rotation = Quaternion.Euler(0f, 0f, baseRotationZ + wobble);

        if (floatAmplitudeX > 0f || floatAmplitudeY > 0f)
        {
            float dx = Mathf.Sin(Time.time * floatSpeedX + phaseOffsetX) * floatAmplitudeX;
            float dy = Mathf.Sin(Time.time * floatSpeedY + phaseOffsetY) * floatAmplitudeY;
            transform.position = basePosition + new Vector3(dx, dy, 0f);
        }
    }
}
