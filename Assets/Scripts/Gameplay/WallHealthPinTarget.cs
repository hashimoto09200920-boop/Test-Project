using UnityEngine;

/// <summary>
/// WallHealth（ブロック）にドリル弾（PinnedReflectBullet）が留まっている間の「留まり先」の目印。
/// PinnedReflectBulletは留まり先が破棄されると留まるのをやめて元の向きへ直進を再開する。
/// ブロックが壊れても（見た目を残すブロックでは）WallHealth自体は破棄されないため、
/// 壊れた瞬間にこの目印だけを破棄して、ドリル弾に「留まり先が無くなった」ことを伝える。
/// WallHealthが必要な時に自動で付ける（手動で付ける必要は無い）。
/// </summary>
[DisallowMultipleComponent]
public class WallHealthPinTarget : MonoBehaviour
{
}
