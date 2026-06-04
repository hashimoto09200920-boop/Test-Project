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

        public static event System.Action OnSelfHealPixelDancer;
        public static event System.Action OnSelfHealFloor;

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

        // スキル取得回数の追跡（スキル名 → 取得回数）
        private readonly Dictionary<string, int> skillAcquisitionCounts = new Dictionary<string, int>();

        // ジェム由来の取得回数（スキル名 → ジェム取得回数）
        private readonly Dictionary<string, int> skillGemCounts = new Dictionary<string, int>();

        // ショップ由来の取得回数（スキル名 → ドリンク購入取得回数）
        private readonly Dictionary<string, int> skillShopCounts = new Dictionary<string, int>();

        /// <summary>
        /// 現在アクティブなスキルのリスト（読み取り専用）
        /// </summary>
        public IReadOnlyList<SkillDefinition> ActiveSkills => activeSkills;

        /// <summary>
        /// スキル取得時に発火するイベント
        /// </summary>
        public event System.Action<SkillDefinition> OnSkillAcquired;

        /// <summary>
        /// ベース値（初期値）の公開プロパティ
        /// </summary>
        public float BaseLeftMaxCost => baseLeftMaxCost;
        public float BaseRedMaxCost => baseRedMaxCost;

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
        private float baseNormalAccelMultiplier;
        private float baseRedAccelMultiplier;

        // 敵速度低下スキルのパラメータ
        private float enemySlowMultiplier = 1f; // 1.0 = 速度変化なし
        private float enemySlowDuration = 0f;   // 持続時間（秒）

        [Header("Block Damage Settings")]
        [Tooltip("通常反射弾のブロックダメージ（デフォルト: 1）")]
        [SerializeField] private float blockNormalDamage = 1f;

        [Tooltip("Just反射弾のブロックダメージ（デフォルト: 2）")]
        [SerializeField] private float blockJustDamage = 2f;

        private float baseBlockNormalDamage;
        private float baseBlockJustDamage;

        [Header("Shield Skill Settings")]
        [Tooltip("シールドへのダメージ倍率（デフォルト: 1.0）")]
        [SerializeField] private float shieldDamageMultiplier = 1f;

        [Tooltip("シールド破壊後のダメージブースト倍率（デフォルト: 1.0）")]
        [SerializeField] private float shieldBreakDamageBoostMultiplier = 1f;

        [Tooltip("シールド破壊後の回復完全停止時間（秒）（デフォルト: 0）")]
        [SerializeField] private float shieldRecoveryStopDuration = 0f;

        // シールド破壊後のダメージブースト状態
        private bool shieldBreakBoostActive = false;
        private float shieldBreakBoostTimer = 0f;
        private float shieldBreakBoostDuration = 0f; // スキルアセットから取得
        private EnemyShield currentBoostShield = null; // B7発動元の敵シールド参照（敵死亡検知用）

        [Header("Category C Skill Settings")]
        [Tooltip("ジャスト反射猶予時間の延長量（加算）")]
        [SerializeField] private float justWindowExtension = 0f;

        [Tooltip("ジャスト反射貫通回数（0=なし、1=1回、-1=無制限）")]
        [SerializeField] private int justPenetrationCount = 0;

        private float selfHealDuration = 0f;

        [Tooltip("円判定猶予時間の延長量（加算）")]
        [SerializeField] private float circleTimeExtension = 0f;

        [Tooltip("円成立後の維持時間の延長量（加算）")]
        [SerializeField] private float circleExtraLifeExtension = 0f;

        // セルフヒールの状態
        private float selfHealTimer = 0f;
        private int selfHealAcquisitionCount = 0;

        // A8スキル: 敵ヒットごとダメージ加算の最大回数（スキル取得回数 = レベル）
        private int a8MaxAdditions = 0;
        // A8スキル: 1ヒットあたりの加算量（SkillDefinition.effectValueから取得）
        private float a8DamagePerHit = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 自動検索（SkillTestTool.Start()より先に実行されるよう、Awake()で初期化）
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

        private void Update()
        {
            // シールド破壊後のダメージブーストタイマー管理
            if (shieldBreakBoostActive)
            {
                // B7発動元の敵が死亡（シールドコンポーネントが破棄）したらブースト即時終了
                if (currentBoostShield == null)
                {
                    shieldBreakBoostActive = false;
                    shieldBreakBoostTimer = 0f;
                    if (showLog) Debug.Log("[SkillManager] Shield break damage boost cancelled: enemy died");
                }
                else
                {
                    shieldBreakBoostTimer -= Time.deltaTime;
                    if (shieldBreakBoostTimer <= 0f)
                    {
                        shieldBreakBoostActive = false;
                        shieldBreakBoostTimer = 0f;
                        currentBoostShield = null;
                        if (showLog) Debug.Log("[SkillManager] Shield break damage boost expired");
                    }
                }
            }

            // セルフヒールタイマー管理
            if (selfHealAcquisitionCount > 0 && selfHealDuration > 0f)
            {
                selfHealTimer += Time.deltaTime;
                if (selfHealTimer >= selfHealDuration)
                {
                    // HP回復処理
                    bool healed = false;

                    if (pixelDancer != null && pixelDancer.CurrentHP < pixelDancer.MaxHP)
                    {
                        pixelDancer.Heal(1);
                        healed = true;
                        OnSelfHealPixelDancer?.Invoke();
                    }

                    if (floorHealth != null && floorHealth.CurrentHP < floorHealth.MaxHP)
                    {
                        floorHealth.Heal(1);
                        healed = true;
                        OnSelfHealFloor?.Invoke();
                    }

                    if (healed && showLog)
                    {
                        Debug.Log($"[SkillManager] Self-heal triggered (duration: {selfHealDuration}s)");
                    }

                    // タイマーリセット
                    selfHealTimer = 0f;
                }
            }
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
                baseNormalAccelMultiplier = paddleDrawer.NormalAccelMultiplier;
                baseRedAccelMultiplier = paddleDrawer.RedAccelMultiplier;
            }

            // Lifetime は PaddleDrawer から取得
            if (paddleDrawer != null)
            {
                baseNormalLifetime = paddleDrawer.NormalLifetime;
                baseRedLifetime = paddleDrawer.RedLifetime;
            }
            else
            {
                // デフォルト値
                baseNormalLifetime = 1.2f;
                baseRedLifetime = 1.2f;
            }

            if (pixelDancer != null)
            {
                basePixelDancerHP = pixelDancer.InitialHP;
            }

            if (floorHealth != null)
            {
                baseFloorHP = floorHealth.MaxHP;
            }

            baseBlockNormalDamage = blockNormalDamage;
            baseBlockJustDamage   = blockJustDamage;
        }

        /// <summary>
        /// スキルを追加してすぐに適用
        /// </summary>
        public void AddSkill(SkillDefinition skill, SkillSource source = SkillSource.Card)
        {
            if (skill == null) return;

            activeSkills.Add(skill);

            // 取得回数を追跡
            string skillKey = skill.name; // アセット名をキーとして使用
            if (!skillAcquisitionCounts.ContainsKey(skillKey))
            {
                skillAcquisitionCounts[skillKey] = 0;
            }
            skillAcquisitionCounts[skillKey]++;

            // ジェム由来の場合は別途カウント
            if (source == SkillSource.Gem)
            {
                if (!skillGemCounts.ContainsKey(skillKey))
                    skillGemCounts[skillKey] = 0;
                skillGemCounts[skillKey]++;
            }
            // ショップ由来の場合は別途カウント
            if (source == SkillSource.Shop)
            {
                if (!skillShopCounts.ContainsKey(skillKey))
                    skillShopCounts[skillKey] = 0;
                skillShopCounts[skillKey]++;
            }

            if (showLog)
            {
                Debug.Log($"[SkillManager] Added skill: {skill.skillName} ({skill.effectType}) - Count: {skillAcquisitionCounts[skillKey]}/{skill.maxAcquisitionCount}");
            }

            // 全スキルを再適用
            ApplyAllSkills();

            // イベント発火
            OnSkillAcquired?.Invoke(skill);
        }

        /// <summary>
        /// スキルが取得可能かチェック（上限に達していないか）
        /// </summary>
        public bool CanAcquireSkill(SkillDefinition skill)
        {
            if (skill == null) return false;
            if (skill.maxAcquisitionCount <= 0) return true; // 0 = 無制限

            string skillKey = skill.name;
            if (!skillAcquisitionCounts.ContainsKey(skillKey))
            {
                return true; // まだ取得していない
            }

            return skillAcquisitionCounts[skillKey] < skill.maxAcquisitionCount;
        }

        /// <summary>
        /// スキルの現在の取得回数を取得
        /// </summary>
        public int GetSkillAcquisitionCount(SkillDefinition skill)
        {
            if (skill == null) return 0;
            string skillKey = skill.name;
            return skillAcquisitionCounts.ContainsKey(skillKey) ? skillAcquisitionCounts[skillKey] : 0;
        }

        /// <summary>
        /// スキルのジェム由来の取得回数を取得（HUDタイル色分け用）
        /// </summary>
        public int GetSkillGemCount(SkillDefinition skill)
        {
            if (skill == null) return 0;
            string skillKey = skill.name;
            return skillGemCounts.ContainsKey(skillKey) ? skillGemCounts[skillKey] : 0;
        }

        /// <summary>
        /// スキルのショップ由来の取得回数を取得（HUDタイル色分け用）
        /// </summary>
        public int GetSkillShopCount(SkillDefinition skill)
        {
            if (skill == null) return 0;
            string skillKey = skill.name;
            return skillShopCounts.ContainsKey(skillKey) ? skillShopCounts[skillKey] : 0;
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

            // ★SlowMotionEffectUpの回復速度ボーナスを別途累積（durationフィールドを使用）
            float slowMotionRecoveryBonus = 0f;

            foreach (var skill in activeSkills)
            {
                // ★B7：威力倍率は非スタック、取得回数×durationPerLevelで持続時間を計算
                if (skill.effectType == SkillEffectType.ShieldBreakDamageBoost)
                {
                    string skillKey = skill.name;
                    int count = skillAcquisitionCounts.ContainsKey(skillKey) ? skillAcquisitionCounts[skillKey] : 1;
                    shieldBreakBoostDuration = skill.duration * count;
                    shieldBreakDamageBoostMultiplier = skill.effectValue; // 非スタック（毎回同じ値で上書き）
                    continue; // accumulatedAdditive/Multiplierへの追加をスキップ
                }

                // ★B8：回復停止時間は非スタック、取得回数×durationPerLevelで停止時間を計算
                if (skill.effectType == SkillEffectType.ShieldRecoveryDelay)
                {
                    string skillKey = skill.name;
                    int count = skillAcquisitionCounts.ContainsKey(skillKey) ? skillAcquisitionCounts[skillKey] : 1;
                    shieldRecoveryStopDuration = skill.duration * count;
                    continue;
                }

                // ★敵速度低下：effectValueはスタックしない、取得回数×durationPerLevelで持続時間を計算
                if (skill.effectType == SkillEffectType.EnemySpeedDown)
                {
                    string skillKey = skill.name;
                    int count = skillAcquisitionCounts.ContainsKey(skillKey) ? skillAcquisitionCounts[skillKey] : 1;
                    enemySlowDuration = skill.effectValue * count;
                    ApplyEffect(skill.effectType, skill.effectValue, false);
                    continue; // accumulatedAdditive/Multiplierへの追加をスキップ
                }

                // ★セルフヒールの持続時間と取得回数を保存
                if (skill.effectType == SkillEffectType.SelfHeal)
                {
                    string skillKey = skill.name;
                    int count = skillAcquisitionCounts.ContainsKey(skillKey) ? skillAcquisitionCounts[skillKey] : 0;
                    selfHealAcquisitionCount = count;

                    // 取得回数に応じて持続時間をアセットから設定
                    if (count == 1)
                    {
                        selfHealDuration = Mathf.Max(0.1f, skill.duration);
                    }
                    else if (count >= 2)
                    {
                        float lv2 = skill.durationLevel2 > 0f ? skill.durationLevel2 : skill.duration;
                        selfHealDuration = Mathf.Max(0.1f, lv2);
                    }
                }

                // ★A8: 敵ヒットごとダメージ加算の最大回数 = 取得回数、加算量 = effectValue
                if (skill.effectType == SkillEffectType.ReflectedBulletSpeedUp)
                {
                    string skillKey = skill.name;
                    int count = skillAcquisitionCounts.ContainsKey(skillKey) ? skillAcquisitionCounts[skillKey] : 0;
                    a8MaxAdditions = count;
                    a8DamagePerHit = skill.effectValue;
                }

                // ★SlowMotionEffectUpの回復速度ボーナスを累積
                if (skill.effectType == SkillEffectType.SlowMotionEffectUp)
                {
                    slowMotionRecoveryBonus += skill.duration; // durationフィールドを回復速度ボーナスとして使用
                }

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
            Debug.Log($"[SkillManager] ApplyAllSkills: activeSkills.Count={activeSkills.Count}, accumulatedAdditive.Count={accumulatedAdditive.Count}");
            foreach (var kvp in accumulatedAdditive)
            {
                Debug.Log($"[SkillManager] Applying additive effect: {kvp.Key} = {kvp.Value}");
                ApplyEffect(kvp.Key, kvp.Value, false);
            }
            foreach (var kvp in accumulatedMultiplier)
            {
                ApplyEffect(kvp.Key, kvp.Value, true);
            }

            // ★SlowMotionEffectUpの回復速度ボーナスを適用
            if (slowMotionRecoveryBonus > 0f && SlowMotionManager.Instance != null)
            {
                SlowMotionManager.Instance.AddNormalRecoveryRateBonus(slowMotionRecoveryBonus);
                if (showLog)
                {
                    Debug.Log($"[SkillManager] SlowMotionEffectUp recovery bonus applied: +{slowMotionRecoveryBonus} sec/sec");
                }
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
            // SlowMotionManagerのスキルボーナスをリセット
            if (SlowMotionManager.Instance != null)
            {
                SlowMotionManager.Instance.ResetSkillBonuses();
            }

            // ブロックダメージをベース値に戻す
            blockNormalDamage = baseBlockNormalDamage;
            blockJustDamage   = baseBlockJustDamage;

            // A8スキルリセット
            a8MaxAdditions = 0;
            a8DamagePerHit = 1f;

            // シールド系倍率をリセット（*= で累積するため毎回1fに戻す必要がある）
            shieldDamageMultiplier          = 1f;
            shieldBreakDamageBoostMultiplier = 1f;
            shieldRecoveryStopDuration      = 0f;

            // C4: ApplyAllSkills呼び出しごとに+=で累積しないようリセット
            circleTimeExtension     = 0f;
            circleExtraLifeExtension = 0f;
        }

        /// <summary>
        /// 個別のスキル効果を適用
        /// </summary>
        private void ApplyEffect(SkillEffectType effectType, float value, bool isMultiplier)
        {
            // ★SlowMotionEffectUpは専用のマネージャーを使用するため、通常のコンポーネントチェックをスキップ
            if (effectType != SkillEffectType.SlowMotionEffectUp)
            {
                if (paddleCostManager == null || strokeManager == null || paddleDrawer == null ||
                    pixelDancer == null || floorHealth == null)
                {
                    return; // 必要なコンポーネントがない場合はスキップ
                }
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
                    Debug.Log($"[SkillManager] MaxStrokesUp: baseMaxStrokes={baseMaxStrokes}, value={value}, isMultiplier={isMultiplier}, newStrokes={newStrokes}");
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

                case SkillEffectType.ReflectedBulletSpeedUp:
                    // 新仕様: 敵ヒットごとに基礎ダメージ+1加算（a8MaxAdditionsはApplyAllSkillsループで設定済み）
                    break;

                case SkillEffectType.BlockDamageUp:
                    // ブロックダメージを増加（通常反射とJust反射の両方）
                    // 他のスキルと同様にベース値から計算する（ApplyAllSkills再実行で累積するバグを防ぐ）
                    blockNormalDamage = isMultiplier ? baseBlockNormalDamage * value : baseBlockNormalDamage + value;
                    blockJustDamage   = isMultiplier ? baseBlockJustDamage  * value : baseBlockJustDamage  + value;
                    if (showLog)
                    {
                        Debug.Log($"[SkillManager] BlockDamageUp applied: +{value} (Normal: {blockNormalDamage}, Just: {blockJustDamage})");
                    }
                    break;

                case SkillEffectType.EnemySpeedDown:
                    enemySlowMultiplier = 0.5f;
                    if (showLog)
                    {
                        Debug.Log($"[SkillManager] EnemySpeedDown configured: {value * 100f}% slow for {enemySlowDuration}s (multiplier: {enemySlowMultiplier})");
                    }
                    break;

                case SkillEffectType.ShieldDamageUp:
                    // シールドダメージ倍率を設定（乗算）
                    if (isMultiplier)
                    {
                        shieldDamageMultiplier *= value;
                    }
                    else
                    {
                        shieldDamageMultiplier += value;
                    }
                    if (showLog)
                    {
                        Debug.Log($"[SkillManager] ShieldDamageUp applied: x{shieldDamageMultiplier}");
                    }
                    break;

                case SkillEffectType.ShieldBreakDamageBoost:
                    // シールド破壊後ダメージブースト倍率を設定（乗算）
                    if (isMultiplier)
                    {
                        shieldBreakDamageBoostMultiplier *= value;
                    }
                    else
                    {
                        shieldBreakDamageBoostMultiplier += value;
                    }
                    if (showLog)
                    {
                        Debug.Log($"[SkillManager] ShieldBreakDamageBoost applied: x{shieldBreakDamageBoostMultiplier} for {shieldBreakBoostDuration}s");
                    }
                    break;

                case SkillEffectType.ShieldRecoveryDelay:
                    // B8: duration-basedに変更済み（ApplyAllSkillsループで処理）
                    if (showLog)
                    {
                        Debug.Log($"[SkillManager] ShieldRecoveryDelay: stop duration = {shieldRecoveryStopDuration}s");
                    }
                    break;

                case SkillEffectType.JustWindowExtension:
                    // ジャスト反射猶予時間延長（加算）
                    if (isMultiplier)
                    {
                        justWindowExtension *= value;
                    }
                    else
                    {
                        justWindowExtension += value;
                    }
                    if (showLog)
                    {
                        Debug.Log($"[SkillManager] JustWindowExtension applied: +{justWindowExtension}s");
                    }
                    break;

                case SkillEffectType.JustPenetration:
                    // ジャスト反射貫通（取得回数で判定）
                    // このスキルは取得回数に応じて動作が変わるため、ここでは取得回数をカウント
                    // 実際の貫通処理はGetJustPenetrationCount()で取得回数から判定
                    if (showLog)
                    {
                        Debug.Log($"[SkillManager] JustPenetration skill active");
                    }
                    break;

                case SkillEffectType.SelfHeal:
                    // セルフヒール（取得回数と持続時間はApplyAllSkills()で管理）
                    if (showLog)
                    {
                        Debug.Log($"[SkillManager] SelfHeal applied: {selfHealDuration}s (count: {selfHealAcquisitionCount})");
                    }
                    break;

                case SkillEffectType.CircleTimeExtension:
                    // 円判定猶予時間延長（加算）＋維持時間延長（固定0.5s/取得）
                    if (isMultiplier)
                    {
                        circleTimeExtension *= value;
                        circleExtraLifeExtension *= value;
                    }
                    else
                    {
                        circleTimeExtension += value;
                        circleExtraLifeExtension += 0.5f;
                    }
                    if (showLog)
                    {
                        Debug.Log($"[SkillManager] CircleTimeExtension applied: gate+{circleTimeExtension}s, extraLife+{circleExtraLifeExtension}s");
                    }
                    break;

                case SkillEffectType.SlowMotionEffectUp:
                    // スローモーション効果アップ（持続時間のみ、回復速度はApplyAllSkills()で処理）
                    if (SlowMotionManager.Instance != null)
                    {
                        // 最大持続時間を増加（effectValue = 持続時間ボーナス（秒））
                        SlowMotionManager.Instance.AddMaxDurationBonus(value);

                        if (showLog)
                        {
                            Debug.Log($"[SkillManager] SlowMotionEffectUp duration applied: +{value}s");
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// HPを満タンに回復（ゲーム開始時のジェム/ドリンクブースト適用後に呼ぶ）
        /// </summary>
        public void RestoreHPToFull()
        {
            if (pixelDancer != null) pixelDancer.RestoreToFullHP();
            if (floorHealth != null) floorHealth.RestoreToFullHP();
        }

        /// <summary>
        /// すべてのスキルをクリア（シーン遷移時に呼ぶ）
        /// </summary>
        public void ClearAllSkills()
        {
            activeSkills.Clear();
            skillAcquisitionCounts.Clear();
            skillGemCounts.Clear();
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

        /// <summary>
        /// 敵速度低下スキルが有効か確認し、パラメータを取得
        /// </summary>
        /// <param name="slowMultiplier">速度倍率（0.7 = 70%の速度）</param>
        /// <param name="duration">持続時間（秒）</param>
        /// <returns>スキルが有効な場合true</returns>
        public bool TryGetEnemySlowEffect(out float slowMultiplier, out float duration)
        {
            slowMultiplier = enemySlowMultiplier;
            duration = enemySlowDuration;

            // EnemySpeedDown スキルが有効かチェック
            foreach (var skill in activeSkills)
            {
                if (skill.effectType == SkillEffectType.EnemySpeedDown)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A8スキルの最大ダメージ加算回数を取得（スキル取得回数 = レベル）
        /// </summary>
        public int GetA8MaxAdditions() => a8MaxAdditions;

        /// <summary>
        /// A8スキルの1ヒットあたりダメージ加算量を取得（SkillDefinition.effectValueから）
        /// </summary>
        public float GetA8DamagePerHit() => a8DamagePerHit;

        /// <summary>
        /// ブロックダメージの値を取得（スキルによる変更があれば反映）
        /// </summary>
        /// <param name="normalDamage">通常反射弾のブロックダメージ</param>
        /// <param name="justDamage">Just反射弾のブロックダメージ</param>
        public void GetBlockDamage(out float normalDamage, out float justDamage)
        {
            normalDamage = blockNormalDamage;
            justDamage = blockJustDamage;
        }

        /// <summary>
        /// シールドダメージ倍率を取得
        /// </summary>
        public float GetShieldDamageMultiplier()
        {
            return shieldDamageMultiplier;
        }

        public bool IsShieldBreakBoostActive => shieldBreakBoostActive;
        public float ShieldBreakBoostTimeRemaining => shieldBreakBoostTimer;
        public float ShieldBreakBoostMultiplier => shieldBreakDamageBoostMultiplier;
        public EnemyShield CurrentBoostShield => currentBoostShield;

        /// <summary>
        /// シールド破壊後のダメージブースト効果を取得
        /// </summary>
        /// <param name="boostMultiplier">ダメージブースト倍率</param>
        /// <param name="duration">持続時間（秒）</param>
        /// <returns>スキルが有効な場合true</returns>
        public bool TryGetShieldBreakDamageBoost(out float boostMultiplier, out float duration)
        {
            boostMultiplier = shieldBreakDamageBoostMultiplier;
            duration = shieldBreakBoostDuration;

            // ShieldBreakDamageBoost スキルが有効かチェック
            foreach (var skill in activeSkills)
            {
                if (skill.effectType == SkillEffectType.ShieldBreakDamageBoost)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// シールド破壊後の回復完全停止時間を取得（秒）
        /// </summary>
        public float GetShieldRecoveryStopDuration()
        {
            return shieldRecoveryStopDuration;
        }

        /// <summary>
        /// シールド破壊後のダメージブーストが有効かチェック（指定シールドのみ有効）
        /// </summary>
        /// <param name="shield">判定対象の EnemyShield。null または破壊元シールドと異なる場合は 1f を返す。</param>
        /// <returns>現在のダメージ倍率（1.0 = ブースト無効）</returns>
        public float GetCurrentDamageMultiplier(EnemyShield shield = null)
        {
            if (!shieldBreakBoostActive) return 1f;
            if (shield == null || shield != currentBoostShield) return 1f;

            // ShieldBreakDamageBoost スキルが有効な場合のみブーストを適用
            foreach (var skill in activeSkills)
            {
                if (skill.effectType == SkillEffectType.ShieldBreakDamageBoost)
                {
                    return shieldBreakDamageBoostMultiplier;
                }
            }

            return 1f;
        }

        /// <summary>
        /// 敵のシールド破壊イベントをサブスクライブ（EnemySpawnerから呼ばれる）
        /// </summary>
        public void SubscribeToEnemyShield(EnemyShield shield)
        {
            if (shield == null) return;

            // ShieldBreakDamageBoost スキルが有効な場合のみサブスクライブ
            bool hasSkill = false;
            foreach (var skill in activeSkills)
            {
                if (skill.effectType == SkillEffectType.ShieldBreakDamageBoost)
                {
                    hasSkill = true;
                    break;
                }
            }

            if (!hasSkill) return;

            shield.OnShieldBroken += () => { currentBoostShield = shield; OnEnemyShieldBroken(); };
        }

        /// <summary>
        /// ブースト中に敵へ弾がヒットした際にタイマーをリセットする
        /// </summary>
        public void RefreshShieldBreakBoost()
        {
            if (!shieldBreakBoostActive) return;
            shieldBreakBoostTimer = shieldBreakBoostDuration;
        }

        /// <summary>
        /// シールド破壊イベントハンドラ
        /// </summary>
        private void OnEnemyShieldBroken()
        {
            // タイマーを開始（スタックしない）
            shieldBreakBoostActive = true;
            shieldBreakBoostTimer = shieldBreakBoostDuration;

            if (showLog)
            {
                Debug.Log($"[SkillManager] Shield broken! Damage boost activated: x{shieldBreakDamageBoostMultiplier} for {shieldBreakBoostDuration}s");
            }
        }

        // ===== Category C Skill Getters =====

        /// <summary>
        /// ジャスト反射猶予時間の延長量を取得（PaddleDrawerから呼ばれる）
        /// </summary>
        public float GetJustWindowExtension()
        {
            return justWindowExtension;
        }

        /// <summary>
        /// ジャスト反射貫通回数を取得（PaddleDotから呼ばれる）
        /// </summary>
        /// <returns>0=貫通なし、1=1回のみ貫通、-1=無制限貫通</returns>
        public int GetJustPenetrationCount()
        {
            // JustPenetrationスキルの取得回数をチェック
            foreach (var skill in activeSkills)
            {
                if (skill.effectType == SkillEffectType.JustPenetration)
                {
                    string skillKey = skill.name;
                    int count = skillAcquisitionCounts.ContainsKey(skillKey) ? skillAcquisitionCounts[skillKey] : 0;

                    if (count == 0)
                    {
                        return 0; // 未取得
                    }
                    else if (count == 1)
                    {
                        return 1; // 1回のみ貫通
                    }
                    else // count >= 2
                    {
                        return -1; // 無制限貫通
                    }
                }
            }

            return 0; // スキルなし
        }

        /// <summary>
        /// 円判定猶予時間の延長量を取得（PaddleDrawerから呼ばれる）
        /// </summary>
        public float GetCircleTimeExtension()
        {
            return circleTimeExtension;
        }

        /// <summary>
        /// 円成立後の維持時間の延長量を取得（PaddleDrawerから呼ばれる）
        /// </summary>
        public float GetCircleExtraLifeExtension()
        {
            return circleExtraLifeExtension;
        }

        /// <summary>
        /// ブロックアイテムの円収集倍率を返す（基本2x、C4取得ごとに+1x、最大4x）
        /// </summary>
        public int GetBlockItemCircleMultiplier()
        {
            int c4Count = Mathf.RoundToInt(circleExtraLifeExtension / 0.5f);
            return Mathf.Clamp(2 + c4Count, 2, 4);
        }

        /// <summary>
        /// セルフヒールタイマーをリセット（被弾時に呼ばれる）
        /// </summary>
        public void ResetSelfHealTimer()
        {
            selfHealTimer = 0f;
            if (showLog && selfHealAcquisitionCount > 0)
            {
                Debug.Log("[SkillManager] Self-heal timer reset due to damage");
            }
        }

        /// <summary>
        /// ジャスト反射貫通スキルが有効か確認（EnemyBulletから呼ばれる）
        /// 消費カウントはEnemyBullet側で弾ごとに管理する
        /// </summary>
        /// <returns>貫通スキルを持っている場合true</returns>
        public bool TryConsumeJustPenetration()
        {
            return GetJustPenetrationCount() != 0;
        }

        // ===== Debug / Verification =====

        [ContextMenu("Debug: Print Shield Skill Values")]
        private void DebugPrintShieldValues()
        {
            int b6Count = 0, b7Count = 0, b8Count = 0;
            foreach (var skill in activeSkills)
            {
                if (skill.effectType == SkillEffectType.ShieldDamageUp)         b6Count++;
                if (skill.effectType == SkillEffectType.ShieldBreakDamageBoost) b7Count++;
                if (skill.effectType == SkillEffectType.ShieldRecoveryDelay)    b8Count++;
            }

            Debug.Log(
                $"[SkillManager Debug] Shield Skill Values\n" +
                $"  B6 ShieldDamageUp      : 取得{b6Count}枚 → shieldDamageMultiplier = {shieldDamageMultiplier:F4}  (期待値: {Mathf.Pow(1.2f, b6Count):F4})\n" +
                $"  B7 ShieldBreakBoost    : 取得{b7Count}枚 → shieldBreakDamageBoostMultiplier = {shieldBreakDamageBoostMultiplier:F4}, duration={shieldBreakBoostDuration}s\n" +
                $"  B8 ShieldRecoveryDelay : 取得{b8Count}枚 → shieldRecoveryStopDuration = {shieldRecoveryStopDuration}s  (期待値: {b8Count * 10f}s)"
            );
        }
    }
}
