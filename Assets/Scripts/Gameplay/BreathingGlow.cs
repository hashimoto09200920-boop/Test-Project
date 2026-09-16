using UnityEngine;

// 背景装飾（月など）の明るさを、周期的にゆっくり呼吸するように明滅させる。
// SpriteRendererの色を直接書き換えるだけなので負荷はごくわずか。
[RequireComponent(typeof(SpriteRenderer))]
public class BreathingGlow : MonoBehaviour
{
    [Tooltip("明滅1周期の時間（秒）。暗い→明るい→暗いで1周期")]
    [SerializeField] private float periodSeconds = 4f;

    [Tooltip("最も暗い時の明るさ倍率（1で変化なし）")]
    [SerializeField] private float minBrightness = 0.55f;

    [Tooltip("最も明るい時の明るさ倍率（1で変化なし）")]
    [SerializeField] private float maxBrightness = 1.6f;

    [Tooltip("周期の開始位置をランダムにずらす（0-1）。複数の光る背景要素を同じ動きで明滅させたくない場合にON")]
    [SerializeField] private bool randomizePhase = false;

    private SpriteRenderer sr;
    private Color baseColor;
    private float phaseOffset;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        phaseOffset = randomizePhase ? Random.Range(0f, periodSeconds) : 0f;
    }

    private void Update()
    {
        float t = (Mathf.Sin((Time.time + phaseOffset) * (2f * Mathf.PI / periodSeconds)) + 1f) * 0.5f;
        float brightness = Mathf.Lerp(minBrightness, maxBrightness, t);
        sr.color = new Color(baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);
    }
}
