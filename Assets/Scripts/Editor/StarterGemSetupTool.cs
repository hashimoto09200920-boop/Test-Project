using Game.Gems;
using Game.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 初期ジェム（2026/11/10）をPlay前に作るメニュー。
/// ① Assets/Resources/GameData/Gems に初期ジェムの設定（Gem_Starter01〜03）を作る（既にあれば中身だけ合わせる）
/// ② 翻訳表（LocalizationTable）に名前（日本語・英語）を入れる
/// ③ 02_Gem を開いていれば、ジェムのカードの見本（GemItemTemplate）に「STARTER」の札（FX_StarterTag）を作る
/// 配るのは ProgressManager（起動時・進み具合を全部消した時に1回だけ）。値はInspectorで後から変えられる
/// </summary>
public static class StarterGemSetupTool
{
    private const string GemFolder = "Assets/Resources/GameData/Gems/";
    private const string SkillFolder = "Assets/Resources/GameData/Skills/";
    private const string LocPath = "Assets/Resources/GameData/LocalizationTable.asset";

    private struct Spec
    {
        public string asset, nameJa, nameEn, skill;
        public bool equip;
    }

    private static readonly Spec[] Specs =
    {
        new Spec { asset = "Gem_Starter01", nameJa = "連線の始石", nameEn = "Linkline Seedstone",   skill = "Skill_A7_MaxStrokesUp",          equip = true  },
        new Spec { asset = "Gem_Starter02", nameJa = "白光の始石", nameEn = "Whitelight Seedstone", skill = "Skill_A1_LeftMaxCostUp",         equip = false },
        new Spec { asset = "Gem_Starter03", nameJa = "跳光の始石", nameEn = "Rebound Seedstone",    skill = "Skill_A8_ReflectedBulletSpeedUp", equip = false },
    };

    private const int SellPrice = 10;
    private static readonly Color StarterTagColor = new Color(0.3f, 0.85f, 1f, 1f); // 装備中の札（オレンジ）と見分けるシアン

    [MenuItem("Tools/ジェム画面/初期ジェムを作成（02_Gemを開いて実行）")]
    private static void Build()
    {
        var log = new System.Text.StringBuilder();

        // ① ジェムの設定
        var template = AssetDatabase.LoadAssetAtPath<GemDefinition>(GemFolder + "Gem_Area01.asset");
        foreach (var s in Specs)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(SkillFolder + s.skill + ".asset") == null)
            {
                log.AppendLine($"⚠ スキル {s.skill} が見つかりません（{s.asset} は作りませんでした）");
                continue;
            }
            string path = GemFolder + s.asset + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<GemDefinition>(path);
            bool created = def == null;
            if (created)
            {
                def = ScriptableObject.CreateInstance<GemDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            Undo.RecordObject(def, "初期ジェム");
            def.gemName = s.nameJa;
            if (template != null) def.icon = template.icon; // アイコンは全ジェム共通（表示はカードの見本の画像）
            def.isStarterGem = true;
            def.starterSkillName = s.skill;
            def.starterEquipOnGrant = s.equip;
            def.bonusSkill1Chance = 0f;
            def.bonusSkill2Chance = 0f;
            def.requiredSlots = 1;
            def.sellPrice = SellPrice;
            def.maxUses = 1; // 初期ジェムは無限化して配るので使われない
            def.dropArea = GemAreaId.Area_01; // 初期ジェムはエリア報酬に出ない（GemManager側で除外）
            EditorUtility.SetDirty(def);
            log.AppendLine($"・{(created ? "作成" : "更新")}：{s.asset}「{s.nameJa}」（{s.skill}{(s.equip ? "・最初から装備" : "")}・売却{SellPrice}G）");
        }
        AssetDatabase.SaveAssets();

        // ② 名前の翻訳
        var loc = AssetDatabase.LoadAssetAtPath<LocalizationTable>(LocPath);
        if (loc != null)
        {
            Undo.RecordObject(loc, "初期ジェムの名前");
            foreach (var s in Specs)
            {
                string key = $"gem.{s.asset}.name";
                var e = loc.entries.Find(x => x != null && x.key == key);
                if (e == null) { e = new LocalizationTable.Entry { key = key }; loc.entries.Add(e); }
                e.japanese = s.nameJa;
                e.english = s.nameEn;
            }
            EditorUtility.SetDirty(loc);
            AssetDatabase.SaveAssets();
            log.AppendLine("・翻訳表に初期ジェムの名前（日本語・英語）を入れました");
        }
        else log.AppendLine("⚠ " + LocPath + " が見つかりません（名前の英語は入っていません）");

        // ③ カードの見本に「STARTER」の札
        var gemUI = Object.FindFirstObjectByType<GemManagementUI>(FindObjectsInactive.Include);
        if (gemUI != null && gemUI.gameObject.scene.name == "02_Gem")
        {
            var tpl = new SerializedObject(gemUI).FindProperty("gemItemTemplate").objectReferenceValue as GameObject;
            var equipped = tpl != null ? tpl.transform.Find("FX_EquippedTag") : null;
            if (tpl == null || equipped == null) log.AppendLine("⚠ カードの見本（GemItemTemplate）か、その中の FX_EquippedTag が見つかりません（札は作りませんでした）");
            else if (tpl.transform.Find("FX_StarterTag") != null) log.AppendLine("・「STARTER」の札は既にあります（作り直しはしません）");
            else
            {
                var tag = Object.Instantiate(equipped.gameObject, tpl.transform);
                tag.name = "FX_StarterTag";
                Undo.RegisterCreatedObjectUndo(tag, "STARTERの札");
                tag.transform.SetSiblingIndex(equipped.GetSiblingIndex() + 1);
                var rt = (RectTransform)tag.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;   // カードの左下（装備中の札は右下）
                rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(12f, 10f);
                rt.sizeDelta = new Vector2(110f, rt.sizeDelta.y);
                var bg = tag.GetComponent<Image>();
                if (bg != null) bg.color = StarterTagColor;
                var check = tag.transform.Find("Check");
                if (check != null) check.gameObject.SetActive(false); // チェックの印は装備中の札だけ
                foreach (var t in tag.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    t.text = "STARTER";
                    var trt = t.rectTransform;
                    bool shadow = t.gameObject.name == "Shadow";
                    trt.offsetMin = new Vector2(shadow ? 1.5f : 0f, shadow ? -1.5f : 0f);
                    trt.offsetMax = new Vector2(shadow ? 1.5f : 0f, shadow ? -1.5f : 0f);
                    t.alignment = TextAlignmentOptions.Center;
                }
                tag.SetActive(false); // 初期ジェムのカードだけ GemScreenFX が表示する
                EditorSceneManager.MarkSceneDirty(gemUI.gameObject.scene);
                log.AppendLine("・カードの見本に「STARTER」の札（FX_StarterTag・左下・シアン）を作りました。シーンを保存してください");
            }
        }
        else log.AppendLine("⚠ 02_Gem が開いていないので「STARTER」の札は作っていません（02_Gem を開いてもう一度実行してください）");

        Debug.Log("[StarterGemSetupTool]\n" + log);
        EditorUtility.DisplayDialog("初期ジェム", log.ToString(), "OK");
    }
}
