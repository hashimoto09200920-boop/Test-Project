using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// InputSystemUIInputModuleのPointer Behaviorをデフォルト(SingleMouseOrPenButMultiTouchAndTrack)のままにすると、
    /// WebGL等でタッチ操作が「タッチ」と「エミュレートされたマウス」の2系統の独立したポインタとして
    /// 二重に処理され、1回のタップでUIイベント(PointerDown/Click)が2回発火してしまうことがある。
    /// SingleUnifiedPointerに統一し、タッチとマウスを常に1つのポインタとして扱わせることで二重発火を防ぐ。
    /// シーン読み込みのたびに自動で適用するため、EditorでのInspector手動設定は不要。
    /// </summary>
    internal static class InputPointerBehaviorSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            Apply();
            SceneManager.sceneLoaded += (_, __) => Apply();
        }

        private static void Apply()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null) return;

            uiModule.pointerBehavior = UIPointerBehavior.SingleUnifiedPointer;
        }
    }
}
