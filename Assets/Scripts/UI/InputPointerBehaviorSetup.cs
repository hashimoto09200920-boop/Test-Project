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
    ///
    /// ★SingleUnifiedPointerは「すべての指・マウス・ペンを1つの論理ポインタに統合する」設定のため、
    ///   マルチタッチ(スローモーションボタンを1本指で押しっぱなしにしながら、別の指で線を描く操作)が
    ///   構造的に成立しない。1本目の指のPointerUpが正しく発火せず、指を離してもスローモーションが
    ///   ONのまま残る不具合の原因になっていた。WebGLの二重発火はブラウザ側がタッチとは別に
    ///   マウスイベントを合成することが原因のため、この対策はWebGL限定で適用し、
    ///   スマホ/PC(Standalone)ではデフォルト(SingleMouseOrPenButMultiTouchAndTrack。マウス/ペンは
    ///   1つに統合しつつ、各指は独立したポインタとして正しく追跡される)のままにしてマルチタッチを維持する。
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
#if UNITY_WEBGL && !UNITY_EDITOR
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null) return;

            uiModule.pointerBehavior = UIPointerBehavior.SingleUnifiedPointer;
#endif
        }
    }
}
