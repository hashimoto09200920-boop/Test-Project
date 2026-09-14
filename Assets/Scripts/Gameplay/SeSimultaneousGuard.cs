using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「ほぼ同時（同一フレーム）」に何度も鳴ろうとするSEを、1フレームにつき1回だけに制限する共通ガード。
/// 弾を大量に同時反射/同時消滅させた時に、同じ種類のSEが何十個も重なって音が割れる/うるさくなる問題への対策。
/// Time.time基準の間隔制限（例: paddleHitMinInterval）と違い、1フレームでもズレていれば必ず許可するため、
/// 複数の弾を意図的に少しずらして当てた場合はそれぞれきちんと鳴る。
/// </summary>
public static class SeSimultaneousGuard
{
    private static readonly Dictionary<string, int> lastPlayFrame = new Dictionary<string, int>();

    /// <summary>
    /// 指定したキーのSEが同一フレーム内で既に許可済みなら false（再生をスキップすべき）を返す。
    /// 初回、または前回許可したフレームと異なる場合はtrueを返し、以後同フレーム内の呼び出しをブロックする。
    /// </summary>
    public static bool TryAllow(string key)
    {
        int frame = Time.frameCount;
        if (lastPlayFrame.TryGetValue(key, out int last) && last == frame) return false;
        lastPlayFrame[key] = frame;
        return true;
    }
}
