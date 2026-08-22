using UnityEngine;
using UnityEditor;

/// <summary>
/// EnemyHealthDisplayのnumberFont（HP/Shield数値のフォント）が未設定のエネミーPrefabに、
/// プロジェクト共通フォント（NotoSansJP-Regular.ttf）を一括設定するEditorスクリプト。
/// フォント未指定だとPC/モバイルで異なるフォールバックフォントが使われ、文字幅の違いから
/// 数値の表示位置（右寄せ基準）がプラットフォームごとにズレる不具合の対策。
/// 既にnumberFontが設定済みのPrefabは上書きしない。
/// </summary>
public class FixEnemyHealthDisplayFont : MonoBehaviour
{
    private const string PrefabFolder = "Assets/Prefabs/Enemies";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular.ttf";

    [MenuItem("Tools/Enemies/Fix Missing HP Number Font")]
    public static void FixAll()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[FixEnemyHealthDisplayFont] フォントが読み込めませんでした: {FontPath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            EnemyHealthDisplay display = prefab.GetComponent<EnemyHealthDisplay>();
            if (display == null) continue;

            SerializedObject so = new SerializedObject(display);
            SerializedProperty fontProp = so.FindProperty("numberFont");
            if (fontProp == null) continue;

            if (fontProp.objectReferenceValue == null)
            {
                fontProp.objectReferenceValue = font;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SavePrefabAsset(prefab);
                fixedCount++;
                Debug.Log($"[FixEnemyHealthDisplayFont] Fixed: {path}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FixEnemyHealthDisplayFont] Done. {fixedCount} prefab(s) updated.");
    }
}
