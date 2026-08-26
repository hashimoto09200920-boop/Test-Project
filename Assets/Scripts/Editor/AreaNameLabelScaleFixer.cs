using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// AreaConstellationFXのエリア名ラベル画像（①〜⑪）は、AI生成のたびに文字の太さ・余白比率が
    /// ブレるため、そのままだとエリアごとにフォントサイズが揃わない。
    /// 各画像を実測した「文字の高さ／キャンバス高さ」の比率をもとに、①Soul Townを基準(1.0)として
    /// 逆比例の補正倍率(nameSpriteScale / areaNameLockedSpriteScale)を計算し、一括で設定する。
    /// ★初回実装時は不透明度5%以上を「文字」とみなして測定していたが、①Soul Townだけ他画像より
    /// 　グロー（発光のにじみ）が柔らかく広いため、この閾値だと実際の文字の太さ（芯）より
    /// 　大きく測定されてしまい、①だけ基準がズレて②〜⑩が①より大きく補正されてしまった
    /// 　（②〜⑩同士は揃うのに①だけ違って見えるバグの原因）。
    /// 　不透明度50%以上（にじみを除いた文字の芯）に測定基準を統一して再計算した値を使用する。
    /// 実測手順：PNGの不透明度50%以上ピクセルの境界ボックス高さ / キャンバス高さ(288px) を算出。
    /// 　① Soul Town  0.302 (基準)
    /// 　② Jungle     0.313
    /// 　③ Iron Nest  0.302
    /// 　④ Deep Night 0.274
    /// 　⑤ Theater    0.306
    /// 　⑥ Sand Ruins 0.243
    /// 　⑦ Tower      0.330
    /// 　⑧ Starward   0.260
    /// 　⑨ Cosmos     0.299
    /// 　⑩ Zenith     0.313
    /// 　⑪ ？         0.385（2026/8/25 塗りつぶし版に作り直し後の実測値）
    /// </summary>
    public static class AreaNameLabelScaleFixer
    {
        // nodes配列のインデックス順（Area_01〜Area_10）に対応。0.302(Soul Town実測値) / 各実測値。
        private static readonly float[] NodeScales =
        {
            1.000f, // Area_01 Soul Town
            0.965f, // Area_02 Jungle
            1.000f, // Area_03 Iron Nest
            1.102f, // Area_04 Deep Night
            0.987f, // Area_05 Theater
            1.243f, // Area_06 Sand Ruins
            0.915f, // Area_07 Tower
            1.162f, // Area_08 Starward
            1.010f, // Area_09 Cosmos
            0.965f, // Area_10 Zenith
        };

        private const float LockedScale = 0.784f; // ⑪ ？（2回目生成分・実測0.385を基準0.302に合わせる）

        [MenuItem("Tools/AreaSelect/Apply Measured Area Name Label Scale")]
        public static void Apply()
        {
            var fx = Object.FindFirstObjectByType<AreaConstellationFX>(FindObjectsInactive.Include);
            if (fx == null)
            {
                Debug.LogError("[AreaNameLabelScaleFixer] AreaConstellationFXが見つかりません。03_AreaSelectシーンを開いてから実行してください。");
                return;
            }

            var so = new SerializedObject(fx);
            var nodesProp = so.FindProperty("nodes");
            if (nodesProp == null)
            {
                Debug.LogError("[AreaNameLabelScaleFixer] nodesフィールドが見つかりません。");
                return;
            }

            int applied = 0;
            for (int i = 0; i < nodesProp.arraySize && i < NodeScales.Length; i++)
            {
                var elem = nodesProp.GetArrayElementAtIndex(i);
                var scaleProp = elem.FindPropertyRelative("nameSpriteScale");
                var areaIdProp = elem.FindPropertyRelative("areaId");
                if (scaleProp == null)
                {
                    Debug.LogWarning($"[AreaNameLabelScaleFixer] nodes[{i}]にnameSpriteScaleが見つかりません。");
                    continue;
                }
                scaleProp.floatValue = NodeScales[i];
                Debug.Log($"[AreaNameLabelScaleFixer] nodes[{i}] ({areaIdProp?.stringValue}) の nameSpriteScale = {NodeScales[i]}");
                applied++;
            }

            var lockedScaleProp = so.FindProperty("areaNameLockedSpriteScale");
            if (lockedScaleProp != null)
            {
                lockedScaleProp.floatValue = LockedScale;
                Debug.Log($"[AreaNameLabelScaleFixer] areaNameLockedSpriteScale = {LockedScale}");
                applied++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(fx);
            EditorSceneManager.MarkSceneDirty(fx.gameObject.scene);
            Debug.Log($"[AreaNameLabelScaleFixer] 完了。{applied}件設定しました。シーンを保存してから「Build Constellation」→「Add Hover Effect To All Area Buttons」の順で再実行してください。");
        }
    }
}
