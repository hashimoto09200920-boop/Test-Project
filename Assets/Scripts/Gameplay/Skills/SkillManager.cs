using System.Collections.Generic;
using UnityEngine;

namespace Game.Skills
{
    /// <summary>
    /// アクティブなスキルを管理し、効果を各システムに適用する
    /// 05_Gameシーン内でのみ有効（シーン遷移時にクリア）
    /// </summary>
    public class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }

        [Header("References (Auto-find on Start)")]
        [SerializeField] private PaddleCostManager paddleCostManager;
        [SerializeField] private StrokeManager strokeManager;
        [SerializeField] private PaddleDrawer paddleDrawer;
        [SerializeField] private PixelDancerController pixelDancer;
        [SerializeField] private FloorHealth floorHealth;

        [Header("Debug")]
        [SerializeField] private bool showLog = true;

        // アクティブなスキルのリスト（重複選択可能）
        private readonly List<SkillDefinition> activeSkills = new List<SkillDefinition>();

        /// <summary>
        /// 現在アクティブなスキルのリスト（読み取り専用）
        /// </summary>
        public IReadOnlyList<SkillDefinition> ActiveSkills => activeSkills;

        // ベース値のキャッシュ（初期値を保存）
        private float baseLeftMaxCost;
        private float baseRedMaxCost;
        private float baseLeftRecovery;
        private float baseRedRecovery;
        private int baseMaxStrokes;
        private float baseNormalLifetime;
        private float baseRedLifetime;
        private int baseNormalHardness;
        private int baseRedHardness;
        private float baseJustDamageMultiplier;
        private int basePixelDancerHP;
        private int baseFloorHP;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // 自動検索
            if (paddleCostManager == null)
                paddleCostManager = FindFirstObjectByType<PaddleCostManager>();
            if (strokeManager == null)
                strokeManager = FindFirstObjectByType<StrokeManager>();
            if (paddleDrawer == null)
                paddleDrawer = FindFirstObjectByType<PaddleDrawer>();
            if (pixelDancer == null)
                pixelDancer = FindFirstObjectByType<PixelDancerController>();
            if (floorHealth == null)
                floorHealth = FindFirstObjectByType<FloorHealth>();

            // ベース値をキャッシュ
            CacheBaseValues();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 初期値をキャッシュ（スキル適用前の値）
        /// </summary>
        private void CacheBaseValues()
        {
            if (paddleCostManager != null)
            {
                baseLeftMaxCost = paddleCostManager.LeftMaxCost;
                baseRedMaxCost = paddleCostManager.RedMaxCost;
                baseLeftRecovery = paddleCostManager.LeftRecoverPerSecond;
                baseRedRecovery = paddleCostManager.RedRecoverPerSecond;
            }

            if (strokeManager != null)
            {
                baseMaxStrokes = strokeManager.MaxStrokes;
            }

            if (paddleDrawer != null)
            {
                baseNormalHardness = paddleDrawer.NormalHardness;
                baseRedHardness = paddleDrawer.RedHardness;
                baseJustDamageMultiplier = paddleDrawer.JustDamageMultiplier;
            }

            // Lifetime は PaddleDot prefab から取得（PaddleDrawer経由）
            // デフォルト値として設定
            baseNormalLifetime = 1.2f;
            baseRedLifetime = 1.2f;

            if (pixelDancer != null)
            {
                basePixelDancerHP = pixelDancer.InitialHP;
            }

            if (floorHealth != null)
            {
                baseFloorHP = floorHealth.MaxHP;
            }
        }

        /// <summary>
        /// スキルを追加してすぐに適用
        /// </summary>
        public void AddSkill(SkillDefinition skill)
        {
            if (skill == null) return;

            activeSkills.Add(skill);

            if (showLog)
            {
                Debug.Log($"[SkillManager] Added skill: {skill.skillName} ({skill.effectType})");
            }

            // 全スキルを再適用
            ApplyAllSkills();
        }

        /// <summary>
        /// すべてのアクティブスキルの効果を再計算して適用
        /// </summary>
        private void ApplyAllSkills()
        {
            // まずベース値にリセット
            ResetToBaseValues();

            // 各スキル効果タイプごとに累積
            Dictionary<SkillEffectType, float> accumulatedAdditive = new Dictionary<SkillEffectType, float>();
            Dictionary<SkillEffectType, float> accumulatedMultiplier = new Dictionary<SkillEffectType, float>();

            foreach (var skill in activeSkills)
            {
                if (skill.isMultiplier)
                {
                    if (!accumulatedMultiplier.ContainsKey(skill.effectType))
                        accumulatedMultiplier[skill.effectType] = 1f;
                    accumulatedMultiplier[skill.effectType] *= skill.effectValue;
                }
                else
                {
                    if (!accumulatedAdditive.ContainsKey(skill.effectType))
                        accumulatedAdditive[skill.effectType] = 0f;
                    accumulatedAdditive[skill.effectType] += skill.effectValue;
                }
            }

            // 適用（加算 → 乗算の順）
            foreach (var kvp in accumulatedAdditive)
            {
                ApplyEffect(kvp.Key, kvp.Value, false);
            }
            foreach (var kvp in accumulatedMultiplier)
            {
                ApplyEffect(kvp.Key, kvp.Value, true);
            }

            if (showLog)
            {
                Debug.Log($"[SkillManager] Applied {activeSkills.Count} skill(s)");
            }
        }

        /// <summary>
        /// ベース値にリセット
        /// </summary>
        private void ResetToBaseValues()
        {
            // TODO: 各システムにセッターを追加して、ベース値に戻す
            // 現在は ApplyEffect で直接上書きするので、ここでは何もしない
        }

        /// <summary>
        /// 個別のスキル効果を適用
        /// </summary>
        private void ApplyEffect(SkillEffectType effectType, float value, bool isMultiplier)
        {
            if (paddleCostManager == null || strokeManager == null || paddleDrawer == null ||
                pixelDancer == null || floorHealth == null)
            {
                return; // 必要なコンポーネントがない場合はスキップ
            }

            float newValue;

            switch (effectType)
            {
                case SkillEffectType.LeftMaxCostUp:
                    newValue = isMultiplier ? baseLeftMaxCost * value : baseLeftMaxCost + value;
                    paddleCostManager.SetLeftMaxCost(newValue);
                    break;

                case SkillEffectType.RedMaxCostUp:
                    newValue = isMultiplier ? baseRedMaxCost * value : baseRedMaxCost + value;
                    paddleCostManager.SetRedMaxCost(newValue);
                    break;

                case SkillEffectType.LeftRecoveryUp:
                    newValue = isMultiplier ? baseLeftRecovery * value : baseLeftRecovery + value;
                    paddleCostManager.SetLeftRecoverPerSecond(newValue);
                    break;

                case SkillEffectType.RedRecoveryUp:
                    newValue = isMultiplier ? baseRedRecovery * value : baseRedRecovery + value;
                    paddleCostManager.SetRedRecoverPerSecond(newValue);
                    break;

                case SkillEffectType.MaxStrokesUp:
                    int newStrokes = isMultiplier
                        ? Mathf.RoundToInt(baseMaxStrokes * value)
                        : baseMaxStrokes + Mathf.RoundToInt(value);
                    strokeManager.SetMaxStrokes(newStrokes);
                    break;

                case SkillEffectType.JustDamageUp:
                    newValue = isMultiplier ? baseJustDamageMultiplier * value : baseJustDamageMultiplier + value;
                    paddleDrawer.SetJustDamageMultiplier(newValue);
                    break;

                case SkillEffectType.LeftLifetimeUp:
                    newValue = isMultiplier ? baseNormalLifetime * value : baseNormalLifetime + value;
                    paddleDrawer.SetNormalLifetime(newValue);
                    break;

                case SkillEffectType.RedLifetimeUp:
                    newValue = isMultiplier ? baseRedLifetime * value : baseRedLifetime + value;
                    paddleDrawer.SetRedLifetime(newValue);
                    break;

                case SkillEffectType.HardnessUp:
                    int newNormalHardness = isMultiplier
                        ? Mathf.RoundToInt(baseNormalHardness * value)
                        : baseNormalHardness + Mathf.RoundToInt(value);
                    int newRedHardness = isMultiplier
                        ? Mathf.RoundToInt(baseRedHardness * value)
                        : baseRedHardness + Mathf.RoundToInt(value);
                    paddleDrawer.SetNormalHardness(newNormalHardness);
                    paddleDrawer.SetRedHardness(newRedHardness);
                    break;

                case SkillEffectType.PixelDancerHPUp:
                    int newPixelHP = isMultiplier
                        ? Mathf.RoundToInt(basePixelDancerHP * value)
                        : basePixelDancerHP + Mathf.RoundToInt(value);
                    pixelDancer.SetInitialHP(newPixelHP);
                    break;

                case SkillEffectType.FloorHPUp:
                    int newFloorHP = isMultiplier
                        ? Mathf.RoundToInt(baseFloorHP * value)
                        : baseFloorHP + Mathf.RoundToInt(value);
                    floorHealth.SetMaxHP(newFloorHP);
                    break;
            }
        }

        /// <summary>
        /// すべてのスキルをクリア（シーン遷移時に呼ぶ）
        /// </summary>
        public void ClearAllSkills()
        {
            activeSkills.Clear();
            ResetToBaseValues();

            if (showLog)
            {
                Debug.Log("[SkillManager] Cleared all skills");
            }
        }

        /// <summary>
        /// 現在のアクティブスキル数を取得
        /// </summary>
        public int GetActiveSkillCount()
        {
            return activeSkills.Count;
        }

        /// <summary>
        /// アクティブスキルのリストを取得（読み取り専用）
        /// </summary>
        public IReadOnlyList<SkillDefinition> GetActiveSkills()
        {
            return activeSkills.AsReadOnly();
        }
    }
}
