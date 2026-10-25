using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 右上の所持数（スタミナ・ゴールド・虹の雫）に、03_AreaSelect と同じ暗い下地（FX_Plate）を付ける（2026/10/24）。
/// 対象：02_Gem・04_Drink・07_Shop。下地の画像・色・位置・大きさは AreaSelectScreenFXSetupTool（03_AreaSelect）と同じ。
/// 下地を付けると画面の右端で見切れるため、所持数の表示全体（下地・アイコン・数字）を少し左へ寄せる（970→940）。
/// 何度実行しても安全（既に下地があれば作り直さず、設定だけ合わせる）。
/// </summary>
public static class HudPlateSetupTool
{
    private static readonly string[] ScenePaths = { "Assets/Scenes/02_Gem.unity", "Assets/Scenes/04_Drink.unity", "Assets/Scenes/07_Shop.unity" };
    private const string PillPath = "Assets/Art/UI/GemFX/Gem_Pill.png";
    private const float HudCenterAnchoredX = 940f;  // 画面中央基準の時の X（元は 970＝下地が右端で見切れる、900＝左に寄りすぎ）
    private const float HudRightAnchoredX = -140f;  // 画面右端基準の時の X（中央基準の 940 と同じ位置）
    private static readonly Color PlateColor = new Color(0.04f, 0.06f, 0.13f, 0.72f); // 03_AreaSelect と同じ

    [MenuItem("Tools/画面共通/右上の所持数に下地を付ける（02_Gem・04_Drink・07_Shop）")]
    private static void Run()
    {
        var pill = AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
        if (pill == null) { Debug.LogError($"[HudPlateSetupTool] {PillPath} が見つかりません"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var log = new System.Text.StringBuilder();
        foreach (var path in ScenePaths)
        {
            if (!System.IO.File.Exists(path)) { log.AppendLine($"{path}：ありません"); continue; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var hud in root.GetComponentsInChildren<StaminaHUD>(true)) count += AddPlate(hud.transform, pill);
                foreach (var hud in root.GetComponentsInChildren<GoldHUD>(true)) count += AddPlate(hud.transform, pill);
                foreach (var hud in root.GetComponentsInChildren<InfiniteStoneHUD>(true)) count += AddPlate(hud.transform, pill);
            }
            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            log.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path)}：{count}個に下地を付けました");
        }
        Debug.Log("[HudPlateSetupTool] 右上の所持数の下地\n" + log);
        EditorUtility.DisplayDialog("所持数の下地", log.ToString(), "OK");
    }

    // アイコン〜数字の後ろに敷く（03_AreaSelect と同じ：左端から 12、幅 200・高さ 54、角丸の板）
    private static int AddPlate(Transform hud, Sprite pill)
    {
        // 表示全体を左へ寄せる（下地が右端で見切れないぎりぎりより少し内側）
        if (hud is RectTransform hr)
        {
            if (Mathf.Approximately(hr.anchorMin.x, 0.5f)) hr.anchoredPosition = new Vector2(HudCenterAnchoredX, hr.anchoredPosition.y);
            else if (Mathf.Approximately(hr.anchorMin.x, 1f)) hr.anchoredPosition = new Vector2(HudRightAnchoredX, hr.anchoredPosition.y);
        }
        var t = hud.Find("FX_Plate") as RectTransform;
        if (t == null)
        {
            var go = new GameObject("FX_Plate", typeof(RectTransform));
            go.layer = hud.gameObject.layer;
            t = (RectTransform)go.transform;
            t.SetParent(hud, false);
            Undo.RegisterCreatedObjectUndo(go, "Add HUD Plate");
        }
        t.SetSiblingIndex(0);
        t.anchorMin = t.anchorMax = new Vector2(0f, 0.5f);
        t.pivot = new Vector2(0f, 0.5f);
        t.anchoredPosition = new Vector2(12f, 0f);
        t.sizeDelta = new Vector2(200f, 54f);
        var img = t.GetComponent<Image>();
        if (img == null) img = t.gameObject.AddComponent<Image>();
        img.sprite = pill;
        img.type = Image.Type.Sliced;
        img.color = PlateColor;
        img.raycastTarget = false;
        return 1;
    }
}
