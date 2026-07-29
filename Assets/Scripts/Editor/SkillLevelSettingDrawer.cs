using UnityEngine;
using UnityEditor;
using Game.Skills;
using Game.Testing;

namespace Game.Editor
{
    /// <summary>
    /// SkillTestTool.SkillLevelSettingのInspector表示用。
    /// レベルスライダーの上限を、割り当てたSkillDefinitionのmaxAcquisitionCountに合わせて描画する
    /// （CategoryA=4回・CategoryB=3回・CategoryC=2回など、スキルごとの実際の上限と一致させるため）。
    /// </summary>
    [CustomPropertyDrawer(typeof(SkillTestTool.SkillLevelSetting))]
    public class SkillLevelSettingDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty skillProp = property.FindPropertyRelative("skill");
            SerializedProperty levelProp = property.FindPropertyRelative("level");

            float skillWidth = position.width * 0.5f;
            Rect skillRect = new Rect(position.x, position.y, skillWidth - 4f, position.height);
            Rect levelRect = new Rect(position.x + skillWidth, position.y, position.width - skillWidth, position.height);

            EditorGUI.PropertyField(skillRect, skillProp, GUIContent.none);

            SkillDefinition skill = skillProp.objectReferenceValue as SkillDefinition;

            if (skill == null)
            {
                EditorGUI.IntField(levelRect, levelProp.intValue);
            }
            else if (skill.maxAcquisitionCount <= 0)
            {
                // 0 = 無制限スキル：スライダーではなく通常の数値入力にする
                levelProp.intValue = Mathf.Max(0, EditorGUI.IntField(levelRect, levelProp.intValue));
            }
            else
            {
                int max = skill.maxAcquisitionCount;
                int clamped = Mathf.Clamp(levelProp.intValue, 0, max);
                levelProp.intValue = EditorGUI.IntSlider(levelRect, clamped, 0, max);
            }

            EditorGUI.EndProperty();
        }
    }
}
