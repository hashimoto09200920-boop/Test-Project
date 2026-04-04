using System.Collections;
using UnityEngine;

/// <summary>
/// ダメージ時にSpriteをシェイクさせるコンポーネント。
/// SpriteSwimAnimation（localRotation/localScale制御）とは独立して
/// localPositionオフセットのみを操作するため競合しない。
/// </summary>
public class EnemySpriteShake : MonoBehaviour
{
    [Header("Normal Hit Shake")]
    [Tooltip("通常反射弾ヒット時のシェイク継続時間（秒）")]
    [SerializeField] private float normalShakeDuration = 0.15f;

    [Tooltip("通常反射弾ヒット時のシェイク強度（ワールド単位）")]
    [SerializeField] private float normalShakeIntensity = 0.06f;

    [Header("Just Hit Shake")]
    [Tooltip("ジャスト反射弾ヒット時のシェイク継続時間（秒）")]
    [SerializeField] private float justShakeDuration = 0.25f;

    [Tooltip("ジャスト反射弾ヒット時のシェイク強度（ワールド単位）")]
    [SerializeField] private float justShakeIntensity = 0.14f;

    [Header("Common")]
    [Tooltip("ONにすると時間経過で強度が減衰する")]
    [SerializeField] private bool decayOverTime = true;

    private Coroutine shakeCoroutine;
    private Vector3 shakeOffset = Vector3.zero;

    /// <summary>シェイク中かどうか（SpriteSwimAnimationの向き判定スキップ用）</summary>
    public bool IsShaking { get; private set; }

    /// <summary>
    /// シェイクを開始する。isJust=trueでジャスト用設定を使用。
    /// 連打時は前のシェイクを止めて即再開。
    /// </summary>
    public void TriggerShake(bool isJust = false)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            transform.localPosition -= shakeOffset;
            shakeOffset = Vector3.zero;
        }
        float duration  = isJust ? justShakeDuration  : normalShakeDuration;
        float intensity = isJust ? justShakeIntensity : normalShakeIntensity;
shakeCoroutine = StartCoroutine(ShakeCoroutine(duration, intensity));
    }

    private IEnumerator ShakeCoroutine(float duration, float intensity)
    {
        IsShaking = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 前フレームのオフセットを除去
            transform.localPosition -= shakeOffset;

            float progress = elapsed / duration;
            float currentIntensity = decayOverTime
                ? intensity * (1f - progress)
                : intensity;

            // 新しいオフセットを適用
            shakeOffset = new Vector3(
                Random.Range(-currentIntensity, currentIntensity),
                Random.Range(-currentIntensity, currentIntensity),
                0f
            );
            transform.localPosition += shakeOffset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // シェイク終了：オフセットをリセット
        transform.localPosition -= shakeOffset;
        shakeOffset = Vector3.zero;
        IsShaking = false;
        shakeCoroutine = null;
    }
}
