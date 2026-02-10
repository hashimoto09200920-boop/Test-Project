using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Skills;

/// <summary>
/// スキルの詳細情報を表示するツールチップ
/// </summary>
public class SkillTooltip : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI currentEffectText;
    [SerializeField] private TextMeshProUGUI nextEffectText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Display Settings")]
    [SerializeField] private Vector2 offset = new Vector2(10f, 10f);
    [SerializeField] private float fadeSpeed = 10f;

    private RectTransform rectTransform;
    private bool isVisible = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // テキストフィールドを自動検出
        AutoAssignTextFields();

        // 初期状態は非表示
        Hide();
    }

    /// <summary>
    /// 子オブジェクトから自動的にテキストフィールドを割り当て
    /// </summary>
    private void AutoAssignTextFields()
    {
        if (skillNameText == null)
        {
            Transform t = transform.Find("SkillName");
            if (t != null) skillNameText = t.GetComponent<TextMeshProUGUI>();
        }

        if (categoryText == null)
        {
            Transform t = transform.Find("Category");
            if (t != null) categoryText = t.GetComponent<TextMeshProUGUI>();
        }

        if (levelText == null)
        {
            Transform t = transform.Find("Level");
            if (t != null) levelText = t.GetComponent<TextMeshProUGUI>();
        }

        if (descriptionText == null)
        {
            Transform t = transform.Find("Description");
            if (t != null) descriptionText = t.GetComponent<TextMeshProUGUI>();
        }

        if (currentEffectText == null)
        {
            Transform t = transform.Find("CurrentEffect");
            if (t != null) currentEffectText = t.GetComponent<TextMeshProUGUI>();
        }

        if (nextEffectText == null)
        {
            Transform t = transform.Find("NextEffect");
            if (t != null) nextEffectText = t.GetComponent<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        if (isVisible)
        {
            // マウスカーソルに追従
            Vector2 mousePosition = Input.mousePosition;
            rectTransform.position = mousePosition + offset;

            // 画面外に出ないように調整
            ClampToScreen();

            // フェードイン
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, Time.deltaTime * fadeSpeed);
        }
        else
        {
            // フェードアウト
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0f, Time.deltaTime * fadeSpeed);
        }
    }

    /// <summary>
    /// ツールチップを表示
    /// </summary>
    public void Show(SkillDefinition skill, int currentLevel, int maxLevel)
    {
        if (skill == null) return;

        isVisible = true;
        canvasGroup.blocksRaycasts = false;

        // スキル名
        if (skillNameText != null)
        {
            skillNameText.text = skill.skillName;
        }

        // カテゴリ
        if (categoryText != null)
        {
            string categoryName = skill.category switch
            {
                SkillCategory.CategoryA => "カテゴリA",
                SkillCategory.CategoryB => "カテゴリB",
                _ => "その他"
            };
            categoryText.text = categoryName;
        }

        // レベル情報
        if (levelText != null)
        {
            if (currentLevel == 0)
            {
                levelText.text = "未取得";
            }
            else if (maxLevel > 0 && currentLevel >= maxLevel)
            {
                levelText.text = $"取得回数: {currentLevel}/{maxLevel} (MAX)";
            }
            else if (maxLevel > 0)
            {
                levelText.text = $"取得回数: {currentLevel}/{maxLevel}";
            }
            else
            {
                levelText.text = $"取得回数: {currentLevel} (無制限)";
            }
        }

        // 説明
        if (descriptionText != null)
        {
            descriptionText.text = skill.description;
        }

        // 現在の効果
        if (currentEffectText != null)
        {
            if (currentLevel > 0)
            {
                currentEffectText.text = $"現在の効果: {GetEffectDescription(skill, currentLevel)}";
            }
            else
            {
                currentEffectText.text = "";
            }
        }

        // 次レベルの効果
        if (nextEffectText != null)
        {
            if (maxLevel > 0 && currentLevel >= maxLevel)
            {
                nextEffectText.text = "最大レベル到達";
            }
            else
            {
                nextEffectText.text = $"次レベル: {GetEffectDescription(skill, currentLevel + 1)}";
            }
        }
    }

    /// <summary>
    /// ツールチップを非表示
    /// </summary>
    public void Hide()
    {
        isVisible = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 効果の説明文を生成
    /// </summary>
    private string GetEffectDescription(SkillDefinition skill, int level)
    {
        float totalValue = skill.effectValue * level;

        string effectTypeStr = skill.effectType switch
        {
            // Category A
            SkillEffectType.LeftMaxCostUp => $"+{totalValue}",
            SkillEffectType.RedMaxCostUp => $"+{totalValue}",
            SkillEffectType.LeftRecoveryUp => $"+{totalValue}",
            SkillEffectType.RedRecoveryUp => $"+{totalValue}",
            SkillEffectType.MaxStrokesUp => $"+{totalValue}本",
            SkillEffectType.JustDamageUp => $"x{1f + totalValue * 0.01f:F2}",
            SkillEffectType.LeftLifetimeUp => $"+{totalValue}秒",
            SkillEffectType.RedLifetimeUp => $"+{totalValue}秒",
            SkillEffectType.ReflectedBulletSpeedUp => $"+{totalValue}%",
            SkillEffectType.BlockDamageUp => $"+{totalValue}",

            // Category B
            SkillEffectType.HardnessUp => $"+{totalValue}",
            SkillEffectType.FloorHPUp => $"+{totalValue}",
            SkillEffectType.PixelDancerHPUp => $"+{totalValue}",
            SkillEffectType.EnemySpeedDown => $"-{totalValue}%",
            SkillEffectType.JustDamageUpB => $"x{1f + totalValue * 0.01f:F2}",
            SkillEffectType.ShieldDamageUp => $"+{totalValue}%",
            SkillEffectType.ShieldBreakDamageBoost => $"x{totalValue:F2}",
            SkillEffectType.ShieldRecoveryDelay => $"+{totalValue}秒",

            // Category C
            SkillEffectType.JustWindowExtension => $"+{totalValue}秒",
            SkillEffectType.JustPenetration => totalValue >= 999 ? "無制限" : $"{totalValue}回",
            SkillEffectType.SelfHeal => $"{skill.duration}秒間有効",
            SkillEffectType.CircleTimeExtension => $"+{totalValue}秒",

            _ => $"+{totalValue}"
        };

        return effectTypeStr;
    }

    /// <summary>
    /// ツールチップが画面外に出ないように調整
    /// </summary>
    private void ClampToScreen()
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

        Vector2 adjustedPosition = rectTransform.position;

        // 右端チェック
        if (max.x > Screen.width)
        {
            adjustedPosition.x -= (max.x - Screen.width);
        }

        // 左端チェック
        if (min.x < 0)
        {
            adjustedPosition.x -= min.x;
        }

        // 上端チェック
        if (max.y > Screen.height)
        {
            adjustedPosition.y -= (max.y - Screen.height);
        }

        // 下端チェック
        if (min.y < 0)
        {
            adjustedPosition.y -= min.y;
        }

        rectTransform.position = adjustedPosition;
    }
}
