using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 全AreaConfig(Area1〜10)のvsBossThemeColorを、AreaConstellationFXのnodes配列で
    /// 使われている各Areaのテーマカラーと同じ値に設定する。
    /// ボス側のVS演出（火花・Rim Flash）は「ボス自身の色」ではなく「そのAreaのAreaカラー」を
    /// 使う方針のため（GravePoleの紫は偶然Area1カラーと近かっただけ）。
    /// </summary>
    public static class AreaBossThemeColorFixer
    {
        // AreaConstellationFXのnodes配列と同じ値（0.6, 0.56, 0.78等）
        private static readonly Color[] AreaColors =
        {
            new Color(0.60784316f, 0.56078434f, 0.78039217f), // Area1
            new Color(0.29803923f, 0.6862745f,  0.49019608f), // Area2
            new Color(0.5529412f,  0.6f,        0.68235296f), // Area3
            new Color(0.8784314f,  0.47843137f, 0.24705882f), // Area4
            new Color(0.69803923f, 0.22745098f, 0.32156864f), // Area5
            new Color(0.8784314f,  0.6901961f,  0.30980393f), // Area6
            new Color(0.30980393f, 0.56078434f, 0.8784314f),  // Area7
            new Color(0.37254903f, 0.8392157f,  0.8392157f),  // Area8
            new Color(0.6392157f,  0.68235296f, 0.8784314f),  // Area9
            new Color(0.91f,       0.79f,       0.42f),       // Area10
        };

        [MenuItem("Tools/VsIntro/Set All Area Boss Theme Colors To Area Colors")]
        public static void SetAll()
        {
            int applied = 0;
            for (int i = 0; i < AreaColors.Length; i++)
            {
                int areaNumber = i + 1;
                string path = $"Assets/Data/AreaConfigs/Area{areaNumber}Config.asset";
                var config = AssetDatabase.LoadAssetAtPath<AreaConfig>(path);
                if (config == null)
                {
                    Debug.LogWarning($"[AreaBossThemeColorFixer] {path} が見つかりません。スキップします。");
                    continue;
                }

                var so = new SerializedObject(config);
                var prop = so.FindProperty("vsBossThemeColor");
                if (prop == null)
                {
                    Debug.LogWarning($"[AreaBossThemeColorFixer] {path} にvsBossThemeColorフィールドが見つかりません。");
                    continue;
                }

                Color c = AreaColors[i];
                c.a = 1f;
                prop.colorValue = c;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                Debug.Log($"[AreaBossThemeColorFixer] Area{areaNumber}Config.vsBossThemeColor = {c}");
                applied++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AreaBossThemeColorFixer] 完了。{applied}件設定しました。");
        }
    }
}
