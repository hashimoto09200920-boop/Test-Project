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
                baseSkillName     = RollSkillFromCategory(def.baseSkillCategory),
                bonusSkill1Name   = RollBonusSkill(def, def.bonusSkill1Chance),
                bonusSkill2Name   = RollBonusSkill(def, def.bonusSkill2Chance),
                remainingUses     = def.maxUses,
            };
        }

        /// <summary>
        /// 指定カテゴリのスキルからランダムに1つ選んでアセット名を返す（付与確率のチェックは行わない、常時実行用）
        /// </summary>
        private string RollSkillFromCategory(SkillCategory category)
        {
            var allSkills = Resources.LoadAll<SkillDefinition>(SkillResourcesPath);
            var candidates = new List<SkillDefinition>();
            foreach (var skill in allSkills)
            {
                if (skill.category == category)
                    candidates.Add(skill);
            }
            if (candidates.Count == 0) return "";
            return candidates[Random.Range(0, candidates.Count)].name;
        }

        /// <summary>
        /// 確率でボーナススキル付与の成否を判定し、成功した場合はジェム共通の
        /// カテゴリ別重み（categoryA/B/CWeight）でカテゴリを1つ抽選してから、
        /// そのカテゴリ内のスキルをランダムに1つ選ぶ。外れた場合は空文字を返す。
        /// </summary>
        private string RollBonusSkill(GemDefinition def, float chance)
        {
            if (chance <= 0f) return "";

            // 確率判定（0〜100%）
            if (Random.value * 100f > chance) return "";

            SkillCategoryFlags chosenCategory = PickWeightedCategory(def);
            if (chosenCategory == SkillCategoryFlags.None) return "";

            // 対象カテゴリのスキルを収集
            var allSkills = Resources.LoadAll<SkillDefinition>(SkillResourcesPath);
            var candidates = new List<SkillDefinition>();
            foreach (var skill in allSkills)
            {
                if (MatchesCategory(skill.category, chosenCategory))
                    candidates.Add(skill);
            }

            if (candidates.Count == 0) return "";
            return candidates[Random.Range(0, candidates.Count)].name;
        }

        /// <summary>
        /// categoryA/B/CWeightの比率でカテゴリを1つ重み付き抽選する。
        /// 合計が0以下の場合はNoneを返す（付与なし扱い）。
        /// </summary>
        private static SkillCategoryFlags PickWeightedCategory(GemDefinition def)
        {
            float a = Mathf.Max(0f, def.categoryAWeight);
            float b = Mathf.Max(0f, def.categoryBWeight);
            float c = Mathf.Max(0f, def.categoryCWeight);
            float total = a + b + c;
            if (total <= 0f) return SkillCategoryFlags.None;

            float roll = Random.value * total;
            if (roll < a) return SkillCategoryFlags.CategoryA;
            if (roll < a + b) return SkillCategoryFlags.CategoryB;
            return SkillCategoryFlags.CategoryC;
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

        // ================== Gem Reward Roll (UI用) ==================

        /// <summary>
        /// 指定エリアのジェムを count 個ロールして返す（インベントリには追加しない）
        /// </summary>
        public GemInstance[] RollGemsForArea(string areaId, int count = 3)
        {
            var gemDef = FindGemDefinitionForArea(areaId);
            if (gemDef == null)
            {
                Debug.LogWarning($"[GemManager] No GemDefinition for area: {areaId}");
                return new GemInstance[0];
            }
            var results = new GemInstance[count];
            for (int i = 0; i < count; i++)
                results[i] = RollGem(gemDef);
            return results;
        }

        /// <summary>
        /// 指定エリアの GemDefinition を返す（UI表示用）
        /// </summary>
        public GemDefinition GetGemDefinitionForArea(string areaId) => FindGemDefinitionForArea(areaId);

        /// <summary>
        /// GemInstance をインベントリへ追加してセーブする。満杯なら false を返す。
        /// </summary>
        public bool AddGemToInventory(GemInstance instance)
        {
            if (ProgressManager.Instance == null || instance == null) return false;
            var inventory = ProgressManager.Instance.Data.gemInventory;
            if (inventory.Count >= MaxInventory) return false;
            inventory.Add(instance);
            ProgressManager.Instance.Save();
            return true;
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

                // 基本スキルを適用（入手時にカテゴリ内からランダム選出済みのものをGemInstanceから読む）
                var baseSkill = LoadBaseSkill(gemInstance);
                if (baseSkill != null)
                {
                    skillManager.AddSkill(baseSkill, SkillSource.Gem);
                    Debug.Log($"[GemManager] Base skill: {baseSkill.skillName} ({gemDef.gemName})");
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

                int applied = 0;
                for (int i = 0; i < kvp.Value; i++)
                {
                    if (!skillManager.CanAcquireSkill(skill)) break;
                    skillManager.AddSkill(skill, SkillSource.Shop);
                    applied++;
                }

                Debug.Log($"[GemManager] DrinkBoost applied: {skill.skillName} +{applied} (requested={kvp.Value})");
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
        /// GemInstance の基本スキル SkillDefinition を返す（入手時にロールされ、baseSkillNameに保存済み）
        /// </summary>
        public SkillDefinition LoadBaseSkill(GemInstance instance)
        {
            if (instance == null || string.IsNullOrEmpty(instance.baseSkillName)) return null;
            return Resources.Load<SkillDefinition>($"{SkillResourcesPath}/{instance.baseSkillName}");
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

        // ================== Uses (使用回数) ==================

        /// <summary>
        /// 装備中ジェムの残り使用回数を1減らす（05_Gameシーン開始時、非チュートリアルプレイのみ呼ぶ）。
        /// クリア/ゲームオーバー/リタイアいずれのルートでも、プレイ開始時点で一律1回分消費する。
        /// </summary>
        public void DecrementEquippedGemUses()
        {
            var data = ProgressManager.Instance?.Data;
            if (data == null) return;
            if (data.hasUnlimitedGemUses) return; // 課金：無制限購入済みは一切消費しない

            bool changed = false;
            foreach (int idx in data.equippedGemIndices)
            {
                if (idx < 0 || idx >= data.gemInventory.Count) continue;
                var gemInst = data.gemInventory[idx];
                if (gemInst.isInfinite) continue; // このジェム個体だけ無限化済みなら消費しない
                if (gemInst.remainingUses > 0)
                {
                    gemInst.remainingUses--;
                    changed = true;
                }
            }

            if (changed) ProgressManager.Instance.Save();
        }

        /// <summary>
        /// 残り使用回数が0になったジェムをインベントリから削除する（AreaSelect復帰時に呼ぶ）。
        /// 削除したジェムの表示用テキスト（名前＋スキル構成）のリストを返す（消滅通知UI用）。
        /// 装備枠から削除されたインデックスの除去・繰り上げも行う。
        /// </summary>
        public List<string> RemoveDepletedGemsAndGetInfo()
        {
            var removedMessages = new List<string>();
            var data = ProgressManager.Instance?.Data;
            if (data == null) return removedMessages;
            if (data.hasUnlimitedGemUses) return removedMessages; // 課金：無制限購入済みは消滅しない

            bool changed = false;
            for (int i = data.gemInventory.Count - 1; i >= 0; i--)
            {
                var gemInst = data.gemInventory[i];
                if (gemInst == null || gemInst.isInfinite || gemInst.remainingUses > 0) continue;

                removedMessages.Add(BuildGemDisplayInfo(gemInst));
                data.gemInventory.RemoveAt(i);

                data.equippedGemIndices.RemoveAll(idx => idx == i);
                for (int j = 0; j < data.equippedGemIndices.Count; j++)
                {
                    if (data.equippedGemIndices[j] > i) data.equippedGemIndices[j]--;
                }
                changed = true;
            }

            if (changed) ProgressManager.Instance.Save();

            removedMessages.Reverse(); // インベントリの元の並び順（古い順）に戻す
            return removedMessages;
        }

        /// <summary>
        /// 装備中で残り使用回数がちょうど1（＝このプレイで消滅する）ジェムの表示用テキストを返す。
        /// エリア出撃前の確認ダイアログ用。
        /// </summary>
        public List<string> GetEquippedGemsAtLastUse()
        {
            var result = new List<string>();
            var data = ProgressManager.Instance?.Data;
            if (data == null) return result;
            if (data.hasUnlimitedGemUses) return result; // 課金：無制限購入済みは出撃前警告の対象外

            foreach (int idx in data.equippedGemIndices)
            {
                if (idx < 0 || idx >= data.gemInventory.Count) continue;
                var gemInst = data.gemInventory[idx];
                if (gemInst.isInfinite) continue; // このジェム個体だけ無限化済みなら警告対象外
                if (gemInst.remainingUses == 1)
                    result.Add(BuildGemDisplayInfo(gemInst));
            }
            return result;
        }

        /// <summary>
        /// 「ジェム名（基本スキル：X／追加スキル：Y・Z）」形式の表示用テキストを組み立てる。
        /// 同名ジェムを複数所持していても区別できるよう、スキル構成まで含める。
        /// </summary>
        public string BuildGemDisplayInfo(GemInstance instance)
        {
            var def = LoadGemDefinition(instance);
            string name = def != null ? def.gemName : instance.gemDefinitionName;

            var skillNames = new List<string>();
            var baseSkill = LoadBaseSkill(instance);
            if (baseSkill != null) skillNames.Add(baseSkill.skillName);
            var bonus1 = LoadBonusSkill1(instance);
            if (bonus1 != null) skillNames.Add(bonus1.skillName);
            var bonus2 = LoadBonusSkill2(instance);
            if (bonus2 != null) skillNames.Add(bonus2.skillName);

            string skillPart = skillNames.Count > 0 ? string.Join("・", skillNames) : "なし";
            return $"{name}（{skillPart}）";
        }

        /// <summary>
        /// 残り使用回数に応じて売却額を調整する（新品と残りわずかを同額にしないため）。
        /// 最低でも1Gは保証する。
        /// </summary>
        public int GetAdjustedSellPrice(GemInstance instance)
        {
            var def = LoadGemDefinition(instance);
            if (def == null) return 0;

            // 課金：無制限購入済みは残り回数を考慮せず満額
            if (ProgressManager.Instance != null && ProgressManager.Instance.Data.hasUnlimitedGemUses)
                return def.sellPrice;
            // このジェム個体だけ無限化済みの場合も満額
            if (instance.isInfinite)
                return def.sellPrice;

            if (def.maxUses <= 0) return def.sellPrice;

            float ratio = Mathf.Clamp01((float)instance.remainingUses / def.maxUses);
            return Mathf.Max(1, Mathf.RoundToInt(def.sellPrice * ratio));
        }

        /// <summary>課金：ジェム使用回数無制限が購入済みかどうか</summary>
        public bool HasUnlimitedGemUses => ProgressManager.Instance != null && ProgressManager.Instance.Data.hasUnlimitedGemUses;

        /// <summary>
        /// 指定したインベントリIndexのジェムを個別に無限化する（無限化アイテム使用時に呼ぶ）。
        /// アイテムの所持数消費はUI側（InfiniteStoneManager）が別途行う。
        /// </summary>
        public bool SetGemInfinite(int inventoryIdx)
        {
            var data = ProgressManager.Instance?.Data;
            if (data == null) return false;
            if (inventoryIdx < 0 || inventoryIdx >= data.gemInventory.Count) return false;

            var gemInst = data.gemInventory[inventoryIdx];
            if (gemInst == null || gemInst.isInfinite) return false;

            gemInst.isInfinite = true;
            ProgressManager.Instance.Save();
            return true;
        }
    }
}
