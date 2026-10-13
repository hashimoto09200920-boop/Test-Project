#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

/// <summary>
/// ★調査用（処理落ち・一時停止の原因特定。原因が確定して直ったら、このファイルと各所の PerfSpikeProbe.* の行を削除する）。
/// EditorのPlay中と開発ビルド（Development Build）だけで動く。製品版のビルドには含まれない。
/// 1フレームが threshold 秒を超えた時だけ、そのフレームの内訳を Editor.log（端末なら logcat）へ1行で出す：
///   ・Unityの計測値（物理演算・スクリプト・描画・ガベージコレクション等）のうち時間の長い順
///   ・物理の更新回数、弾・線の点への衝突通知の回数、画面上の弾・線の点の数、そのフレームのメモリ確保量
/// ゲームの動作には影響しない（読むだけ）。シーンへの配置は不要（起動時に自動で作られる）。
/// </summary>
public class PerfSpikeProbe : MonoBehaviour
{
    private const float Threshold = 0.1f;   // この秒数を超えたフレームを記録する
    private const int TopMarkers = 10;      // 時間の長い計測値をいくつ出すか

    // 各所から数える（物理の1ステップごとの衝突通知の数）
    public static int BulletEnterCalls, BulletStayCalls, DotEnterCalls;

    private static readonly string[] Prefixes = { "Physics2D.", "FixedUpdate.", "Update.", "PreLateUpdate.", "PostLateUpdate.", "PreUpdate.", "GC." };

    private readonly List<(string name, ProfilerRecorder rec)> markers = new List<(string, ProfilerRecorder)>();
    private readonly HashSet<string> markerNames = new HashSet<string>();
    private ProfilerRecorder gcAlloc;
    private float nextResolve;
    private int fixedSteps;
    private int lastFixedSteps, lastBulletEnter, lastBulletStay, lastDotEnter;
    private readonly List<(string name, double ms)> sortBuf = new List<(string, double)>();
    private readonly StringBuilder sb = new StringBuilder(1024);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("PerfSpikeProbe");
        DontDestroyOnLoad(go);
        go.AddComponent<PerfSpikeProbe>();
    }

    private void OnEnable()
    {
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        ResolveMarkers();
    }

    private void OnDisable()
    {
        foreach (var m in markers) m.rec.Dispose();
        markers.Clear();
        markerNames.Clear();
        gcAlloc.Dispose();
    }

    /// <summary>名前が Prefixes で始まる計測値を全部拾う（後から登録される物もあるため、ときどき探し直す）</summary>
    private void ResolveMarkers()
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var h in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            string n = d.Name;
            if (string.IsNullOrEmpty(n) || markerNames.Contains(n)) continue;
            bool want = false;
            foreach (var p in Prefixes) if (n.StartsWith(p)) { want = true; break; }
            if (!want) continue;
            markerNames.Add(n);
            markers.Add((n, new ProfilerRecorder(h, 1)));
        }
        nextResolve = Time.unscaledTime + 3f;
    }

    private void FixedUpdate() { fixedSteps++; }

    private void Update()
    {
        // ここで見る unscaledDeltaTime は「前のフレーム」の長さ。計測値・回数も前のフレームの分（LateUpdateで保存）を使う
        if (Time.unscaledDeltaTime > Threshold) Report(Time.unscaledDeltaTime);
        if (Time.unscaledTime >= nextResolve) ResolveMarkers();
    }

    private void LateUpdate()
    {
        lastFixedSteps = fixedSteps; fixedSteps = 0;
        lastBulletEnter = BulletEnterCalls; BulletEnterCalls = 0;
        lastBulletStay = BulletStayCalls; BulletStayCalls = 0;
        lastDotEnter = DotEnterCalls; DotEnterCalls = 0;
    }

    private void Report(float dt)
    {
        sortBuf.Clear();
        foreach (var m in markers)
        {
            if (!m.rec.Valid) continue;
            double ms = m.rec.LastValue / 1e6; // ナノ秒→ミリ秒
            if (ms >= 1.0) sortBuf.Add((m.name, ms));
        }
        sortBuf.Sort((a, b) => b.ms.CompareTo(a.ms));

        int bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None).Length;
        int dots = FindObjectsByType<PaddleDot>(FindObjectsSortMode.None).Length;

        sb.Clear();
        sb.Append("[PerfSpike] フレーム ").Append((dt * 1000f).ToString("F0")).Append("ms")
          .Append(" | 物理の更新回数=").Append(lastFixedSteps)
          .Append(" 弾の衝突通知 Enter=").Append(lastBulletEnter).Append(" Stay=").Append(lastBulletStay)
          .Append(" 線の点のEnter=").Append(lastDotEnter)
          .Append(" | 弾=").Append(bullets).Append(" 線の点=").Append(dots)
          .Append(" | メモリ確保=").Append(gcAlloc.Valid ? (gcAlloc.LastValue / 1024).ToString() : "?").Append("KB")
          .Append(" | 内訳:");
        for (int i = 0; i < sortBuf.Count && i < TopMarkers; i++)
            sb.Append(' ').Append(sortBuf[i].name).Append('=').Append(sortBuf[i].ms.ToString("F1")).Append("ms");
        Debug.LogWarning(sb.ToString());
    }
}
#endif
