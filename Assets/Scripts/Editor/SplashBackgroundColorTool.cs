using UnityEditor;
using UnityEngine;

/// <summary>
/// Unityの起動画面（Splash）の背景色を、タイトル画面の星空（Art/Title/星空①.png）の平均色に近い濃い紺にする。
/// スマホでアプリが裏で終了させられた後に再開すると、起動し直しの間だけ起動画面の背景色が見えるため、
/// 濃いグレー（35,31,32）のままだと「星空ではないグレーの背景」に見えていた。
/// 設定先：Project Settings > Player > Splash Image > Background Color（後からここで直接変更してもよい）
/// </summary>
public static class SplashBackgroundColorTool
{
    // 星空①.png の全ピクセル平均（R6 G5 B22）
    private static readonly Color StarfieldNavy = new Color32(6, 5, 22, 255);

    [MenuItem("Tools/起動画面/背景色を星空に合わせる")]
    private static void Apply()
    {
        Color before = PlayerSettings.SplashScreen.backgroundColor;
        PlayerSettings.SplashScreen.backgroundColor = StarfieldNavy;
        AssetDatabase.SaveAssets();
        Debug.Log($"[SplashBackgroundColorTool] 起動画面の背景色を変更しました：{(Color32)before} → {(Color32)StarfieldNavy}" +
                  "（Project Settings > Player > Splash Image > Background Color）。スマホに入れ直すと反映されます");
    }
}
