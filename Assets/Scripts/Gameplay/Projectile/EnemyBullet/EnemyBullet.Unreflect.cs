using UnityEngine;

public partial class EnemyBullet
{
    // A8スキルが基礎ダメージを最初に増やす直前の値（-1＝まだ増えていない）。未反射に戻す時にA8分を取り消すために使う
    private int damageBeforeA8 = -1;

    /// <summary>
    /// 未反射に戻った瞬間に発火（EnemyLineReflectorで跳ね返されて「敵の弾」に戻った時）
    /// </summary>
    public event System.Action OnRevertedToUnreflected;

    /// <summary>
    /// プレイヤーが反射させた弾を、未反射の弾（プレイヤー・Floorに当たり、プレイヤーの線でまた反射できる弾）に戻す。
    /// ★現在はArea10最終ボスNeonDancerの「敵の線」（EnemyLineReflector）からのみ呼ばれる。既存の処理からは呼ばれない。
    /// 戻すもの：レイヤー（ReflectedBullet→UnreflectedBullet）・反射済みフラグ・Just状態（ダメージ倍率・光る見た目）・
    ///           反射のTrail/Particle/Just矢じりVFX・反射のクールダウン・煙幕弾の反射済みフラグ・ミサイル軌道
    /// 残すもの：速度（Just反射等で加速した分はそのまま）・反射回数の残り
    /// ★A8（敵ヒットごとの基礎ダメージ加算）で増えた分は取り消し、加算回数も0に戻す
    /// </summary>
    public void RevertToUnreflected(Vector2 newDirection)
    {
        if (isBeingDestroyed) return;

        EnsureLayerCache();
        if (s_unreflectedLayer >= 0) gameObject.layer = s_unreflectedLayer;

        IsReflected = false;
        hasPaddleReflectedOnce = false;
        LastReflectedByStroke = null;

        // Just状態（ダメージ倍率と光る見た目）を戻す
        DamageMultiplier = 1f;
        c2PenetrationsRemaining = 0;
        if (flashCo != null) { StopCoroutine(flashCo); flashCo = null; }
        ApplyVisualByState();

        // 戻った直後にプレイヤーの線へ当たった時も、通常どおり反射処理が走るようにする
        lastPaddleReflectTime = -999f;
        reflectOverrideUntil = -999f;

        // 煙幕弾：もう一度プレイヤーの線で反射された時に煙を出せるようにする
        smokeGrenadeHasReflectedOnce = false;

        // A8で増えた基礎ダメージを取り消す（もう一度反射されたら、また0回目から加算される）
        if (a8EnemyHitCount > 0 && damageBeforeA8 >= 0) damageValue = damageBeforeA8;
        a8EnemyHitCount = 0;
        lastA8HitFrame = -999;
        damageBeforeA8 = -1;

        // ミサイル軌道が残っていると毎フレーム速度を上書きされるため止める
        ClearMissileArc();

        SetDirection(newDirection);

        if (feedback == null) feedback = GetComponent<EnemyBulletFeedback>();
        if (feedback != null) feedback.RevertToUnreflectedVisual();

        OnRevertedToUnreflected?.Invoke();
    }
}
