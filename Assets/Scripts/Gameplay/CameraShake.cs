using System.Collections;
using UnityEngine;

/// <summary>
/// カメラシェイク。Main Camera にアタッチして使う。
/// 呼び出し側: CameraShake.Shake() または CameraShake.Instance.ShakeOnce()
/// </summary>
[DisallowMultipleComponent]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Shake Settings")]
    [Tooltip("シェイクの継続時間（秒）")]
    [SerializeField] private float shakeDuration = 0.2f;

    [Tooltip("シェイクの振幅（ワールド単位）")]
    [SerializeField] private float shakeMagnitude = 0.08f;

    private Vector3 originalLocalPos;
    private Coroutine shakeCo;

    private void Awake()
    {
        Instance = this;
        originalLocalPos = transform.localPosition;
    }

    /// <summary>デフォルト設定でシェイクを起動する</summary>
    public static void Shake()
    {
        if (Instance != null)
            Instance.ShakeOnce(Instance.shakeDuration, Instance.shakeMagnitude);
    }

    /// <summary>パラメータ指定でシェイクを起動する</summary>
    public static void Shake(float duration, float magnitude)
    {
        if (Instance != null)
            Instance.ShakeOnce(duration, magnitude);
    }

    public void ShakeOnce(float duration, float magnitude)
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                transform.localPosition = originalLocalPos;
                yield return null;
                continue;
            }

            float progress = elapsed / duration;
            float currentMagnitude = magnitude * (1f - progress); // 徐々に収束

            float x = Random.Range(-1f, 1f) * currentMagnitude;
            float y = Random.Range(-1f, 1f) * currentMagnitude;

            transform.localPosition = originalLocalPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalLocalPos;
        shakeCo = null;
    }
}
