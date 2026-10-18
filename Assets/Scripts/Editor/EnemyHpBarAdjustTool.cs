using UnityEditor;
using UnityEngine;

/// <summary>
/// 個別エネミーのHPバーの微調整（EnemyHealthDisplay の Unified Width Scale / Unified Offset X / Unified Offset Y）を各Prefabに設定する。
/// Prefabはファイルを直接編集せず、開いて設定して保存する。何度実行しても同じ値になる。
/// </summary>
public static class EnemyHpBarAdjustTool
{
    // (Prefab名, 横幅の倍率, 左右のずらし, 上下のずらし)
    private static readonly (string prefab, float widthScale, float offsetX, float offsetY)[] Targets =
    {
        ("Fingers",   1.5f,  0f,    0f),    // Area5ボス：長さ1.5倍。左右はBossHandの位置に合わせる（FollowX）
        ("Shaman",    1f,    0f,   -0.55f), // バーが高すぎる→下へ
        ("ArcGuard",  1f,    0f,   -1.0f),  // バーが高すぎる（尾を含めた絵の上端で測っている）→下へ
        ("Minimo",    1.5f,  0f,    0f),
        ("IronNest",  1.5f,  0f,    0f),
        ("Obelisk",   1.5f,  0f,    0f),
        ("GravePole", 3f,    0f,    0f),    // 2倍→さらに1.5倍
    };

    [MenuItem("Tools/エネミーHP表示/個別のバー調整を設定（Fingers・Shaman・ArcGuard・Minimo・IronNest・Obelisk・GravePole）")]
    private static void Run()
    {
        foreach (var t in Targets)
        {
            string path = $"Assets/Prefabs/Enemies/{t.prefab}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) { Debug.LogError($"[EnemyHpBarAdjustTool] {path} が見つかりません"); continue; }
            try
            {
                var ehd = root.GetComponent<EnemyHealthDisplay>();
                if (ehd == null) { Debug.LogError($"[EnemyHpBarAdjustTool] {t.prefab} のルートに EnemyHealthDisplay がありません"); continue; }
                var so = new SerializedObject(ehd);
                so.FindProperty("unifiedWidthScale").floatValue = t.widthScale;
                so.FindProperty("unifiedOffsetX").floatValue = t.offsetX;
                so.FindProperty("unifiedOffsetY").floatValue = t.offsetY;
                // Fingers：バーの左右の中心をBossHand（フェーズ・コマで位置が変わる本体）に合わせる
                if (t.prefab == "Fingers")
                {
                    var hand = root.transform.Find("BossHand");
                    if (hand == null) foreach (var tr in root.GetComponentsInChildren<Transform>(true)) if (tr.name == "BossHand") { hand = tr; break; }
                    if (hand == null) Debug.LogError("[EnemyHpBarAdjustTool] Fingers に BossHand が見つかりません");
                    so.FindProperty("unifiedFollowX").objectReferenceValue = hand;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ehd);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[EnemyHpBarAdjustTool] {t.prefab}：横幅の倍率={t.widthScale}、左右={t.offsetX}、上下={t.offsetY}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
