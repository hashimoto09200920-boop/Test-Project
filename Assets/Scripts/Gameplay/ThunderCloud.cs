using System.Collections;
using UnityEngine;

public class ThunderCloud : MonoBehaviour
{
    // ======================================================
    // Inspector
    // ======================================================

    [Header("Visual")]
    [SerializeField] private SpriteRenderer cloudRenderer;
    [SerializeField] private ParticleSystem cloudParticle;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration  = 1.5f;
    [SerializeField] private float fadeOutDuration = 2f;

    [Header("Cloud Range")]
    [Tooltip("雷弾のX範囲（雲の中心からの半幅）")]
    [SerializeField] private float halfWidth = 7f;
    [Tooltip("発射位置のY（雲中心からのオフセット）")]
    [SerializeField] private float muzzleYOffset = -0.5f;

    [Header("Bullet (default — overridden by ShamanController at spawn)")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private Transform   projectileRoot;
    [SerializeField] private float       bulletSpeed    = 8f;
    [SerializeField] private float       bulletLifeTime = 5f;

    [Header("Shot Settings")]
    [SerializeField] private int   totalShots       = 20;
    [SerializeField] private float shotStagger      = 0.3f;
    [SerializeField] private float telegraphSeconds = 0.8f;
    [SerializeField] private float telegraphLength  = 8f;
    [SerializeField] private float telegraphWidth   = 0.05f;
    [SerializeField] private Color telegraphColor   = new Color(1f, 0.9f, 0f, 0.8f);

    // EnemyDataのBulletTypeをShamanControllerから注入
    private EnemyData.BulletType      _bulletType;
    private EnemyBullet               _bulletPrefabOverride;
    private Transform                 _projectileRootOverride;
    private ShamanController.ShamanFrame[] _frames;
    private Vector3                   _originPos;

    // ======================================================
    // Setup（ShamanControllerがスポーン後に呼ぶ）
    // ======================================================

    public void SetFrames(ShamanController.ShamanFrame[] frames)
    {
        _frames = frames;
    }

    public void SetBulletType(EnemyData.BulletType type, EnemyBullet prefab, Transform root)
    {
        _bulletType             = type;
        _bulletPrefabOverride   = prefab;
        _projectileRootOverride = root;
    }

    // ======================================================
    // Activate（セットアップ完了後に呼ぶ）
    // ======================================================

    public void Activate()
    {
        _originPos = transform.position;

        if (cloudRenderer != null)
        {
            var c = cloudRenderer.color;
            c.a = 0f;
            cloudRenderer.color = c;

            if (_frames != null && _frames.Length > 0)
                cloudRenderer.sprite = _frames[0]?.sprite;
        }
        StartCoroutine(ThunderRoutine());
        if (_frames != null && _frames.Length > 1)
            StartCoroutine(FrameCycleRoutine());
    }

    // ======================================================
    // Main Routine
    // ======================================================

    private IEnumerator ThunderRoutine()
    {
        yield return StartCoroutine(FadeSprite(0f, 1f, fadeInDuration));

        if (cloudParticle != null)
            cloudParticle.Play();

        // 全ショットをスタガーで起動
        for (int i = 0; i < totalShots; i++)
        {
            float xPos = transform.position.x + Random.Range(-halfWidth, halfWidth);
            float yPos = transform.position.y + muzzleYOffset;
            Vector3 muzzlePos = new Vector3(xPos, yPos, transform.position.z);
            StartCoroutine(FireWithTelegraph(muzzlePos));
            yield return new WaitForSeconds(shotStagger);
        }

        // 最後のテレグラフ＋弾が終わるまで待機
        yield return new WaitForSeconds(telegraphSeconds + 0.5f);

        if (cloudParticle != null)
            cloudParticle.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        yield return StartCoroutine(FadeSprite(1f, 0f, fadeOutDuration));

        Destroy(gameObject);
    }

    // ======================================================
    // フレームアニメーション（cloudRenderer）
    // ======================================================

    private IEnumerator FrameCycleRoutine()
    {
        if (_frames == null || _frames.Length == 0) yield break;

        int idx = 0;
        while (cloudRenderer != null)
        {
            var f = _frames[idx];
            if (f != null)
            {
                if (f.sprite != null) cloudRenderer.sprite = f.sprite;
                cloudRenderer.transform.position = new Vector3(
                    _originPos.x + f.offset.x,
                    _originPos.y + f.offset.y,
                    _originPos.z);
            }
            float dur = (f != null && f.duration > 0f) ? f.duration : 0.15f;
            yield return new WaitForSeconds(dur);
            idx = (idx + 1) % _frames.Length;
        }
    }

    // ======================================================
    // 1発分のTelegraph + 発射
    // ======================================================

    private IEnumerator FireWithTelegraph(Vector3 muzzlePos)
    {
        Vector2 dir = Vector2.down;

        // Telegraph ライン生成
        GameObject lineGo = new GameObject("ThunderTelegraph");
        Transform lineParent = (_projectileRootOverride ?? projectileRoot) ?? transform;
        lineGo.transform.SetParent(lineParent, false);

        LineRenderer lr = lineGo.AddComponent<LineRenderer>();
        lr.material = GetOrCreateLineMaterial();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, muzzlePos);
        lr.SetPosition(1, muzzlePos + (Vector3)(dir * telegraphLength));
        lr.startWidth  = telegraphWidth;
        lr.endWidth    = telegraphWidth;
        lr.startColor  = telegraphColor;
        lr.endColor    = telegraphColor;
        lr.numCapVertices = 4;
        lr.alignment   = LineAlignment.View;

        // 点滅
        float elapsed = 0f;
        int blinkCount = 6;
        while (elapsed < telegraphSeconds)
        {
            elapsed += Time.deltaTime;
            float t   = elapsed / telegraphSeconds;
            int   seg = Mathf.Min(blinkCount * 2 - 1, Mathf.FloorToInt(t * blinkCount * 2));
            bool  on  = (seg % 2 == 0);
            Color c   = telegraphColor;
            c.a = on ? telegraphColor.a : telegraphColor.a * 0.2f;
            lr.startColor = c;
            lr.endColor   = c;
            yield return null;
        }

        Destroy(lineGo);

        // 弾発射
        EnemyBullet activePrefab = _bulletPrefabOverride ?? bulletPrefab;
        Transform   activeRoot   = _projectileRootOverride ?? projectileRoot;
        if (activePrefab != null && activeRoot != null
            && !FloorHealth.IsBrokenGlobal
            && !PixelDancerController.IsPlayerDeadGlobal
            && !PixelDancerController.IsDownGlobal)
        {
            var bullet = Instantiate(activePrefab, muzzlePos, Quaternion.identity, activeRoot);
            bullet.SetDirection(dir);
            if (_bulletType != null)
                EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, _bulletType, bulletSpeed, bulletLifeTime);
            else
                bullet.ApplyBullet(bulletSpeed, bulletLifeTime);
        }
    }

    // ======================================================
    // Fade
    // ======================================================

    private IEnumerator FadeSprite(float from, float to, float dur)
    {
        float elapsed = 0f;
        dur = Mathf.Max(dur, 0.001f);
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            SetSpriteAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / dur)));
            yield return null;
        }
        SetSpriteAlpha(to);
    }

    private void SetSpriteAlpha(float alpha)
    {
        if (cloudRenderer == null) return;
        var c = cloudRenderer.color;
        c.a = alpha;
        cloudRenderer.color = c;
    }

    // ======================================================
    // Helper
    // ======================================================

    private static Material _lineMat;

    private static Material GetOrCreateLineMaterial()
    {
        if (_lineMat != null) return _lineMat;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null) _lineMat = new Material(sh);
        return _lineMat;
    }

    // ======================================================
    // Context Menu
    // ======================================================

    [ContextMenu("Setup ThunderCloud References")]
    private void SetupReferences()
    {
#if UNITY_EDITOR
        // Root の SpriteRenderer → cloudRenderer
        cloudRenderer = GetComponent<SpriteRenderer>();

        // 子の ParticleSystem → cloudParticle（なくてもOK）
        cloudParticle = GetComponentInChildren<ParticleSystem>();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[ThunderCloud] References set up. cloudRenderer=" + cloudRenderer +
                  ", cloudParticle=" + cloudParticle);
#endif
    }
}
