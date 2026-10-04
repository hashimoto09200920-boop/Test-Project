using System.Collections;
using UnityEngine;

/// <summary>
/// NeonDancer後半の「敵の線」1本分（見た目と寿命）。当たり判定の処理はEnemyLineReflectorが行う。
/// 流れ：予告線（薄い点線）→ 端から伸びて実体化 → 寿命（実体化しきってから数える）→ フェードして消える。
/// 実行時にNeonDancerControllerが生成する（ボスの階層の外に置く）。
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerEnemyLine : MonoBehaviour
{
    public class Settings
    {
        public float width = 0.06f;
        public float telegraphDuration = 0.5f;
        public float telegraphWidth = 0.04f;
        public float telegraphAlpha = 0.4f;
        public float telegraphDashLength = 0.15f;
        public float growDuration = 0.3f;
        public float lifetime = 3f;
        public float fadeOutDuration = 0.3f;
        public float colliderExtraRadius = 0.02f;
        public int layer = 6;
        public string sortingLayerName = "Default";
        public int sortingOrder = 1100;
        public Material material;
    }

    public EnemyLineReflector Reflector { get; private set; }
    public bool IsFinished { get; private set; }

    private LineRenderer solid;
    private LineRenderer telegraph;
    private EdgeCollider2D edge;
    private Vector3[] points;
    private Settings s;
    private Color color;
    private Material solidMat;
    private Material telegraphMat;
    private Texture2D dashTex;

    private static float TimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    public static NeonDancerEnemyLine Create(Vector3[] pathPoints, Color lineColor, Settings settings)
    {
        var go = new GameObject("ND_EnemyLine");
        go.layer = settings.layer;
        var line = go.AddComponent<NeonDancerEnemyLine>();
        line.Init(pathPoints, lineColor, settings);
        return line;
    }

    private void Init(Vector3[] pathPoints, Color lineColor, Settings settings)
    {
        points = pathPoints;
        s = settings;
        color = lineColor;

        Reflector = gameObject.AddComponent<EnemyLineReflector>();
        Reflector.IsSolid = false;

        edge = gameObject.AddComponent<EdgeCollider2D>();
        edge.edgeRadius = s.width * 0.5f + s.colliderExtraRadius;
        edge.enabled = false;

        Shader sh = s.material != null ? null : Shader.Find("Sprites/Default");
        solidMat = s.material != null ? new Material(s.material) : (sh != null ? new Material(sh) : null);
        telegraphMat = s.material != null ? new Material(s.material) : (sh != null ? new Material(sh) : null);

        solid = CreateLineRenderer("Solid", solidMat, s.width);
        solid.positionCount = 0;

        // 予告線：点線（短い破線のテクスチャを線に沿って繰り返す）
        var tgo = new GameObject("Telegraph");
        tgo.transform.SetParent(transform, false);
        telegraph = tgo.AddComponent<LineRenderer>();
        SetupLineRenderer(telegraph, telegraphMat, s.telegraphWidth);
        dashTex = new Texture2D(8, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        for (int i = 0; i < 8; i++) dashTex.SetPixel(i, 0, i < 4 ? Color.white : Color.clear);
        dashTex.Apply();
        if (telegraphMat != null)
        {
            telegraphMat.mainTexture = dashTex;
            telegraphMat.mainTextureScale = new Vector2(1f / Mathf.Max(0.01f, s.telegraphDashLength * 2f), 1f);
        }
        telegraph.textureMode = LineTextureMode.Tile;
        telegraph.positionCount = points.Length;
        telegraph.SetPositions(points);
        Color tc = color; tc.a = s.telegraphAlpha;
        telegraph.startColor = tc; telegraph.endColor = tc;

        StartCoroutine(LifeRoutine());
    }

    private LineRenderer CreateLineRenderer(string name, Material mat, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        SetupLineRenderer(lr, mat, width);
        lr.startColor = color; lr.endColor = color;
        return lr;
    }

    private void SetupLineRenderer(LineRenderer lr, Material mat, float width)
    {
        if (mat != null) lr.sharedMaterial = mat;
        lr.useWorldSpace = true;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.sortingLayerName = s.sortingLayerName;
        lr.sortingOrder = s.sortingOrder;
    }

    private IEnumerator LifeRoutine()
    {
        // ① 予告線
        float t = 0f;
        while (t < s.telegraphDuration)
        {
            t += Time.deltaTime * TimeScale;
            yield return null;
        }
        telegraph.enabled = false;

        // ② 端から伸びて実体化（伸びた分だけ当たり判定がある）
        Reflector.IsSolid = true;
        edge.enabled = true;
        float total = PathLength();
        t = 0f;
        float grow = Mathf.Max(0.01f, s.growDuration);
        while (t < grow)
        {
            t += Time.deltaTime * TimeScale;
            ApplyPartial(total * Mathf.Clamp01(t / grow));
            yield return null;
        }
        ApplyPartial(total);

        // ③ 寿命（実体化しきってから数える。最後のfadeOutDuration秒で薄くなる）
        float life = Mathf.Max(0.01f, s.lifetime);
        float fade = Mathf.Clamp(s.fadeOutDuration, 0f, life);
        t = 0f;
        while (t < life)
        {
            t += Time.deltaTime * TimeScale;
            float remain = life - t;
            if (fade > 0f && remain < fade)
            {
                Color c = color; c.a = color.a * Mathf.Clamp01(remain / fade);
                solid.startColor = c; solid.endColor = c;
            }
            yield return null;
        }

        Finish();
    }

    /// <summary>即座に消す（ボス撃破時など）</summary>
    public void Finish()
    {
        if (IsFinished) return;
        IsFinished = true;
        if (Reflector != null) Reflector.IsSolid = false;
        if (edge != null) edge.enabled = false;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (solidMat != null) Destroy(solidMat);
        if (telegraphMat != null) Destroy(telegraphMat);
        if (dashTex != null) Destroy(dashTex);
    }

    private float PathLength()
    {
        float len = 0f;
        for (int i = 1; i < points.Length; i++) len += Vector3.Distance(points[i - 1], points[i]);
        return len;
    }

    // 始点から指定の長さまでを線と当たり判定に反映する
    private void ApplyPartial(float length)
    {
        var list = new System.Collections.Generic.List<Vector3> { points[0] };
        float acc = 0f;
        for (int i = 1; i < points.Length; i++)
        {
            float d = Vector3.Distance(points[i - 1], points[i]);
            if (acc + d >= length)
            {
                float k = d > 0.0001f ? (length - acc) / d : 1f;
                list.Add(Vector3.Lerp(points[i - 1], points[i], k));
                break;
            }
            list.Add(points[i]);
            acc += d;
        }
        if (list.Count < 2) list.Add(points[0] + (points.Length > 1 ? (points[1] - points[0]).normalized * 0.01f : Vector3.right * 0.01f));

        solid.positionCount = list.Count;
        solid.SetPositions(list.ToArray());

        var pts = new Vector2[list.Count];
        for (int i = 0; i < list.Count; i++) pts[i] = list[i];
        edge.points = pts; // オブジェクトは原点・回転なしなので、ワールド座標をそのままローカルとして使える
    }
}
