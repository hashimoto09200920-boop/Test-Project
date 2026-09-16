using UnityEngine;

// 月などの背景装飾に、心臓の鼓動のような「ドクン、ドクン……（間）」というリズムで
// 明るさ・わずかな拡大縮小を同時に起こす「鳴動」演出。
// 滑らかな正弦波ではなく、二拍＋休止という不均等なリズムにすることで脈動らしさを出す。
// SpriteRenderer.colorとtransform.localScaleの両方を扱うが、他のコンポーネントと
// プロパティを取り合わないよう、この2つの更新はこのスクリプト1つに集約している。
[RequireComponent(typeof(SpriteRenderer))]
public class HeartbeatPulse : MonoBehaviour
{
    [Tooltip("1サイクル（ドクン、ドクン→休止）の合計時間（秒）")]
    [SerializeField] private float cycleDuration = 6f;

    [Tooltip("1拍目が鳴るタイミング（サイクル開始からの秒数）")]
    [SerializeField] private float firstBeatTime = 0f;

    [Tooltip("2拍目が鳴るタイミング（サイクル開始からの秒数）")]
    [SerializeField] private float secondBeatTime = 0.9f;

    [Tooltip("拍の鋭さ（小さいほど鋭く短いパルスになる。大きいほどなだらかで滑らかになる）")]
    [SerializeField] private float beatSharpness = 0.35f;

    [Tooltip("平常時（拍と拍の間）の明るさ倍率")]
    [SerializeField] private float restBrightness = 0.85f;

    [Tooltip("拍のピーク時の明るさ倍率")]
    [SerializeField] private float peakBrightness = 1.15f;

    [Tooltip("拍のピーク時のスケール倍率（1で拡大縮小なし）")]
    [SerializeField] private float peakScale = 1.08f;

    // 現在のパルス強度（0=平常、1=ピーク）。他の演出が
    // 同じリズムに同期するための公開値
    public float CurrentPulse { get; private set; }

    private SpriteRenderer sr;
    private Color baseColor;
    private Vector3 baseScale;
    private Area09MoonController visibility; // 無ければ常にフル表示扱い

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        baseScale = transform.localScale;
        visibility = GetComponent<Area09MoonController>();
    }

    private void Update()
    {
        float t = Time.time % cycleDuration;

        float pulse = Beat(t, firstBeatTime) + Beat(t, secondBeatTime);
        pulse = Mathf.Clamp01(pulse);
        CurrentPulse = pulse;

        float visAlpha = visibility != null ? visibility.VisibilityAlpha : 1f;

        float brightness = Mathf.Lerp(restBrightness, peakBrightness, pulse);
        sr.color = new Color(baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a * visAlpha);

        float scale = Mathf.Lerp(1f, peakScale, pulse);
        transform.localScale = baseScale * scale;
    }

    // beatTimeを中心とした鋭いパルス波形（ガウス関数）。0=平常、1=ピーク
    private float Beat(float t, float beatTime)
    {
        float d = t - beatTime;
        return Mathf.Exp(-(d * d) / (beatSharpness * beatSharpness));
    }
}
