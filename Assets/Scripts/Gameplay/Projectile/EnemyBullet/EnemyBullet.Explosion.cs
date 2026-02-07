using UnityEngine;

public partial class EnemyBullet
{
    // =========================================================
    // ★Countdown Explosion（生成直後からカウントダウン → 範囲爆発）
    // =========================================================
    [Header("Countdown Explosion")]
    [Tooltip("ON: 生成直後から爆発タイマー開始し、時間で範囲爆発する")]
    [SerializeField] private bool useCountdownExplosion = false;

    [Tooltip("爆発までの秒数（生成直後からカウント）")]
    [SerializeField] private float explosionDelaySeconds = 2.0f;

    [Tooltip("爆発半径（ワールド座標）")]
    [SerializeField] private float explosionRadius = 1.25f;

    [Tooltip("敵(EnemyDamageReceiver)への爆発ダメージ")]
    [SerializeField] private int explosionDamageToEnemy = 2;

    [Tooltip("壁(WallHealth)への爆発ダメージ")]
    [SerializeField] private int explosionDamageToWall = 1;

    [Tooltip("プレイヤー(PixelDancerController)への爆発ダメージ")]
    [SerializeField] private int explosionDamageToPlayer = 1;

    [Tooltip("フロア(FloorHealth)への爆発ダメージ")]
    [SerializeField] private int explosionDamageToFloor = 1;

    [Tooltip("爆発の対象Layer。未設定(0)なら全Layer対象（Everything相当）。")]
    [SerializeField] private LayerMask explosionTargetLayers = 0;

    // =========================
    // ★追加：爆発 可視化（リング） + 点滅（加速ON/OFF）
    // =========================
    [Header("Explosion Visual (Radius / Blink)")]
    [Tooltip("ON: 爆発半径リングを弾の周囲に表示する（ゲーム中可視化）")]
    [SerializeField] private bool explosionShowRadius = true;

    [Tooltip("リングの線幅（ワールド）")]
    [SerializeField] private float explosionRadiusLineWidth = 0.03f;

    [Tooltip("リング色（Alpha含む）")]
    [SerializeField] private Color explosionRadiusColor = new Color(1f, 0.2f, 0.2f, 0.65f);

    [Tooltip("リング分割数（多いほど円が滑らか、重い）")]
    [SerializeField] private int explosionRadiusSegments = 48;

    [Tooltip("ON: 爆発が近づくほど弾をON/OFF点滅（どんどん速くなる）")]
    [SerializeField] private bool explosionBlinkEnabled = true;

    [Tooltip("残り時間がこの秒数以下になったら点滅開始")]
    [SerializeField] private float explosionBlinkStartSeconds = 1.2f;

    [Tooltip("点滅の最低周波数（Hz）。開始直後の遅い点滅")]
    [SerializeField] private float explosionBlinkMinHz = 2.0f;

    [Tooltip("点滅の最高周波数（Hz）。爆発直前の速い点滅")]
    [SerializeField] private float explosionBlinkMaxHz = 14.0f;

    // ランタイム
    private float explosionStartTime = -999f;
    private bool explosionTriggered = false;

    private bool explosionInitDone = false;

    // ★リング（LineRenderer）
    private GameObject explosionRingGo;
    private LineRenderer explosionRingLr;
    private bool explosionRingCreated = false;

    // ★点滅
    private bool explosionBlinkVisible = true;
    private float explosionNextBlinkToggleTime = -999f;

    private static readonly System.Collections.Generic.List<Collider2D> s_explosionHitList
        = new System.Collections.Generic.List<Collider2D>(64);

    // =========================================================
    // ★追加：重複排除のための visited（ローカルで使うが、再利用でGC抑制）
    // =========================================================
    private static readonly System.Collections.Generic.HashSet<int> s_explosionVisited
        = new System.Collections.Generic.HashSet<int>(128);

    // =========================================================
    // ★重要：EnemyShooter から呼ばれる注入API（CS1061対策）
    // =========================================================
    public void ApplyCountdownExplosion(bool enabled, float delaySeconds, float radius, int damageToEnemy, int damageToWall, int damageToPlayer = 1, int damageToFloor = 1)
    {
        useCountdownExplosion = enabled;
        explosionDelaySeconds = delaySeconds;
        explosionRadius = radius;
        explosionDamageToEnemy = damageToEnemy;
        explosionDamageToWall = damageToWall;
        explosionDamageToPlayer = damageToPlayer;
        explosionDamageToFloor = damageToFloor;

        explosionInitDone = false;

        explosionRingCreated = false;
        explosionBlinkVisible = true;
        explosionNextBlinkToggleTime = -999f;
    }

    private void EnsureExplosionInit()
    {
        if (explosionInitDone) return;
        explosionInitDone = true;

        if (useCountdownExplosion)
        {
            explosionStartTime = Time.time;
            explosionTriggered = false;
        }
        else
        {
            explosionStartTime = -999f;
            explosionTriggered = false;
        }
    }

    private void TickCountdownExplosion()
    {
        if (!useCountdownExplosion) return;
        if (explosionTriggered) return;
        if (isBeingDestroyed) return;

        float delay = Mathf.Max(0f, explosionDelaySeconds);

        if (explosionStartTime < -998f)
        {
            explosionStartTime = Time.time;
        }

        float t = Time.time - explosionStartTime;
        if (t >= delay)
        {
            TriggerExplosion();
        }
    }

    private void TriggerExplosion()
    {
        if (explosionTriggered) return;
        if (isBeingDestroyed) return;

        explosionTriggered = true;

        isBeingDestroyed = true;

        // VFX/SE 分離（爆発）
        if (feedback != null) feedback.OnExplosion(transform.position);

        ApplyExplosionDamage();

        DestroyExplosionRing();

        Destroy(gameObject);
    }

    private void ApplyExplosionDamage()
    {
        float r = Mathf.Max(0.01f, explosionRadius);

        LayerMask mask = explosionTargetLayers;
        if (mask == 0) mask = ~0;

        s_explosionHitList.Clear();

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = mask;
        filter.useTriggers = true;

        int hitCount = Physics2D.OverlapCircle(transform.position, r, filter, s_explosionHitList);
        if (hitCount <= 0) return;

        int dmgEnemy = Mathf.Max(0, explosionDamageToEnemy);
        int dmgWall = Mathf.Max(0, explosionDamageToWall);
        int dmgPlayer = Mathf.Max(0, explosionDamageToPlayer);
        int dmgFloor = Mathf.Max(0, explosionDamageToFloor);

        s_explosionVisited.Clear();

        int n = Mathf.Min(hitCount, s_explosionHitList.Count);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = s_explosionHitList[i];
            if (c == null) continue;

            if (c == bulletCol) continue;

            // ---- Enemy（優先度: EnemyPart > EnemyDamageReceiver > EnemyStats）
            if (dmgEnemy > 0)
            {
                // ★優先度1: EnemyPart（WeakPoint System 対応）
                EnemyPart part = c.GetComponent<EnemyPart>();
                if (part != null)
                {
                    int id = part.GetInstanceID();
                    if (s_explosionVisited.Add(id))
                    {
                        part.ApplyExplosionDamage(dmgEnemy, transform.position);
                    }
                }
                else
                {
                    // ★優先度2: EnemyDamageReceiver（従来の全体ダメージ）
                    EnemyDamageReceiver edr = c.GetComponent<EnemyDamageReceiver>();
                    if (edr == null) edr = c.GetComponentInParent<EnemyDamageReceiver>();

                    if (edr != null)
                    {
                        int id = edr.GetInstanceID();
                        if (s_explosionVisited.Add(id))
                        {
                            edr.ApplyExplosionDamage(dmgEnemy, transform.position);
                        }
                    }
                    else
                    {
                        // ★優先度3: フォールバック（EnemyStats 直付け/親）
                        EnemyStats es = c.GetComponent<EnemyStats>();
                        if (es == null) es = c.GetComponentInParent<EnemyStats>();
                        if (es != null)
                        {
                            int id = es.GetInstanceID();
                            if (s_explosionVisited.Add(id))
                            {
                                es.Damage(dmgEnemy);
                            }
                        }
                    }
                }
            }

            // ---- Wall（WallHealth 単位で一回だけ）
            if (dmgWall > 0)
            {
                WallHealth wh = c.GetComponent<WallHealth>();
                if (wh == null) wh = c.GetComponentInParent<WallHealth>();

                if (wh != null)
                {
                    int id = wh.GetInstanceID();
                    if (s_explosionVisited.Add(id))
                    {
                        wh.ApplyExplosionDamage(dmgWall, transform.position);
                    }
                }
            }

            // ---- Player（PixelDancerController 単位で一回だけ）
            if (dmgPlayer > 0)
            {
                PixelDancerController player = c.GetComponent<PixelDancerController>();
                if (player == null) player = c.GetComponentInParent<PixelDancerController>();

                if (player != null)
                {
                    int id = player.GetInstanceID();
                    if (s_explosionVisited.Add(id))
                    {
                        player.ApplyExplosionDamage(dmgPlayer);
                    }
                }
            }

            // ---- Floor（FloorHealth 単位で一回だけ）
            if (dmgFloor > 0)
            {
                FloorHealth floor = c.GetComponent<FloorHealth>();
                if (floor == null) floor = c.GetComponentInParent<FloorHealth>();

                if (floor != null)
                {
                    int id = floor.GetInstanceID();
                    if (s_explosionVisited.Add(id))
                    {
                        floor.ApplyExplosionDamage(dmgFloor);
                    }
                }
            }
        }
    }

    private float GetExplosionRemainingSeconds()
    {
        if (!useCountdownExplosion) return -1f;
        if (explosionStartTime < -998f) return Mathf.Max(0f, explosionDelaySeconds);
        float t = Time.time - explosionStartTime;
        return Mathf.Max(0f, explosionDelaySeconds - t);
    }

    private void EnsureExplosionRing()
    {
        if (!useCountdownExplosion) return;
        if (!explosionShowRadius) return;
        if (explosionRingCreated) return;

        explosionRingCreated = true;

        explosionRingGo = new GameObject("ExplosionRadiusRing");
        explosionRingGo.transform.position = transform.position;

        explosionRingLr = explosionRingGo.AddComponent<LineRenderer>();

        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null) explosionRingLr.material = new Material(sh);

        explosionRingLr.useWorldSpace = true;
        explosionRingLr.loop = true;

        int seg = Mathf.Clamp(explosionRadiusSegments, 12, 256);
        explosionRingLr.positionCount = seg;

        float w = Mathf.Max(0.001f, explosionRadiusLineWidth);
        explosionRingLr.startWidth = w;
        explosionRingLr.endWidth = w;

        explosionRingLr.startColor = explosionRadiusColor;
        explosionRingLr.endColor = explosionRadiusColor;

        explosionRingLr.numCapVertices = 4;
        explosionRingLr.numCornerVertices = 2;
    }

    private void TickExplosionRing()
    {
        if (!useCountdownExplosion) { DestroyExplosionRing(); return; }
        if (!explosionShowRadius) { DestroyExplosionRing(); return; }
        if (explosionTriggered || isBeingDestroyed) { DestroyExplosionRing(); return; }

        EnsureExplosionRing();
        if (explosionRingLr == null) return;

        explosionRingGo.transform.position = transform.position;

        int seg = explosionRingLr.positionCount;
        float r = Mathf.Max(0.01f, explosionRadius);

        for (int i = 0; i < seg; i++)
        {
            float a = (float)i / (float)seg * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * r;
            float y = Mathf.Sin(a) * r;
            explosionRingLr.SetPosition(i, new Vector3(transform.position.x + x, transform.position.y + y, transform.position.z));
        }
    }

    private void DestroyExplosionRing()
    {
        if (explosionRingGo != null)
        {
            Destroy(explosionRingGo);
            explosionRingGo = null;
            explosionRingLr = null;
        }
        explosionRingCreated = false;
    }

    private void SetBulletVisible(bool visible)
    {
        if (visualRenderer == null)
        {
            Transform v = transform.Find("Visual");
            if (v != null) visualRenderer = v.GetComponent<SpriteRenderer>();
        }
        if (overlayRenderer == null)
        {
            Transform o = transform.Find("JustOverlay");
            if (o != null) overlayRenderer = o.GetComponentInChildren<SpriteRenderer>();
        }

        if (visualRenderer != null) visualRenderer.enabled = visible;

        if (overlayRenderer != null)
        {
            if (!visible)
            {
                overlayRenderer.enabled = false;
            }
            else
            {
                ApplyVisualByState();
            }
        }
    }

    private void TickExplosionBlink()
    {
        if (!useCountdownExplosion) { SetBulletVisible(true); return; }
        if (!explosionBlinkEnabled) { SetBulletVisible(true); return; }
        if (explosionTriggered || isBeingDestroyed) return;

        float remain = GetExplosionRemainingSeconds();
        float start = Mathf.Max(0f, explosionBlinkStartSeconds);

        if (remain > start)
        {
            explosionBlinkVisible = true;
            explosionNextBlinkToggleTime = -999f;
            SetBulletVisible(true);
            return;
        }

        float t = (start <= 0.0001f) ? 1f : Mathf.Clamp01(1f - (remain / start));

        float minHz = Mathf.Max(0.1f, explosionBlinkMinHz);
        float maxHz = Mathf.Max(minHz, explosionBlinkMaxHz);

        float hz = Mathf.Lerp(minHz, maxHz, t);

        float halfPeriod = 0.5f / hz;

        float now = Time.time;
        if (explosionNextBlinkToggleTime < -998f)
        {
            explosionNextBlinkToggleTime = now + halfPeriod;
            explosionBlinkVisible = true;
            SetBulletVisible(true);
            return;
        }

        if (now >= explosionNextBlinkToggleTime)
        {
            explosionBlinkVisible = !explosionBlinkVisible;
            SetBulletVisible(explosionBlinkVisible);
            explosionNextBlinkToggleTime = now + halfPeriod;
        }
    }

    /// <summary>
    /// カウントダウンBeep SEの更新（点滅と同期）
    /// </summary>
    private void TickCountdownBeepSe()
    {
        if (!useCountdownExplosion) return;
        if (explosionTriggered || isBeingDestroyed) return;
        if (feedback == null) return;

        float remain = GetExplosionRemainingSeconds();
        float blinkStart = Mathf.Max(0f, explosionBlinkStartSeconds);

        feedback.TickCountdownBeep(remain, blinkStart);
    }

    private void OnDrawGizmosSelected()
    {
        if (!useCountdownExplosion) return;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, explosionRadius));
    }
}
