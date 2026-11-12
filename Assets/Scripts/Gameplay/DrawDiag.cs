using UnityEngine;

/// <summary>
/// ★調査用（2026/11/12）。「左上で線を引いても見えない・当たらない」不具合の原因を、実機の logcat で確かめるための記録。
/// logcat の Unity タグで「[DrawDiag2]」を探す。原因がわかって直したら、このファイルと呼び出しを必ず全部消す
/// （呼び出しの場所：PaddleDrawer・SlowMotionUIManager・SlowMotionManager。grep「DrawDiag.」）
/// </summary>
public static class DrawDiag
{
    public static bool Enabled = true;

    public static void Log(string msg)
    {
        if (!Enabled) return;
        Debug.Log($"[DrawDiag2] f{Time.frameCount} t{Time.unscaledTime:F2} {msg}");
    }

    public static string V(Vector2 v) => $"({v.x:F0},{v.y:F0})";
    public static string W(Vector3 v) => $"({v.x:F2},{v.y:F2})";
}
