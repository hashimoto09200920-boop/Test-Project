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

    [Header("Gizmos Debug (Scene View)")]
    [SerializeField] private bool debugGizmos = true;
    [SerializeField] private float gizmoSphereRadius = 0.08f;
    [SerializeField] private bool debugLogOncePerHit = false;

    [SerializeField] private float popupNormalFontSize = 5.5f;
    [SerializeField] private float popupPoweredFontSize = 7.0f;

    [SerializeField] private Color popupNormalColor = Color.white;
    [SerializeField] private Color popupPoweredColor = Color.yellow;

    [Header("B: Hit VFX")]
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private GameObject poweredHitVfxPrefab;

    [Header("C: Hit SE")]
    [SerializeField] private AudioClip hitSe;
    [SerializeField] private AudioClip poweredHitSe;
    [SerializeField] private float seVolume = 1.0f;

    // ====== Debug cached points (last hit) ======
    private bool hasDebugPoints = false;
    private Vector3 dbgHitPos;
    private Vector3 dbgAnchorPos;
    private Vector3 dbgPopupPos;

    // 既存API維持（EnemyDamageReceiver.cs が (int, bool, Vector3) で呼ぶ）
    public void PlayHitFeedback(int damage, bool isPowered, Vector3 hitWorldPos)
    {
        Vector3 anchor = GetAnchorWorld();

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
        Vector3 p = basePos + popupOffset;

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
            DamagePopup pop = Instantiate(damagePopupPrefab, p, Quaternion.identity);
            pop.Setup(damage, isPowered, popupNormalFontSize, popupPoweredFontSize, popupNormalColor, popupPoweredColor);
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
            AudioSource.PlayClipAtPoint(clip, hitWorldPos, seVolume);
        }
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
