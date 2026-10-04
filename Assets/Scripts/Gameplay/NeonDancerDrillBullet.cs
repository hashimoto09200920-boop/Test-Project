using UnityEngine;

/// <summary>
/// NeonDancerの⑨Drillにだけ発射時に付ける部品（NeonDancer専用仕様）。
/// 反射していないドリルがプレイヤー側のダンサー/床に当たったら、必ず消える（跳ね返らない）ようにする。
///
/// ★共有のPinnedReflectBullet.TryPinToEnemyは、当たった瞬間に規定ヒット数へ到達すると（残りヒット数がちょうど1）
///   留まらず「通常の1回分のダメージ」を呼び出し元に任せるが、ドリルはEnemyBullet.TryHandlePlayerOrFloorHitで
///   消えない扱いのため、ダメージだけ入って物理的に跳ね返ってしまう（線で3ヒット溜めてから線が消えた時に起きる）。
///   共有コードは改修せず、当たったフレームの最後（LateUpdate）に「留まっていなければ消す」で対応する。
///   ダメージはダンサー/床側の衝突処理が同じフレームで適用済み。
/// プール再利用される弾に残らないよう、消える時に自分自身を外す。
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerDrillBullet : MonoBehaviour
{
    private EnemyBullet bullet;
    private PinnedReflectBullet pinned;
    private bool armed;
    private bool touchedPlayerSideThisFrame;

    public void Arm(EnemyBullet b)
    {
        bullet = b;
        pinned = b.CachedPinnedReflect;
        armed = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null) CheckTouch(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckTouch(other);
    }

    private void CheckTouch(Collider2D c)
    {
        if (!armed || c == null || bullet == null) return;
        if (bullet.HasPaddleReflectedOnce) return; // 反射済みの弾はダンサー/床に当たらない（既存仕様）
        // 共有のTryHandlePlayerOrFloorHitと同じ判定（GetComponentInParentは使わない）
        if (c.GetComponent<PixelDancerController>() == null && c.GetComponent<FloorHealth>() == null) return;
        touchedPlayerSideThisFrame = true;
    }

    private void LateUpdate()
    {
        if (!touchedPlayerSideThisFrame) return;
        touchedPlayerSideThisFrame = false;
        if (!armed || bullet == null || !bullet.gameObject.activeInHierarchy) return;

        // 留まって多段ヒット中なら、規定回数に達した時にPinnedReflectBullet側が消すので何もしない
        if (pinned != null && pinned.IsPinned) return;

        // 留まらなかった（最後の1ヒットだった）：跳ね返らずに消える
        bullet.PlayDestroyFeedbackAndDestroy();
    }

    private void OnDisable()
    {
        armed = false;
        Destroy(this);
    }
}
