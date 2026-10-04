using UnityEngine;

/// <summary>
/// 【確認用・一時的】NeonDancerの⑨Drillが線に留まっているか（PinnedReflectBullet.IsPinned）をEditor.logへ記録する。
/// 原因が分かったら、この部品とNeonDancerControllerの付与処理は削除してよい。
/// プール再利用される弾に残らないよう、消える時に自分自身を外す。
/// </summary>
public class NeonDancerDrillDiag : MonoBehaviour
{
    private EnemyBullet bullet;
    private PinnedReflectBullet pinned;
    private bool lastPinned;
    private int id;
    private static int s_next;

    public void Arm(EnemyBullet b)
    {
        bullet = b;
        pinned = b.CachedPinnedReflect;
        id = ++s_next;
        var f = typeof(PinnedReflectBullet).GetField("requiredHits", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        object req = (pinned != null && f != null) ? f.GetValue(pinned) : null;
        var pen = b.CachedPenetration;
        Debug.Log($"[NeonDancerDrillDiag#{id}] 発射 PinnedReflect={(pinned != null)} requiredHits={req} 貫通力={(pen != null ? pen.Penetration.ToString() : "なし")} prefab={b.name}");
        b.OnReflected += () => Debug.Log($"[NeonDancerDrillDiag#{id}] 反射 frame={Time.frameCount} 留まり中={(pinned != null && pinned.IsPinned)}");
        b.OnPenetratedLine += () => Debug.Log($"[NeonDancerDrillDiag#{id}] 線を貫通（硬度より貫通力が高い） frame={Time.frameCount}");
    }

    private void Update()
    {
        if (pinned == null) return;
        bool now = pinned.IsPinned;
        if (now != lastPinned)
        {
            Debug.Log($"[NeonDancerDrillDiag#{id}] {(now ? "線に留まった" : "線から離れた")} frame={Time.frameCount}");
            lastPinned = now;
        }
    }

    private void OnDisable()
    {
        if (bullet != null) Debug.Log($"[NeonDancerDrillDiag#{id}] 弾が消えた frame={Time.frameCount}");
        Destroy(this);
    }
}
