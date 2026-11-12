// ミッション機能で使う種類の定義（2026/11/07）

/// <summary>エネミーの大きさの区分（ミッションの数え分けとパッシブのダメージアップに使う）。
/// Area1〜9のStage1に配置＝Small、Stage2に配置＝Medium、Stage3＝Boss（NeonDancer・IronNestのNM01〜03もBoss）。
/// Noneは区分なし（Tutorial専用・ボスが呼び出すドローン等）で、区分別のミッションには数えない。</summary>
public enum EnemySizeClass
{
    None = 0,
    Small = 1,
    Medium = 2,
    Boss = 3,
}

/// <summary>ミッションの進捗として数える項目。★セーブには名前（文字列）で保存するので、名前を変えると進捗が消える。末尾に追加すること</summary>
public enum MissionStat
{
    ReflectWhite,             // 白線で弾を反射した回数
    ReflectRed,               // 赤線で弾を反射した回数
    GaugeFullWhite,           // 白線のゲージが満タンまで戻った回数
    GaugeFullRed,             // 赤線のゲージが満タンまで戻った回数
    MultiReflectWhite,        // 白線1本で弾を2個以上反射した回数（1本につき最大1回）
    MultiReflectRed,          // 赤線1本で弾を2個以上反射した回数（1本につき最大1回）
    LinesDrawn,               // 線を引いた本数
    Kills,                    // 撃破数（Result画面のKILLSと同じ数え方）
    BlocksDestroyed,          // ブロックを壊した数
    SlowMotionCircles,        // スローモーション中に円を作った回数
    ItemsCollected,           // アイテムを取った数
    SoulsRescued,             // 魂を救出した数
    KillsSmall,               // 小型エネミーの撃破数
    KillsMedium,              // 中型エネミーの撃破数
    KillsBoss,                // ボスの撃破数
    ShieldBreaksSmall,        // 小型エネミーのシールドを壊した回数
    ShieldBreaksMedium,       // 中型エネミーのシールドを壊した回数
    ShieldBreaksBoss,         // ボスのシールドを壊した回数
    StageClearNoWhiteOverheat,// 白線を1回もオーバーヒートさせずにStageをクリアした回数
    StageClearNoRedOverheat,  // 赤線を1回もオーバーヒートさせずにStageをクリアした回数
    SlowMotionUses,           // スローモーションを使った回数
    GoldEarned,               // 獲得したゴールド
    SkillsSelected,           // スキル3択でスキルを選んだ回数
    StageClearNoDancerHit,    // ダンサーが1回も被弾せずにStageをクリアした回数
    JustReflects,             // Just反射の回数
    CircleEnclose5Bullets,    // 1つの円で弾を5個以上同時に囲んだ（達成したら1）
    RankS,                    // ランクSを取った（Areaごと。達成したら1）
    Area10Clear,              // Area10をクリアした（達成したら1）
    Area9ClearNoDown,         // ダウンせずにArea9をクリアした（達成したら1）。M50専用（以前からの進捗を残すため）
    AreaClearNoDown,          // ダウンせずにAreaをクリアした（Areaごと。達成したら1）（2026/11/08）
    AreaFirstClear,           // Areaをクリアした（Areaごと。達成したら1）。Area2/5/8の虹の雫のミッション用（2026/11/08）
    GoldenShoesCollected,     // ゴールドシューズを取った回数（2026/11/10）
    GoldBoltCollected,        // アクセラレーターを取った回数（2026/11/10）
    MultiBoundCollected,      // マルチバウンドを取った回数（2026/11/11）
    SpiralShotCollected,      // スパイラルショットを取った回数（2026/11/11）
    BlastShotCollected,       // ブラストショットを取った回数（2026/11/11）
    SplitShotCollected,       // スプリットショットを取った回数（2026/11/11）
    FoodTicketCollected,      // お食事券を取った回数（2026/11/11。円で取って複数枚でも1回）
}

public enum MissionRewardType
{
    Gold = 0,
    Passive = 1,
    RainbowDrop = 2,          // 虹の雫（2026/11/08）
    FoodTicket = 3,           // お食事券（2026/11/11）
    Notice = 4,               // お知らせだけ（次のエリアの解放・ジェムの装備スロット+1。どちらもクリアした時に自動で行われるので、受け取っても何も増えない）（2026/11/11）
}

/// <summary>パッシブの種類。★セーブ・設定アセットに数値で保存されるので、途中に挿入せず末尾に追加すること</summary>
public enum MissionPassiveType
{
    None = 0,
    CategoryASkill = 1,       // CategoryAのスキル（passiveSkillで指定）を1回分、最初から獲得
    AutoItemCollect = 2,      // Stage遷移時に画面に残っているアイテムを自動で取得
    AutoSoulRescue = 3,       // 救出できなかった魂を1プレイにつき1回だけ自動で救出
    DamageSmall = 4,          // 小型エネミーへのダメージ +1/+2
    DamageMedium = 5,         // 中型エネミーへのダメージ +1/+2
    DamageBoss = 6,           // ボスへのダメージ +1/+2
    ShieldDamageSmall = 7,    // 小型エネミーへのシールドダメージ +1/+2
    ShieldDamageMedium = 8,   // 中型エネミーへのシールドダメージ +1/+2
    ShieldDamageBoss = 9,     // ボスへのシールドダメージ +1/+2
    WhiteOverheatShorten = 10,// 白線のオーバーヒート時間 -10%/-20%
    RedOverheatShorten = 11,  // 赤線のオーバーヒート時間 -10%/-20%
    SlowMotionOverheatShorten = 12, // スローモーションゲージのオーバーヒート時間 -10%/-20%
    GoldGainUp = 13,          // Gold獲得量 +10%/+20%
    SkillReroll = 14,         // スキル3択の引き直し 1回/2回
    StageClearHeal = 15,      // ステージクリア時にダンサーとフロアを +1/+2 回復
    GoldenShoesDuration = 16, // ゴールドシューズの効果時間を40秒/50秒にする（2026/11/10）
    GoldBoltDuration = 17,    // アクセラレーターの効果時間を40秒/50秒にする（2026/11/10）
    MultiBoundDuration = 18,  // マルチバウンドの効果時間を40秒/50秒にする（2026/11/11）
    SpiralShotHits = 19,      // スパイラルショットの敵へのヒット回数 +4/+8（2026/11/11）
    BlastShotPower = 20,      // ブラストショット 1段目：ダメージアップ／2段目：爆発範囲アップ（2026/11/11）
    SplitShotPower = 21,      // スプリットショット 1段目：ダメージアップ（×1.5）／2段目：分裂数+1（2026/11/11）
}
