using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// EnemyHealthDisplayのDebuff Icon（B4/B7/B8）が未設定のエネミーPrefabに、
/// 他のエネミーと同じ共通アイコンを一括設定するEditorスクリプト。
/// 既に何か設定済みのフィールドは上書きしない。
/// </summary>
public class FixMissingDebuffIcons : MonoBehaviour
{
    private const string PrefabFolder = "Assets/Prefabs/Enemies";
    private const string B4Path = "Assets/Art/Skill/B4.png";
    private const string B7Path = "Assets/Art/Skill/B7.png";
    private const string B8Path = "Assets/Art/Skill/B8.png";

    [MenuItem("Tools/Enemies/Fix Missing Debuff Icons")]
    public static void FixAll()
    {
        Sprite b4 = AssetDatabase.LoadAssetAtPath<Sprite>(B4Path);
        Sprite b7 = AssetDatabase.LoadAssetAtPath<Sprite>(B7Path);
        Sprite b8 = AssetDatabase.LoadAssetAtPath<Sprite>(B8Path);

        if (b4 == null || b7 == null || b8 == null)
        {
            Debug.LogError($"[FixMissingDebuffIcons] スプライトが読み込めませんでした。B4={b4}, B7={b7}, B8={b8}");
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
            SerializedProperty slowProp = so.FindProperty("slowDebuffSprite");
            SerializedProperty b7Prop = so.FindProperty("b7ShieldBreakSprite");
            SerializedProperty b8Prop = so.FindProperty("b8ShieldStopSprite");
            SerializedProperty sizeProp = so.FindProperty("debuffIconSize");
            SerializedProperty offsetProp = so.FindProperty("debuffIconOffset");
            SerializedProperty spacingProp = so.FindProperty("debuffIconSpacing");
            SerializedProperty durationOffsetYProp = so.FindProperty("debuffDurationTextOffsetY");

            bool changed = false;
            if (slowProp != null && slowProp.objectReferenceValue == null) { slowProp.objectReferenceValue = b4; changed = true; }
            if (b7Prop != null && b7Prop.objectReferenceValue == null) { b7Prop.objectReferenceValue = b7; changed = true; }
            if (b8Prop != null && b8Prop.objectReferenceValue == null) { b8Prop.objectReferenceValue = b8; changed = true; }

            // 他の全エネミーと同じ標準レイアウト値に統一する（スプライト設定とは独立に、
            // C#側のクラスデフォルト値のまま=未調整のものだけを対象にする）
            if (sizeProp != null && !Mathf.Approximately(sizeProp.floatValue, 0.07f)) { sizeProp.floatValue = 0.07f; changed = true; }
            if (offsetProp != null && offsetProp.vector2Value != new Vector2(0f, 0.3f)) { offsetProp.vector2Value = new Vector2(0f, 0.3f); changed = true; }
            if (spacingProp != null && !Mathf.Approximately(spacingProp.floatValue, 0.4f)) { spacingProp.floatValue = 0.4f; changed = true; }
            if (durationOffsetYProp != null && !Mathf.Approximately(durationOffsetYProp.floatValue, 0.3f)) { durationOffsetYProp.floatValue = 0.3f; changed = true; }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SavePrefabAsset(prefab);
                fixedCount++;
                Debug.Log($"[FixMissingDebuffIcons] Fixed: {path}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FixMissingDebuffIcons] Done. {fixedCount} prefab(s) updated.");
    }
}
