using System.Collections.Generic;
using UnityEngine;

public class Stroke : MonoBehaviour
{
    public PaddleDot.LineType Type { get; private set; }
    public bool IsFinished { get; private set; }
    public bool IsCircle { get; private set; }
    public bool HasCircleBounds { get; private set; }
    public Bounds CircleBounds { get; private set; }

    /// <summary>ミッション用：この線で反射した弾の数（2個目で「複数の弾を反射」を1回数える。MissionProgressManager.OnLineReflect）</summary>
    public int MissionReflectCount { get; set; }
    private bool missionLineCounted;

    private StrokeManager manager;
    private int aliveDotCount = 0;

    private float finishGraceTime = 0.0f;
    private float finishTimer = 0.0f;

    private Vector3 startPos;
    private Vector3 endPos;
    private bool hasStart;
    private bool hasEnd;

    private float circleExtraLife = 1.0f;
    private float circleCloseDistance = 0.6f;

    private int circleMinDots = 0;
    private float circleMinPerimeter = 0f;
    private float circleMinBoundsSize = 0f;
    private float circleMaxAspect = 999f;

    private readonly List<PaddleDot> dots = new List<PaddleDot>();
    // ★負荷対策：外した点はすぐ詰めずにnullにしておき、後でまとめて詰める（removedCount＝null の数）。
    //   以前は List.Remove で1点ごとに先頭から探していたため、円の全点が同じフレームに寿命切れになると
    //   点の数の2乗の処理になっていた。詰めた後のリストの中身・順番は以前と同じ
    private int removedCount;

    // 始点Dot（最初に登録されたDot）
    private PaddleDot firstDot;

    // ★線の当たり判定の新方式（PaddleLineSegment）：この線の区間の一覧と、今伸ばしている途中の区間。2026/11/03
    private readonly List<PaddleLineSegment> segments = new List<PaddleLineSegment>();
    private PaddleLineSegment openSegment;
    private bool hasLastSegmentEnd;
    private Vector2 lastSegmentEnd;

    // ★デバッグ：ストローク開始時刻
    private float strokeStartTime;

    // ★追加：円を許可する制限時間（秒）
    // -1 のときは制限なし（ただし今回は必ずセットする運用）
    private float circleGateSeconds = -1f;

    // ★デバッグ：最後の円判定情報（画面表示用）
    public static float Debug_LastCloseSeconds { get; private set; }
    public static bool Debug_LastCircleResult { get; private set; }
    public static string Debug_LastReason { get; private set; } = "";
    public static string Debug_LastType { get; private set; } = "";

    public void Initialize(StrokeManager owner, PaddleDot.LineType type)
    {
        manager = owner;
        Type = type;

        IsFinished = false;
        IsCircle = false;
        HasCircleBounds = false;

        aliveDotCount = 0;
        finishGraceTime = 0f;
        finishTimer = 0f;

        hasStart = false;
        hasEnd = false;

        dots.Clear();
        removedCount = 0;
        firstDot = null;

        ReleaseAllSegments();

        strokeStartTime = Time.time;
        circleGateSeconds = -1f;
    }

    // ★Begin直後に呼ぶ（開始時刻を確実に）
    public void MarkStrokeStartNow()
    {
        strokeStartTime = Time.time;
    }

    // ★追加：PaddleDrawerから「猶予秒」を渡す（＝LifeTime秒）
    public void SetCircleGateSeconds(float seconds)
    {
        circleGateSeconds = Mathf.Max(0f, seconds);
    }

    public void ConfigureCircleRule(
        float closeDistance,
        float extraLifeSeconds,
        int minDots,
        float minPerimeter,
        float minBoundsSize,
        float maxAspect
    )
    {
        circleCloseDistance = Mathf.Max(0.01f, closeDistance);
        circleExtraLife = Mathf.Max(0f, extraLifeSeconds);

        circleMinDots = Mathf.Max(0, minDots);
        circleMinPerimeter = Mathf.Max(0f, minPerimeter);
        circleMinBoundsSize = Mathf.Max(0f, minBoundsSize);
        circleMaxAspect = Mathf.Max(1.0f, maxAspect);
    }

    public void SetStartPos(Vector3 p) { startPos = p; hasStart = true; }
    public void SetEndPos(Vector3 p) { endPos = p; hasEnd = true; }

    public void RegisterDot(PaddleDot dot)
    {
        aliveDotCount++;
        if (dot != null) { dot.strokeIndex = dots.Count; dots.Add(dot); }

        // ミッション：2つ目の点ができた＝少しでも線を引いた時に1本と数える（タップだけ・連打では数えない）
        if (!missionLineCounted && dots.Count >= 2)
        {
            missionLineCounted = true;
            MissionProgressManager.OnLineDrawn();
        }

        if (firstDot == null && dot != null)
        {
            firstDot = dot;
        }
    }

    /// <summary>
    /// 指定した点の付近の「線の向き」（描いた順の前後range個の点を結んだ向き。単位ベクトル）を返す。
    /// 線は円（PaddleDot）を並べて作るため、円の表面の向き（RaycastHit2D.normal）は当たる位置でぶれる。
    /// 線そのものの向きで反射させたい時に使う（現在はビームの反射のみ）。求められなければfalse
    /// </summary>
    public bool TryGetLocalDirection(PaddleDot dot, int range, out Vector2 direction)
    {
        direction = Vector2.zero;
        CompactDots();
        int i = dot != null && dot.strokeIndex >= 0 && dot.strokeIndex < dots.Count && dots[dot.strokeIndex] == dot ? dot.strokeIndex : dots.IndexOf(dot);
        if (i < 0 || dots.Count < 2) return false;
        range = Mathf.Max(1, range);
        PaddleDot a = null, b = null;
        for (int k = Mathf.Max(0, i - range); k <= i && a == null; k++)
            if (dots[k] != null && dots[k].gameObject.activeInHierarchy) a = dots[k];
        for (int k = Mathf.Min(dots.Count - 1, i + range); k >= i && b == null; k--)
            if (dots[k] != null && dots[k].gameObject.activeInHierarchy) b = dots[k];
        if (a == null || b == null || a == b) return false;
        Vector2 d = (Vector2)(b.transform.position - a.transform.position);
        if (d.sqrMagnitude < 0.0001f) return false;
        direction = d.normalized;
        return true;
    }

    // =========================================================
    // ★線の当たり判定の新方式（PaddleLineSegment）。PaddleDrawer の Line Collider Mode = Segment の時だけ使う
    // =========================================================
    /// <summary>
    /// 新しく置いた点を、今伸ばしている区間に加える。区間の長さが segmentLength に達したら区切り、次の点から新しい区間を始める
    /// （新しい区間は前の区間の終点から始めるので、区間の間に隙間はできない）
    /// </summary>
    public void AttachDotToSegment(PaddleDot dot, float segmentLength, PaddleDot template)
    {
        if (dot == null) return;
        if (openSegment == null)
        {
            openSegment = PaddleLineSegment.Get(template, null, this);
            if (hasLastSegmentEnd) openSegment.SetAnchor(lastSegmentEnd);
            if (IsCircle) openSegment.SetThicknessScale(dot.CircleThicknessScale);
            segments.Add(openSegment);
        }
        openSegment.AddDot(dot);
        dot.lineSegment = openSegment;

        if (openSegment.Length >= Mathf.Max(0.01f, segmentLength))
        {
            lastSegmentEnd = dot.transform.position;
            hasLastSegmentEnd = true;
            openSegment = null;
        }
    }

    private void ReleaseAllSegments()
    {
        // 通常は点が全部消えた時点で区間も消えているため空。念のための後始末（区間はシーン直下にあるので、線と一緒には消えない）
        for (int i = 0; i < segments.Count; i++)
        {
            PaddleLineSegment s = segments[i];
            if (s != null && s.OwnerStroke == this) s.ForceRelease();
        }
        segments.Clear();
        openSegment = null;
        hasLastSegmentEnd = false;
    }

    public void UnregisterDot(PaddleDot dot)
    {
        // 新方式：点が消えたら、その点が属する区間にも知らせる（区間の点が全部消えたら区間も消える）
        if (dot != null && dot.lineSegment != null)
        {
            PaddleLineSegment seg = dot.lineSegment;
            dot.lineSegment = null;
            if (seg.OnDotRemoved(dot))
            {
                segments.Remove(seg);
                if (seg == openSegment) openSegment = null;
            }
        }

        aliveDotCount = Mathf.Max(0, aliveDotCount - 1);
        if (dot != null)
        {
            int i = dot.strokeIndex;
            if (i < 0 || i >= dots.Count || dots[i] != dot) i = dots.IndexOf(dot); // 念のため（通常は通らない）
            if (i >= 0) { dots[i] = null; removedCount++; }
            dot.strokeIndex = -1;
        }

        if (IsFinished && aliveDotCount == 0)
        {
            CompleteStroke();
        }
    }

    public void Finish(float dotLifeTimeSeconds)
    {
        if (IsFinished) return;

        IsFinished = true;

        // デバッグ：閉じるまでの秒数
        Debug_LastCloseSeconds = Mathf.Max(0f, Time.time - strokeStartTime);
        Debug_LastType = Type.ToString();

        TryMarkCircle();

        float extra = IsCircle ? circleExtraLife : 0f;
        finishGraceTime = Mathf.Max(0f, dotLifeTimeSeconds + extra);
        finishTimer = 0f;

        if (aliveDotCount == 0)
        {
            CompleteStroke();
        }
    }

    private void TryMarkCircle()
    {
        Debug_LastCircleResult = false;
        Debug_LastReason = "";
        HasCircleBounds = false;

        if (!hasStart || !hasEnd)
        {
            Debug_LastReason = "Start/End not set";
            return;
        }

        // ★最重要：寿命（秒）だけでゲートする
        // 例：LifeTime=0.5 なら 0.5秒以内に閉じたときだけ円OK
        if (circleGateSeconds >= 0f)
        {
            float closeSeconds = Mathf.Max(0f, Time.time - strokeStartTime);
            if (closeSeconds > circleGateSeconds)
            {
                Debug_LastReason = $"Time gate exceeded ({closeSeconds:0.000}s > {circleGateSeconds:0.000}s)";
                return;
            }
        }

        // 念のため：始点Dot参照が無い場合はNG
        if (firstDot == null)
        {
            Debug_LastReason = "First dot missing";
            return;
        }

        // 始点と終点が近い
        float d = Vector3.Distance(startPos, endPos);
        if (d > circleCloseDistance)
        {
            Debug_LastReason = "Close distance too far";
            return;
        }

        // 形状チェック（誤判定対策）
        int count = 0;
        bool hasPrev = false;
        Vector3 prev = Vector3.zero;
        float perimeter = 0f;

        bool hasBounds = false;
        float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

        for (int i = 0; i < dots.Count; i++)
        {
            PaddleDot dot = dots[i];
            if (dot == null) continue;

            Vector3 p = dot.transform.position;
            count++;

            if (!hasBounds)
            {
                minX = maxX = p.x;
                minY = maxY = p.y;
                hasBounds = true;
            }
            else
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            if (hasPrev)
            {
                perimeter += Vector3.Distance(prev, p);
            }
            prev = p;
            hasPrev = true;
        }

        if (!hasBounds)
        {
            Debug_LastReason = "No dots";
            return;
        }

        if (circleMinDots > 0 && count < circleMinDots)
        {
            Debug_LastReason = "Min dots not met";
            return;
        }

        if (circleMinPerimeter > 0f && perimeter < circleMinPerimeter)
        {
            Debug_LastReason = "Perimeter too short";
            return;
        }

        float width = maxX - minX;
        float height = maxY - minY;

        if (circleMinBoundsSize > 0f)
        {
            if (width < circleMinBoundsSize || height < circleMinBoundsSize)
            {
                Debug_LastReason = "Bounds too small";
                return;
            }
        }

        float minSide = Mathf.Max(0.0001f, Mathf.Min(width, height));
        float maxSide = Mathf.Max(width, height);
        float aspect = maxSide / minSide;

        if (aspect > circleMaxAspect)
        {
            Debug_LastReason = "Aspect too long (line-like)";
            return;
        }

        // 円成立
        IsCircle = true;
        Debug_LastCircleResult = true;
        Debug_LastReason = "OK";

        // Bounds保存
        Vector3 center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 1f);
        CircleBounds = new Bounds(center, size);
        HasCircleBounds = true;

        manager?.NotifyCircleFormed(this);

        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] == null) continue;
            dots[i].ExtendLife(circleExtraLife);
            dots[i].ApplyCircleVisual();
        }

        // 新方式：線の区間も、点と同じだけ太くする（従来方式は点の当たり判定が点の拡大で太くなっていた）
        if (segments.Count > 0 && firstDot != null)
        {
            float thick = firstDot.CircleThicknessScale;
            for (int i = 0; i < segments.Count; i++)
                if (segments[i] != null) segments[i].SetThicknessScale(thick);
        }

        if (PaddleDrawer.Instance != null)
        {
            var positions = new List<Vector3>(dots.Count);
            for (int i = 0; i < dots.Count; i++)
            {
                if (dots[i] != null) positions.Add(dots[i].transform.position);
            }
            PaddleDrawer.Instance.SpawnCircleVfx(Type, positions, CircleBounds.center);
        }
    }

    private void Update()
    {
        // ★負荷軽減：以前は各PaddleDotが自分のUpdate()で毎フレーム寿命を管理しており、
        //   線が長くなる（Dot数が増える）ほどUpdate呼び出し数が増えていた。
        //   ここでStrokeがまとめて全Dot分の寿命チェックを行う方式に変更した。
        //   dots.Count-1から0へ向かって処理するのは、寿命切れのDotをReleaseOrDestroySelf()
        //   経由でUnregisterDot()させる際にdotsリスト自体が変化しても安全なため
        //   （後方から処理すれば、まだ処理していない添字がズレない）。
        for (int i = dots.Count - 1; i >= 0; i--)
        {
            PaddleDot dot = dots[i];
            if (dot == null) continue;

            if (dot.Tick(Time.deltaTime))
            {
                dot.ReleaseOrDestroySelf();
            }
        }
        CompactDots();

        if (!IsFinished) return;

        if (finishGraceTime > 0f)
        {
            finishTimer += Time.deltaTime;
            if (finishTimer >= finishGraceTime)
            {
                finishGraceTime = 0f;
                if (aliveDotCount == 0)
                {
                    CompleteStroke();
                }
            }
        }
    }

    /// <summary>
    /// 線が貫通で強制破断される時（ForceBreakStroke）に、子Dot達をDestroyで巻き添えにせず
    /// 個別にプールへ戻すために呼ぶ。dotsは走査中にUnregisterDot()で変化するため、
    /// スナップショットを取ってから処理する。
    /// </summary>
    /// <summary>今表示されている点を順番どおりに outList へ入れる（線が壊れる演出用。ReflectedBulletFXManager）</summary>
    public int CopyActiveDots(List<PaddleDot> outList)
    {
        outList.Clear();
        CompactDots();
        for (int i = 0; i < dots.Count; i++)
            if (dots[i] != null && dots[i].gameObject.activeInHierarchy) outList.Add(dots[i]);
        return outList.Count;
    }

    // ★負荷軽減：線を消すたびに点の一覧のコピーをnewせず、使い回しのListを借りる（入れ子で呼ばれても別のListを使うので安全）
    private static readonly Stack<List<PaddleDot>> s_releaseSnapshotPool = new Stack<List<PaddleDot>>();

    public void ReleaseAllDots()
    {
        List<PaddleDot> snapshot = s_releaseSnapshotPool.Count > 0 ? s_releaseSnapshotPool.Pop() : new List<PaddleDot>(dots.Count);
        snapshot.Clear();
        snapshot.AddRange(dots);
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (snapshot[i] != null) snapshot[i].ReleaseOrDestroySelf();
        }
        snapshot.Clear();
        s_releaseSnapshotPool.Push(snapshot);
    }

    /// <summary>外した点（null）を詰める。残った点の順番はそのまま（List.Removeを繰り返した時と同じ結果）</summary>
    private void CompactDots()
    {
        if (removedCount == 0) return;
        int w = 0;
        for (int r = 0; r < dots.Count; r++)
        {
            var d = dots[r];
            if (ReferenceEquals(d, null)) continue;
            dots[w] = d;
            d.strokeIndex = w;
            w++;
        }
        dots.RemoveRange(w, dots.Count - w);
        removedCount = 0;
    }

    private void CompleteStroke()
    {
        manager?.NotifyStrokeEnded(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        ReleaseAllSegments();

        // ★PaddleDot側のstaticキャッシュ(strokeBaseRenderedColorCache)に残る
        //   このStrokeへの参照を、破棄と同時に確実に取り除く（メモリリーク防止）
        PaddleDot.ClearCachedColorFor(this);
    }
}
