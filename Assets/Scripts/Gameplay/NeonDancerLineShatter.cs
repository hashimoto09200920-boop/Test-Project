using UnityEngine;

/// <summary>
/// NeonDancerの「敵の線」が壊れた時の、線が砕け散る演出（NeonDancer専用）。
/// 線を短い破片に分け、壊れた位置から外へ弾けるように飛ばし、回転・落下させながらフェードして消す。
/// 破片の周りに光の粒も散らす。
/// ★負荷軽減：演出が終わったら破棄せず非表示にして取っておき、次に壊れた時に破片ごと使い回す
///   （使い回す時は位置・回転・色・太さ・描画順をすべて新規作成時と同じ状態に設定し直す）。
/// </summary>
public class NeonDancerLineShatter : MonoBehaviour
{
    public class Settings
    {
        public int shardCount = 18;
        public Vector2 shardSpeed = new Vector2(1.5f, 4f);
        public Vector2 shardSpin = new Vector2(180f, 720f);
        public float gravity = 4f;
        public float duration = 0.7f;
        public int sparkCount = 24;
        public Vector2 sparkSpeed = new Vector2(2f, 6f);
        public float sparkSize = 0.06f;
    }

    private class Piece
    {
        public Transform t;
        public LineRenderer lr;
        public Vector2 vel;
        public float spin;
        public float width;
    }

    private Piece[] pieces;
    private float elapsed;
    private Settings s;
    private Color color;
    private Material mat;
    private Material matSource;       // matの元（Spawnに渡されたマテリアル。nullならSprites/Default）
    private bool matCreated;
    private readonly System.Collections.Generic.List<Piece> pieceCache = new System.Collections.Generic.List<Piece>();

    private static readonly System.Collections.Generic.Stack<NeonDancerLineShatter> s_pool =
        new System.Collections.Generic.Stack<NeonDancerLineShatter>();
    private static Shader s_defaultShader;

    public static void Spawn(Vector3[] linePoints, Vector3 breakPoint, Color lineColor, float lineWidth, Material material,
                             string sortingLayerName, int sortingOrder, Settings settings)
    {
        if (linePoints == null || linePoints.Length < 2) return;
        NeonDancerLineShatter fx = null;
        while (fx == null && s_pool.Count > 0) fx = s_pool.Pop(); // シーン切替などで破棄済みのものは飛ばす
        if (fx == null)
        {
            var go = new GameObject("ND_EnemyLineShatter");
            fx = go.AddComponent<NeonDancerLineShatter>();
        }
        else fx.gameObject.SetActive(true);
        fx.Init(linePoints, breakPoint, lineColor, lineWidth, material, sortingLayerName, sortingOrder, settings);
    }

    private void Init(Vector3[] linePoints, Vector3 breakPoint, Color lineColor, float lineWidth, Material material,
                      string sortingLayerName, int sortingOrder, Settings settings)
    {
        s = settings;
        color = lineColor;
        elapsed = 0f;
        if (!matCreated || matSource != material)
        {
            if (mat != null) Destroy(mat);
            if (material == null && s_defaultShader == null) s_defaultShader = Shader.Find("Sprites/Default");
            Shader sh = material != null ? null : s_defaultShader;
            mat = material != null ? new Material(material) : (sh != null ? new Material(sh) : null);
            matSource = material;
            matCreated = true;
        }

        // 線を等間隔の破片に分ける
        float total = 0f;
        for (int i = 1; i < linePoints.Length; i++) total += Vector3.Distance(linePoints[i - 1], linePoints[i]);
        int n = Mathf.Max(1, s.shardCount);
        float pieceLen = total / n;
        int pieceCount = n + Mathf.Max(0, s.sparkCount);
        pieces = new Piece[pieceCount];
        usedPieces = 0;

        for (int k = 0; k < n; k++)
        {
            Vector3 a = PointAt(linePoints, pieceLen * k);
            Vector3 b = PointAt(linePoints, pieceLen * (k + 1));
            Vector3 mid = (a + b) * 0.5f;
            // 壊れた位置から外へ弾ける（近い破片ほど速い）＋少しばらつき
            Vector2 away = (Vector2)(mid - breakPoint);
            float dist = away.magnitude;
            Vector2 dir = dist > 0.001f ? away / dist : Random.insideUnitCircle.normalized;
            dir = (dir + Random.insideUnitCircle * 0.6f).normalized;
            float speed = Mathf.Lerp(Mathf.Max(s.shardSpeed.x, s.shardSpeed.y), Mathf.Min(s.shardSpeed.x, s.shardSpeed.y), Mathf.Clamp01(dist / Mathf.Max(0.01f, total * 0.5f)));
            pieces[k] = SetupPiece(mid, (b - a) * 0.5f, lineWidth, dir * speed,
                Random.Range(Mathf.Min(s.shardSpin.x, s.shardSpin.y), Mathf.Max(s.shardSpin.x, s.shardSpin.y)) * (Random.value < 0.5f ? -1f : 1f),
                sortingLayerName, sortingOrder);
        }
        // 光の粒（ごく短い線＝点として描く）
        for (int k = 0; k < s.sparkCount; k++)
        {
            Vector3 p = PointAt(linePoints, Random.Range(0f, total));
            Vector2 dir = Random.insideUnitCircle.normalized;
            float speed = Random.Range(Mathf.Min(s.sparkSpeed.x, s.sparkSpeed.y), Mathf.Max(s.sparkSpeed.x, s.sparkSpeed.y));
            pieces[n + k] = SetupPiece(p, (Vector3)(dir * s.sparkSize * 0.5f), s.sparkSize, dir * speed, 0f, sortingLayerName, sortingOrder + 1);
        }
        // 前回より破片が少ない時、余った破片は隠しておく
        for (int i = usedPieces; i < pieceCache.Count; i++)
            if (pieceCache[i] != null && pieceCache[i].t != null) pieceCache[i].t.gameObject.SetActive(false);
    }

    private int usedPieces;

    // 取っておいた破片を使い回す（無ければ作る）。設定は毎回すべて新規作成時と同じにする
    private Piece SetupPiece(Vector3 center, Vector3 halfVec, float width, Vector2 vel, float spin, string sortingLayerName, int sortingOrder)
    {
        Piece p;
        if (usedPieces < pieceCache.Count && pieceCache[usedPieces] != null && pieceCache[usedPieces].t != null)
        {
            p = pieceCache[usedPieces];
            p.t.gameObject.SetActive(true);
            p.t.localRotation = Quaternion.identity;
            p.t.localScale = Vector3.one;
            p.t.position = center;
            LineRenderer lr = p.lr;
            if (mat != null) lr.sharedMaterial = mat;
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, -halfVec);
            lr.SetPosition(1, halfVec);
            lr.startWidth = width; lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.sortingLayerName = sortingLayerName;
            lr.sortingOrder = sortingOrder;
            lr.startColor = color; lr.endColor = color;
            p.vel = vel; p.spin = spin; p.width = width;
        }
        else
        {
            p = CreatePiece(center, halfVec, width, vel, spin, sortingLayerName, sortingOrder);
            if (usedPieces < pieceCache.Count) pieceCache[usedPieces] = p;
            else pieceCache.Add(p);
        }
        usedPieces++;
        return p;
    }

    private Piece CreatePiece(Vector3 center, Vector3 halfVec, float width, Vector2 vel, float spin, string sortingLayerName, int sortingOrder)
    {
        var go = new GameObject("Shard");
        go.transform.SetParent(transform, false);
        go.transform.position = center;
        var lr = go.AddComponent<LineRenderer>();
        if (mat != null) lr.sharedMaterial = mat;
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, -halfVec);
        lr.SetPosition(1, halfVec);
        lr.startWidth = width; lr.endWidth = width;
        lr.numCapVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;
        lr.startColor = color; lr.endColor = color;
        return new Piece { t = go.transform, lr = lr, vel = vel, spin = spin, width = width };
    }

    private static Vector3 PointAt(Vector3[] pts, float length)
    {
        float acc = 0f;
        for (int i = 1; i < pts.Length; i++)
        {
            float d = Vector3.Distance(pts[i - 1], pts[i]);
            if (acc + d >= length) return Vector3.Lerp(pts[i - 1], pts[i], d > 0.0001f ? (length - acc) / d : 0f);
            acc += d;
        }
        return pts[pts.Length - 1];
    }

    private void Update()
    {
        float dt = Time.deltaTime * (SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f);
        elapsed += dt;
        float k = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, s.duration));
        Color c = color; c.a = color.a * (1f - k);
        foreach (var p in pieces)
        {
            if (p == null || p.t == null) continue;
            p.vel += Vector2.down * s.gravity * dt;
            p.t.position += (Vector3)(p.vel * dt);
            p.t.Rotate(0f, 0f, p.spin * dt);
            p.lr.startColor = c; p.lr.endColor = c;
            float w = p.width * (1f - k * 0.5f);
            p.lr.startWidth = w; p.lr.endWidth = w;
        }
        if (k >= 1f)
        {
            // 破棄せず隠して取っておく（次に線が壊れた時に使い回す）
            gameObject.SetActive(false);
            s_pool.Push(this);
        }
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
