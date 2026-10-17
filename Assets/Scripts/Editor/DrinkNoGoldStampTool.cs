using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ドリンク画面の「ゴールド不足」表示を、言語に関係なく分かる「コイン＋禁止マーク＋不足額」にする（04_Drink のドリンクカードの雛形）。
/// 作るもの（ShopUI.drinkCardTemplate > FX_NoGold の下）：
///   Stamp（表示時にDrinkScreenFXが大きい状態から「ドン」と押す動きを1回だけ付ける入れ物）
///     ├ Coin（金色のコイン。カードの価格の横のコインと同じ絵）
///     ├ BanOutline（禁止マークの濃い縁取り）/ Ban（赤い禁止マーク：丸に斜線）
///     └ Shortage（足りない金額「-30」。DrinkScreenFXが実際の不足額を入れる）
///   Text / Shadow（以前の「ゴールド不足」の文字。使わないので非表示）
/// カードを暗くする板（FX_NoGold本体のImage）は不透明度75%。
/// 何度実行しても同じ形に作り直す（位置・大きさ・色を手で調整した後は再実行しないこと）。
/// ※全体を作り直す「見やすさ改善（1回目）」は実行し直さなくてよい。
/// </summary>
public static class DrinkNoGoldStampTool
{
    private const string ScenePath = "Assets/Scenes/04_Drink.unity";
    private const string BanPath = "Assets/Art/UI/DrinkFX/NoGold_Ban.png";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
    private const string MatPath = "Assets/Art/UI/DrinkFX/NoGold_NeonText.mat";

    private static readonly Color BanRed = new Color(1f, 0.15f, 0.2f, 1f);
    private static readonly Color BanOutlineColor = new Color(0.12f, 0f, 0.02f, 0.9f);
    private static readonly Color ShortageRed = new Color(1f, 0.32f, 0.35f, 1f);
    private const float DimAlpha = 0.75f;

    [MenuItem("Tools/ドリンク画面/ゴールド不足をコイン＋禁止マークにする（04_Drink）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var ban = LoadSprite(BanPath);
        if (ban == null) { Debug.LogError($"[DrinkNoGoldStampTool] 禁止マークの画像が見つかりません：{BanPath}"); return; }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError($"[DrinkNoGoldStampTool] フォントが見つかりません：{FontPath}"); return; }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            ShopUI shop = null;
            foreach (var root in scene.GetRootGameObjects()) { shop = root.GetComponentInChildren<ShopUI>(true); if (shop != null) break; }
            if (shop == null) { Debug.LogError("[DrinkNoGoldStampTool] 04_DrinkにShopUIが見つかりません"); return; }
            var tplUI = new SerializedObject(shop).FindProperty("drinkCardTemplate").objectReferenceValue as DrinkCardUI;
            if (tplUI == null) { Debug.LogError("[DrinkNoGoldStampTool] ShopUIのdrinkCardTemplateが未設定です"); return; }
            var card = tplUI.transform;
            var ng = card.Find("FX_NoGold") as RectTransform;
            if (ng == null) { Debug.LogError("[DrinkNoGoldStampTool] ドリンクカードにFX_NoGoldがありません。先に「見やすさ改善（1回目）」を実行してください"); return; }

            // カードを暗くする板（買えないカードの内容を沈める）
            var dim = ng.GetComponent<Image>();
            if (dim != null) { var dc = dim.color; dc.a = DimAlpha; dim.color = dc; EditorUtility.SetDirty(dim); }

            // ===== 入れ物（以前のはんこの部品・文字は片付ける） =====
            var stamp = Child(ng, "Stamp");
            foreach (var old in new[] { "Fill", "FrameOuter", "FrameInner" })
            {
                var o = stamp.Find(old);
                if (o != null) Object.DestroyImmediate(o.gameObject);
            }
            // 以前の文字は入れ物の外（FX_NoGold直下）へ戻して非表示にする（DrinkScreenFXが文言を入れても見えない）
            foreach (var name in new[] { "Text", "Shadow" })
            {
                var t = stamp.Find(name) ?? ng.Find(name);
                if (t == null) continue;
                t.SetParent(ng, false);
                t.gameObject.SetActive(false);
            }
            stamp.anchorMin = stamp.anchorMax = stamp.pivot = new Vector2(0.5f, 0.5f);
            stamp.anchoredPosition = new Vector2(0f, 14f);
            stamp.sizeDelta = new Vector2(220f, 220f);
            stamp.localRotation = Quaternion.identity;
            stamp.localScale = Vector3.one;
            stamp.SetAsLastSibling();

            // ===== コイン（カードの価格の横のコインと同じ絵） =====
            Sprite coinSprite = card.Find("NamePriceRow/GoldIcon")?.GetComponent<Image>()?.sprite;
            var coin = Child(stamp, "Coin");
            Center(coin, new Vector2(0f, 18f), new Vector2(118f, 118f));
            var coinImg = Img(coin, coinSprite, Color.white);
            coinImg.preserveAspect = true;

            // ===== 禁止マーク（濃い縁取り＋赤） =====
            var banOutline = Child(stamp, "BanOutline");
            Center(banOutline, new Vector2(0f, 18f), new Vector2(176f, 176f));
            Img(banOutline, ban, BanOutlineColor);
            var banImg = Child(stamp, "Ban");
            Center(banImg, new Vector2(0f, 18f), new Vector2(164f, 164f));
            Img(banImg, ban, BanRed);

            // ===== 不足額（数字なので言語に関係ない） =====
            var shortage = Child(stamp, "Shortage");
            Center(shortage, new Vector2(0f, -88f), new Vector2(260f, 60f));
            var st = shortage.GetComponent<TextMeshProUGUI>();
            if (st == null) st = shortage.gameObject.AddComponent<TextMeshProUGUI>();
            st.font = font;
            st.fontSharedMaterial = EnsureTextMaterial(font);
            st.fontSize = 46f;
            st.fontStyle = FontStyles.Bold;
            st.color = ShortageRed;
            st.alignment = TextAlignmentOptions.Center;
            st.textWrappingMode = TextWrappingModes.NoWrap;
            st.raycastTarget = false;
            st.text = "-30"; // 表示時にDrinkScreenFXが実際の不足額を入れる

            coin.SetSiblingIndex(0);
            banOutline.SetSiblingIndex(1);
            banImg.SetSiblingIndex(2);
            shortage.SetSiblingIndex(3);

            EditorUtility.SetDirty(tplUI.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DrinkNoGoldStampTool] 04_Drink：ドリンクカード > FX_NoGold > Stamp（コイン＋禁止マーク＋不足額）を作って保存しました" +
                      (coinSprite == null ? "。※NamePriceRow/GoldIconの画像が見つからず、コインの画像は未設定です（Stamp > Coin のImageに設定してください）" : ""));
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>画像をSprite（Single）として読み込む。Spriteとして取り込まれていなければ設定し直す</summary>
    private static Sprite LoadSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>不足額の文字用TMPマテリアル（濃い赤の縁取り＋小さな影）。無ければフォントのマテリアルから作る</summary>
    private static Material EnsureTextMaterial(TMP_FontAsset font)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(font.material) { name = "NoGold_NeonText" };
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        // ★キーワードが無いと縁取り・影の値を入れても描画されない
        mat.EnableKeyword("OUTLINE_ON");
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.2f, 0f, 0.03f, 1f));
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.6f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static RectTransform Child(Transform parent, string name)
    {
        var t = parent.Find(name) as RectTransform;
        if (t != null) return t;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
    }

    private static Image Img(RectTransform rt, Sprite s, Color c)
    {
        var im = rt.GetComponent<Image>();
        if (im == null) im = rt.gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.type = Image.Type.Simple; im.raycastTarget = false; im.preserveAspect = false;
        return im;
    }
}
