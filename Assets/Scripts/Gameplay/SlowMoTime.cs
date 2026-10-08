using System.Collections;
using UnityEngine;

/// <summary>
/// スローモーション（SlowMotionManager.TimeScale。Time.timeScaleは変えない独自方式）に合わせるための補助。
/// ・Scale：今の時間の倍率（スローモーション中は小さくなる）
/// ・Wait：スローモーションに合わせて待つ（WaitForSecondsの代わり。実時間ではなくゲーム内の時間で待つ）
/// ・ParticleSpeed：Particle Systemの再生速度をスローモーションに合わせる（倍率が変わった時だけ設定する）
/// </summary>
public static class SlowMoTime
{
    public static float Scale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    /// <summary>スローモーションに合わせて進む1フレームの時間</summary>
    public static float DeltaTime => Time.deltaTime * Scale;

    public static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += DeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Particle Systemの再生速度をスローモーションに合わせる。lastAppliedは呼び出し側で保持する
    /// （倍率が変わったフレームだけ設定する。初期値はfloat.NaNにしておくこと）
    /// </summary>
    public static void ParticleSpeed(ParticleSystem[] systems, ref float lastApplied)
    {
        float s = Scale;
        if (s == lastApplied || systems == null) return;
        lastApplied = s;
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.simulationSpeed = s;
        }
    }
}
