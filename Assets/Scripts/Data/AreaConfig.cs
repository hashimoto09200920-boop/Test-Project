using UnityEngine;

/// <summary>
/// エリア毎のゲーム設定を保持するScriptableObject
/// Wave Stages、背景、BGMなどをエリア毎に管理
/// </summary>
[CreateAssetMenu(fileName = "AreaConfig", menuName = "Game/Area Configuration", order = 1)]
public class AreaConfig : ScriptableObject
{
    public enum MidLayerScrollMode { Fog, Rain, None }

    [Header("Area Info")]
    [Tooltip("エリアの表示名（UI表示用）")]
    public string areaName = "Area 1";

    [Tooltip("エリア番号（1始まり）")]
    public int areaNumber = 1;

    [Tooltip("エリアの説明文（オプション）")]
    [TextArea(2, 4)]
    public string areaDescription = "";

    [Header("Wave Configuration")]
    [Tooltip("このエリアで使用するWave Stages設定\nEnemySpawner.WaveStageの配列")]
    public EnemySpawner.WaveStage[] waveStages;

    [Header("Visual Settings (Optional)")]
    [Tooltip("エリア専用の背景スプライト（Stage1/2用）")]
    public Sprite backgroundSprite;

    [Tooltip("Stage3用の背景スプライト（Stage3開始時にAからBへ切り替わる。設定しない場合は切り替えなし）")]
    public Sprite backgroundSpriteB;

    [Tooltip("背景色（背景スプライトがない場合に使用）")]
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.2f);

    [Tooltip("エリア専用の霧スプライト（Mid Layer用。設定しない場合は霧なし）")]
    public Sprite backgroundFogSprite;

    [Tooltip("霧レイヤーのスクロール方式")]
    public MidLayerScrollMode midLayerScrollMode = MidLayerScrollMode.Fog;

    [Tooltip("霧レイヤーのスケール")]
    public Vector3 backgroundFogScale = Vector3.one;

    [Tooltip("霧レイヤーのローカル座標")]
    public Vector3 backgroundFogPosition = Vector3.zero;

    [Tooltip("エリア専用の影絵スプライト（Stage1/2用）")]
    public Sprite backgroundSilhouetteSprite;

    [Tooltip("Stage1/2影絵のスケール")]
    public Vector3 backgroundSilhouetteScaleA = Vector3.one;

    [Tooltip("Stage1/2影絵のローカル座標")]
    public Vector3 backgroundSilhouettePositionA = Vector3.zero;

    [Tooltip("Stage3用の影絵スプライト（Stage3開始時にAからBへ切り替わる。設定しない場合は切り替えなし）")]
    public Sprite backgroundSilhouetteSpriteB;

    [Tooltip("Stage3影絵のスケール")]
    public Vector3 backgroundSilhouetteScaleB = Vector3.one;

    [Tooltip("Stage3影絵のローカル座標")]
    public Vector3 backgroundSilhouettePositionB = Vector3.zero;

    [Header("Audio Settings (Optional)")]
    [Tooltip("エリア専用のBGM（設定しない場合はデフォルトBGMを使用）")]
    public AudioClip bgmClip;

    [Tooltip("BGMの音量（0.0～1.0）")]
    [Range(0f, 1f)]
    public float bgmVolume = 0.7f;

    [Header("Difficulty (Optional)")]
    [Tooltip("難易度レベル（1=Easy, 2=Normal, 3=Hard）\n将来的な拡張用")]
    [Range(1, 5)]
    public int difficultyLevel = 1;

    /// <summary>
    /// 設定の検証
    /// </summary>
    public bool IsValid()
    {
        if (waveStages == null || waveStages.Length == 0)
        {
            Debug.LogError($"[AreaConfig] {name}: Wave Stages is empty!");
            return false;
        }

        return true;
    }

    /// <summary>
    /// エリア情報を文字列で取得
    /// </summary>
    public string GetDisplayName()
    {
        return $"{areaName} (Area {areaNumber})";
    }
}
