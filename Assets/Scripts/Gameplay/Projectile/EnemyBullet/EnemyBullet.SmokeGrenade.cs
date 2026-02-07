using UnityEngine;

public partial class EnemyBullet
{
    [Header("Smoke Grenade Runtime (Injected)")]
    [SerializeField] private bool smokeGrenadeEnabled = false;
    [SerializeField] private float smokeRadius = 3f;
    [SerializeField] private float smokeDuration = 5f;
    [SerializeField] private float smokeExpansionSpeed = 0.5f;
    [SerializeField] private GameObject smokeParticlePrefab;
    [SerializeField] private AudioClip smokeReflectSE;
    [SerializeField] private GameObject smokeCircleDissolveFx;
    [SerializeField] private AudioClip smokeCircleDissolveSE;
    [SerializeField] private AudioClip smokeCloudCircleDissolveSE;

    private bool smokeGrenadeHasReflectedOnce = false; // 反射済みフラグ

    public void ApplySmokeGrenade(
        bool enable,
        float radius,
        float duration,
        float expansionSpeed,
        GameObject particlePrefab,
        AudioClip reflectSE,
        GameObject circleDissolveFx,
        AudioClip circleDissolveSE,
        AudioClip smokeCloudDissolveSE
    )
    {
        smokeGrenadeEnabled = enable;
        smokeRadius = Mathf.Max(1f, radius);
        smokeDuration = Mathf.Max(1f, duration);
        smokeExpansionSpeed = Mathf.Max(0.1f, expansionSpeed);
        smokeParticlePrefab = particlePrefab;
        smokeReflectSE = reflectSE;
        smokeCircleDissolveFx = circleDissolveFx;
        smokeCircleDissolveSE = circleDissolveSE;
        smokeCloudCircleDissolveSE = smokeCloudDissolveSE;
        smokeGrenadeHasReflectedOnce = false;

        if (smokeGrenadeEnabled)
        {
            // 煙幕弾は威力0に設定
            SetDamage(0);
        }
    }

    /// <summary>
    /// 白線/赤線で反射された時に呼ばれる（PaddleDrawerから呼び出し想定）
    /// </summary>
    public void OnSmokeGrenadeReflected(Vector3 reflectPosition)
    {
        if (!smokeGrenadeEnabled) return;

        smokeGrenadeHasReflectedOnce = true;

        // 煙を生成
        SpawnSmoke(reflectPosition);

        // 反射音を再生
        if (smokeReflectSE != null)
        {
            AudioSource.PlayClipAtPoint(smokeReflectSE, reflectPosition, 1f);
        }

        Debug.Log($"[SmokeGrenade] Reflected at {reflectPosition} | radius={smokeRadius} | duration={smokeDuration}");
    }

    /// <summary>
    /// 反射後の煙幕弾が何かに当たった時に呼ばれる
    /// </summary>
    public void OnSmokeGrenadeCollision(Vector3 collisionPosition)
    {
        if (!smokeGrenadeEnabled) return;
        if (!smokeGrenadeHasReflectedOnce) return; // 反射前は煙を出さない

        // さらに煙を拡散
        SpawnSmoke(collisionPosition);

        Debug.Log($"[SmokeGrenade] Collision at {collisionPosition} | spawning additional smoke");
    }

    private void SpawnSmoke(Vector3 position)
    {
        if (smokeParticlePrefab == null)
        {
            Debug.LogWarning("[SmokeGrenade] smokeParticlePrefab is null. Cannot spawn smoke.");
            return;
        }

        // 煙パーティクルを生成
        GameObject smokeObj = Instantiate(smokeParticlePrefab, position, Quaternion.identity);

        // SmokeCloudコンポーネントで煙を制御
        SmokeCloud smoke = smokeObj.GetComponent<SmokeCloud>();
        if (smoke != null)
        {
            smoke.Initialize(smokeRadius, smokeDuration, smokeExpansionSpeed, smokeCloudCircleDissolveSE);
        }
        else
        {
            Debug.LogWarning("[SmokeCloud] SmokeCloud component not found on spawned prefab.");
            Destroy(smokeObj, smokeDuration); // フォールバック: 時間経過で削除
        }
    }

    /// <summary>
    /// 白線/赤線で円に囲まれた時に呼ばれる（煙を出さずに即消滅）
    /// </summary>
    public void DissolveByCircle()
    {
        if (!smokeGrenadeEnabled) return;

        Debug.Log($"[SmokeGrenade] Dissolved by circle at {transform.position}");

        Vector3 position = transform.position;

        // VFXを再生
        if (smokeCircleDissolveFx != null)
        {
            Instantiate(smokeCircleDissolveFx, position, Quaternion.identity);
        }

        // SEを再生
        if (smokeCircleDissolveSE != null)
        {
            AudioSource.PlayClipAtPoint(smokeCircleDissolveSE, position, 1f);
        }

        // 煙を出さずに即消滅
        if (!isBeingDestroyed)
        {
            isBeingDestroyed = true;
            Destroy(gameObject);
        }
    }

    public bool IsSmokeGrenadeActive => smokeGrenadeEnabled;
    public bool HasSmokeGrenadeReflected => smokeGrenadeHasReflectedOnce;
}
