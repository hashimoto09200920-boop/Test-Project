using System.Collections;
using UnityEngine;

public class TornadoCloud : MonoBehaviour
{
    // ======================================================
    // Inspector
    // ======================================================

    [Header("Visual")]
    [SerializeField] private ParticleSystem smokeParticle;
    [SerializeField] private ParticleSystem sandGrainParticle;
    [SerializeField] private CircleCollider2D smokeTrigger;
    [SerializeField] private float maxRadius            = 2f;
    [SerializeField] private float expansionSpeed       = 0.07f;
    [SerializeField] private float emissionRateOverTime = 3f;
    [SerializeField] private string sortingLayerName    = "Default";
    [SerializeField] private int    sortingOrder        = 1000;

    [Header("Particle Settings")]
    [Tooltip("パーティクルの最小サイズ")]
    [SerializeField] private float particleSizeMin  = 1f;
    [Tooltip("パーティクルの最大サイズ")]
    [SerializeField] private float particleSizeMax  = 2f;
    [Tooltip("各パーティクルの寿命（秒）。短くすると外への拡散距離が縮まる。0の場合はdurationを使用")]
    [SerializeField] private float particleLifetime = 3f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration  = 1f;
    [SerializeField] private float fadeOutDuration = 2f;

    [Header("Duration")]
    [SerializeField] private float duration = 10f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.5f;

    [Header("Rotation")]
    [Tooltip("視覚的な回転速度（度/秒）。clockwise引数で方向を決定する")]
    [SerializeField] private float rotationSpeedDeg = 90f;

    [Header("Bullet (default — overridden by ShamanController at spawn)")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private Transform   projectileRoot;
    [SerializeField] private float       fireInterval   = 1.5f;
    [SerializeField] private float       bulletSpeed    = 5f;
    [SerializeField] private float       bulletLifeTime = 4f;

    // EnemyDataのBulletTypeをShamanControllerから注入（SetBulletType経由）
    private EnemyData.BulletType _bulletType;
    private EnemyBullet          _bulletPrefabOverride;
    private Transform            _projectileRootOverride;
    private float                _fireIntervalOverride = -1f;

    [Header("Audio")]
    [SerializeField] private AudioClip circleDissolveClip;

    // ======================================================
    // Runtime
    // ======================================================

    private bool    _dissolved;
    private float   _elapsed;
    private float   _currentRadius;
    private Vector2 _moveDir;
    private float   _actualRotSpeed;

    private Gradient           _fadeGrad;
    private GradientColorKey[] _colorKeys;
    private GradientAlphaKey[] _alphaKeys;

    public bool IsAlive => !_dissolved && _elapsed < duration;

    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    // ======================================================
    // Setup（ShamanControllerがスポーン後に呼ぶ）
    // ======================================================

    public void SetBulletType(EnemyData.BulletType type, EnemyBullet prefab, Transform root, float interval)
    {
        _bulletType             = type;
        _bulletPrefabOverride   = prefab;
        _projectileRootOverride = root;
        _fireIntervalOverride   = Mathf.Max(0.01f, interval);
    }

    public void SetSmokeColor(Color smokeColor, Color grainColor)
    {
        if (smokeParticle != null)
        {
            var m = smokeParticle.main;
            m.startColor = new ParticleSystem.MinMaxGradient(smokeColor);
        }
        if (sandGrainParticle != null)
        {
            var m = sandGrainParticle.main;
            m.startColor = new ParticleSystem.MinMaxGradient(grainColor);
        }
    }

    public void SetFadeDurations(float fadeIn, float fadeOut)
    {
        fadeInDuration  = Mathf.Max(0f, fadeIn);
        fadeOutDuration = Mathf.Max(0f, fadeOut);
    }

    public void SetEmissionRate(float rate)
    {
        emissionRateOverTime = rate;
    }

    public void SetParticleSize(float sizeMin, float sizeMax)
    {
        particleSizeMin = sizeMin;
        particleSizeMax = Mathf.Max(sizeMin, sizeMax);
    }

    public void SetParticleLifetime(float lifetime)
    {
        particleLifetime = Mathf.Max(0.1f, lifetime);
    }

    // ======================================================
    // Initialize
    // ======================================================

    public void Initialize(float dur, float spd, bool clockwise, Vector2 moveDir)
    {
        duration        = Mathf.Max(1f, dur);
        moveSpeed       = Mathf.Max(0f, spd);
        _moveDir        = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector2.right;
        _actualRotSpeed = rotationSpeedDeg * (clockwise ? -1f : 1f);
        _currentRadius  = 0.1f;
        _elapsed        = 0f;
        _dissolved      = false;

        _fadeGrad  = new Gradient();
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

        ApplySortingLayer();

        if (smokeParticle != null)
        {
            var main = smokeParticle.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetime > 0f ? particleLifetime : duration);
            main.startSpeed    = expansionSpeed;
            main.startSize     = new ParticleSystem.MinMaxCurve(particleSizeMin, particleSizeMax);
            if (emissionRateOverTime > 0f)
            {
                var em = smokeParticle.emission;
                em.rateOverTime = emissionRateOverTime;
            }
            var shape = smokeParticle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.1f;
            ApplyGlobalAlpha(0f);
            smokeParticle.Play();
        }

        if (sandGrainParticle != null)
            sandGrainParticle.Play();

        if (smokeTrigger == null)
            smokeTrigger = GetComponent<CircleCollider2D>();
        if (smokeTrigger == null)
            smokeTrigger = gameObject.AddComponent<CircleCollider2D>();
        smokeTrigger.isTrigger = true;
        smokeTrigger.radius    = _currentRadius;

        EnemyBullet activePrefab = _bulletPrefabOverride ?? bulletPrefab;
        if (activePrefab != null)
            StartCoroutine(FireLoop());

        Destroy(gameObject, duration + fadeOutDuration + 1f);
    }

    // ======================================================
    // Update
    // ======================================================

    private void Update()
    {
        if (_dissolved) return;

        _elapsed += Time.deltaTime;

        // 拡散
        if (_currentRadius < maxRadius)
        {
            _currentRadius = Mathf.Min(_currentRadius + expansionSpeed * Time.deltaTime, maxRadius);
            if (smokeTrigger != null) smokeTrigger.radius = _currentRadius;
            if (smokeParticle != null)
            {
                var shape = smokeParticle.shape;
                shape.radius = _currentRadius;
            }
        }

        // 移動
        transform.position += (Vector3)(_moveDir * moveSpeed * Time.deltaTime);

        // 視覚的回転（CircleCollider2Dは円なので回転しても判定は変わらない）
        transform.Rotate(0f, 0f, _actualRotSpeed * Time.deltaTime);

        // フェード
        ApplyGlobalAlpha(ComputeAlpha());

        // フェードアウト開始タイミングで放出停止
        float fadeOutStart = duration - fadeOutDuration;
        if (_elapsed >= fadeOutStart)
        {
            if (smokeParticle != null && smokeParticle.isEmitting)
                smokeParticle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (sandGrainParticle != null && sandGrainParticle.isEmitting)
                sandGrainParticle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        if (_elapsed >= duration)
            Destroy(gameObject);
    }

    // ======================================================
    // 弾発射
    // ======================================================

    private IEnumerator FireLoop()
    {
        yield return new WaitForSeconds(Mathf.Max(fadeInDuration, 0.2f));

        float interval = _fireIntervalOverride > 0f ? _fireIntervalOverride : fireInterval;
        while (!_dissolved && _elapsed < duration - fadeOutDuration)
        {
            FireOnce();
            yield return new WaitForSeconds(interval);
        }
    }

    private void FireOnce()
    {
        EnemyBullet prefab = _bulletPrefabOverride ?? bulletPrefab;
        Transform   root   = _projectileRootOverride ?? projectileRoot;
        if (prefab == null || root == null) return;
        if (FloorHealth.IsBrokenGlobal) return;
        if (PixelDancerController.IsPlayerDeadGlobal || PixelDancerController.IsDownGlobal) return;

        float angle = Random.Range(0f, 360f);
        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        var bullet = Instantiate(prefab, transform.position, Quaternion.identity, root);
        bullet.SetDirection(dir);
        if (_bulletType != null)
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, _bulletType, bulletSpeed, bulletLifeTime);
        else
            bullet.ApplyBullet(bulletSpeed, bulletLifeTime);
    }

    // ======================================================
    // 円判定による消滅
    // ======================================================

    public void DissolveByCircle(Vector2 circleCenter, float circleRadius)
    {
        if (_dissolved) return;
        if (Vector2.Distance(transform.position, circleCenter) > circleRadius) return;

        _dissolved = true;

        if (circleDissolveClip != null)
            AudioSource.PlayClipAtPoint(circleDissolveClip, transform.position, MasterSEVolume);
        if (smokeParticle != null)
            smokeParticle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        if (sandGrainParticle != null)
            sandGrainParticle.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        StartCoroutine(QuickFadeAndDestroy(Mathf.Min(0.4f, fadeOutDuration)));
    }

    private IEnumerator QuickFadeAndDestroy(float fadeDur)
    {
        float e = 0f;
        while (e < fadeDur)
        {
            e += Time.deltaTime;
            ApplyGlobalAlpha(1f - Mathf.Clamp01(e / fadeDur));
            yield return null;
        }
        Destroy(gameObject);
    }

    // ======================================================
    // Helpers
    // ======================================================

    private float ComputeAlpha()
    {
        if (_elapsed < fadeInDuration)
            return Mathf.Clamp01(_elapsed / Mathf.Max(fadeInDuration, 0.001f));
        float fadeOutStart = duration - fadeOutDuration;
        if (_elapsed > fadeOutStart)
            return Mathf.Clamp01(1f - (_elapsed - fadeOutStart) / Mathf.Max(fadeOutDuration, 0.001f));
        return 1f;
    }

    private void ApplyGlobalAlpha(float alpha)
    {
        if (_fadeGrad == null) return;
        _alphaKeys[0].alpha = alpha;
        _alphaKeys[1].alpha = alpha;
        _fadeGrad.SetKeys(_colorKeys, _alphaKeys);

        ApplyAlphaToPS(smokeParticle);
        ApplyAlphaToPS(sandGrainParticle);
    }

    private void ApplyAlphaToPS(ParticleSystem ps)
    {
        if (ps == null) return;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(_fadeGrad);
    }

    private void ApplySortingLayer()
    {
        SetPSSort(smokeParticle,    sortingOrder);
        SetPSSort(sandGrainParticle, sortingOrder + 1);
    }

    private void SetPSSort(ParticleSystem ps, int order)
    {
        if (ps == null) return;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        r.sortingLayerName = sortingLayerName;
        r.sortingOrder     = order;
    }

    // ======================================================
    // Context Menu
    // ======================================================

    [ContextMenu("Setup TornadoCloud References")]
    private void SetupReferences()
    {
#if UNITY_EDITOR
        // Root の ParticleSystem → smokeParticle
        smokeParticle = GetComponent<ParticleSystem>();

        // 子 "SandGrainPS" → sandGrainParticle
        foreach (Transform child in transform)
        {
            var ps = child.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                sandGrainParticle = ps;
                break;
            }
        }

        // CircleCollider2D（あれば）
        smokeTrigger = GetComponent<CircleCollider2D>();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[TornadoCloud] References set up. smokeParticle=" + smokeParticle +
                  ", sandGrainParticle=" + sandGrainParticle);
#endif
    }
}
