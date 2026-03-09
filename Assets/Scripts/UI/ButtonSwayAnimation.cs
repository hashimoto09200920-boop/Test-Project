using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ボタン画像をゆっくり揺らす＋明暗アニメーション
/// アタッチするだけで動作する
/// </summary>
public class ButtonSwayAnimation : MonoBehaviour
{
    [Header("Sway Animation")]
    [Tooltip("最大回転角度（度）\n小さいほど微妙な揺れ、大きいほど大きな揺れ")]
    [SerializeField] private float swayAngle = 4f;

    [Tooltip("揺れの速度（Hz）\n0.5 = ゆっくり、1.0 = やや速い")]
    [SerializeField] private float swaySpeed = 0.5f;

    [Tooltip("ONにすると起動時にランダムな位相オフセットを付加\n複数ボタンが同じタイミングで揺れるのを防ぐ")]
    [SerializeField] private bool randomizeOffset = true;

    [Tooltip("手動で位相オフセットを設定（randomizeOffsetがOFFのとき有効）\n0〜1の値で開始タイミングをずらせる")]
    [Range(0f, 1f)]
    [SerializeField] private float phaseOffset = 0f;

    [Header("Brightness Animation")]
    [Tooltip("明暗アニメーションを有効にする")]
    [SerializeField] private bool enableBrightness = true;

    [Tooltip("明暗アニメーションの最小輝度（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float brightnessMin = 0.7f;

    [Tooltip("明暗アニメーションの最大輝度（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float brightnessMax = 1.0f;

    [Tooltip("明暗アニメーションの速度の最小値（Hz）")]
    [SerializeField] private float brightnessSpeedMin = 0.2f;

    [Tooltip("明暗アニメーションの速度の最大値（Hz）")]
    [SerializeField] private float brightnessSpeedMax = 0.7f;

    [Tooltip("速度が次のランダム値に変化するまでの時間（秒）")]
    [SerializeField] private float brightnessSpeedChangeInterval = 3f;

    private RectTransform rectTransform;
    private Image targetImage;
    private Color originalColor;
    private float swayTimeOffset;

    // 明暗アニメーション用
    private float brightnessT;
    private float brightnessCurrentSpeed;
    private float brightnessTargetSpeed;
    private float brightnessSpeedTimer;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        targetImage = GetComponent<Image>();

        // 自身のImageが透明（a≒0）な場合、子オブジェクトの可視Imageを探す
        if (targetImage == null || targetImage.color.a < 0.01f)
        {
            foreach (var img in GetComponentsInChildren<Image>())
            {
                if (img.color.a > 0.01f)
                {
                    targetImage = img;
                    break;
                }
            }
        }

        if (targetImage != null)
            originalColor = targetImage.color;

        swayTimeOffset = randomizeOffset ? Random.Range(0f, 1f) : phaseOffset;

        brightnessT = Random.Range(0f, 1f);
        brightnessCurrentSpeed = Random.Range(brightnessSpeedMin, brightnessSpeedMax);
        brightnessTargetSpeed  = Random.Range(brightnessSpeedMin, brightnessSpeedMax);
        brightnessSpeedTimer   = 0f;
    }

    private void Update()
    {
        // 揺れ
        float t = (Time.unscaledTime * swaySpeed + swayTimeOffset) * Mathf.PI * 2f;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t) * swayAngle);

        // 明暗
        if (enableBrightness && targetImage != null)
        {
            float dt = Time.unscaledDeltaTime;

            brightnessSpeedTimer += dt;
            brightnessCurrentSpeed = Mathf.Lerp(brightnessCurrentSpeed, brightnessTargetSpeed, dt * 1.5f);
            if (brightnessSpeedTimer >= brightnessSpeedChangeInterval)
            {
                brightnessTargetSpeed = Random.Range(brightnessSpeedMin, brightnessSpeedMax);
                brightnessSpeedTimer  = 0f;
            }

            brightnessT += dt * brightnessCurrentSpeed;
            float brightness = Mathf.Lerp(brightnessMin, brightnessMax,
                                          (Mathf.Sin(brightnessT * Mathf.PI * 2f) + 1f) * 0.5f);
            targetImage.color = new Color(
                originalColor.r * brightness,
                originalColor.g * brightness,
                originalColor.b * brightness,
                originalColor.a);
        }
    }

    private void OnDisable()
    {
        if (rectTransform != null)
            rectTransform.localRotation = Quaternion.identity;
        if (targetImage != null)
            targetImage.color = originalColor;
    }
}
