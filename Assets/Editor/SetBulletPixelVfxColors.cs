using UnityEditor;
using UnityEngine;

/// <summary>
/// BulletType.pixelVfxColorを弾スプライトの色に合わせて設定するEditorツール。
/// GUIDではなくスプライトのパス名で判定するため確実に動作する。
/// </summary>
public static class SetBulletPixelVfxColors
{
    // ── 全リセット ─────────────────────────────────────────
    [MenuItem("Tools/Set Bullet Pixel VFX Color/[Reset All to White]")]
    static void ResetAll()
    {
        ApplyColor(Color.white, bt => true, "RESET ALL");
    }

    // ── 01_Normal: spriteOverride == null (デフォルト赤弾) ──
    [MenuItem("Tools/Set Bullet Pixel VFX Color/01 Normal (red-orange)")]
    static void Set01Normal()
    {
        ApplyColor(new Color(1f, 0.30f, 0.03f), IsNormal, "01_Normal");
    }

    // ── 02_Slow: 紫 ──────────────────────────────────────
    [MenuItem("Tools/Set Bullet Pixel VFX Color/02 Slow (purple)")]
    static void Set02Slow()
    {
        ApplyColor(new Color(0.65f, 0.05f, 0.85f), bt => MatchPath(bt, "02_Slow"), "02_Slow");
    }

    // ── 03_Speed Curve: 緑 ───────────────────────────────
    [MenuItem("Tools/Set Bullet Pixel VFX Color/03 Speed Curve (green)")]
    static void Set03SpeedCurve()
    {
        ApplyColor(new Color(0.2f, 0.9f, 0.05f), bt => MatchPath(bt, "03_Speed"), "03_Speed Curve");
    }

    // ── 判定ヘルパー ─────────────────────────────────────

    /// spriteOverride == null → 01_Normal（デフォルト赤弾）
    private static bool IsNormal(EnemyData.BulletType bt)
    {
        return bt.spriteOverride == null;
    }

    /// spriteOverride のパスが nameContains を含むか
    private static bool MatchPath(EnemyData.BulletType bt, string nameContains)
    {
        if (bt.spriteOverride == null) return false;
        string path = AssetDatabase.GetAssetPath(bt.spriteOverride);
        return !string.IsNullOrEmpty(path) && path.Contains(nameContains);
    }

    // ── 共通処理 ─────────────────────────────────────────
    private static void ApplyColor(Color color, System.Func<EnemyData.BulletType, bool> predicate, string label)
    {
        int updated = 0;
        int skipped = 0;
        int dataCount = 0;
        int btTotal = 0;

        string[] guids = AssetDatabase.FindAssets("t:EnemyData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null) { Debug.LogWarning($"[VFXColor] null data: {path}"); continue; }
            if (data.bulletTypes == null) { Debug.LogWarning($"[VFXColor] bulletTypes null: {path}"); continue; }
            dataCount++;

            bool dirty = false;
            foreach (var bt in data.bulletTypes)
            {
                if (bt == null) continue;
                btTotal++;
                bool match = predicate(bt);
                Debug.Log($"[VFXColor] {System.IO.Path.GetFileNameWithoutExtension(path)} / {bt.name} | sprite={bt.spriteOverride} | match={match}");
                if (!match) { skipped++; continue; }

                bt.pixelVfxColor = color;
                dirty = true;
                updated++;
            }

            if (dirty) EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            $"Pixel VFX Color 設定完了 - {label}",
            $"EnemyData: {dataCount}件\nBulletType合計: {btTotal}件\n更新: {updated} 件\nスキップ: {skipped} 件",
            "OK"
        );
    }
}
