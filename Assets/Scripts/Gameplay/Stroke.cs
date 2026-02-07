using System.Collections.Generic;
using UnityEngine;

public class Stroke : MonoBehaviour
{
    public PaddleDot.LineType Type { get; private set; }
    public bool IsFinished { get; private set; }
    public bool IsCircle { get; private set; }
    public bool HasCircleBounds { get; private set; }
    public Bounds CircleBounds { get; private set; }

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

    // 始点Dot（最初に登録されたDot）
    private PaddleDot firstDot;

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
        firstDot = null;

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
        if (dot != null) dots.Add(dot);

        if (firstDot == null && dot != null)
        {
            firstDot = dot;
        }
    }

    public void UnregisterDot(PaddleDot dot)
    {
        aliveDotCount = Mathf.Max(0, aliveDotCount - 1);
        if (dot != null) dots.Remove(dot);

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
    }

    private void Update()
    {
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

    private void CompleteStroke()
    {
        manager?.NotifyStrokeEnded(this);
        Destroy(gameObject);
    }
}
