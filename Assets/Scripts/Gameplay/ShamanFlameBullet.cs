using UnityEngine;

/// <summary>
/// Area6ボス「Shaman」の「精霊の炎」で、プレイヤーの線で反射されて飛んでいく火の玉の弾にだけ付ける部品（Shaman専用）。
/// ・見た目は炎のParticle（弾の画像は描画だけ止め、炎のParticleを子として付ける。消える時に元に戻す）
/// ・Trail（軌跡）を出さない（TrailRendererの描画だけ止め、消える時に元に戻す）
/// ・線での反射・敵や壁への当たり・弾同士の接触の回数で消えない（反射回数の上限を無効にする。画面外のKillZoneでは消える）
/// ・反射された火の玉だけが、近くの未反射の弾を吸収する（反射済みの弾は吸収しない）。
///   回っている火の玉（ShamanSpiritFlame。色が変わっていないものも）も吸収する。
///   吸収するたびに：弾の見た目と当たり判定を少し大きく（最大Max Absorb Count回まで）、SE（火炎魔法1）。威力は上がらない
///   ★共有のEnemyBulletは「通常の反射弾が未反射弾に触れると両方消える」ため、物理的に触れる直前（FixedUpdate）に
///     近くの未反射弾を吸収して消す（共有コードは変更しない）
/// ・プールで再利用される弾に残らないよう、弾が消える時に自分自身を外す（大きさはEnemyBullet側が再利用時に元へ戻す）
/// </summary>
[DisallowMultipleComponent]
public class ShamanFlameBullet : MonoBehaviour
{
    private EnemyBullet bullet;
    private Rigidbody2D rb;
    private CircleCollider2D circle;
    private TrailRenderer[] trails;
    private SpriteRenderer[] sprites;
    private ParticleSystem fxInstance;
    private AudioClip absorbSE;
    private float absorbSEVolume;
    private int maxAbsorb;
    private float growthPerAbsorb;
    private int absorbed;
    private bool armed;

    private static readonly Collider2D[] s_hits = new Collider2D[32];
    private static ContactFilter2D s_filter;
    private static bool s_filterReady;

    /// <param name="fxTint">炎の粒の色（null＝炎のParticleの元の色のまま）。青白くなった火の玉を反射した時はその色を引き継ぐ</param>
    public void Arm(EnemyBullet b, ParticleSystem fxPrefab, AudioClip se, float seVolume, int maxAbsorbCount, float growth, Color? fxTint = null)
    {
        bullet = b;
        rb = b.GetComponent<Rigidbody2D>();
        circle = b.GetComponent<CircleCollider2D>();
        absorbSE = se;
        absorbSEVolume = seVolume;
        maxAbsorb = Mathf.Max(0, maxAbsorbCount);
        growthPerAbsorb = Mathf.Max(0f, growth);
        absorbed = 0;
        armed = true;

        // 反射回数で消えないようにする（上限0＝無制限）
        b.ConfigurePaddleBounceLimit(0);

        // Trailは出さない
        trails = b.GetComponentsInChildren<TrailRenderer>(true);
        foreach (var t in trails) if (t != null) t.forceRenderingOff = true;

        // 見た目：弾の画像は描画だけ止め、炎のParticleを子として付ける（弾が大きくなると炎も一緒に大きくなる）
        if (fxPrefab != null)
        {
            sprites = b.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in sprites) if (sr != null) sr.forceRenderingOff = true;
            fxInstance = Instantiate(fxPrefab, b.transform);
            fxInstance.transform.localPosition = Vector3.zero;
            fxInstance.transform.localRotation = Quaternion.identity;
            if (fxTint.HasValue)
                foreach (var ps in fxInstance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startColor = fxTint.Value;
                }
            fxInstance.Play(true);
        }

        if (!s_filterReady)
        {
            s_filter = new ContactFilter2D();
            s_filter.useTriggers = true;
            s_filter.SetLayerMask(Physics2D.AllLayers);
            s_filterReady = true;
        }
    }

    // 物理の衝突判定（FixedUpdateの後）より先に、近くの未反射弾を吸収する
    private void FixedUpdate()
    {
        if (!armed || bullet == null) return;
        if (!(bullet.IsReflected || bullet.HasPaddleReflectedOnce)) return; // 未反射の火の玉は吸収しない

        float radius = BulletRadius();
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        // 1ステップで近づく分（自分と相手の速さ）を余裕として足す
        float searchRadius = radius + (speed + 12f) * Time.fixedDeltaTime;
        int n = Physics2D.OverlapCircle(transform.position, searchRadius, s_filter, s_hits);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = s_hits[i];
            if (c == null) continue;
            // 回っている火の玉も吸収する
            ShamanSpiritFlame orbitFlame = c.GetComponent<ShamanSpiritFlame>();
            if (orbitFlame != null)
            {
                if (orbitFlame.IsAlive) { orbitFlame.Vanish(); OnAbsorbed(); }
                continue;
            }
            EnemyBullet other = c.GetComponentInParent<EnemyBullet>();
            if (other == null || other == bullet || !other.gameObject.activeInHierarchy) continue;
            if (other.IsReflected || other.HasPaddleReflectedOnce) continue; // 反射済みの弾は吸収しない
            Absorb(other);
        }
    }

    private void Absorb(EnemyBullet other)
    {
        other.ReleaseOrDestroySelf(); // 吸収した弾は静かに消す
        OnAbsorbed();
    }

    // 吸収した時：少し大きくなる（威力は上がらない）＋SE
    private void OnAbsorbed()
    {
        if (absorbed < maxAbsorb)
        {
            absorbed++;
            transform.localScale *= 1f + growthPerAbsorb;                    // 見た目と当たり判定を少し大きく
        }
        if (absorbSE != null && absorbSEVolume > 0f && SeSimultaneousGuard.TryAllow("ShamanFlameBullet_Absorb"))
            AudioOneShotPool.Play(absorbSE, absorbSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f), transform.position, null, 0.1f);
    }

    private float BulletRadius()
    {
        if (circle != null)
        {
            Vector3 s = transform.lossyScale;
            return circle.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
        }
        return 0.2f * Mathf.Abs(transform.lossyScale.x);
    }

    private void OnDisable()
    {
        armed = false;
        if (trails != null) foreach (var t in trails) if (t != null) t.forceRenderingOff = false;
        if (sprites != null) foreach (var sr in sprites) if (sr != null) sr.forceRenderingOff = false;
        if (fxInstance != null) Destroy(fxInstance.gameObject); // プールで再利用される弾に炎を残さない
        Destroy(this);
    }
}
