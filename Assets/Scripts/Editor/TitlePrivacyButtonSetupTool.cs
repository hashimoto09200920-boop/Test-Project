using System.IO;
using System.Linq;
using Game.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 01_Title のサウンド設定パネル（SoundPanel）の戻るボタンの横に、広告の「プライバシー設定」ボタンを追加する（2026/10/24）。
/// ・SoundPanel > BottomRow（PrivacyOptionsRow）> BackSlot > BackButton（元の戻るボタンをここへ移す）
///                                              > PrivacySlot > PrivacyButton（戻るボタンを複製し、アイコンを盾のマークにしたもの）
/// ・プライバシー設定のボタンは、広告の同意をあとから変える画面が必要な利用者（主にヨーロッパ）にだけ出る。それ以外は今までどおり
/// ・盾のアイコン（Assets/Art/中断画面/PrivacyIcon.png）が無ければ仮の画像を作る（あとで好きな画像に差し替えてよい）
/// 何度実行しても安全（既にあれば作り直さず、参照だけ合わせる）。
/// </summary>
public static class TitlePrivacyButtonSetupTool
{
    private const string ScenePath = "Assets/Scenes/01_Title.unity";
    private const string IconPath = "Assets/Art/中断画面/PrivacyIcon.png";

    [MenuItem("Tools/タイトル画面/サウンド設定にプライバシー設定のボタンを追加（01_Title）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var icon = EnsureIcon();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform panel = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            panel = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SoundPanel");
            if (panel != null) break;
        }
        if (panel == null) { EditorUtility.DisplayDialog("プライバシー設定のボタン", "01_Title に SoundPanel が見つかりません。", "OK"); return; }

        var row = panel.Find("BottomRow") as RectTransform;
        RectTransform backSlot, privacySlot;
        Button privacyBtn;
        if (row == null)
        {
            var back = panel.Find("BackButton") as RectTransform;
            if (back == null) { EditorUtility.DisplayDialog("プライバシー設定のボタン", "SoundPanel > BackButton が見つかりません。", "OK"); return; }
            int index = back.GetSiblingIndex();

            // 行（縦に並べる仕組みの中で、元の戻るボタンと同じ場所・大きさを取る）
            row = NewRect("BottomRow", panel, back.sizeDelta);
            row.SetSiblingIndex(index);
            var srcLe = back.GetComponent<LayoutElement>();
            var le = row.gameObject.AddComponent<LayoutElement>();
            if (srcLe != null) EditorUtility.CopySerialized(srcLe, le);

            backSlot = NewRect("BackSlot", row, back.sizeDelta);
            back.SetParent(backSlot, false);
            back.anchorMin = back.anchorMax = back.pivot = new Vector2(0.5f, 0.5f);
            back.anchoredPosition = Vector2.zero;

            privacySlot = NewRect("PrivacySlot", row, back.sizeDelta);
            var clone = Object.Instantiate(back.gameObject, privacySlot);
            clone.name = "PrivacyButton";
            var crt = (RectTransform)clone.transform;
            crt.anchoredPosition = Vector2.zero;
            privacyBtn = clone.GetComponent<Button>();
            if (privacyBtn != null)
            {
                // 複製元（戻る）のクリック先は消す。押した時の処理は PrivacyOptionsRow が付ける
                var bso = new SerializedObject(privacyBtn);
                bso.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").arraySize = 0;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }
            var iconT = clone.transform.Find("BackIcon") as RectTransform;
            if (iconT != null)
            {
                iconT.name = "PrivacyIcon";
                var img = iconT.GetComponent<Image>();
                if (img != null) { img.sprite = icon; img.preserveAspect = true; }
                iconT.sizeDelta = new Vector2(150f, 150f);
            }
            Undo.RegisterCreatedObjectUndo(row.gameObject, "Add Privacy Button");
        }
        else
        {
            backSlot = row.Find("BackSlot") as RectTransform;
            privacySlot = row.Find("PrivacySlot") as RectTransform;
            privacyBtn = privacySlot != null ? privacySlot.GetComponentInChildren<Button>(true) : null;
        }

        // プライバシー設定を開けなかった時の案内（行の少し上に重ねて出す暗い板＋文字。ふだんは非表示）
        var fail = row.Find("FailMessage") as RectTransform;
        TMPro.TextMeshProUGUI failText;
        if (fail == null)
        {
            fail = NewRect("FailMessage", row, new Vector2(560f, 110f));
            fail.anchoredPosition = new Vector2(0f, 120f);
            var plate = fail.gameObject.AddComponent<Image>();
            plate.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/GemFX/Gem_Pill.png");
            plate.type = Image.Type.Sliced;
            plate.color = new Color(0.04f, 0.06f, 0.13f, 0.9f);
            plate.raycastTarget = false;
            var tr = NewRect("Text", fail, Vector2.zero);
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(20f, 8f); tr.offsetMax = new Vector2(-20f, -8f);
            failText = tr.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NotoSansJP-Regular SDF.asset");
            if (font != null) { failText.font = font; failText.fontSharedMaterial = font.material; }
            failText.fontSize = 30f;
            failText.alignment = TMPro.TextAlignmentOptions.Center;
            failText.color = Color.white;
            failText.raycastTarget = false;
            failText.text = "プライバシー設定を開けませんでした。\n通信環境を確認してください";
            tr.gameObject.AddComponent<LanguageFontSwitch>().kind = LanguageFontSwitch.Kind.Body;
        }
        else failText = fail.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        fail.SetAsLastSibling();
        fail.gameObject.SetActive(false);
        AddLoc();

        var comp = row.GetComponent<PrivacyOptionsRow>();
        if (comp == null) comp = row.gameObject.AddComponent<PrivacyOptionsRow>();
        var so = new SerializedObject(comp);
        so.FindProperty("backSlot").objectReferenceValue = backSlot;
        so.FindProperty("privacySlot").objectReferenceValue = privacySlot;
        so.FindProperty("privacyButton").objectReferenceValue = privacyBtn;
        so.FindProperty("failMessage").objectReferenceValue = fail.gameObject;
        so.FindProperty("failMessageText").objectReferenceValue = failText;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (privacySlot != null) privacySlot.gameObject.SetActive(false); // ふだんは出さない（必要な利用者にだけ出る）

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TitlePrivacyButtonSetupTool] 01_Title のサウンド設定にプライバシー設定のボタンを追加して保存しました（SoundPanel > BottomRow）");
        EditorUtility.DisplayDialog("プライバシー設定のボタン",
            "追加して保存しました。\n2つ並んだ見た目は、SoundPanel > BottomRow の PrivacyOptionsRow で「Preview Pair In Editor」を ON にすると Play 前に確認できます（確認したら OFF に戻してください）。", "OK");
    }

    // 翻訳表に「開けなかった時」の文言を入れる（何度実行しても同じ結果）
    private static void AddLoc()
    {
        var table = AssetDatabase.LoadAssetAtPath<Game.Localization.LocalizationTable>("Assets/Resources/GameData/LocalizationTable.asset");
        if (table == null) return;
        const string key = "privacy.openFailed";
        var e = table.entries.Find(x => x != null && x.key == key);
        if (e == null) { e = new Game.Localization.LocalizationTable.Entry { key = key }; table.entries.Add(e); }
        e.japanese = "プライバシー設定を開けませんでした。\n通信環境を確認してください";
        e.english = "Couldn't open privacy settings.\nPlease check your connection.";
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return rt;
    }

    // 仮の盾のアイコン（青い盾に白いチェック）。既にあれば作らない（差し替えた画像を上書きしない）
    private static Sprite EnsureIcon()
    {
        if (!File.Exists(IconPath))
        {
            const int N = 256, SS = 4;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var border = new Color(0.12f, 0.36f, 0.85f, 1f);
            var top = new Color(0.55f, 0.8f, 1f, 1f);
            var bottom = new Color(0.2f, 0.52f, 0.98f, 1f);
            for (int py = 0; py < N; py++)
                for (int px = 0; px < N; px++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float x = ((px + (sx + 0.5f) / SS) / N) * 2f - 1f;   // -1〜1
                            float y = 1f - (py + (sy + 0.5f) / SS) / N;          // 0（上）〜1（下）
                            Color c = Color.clear;
                            if (InShield(x, y, 0f))
                            {
                                c = InShield(x, y, 0.09f) ? Color.Lerp(top, bottom, y) : border;
                                if (InCheck(x, y)) c = Color.white;
                            }
                            acc += c;
                        }
                    tex.SetPixel(px, py, acc / (SS * SS));
                }
            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(IconPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            Debug.Log($"[TitlePrivacyButtonSetupTool] 仮の盾のアイコンを作りました：{IconPath}（好きな画像に差し替えてよい）");
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    // 盾の形：上は少し反った横線、中ほどまでは同じ幅、下は丸みを帯びて先が尖る。inset＝内側へ縮める量
    private static bool InShield(float x, float y, float inset)
    {
        float topY = 0.08f + 0.05f * x * x + inset;
        if (y < topY || y > 0.94f - inset) return false;
        float half = y < 0.5f ? 0.82f : 0.82f * Mathf.Pow(Mathf.Clamp01(1f - (y - 0.5f) / 0.44f), 0.75f);
        return Mathf.Abs(x) <= half - inset;
    }

    // 白いチェックマーク
    private static bool InCheck(float x, float y)
    {
        const float w = 0.075f;
        return DistToSeg(x, y, -0.36f, 0.48f, -0.08f, 0.70f) < w || DistToSeg(x, y, -0.08f, 0.70f, 0.40f, 0.28f) < w;
    }

    private static float DistToSeg(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
        float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
