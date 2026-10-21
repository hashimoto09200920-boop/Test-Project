using UnityEditor;
using UnityEngine;

/// <summary>
/// シールドにJustで当てた時のダメージ数字を、HPのJust（黄色の文字＋赤い縁）と同じ作りの「明るい文字＋はっきりした縁」にする（2026/10/20）。
/// ・全エネミーPrefabの EnemyHitFeedback「Popup Shield Powered Color」を、ほぼ白の水色 #CFEFFF (0.81, 0.94, 1.00) に一括で変える。
///   旧の濃い青 (0.10, 0.35, 0.90) と、一度試した明るい青 #3D8BFF (0.24, 0.55, 1.00) のものだけを変えるので、個別に変えた色は上書きしない。
/// ・マテリアル DamagePopup_JustMat_Shield の縁取りを、濃い青 #1A3DFF・太さ 0.1（HPのJustと同じ太さ）にする。
/// 再実行しても同じ結果になる。
/// </summary>
public static class ShieldJustPopupColorBulkTool
{
    private static readonly Color[] OldColors =
    {
        new Color(0.10f, 0.35f, 0.90f, 1f), // 旧：濃い青
        new Color(0.24f, 0.55f, 1.00f, 1f), // 試した：明るい青 #3D8BFF
    };
    private static readonly Color NewFaceColor    = new Color(0.81f, 0.94f, 1.00f, 1f); // #CFEFFF
    private static readonly Color NewOutlineColor = new Color(0.10f, 0.24f, 1.00f, 1f); // #1A3DFF
    private const float NewOutlineWidth = 0.1f;
    private const string MaterialPath = "Assets/Prefabs/Effects/DamagePopup_JustMat_Shield.mat";

    [MenuItem("Tools/Enemies/シールドJustダメージの文字色を見やすく（全エネミー）")]
    private static void Apply()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        var targets = new System.Collections.Generic.List<string>();
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            foreach (var fb in go.GetComponentsInChildren<EnemyHitFeedback>(true))
            {
                var so = new SerializedObject(fb);
                var prop = so.FindProperty("popupShieldPoweredColor");
                if (prop != null && IsOld(prop.colorValue)) { targets.Add(path); break; }
            }
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (!EditorUtility.DisplayDialog("シールドJustの文字色",
                $"・{targets.Count}個のPrefabの「Popup Shield Powered Color」を #CFEFFF（ほぼ白の水色）に変えます。\n" +
                $"・マテリアル DamagePopup_JustMat_Shield の縁取りを #1A3DFF（濃い青）・太さ {NewOutlineWidth} にします。" +
                (mat == null ? "\n（マテリアルが見つからないため、こちらは変更しません）" : "") +
                "\n\n一覧はConsoleに出力します。続けますか？",
                "変更する", "キャンセル"))
            return;

        int changed = 0;
        var listText = new System.Text.StringBuilder();
        foreach (string path in targets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;
            foreach (var fb in root.GetComponentsInChildren<EnemyHitFeedback>(true))
            {
                var so = new SerializedObject(fb);
                var prop = so.FindProperty("popupShieldPoweredColor");
                if (prop == null || !IsOld(prop.colorValue)) continue;
                prop.colorValue = NewFaceColor;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
                changed++;
                listText.AppendLine($"{path} : {fb.gameObject.name}");
            }
            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (mat != null)
        {
            Undo.RecordObject(mat, "Shield Just Outline");
            mat.SetColor("_OutlineColor", NewOutlineColor);
            mat.SetFloat("_OutlineWidth", NewOutlineWidth);
            mat.EnableKeyword("OUTLINE_ON");
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[ShieldJustPopupColorBulkTool] シールドJustの文字色を #CFEFFF に変更（{changed}件）" +
                  (mat != null ? $"・縁取りを #1A3DFF 太さ{NewOutlineWidth} に変更" : "・マテリアル未検出") + $"\n{listText}");
        EditorUtility.DisplayDialog("シールドJustの文字色", $"文字色：{changed}件を変更しました。" + (mat != null ? "\n縁取り：変更しました。" : ""), "OK");
    }

    private static bool IsOld(Color c)
    {
        foreach (var o in OldColors)
            if (Mathf.Abs(c.r - o.r) < 0.005f && Mathf.Abs(c.g - o.g) < 0.005f && Mathf.Abs(c.b - o.b) < 0.005f) return true;
        return false;
    }
}
