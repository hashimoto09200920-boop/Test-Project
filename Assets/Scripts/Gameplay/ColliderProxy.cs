using UnityEngine;

/// <summary>
/// 子オブジェクトのCollider2DでキャッチしたCollision/Triggerイベントを
/// 親オブジェクトのWallHealthに転送するプロキシ。
///
/// 使い方：
///   ルートに WallHealth を置き、当たり判定用の子オブジェクト（ColliderObj）に
///   このスクリプトとCollider2Dを付ける。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ColliderProxy : MonoBehaviour
{
    private WallHealth wallHealth;

    private void Awake()
    {
        wallHealth = GetComponentInParent<WallHealth>();
        if (wallHealth == null)
            Debug.LogWarning("[ColliderProxy] 親にWallHealthが見つかりません。", this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (wallHealth == null) return;
        wallHealth.OnChildCollisionEnter2D(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (wallHealth == null) return;
        wallHealth.OnChildTriggerEnter2D(other);
    }
}
