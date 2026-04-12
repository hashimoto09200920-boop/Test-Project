using UnityEngine;

/// <summary>
/// スプライトをZ軸で連続回転させるコンポーネント。
///
/// Block_Orbit など「軌道上を移動しながらスプライト自体も自転させたい」
/// オブジェクトにアタッチして使う。
///
/// ・位置制御（FortressEnemy.UpdateOrbitPositions）とは完全に独立している
/// ・BlockWobble（ゆらゆら）とも独立している
/// ・rotationSpeed が正 = 時計回り（Unity では Z の負方向が時計回り）
/// </summary>
public class SpriteRotator : MonoBehaviour
{
    [Tooltip("自転速度（度/秒）\n正の値 = 時計回り、負の値 = 反時計回り")]
    [SerializeField] private float rotationSpeed = 30f;

    private void Update()
    {
        // Z軸を時計回りに回転（Unityの座標系では負方向が時計回り）
        transform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
    }
}
