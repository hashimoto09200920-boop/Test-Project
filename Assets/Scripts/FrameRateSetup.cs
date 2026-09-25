using System.Collections;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// ゲーム全体のフレームレート上限を起動時に一度だけ設定する。
    /// ★Androidでは`QualitySettings.vSyncCount`が効かず、`Application.targetFrameRate`
    ///   未設定のままだと上限なしでレンダリングし続ける。可変/高リフレッシュレート対応の
    ///   画面（例：OPPO Reno11A）だと、この上限なし状態が原因で画面のチラつき・
    ///   アニメーションが速く見える不具合が発生することが確認された
    ///   （固定60Hzの画面（Rakuten Hand 5G）やPCでは発生しない）。
    /// </summary>
    internal static class FrameRateSetup
    {
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Application.targetFrameRate = TargetFrameRate;

#if UNITY_ANDROID && !UNITY_EDITOR
            // ★BeforeSceneLoad時点ではAndroidのネイティブ画面(Surface)がまだ準備できておらず、
            //   Screen.SetResolutionの物理リフレッシュレート指定が無視される可能性があるため、
            //   実際に描画が始まった後（数フレーム経過後）に改めて設定し直す。
            var runner = new GameObject("FrameRateSetupRunner");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<DelayedRefreshRateApplier>();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private class DelayedRefreshRateApplier : MonoBehaviour
        {
            private IEnumerator Start()
            {
                // ★数フレーム＋実時間を少し待ってからネイティブ側の画面モードへ反映させる。
                for (int i = 0; i < 10; i++) yield return null;
                yield return new WaitForSecondsRealtime(0.2f);

                ApplyRefreshRate();
            }

            private void ApplyRefreshRate()
            {
                // ★Application.targetFrameRateはUnity側の描画回数しか制限できない。
                //   OPPO Reno11A実機のdumpsys display出力で、画面パネル自体の物理リフレッシュレートは
                //   (0〜120Hz)の範囲で端末側が自由に切り替えられる状態のままであることを確認した
                //   （renderは60Hzに制限されていてもphysicalは可変のまま）。このパネル側の周波数切り替え
                //   自体がチラつきの原因になっている可能性があるため、Screen.SetResolutionで
                //   パネルの物理リフレッシュレートも明示的に60Hzへ固定する。
                var refreshRate = new RefreshRate { numerator = TargetFrameRate, denominator = 1 };
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, Screen.fullScreenMode, refreshRate);
                Debug.Log($"[FrameRateSetup] Applied refresh rate {TargetFrameRate}Hz after delay. " +
                    $"Screen.currentResolution.refreshRateRatio={Screen.currentResolution.refreshRateRatio.value}");

                // ★Screen.SetResolutionはUnity側の描画ペース調整に留まり、Android OS側の
                //   物理リフレッシュレート範囲(mDesiredDisplayModeSpecsのphysical)には反映されない
                //   ことを実機のdumpsys displayで確認済み。AndroidネイティブAPIの
                //   Window.setFrameRate()(API 30+)を直接呼び出し、OS側にも明示的に要求する。
                try
                {
                    using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                    {
                        // Surface.FRAME_RATE_COMPATIBILITY_DEFAULT = 0
                        window.Call("setFrameRate", (float)TargetFrameRate, 0);
                        Debug.Log("[FrameRateSetup] Called Android Window.setFrameRate(60, DEFAULT) directly.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[FrameRateSetup] Window.setFrameRate failed (Android <11 or other issue): {e.Message}");
                }
            }
        }
#endif
    }
}
