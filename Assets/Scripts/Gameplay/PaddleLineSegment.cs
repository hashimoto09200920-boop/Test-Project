using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ★線の当たり判定の新方式（PaddleDrawer の Line Collider Mode = Segment の時だけ使う。2026/11/03）。
///
/// 従来は線の点（PaddleDot、0.01間隔）1個ずつに当たり判定があり、弾1発が直線に触れるだけで約36個の点と同時に接触して、
/// スマホでは物理エンジンの計算が追いつかず数秒止まっていた（計測で確認）。
/// 新方式では、点の見た目はそのままにして点の当たり判定を切り、線に沿った短い区間（Segment Length）ごとに
/// 細長い当たり判定（カプセル形）を1つ置く。弾1発あたりの接触は数か所になる。
///
/// ・反射・貫通・ジャスト・ドリル弾などの処理は、当たった位置にいちばん近い点（PaddleDot）にそのまま任せる（処理の中身は従来と同じ）。
/// ・区間は、その区間に属する点がすべて消えた時に消える（円の延長寿命・線の破断にも自動で追従する）。
/// ・物理素材・レイヤー・太さは線の点のプレハブから引き継ぐ（Inspectorで設定する物は無い）。
/// ・使い回し（プール）するため、生成・破棄の負荷も小さい。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PaddleLineSegment : MonoBehaviour
{
    private Rigidbody2D rb;
    private CapsuleCollider2D capsule;
    private Stroke ownerStroke;

    private readonly List<PaddleDot> dots = new List<PaddleDot>(32);
    private int aliveCount;

    private bool hasAnchor;          // 前の区間の終点（隙間ができないよう、ここから区間を始める）
    private Vector2 anchor;
    private Vector2 lastPoint;       // 最後に加えた点の位置
    private float radius = 0.01f;    // 線の点の当たり判定の半径（ワールド座標）
    private float thicknessScale = 1f;

    // 衝突を無視した弾（使い回し時に必ず解除する。PaddleDotと同じ理由）
    private readonly List<Collider2D> ignoredBulletColliders = new List<Collider2D>(4);

    public Collider2D Collider => capsule;
    public Stroke OwnerStroke => ownerStroke;
    /// <summary>この区間の長さ（始点〜最後の点）</summary>
    public float Length => (lastPoint - StartPoint).magnitude;
    private Vector2 StartPoint => hasAnchor ? anchor : (dots.Count > 0 && dots[0] != null ? (Vector2)dots[0].transform.position : lastPoint);

    // =========================================================
    // プール
    // =========================================================
    private static readonly Stack<PaddleLineSegment> s_pool = new Stack<PaddleLineSegment>(64);

    /// <summary>区間を1つ借りる。template（線の点のプレハブ）から物理素材・レイヤー・半径を引き継ぐ</summary>
    public static PaddleLineSegment Get(PaddleDot template, Transform parent, Stroke owner)
    {
        PaddleLineSegment seg = null;
        while (s_pool.Count > 0 && seg == null) seg = s_pool.Pop(); // シーンを抜けて破棄された物は捨てる
        if (seg == null)
        {
            var go = new GameObject("PaddleLineSegment");
            go.SetActive(false);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            go.AddComponent<CapsuleCollider2D>();
            seg = go.AddComponent<PaddleLineSegment>();
        }
        seg.transform.SetParent(parent, false);
        seg.Setup(template, owner);
        seg.gameObject.SetActive(true);
        return seg;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        capsule = GetComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Horizontal;
    }

    private void Setup(PaddleDot template, Stroke owner)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (capsule == null) { capsule = GetComponent<CapsuleCollider2D>(); capsule.direction = CapsuleDirection2D.Horizontal; }

        ownerStroke = owner;
        dots.Clear();
        aliveCount = 0;
        hasAnchor = false;
        thicknessScale = 1f;

        // 線の点のプレハブと同じ物理素材・レイヤー・当たり判定の太さにする
        radius = 0.01f;
        if (template != null)
        {
            gameObject.layer = template.gameObject.layer;
            var tc = template.GetComponent<CircleCollider2D>();
            if (tc != null)
            {
                Vector3 s = template.transform.localScale;
                radius = Mathf.Max(0.0001f, tc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y)));
                capsule.sharedMaterial = tc.sharedMaterial;
                capsule.isTrigger = tc.isTrigger;
            }
        }
        rb.bodyType = RigidbodyType2D.Static;
    }

    private void OnEnable()
    {
        if (capsule != null) PaddleDot.RegisterLineCollider(capsule);
    }

    private void OnDisable()
    {
        if (capsule != null) PaddleDot.UnregisterLineCollider(capsule);
        // 衝突の無視を解除しておく（次に別の線として使い回した時、すり抜けないように）
        for (int i = 0; i < ignoredBulletColliders.Count; i++)
        {
            Collider2D col = ignoredBulletColliders[i];
            if (col != null && capsule != null) Physics2D.IgnoreCollision(capsule, col, false);
        }
        ignoredBulletColliders.Clear();
    }

    private void OnDestroy()
    {
        if (capsule != null) PaddleDot.UnregisterLineCollider(capsule);
    }

    /// <summary>点が残っていても区間を片付ける（線が破棄される時の念のための後始末。Stroke.ReleaseAllSegmentsから呼ぶ）</summary>
    public void ForceRelease()
    {
        if (!gameObject.activeSelf) return; // すでにプールに戻っている
        for (int i = 0; i < dots.Count; i++)
        {
            PaddleDot d = dots[i];
            if (d != null && d.lineSegment == this) d.lineSegment = null; // 点が後で消えた時に、使い回し中の区間へ知らせないように
        }
        Release();
    }

    private void Release()
    {
        dots.Clear();
        aliveCount = 0;
        ownerStroke = null;
        gameObject.SetActive(false);
        s_pool.Push(this);
    }

    // =========================================================
    // 点の追加・削除
    // =========================================================
    /// <summary>前の区間の終点。ここから区間を始めることで、区間と区間の間に隙間ができない</summary>
    public void SetAnchor(Vector2 p)
    {
        hasAnchor = true;
        anchor = p;
    }

    public void AddDot(PaddleDot dot)
    {
        if (dot == null) return;
        dots.Add(dot);
        aliveCount++;
        lastPoint = dot.transform.position;
        UpdateShape();
    }

    /// <summary>点が寿命・破断で消えた時（Stroke.UnregisterDotから呼ぶ）。全部消えたら区間も消えてtrueを返す</summary>
    public bool OnDotRemoved(PaddleDot dot)
    {
        int i = dots.IndexOf(dot); // 1区間の点は数十個なので探しても軽い
        if (i >= 0) dots[i] = null;
        aliveCount = Mathf.Max(0, aliveCount - 1);
        if (aliveCount > 0) return false;
        Release();
        return true;
    }

    /// <summary>円が成立した時に、点と同じだけ太くする（PaddleDot.ApplyCircleVisual と同じ倍率）</summary>
    public void SetThicknessScale(float scale)
    {
        thicknessScale = Mathf.Max(1f, scale);
        UpdateShape();
    }

    private void UpdateShape()
    {
        Vector2 a = StartPoint;
        Vector2 b = lastPoint;
        Vector2 d = b - a;
        float len = d.magnitude;
        float r = radius * thicknessScale;
        float angle = len > 0.00001f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f;
        Vector3 center = (a + b) * 0.5f;
        center.z = dots.Count > 0 && dots[dots.Count - 1] != null ? dots[dots.Count - 1].transform.position.z : 0f;
        transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
        transform.localScale = Vector3.one;
        capsule.offset = Vector2.zero;
        capsule.size = new Vector2(len + r * 2f, r * 2f);
    }

    // =========================================================
    // 当たった位置にいちばん近い点（反射などの処理はこの点に任せる）
    // =========================================================
    public PaddleDot NearestDot(Vector2 point)
    {
        PaddleDot best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < dots.Count; i++)
        {
            PaddleDot d = dots[i];
            if (d == null || !d.gameObject.activeInHierarchy) continue;
            float sq = ((Vector2)d.transform.position - point).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = d; }
        }
        return best;
    }

    /// <summary>位置が分からない時の代表の点（区間の真ん中に近い、まだ残っている点）</summary>
    public PaddleDot AnyDot()
    {
        int mid = dots.Count / 2;
        for (int k = 0; k < dots.Count; k++)
        {
            int i = (mid + k) % dots.Count;
            PaddleDot d = dots[i];
            if (d != null && d.gameObject.activeInHierarchy) return d;
        }
        return null;
    }

    public void IgnoreBullet(Collider2D bulletCol)
    {
        if (capsule == null || bulletCol == null) return;
        Physics2D.IgnoreCollision(capsule, bulletCol, true);
        if (!ignoredBulletColliders.Contains(bulletCol)) ignoredBulletColliders.Add(bulletCol);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null) return;
        Vector2 p = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)collision.transform.position;
        PaddleDot dot = NearestDot(p);
        if (dot == null) return;
        dot.HandleBulletCollision(collision, this);
    }
}
