using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 煙幕弾から生成される煙のエフェクトを管理
/// - 徐々に拡散
/// - 視界制限（範囲内のオブジェクトを非表示）
/// - 白線/赤線の円判定で消滅
/// </summary>
public class SmokeCloud : MonoBehaviour
{
    [Header("Smoke Parameters")]
    [SerializeField] private float maxRadius = 3f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float expansionSpeed = 0.5f;

    [Header("Visual")]
    [SerializeField] private ParticleSystem smokeParticle;
    [SerializeField] private CircleCollider2D smokeTrigger;
    [Tooltip("パーティクルの放出レート（個/秒）。0以下の場合はPrefabのデフォルト値を使用")]
    [SerializeField] private float emissionRateOverTime = 0f;

    [Header("Rendering")]
    [Tooltip("煙の描画レイヤー名（未設定なら\"Default\"）")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("煙の描画順序（大きいほど手前）。弾/敵/プレイヤーより大きい値を設定。デフォルト1000で最前面")]
    [SerializeField] private int sortingOrder = 1000;

    [Header("Fade")]
    [Tooltip("フェードイン秒数（煙が出現する時にアルファ0→1する時間）")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [Tooltip("フェードアウト秒数（煙が消える前にアルファ1→0する時間）")]
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("Audio")]
    [Tooltip("煙が円判定で消滅する時のSE（未設定なら鳴らない）")]
    [SerializeField] private AudioClip circleDissolveClip;

    private float currentRadius = 0f;
    private float elapsedTime = 0f;
    private bool _dissolved = false;

    // Gradient を毎フレーム new せずキャッシュ（GC軽減）
    private Gradient _fadeGrad;
    private GradientColorKey[] _colorKeys;
    private GradientAlphaKey[] _alphaKeys;

    private HashSet<GameObject> hiddenObjects = new HashSet<GameObject>();

    public void SetFadeDurations(float fadeIn, float fadeOut)
    {
        fadeInDuration  = Mathf.Max(0f, fadeIn);
        fadeOutDuration = Mathf.Max(0f, fadeOut);
    }

    public void SetEmissionRate(float rate)
    {
        emissionRateOverTime = rate;
    }

    public void Initialize(float radius, float dur, float expSpeed)
    {
        Initialize(radius, dur, expSpeed, null);
    }

    public void Initialize(float radius, float dur, float expSpeed, AudioClip dissolveSE)
    {
        maxRadius = Mathf.Max(1f, radius);
        duration = Mathf.Max(1f, dur);
        expansionSpeed = Mathf.Max(0.1f, expSpeed);
        circleDissolveClip = dissolveSE;
        currentRadius = 0.1f;
        elapsedTime = 0f;
        _dissolved = false;

        if (smokeParticle != null)
        {
            var main = smokeParticle.main;
            main.startLifetime = duration;
            main.startSpeed = expansionSpeed;

            if (emissionRateOverTime > 0f)
            {
                var emission = smokeParticle.emission;
                emission.rateOverTime = emissionRateOverTime;
            }

            var shape = smokeParticle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            // フェードイン開始なのでアルファ0で再生
            ApplyGlobalAlpha(0f);
            smokeParticle.Play();
        }

        if (smokeTrigger == null)
            smokeTrigger = gameObject.AddComponent<CircleCollider2D>();
        smokeTrigger.isTrigger = true;
        smokeTrigger.radius = currentRadius;

        // 安全弁（通常は Update の destroy が先に走る）
        Destroy(gameObject, duration + fadeOutDuration + 1f);
    }

    private void Awake()
    {
        if (smokeParticle == null)
            smokeParticle = GetComponentInChildren<ParticleSystem>();

        if (smokeTrigger == null)
        {
            smokeTrigger = GetComponent<CircleCollider2D>();
            if (smokeTrigger == null)
            {
                smokeTrigger = gameObject.AddComponent<CircleCollider2D>();
                smokeTrigger.isTrigger = true;
            }
        }

        // Gradient キャッシュ
        _fadeGrad = new Gradient();
        _colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        };
        _alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
        };

        ApplySortingOrder();
    }

    private void ApplySortingOrder()
    {
        if (smokeParticle == null) return;
        var r = smokeParticle.GetComponent<ParticleSystemRenderer>();
        if (r != null)
        {
            r.sortingLayerName = sortingLayerName;
            r.sortingOrder = sortingOrder;
        }
    }

    /// <summary>
    /// クラウド全体の経過時間からアルファを計算してパーティクルシステム全体に適用する。
    /// colorOverLifetime をフラット（全年齢同一値）にすることで全パーティクルに等しく乗算される。
    /// </summary>
    private void ApplyGlobalAlpha(float alpha)
    {
        if (smokeParticle == null) return;

        _alphaKeys[0].alpha = alpha;
        _alphaKeys[1].alpha = alpha;
        _fadeGrad.SetKeys(_colorKeys, _alphaKeys);

        var col = smokeParticle.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(_fadeGrad);
    }

    private float ComputeCloudAlpha()
    {
        // フェードイン
        if (elapsedTime < fadeInDuration)
            return Mathf.Clamp01(elapsedTime / Mathf.Max(fadeInDuration, 0.001f));

        // フェードアウト開始タイミング
        float fadeOutStart = duration - fadeOutDuration;
        if (elapsedTime > fadeOutStart)
            return Mathf.Clamp01(1f - (elapsedTime - fadeOutStart) / Mathf.Max(fadeOutDuration, 0.001f));

        return 1f;
    }

    private void Update()
    {
        if (_dissolved) return;

        elapsedTime += Time.deltaTime;

        // 拡散
        if (currentRadius < maxRadius)
        {
            currentRadius += expansionSpeed * Time.deltaTime;
            currentRadius = Mathf.Min(currentRadius, maxRadius);
            if (smokeTrigger != null) smokeTrigger.radius = currentRadius;
            if (smokeParticle != null)
            {
                var shape = smokeParticle.shape;
                shape.radius = currentRadius;
            }
        }

        // 毎フレームアルファを全パーティクルに適用
        ApplyGlobalAlpha(ComputeCloudAlpha());

        // フェードアウト開始タイミングで放出停止
        float fadeOutStart = duration - fadeOutDuration;
        if (elapsedTime >= fadeOutStart && smokeParticle != null && smokeParticle.isEmitting)
            smokeParticle.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        // 消滅
        if (elapsedTime >= duration)
        {
            RestoreAllHiddenObjects();
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (ShouldHideObject(other.gameObject))
            HideObject(other.gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (hiddenObjects.Contains(other.gameObject))
            ShowObject(other.gameObject);
    }

    private bool ShouldHideObject(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.GetComponent<EnemyBullet>() != null) return true;
        if (obj.GetComponent<EnemyMover>() != null) return true;
        if (obj.GetComponentInParent<EnemyMover>() != null) return true;
        return false;
    }

    private void HideObject(GameObject obj)
    {
        if (obj == null || hiddenObjects.Contains(obj)) return;
        foreach (var r in obj.GetComponentsInChildren<SpriteRenderer>())
            r.enabled = false;
        hiddenObjects.Add(obj);
        Debug.Log($"[SmokeCloud] Hid object: {obj.name}");
    }

    private void ShowObject(GameObject obj)
    {
        if (obj == null || !hiddenObjects.Contains(obj)) return;
        foreach (var r in obj.GetComponentsInChildren<SpriteRenderer>())
            r.enabled = true;
        hiddenObjects.Remove(obj);
        Debug.Log($"[SmokeCloud] Showed object: {obj.name}");
    }

    private void RestoreAllHiddenObjects()
    {
        foreach (var obj in hiddenObjects)
        {
            if (obj == null) continue;
            foreach (var r in obj.GetComponentsInChildren<SpriteRenderer>())
                r.enabled = true;
        }
        hiddenObjects.Clear();
    }

    /// <summary>
    /// 白線/赤線で囲まれた時に呼ばれる（PaddleDrawerから呼び出し想定）
    /// </summary>
    public void DissolveByCircle(Vector2 circleCenter, float circleRadius)
    {
        float distance = Vector2.Distance(transform.position, circleCenter);
        if (distance <= circleRadius)
        {
            Debug.Log($"[SmokeCloud] Dissolved by circle at {circleCenter} with radius {circleRadius}");

            if (circleDissolveClip != null)
                AudioSource.PlayClipAtPoint(circleDissolveClip, transform.position, 1f);

            RestoreAllHiddenObjects();
            _dissolved = true;

            if (smokeParticle != null)
                smokeParticle.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            // 円消去は素早くフェードアウト
            StartCoroutine(QuickFadeAndDestroy(Mathf.Min(0.4f, fadeOutDuration)));
        }
    }

    private IEnumerator QuickFadeAndDestroy(float fadeDuration)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            ApplyGlobalAlpha(1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        RestoreAllHiddenObjects();
    }
}
