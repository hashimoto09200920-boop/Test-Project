using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 英語表示の時に英語になる文字（と英語固定の文字）に、LanguageFontSwitch（英語用フォントへの切り替え）を一括で付ける。
/// 対象は下の表（Hierarchyのパスの末尾で指定）。01〜05の各シーンを開いて付けて保存する。何度実行しても同じ（設定を上書き）。
/// 付けた後はInspectorで Kind（Heading＝Bungee／Body＝Rajdhani）と Always Use English Font を変更できる
/// （手で変えた後に再実行すると表の設定に戻る）。
/// </summary>
public static class LanguageFontSwitchSetupTool
{
    private static readonly string[] Scenes =
    {
        "Assets/Scenes/01_Title.unity",
        "Assets/Scenes/02_Gem.unity",
        "Assets/Scenes/03_AreaSelect.unity",
        "Assets/Scenes/04_Drink.unity",
        "Assets/Scenes/05_Game.unity",
    };

    private const LanguageFontSwitch.Kind H = LanguageFontSwitch.Kind.Heading;
    private const LanguageFontSwitch.Kind B = LanguageFontSwitch.Kind.Body;

    // 英語の時だけ文字を小さくする所（パスの末尾 → 倍率）。英語の方が横に長く枠に重なる所
    private static readonly System.Collections.Generic.Dictionary<string, float> EnglishSizeScales = new System.Collections.Generic.Dictionary<string, float>
    {
        { "FX_EffectBadge/Text", 0.8f },
        { "FX_EffectBadge/Shadow", 0.8f },
        { "SelectionPanel/SkillCard1/SkillNameText", 0.93f },
        { "SelectionPanel/SkillCard2/SkillNameText", 0.93f },
        { "SelectionPanel/SkillCard3/SkillNameText", 0.93f },
    };

    // 日本語の見出し用フォント（Dela Gothic One）の時だけ文字を小さくする所（パスの末尾 → 倍率）
    private static readonly System.Collections.Generic.Dictionary<string, float> JapaneseSizeScales = new System.Collections.Generic.Dictionary<string, float>
    {
        { "SelectionPanel/SkillCard1/SkillNameText", 0.85f },
        { "SelectionPanel/SkillCard2/SkillNameText", 0.85f },
        { "SelectionPanel/SkillCard3/SkillNameText", 0.85f },
    };

    // （パスの末尾, 種類, 常に英語用フォント）
    private static readonly (string path, LanguageFontSwitch.Kind kind, bool always)[] Rules =
    {
        // ===== 01_Title =====
        ("LanguagePanel/TitleText", H, false),
        // ===== ジェム（02_Gem／03_AreaSelect） =====
        ("LowUsesWarningText/Text", B, false),
        ("SellConfirmDialog/ConfirmText", B, false),
        ("GemItemTemplate/TextContainer/NameRow/NameText", H, false),
        ("SkillIconsContainer/SkillRow_Base/SkillName", H, false),
        ("SkillIconsContainer/SkillRow_Bonus1/SkillName", H, false),
        ("SkillIconsContainer/SkillRow_Bonus2/SkillName", H, false),
        ("FX_EquippedTag/Text", H, false),
        ("FX_EquippedTag/Shadow", H, false),
        ("FX_SlotPips/Label", H, false),
        ("FX_InventoryBadge/FX_InventoryText", H, false),
        ("GemDepletionNoticePanel/Box/MessageText", B, false),
        ("GemPreLaunchConfirmPanel/Box/MessageText", B, false),
        ("StaminaHUD/StaminaCountdownText", H, false),
        // ===== ドリンクのカード（02／03／04） =====
        ("DrinkCardTemplate/NamePriceRow/DrinkNameText", H, false),
        ("DrinkCardTemplate/NamePriceRow/FX_NameShadow", H, false),
        ("DrinkCardTemplate/FlavorTextContainer/FlavorText", B, false),
        ("DrinkCardTemplate/SkillsContainer/SkillRow_1/SkillName", H, false),
        ("DrinkCardTemplate/SkillsContainer/SkillRow_2/SkillName", H, false),
        ("DrinkCardTemplate/SkillsContainer/SkillRow_3/SkillName", H, false),
        ("PurchasedOverlay/PurchasedLabel", H, false),
        ("FX_EffectBadge/Text", H, false),
        ("FX_EffectBadge/Shadow", H, false),
        ("FX_NoGold/Text", H, false),
        ("FX_NoGold/Shadow", H, false),
        // ===== 05_Game：スキル選択・ツールチップ =====
        ("SelectionPanel/SkillCard1/SkillNameText", H, false),
        ("SelectionPanel/SkillCard2/SkillNameText", H, false),
        ("SelectionPanel/SkillCard3/SkillNameText", H, false),
        ("SelectionPanel/SkillCard1/DescriptionText", B, false),
        ("SelectionPanel/SkillCard2/DescriptionText", B, false),
        ("SelectionPanel/SkillCard3/DescriptionText", B, false),
        ("SkillSelectionUI/SelectionPanel/TitleText", H, false),
        ("SkillTooltip/SkillName", H, false),
        ("SkillTooltip/Description", B, false),
        ("SkillTooltip/Category", B, false),
        ("SkillTooltip/Level", B, false),
        ("SkillTooltip/CurrentEffect", B, false),
        ("SkillTooltip/NextEffect", B, false),
        // ===== 05_Game：ジェム報酬 =====
        ("CardRow/GemCard0/GemNameText", H, false),
        ("CardRow/GemCard1/GemNameText", H, false),
        ("CardRow/GemCard2/GemNameText", H, false),
        ("CardRow/SelectedCard/GemNameText", H, false),
        ("CardRow/Unselected0/GemNameText", H, false),
        ("CardRow/Unselected1/GemNameText", H, false),
        ("Phase2Panel/TitleText", H, false),
        ("Phase2Panel/CloseButton/Text", H, false),
        ("ConfirmDialog/ConfirmBg/ConfirmText", B, false),
        ("FullMessagePanel/FullBg/MessageText", B, false),
        // ===== 05_Game：中断メニュー・ヘルプ =====
        ("PauseOverlayCanvas/ConfirmPanel/ConfirmText", B, false),
        ("HelpPager/Content/Title", H, false),
        ("HelpPager/Content/Body", B, false),
        // ===== 05_Game：チュートリアル =====
        ("TutorialCanvas/Panel/Card/TitleText", H, false),
        ("TutorialCanvas/Panel/Card/BodyText", B, false),
        ("PracticeHintRoot/PracticeHintText", B, false),
        ("Guide_MissionTab/Guide_MissionHeader", H, false),
        ("Guide_MissionPanel/Guide_MissionProgress", H, false),
        ("Guide_MissionRow0/Text", B, false),
        ("Guide_MissionRow1/Text", B, false),
        ("Guide_MissionRow2/Text", B, false),
        ("Guide_MissionRow3/Text", B, false),
        ("Guide_MissionPanel/Guide_Footer", B, false),
        ("Guide_BulletGuide/Guide_ZoneLabel", H, false),
        ("Guide_Front/Guide_NowLabel", H, false),
        ("Guide_SuccessPop/Text", H, false),
        // ===== 英語固定の文字（日本語表示でもBungeeにそろえる） =====
        ("ResultPanel/ContentBg/TitleLabel", H, true),
        ("ResultPanel/ContentBg/HeaderLeft", H, true),
        ("ResultPanel/ContentBg/HeaderCenter", H, true),
        ("ResultPanel/ContentBg/HeaderRight", H, true),
        ("ResultPanel/ContentBg/NormalLabel", H, true),
        ("ResultPanel/ContentBg/JustLabel", H, true),
        ("ResultPanel/ContentBg/AccuracyLabel", H, true),
        ("ResultPanel/ContentBg/KillsLabel", H, true),
        ("ResultPanel/ContentBg/ReceivedLabel", H, true),
        ("ResultPanel/ContentBg/BlocksLabel", H, true),
        ("ResultPanel/ContentBg/DownsLabel", H, true),
        ("ResultPanel/ContentBg/OverheatLabel", H, true),
        ("ResultPanel/ContentBg/GoldLabel", H, true),
        ("FX_NextTag/Text", H, true),
        ("ThankYouScreen/Title/TitleText", H, true),
    };

    [MenuItem("Tools/英語のフォント/英語の文字に切り替えの部品を付ける（01〜05）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new StringBuilder();
        int total = 0;
        foreach (var path in Scenes)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            int n = 0;
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        string p = PathOf(t.transform);
                        foreach (var r in Rules)
                        {
                            if (!p.EndsWith(r.path)) continue;
                            var sw = t.GetComponent<LanguageFontSwitch>();
                            if (sw == null) sw = t.gameObject.AddComponent<LanguageFontSwitch>();
                            sw.kind = r.kind;
                            sw.alwaysUseEnglishFont = r.always;
                            if (EnglishSizeScales.TryGetValue(r.path, out float sc)) sw.englishSizeScale = sc;
                            if (JapaneseSizeScales.TryGetValue(r.path, out float jsc)) sw.japaneseSizeScale = jsc;
                            EditorUtility.SetDirty(sw);
                            n++;
                            break;
                        }
                    }
                }
                if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            log.Append($"{System.IO.Path.GetFileNameWithoutExtension(path)}：{n}個　");
            total += n;
        }
        int added = AddTranslations();
        Debug.Log($"[LanguageFontSwitchSetupTool] 英語の文字に切り替えの部品を付けて保存しました（合計{total}個）　{log}／翻訳を{added}件追加");
    }

    // 翻訳漏れだった文言（キー, 日本語, 英語）。すでに同じキーがある時は上書きしない
    private static readonly (string key, string ja, string en)[] Translations =
    {
        ("tooltip.category.a", "カテゴリA", "Category A"),
        ("tooltip.category.b", "カテゴリB", "Category B"),
        ("tooltip.category.other", "その他", "Other"),
        ("tooltip.notAcquired", "未取得", "Not acquired"),
        ("tooltip.level.max", "取得回数: {0}/{1} (MAX)", "Taken: {0}/{1} (MAX)"),
        ("tooltip.level", "取得回数: {0}/{1}", "Taken: {0}/{1}"),
        ("tooltip.level.unlimited", "取得回数: {0} (無制限)", "Taken: {0} (Unlimited)"),
        ("tooltip.current", "現在の効果: {0}", "Current: {0}"),
        ("tooltip.maxReached", "最大レベル到達", "Max level reached"),
        ("tooltip.next", "次レベル: {0}", "Next level: {0}"),
        ("tooltip.unit.lines", "+{0}本", "+{0} lines"),
        ("tooltip.unit.sec", "+{0}秒", "+{0}s"),
        ("tooltip.unit.times", "{0}回", "{0} times"),
        ("tooltip.unlimited", "無制限", "Unlimited"),
        ("tooltip.unit.activeFor", "{0}秒間有効", "Active for {0}s"),
        ("skill.select.title", "スキル選択 ({0} 回残り)", "Choose a Skill ({0} left)"),
        ("skill.select.noSkills", "全てのスキルが上限に達しています", "All skills are maxed out"),
        ("gem.reward.title", "ジェムを取得しました！", "Gem Obtained!"),
        ("common.close", "閉じる", "Close"),
        ("gem.depletedNotice", "以下のジェムは使用回数が0になり、消滅しました。", "These gems ran out of uses and vanished."),
        ("reset.done", "データを初期化しました", "Data has been reset"),
    };

    private static int AddTranslations()
    {
        var table = Resources.Load<Game.Localization.LocalizationTable>("GameData/LocalizationTable");
        if (table == null) { Debug.LogWarning("[LanguageFontSwitchSetupTool] LocalizationTable が見つかりません（翻訳は追加していません）"); return 0; }
        var so = new SerializedObject(table);
        var entries = so.FindProperty("entries");
        var existing = new HashSet<string>();
        for (int i = 0; i < entries.arraySize; i++) existing.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue);
        int added = 0;
        foreach (var (key, ja, en) in Translations)
        {
            if (existing.Contains(key)) continue;
            entries.arraySize++;
            var el = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            el.FindPropertyRelative("key").stringValue = key;
            el.FindPropertyRelative("japanese").stringValue = ja;
            el.FindPropertyRelative("english").stringValue = en;
            added++;
        }
        if (added > 0) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(table); AssetDatabase.SaveAssets(); }
        return added;
    }

    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var c = t; c != null; c = c.parent) parts.Add(c.name.Trim());
        parts.Reverse();
        return string.Join("/", parts);
    }
}
