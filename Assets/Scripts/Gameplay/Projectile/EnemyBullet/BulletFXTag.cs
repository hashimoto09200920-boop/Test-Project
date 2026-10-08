using UnityEngine;

/// <summary>
/// BulletFXManagerに登録された弾に1つだけ付ける目印（プールで使い回される弾に付けたまま再利用する）。
/// 弾が消えた（プールに戻った）瞬間に管理役へ知らせ、変えた見た目を元に戻してもらう。
/// </summary>
[DisallowMultipleComponent]
public class BulletFXTag : MonoBehaviour
{
    [System.NonSerialized] public EnemyBullet bullet;

    private void OnDisable()
    {
        var m = BulletFXManager.Existing;
        if (m != null && bullet != null) m.OnBulletDisabled(bullet);
    }
}
