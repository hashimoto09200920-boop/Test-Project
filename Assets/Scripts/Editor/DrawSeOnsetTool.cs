using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 調査用（2026/10/25）：線を引く音（白線SE2・赤線SE3）が、音のファイルの先頭から何秒目で鳴り始めるかを測って Console に出す。
/// エディタのメニューから押した時だけ動く（ゲーム中の処理・ビルドには一切含まれない）。音のファイルは変更しない。
/// </summary>
public static class DrawSeOnsetTool
{
    private static readonly string[] ClipPaths =
    {
        "Assets/Audio/Puddle/白線SE2.mp3",
        "Assets/Audio/Puddle/赤線SE3.mp3",
    };

    private const float SegmentSeconds = 0.05f; // 0.05秒ごとの音の大きさを出す
    private const float ReportSeconds = 0.6f;   // 先頭からこの秒数まで出す

    [MenuItem("Tools/サウンド/線の音の鳴り始めを測る")]
    private static void Run()
    {
        var sb = new StringBuilder("[DrawSeOnset] 線の音の鳴り始め\n");
        foreach (var path in ClipPaths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) { sb.AppendLine($"■ {path}：見つかりません"); continue; }
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();

            int ch = clip.channels, freq = clip.frequency, samples = clip.samples;
            var data = new float[samples * ch];
            if (!clip.GetData(data, 0)) { sb.AppendLine($"■ {clip.name}：音のデータを読み出せませんでした（Load Type を確認）"); continue; }

            // 1サンプルごとに全チャンネルの最大の振れ幅を取る
            float peak = 0f;
            var amp = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float m = 0f;
                for (int c = 0; c < ch; c++) m = Mathf.Max(m, Mathf.Abs(data[i * ch + c]));
                amp[i] = m;
                if (m > peak) peak = m;
            }

            int onset10 = FirstAbove(amp, peak * 0.1f);  // 最大の10%を超えた最初の位置
            int onset50 = FirstAbove(amp, peak * 0.5f);  // 最大の50%を超えた最初の位置
            int peakAt = System.Array.IndexOf(amp, peak);

            sb.AppendLine($"■ {clip.name}（長さ {clip.length:0.000}秒・{freq}Hz・{ch}ch・最大の振れ幅 {peak:0.000}）");
            sb.AppendLine($"　鳴り始め（最大の10%）：{Sec(onset10, freq)}　はっきり聞こえ始め（最大の50%）：{Sec(onset50, freq)}　一番大きい所：{Sec(peakAt, freq)}");

            int seg = Mathf.Max(1, Mathf.RoundToInt(SegmentSeconds * freq));
            int end = Mathf.Min(samples, Mathf.RoundToInt(ReportSeconds * freq));
            sb.Append("　0.05秒ごとの大きさ（最大＝100）：");
            for (int s = 0; s < end; s += seg)
            {
                float m = 0f;
                for (int i = s; i < Mathf.Min(end, s + seg); i++) m = Mathf.Max(m, amp[i]);
                sb.Append($"{(float)s / freq:0.00}秒={(peak > 0f ? m / peak * 100f : 0f):0} ");
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    private static int FirstAbove(float[] amp, float threshold)
    {
        for (int i = 0; i < amp.Length; i++) if (amp[i] >= threshold) return i;
        return -1;
    }

    private static string Sec(int sample, int freq) => sample < 0 ? "なし" : $"{(float)sample / freq:0.000}秒";
}
