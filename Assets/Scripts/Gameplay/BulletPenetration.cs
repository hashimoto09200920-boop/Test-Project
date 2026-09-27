using UnityEngine;

[DisallowMultipleComponent]
public class BulletPenetration : MonoBehaviour
{
    [Header("Penetration")]
    [Tooltip("弾の貫通値。線の硬度より大きい時だけ線を破壊して直進する。")]
    [SerializeField] private int penetration = 1;

    [Header("Runtime")]
    [Tooltip("衝突直前の速度（直進維持用）。")]
    [SerializeField] private Vector2 lastVelocity;

    [Header("Runtime (Penetrate Lock)")]
    [Tooltip("この弾が最後にPENETRATE（線破壊）を起こしたフレーム。")]
    [SerializeField] private int lastPenetrateFrame = -999999;

    private Rigidbody2D rb;

    private int lastConsumeFrame = -999999;

    // ★プーリング再利用時の復元用：プレハブ本来の貫通値（Awake()で一度だけキャッシュ）
    private int originalPenetration;

    public int Penetration => penetration;
    public Vector2 LastVelocity => lastVelocity;

    public bool WasPenetratedThisFrame => (Time.frameCount == lastPenetrateFrame);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalPenetration = penetration;
    }

    private void OnEnable()
    {
        lastPenetrateFrame = -999999;
        lastConsumeFrame = -999999;
        // ★EnemyShooter.ApplyBulletTypeToEnemyBulletのSetPenetration呼び出しは弾種側で
        //   上書き指定がある時だけ条件付きで実行される。ここで一度プレハブ本来の値へ戻して
        //   おかないと、前回の生涯で消費された値がプーリング再利用時に持ち越されてしまう。
        penetration = originalPenetration;
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            lastVelocity = rb.linearVelocity;
        }
    }

    public void MarkPenetratedThisFrame()
    {
        lastPenetrateFrame = Time.frameCount;
    }

    public void ConsumeOnBreakOnce()
    {
        if (penetration <= 0) return;

        int f = Time.frameCount;
        if (f == lastConsumeFrame) return;

        lastConsumeFrame = f;
        penetration -= 1;
        if (penetration < 0) penetration = 0;
    }

    public void RestorePreCollisionVelocity()
    {
        if (rb == null) return;

        rb.linearVelocity = lastVelocity;
        rb.angularVelocity = 0f;
    }

    // =========================================================
    // ★追加：外部から penetration を上書き
    // =========================================================
    public void SetPenetration(int value)
    {
        penetration = Mathf.Max(0, value);
    }
}
