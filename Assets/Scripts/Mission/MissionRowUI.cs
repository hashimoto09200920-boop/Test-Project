using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ミッション画面の1行分の部品（2026/11/07）。
/// Hierarchyの「RowTemplate」（非表示の見本）に付けておき、プレイ中はMissionPanelUIがこれを50行分複製して使う。
/// 文字の大きさ・色・配置は、見本（RowTemplate）をPlay前にInspectorで調整する。
/// </summary>
public class MissionRowUI : MonoBehaviour
{
    [Header("部品（Tools > ミッション >「6. ミッション画面を作成」で自動で入る）")]
    public Image background;
    public TextMeshProUGUI nameText;
    [Tooltip("2段目のミッションに出す「Ⅱ」の札")]
    public GameObject tierTag;
    public TextMeshProUGUI tierText;
    [Tooltip("進捗バーの中身。横の長さ（anchorMax.x）を進捗の割合にする")]
    public RectTransform progressFill;
    public Image progressFillImage;
    [Tooltip("進捗の数字（数字用フォント）")]
    public TextMeshProUGUI progressText;
    [Tooltip("報酬の種類の札（パッシブ／ゴールド）の背景")]
    public Image rewardChipImage;
    public TextMeshProUGUI rewardTypeText;
    [Tooltip("パッシブの報酬名")]
    public TextMeshProUGUI rewardValueText;
    [Tooltip("ゴールドの金額（数字用フォント）")]
    public TextMeshProUGUI rewardGoldText;
    public Button claimButton;
    public Image claimButtonImage;
    [Tooltip("受け取れる間、ゆっくり明るさが変わる光")]
    public Image claimGlow;
    public TextMeshProUGUI claimText;

    [HideInInspector] public MissionDefinition definition;
    [HideInInspector] public bool claimable;

    public void SetProgress01(float ratio)
    {
        if (progressFill == null) return;
        ratio = Mathf.Clamp01(ratio);
        progressFill.anchorMin = new Vector2(0f, 0f);
        progressFill.anchorMax = new Vector2(ratio, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
    }
}
