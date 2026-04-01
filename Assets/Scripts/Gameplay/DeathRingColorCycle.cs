using UnityEngine;

/// <summary>
/// SpriteRendererの色を指定HSV範囲内でサイクルさせる。
/// VFX_Explosion_A_RingSparksのRingオブジェクトにアタッチ。
/// LateUpdateで処理するのでAnimatorのスケール/alpha制御と干渉しない。
/// minHue〜maxHue の範囲内でループするので爆発色（赤・橙・黄）に限定できる。
/// </summary>
public class DeathRingColorCycle : MonoBehaviour
{
    [Tooltip("1秒あたりのHue変化量（範囲内で往復）")]
    [SerializeField] private float cycleSpeed = 4f;

    [Tooltip("Hueの下限（0=赤）")]
    [Range(0f, 1f)]
    [SerializeField] private float minHue = 0f;

    [Tooltip("Hueの上限（0.17=黄、0.08=橙）")]
    [Range(0f, 1f)]
    [SerializeField] private float maxHue = 0.14f;

    [Tooltip("彩度（1=鮮やか）")]
    [Range(0f, 1f)]
    [SerializeField] private float saturation = 1f;

    [Tooltip("明度（1=最大輝度）")]
    [Range(0f, 1f)]
    [SerializeField] private float brightness = 1f;

    private SpriteRenderer sr;
    private float hue;
    private float direction = 1f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        hue = minHue;
    }

    /// <summary>DeathVFXSettings から実行時に設定を上書きする</summary>
    public void SetConfig(float newMinHue, float newMaxHue, float newCycleSpeed)
    {
        minHue = newMinHue;
        maxHue = newMaxHue;
        cycleSpeed = newCycleSpeed;
        hue = minHue;
        direction = 1f;
    }

    private void LateUpdate()
    {
        if (sr == null) return;

        float range = maxHue - minHue;
        if (range <= 0f) return;

        hue += Time.deltaTime * cycleSpeed * direction;

        // 範囲端で折り返す
        if (hue >= maxHue) { hue = maxHue; direction = -1f; }
        else if (hue <= minHue) { hue = minHue; direction = 1f; }

        Color c = Color.HSVToRGB(hue, saturation, brightness);
        // alphaはAnimatorが制御している可能性があるため元の値を維持
        sr.color = new Color(c.r, c.g, c.b, sr.color.a);
    }
}
