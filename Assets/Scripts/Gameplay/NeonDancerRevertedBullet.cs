using UnityEngine;

/// <summary>
/// NeonDancerの「敵の線」で跳ね返されて未反射に戻った弾の見た目（NeonDancer専用）。
/// 寝返った弾だとひと目で分かるよう色とTrailを変え、プレイヤーがもう一度反射したら元の色に戻す。
/// 弾が消えた（プールへ戻った）時に自分自身を外す。
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerRevertedBullet : MonoBehaviour
{
    private EnemyBullet bullet;
    private SpriteRenderer visual;
    private Color originalColor = Color.white;

    public static void Apply(EnemyBullet b, Color tint, Color trailColor, float trailTime, float trailWidth)
    {
        if (b == null) return;
        var marker = b.GetComponent<NeonDancerRevertedBullet>();
        if (marker == null)
        {
            marker = b.gameObject.AddComponent<NeonDancerRevertedBullet>();
            marker.Init(b);
        }
        b.SetVisualColor(tint);
        b.SetUnreflectedTrail(trailColor, trailTime, trailWidth, 0f);
    }

    private void Init(EnemyBullet b)
    {
        bullet = b;
        Transform v = b.transform.Find("Visual");
        visual = v != null ? v.GetComponent<SpriteRenderer>() : b.GetComponentInChildren<SpriteRenderer>();
        if (visual != null) originalColor = visual.color;
        b.OnReflected += RestoreColor;
    }

    // プレイヤーの線で再び反射されたら元の色に戻す（また敵の線で戻されたら再度色を付ける）
    private void RestoreColor()
    {
        if (bullet != null) bullet.SetVisualColor(originalColor);
    }

    private void OnDisable()
    {
        if (bullet != null) bullet.OnReflected -= RestoreColor;
        Destroy(this);
    }
}
