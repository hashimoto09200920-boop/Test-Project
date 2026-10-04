using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NeonDancer専用の煙幕プレハブ（NeonDancer_SmokeParticle）に付ける目印。
/// - 生成フレーム・位置を記録し、Just反射した⑤の煙幕を同じフレームのうちに消せるようにする
///   （PaddleDotは「煙を出す→Just判定」の順のため、煙を出さないようにするには直後に消すしかない。共有コードは改修しない）
/// - 生成位置をログに出す（⑤がDancer/Floorに当たった時に煙が出ているかの確認用）
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerSmokeMarker : MonoBehaviour
{
    [Tooltip("生成位置をConsole/Editor.logに出す（確認が済んだらOFFにしてよい）")]
    [SerializeField] private bool logSpawn = true;

    private static readonly List<NeonDancerSmokeMarker> s_alive = new List<NeonDancerSmokeMarker>();
    private int spawnFrame;

    [Tooltip("同じフレームにこの距離以内で生成された煙幕は1つにまとめる（反射した⑤が線の点(PaddleDot)に複数同時に当たり、煙が重複生成されるため）")]
    [SerializeField] private float mergeDistance = 1.0f;

    private void Awake()
    {
        spawnFrame = Time.frameCount;

        // ★同じフレーム・近い位置に既に煙幕があれば、自分は消して1つにまとめる
        for (int i = s_alive.Count - 1; i >= 0; i--)
        {
            var m = s_alive[i];
            if (m == null) { s_alive.RemoveAt(i); continue; }
            if (m.spawnFrame == spawnFrame && Vector2.Distance(m.transform.position, transform.position) <= mergeDistance)
            {
                Destroy(gameObject);
                return;
            }
        }

        s_alive.Add(this);
        if (logSpawn) Debug.Log($"[NeonDancerSmoke] 煙幕生成 pos=({transform.position.x:F2},{transform.position.y:F2}) frame={spawnFrame}");
    }

    private void OnDestroy()
    {
        s_alive.Remove(this);
    }

    /// <summary>このフレームにpos付近で生成された煙幕を消す（Just反射時用）。消した数を返す</summary>
    public static int DestroySpawnedThisFrameNear(Vector3 pos, float maxDistance)
    {
        int count = 0;
        for (int i = s_alive.Count - 1; i >= 0; i--)
        {
            var m = s_alive[i];
            if (m == null) { s_alive.RemoveAt(i); continue; }
            if (m.spawnFrame != Time.frameCount) continue;
            if (Vector2.Distance(m.transform.position, pos) > maxDistance) continue;
            s_alive.RemoveAt(i);
            Destroy(m.gameObject); // SmokeCloud.OnDestroyで隠した弾の表示も元に戻る
            count++;
        }
        return count;
    }
}
