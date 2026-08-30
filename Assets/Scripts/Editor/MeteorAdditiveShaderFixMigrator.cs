using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

/// <summary>
/// MeteorEffectが使っていたM_OrbGlow_Additive(URP標準のParticles/Unlitシェーダー)は
/// "UniversalForward"パスしか持たず、このプロジェクトが使うURP 2D Renderer(Renderer2D)は
/// SpriteRendererの描画に"Universal2D"パスしか実行しないため、色のTintが正しく反映されなかった。
/// Sprites/Additive(Assets/Shaders/Sprite_Additive.shader)はUnity純正のSprite-Unlit-Defaultを
/// ベースに両パスを実装しているため、2D Rendererでも確実に描画・着色される。
/// </summary>
public static class MeteorAdditiveShaderFixMigrator
{
    [MenuItem("Tools/Area09/Fix Meteor Additive Shader")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "05_Game")
        {
            Debug.LogWarning("[MeteorAdditiveShaderFixMigrator] 05_Gameシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        var shader = Shader.Find("Sprites/Additive");
        if (shader == null)
        {
            Debug.LogError("[MeteorAdditiveShaderFixMigrator] シェーダー\"Sprites/Additive\"が見つかりません。" +
                            "Assets/Shaders/Sprite_Additive.shaderが正しくインポートされているか確認してください。");
            return;
        }

        const string assetPath = "Assets/Materials/M_MeteorAdditive.mat";
        string dir = Path.GetDirectoryName(assetPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        var mat = existing != null ? existing : new Material(shader);
        mat.shader = shader;
        mat.SetColor("_Color", Color.white);

        if (existing == null)
        {
            AssetDatabase.CreateAsset(mat, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();

        GameObject go = FindDeepInScene(scene, "MeteorEffect");
        if (go == null)
        {
            Debug.LogWarning("[MeteorAdditiveShaderFixMigrator] MeteorEffectが見つかりません。マテリアルの生成のみ行いました。");
            return;
        }

        var meteor = go.GetComponent<MeteorEffect>();
        if (meteor == null)
        {
            Debug.LogWarning("[MeteorAdditiveShaderFixMigrator] MeteorEffectコンポーネントが見つかりません。");
            return;
        }

        var so = new SerializedObject(meteor);
        var matProp = so.FindProperty("additiveMaterial");
        Object before = matProp.objectReferenceValue;
        matProp.objectReferenceValue = mat;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MeteorAdditiveShaderFixMigrator] additiveMaterial: {before} → {mat.name} に差し替えました。");
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
