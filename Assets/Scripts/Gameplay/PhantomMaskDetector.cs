using UnityEngine;

/// <summary>
/// MaskHitZone 子オブジェクトにアタッチ。
/// 仮面エリアへの反射弾ヒットを検出し、ブロックダメージ計算で PhantomController へ通知する。
/// </summary>
public class PhantomMaskDetector : MonoBehaviour
{
    [Tooltip("ダメージの最短間隔（秒）。多重ヒット防止")]
    [SerializeField] float hitMinInterval = 0.1f;

    private PhantomController _controller;
    private float _lastHitTime = -999f;

    private void Awake()
    {
        _controller = GetComponentInParent<PhantomController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_controller == null) return;

        EnemyBullet bullet = other.GetComponent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;

        if (Time.time - _lastHitTime < hitMinInterval) return;
        _lastHitTime = Time.time;

        // ブロックダメージ計算（EnemyShield.ApplyDamage と同方式）
        float raw       = bullet.DamageValue * Mathf.Max(1f, bullet.DamageMultiplier);
        float shieldMul = Game.Skills.SkillManager.Instance != null
            ? Game.Skills.SkillManager.Instance.GetShieldDamageMultiplier()
            : 1f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(raw * shieldMul));

        _controller.OnMaskHit(damage);
        bullet.RegisterEnemyHitAsBounce();

        // 弾を消さず速度を反射させる
        var bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
        {
            Vector2 normal = ((Vector2)bullet.transform.position - (Vector2)transform.position).normalized;
            bulletRb.linearVelocity = Vector2.Reflect(bulletRb.linearVelocity, normal);
        }
    }
}
