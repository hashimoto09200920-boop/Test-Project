using Game.Localization;
using TMPro;
using UnityEngine;

/// <summary>
/// Result画面の「MISSION CLEAR」枠（ResultPanelの子）。そのプレイで達成したミッションがある時だけ表示する。
/// 最大 rows.Length 件（3件）まで出し、それより多い時は「ほか○件」。表示はResultPanelが出た時（OnEnable）に作る。
/// 作成は Tools > ミッション > 9。位置・大きさ・色・文言はInspectorで調整。
/// </summary>
public class MissionResultBoxUI : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public GameObject root;
        public TMP_Text nameText;
        public TMP_Text rewardText;
    }

    [Header("参照（メニュー9で自動設定）")]
    [Tooltip("枠全体（達成が0件なら非表示）")]
    [SerializeField] private GameObject box;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Row[] rows = new Row[3];
    [SerializeField] private TMP_Text moreText;

    [Header("文字")]
    [SerializeField] private string titleLabel = "MISSION CLEAR";
    [SerializeField] private string passiveFormatJa = "パッシブ：{0}";
    [SerializeField] private string passiveFormatEn = "PASSIVE: {0}";
    [SerializeField] private string goldFormatJa = "{0} ゴールド";
    [SerializeField] private string goldFormatEn = "{0} GOLD";
    [SerializeField] private string rainbowDropFormatJa = "虹の雫 ×{0}";
    [SerializeField] private string rainbowDropFormatEn = "PRISM DROP ×{0}";
    [SerializeField] private string foodTicketFormatJa = "お食事券 ×{0}";
    [SerializeField] private string foodTicketFormatEn = "FOOD TICKET ×{0}";
    [SerializeField] private string noticeTextJa = "次のエリア解放・スロット+1";
    [SerializeField] private string noticeTextEn = "Next area / Slot +1";
    [Tooltip("4件以上の時の残りの件数（{0}=件数）")]
    [SerializeField] private string moreFormatJa = "ほか{0}件";
    [SerializeField] private string moreFormatEn = "+{0} more";

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        var list = MissionProgressManager.NewlyAchievedThisRun;
        var catalog = MissionProgressManager.Instance != null ? MissionProgressManager.Instance.Catalog : null;
        int total = (list != null && catalog != null) ? list.Count : 0;
        if (box != null) box.SetActive(total > 0);
        if (total == 0) return;

        bool en = LocalizationManager.CurrentLanguageStatic == GameLanguage.English;
        if (titleText != null) titleText.text = titleLabel;

        int shown = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null || row.root == null) continue;
            MissionDefinition def = i < total ? catalog.Find(list[i]) : null;
            row.root.SetActive(def != null);
            if (def == null) continue;
            shown++;
            if (row.nameText != null) row.nameText.text = def.GetDisplayName(en);
            if (row.rewardText != null) row.rewardText.text = RewardText(def, en);
        }

        int rest = total - shown;
        if (moreText != null)
        {
            moreText.gameObject.SetActive(rest > 0);
            if (rest > 0) moreText.text = string.Format(en ? moreFormatEn : moreFormatJa, rest);
        }
    }

    private string RewardText(MissionDefinition def, bool en)
    {
        if (def.rewardType == MissionRewardType.Gold)
            return string.Format(en ? goldFormatEn : goldFormatJa, def.goldAmount.ToString("N0"));
        if (def.rewardType == MissionRewardType.RainbowDrop)
            return string.Format(en ? rainbowDropFormatEn : rainbowDropFormatJa, def.rainbowDropAmount);
        if (def.rewardType == MissionRewardType.FoodTicket)
            return string.Format(en ? foodTicketFormatEn : foodTicketFormatJa, def.foodTicketAmount);
        if (def.rewardType == MissionRewardType.Notice)
            return en ? noticeTextEn : noticeTextJa; // 細かい内容（エリア名）は達成の通知で出す
        string label = en && !string.IsNullOrEmpty(def.rewardLabelEn) ? def.rewardLabelEn : def.rewardLabel;
        return string.Format(en ? passiveFormatEn : passiveFormatJa, label);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 【Play中に使う確認用】文字が長いミッション5件（M25・M29・M19・M30・M26）を「今回達成した」ことにして、
    /// 実際のResult画面（ResultScreenUI.Show）を出す。字体の切り替え・フェード・枠の作りが実機と同じ処理で出るので、
    /// エリアをクリアしなくても実機に近い見た目で文字の重なりを確かめられる。
    /// ・負けた時の表示（isVictory=false）で出すので、ランクの記録（ベストランク・ランクSのミッション）は書き換えない
    /// ・ミッションの進捗（数）も増やさない。「今回達成した」一覧はこのPlayの間だけ（次に05_Gameに入ると消える）
    /// </summary>
    [ContextMenu("Play中：長いミッション5件でResult画面を出す（実機と同じ字体で確認）")]
    public void PlayModeShowResultWithLongest()
    {
        Debug.Log($"[MissionResultBoxUI] 確認用メニューが選ばれました（Play中={Application.isPlaying}）");
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.DisplayDialog("ミッション", "Play中に使ってください。05_Game を Play してから、もう一度このメニューを選んでください。", "OK");
            return;
        }
        var result = FindFirstObjectByType<ResultScreenUI>(FindObjectsInactive.Include);
        if (result == null) { Debug.LogWarning("[MissionResultBoxUI] ResultScreenUI が見つかりません"); return; }
        MissionProgressManager.NewlyAchievedThisRun.Clear();
        MissionProgressManager.NewlyAchievedThisRun.AddRange(new[] { "M25", "M29", "M19", "M30", "M26" });
        result.Show(null, false);
        Debug.Log("[MissionResultBoxUI] 確認用：長いミッション5件（M25・M29・M19・M30・M26）でResult画面を出しました（ランク・進捗は記録しません）");
    }

    // ---- 【Play中に使う確認用】全ミッションを3件ずつ順番に出す・日本語と英語を切り替える ----
    private static int s_checkPage = -1;

    /// <summary>全ミッション（MissionCatalogの並び）を3件ずつ「今回達成した」ことにしてResult画面に出す。delta=+1で次、-1で前</summary>
    public void PlayModeShowPage(int delta)
    {
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.DisplayDialog("ミッション", "Play中に使ってください。05_Game を Play してから、もう一度このメニューを選んでください。", "OK");
            return;
        }
        var catalog = MissionProgressManager.Instance != null ? MissionProgressManager.Instance.Catalog : null;
        var result = FindFirstObjectByType<ResultScreenUI>(FindObjectsInactive.Include);
        if (catalog == null || result == null) { Debug.LogWarning("[MissionResultBoxUI] MissionCatalog か ResultScreenUI が見つかりません"); return; }
        int per = Mathf.Max(1, rows.Length);
        int pages = Mathf.Max(1, (catalog.missions.Count + per - 1) / per);
        s_checkPage = ((s_checkPage + delta) % pages + pages) % pages;
        MissionProgressManager.NewlyAchievedThisRun.Clear();
        var ids = new System.Collections.Generic.List<string>();
        for (int i = s_checkPage * per; i < Mathf.Min(catalog.missions.Count, (s_checkPage + 1) * per); i++)
            if (catalog.missions[i] != null) ids.Add(catalog.missions[i].id);
        MissionProgressManager.NewlyAchievedThisRun.AddRange(ids);
        // Result画面がまだ出ていなければ出す（負けた時の表示＝ランクは記録しない）。出ていれば中身だけ入れ替える
        if (box == null || !box.activeInHierarchy) result.Show(null, false);
        else Refresh();
        Debug.Log($"[MissionResultBoxUI] 確認用 {s_checkPage + 1}/{pages}ページ：{string.Join("・", ids)}（{(LocalizationManager.CurrentLanguageStatic == GameLanguage.English ? "英語" : "日本語")}）");
    }

    /// <summary>日本語と英語を切り替えて、今出しているミッションを出し直す（PCの言語設定も切り替わる）</summary>
    public void PlayModeToggleLanguage()
    {
        if (!Application.isPlaying || LocalizationManager.Instance == null)
        {
            UnityEditor.EditorUtility.DisplayDialog("ミッション", "Play中に使ってください。05_Game を Play してから、もう一度このメニューを選んでください。", "OK");
            return;
        }
        var next = LocalizationManager.Instance.CurrentLanguage == GameLanguage.English ? GameLanguage.Japanese : GameLanguage.English;
        LocalizationManager.Instance.SetLanguage(next); // 文字の字体（LanguageFontSwitch）はこれで切り替わる
        Refresh();
        Debug.Log($"[MissionResultBoxUI] 確認用：言語を{(next == GameLanguage.English ? "英語" : "日本語")}に切り替えました");
    }

    [ContextMenu("一番長いミッション3件で表示（Play前の確認用・文字の重なりチェック）")]
    private void PreviewLongest()
    {
        // 名前・報酬が一番長いミッション（M25・M29・M19）を、実際の一覧（MissionCatalog）から出す。プレイせずに文字の重なりを確認するため
        var catalog = UnityEngine.Resources.Load<MissionCatalog>(MissionCatalog.ResourcesPath);
        if (catalog == null) { Debug.LogWarning("[MissionResultBoxUI] MissionCatalog が見つかりません"); return; }
        string[] ids = { "M25", "M29", "M19" };
        ShowParentsForPreview(); // Result画面（ResultPanel）はPlay前は非表示なので、確認の間だけ表示する
        if (box != null) box.SetActive(true);
        if (titleText != null) titleText.text = titleLabel;
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null || rows[i].root == null) continue;
            var def = i < ids.Length ? catalog.Find(ids[i]) : null;
            rows[i].root.SetActive(def != null);
            if (def == null) continue;
            if (rows[i].nameText != null) rows[i].nameText.text = def.displayName;
            if (rows[i].rewardText != null) rows[i].rewardText.text = RewardText(def, false);
        }
        if (moreText != null) { moreText.gameObject.SetActive(true); moreText.text = string.Format(moreFormatJa, 2); }
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }

    // ---- 確認用：非表示の親（ResultPanel等）を一時的に表示する。シーンを保存する直前に必ず元の非表示に戻す ----
    // 一時的に表示した親を覚えておく（SessionState＝スクリプトの再コンパイルをまたいでも消えない）
    private const string PreviewKey = "MissionResultBoxUI.PreviewActivated";

    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterSaveHook()
    {
        // シーンを保存する直前に、確認のため表示した親を必ず非表示に戻す（Result画面を表示したまま保存しないため）
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (scene, path) => HideParentsAfterPreview();
    }

    private void ShowParentsForPreview()
    {
        var ids = new System.Collections.Generic.List<string>(UnityEditor.SessionState.GetString(PreviewKey, "").Split('|'));
        string topName = null;
        for (var t = transform; t != null; t = t.parent)
        {
            if (t.gameObject.activeSelf) continue;
            t.gameObject.SetActive(true);
            ids.Add(UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString());
            topName = t.name;
        }
        UnityEditor.SessionState.SetString(PreviewKey, string.Join("|", ids));
        if (topName != null)
            Debug.Log("[MissionResultBoxUI] 確認のため " + topName + " を一時的に表示しました。シーンを保存する時に自動で非表示に戻します（右クリック「確認表示を終わる（Result画面を非表示に戻す）」でも戻せます）");
    }

    [ContextMenu("確認表示を終わる（Result画面を非表示に戻す）")]
    private void EndPreview() => HideParentsAfterPreview();

    private static void HideParentsAfterPreview()
    {
        string saved = UnityEditor.SessionState.GetString(PreviewKey, "");
        if (string.IsNullOrEmpty(saved)) return;
        foreach (var s in saved.Split('|'))
        {
            if (string.IsNullOrEmpty(s) || !UnityEditor.GlobalObjectId.TryParse(s, out var gid)) continue;
            if (UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) is GameObject go && go != null) go.SetActive(false);
        }
        UnityEditor.SessionState.EraseString(PreviewKey);
    }

    [ContextMenu("3件＋ほか2件の見た目で表示（Play前の確認用）")]
    private void PreviewFill()
    {
        if (box != null) box.SetActive(true);
        if (titleText != null) titleText.text = titleLabel;
        string[] names = { "ソウルを100体救出する", "白線で300回反射する", "Area3でランクSを取る" };
        string[] rewards = { string.Format(passiveFormatJa, "ソウルリバイブ"), string.Format(passiveFormatJa, "マルチライン+1"), string.Format(goldFormatJa, "500") };
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null || rows[i].root == null) continue;
            rows[i].root.SetActive(true);
            if (rows[i].nameText != null) rows[i].nameText.text = names[i % names.Length];
            if (rows[i].rewardText != null) rows[i].rewardText.text = rewards[i % rewards.Length];
        }
        if (moreText != null) { moreText.gameObject.SetActive(true); moreText.text = string.Format(moreFormatJa, 2); }
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }
#endif
}
