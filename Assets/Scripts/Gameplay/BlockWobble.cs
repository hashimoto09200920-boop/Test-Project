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

    private void Awake()
    {
        // 生成時点の Z 回転を基準角として記録（FortressEnemy が設定した角度を保持）
        baseRotationZ = transform.eulerAngles.z;

        // インスタンスごとに位相をランダムにずらして非同期揺れを実現
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float wobble = Mathf.Sin(Time.time * speed + phaseOffset) * amplitude;
        transform.rotation = Quaternion.Euler(0f, 0f, baseRotationZ + wobble);
    }
}
