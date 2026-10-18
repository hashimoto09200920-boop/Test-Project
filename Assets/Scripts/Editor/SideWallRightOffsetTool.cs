using UnityEditor;
using UnityEngine;

/// <summary>
/// IronNest・Fortress の右の縦の壁を、画面の右端にぴったり揃える値を設定する（2026/10/18）。
/// 右の壁の位置は横の壁のブロック幅（Block Width＝1）で計算しているが、縦の壁のブロック（Block_SideWall）の見た目の幅は0.5のため、
/// Side Wall Right X Offset を -0.25 にすると計算上は右端が画面の端に接するが、絵の透明な余白ぶん内側に見えたため -0.3 にする（マイナスで右へ動く）。
/// 何度実行しても同じ結果。値はプレハブの Inspector でいつでも調整できる。
/// </summary>
public static class SideWallRightOffsetTool
{
    private const float Offset = -0.3f;

    [MenuItem("Tools/IronNest/右の縦の壁を画面の右端に揃える（IronNest・Fortress）")]
    private static void Run()
    {
        Apply<IronNestWalls>("Assets/Prefabs/Enemies/IronNest.prefab");
        Apply<FortressEnemy>("Assets/Prefabs/Enemies/Fortress.prefab");
    }

    private static void Apply<T>(string path) where T : Component
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var comp = root.GetComponentInChildren<T>(true);
            if (comp == null) { Debug.LogError($"[SideWallRightOffsetTool] {path} に {typeof(T).Name} が見つかりません"); return; }
            var so = new SerializedObject(comp);
            var p = so.FindProperty("sideWallRightXOffset");
            if (p == null) { Debug.LogError($"[SideWallRightOffsetTool] {typeof(T).Name} に sideWallRightXOffset がありません"); return; }
            float old = p.floatValue;
            p.floatValue = Offset;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[SideWallRightOffsetTool] {path} > {comp.gameObject.name} > {typeof(T).Name} > Side Wall Right X Offset：{old} → {Offset}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
