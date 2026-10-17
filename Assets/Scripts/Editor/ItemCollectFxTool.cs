using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 05_Gameのアイテム（Gold・Life）取得演出を派手にするための設定。
/// ・数値ポップアップ（Assets/Prefabs/BlockItemPopupText.prefab）に、縁取り＋光の文字マテリアルを作って設定する
///     Gold／Life とも、くっきりした濃い縁取り＋右下の影（色は文字のグラデーション。BlockItemPopupText）
/// ・演出（Assets/Resources/ItemFX.prefab）に、コインとハートの絵（05_GameのBlockItemManagerのGold/Lifeアイコン）を設定する
/// マテリアルの縁取り・光の強さは、作成後に各マテリアルのInspector（Outline／Underlay）で調整できる。
/// </summary>
public static class ItemCollectFxTool
{
    private const string PopupPrefabPath = "Assets/Prefabs/BlockItemPopupText.prefab";
    private const string ItemFxPrefabPath = "Assets/Resources/ItemFX.prefab";
    private const string ScenePath = "Assets/Scenes/05_Game.unity";
    private const string MatDir = "Assets/Art/UI/ItemFX";
    private const string GoldMatPath = MatDir + "/ItemPopup_GoldMat.mat";
    private const string LifeMatPath = MatDir + "/ItemPopup_LifeMat.mat";

    [MenuItem("Tools/アイテム演出/取得演出を派手にする（文字の縁取り・コインとハートの絵を設定）")]
    internal static void Run()
    {
        string msg = "";

        // ===== ① 数値ポップアップの文字マテリアル =====
        GameObject popupRoot = PrefabUtility.LoadPrefabContents(PopupPrefabPath);
        if (popupRoot == null) { Debug.LogError("[ItemCollectFxTool] " + PopupPrefabPath + " が見つかりません"); return; }
        try
        {
            var popup = popupRoot.GetComponent<BlockItemPopupText>();
            var label = popupRoot.GetComponentInChildren<TextMeshPro>(true);
            if (popup == null || label == null || label.font == null)
            {
                Debug.LogError("[ItemCollectFxTool] BlockItemPopupText またはその文字（TextMeshPro）が見つかりません");
                return;
            }
            if (!Directory.Exists(MatDir)) Directory.CreateDirectory(MatDir);
            // くっきりした濃い縁取り＋影（光のにじみはぼやけて読みにくいのでやめた）。色は文字側のグラデーション
            var goldMat = EnsureMat(label.font, GoldMatPath, "ItemPopup_GoldMat", new Color(0.32f, 0.1f, 0f, 1f), 0.28f);
            var lifeMat = EnsureMat(label.font, LifeMatPath, "ItemPopup_LifeMat", new Color(0.32f, 0f, 0.12f, 1f), 0.28f);
            label.fontStyle = FontStyles.Bold;
            EditorUtility.SetDirty(label);
            var so = new SerializedObject(popup);
            so.FindProperty("goldMaterial").objectReferenceValue = goldMat;
            so.FindProperty("lifeMaterial").objectReferenceValue = lifeMat;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(popupRoot, PopupPrefabPath);
            msg += "数値ポップアップに文字マテリアルを設定しました。";
        }
        finally { PrefabUtility.UnloadPrefabContents(popupRoot); }

        // ===== ② コインとハートの絵（05_GameのBlockItemManagerから） =====
        Sprite coin = null, heart = null;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var mgr = root.GetComponentInChildren<BlockItemManager>(true);
                if (mgr == null) continue;
                var mso = new SerializedObject(mgr);
                coin = mso.FindProperty("goldIcon").objectReferenceValue as Sprite;
                heart = mso.FindProperty("lifeIcon").objectReferenceValue as Sprite;
                break;
            }
        }
        finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }

        GameObject fxRoot = PrefabUtility.LoadPrefabContents(ItemFxPrefabPath);
        if (fxRoot == null) { Debug.LogError("[ItemCollectFxTool] " + ItemFxPrefabPath + " が見つかりません"); return; }
        try
        {
            var fx = fxRoot.GetComponent<ItemFXManager>();
            if (fx == null) { Debug.LogError("[ItemCollectFxTool] ItemFX.prefab に ItemFXManager がありません"); return; }
            var so = new SerializedObject(fx);
            if (coin != null) so.FindProperty("coinSprite").objectReferenceValue = coin;
            if (heart != null) so.FindProperty("heartSprite").objectReferenceValue = heart;
            // 光を控えめに（目が痛くならないよう）
            so.FindProperty("flashAlpha").floatValue = 0.12f;
            so.FindProperty("circleBonusFlashAlpha").floatValue = 0f; // 画面全体の光はなし
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(fxRoot, ItemFxPrefabPath);
            msg += $"　ItemFXにコインの絵={(coin != null ? coin.name : "見つからず")}、ハートの絵={(heart != null ? heart.name : "見つからず")}を設定しました。";
        }
        finally { PrefabUtility.UnloadPrefabContents(fxRoot); }

        AssetDatabase.SaveAssets();
        Debug.Log("[ItemCollectFxTool] " + msg);
    }

    /// <summary>くっきりした縁取り＋右下の影の文字マテリアル。無ければフォントのマテリアルから作る</summary>
    private static Material EnsureMat(TMP_FontAsset font, string path, string name, Color outline, float outlineWidth)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        // 文字のフォントを変えた時（Bungee等）は、前のフォントの文字画像を使ったマテリアルでは表示が崩れるため作り直す
        if (mat != null && mat.GetTexture(ShaderUtilities.ID_MainTex) != font.atlasTexture)
        {
            AssetDatabase.DeleteAsset(path);
            mat = null;
        }
        if (mat == null)
        {
            mat = new Material(font.material) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        // ★キーワードが無いと縁取り・光の値を入れても描画されない
        mat.EnableKeyword("OUTLINE_ON");
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.2f);
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.75f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.7f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.7f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
