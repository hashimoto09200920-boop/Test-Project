using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Testing;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// シーン内に並ぶデバッグボタンを、共有設定アセット(DebugButtonsConfig)1つのチェックボックスで
/// 一括表示/非表示・操作不可にする。[ExecuteAlways]によりPlay前のEdit modeでも即座に反映される。
/// ★ベータ版ビルドでデバッグ機能を使えなくする目的があるため、Editor専用にはせず実機ビルドでも動作する。
/// ★Title/AreaSelectなど複数シーンに同じコンポーネントを配置し、同じDebugButtonsConfigアセットを
///   参照させることで、アセット側の1箇所のチェックボックスで全シーンのボタンが連動する。
/// ★対象ボタン名(buttonNames)はシーンごとに異なるため、シーンごとにInspectorで設定する。
/// キーボードショートカットはUnity標準機能との衝突が続いたため採用しない。
/// </summary>
[ExecuteAlways]
public class DebugButtonsVisibilityToggle : MonoBehaviour
{
    [Tooltip("このシーンで一括ON/OFF対象にするデバッグボタンのGameObject名一覧")]
    [SerializeField] private string[] buttonNames;

    [Tooltip("ON/OFFの実体。複数シーンで同じアセットを参照することで一括切り替えできる")]
    [SerializeField] private DebugButtonsConfig config;

    private bool? lastApplied;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        // ★OnValidateはコンパイル直後等でも呼ばれるため、実際に値が変わった時だけ反映する
        if (config != null && lastApplied == config.showDebugButtons) return;
        Apply();
    }

    private void Update()
    {
        // ExecuteAlways中、Inspector以外（他スクリプトからの変更等）での差分も拾えるよう保険で確認する
        if (config != null && lastApplied != config.showDebugButtons) Apply();
    }

    [ContextMenu("Apply Now")]
    private void Apply()
    {
#if UNITY_EDITOR
        // ★ビルド処理中はシーンが正式にロードされていない状態でこのメソッドが呼ばれることがあり、
        //   GetRootGameObjects()が例外を投げてビルド自体を失敗させていた。安全のため二重にガードする。
        if (BuildPipeline.isBuildingPlayer) return;
#endif
        if (config == null || buttonNames == null) return;

        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) return;

        bool show = config.showDebugButtons;

        int applied = 0;
        foreach (var name in buttonNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            var go = FindDeepInScene(scene, name);
            if (go == null) continue;
            if (go.activeSelf != show) go.SetActive(show);
            applied++;
        }
        lastApplied = show;
        Debug.Log($"[DebugButtonsVisibilityToggle] デバッグボタン{applied}個を{(show ? "表示" : "非表示")}にしました。");
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
