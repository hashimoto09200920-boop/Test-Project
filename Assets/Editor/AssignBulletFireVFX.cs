using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AssignBulletFireVFX
{
    private const string PrefabRoot = "Assets/Prefabs/Effects/";

    // スプライトパスに含まれるキーワード → Prefab名 の対応（上から順に判定）
    private static readonly (string keyword, string prefabName)[] Rules =
    {
        ("親弾",         "FirePixelVFX_MultiWarhead"),
        ("子弾",         "FirePixelVFX_MultiWarheadChild"),
        ("02_Slow",      "FirePixelVFX_Slow"),
        ("03_Speed",     "FirePixelVFX_SpeedCurve"),
        ("04_Wave",      "FirePixelVFX_Wave"),
        ("05_Spiral",    "FirePixelVFX_Spiral"),
        ("06_Rapid",     "FirePixelVFX_Rapid"),
        ("07_Multi",     "FirePixelVFX_Multi"),
        ("08_Telegraph", "FirePixelVFX_Telegraph"),
        ("09_Missile",   "FirePixelVFX_Missile"),
        ("10_Countdown", "FirePixelVFX_Countdown"),
        ("11_Multi",     "FirePixelVFX_MultiWarhead"),
        ("12_Smoke",     "FirePixelVFX_Smoke"),
        ("13_Warp",      "FirePixelVFX_Warp"),
        ("Grenade",      "FirePixelVFX_Grenade"),
    };

    [MenuItem("Tools/Assign Bullet Fire VFX")]
    static void Assign()
    {
        var cache = new Dictionary<string, GameObject>();
        var normalPrefab = Load("FirePixelVFX_Normal", cache);
        foreach (var rule in Rules) Load(rule.prefabName, cache);

        int updated = 0, skipped = 0, dataCount = 0, btTotal = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:EnemyData"))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
            if (data == null || data.bulletTypes == null) continue;
            dataCount++;

            bool dirty = false;
            foreach (var bt in data.bulletTypes)
            {
                if (bt == null) continue;
                btTotal++;

                GameObject prefab = normalPrefab;

                if (bt.useMultiWarhead)
                {
                    // MultiWarheadはspriteOverrideではなくmultiParentSpriteを使うので個別判定
                    cache.TryGetValue("FirePixelVFX_MultiWarhead", out prefab);
                }
                else if (bt.spriteOverride != null)
                {
                    string spritePath = AssetDatabase.GetAssetPath(bt.spriteOverride);
                    if (!string.IsNullOrEmpty(spritePath))
                    {
                        foreach (var rule in Rules)
                        {
                            if (spritePath.Contains(rule.keyword))
                            {
                                cache.TryGetValue(rule.prefabName, out prefab);
                                break;
                            }
                        }
                    }
                }

                if (prefab == null) { skipped++; continue; }

                Debug.Log($"[AssignVFX] {System.IO.Path.GetFileNameWithoutExtension(assetPath)} / {bt.name} → {prefab.name}");
                bt.fireVfxPrefab = prefab;
                dirty = true;
                updated++;
            }

            if (dirty) EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Assign Bullet Fire VFX 完了",
            $"EnemyData: {dataCount}件\nBulletType合計: {btTotal}件\n更新: {updated}件\nスキップ: {skipped}件",
            "OK"
        );
    }

    private static GameObject Load(string name, Dictionary<string, GameObject> cache)
    {
        if (cache.ContainsKey(name)) return cache[name];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}{name}.prefab");
        if (prefab == null) Debug.LogWarning($"[AssignVFX] Prefab not found: {name}");
        cache[name] = prefab;
        return prefab;
    }
}
