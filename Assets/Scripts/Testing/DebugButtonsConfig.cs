using UnityEngine;

namespace Game.Testing
{
    /// <summary>
    /// Title/AreaSelect各シーンに散らばるデバッグボタン（計11個）を、このアセット1つの
    /// チェックボックスで一括ON/OFFするための共有設定。DebugButtonsVisibilityToggleが参照する。
    /// ベータテスト版をビルドする際は、このアセットのshowDebugButtonsをOFFにしてからビルドする。
    /// </summary>
    [CreateAssetMenu(fileName = "DebugButtonsConfig", menuName = "Game/Testing/Debug Buttons Config", order = 2)]
    public class DebugButtonsConfig : ScriptableObject
    {
        [Tooltip("OFFにすると、対象の全デバッグボタンを一括非表示・操作不可にする（Editor/実機ビルド両方に反映される）")]
        public bool showDebugButtons = true;
    }
}
