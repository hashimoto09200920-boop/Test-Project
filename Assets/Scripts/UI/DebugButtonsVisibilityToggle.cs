#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

/// <summary>
/// AreaSelect画面右側に並ぶ大量のデバッグボタンを、Inspectorのチェックボックス1つで
/// 一括表示/非表示にする。[ExecuteAlways]によりPlay前のEdit modeでも即座に反映される。
/// キーボードショートカットはUnity標準機能との衝突が続いたため採用しない。
/// ★#if UNITY_EDITORで全体を囲み、実機ビルドには含めない（Editor専用の便利ツールのため）。
/// ★ビルド実行時、[ExecuteAlways]のOnEnable/OnValidateがシーン未ロード状態で発火し
///   "ArgumentException: The scene is not loaded"でビルド自体が失敗する不具合があったため、
///   BuildPipeline.isBuildingPlayer中とscene.isLoaded=false中は一切処理しないようにしている。
/// </summary>
[ExecuteAlways]
public class DebugButtonsVisibilityToggle : MonoBehaviour
{
    [Tooltip("チェックを外すと、AreaSelectの全デバッグボタンを一括非表示にする（Play前でも即座に反映される）")]
    [SerializeField] private bool showDebugButtons = true;

    private static readonly string[] ButtonNames =
    {
        "DebugUnlockNextAreaButton",
        "DebugArea10RankButton",
        "DebugToggleStaminaUnlimitedButton",
        "DebugAddLowUsesGemButton",
        "DebugUnlimitedGemsButton",
        "DebugGoldMaxButton",
        "DebugAddInfiniteStoneButton",
        "DebugClearGemsButton",
        "DebugAddGemsButton",
        "DebugSlotLevelButton",
    };

    private bool? lastApplied;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        // ★OnValidateはコンパイル直後等でも呼ばれるため、実際に値が変わった時だけ反映する
        if (lastApplied == showDebugButtons) return;
        Apply();
    }

    private void Update()
    {
        // ExecuteAlways中、Inspector以外（他スクリプトからの変更等）での差分も拾えるよう保険で確認する
        if (lastApplied != showDebugButtons) Apply();
    }

    [ContextMenu("Apply Now")]
    private void Apply()
    {
        // ★ビルド処理中はシーンが正式にロードされていない状態でこのメソッドが呼ばれることがあり、
        //   GetRootGameObjects()が例外を投げてビルド自体を失敗させていた。安全のため二重にガードする。
        if (BuildPipeline.isBuildingPlayer) return;

        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) return;

        int applied = 0;
        foreach (var name in ButtonNames)
        {
            var go = FindDeepInScene(scene, name);
            if (go == null) continue;
            if (go.activeSelf != showDebugButtons) go.SetActive(showDebugButtons);
            applied++;
        }
        lastApplied = showDebugButtons;
        Debug.Log($"[DebugButtonsVisibilityToggle] デバッグボタン{applied}個を{(showDebugButtons ? "表示" : "非表示")}にしました。");
    }

    private static GameObject FindDeepInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
#endif
