using UnityEngine;

// =========================================================
// 汎用：Beam（EnemyBeamBullet）がこのコンポーネント付きのCollider2Dに当たった時、
// ダメージを与えず・受けず、物理的な壁として反射させるためのマーカー。
// PaddleDotへの反射と同じ扱い（Vector2.Reflect）で、EnemyBeamBullet.BuildChainFromから参照される。
// Obelisk本体のような「反射専用の無敵な壁」に使う想定（WallHealth/EnemyPartとは独立）。
// =========================================================
[RequireComponent(typeof(Collider2D))]
public class BeamReflector : MonoBehaviour
{
    [Tooltip("反射時のスパークVFX（任意・未設定なら何も出ない）")]
    [SerializeField] private GameObject reflectVfxPrefab;
    [SerializeField] private float reflectVfxDestroySeconds = 0.35f;

    public void PlayReflectVfx(Vector3 point)
    {
        if (reflectVfxPrefab == null) return;
        GameObject vfx = Instantiate(reflectVfxPrefab, point, Quaternion.identity);
        if (reflectVfxDestroySeconds > 0f) Destroy(vfx, reflectVfxDestroySeconds);
    }
}
