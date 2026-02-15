using UnityEngine;

public class StrokeManager : MonoBehaviour
{
    [Header("Stroke Limit")]
    [SerializeField] private int maxStrokes = 3;

    [Header("Debug")]
    [SerializeField] private bool showLog = false;

    public int MaxStrokes => maxStrokes;
    public int ActiveStrokesCount { get; private set; }

    private void Awake()
    {
        maxStrokes = Mathf.Max(0, maxStrokes);
        ActiveStrokesCount = 0;
    }

    public bool CanStartStroke()
    {
        if (maxStrokes <= 0) return true; // 0以下なら無制限扱い
        return ActiveStrokesCount < maxStrokes;
    }

    // PaddleRootの下にStrokeを生成して返す
    public Stroke CreateStroke(Transform parent, PaddleDot.LineType type)
    {
        if (!CanStartStroke()) return null;

        GameObject go = new GameObject($"Stroke_{ActiveStrokesCount + 1}_{type}");
        go.transform.SetParent(parent, false);

        Stroke stroke = go.AddComponent<Stroke>();
        stroke.Initialize(this, type);

        ActiveStrokesCount++;

        if (showLog)
        {
            Debug.Log($"[StrokeManager] Start Stroke => {ActiveStrokesCount}/{maxStrokes}");
        }

        return stroke;
    }

    // 円が成立した瞬間に呼ばれる
    public void NotifyCircleFormed(Stroke stroke)
    {
        if (stroke != null && stroke.IsCircle && stroke.HasCircleBounds)
        {
            // プレイヤー救出処理
            PixelDancerController dancer = FindFirstObjectByType<PixelDancerController>();
            if (dancer != null && dancer.IsFalling)
            {
                Vector3 playerPos = dancer.transform.position;
                playerPos.z = stroke.CircleBounds.center.z;
                if (stroke.CircleBounds.Contains(playerPos))
                {
                    dancer.RescueFromCircle();
                }
            }

            // 円内の煙を消滅させる
            DissolveSmokeInCircle(stroke.CircleBounds);
        }
    }

    private void DissolveSmokeInCircle(Bounds circleBounds)
    {
        // 円の中心と半径を計算（Boundsから）
        Vector2 circleCenter = new Vector2(circleBounds.center.x, circleBounds.center.y);
        float circleRadius = Mathf.Max(circleBounds.extents.x, circleBounds.extents.y);

        // 1. シーン内のすべてのSmokeCloudを取得して消滅
        SmokeCloud[] smokeClouds = FindObjectsByType<SmokeCloud>(FindObjectsSortMode.None);
        if (smokeClouds != null && smokeClouds.Length > 0)
        {
            foreach (SmokeCloud smoke in smokeClouds)
            {
                if (smoke != null)
                {
                    smoke.DissolveByCircle(circleCenter, circleRadius);
                }
            }
        }

        // 2. 円内の煙幕弾を検出して消滅（煙を出さずに即消滅）
        EnemyBullet[] bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        if (bullets != null && bullets.Length > 0)
        {
            foreach (EnemyBullet bullet in bullets)
            {
                if (bullet != null && bullet.IsSmokeGrenadeActive)
                {
                    // 弾の位置が円内にあるかチェック
                    Vector2 bulletPos = new Vector2(bullet.transform.position.x, bullet.transform.position.y);
                    float distance = Vector2.Distance(bulletPos, circleCenter);

                    if (distance <= circleRadius)
                    {
                        bullet.DissolveByCircle();
                    }
                }
            }
        }
    }

    // Strokeが消える時に呼ばれる
    public void NotifyStrokeEnded(Stroke stroke)
    {
        ActiveStrokesCount = Mathf.Max(0, ActiveStrokesCount - 1);

        if (showLog)
        {
            Debug.Log($"[StrokeManager] End Stroke => {ActiveStrokesCount}/{maxStrokes}");
        }
    }

    // =========================================================
    // Skill System Setters
    // =========================================================

    /// <summary>
    /// 最大ストローク数を設定（スキルシステム用）
    /// </summary>
    public void SetMaxStrokes(int value)
    {
        int oldValue = maxStrokes;
        maxStrokes = Mathf.Max(0, value);
        Debug.Log($"[StrokeManager] SetMaxStrokes: {oldValue} → {maxStrokes} (input value={value})");
    }
}
