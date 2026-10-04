using UnityEngine;

/// <summary>
/// NeonDancerの後半⑨強化ドリル弾に付ける目印（NeonDancer専用）。
/// 弾はプールで使い回されるため、「最後に強化した弾が表示中か」だけで判定すると、次に同じ弾が
/// 普通のドリルとして使い回された時も「強化弾が生きている」と誤判定してしまう。
/// この目印は弾が消えた（プールへ戻って非アクティブになった）瞬間に自分自身を外すので、
/// 目印が付いている間だけを「強化弾が画面上にいる」とみなせる。
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerEnhancedDrillMarker : MonoBehaviour
{
    private void OnDisable()
    {
        Destroy(this);
    }
}
