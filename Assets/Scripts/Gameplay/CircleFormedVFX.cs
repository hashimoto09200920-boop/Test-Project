using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleFormedVFX : MonoBehaviour
{
    // =============================================
    // A: Outline Trace
    // =============================================
    [Header("A: Outline Trace（輪郭トレース）")]
    [Tooltip("輪郭に沿って順番にemitするParticleSystem。")]
    [SerializeField] private ParticleSystem traceParticles;

    [Tooltip("トレース全体の所要時間（ミリ秒）。dot数に関係なくこの時間で一周する。")]
    [SerializeField, Range(10, 2000)] private int traceDurationMs = 100;

    [Tooltip("トレース粒子のサイズ。線dotより大きくすると光が走るように見える。")]
    [SerializeField] private float traceParticleSize = 0.25f;

    [Tooltip("トレース粒子の色をWhiteに向けてどれだけ明るくするか（0=ストローク色そのまま、1=白）。")]
    [SerializeField, Range(0f, 1f)] private float traceBrightness = 0.6f;

    // =============================================
    // C: Convergence
    // =============================================
    [Header("C: Convergence（収束）")]
    [Tooltip("円の領域から中心へ向かって飛ぶParticleSystem。")]
    [SerializeField] private ParticleSystem convergeParticles;

    [Tooltip("中心へ向かう速度（単位/秒）。大きいほど素早く収束する。")]
    [SerializeField] private float convergeSpeed = 4f;

    [Tooltip("1 dotあたりに出す粒子数。多いほど密度が上がる。")]
    [SerializeField, Range(1, 10)] private int particlesPerPoint = 3;

    [Tooltip("emit位置をランダムにずらす半径（ワールド単位）。大きいほど円の輪郭形状が目立たなくなる。")]
    [SerializeField] private float positionSpread = 0.08f;

    [Tooltip("速度をランダムにずらす量（ワールド単位/秒）。0なら純粋に中心へ直進。大きいほどバラバラに飛ぶ。")]
    [SerializeField] private float velocitySpread = 0.5f;

    [Tooltip("収束粒子のサイズ。小さめ推奨（0.03〜0.08）。")]
    [SerializeField] private float convergeParticleSize = 0.05f;

    // =============================================
    // Runtime state
    // =============================================
    private Color cachedStrokeColor = Color.white;

    // =============================================
    // Public API
    // =============================================
    public void Play(List<Vector3> dotPositions, Vector3 center, Color strokeColor)
    {
        if (dotPositions == null || dotPositions.Count == 0) return;
        cachedStrokeColor = strokeColor;

        if (convergeParticles != null)
            EmitConverge(dotPositions, center);

        if (traceParticles != null)
            StartCoroutine(EmitTrace(dotPositions));
    }

    // =============================================
    // Converge
    // =============================================
    private void EmitConverge(List<Vector3> positions, Vector3 center)
    {
        var ep = new ParticleSystem.EmitParams();
        ep.startSize = convergeParticleSize;
        ep.startColor = cachedStrokeColor;

        foreach (var pos in positions)
        {
            for (int i = 0; i < particlesPerPoint; i++)
            {
                Vector3 emitPos = pos + (Vector3)(Random.insideUnitCircle * positionSpread);
                float dist = Vector3.Distance(emitPos, center);
                if (dist < 0.001f) continue;

                Vector3 baseDir = (center - emitPos).normalized;
                Vector3 jitter = (Vector3)(Random.insideUnitCircle * velocitySpread);
                ep.position = emitPos;
                ep.velocity = baseDir * convergeSpeed + jitter;
                ep.startLifetime = dist / convergeSpeed;
                convergeParticles.Emit(ep, 1);
            }
        }
    }

    // =============================================
    // Trace（サブフレーム精度：WaitForSecondsより正確）
    // =============================================
    private IEnumerator EmitTrace(List<Vector3> positions)
    {
        if (positions.Count == 0) yield break;

        float totalSec = traceDurationMs * 0.001f;
        float intervalSec = totalSec / positions.Count;
        float elapsed = 0f;
        int emitted = 0;

        var ep = new ParticleSystem.EmitParams();
        ep.startSize = traceParticleSize;
        ep.startColor = Color.Lerp(cachedStrokeColor, Color.white, traceBrightness);

        while (emitted < positions.Count)
        {
            int target = Mathf.Min(Mathf.FloorToInt(elapsed / intervalSec), positions.Count);
            while (emitted < target)
            {
                ep.position = positions[emitted];
                traceParticles.Emit(ep, 1);
                emitted++;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
