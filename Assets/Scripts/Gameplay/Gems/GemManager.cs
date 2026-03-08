using System.Collections.Generic;
using UnityEngine;
using Game.Progress;
using Game.Skills;

namespace Game.Gems
{
    /// <summary>
    /// ジェムのロール処理・インベントリ管理を担うシングルトン。DontDestroyOnLoad。
    /// </summary>
    public class GemManager : MonoBehaviour
    {
        public static GemManager Instance { get; private set; }

        public const int MaxInventory = 30;
        private const string SkillResourcesPath = "GameData/Skills";
        private const string GemResourcesPath   = "GameData/Gems";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance == null)
            {
                var go = new GameObject("GemManager");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<GemManager>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ================== Gem Roll ==================

        /// <summary>
        /// 指定エリアのジェムをロールしてインベントリへ追加する。
        /// インベントリが上限(30個)に達している場合は false を返す（売却UIはPhase9で実装）。
        /// </summary>
        public bool TryAddGemForArea(string areaId, out GemInstance result)
        {
            result = null;
            if (ProgressManager.Instance == null) return false;

            var gemDef = FindGemDefinitionForArea(areaId);
            if (gemDef == null)
            {
                Debug.LogWarning($"[GemManager] No GemDefinition for area: {areaId}");
                return false;
            }

            var inventory = ProgressManager.Instance.Data.gemInventory;
            if (inventory.Count >= MaxInventory)
            {
                Debug.Log($"[GemManager] Inventory full ({MaxInventory}). Cannot add gem.");
                return false;
            }

            result = RollGem(gemDef);
            inventory.Add(result);
            ProgressManager.Instance.Save();

            Debug.Log($"[GemManager] Gem added: {gemDef.gemName} | bonus1={result.bonusSkill1Name} | bonus2={result.bonusSkill2Name}");
            return true;
        }

        /// <summary>
        /// Resources/GameData/Gems/ から指定エリアに対応する GemDefinition を返す
        /// </summary>
        private GemDefinition FindGemDefinitionForArea(string areaId)
        {
            var allGems = Resources.LoadAll<GemDefinition>(GemResourcesPath);
            foreach (var gem in allGems)
            {
                // GemAreaId の値を "Area_01" 形式に変換して照合
                string gemAreaStr = $"Area_{(int)gem.dropArea:D2}";
                if (gemAreaStr == areaId)
                    return gem;
            }
            return null;
        }

        /// <summary>
        /// GemDefinition からランダムロールして GemInstance を生成する
        /// </summary>
        private GemInstance RollGem(GemDefinition def)
        {
            return new GemInstance
            {
                gemDefinitionName = def.name,
                bonusSkill1Name   = RollBonusSkill(def.bonusSkill1Category, def.bonusSkill1Chance),
                bonusSkill2Name   = RollBonusSkill(def.bonusSkill2Category, def.bonusSkill2Chance),
            };
        }

        /// <summary>
        /// カテゴリフラグと確率からボーナススキルをロールする。
        /// 外れた場合は空文字を返す。
        /// </summary>
        private string RollBonusSkill(SkillCategoryFlags categoryFlags, float chance)
        {
            if (categoryFlags == SkillCategoryFlags.None || chance <= 0f) return "";

            // 確率判定（0〜100%）
            if (Random.value * 100f > chance) return "";

            // 対象カテゴリのスキルを収集
            var allSkills = Resources.LoadAll<SkillDefinition>(SkillResourcesPath);
            var candidates = new List<SkillDefinition>();
            foreach (var skill in allSkills)
            {
                if (MatchesCategory(skill.category, categoryFlags))
                    candidates.Add(skill);
            }

            if (candidates.Count == 0) return "";
            return candidates[Random.Range(0, candidates.Count)].name;
        }

        private static bool MatchesCategory(SkillCategory category, SkillCategoryFlags flags)
        {
            switch (category)
            {
                case SkillCategory.CategoryA: return (flags & SkillCategoryFlags.CategoryA) != 0;
                case SkillCategory.CategoryB: return (flags & SkillCategoryFlags.CategoryB) != 0;
                case SkillCategory.CategoryC: return (flags & SkillCategoryFlags.CategoryC) != 0;
                default: return false;
            }
        }

        // ================== Equip / Apply ==================

        /// <summary>
        /// 装備中のジェムのスキルを SkillManager に適用する（ゲーム開始時に呼ぶ）
        /// スロット合計が slotLevel を超えるジェムはスキップする
        /// </summary>
        public void ApplyEquippedGems()
        {
            if (ProgressManager.Instance == null) return;
            var skillManager = SkillManager.Instance;
            if (skillManager == null)
            {
                Debug.LogWarning("[GemManager] SkillManager not found. Cannot apply gem effects.");
                return;
            }

            var data = ProgressManager.Instance.Data;
            int usedSlots = 0;

            foreach (int idx in data.equippedGemIndices)
            {
                if (idx < 0 || idx >= data.gemInventory.Count) continue;

                var gemInstance = data.gemInventory[idx];
                var gemDef = LoadGemDefinition(gemInstance);
                if (gemDef == null) continue;

                // スロット上限チェック
                if (usedSlots + gemDef.requiredSlots > data.slotLevel)
                {
                    Debug.LogWarning($"[GemManager] {gemDef.gemName} skipped: slots full ({usedSlots}/{data.slotLevel})");
                    continue;
                }
                usedSlots += gemDef.requiredSlots;

                // 基本スキルを適用
                if (gemDef.baseSkill != null)
                {
                    skillManager.AddSkill(gemDef.baseSkill, SkillSource.Gem);
                    Debug.Log($"[GemManager] Base skill: {gemDef.baseSkill.skillName} ({gemDef.gemName})");
                }

                // ボーナススキル1を適用
                var bonus1 = LoadBonusSkill1(gemInstance);
                if (bonus1 != null)
                {
                    skillManager.AddSkill(bonus1, SkillSource.Gem);
                    Debug.Log($"[GemManager] Bonus skill 1: {bonus1.skillName} ({gemDef.gemName})");
                }

                // ボーナススキル2を適用
                var bonus2 = LoadBonusSkill2(gemInstance);
                if (bonus2 != null)
                {
                    skillManager.AddSkill(bonus2, SkillSource.Gem);
                    Debug.Log($"[GemManager] Bonus skill 2: {bonus2.skillName} ({gemDef.gemName})");
                }
            }

            Debug.Log($"[GemManager] ApplyEquippedGems done. Used slots: {usedSlots}/{data.slotLevel}");
        }

        /// <summary>
        /// ドリンクセッションのブーストを SkillManager に適用する（ゲーム開始時に呼ぶ）
        /// </summary>
        public void ApplyDrinkBoosts()
        {
            var skillManager = SkillManager.Instance;
            if (skillManager == null)
            {
                Debug.LogWarning("[GemManager] SkillManager not found. Cannot apply drink boosts.");
                return;
            }

            if (!Game.Shop.DrinkSession.HasAnyBoost)
            {
                Debug.Log("[GemManager] ApplyDrinkBoosts: no active boosts.");
                return;
            }

            foreach (var kvp in Game.Shop.DrinkSession.ActiveBoosts)
            {
                var skill = Resources.Load<SkillDefinition>($"{SkillResourcesPath}/{kvp.Key}");
                if (skill == null)
                {
                    Debug.LogWarning($"[GemManager] DrinkBoost: skill not found: {kvp.Key}");
                    continue;
                }

                for (int i = 0; i < kvp.Value; i++)
                    skillManager.AddSkill(skill, SkillSource.Shop);

                Debug.Log($"[GemManager] DrinkBoost applied: {skill.skillName} +{kvp.Value}");
            }
        }

        // ================== Loaders ==================

        /// <summary>
        /// GemInstance から GemDefinition を Resources.Load で取得する
        /// </summary>
        public GemDefinition LoadGemDefinition(GemInstance instance)
        {
            if (instance == null || string.IsNullOrEmpty(instance.gemDefinitionName)) return null;
            return Resources.Load<GemDefinition>($"{GemResourcesPath}/{instance.gemDefinitionName}");
        }

        /// <summary>
        /// GemInstance の基本スキル SkillDefinition を返す
        /// </summary>
        public SkillDefinition LoadBaseSkill(GemInstance instance)
        {
            return LoadGemDefinition(instance)?.baseSkill;
        }

        /// <summary>
        /// GemInstance のボーナススキル1 SkillDefinition を返す（nullあり）
        /// </summary>
        public SkillDefinition LoadBonusSkill1(GemInstance instance)
        {
            if (instance == null || string.IsNullOrEmpty(instance.bonusSkill1Name)) return null;
            return Resources.Load<SkillDefinition>($"{SkillResourcesPath}/{instance.bonusSkill1Name}");
        }

        /// <summary>
        /// GemInstance のボーナススキル2 SkillDefinition を返す（nullあり）
        /// </summary>
        public SkillDefinition LoadBonusSkill2(GemInstance instance)
        {
            if (instance == null || string.IsNullOrEmpty(instance.bonusSkill2Name)) return null;
            return Resources.Load<SkillDefinition>($"{SkillResourcesPath}/{instance.bonusSkill2Name}");
        }
    }
}
