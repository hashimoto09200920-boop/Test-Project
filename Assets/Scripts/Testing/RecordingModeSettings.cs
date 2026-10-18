using System.Collections.Generic;
using Game.Skills;
using UnityEngine;

namespace Game.Testing
{
    /// <summary>
    /// 撮影モード（宣伝用の動画・スクリーンショットの撮影用）の設定。Assets/Resources/RecordingModeSettings.asset に置く。
    /// ONの間は、ゲーム開始のたびに、装備ジェム・購入ドリンクの代わりに、ランダムなスキルへレベルを割り振る
    /// （カテゴリーごとの合計はInspectorの値。各スキルの最大レベルは超えない）。
    /// 入手元（HUDの色）は、カード（水色）＝Card Levels のレベル数、ドリンク（紫）＝各カテゴリーの合計×Drink Ratio、残りをジェム（オレンジ）にする。
    /// ゲーム中のスキルカード3択は今まで通り出る。エディタのPlayだけで有効（ビルドでは常に無効）。
    /// 撮影モード中はジェムの使用回数を消費しない。05_GameのSkillTestTool（Apply On Start）も動かさない。
    /// メニュー「Tools/撮影モード/撮影モードの設定を開く（無ければ作成）」で作成・選択する。
    /// </summary>
    [CreateAssetMenu(fileName = "RecordingModeSettings", menuName = "Game/Testing/Recording Mode Settings", order = 3)]
    public class RecordingModeSettings : ScriptableObject
    {
        private const string ResourcePath = "RecordingModeSettings";

        [Tooltip("ONで撮影モード（エディタのPlayだけ有効）")]
        public bool enabledRecordingMode = false;

        [Header("カテゴリーごとのスキルレベルの合計")]
        [Min(0)] public int categoryATotal = 26;
        [Min(0)] public int categoryBTotal = 14;
        [Min(0)] public int categoryCTotal = 3;

        [Header("入手元の配分（HUDの色）")]
        [Tooltip("スキルカード（水色）にするレベル数（全カテゴリー合計。実際のプレイのステージ1・2で1枚ずつに合わせて2）")]
        [Min(0)] public int cardLevels = 2;
        [Tooltip("各カテゴリーの合計のうち、ドリンク（紫）にする割合。残りはジェム（オレンジ）")]
        [Range(0f, 1f)] public float drinkRatio = 0.3f;

        /// <summary>撮影モードが有効か（エディタのPlay中だけ）</summary>
        public static bool IsActive
        {
            get
            {
#if UNITY_EDITOR
                var s = Resources.Load<RecordingModeSettings>(ResourcePath);
                return s != null && s.enabledRecordingMode;
#else
                return false;
#endif
            }
        }

        /// <summary>撮影モードのスキルレベルをランダムに割り振って SkillManager に加える（GameManager.Start から呼ぶ）</summary>
        public static void ApplyRandomSkills(SkillManager manager)
        {
            var s = Resources.Load<RecordingModeSettings>(ResourcePath);
            if (s == null || manager == null) return;
            var all = Resources.LoadAll<SkillDefinition>("GameData/Skills");

            // 1レベルぶん＝(スキル, カテゴリー) を並べる
            var units = new List<(SkillDefinition skill, SkillCategory cat)>();
            int[] totals = { s.categoryATotal, s.categoryBTotal, s.categoryCTotal };
            SkillCategory[] cats = { SkillCategory.CategoryA, SkillCategory.CategoryB, SkillCategory.CategoryC };
            var log = new System.Text.StringBuilder("[RecordingMode] 撮影モードのスキルレベル：");
            for (int c = 0; c < cats.Length; c++)
            {
                var levels = new Dictionary<SkillDefinition, int>();
                var pool = new List<SkillDefinition>();
                foreach (var sk in all) if (sk != null && sk.category == cats[c]) pool.Add(sk);
                int placed = 0;
                while (placed < totals[c])
                {
                    var open = pool.FindAll(sk => sk.maxAcquisitionCount <= 0 || (levels.TryGetValue(sk, out int lv) ? lv : 0) < sk.maxAcquisitionCount);
                    if (open.Count == 0) { Debug.LogWarning($"[RecordingMode] {cats[c]} の合計 {totals[c]} が全スキルの最大レベルの合計を超えるため {placed} で止めました"); break; }
                    var pick = open[Random.Range(0, open.Count)];
                    levels[pick] = (levels.TryGetValue(pick, out int cur) ? cur : 0) + 1;
                    units.Add((pick, cats[c]));
                    placed++;
                }
                log.Append($" {cats[c]}={placed}");
            }

            // 入手元を決める：カードは全体からランダムにCard Levels個、ドリンクは各カテゴリーの合計×割合、残りはジェム
            var sources = new SkillSource[units.Count];
            for (int i = 0; i < sources.Length; i++) sources[i] = SkillSource.Gem;
            var order = new List<int>();
            for (int i = 0; i < units.Count; i++) order.Add(i);
            Shuffle(order);
            int cards = Mathf.Min(s.cardLevels, units.Count);
            for (int i = 0; i < cards; i++) sources[order[i]] = SkillSource.Card;
            for (int c = 0; c < cats.Length; c++)
            {
                int want = Mathf.RoundToInt(totals[c] * s.drinkRatio);
                for (int i = cards; i < order.Count && want > 0; i++)
                {
                    int u = order[i];
                    if (units[u].cat == cats[c] && sources[u] == SkillSource.Gem) { sources[u] = SkillSource.Shop; want--; }
                }
            }

            // HUDの色の並び（ジェム→ドリンク→カード）に合わせて、ジェム・ドリンク・カードの順に加える
            foreach (var src in new[] { SkillSource.Gem, SkillSource.Shop, SkillSource.Card })
                for (int i = 0; i < units.Count; i++)
                    if (sources[i] == src) manager.AddSkill(units[i].skill, src);

            int g = 0, d = 0, k = 0;
            foreach (var src in sources) { if (src == SkillSource.Gem) g++; else if (src == SkillSource.Shop) d++; else k++; }
            log.Append($"（ジェム{g}・ドリンク{d}・カード{k}）");
            Debug.Log(log.ToString());
        }

        private static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
