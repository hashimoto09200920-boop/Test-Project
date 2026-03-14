using UnityEditor;
using UnityEngine;
using Game.Shop;

[CustomEditor(typeof(DrinkDefinition))]
public class DrinkDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("drinkName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("iconDisplaySize"), new GUIContent("アイコン表示サイズ", "0,0の場合はデフォルト160x160を使用"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("効果モード", EditorStyles.boldLabel);
        var isFixedProp = serializedObject.FindProperty("isFixed");
        isFixedProp.boolValue = EditorGUILayout.Popup(
            new GUIContent("モード", "固定: 全スキル確定上昇 / 変動: ランダム選択"),
            isFixedProp.boolValue ? 0 : 1,
            new[] { "固定（全スキル確定上昇）", "変動（ランダム選択）" }
        ) == 0;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("効果 - 対象スキル（最大3つ）", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetSkill1"), new GUIContent("対象スキル 1"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetSkill2"), new GUIContent("対象スキル 2"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetSkill3"), new GUIContent("対象スキル 3"));

        EditorGUILayout.Space();
        if (isFixedProp.boolValue)
        {
            EditorGUILayout.LabelField("【固定】設定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("levelUpCount"), new GUIContent("Level Up Count", "全対象スキルの上昇ポイント"));
        }
        else
        {
            EditorGUILayout.LabelField("【変動】設定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("selectionCount"), new GUIContent("Selection Count", "ランダムで選ぶ回数（重複あり）"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minPoint"), new GUIContent("最小ポイント", "選択されたスキルの最小上昇値"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxPoint"), new GUIContent("最大ポイント", "選択されたスキルの最大上昇値"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("価格", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("price"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("SE", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("purchaseSE"), new GUIContent("購入SE", "購入時に再生するSE（未設定の場合はShopUIのbuySEを使用）"));

        serializedObject.ApplyModifiedProperties();
    }
}
