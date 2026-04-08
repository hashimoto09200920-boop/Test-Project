using System.Collections;
using UnityEngine;

/// <summary>
/// 長押し赤線切り替えVFX。
/// パレットからランダムに色を割り当てて各パーティクルを着色する。
/// </summary>
public class LongPressActivateVFX : MonoBehaviour
{
    private Color[] palette;
    private ParticleSystem ps;

    public void SetPalette(Color[] colors)
    {
        palette = colors;
    }

    private void Start()
    {
        ps = GetComponent<ParticleSystem>();
        if (palette != null && palette.Length > 0)
            StartCoroutine(ApplyColorsNextFrame());
    }

    // バースト放出後の1フレーム待ってから各パーティクルに色を適用
    private IEnumerator ApplyColorsNextFrame()
    {
        yield return null;

        if (ps == null || palette == null || palette.Length == 0) yield break;

        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[ps.particleCount];
        int count = ps.GetParticles(particles);

        for (int i = 0; i < count; i++)
        {
            Color c = palette[Random.Range(0, palette.Length)];
            particles[i].startColor = c;
        }

        ps.SetParticles(particles, count);
    }
}
