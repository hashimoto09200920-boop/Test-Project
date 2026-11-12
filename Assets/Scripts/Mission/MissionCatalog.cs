using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Skills;

/// <summary>ミッション1個分の設定</summary>
[Serializable]
public class MissionDefinition
{
    [Tooltip("ミッションの識別名。★セーブの受け取り済み記録に使うので、作成後は変えないこと（例：M01）")]
    public string id = "M00";

    [Tooltip("ミッション画面に出す名前（日本語）")]
    public string displayName = "";

    [Tooltip("ミッション画面に出す名前（英語）")]
    public string displayNameEn = "";

    [Tooltip("名前に入っているArea名（例：SOUL TOWN）。そのAreaが解放されるまでは、AreaSelectと同じく「？」に置き換えて表示する。空なら置き換えない")]
    public string nameAreaTitle = "";

    [Tooltip("名前に入っているAreaの番号（1〜10）。0なら置き換えない")]
    public int nameAreaNumber = 0;

    /// <summary>画面に出す名前。名前に入っているAreaがまだ解放されていなければ、Area名を「？」にする（AreaSelectと同じ判定）</summary>
    public string GetDisplayName(bool en)
    {
        string name = en && !string.IsNullOrEmpty(displayNameEn) ? displayNameEn : displayName;
        if (nameAreaNumber > 0 && !string.IsNullOrEmpty(nameAreaTitle) && !string.IsNullOrEmpty(name)
            && !Game.Progress.UnlockRules.IsAreaUnlocked($"Area_{nameAreaNumber:D2}"))
            name = name.Replace(nameAreaTitle, en ? "?" : "？");
        return name;
    }

    [Header("達成条件")]
    [Tooltip("数える項目")]
    public MissionStat stat = MissionStat.LinesDrawn;

    [Tooltip("Areaごとの項目（RankS・AreaClearNoDown・AreaFirstClear）のときだけ使うArea番号（1〜10）。それ以外は0")]
    public int areaNumber = 0;

    [Tooltip("目標の回数（この数以上で達成）")]
    [Min(1)] public int target = 1;

    [Tooltip("このミッションを出す前に受け取っておく必要があるミッションのid（2段階の2段目用）。空なら最初から出す")]
    public string requiresClaimedId = "";

    [Header("報酬")]
    public MissionRewardType rewardType = MissionRewardType.Gold;

    [Tooltip("報酬がゴールドのときの金額")]
    [Min(0)] public int goldAmount = 0;

    [Tooltip("報酬が虹の雫のときの個数")]
    [Min(0)] public int rainbowDropAmount = 0;

    [Tooltip("報酬がお食事券のときの枚数")]
    [Min(0)] public int foodTicketAmount = 0;

    [Header("お知らせ（2026/11/11）")]
    [Tooltip("達成した時に、通知の後に1行ずつ出すお知らせ（日本語）。例：次のエリア「SOUL TOWN」をアンロック／ジェムの装備スロット+1。空なら出さない")]
    public string[] noticeLines = new string[0];
    [Tooltip("お知らせ（英語）。noticeLines と同じ順番")]
    public string[] noticeLinesEn = new string[0];

    /// <summary>お知らせの行（言語に合わせる。無ければ空）</summary>
    public string[] GetNoticeLines(bool en)
    {
        var a = en && noticeLinesEn != null && noticeLinesEn.Length > 0 ? noticeLinesEn : noticeLines;
        return a ?? new string[0];
    }

    /// <summary>ミッション画面の並び順に使うカテゴリ（0＝エリア、1＝線・反射、2＝撃破、3＝スローモーション、4＝アイテム、5＝その他）</summary>
    public int Category
    {
        get
        {
            switch (stat)
            {
                case MissionStat.RankS: case MissionStat.Area10Clear: case MissionStat.Area9ClearNoDown:
                case MissionStat.AreaClearNoDown: case MissionStat.AreaFirstClear:
                    return 0;
                case MissionStat.ReflectWhite: case MissionStat.ReflectRed: case MissionStat.GaugeFullWhite: case MissionStat.GaugeFullRed:
                case MissionStat.MultiReflectWhite: case MissionStat.MultiReflectRed: case MissionStat.LinesDrawn:
                case MissionStat.JustReflects: case MissionStat.CircleEnclose5Bullets:
                    return 1;
                case MissionStat.Kills: case MissionStat.KillsSmall: case MissionStat.KillsMedium: case MissionStat.KillsBoss:
                case MissionStat.ShieldBreaksSmall: case MissionStat.ShieldBreaksMedium: case MissionStat.ShieldBreaksBoss:
                case MissionStat.BlocksDestroyed:
                    return 2;
                case MissionStat.SlowMotionCircles: case MissionStat.SlowMotionUses:
                    return 3;
                case MissionStat.ItemsCollected: case MissionStat.SoulsRescued: case MissionStat.GoldenShoesCollected:
                case MissionStat.GoldBoltCollected: case MissionStat.MultiBoundCollected: case MissionStat.SpiralShotCollected:
                case MissionStat.BlastShotCollected: case MissionStat.SplitShotCollected: case MissionStat.FoodTicketCollected:
                    return 4;
                default:
                    return 5; // ゴールド獲得・スキルカード・ダメージを受けずに・オーバーヒートなし
            }
        }
    }

    [Tooltip("報酬がパッシブのときの種類")]
    public MissionPassiveType passiveType = MissionPassiveType.None;

    [Tooltip("段階のあるパッシブの段（1〜5。スキル引き直しだけ5段まで、他は2段まで）。上の段は下の段と置き換え（足し算ではない）")]
    [Range(1, 5)] public int passiveTier = 1;

    [Tooltip("passiveTypeがCategoryASkillのときに獲得するスキル")]
    public SkillDefinition passiveSkill;

    [Tooltip("ミッション画面に出す報酬名（日本語）。例：マルチライン、小型へのダメージ +1")]
    public string rewardLabel = "";

    [Tooltip("ミッション画面に出す報酬名（英語）")]
    public string rewardLabelEn = "";
}

/// <summary>パッシブの効果の数値（Inspectorで調整）。配列の[0]=1段目、[1]=2段目</summary>
[Serializable]
public class PassiveValues
{
    [Tooltip("小型・中型・ボスへのダメージ（反射弾1ヒットごとにHPへのダメージへ足す）")]
    public int[] damageBonus = { 1, 2 };
    [Tooltip("小型・中型・ボスへのシールドダメージ（1ヒットごとにシールドへのダメージへ足す）")]
    public int[] shieldDamageBonus = { 1, 2 };
    [Tooltip("白線・赤線・スローモーションのオーバーヒート時間の短縮割合（0.1＝10%短縮）")]
    public float[] overheatShorten = { 0.1f, 0.2f };
    [Tooltip("ゴールド獲得量の増加割合（0.25＝+25%）。増える分の端数は1回ごとに切り上げ")]
    public float[] goldGain = { 0.25f, 0.5f };
    [Tooltip("スキル引き直しの回数（1回のプレイごと）。[0]〜[4]＝1段目〜5段目")]
    public int[] skillReroll = { 1, 2, 3, 4, 5 };
    [Tooltip("ステージクリア時のダンサーとフロアの回復量")]
    public int[] stageClearHeal = { 1, 2 };
    // ★2026/11/12 効果時間の決め方を「その秒数にする」から「基礎値（20秒）に足す秒数」に変えた。
    //   アセットに保存された前の値（40/50秒）が使われないよう項目名も変えた
    [Tooltip("ゴールドシューズの基礎効果時間に足す秒数。[0]=1段目（+5）、[1]=2段目（+10）。基礎値は GoldenShoesBuff の「Base Duration 20」")]
    public float[] goldenShoesDurationBonus = { 5f, 10f };
    [Tooltip("アクセラレーターの基礎効果時間に足す秒数。[0]=1段目（+5）、[1]=2段目（+10）。基礎値は GoldBoltBuff の「Base Duration 20」")]
    public float[] goldBoltDurationBonus = { 5f, 10f };
    [Tooltip("マルチバウンドの基礎効果時間に足す秒数。[0]=1段目（+5）、[1]=2段目（+10）。基礎値は MultiBoundBuff の「Base Duration 20」")]
    public float[] multiBoundDurationBonus = { 5f, 10f };
    [Tooltip("スパイラルショットの敵へのヒット回数の追加（通常8回に足す）。[0]=1段目（+4＝12回）、[1]=2段目（+8＝16回＝強化ドリルと同じ）")]
    public int[] spiralShotHitBonus = { 4, 8 };
    [Tooltip("ブラストショットの爆発ダメージ。[0]=1段目、[1]=2段目（2段目は1段目と同じダメージで範囲が広がる）。受け取っていない時は ShotItemManager の Blast Damage（10）")]
    public int[] blastShotDamage = { 20, 20 };
    [Tooltip("ブラストショットの爆発の範囲（半径）。[0]=1段目（範囲はそのまま＝0で変えない）、[1]=2段目（4）")]
    public float[] blastShotRadius = { 0f, 4f };
    [Tooltip("スプリットショットのダメージ倍率。[0]=1段目、[1]=2段目（2段目もダメージアップは続く）")]
    public float[] splitShotDamageMul = { 1.5f, 1.5f };
    [Tooltip("スプリットショットの分かれる数の追加。[0]=1段目（0＝増えない）、[1]=2段目（+1）")]
    public int[] splitShotExtraSplits = { 0, 1 };

    [Header("ステージクリア時の回復の演出")]
    [Tooltip("Stageクリアから回復（演出）までの待ち時間（秒・実時間）")]
    public float stageClearHealDelay = 0.3f;
    // 「+N」表示の位置は BlockItemManager の「Dancer/Floor Heal Popup Offset」（セルフヒール・ライフアイテムと共通）

    /// <summary>tier（1または2）に対応する値。tierが0なら0</summary>
    public static int Pick(int[] arr, int tier) => (tier <= 0 || arr == null || arr.Length == 0) ? 0 : arr[Mathf.Clamp(tier - 1, 0, arr.Length - 1)];
    public static float Pick(float[] arr, int tier) => (tier <= 0 || arr == null || arr.Length == 0) ? 0f : arr[Mathf.Clamp(tier - 1, 0, arr.Length - 1)];
}

/// <summary>
/// ミッション89個の一覧。Assets/Resources/GameData/MissionCatalog.asset に置く。
/// 作成は Tools > ミッション > 「1. ミッション一覧を作成（89個）」。回数・金額はInspectorで変えられる。
/// </summary>
[CreateAssetMenu(menuName = "Test Project/Mission Catalog", fileName = "MissionCatalog")]
public class MissionCatalog : ScriptableObject
{
    public const string ResourcesPath = "GameData/MissionCatalog";

    public List<MissionDefinition> missions = new List<MissionDefinition>();

    [Header("パッシブの効果の数値（1段目／2段目。2段目は1段目と置き換え）")]
    public PassiveValues passiveValues = new PassiveValues();

    public MissionDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < missions.Count; i++)
            if (missions[i] != null && missions[i].id == id) return missions[i];
        return null;
    }
}
