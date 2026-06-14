using UnityEngine;

public class PlayerStringSegmentTrigger : MonoBehaviour
{
    [HideInInspector] public PlayerStringController owner;
    [HideInInspector] public int segmentIndex;
    [HideInInspector] public int totalSegments;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var bullet = other.GetComponent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected || owner == null) return;
        var rb = other.GetComponent<Rigidbody2D>();
        float t = totalSegments > 1 ? (float)segmentIndex / (totalSegments - 1) : 0.5f;
        owner.OnStringHit(bullet, rb, transform.up, transform.position, t);
    }
}
