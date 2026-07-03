using UnityEngine;

public class EnemyHitFeedback : MonoBehaviour
{
    [Header("A: Damage Popup")]
    [SerializeField] private DamagePopup damagePopupPrefab;

    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.6f, 0f);

    // 0=従来(ヒット位置寄り), 1=敵アンカー寄り
    [Range(0f, 1f)]
    [SerializeField] private float popupPullToEnemy = 1.0f;

    // ★最重要：敵の「見た目中心」をどこから取るか
    public enum AnchorMode
    {
        TransformPosition,        // transform.position
        Collider2DBoundsCenter,   // Collider2D.bounds.center
        RendererBoundsCenter      // Renderer.bounds.center
    }

    [Header("Anchor")]
    [SerializeField] private AnchorMode anchorMode = AnchorMode.Collider2DBoundsCenter;

    // ★常に敵アンカーに固定（最も近づける）
    [SerializeField] private bool forcePopupAtAnchor = true;

    // ★ヒット座標が離れすぎていたら敵アンカーを使う（保険）
    [SerializeField] private bool replaceFarHitPosWithAnchor = true;
    [SerializeField] private float farHitDistance = 0.15f;

    // ★Z固定（3D TMPで前後ズレがある場合の保険）
    [SerializeField] private bool forcePopupZToAnchor = true;

    [Header("Popup Direction")]
    [Tooltip("斜め方向のXずれ幅（ワールド単位）。0で真上のみ。")]
    [SerializeField] private float popupLateralOffset = 0.3f;

    [Header("Gizmos Debug (Scene View)")]
    [SerializeField] private bool debugGizmos = true;
    [SerializeField] private float gizmoSphereRadius = 0.08f;
    [SerializeField] private bool debugLogOncePerHit = false;

    [SerializeField] private float popupNormalFontSize = 5.5f;
    [SerializeField] private float popupPoweredFontSize = 7.0f;

    [Header("Auto Popup Size")]
    [Tooltip("ONにするとスプライト幅に合わせてpopupFontSizeを自動調整する（上の固定値は無視される）")]
    [SerializeField] private bool autoPopupSize = false;
    [Tooltip("スプライト幅に対するnormalFontSizeの比率")]
    [SerializeField] private float popupSizeRatio = 7.0f;
    [Tooltip("normalFontSizeに対するpoweredFontSizeの倍率")]
    [SerializeField] private float poweredSizeMultiplier = 1.3f;

    [SerializeField] private Color popupNormalColor = Color.white;
    [SerializeField] private Color popupPoweredColor = Color.yellow;

    [Header("Shield Hit Popup Colors")]
    [SerializeField] private Color popupShieldNormalColor  = new Color(0.55f, 0.78f, 1.00f); // 薄い青
    [SerializeField] private Color popupShieldPoweredColor = new Color(0.10f, 0.35f, 0.90f); // 濃い青

    [Header("B: Hit VFX")]
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private GameObject poweredHitVfxPrefab;

    [Header("C: Hit SE")]
    [SerializeField] private AudioClip hitSe;
    [SerializeField] private AudioClip poweredHitSe;
    [SerializeField] private float seVolume = 1.0f;

    // ====== Popup direction pool (-1=左斜め, 0=真上, 1=右斜め) ======
    private int[] _dirPool;
    private int   _dirPoolIndex;

    // ====== Debug cached points (last hit) ======
    private bool hasDebugPoints = false;
    private Vector3 dbgHitPos;
    private Vector3 dbgAnchorPos;
    private Vector3 dbgPopupPos;

    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    public void PlayHitFeedback(int damage, bool isPowered, Vector3 hitWorldPos, bool isShieldHit = false, Vector3? anchorOverride = null)
    {
        Vector3 anchor = anchorOverride ?? GetAnchorWorld();

        Vector3 hitPosUsed = hitWorldPos;

        // ヒット位置が離れすぎているならアンカーに置き換え
        if (replaceFarHitPosWithAnchor)
        {
            float d = Vector3.Distance(hitPosUsed, anchor);
            if (d > Mathf.Max(0f, farHitDistance))
            {
                hitPosUsed = anchor;
            }
        }

        // ポップアップの基準位置
        Vector3 basePos = forcePopupAtAnchor ? anchor : Vector3.Lerp(hitPosUsed, anchor, popupPullToEnemy);
        int dir = NextPopupDirection();
        Vector3 p = basePos + popupOffset + new Vector3(dir * popupLateralOffset, 0f, 0f);

        if (forcePopupZToAnchor)
        {
            p.z = anchor.z;
        }

        // Debug points cache（Gizmosで常時見える）
        hasDebugPoints = true;
        dbgHitPos = hitWorldPos;      // 受け取った元のhit
        dbgAnchorPos = anchor;        // 実際のアンカー
        dbgPopupPos = p;              // 生成予定位置

        if (debugLogOncePerHit)
        {
            Debug.Log($"[EnemyHitFeedback] hit={dbgHitPos} anchor={dbgAnchorPos} popup={dbgPopupPos} mode={anchorMode} force={forcePopupAtAnchor}", this);
        }

        // A: Popup
        if (damagePopupPrefab != null)
        {
            float normalSize  = autoPopupSize ? GetEnemyWidth() * popupSizeRatio : popupNormalFontSize;
            float poweredSize = autoPopupSize ? normalSize * poweredSizeMultiplier : popupPoweredFontSize;
            DamagePopup pop = Instantiate(damagePopupPrefab, p, Quaternion.identity);
            Color normalCol  = isShieldHit ? popupShieldNormalColor  : popupNormalColor;
            Color poweredCol = isShieldHit ? popupShieldPoweredColor : popupPoweredColor;
            pop.Setup(damage, isPowered, normalSize, poweredSize, normalCol, poweredCol);
        }

        // B: VFX（VFXは当たり場所に出すのが自然なので hitWorldPos のまま）
        GameObject vfx = null;
        if (isPowered && poweredHitVfxPrefab != null) vfx = poweredHitVfxPrefab;
        else if (hitVfxPrefab != null) vfx = hitVfxPrefab;

        if (vfx != null)
        {
            Instantiate(vfx, hitWorldPos, Quaternion.identity);
        }

        // C: SE
        AudioClip clip = null;
        if (isPowered && poweredHitSe != null) clip = poweredHitSe;
        else if (hitSe != null) clip = hitSe;

        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, hitWorldPos, seVolume * MasterSEVolume);
        }
    }

    private int NextPopupDirection()
    {
        if (_dirPool == null || _dirPoolIndex >= _dirPool.Length)
        {
            _dirPool = new[] { -1, 0, 1 };
            for (int i = 2; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = _dirPool[i]; _dirPool[i] = _dirPool[j]; _dirPool[j] = tmp;
            }
            _dirPoolIndex = 0;
        }
        return _dirPool[_dirPoolIndex++];
    }

    private float GetEnemyWidth()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.bounds.size.x;
        Collider2D col = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>();
        if (col != null) return col.bounds.size.x;
        return 1f;
    }

    private Vector3 GetAnchorWorld()
    {
        switch (anchorMode)
        {
            case AnchorMode.Collider2DBoundsCenter:
            {
                Collider2D col = GetComponent<Collider2D>();
                if (col != null) return col.bounds.center;
                col = GetComponentInChildren<Collider2D>();
                if (col != null) return col.bounds.center;
                return transform.position;
            }
            case AnchorMode.RendererBoundsCenter:
            {
                Renderer r = GetComponent<Renderer>();
                if (r != null) return r.bounds.center;
                r = GetComponentInChildren<Renderer>();
                if (r != null) return r.bounds.center;
                return transform.position;
            }
            default:
                return transform.position;
        }
    }

    private void OnDrawGizmos()
    {
        if (!debugGizmos) return;
        if (!Application.isPlaying) return;
        if (!hasDebugPoints) return;

        // 注意：Gizmosは Sceneビュー右上「Gizmos」がONでないと表示されません
        float r = Mathf.Max(0.01f, gizmoSphereRadius);

        // hit（黄）
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(dbgHitPos, r);

        // anchor（水色）
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(dbgAnchorPos, r);

        // popup（紫）
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(dbgPopupPos, r);

        // 線（緑：anchor→popup、黄：hit→anchor）
        Gizmos.color = Color.green;
        Gizmos.DrawLine(dbgAnchorPos, dbgPopupPos);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(dbgHitPos, dbgAnchorPos);
    }
}
