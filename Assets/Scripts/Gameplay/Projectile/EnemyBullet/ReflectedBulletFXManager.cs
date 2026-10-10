using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーが反射した弾（ノーマル反射・ジャスト反射）を派手にする演出の管理役。Assets/Resources/ReflectFX.prefab から1つだけ作られる
/// （プレハブが無ければ何もしない＝従来の見た目。メニュー「Tools/弾の見た目/3 …」で作成）。
/// 弾が線で反射された時（EnemyBulletFeedback.OnPaddleReflect / OnJustReflect）に登録され、この管理役が全弾を1回のループでまとめて更新する。
/// 未反射に戻った・消えた弾は登録を外し、変えた見た目（絵の大きさ・色）を元に戻す（弾の動き・当たり判定には一切触れない）。
/// ・共通：① 反射の瞬間のポップ（膨らんで白く光る） ② 発光コア ③ 弾の色を反射色に ④ 速さの伸び（コアを進む方向へ伸ばす） ⑤ 敵への着弾リング
/// ・ジャストのみ：⑥ 炎のオーラ（火の粉） ⑦ 残像 ⑧ 外側の色付きトレイル ⑨ 電光スパーク ⑩ ジャスト弾で倒した時の放射状の光
/// ・敵ヒット：A1 被弾フラッシュ／A2 十字フレア／A3 方向性のある火花／A4 残光／A6 画面の小さな揺れ（ジャスト）／A7 極小ヒットストップ（ジャスト・初期OFF）
/// ・線で反射：B1 線を走る光／B2 スピードライン／B3 衝撃リング／B4 扇状の火花／B5 ジャストのスターバースト（既存VFX_JustReflectに重ねる）／B6 連続反射の盛り上がり
/// ・線が貫通されて壊れた時：C1 連鎖崩壊／C2 破片／C3 閃光とひび／C4 リング／C5 突き抜け方向への飛散／C6 線の色／C7 消える前の白い光／C8 画面の揺れ（旧JustHitSpark_OrangeStrongの代わり）
/// ・未反射弾と反射弾の衝突：D1 2色の対消滅／D2 閃光とX字フレア／D3 2色のリング／D4 押し合う火花／D5 相殺の電光／D6 消える弾の白い膨らみ／D7 突き抜け演出／D8 連続相殺／D9 揺れ（初期OFF）（旧VFX_BulletDestroyの代わり）
/// ・反射弾がブロックに当たった時：線で反射した時と同じ演出（B2〜B5）を使い回す（旧VFX_EnemyHit_Normalの代わり）
/// ・ビームを線で反射した時：弾の反射と同じ演出（B1〜B6）
/// ・反射弾がエネミーに当たった時は、旧VFX（EnemyHitFeedback／EnemyBulletFeedbackのヒットVFX）を出さずA系・⑤だけにする（replaceOldEnemyHitVfx）
/// ・負荷対策：パーティクルは共有（Emitで出す）、画像・線・トレイルは貸し出しで使い回し、上限を超えた弾は①③だけにする
/// ・ドリル弾（PinnedReflectBullet）とビームは対象外（それぞれ専用の演出がある）
/// </summary>
[DisallowMultipleComponent]
public class ReflectedBulletFXManager : MonoBehaviour
{
    [Header("全体")]
    [Tooltip("OFFにすると全ての演出を止める（従来の見た目）")]
    public bool fxEnabled = true;
    [Tooltip("派手な演出（コア・火の粉・残像・トレイル・スパーク）を付ける反射弾の上限。超えた弾は①ポップと③色だけになる")]
    public int maxFancyBullets = 30;
    [Tooltip("粒（パーティクル）の量を減らし始める反射弾の数")]
    public int particleThinStart = 15;

    [Header("共有のパーティクル・画像（メニューで自動設定）")]
    public ParticleSystem sparkle;
    [Tooltip("重力で落ちる破片（線の破壊用）。メニュー「5」で追加。未設定ならSparkleで代用（落ちない）")]
    public ParticleSystem debris;
    public Sprite glowSprite;
    public Sprite ringSprite;
    [Tooltip("加算の画像用マテリアル（コア・リング・残像）")]
    public Material additiveSpriteMaterial;
    [Tooltip("線（電光・撃破の光・外側トレイル）のマテリアル（加算）")]
    public Material lineMaterial;

    [Header("色")]
    public Color normalColor = new Color(0.35f, 0.95f, 1f, 1f);
    public Color justColor = new Color(1f, 0.5f, 0.12f, 1f);
    [Tooltip("ジャストの2色目（外側トレイル・火の粉の赤）")]
    public Color justColor2 = new Color(1f, 0.15f, 0.08f, 1f);

    [Header("① 反射の瞬間のポップ")]
    public bool popEnabled = true;
    public float popDuration = 0.14f;
    public float popScaleNormal = 1.5f;
    public float popScaleJust = 1.8f;
    [Tooltip("ポップ中に弾を白く光らせる量")]
    [Range(0f, 1f)] public float popWhite = 0.85f;

    [Header("② 発光コア（中心の白い光＋色の光）")]
    public bool coreEnabled = true;
    [Tooltip("コアの大きさ（弾の大きさに対する倍率）")]
    public float coreSizeNormal = 2.0f;
    public float coreSizeJust = 2.6f;
    [Tooltip("コアの大きさの上限（ワールド単位）。画像の大きい弾（Condorの羽根など）で巨大にならないように")]
    public float coreMaxSize = 1.1f;
    [Range(0f, 1f)] public float coreAlpha = 0.75f;
    [Tooltip("中心の白さ（0＝色のまま、1＝真っ白）")]
    [Range(0f, 1f)] public float coreWhite = 0.35f;
    public float corePulseSpeed = 14f;
    [Range(0f, 1f)] public float corePulseAmount = 0.2f;

    [Header("③ 弾の色を反射色に")]
    public bool tintEnabled = true;
    [Tooltip("弾の絵を反射色へどれだけ寄せるか（0＝元の色、1＝反射色そのまま）")]
    [Range(0f, 1f)] public float tintAmount = 0.75f;

    [Header("④ 速さの伸び（コアを進む方向へ伸ばす）")]
    public bool stretchEnabled = true;
    public float stretchPerSpeed = 0.07f;
    public float stretchMax = 2.4f;

    [Header("⑤ 敵への着弾リング")]
    public bool hitRingEnabled = true;
    public float hitRingSizeNormal = 1.0f;
    public float hitRingSizeJust = 1.7f;
    public float hitRingDuration = 0.25f;
    public int hitSparksNormal = 4;
    public int hitSparksJust = 10;

    [Header("⑥ 炎のオーラ（ジャストのみ）")]
    public bool emberEnabled = true;
    [Tooltip("火の粉の量（個/秒）")]
    public float emberRate = 40f;
    public float emberSpeed = 1.2f;

    [Header("⑦ 残像（ジャストのみ）")]
    public bool ghostEnabled = true;
    [Range(0, 6)] public int ghostCount = 3;
    public float ghostDelay = 0.035f;
    [Range(0f, 1f)] public float ghostAlpha = 0.45f;

    [Header("⑧ 外側の色付きトレイル（ジャストのみ）")]
    public bool outerTrailEnabled = true;
    public float outerTrailTime = 0.3f;
    public float outerTrailWidth = 0.45f;
    [Range(0f, 1f)] public float outerTrailAlpha = 0.45f;
    [Tooltip("帯の太さの上限（ワールド単位）。画像の大きい弾で太くなりすぎないように")]
    public float outerTrailMaxWidth = 0.5f;
    [Tooltip("先端から最大の太さになるまでの割合（0〜1）。先端を細く尖らせて、平らな切り口が見えないようにする")]
    [Range(0.01f, 0.5f)] public float outerTrailHeadTaper = 0.15f;

    [Header("⑨ 電光スパーク（ジャストのみ）")]
    public bool lightningEnabled = true;
    public float lightningIntervalMin = 0.12f;
    public float lightningIntervalMax = 0.3f;
    public float lightningDuration = 0.07f;
    [Tooltip("稲妻の長さ（弾の大きさに対する倍率）")]
    public float lightningLengthMul = 3f;
    public float lightningWidth = 0.05f;
    public Color lightningColor = new Color(1f, 0.92f, 0.6f, 1f);

    [Header("A 敵ヒット：共通")]
    [Tooltip("1フレームに出す敵ヒット演出の上限（同時に多数当たった時の重なり防止）")]
    public int maxHitFxPerFrame = 3;
    [Tooltip("同時に動いている単発演出（リング・光の筋など）の上限。超えた分は出さない")]
    public int maxActiveAnims = 60;

    [Tooltip("ONなら、反射弾がエネミーに当たった時の旧VFX（EnemyHitFeedback > Hit Vfx / Powered Hit Vfx、EnemyBulletFeedback > Enemy Hit Vfx / Just Powered Vfx）を出さず、この新しい演出だけにする。OFFで旧VFXも重ねて出す")]
    public bool replaceOldEnemyHitVfx = true;

    [Header("A1 被弾フラッシュ（当たったエネミーの絵が一瞬白く光る）")]
    public bool hitFlashEnabled = true;
    public float hitFlashDuration = 0.07f;
    [Range(0f, 1f)] public float hitFlashAlphaNormal = 0.55f;
    [Range(0f, 1f)] public float hitFlashAlphaJust = 0.85f;
    [Tooltip("同じ絵を続けて光らせる最短間隔（秒）")]
    public float hitFlashMinInterval = 0.06f;
    [Tooltip("当たったエネミーを探す半径（弾の位置から）")]
    public float hitFlashSearchRadius = 0.5f;

    [Header("A2 十字フレア")]
    public bool crossFlareEnabled = true;
    public float crossFlareLengthNormal = 0.9f;
    public float crossFlareLengthJust = 1.6f;
    [Tooltip("光の筋の太さ（長さに対する割合）")]
    [Range(0.02f, 0.5f)] public float crossFlareThickness = 0.09f;
    public float crossFlareDuration = 0.13f;

    [Header("A3 方向性のある火花（弾が飛んできた向きの延長へ扇状に）")]
    public bool directionalSparksEnabled = true;
    public int directionalSparksNormal = 5;
    public int directionalSparksJust = 10;
    public float directionalSparkSpeed = 5f;
    [Range(0f, 90f)] public float directionalSparkSpread = 35f;

    [Header("A4 残光（着弾点に光が少し残る）")]
    public bool afterglowEnabled = true;
    public float afterglowSizeNormal = 0.5f;
    public float afterglowSizeJust = 0.8f;
    public float afterglowDuration = 0.35f;
    [Range(0f, 1f)] public float afterglowAlpha = 0.6f;

    [Header("A6 画面の小さな揺れ（ジャストのみ）")]
    public bool justHitShakeEnabled = true;
    public float justHitShakeDuration = 0.07f;
    public float justHitShakeMagnitude = 0.04f;
    [Tooltip("揺れの最短間隔（秒）。連続ヒットで揺れ続けないように")]
    public float justHitShakeMinInterval = 0.25f;

    [Header("A7 極小ヒットストップ（ジャストのみ・初期OFF）")]
    [Tooltip("操作感が変わるため初期はOFF。撃破のヒットストップ中は出さない")]
    public bool justHitStopEnabled = false;
    public float justHitStopSeconds = 0.025f;
    public float justHitStopMinInterval = 0.3f;

    [Header("B 線で反射：共通")]
    [Tooltip("1フレームに出す反射演出の上限（まとめて反射した時の重なり防止）")]
    public int maxReflectFxPerFrame = 3;
    [Tooltip("赤線（加速線）で反射した時の色。白線はノーマルの色、ジャストはジャストの色")]
    public Color redLineColor = new Color(1f, 0.3f, 0.25f, 1f);

    [Tooltip("ビーム（Dragon・Obelisk・Bit・NeonDancer）を線で反射した時も、弾の反射と同じ演出（B1〜B6）を出す。旧VFX（反射VFX・ビームの反射ヒットVFX）は出さない")]
    public bool beamReflectFxEnabled = true;
    [Tooltip("ONなら、弾を線で反射した時の旧VFX（反射ヒットVFX・VFX_NormalReflect / VFX_JustReflect）を出さず、新しい演出（B1〜B6）だけにする")]
    public bool replaceOldBulletReflectVfx = true;
    [Tooltip("ドリル弾（Tsukuyomi・NeonDancer）を線で反射した時も、弾の反射と同じ演出（B1〜B6）を出す。旧VFX（反射VFX・反射ヒットVFX）は出さない")]
    public bool drillReflectFxEnabled = true;
    [Header("F 反射したドリルがエネミーに刺さっている間のヒット")]
    [Tooltip("ヒットの演出の基本の大きさ（反射弾の敵ヒット演出に対する倍率）")]
    public float drillHitBaseScale = 1.4f;
    [Tooltip("ヒットのたびに大きくする割合（1回目＝1.0、2回目＝1＋この値…）")]
    public float drillHitGrowStep = 0.1f;
    [Tooltip("上の大きさの上限（倍率）")]
    public float drillHitGrowMax = 2f;
    [Tooltip("演出の並び順（Defaultレイヤー）。ドリル自身の演出より手前に出す")]
    public int drillHitSortingOrder = 1060;
    [Tooltip("ヒットのたびに【追加で】鳴らすSE（ノーマル）。空なら鳴らさない。※エネミー本体の被弾SE（EnemyDamageReceiverのNormal/Just Hit Clips）は、これとは別にヒットごとに鳴っている")]
    public AudioClip drillHitSe;
    [Tooltip("ヒットのたびに【追加で】鳴らすSE（ジャスト）。空ならノーマル側、それも空なら鳴らさない")]
    public AudioClip drillHitJustSe;
    [Range(0f, 1f)] public float drillHitSeVolume = 0.8f;

    [Header("E 反射したビームの見た目（弾の反射と同じ）")]
    [Tooltip("反射後のビームを、弾の反射と同じ配色（ノーマル＝白〜シアン、ジャスト＝白〜オレンジ〜赤）にして、光の粒・火の粉・稲妻・先端の発光を付ける")]
    public bool beamReflectLookEnabled = true;
    [Tooltip("光の粒の量（個/秒、反射後の区間全体で）")]
    public float beamSparkleRate = 40f;
    [Tooltip("ジャスト：火の粉の量（個/秒）")]
    public float beamEmberRate = 45f;
    [Tooltip("ジャスト：稲妻が走る間隔（秒）")]
    public float beamBoltIntervalMin = 0.1f;
    public float beamBoltIntervalMax = 0.25f;
    public float beamBoltLength = 0.7f;
    [Tooltip("ジャスト：先端の発光の大きさ")]
    public float beamTipGlowSize = 1.0f;
    [Tooltip("反射した瞬間に光る長さ（反射した向きへ）")]
    public float beamPopLength = 2.5f;
    [Tooltip("反射した瞬間に光る太さ（長さに対する割合）")]
    [Range(0.02f, 0.5f)] public float beamPopThickness = 0.14f;
    public float beamPopDuration = 0.14f;

    [Tooltip("ビームの線反射・ビームのエネミーヒットの演出の並び順（Defaultレイヤー）。ビーム本体（8〜14）や反射フラッシュ（1050）より手前に出す")]
    public int beamFxSortingOrder = 1060;
    [Tooltip("ビームが線に当たり続けている間、ヒットのたびに線反射の演出を大きくする割合（1回目＝1.0、2回目＝1＋この値、3回目＝1＋2倍…）")]
    public float beamTickGrowStep = 0.1f;
    [Tooltip("上の大きさの上限（倍率）")]
    public float beamTickGrowMax = 2f;
    [Tooltip("反射したビーム・ドリルがエネミーに当たった時も、反射弾と同じ敵ヒット演出（A系・着弾リング）を出す。旧VFXは出さない")]
    public bool beamDrillEnemyHitFxEnabled = true;
    [Tooltip("反射弾がブロック（WallHealth）に当たった時も、線で反射した時と同じ演出（B2〜B5）を出す。旧VFX（WallHealth > Hit Vfx Prefab）は出さない。OFFで旧VFXに戻る")]
    public bool blockHitFxEnabled = true;
    [Tooltip("同じ弾がブロックに当たった時に演出を出す最短間隔（秒）")]
    public float blockHitMinInterval = 0.08f;

    [Header("B1 線を走る光")]
    public bool lineRunEnabled = true;
    public float lineRunLength = 1.4f;
    public float lineRunDuration = 0.2f;
    public float lineRunWidth = 0.09f;
    [Tooltip("線の向きを測る点の範囲（反射点の前後何個の点で測るか）")]
    public int lineRunSampleRange = 3;

    [Header("B2 反射方向のスピードライン")]
    public bool speedLinesEnabled = true;
    public int speedLineCount = 3;
    public float speedLineLength = 1.2f;
    public float speedLineWidth = 0.06f;
    public float speedLineDuration = 0.18f;
    [Range(0f, 45f)] public float speedLineSpread = 14f;

    [Header("B3 衝撃リング")]
    public bool reflectRingEnabled = true;
    public float reflectRingSizeNormal = 0.9f;
    public float reflectRingSizeJust = 1.4f;
    public float reflectRingDuration = 0.2f;

    [Header("B4 扇状の火花（新しい進行方向側へ）")]
    public bool reflectSparksEnabled = true;
    public int reflectSparksNormal = 5;
    public int reflectSparksJust = 9;
    public float reflectSparkSpeed = 4f;
    [Range(0f, 90f)] public float reflectSparkSpread = 45f;

    [Header("B5 ジャストのスターバースト（既存のVFX_JustReflectに重ねる）")]
    public bool justStarEnabled = true;
    public int justStarRays = 6;
    public float justStarLength = 1.8f;
    [Range(0.02f, 0.5f)] public float justStarThickness = 0.08f;
    public float justStarDuration = 0.18f;
    public int justStarRingParticles = 14;
    public float justStarParticleSpeed = 3.5f;

    [Header("B6 連続反射の盛り上がり（続けて反射するほどB3・B5が大きく明るく）")]
    public bool comboEnabled = true;
    [Tooltip("この秒数以内に次の反射があれば連続とみなす")]
    public float comboWindow = 0.6f;
    [Tooltip("1回ごとに大きくなる割合")]
    public float comboStep = 0.12f;
    [Tooltip("大きくなる回数の上限")]
    public int comboMaxSteps = 5;

    [Header("C 線が貫通されて壊れた時（旧JustHitSpark_OrangeStrongの代わりに出す）")]
    [Tooltip("ONで新しい演出を出し、旧VFX（PaddleDrawer > Line Break White/Red Vfx Prefab）は出さない。OFFで旧VFXに戻る")]
    public bool lineBreakFxEnabled = true;
    [Tooltip("C6 線の色に合わせる：ONなら壊れた線の点の実際の色を使う。OFF・取れない時は下の色（白線＝ノーマルの色、赤線＝赤線の色）")]
    public bool lineBreakUseDotColor = true;
    [Tooltip("1フレームに出す線の破壊演出の上限")]
    public int maxLineBreaksPerFrame = 2;

    [Header("C1 連鎖崩壊 ／ C7 消える前の白い光")]
    public bool lineCollapseEnabled = true;
    [Tooltip("白く光っている時間（秒）。0で光らない")]
    public float lineFlashSeconds = 0.06f;
    [Tooltip("貫通点から両端まで砕け終わる時間（秒）")]
    public float lineCollapseSeconds = 0.25f;
    [Tooltip("崩れる線の太さ（点の大きさに対する倍率）")]
    public float lineCollapseWidthMul = 1.0f;

    [Header("C2 線の破片（線に沿ってばらけて落ちる）")]
    public bool lineDebrisEnabled = true;
    [Tooltip("1本の線から出す破片の上限")]
    public int lineDebrisMax = 40;
    public float lineDebrisSize = 0.07f;
    public float lineDebrisSpeed = 1.2f;

    [Header("C3 貫通点の閃光とひび")]
    public bool lineCrackEnabled = true;
    public float lineCrackFlashSize = 1.1f;
    public float lineCrackFlashDuration = 0.1f;
    public int lineCrackCount = 4;
    public float lineCrackLength = 0.7f;
    public float lineCrackWidth = 0.045f;
    public float lineCrackDuration = 0.14f;

    [Header("C4 衝撃リング")]
    public bool lineBreakRingEnabled = true;
    public float lineBreakRingSize = 1.3f;
    public float lineBreakRingDuration = 0.22f;

    [Header("C5 突き抜け方向への飛散")]
    public bool lineBreakSprayEnabled = true;
    public int lineBreakSprayCount = 12;
    public float lineBreakSpraySpeed = 5f;
    [Range(0f, 90f)] public float lineBreakSpraySpread = 30f;

    [Header("C8 画面の小さな揺れ")]
    public bool lineBreakShakeEnabled = true;
    public float lineBreakShakeDuration = 0.1f;
    public float lineBreakShakeMagnitude = 0.05f;
    public float lineBreakShakeMinInterval = 0.4f;

    [Header("D 未反射弾と反射弾の衝突（旧VFX_BulletDestroyの代わりに出す。他の消え方は今のまま）")]
    [Tooltip("ONで新しい演出を出し、弾同士の衝突では旧VFX（EnemyBulletFeedback > Disappear Vfx Prefab）を出さない。OFFで旧VFXに戻る")]
    public bool clashFxEnabled = true;
    [Tooltip("1フレームに出す衝突演出の上限（弾が多い時の重なり防止）")]
    public int maxClashesPerFrame = 3;
    [Tooltip("未反射弾の色が取れない時の色（敵の弾の色）")]
    public Color clashEnemyColor = new Color(0.8f, 0.12f, 0.18f, 1f);
    [Tooltip("敵の弾の色をどれだけ明るくするか（未反射弾のオーラは暗いため）")]
    [Range(0f, 1f)] public float clashEnemyBrighten = 0.25f;

    [Header("D1 中間点で2色の対消滅")]
    public bool clashAnnihilateEnabled = true;
    public float clashAnnihilateSize = 0.9f;
    public float clashAnnihilateDuration = 0.18f;

    [Header("D2 閃光とX字フレア")]
    public bool clashFlareEnabled = true;
    public float clashFlashSize = 0.8f;
    public float clashFlareLength = 1.2f;
    [Range(0.02f, 0.5f)] public float clashFlareThickness = 0.08f;
    public float clashFlareDuration = 0.12f;

    [Header("D3 2色の衝撃リング")]
    public bool clashRingEnabled = true;
    public float clashRingSize = 1.1f;
    public float clashRingDuration = 0.22f;

    [Header("D4 押し合う火花（それぞれの弾の進む向きへ、その弾の色で）")]
    public bool clashSparksEnabled = true;
    public int clashSparkCount = 5;
    public float clashSparkSpeed = 3.5f;
    [Range(0f, 90f)] public float clashSparkSpread = 40f;

    [Header("D5 相殺の電光")]
    public bool clashBoltEnabled = true;
    public float clashBoltDuration = 0.07f;
    public float clashBoltWidth = 0.045f;

    [Header("D6 消える弾の白い膨らみ")]
    public bool clashPuffEnabled = true;
    public float clashPuffScale = 1.8f;
    public float clashPuffDuration = 0.12f;
    [Range(0f, 1f)] public float clashPuffAlpha = 0.8f;

    [Header("D7 ジャスト／ドリルの突き抜け演出")]
    public bool clashPierceEnabled = true;
    public int clashPierceDebris = 10;
    public float clashPierceSpeed = 5f;
    [Range(0f, 90f)] public float clashPierceSpread = 22f;

    [Header("D8 連続相殺の盛り上がり（続けて相殺するほどD1〜D3が大きく明るく）")]
    public bool clashComboEnabled = true;
    public float clashComboWindow = 0.5f;
    public float clashComboStep = 0.1f;
    public int clashComboMaxSteps = 5;

    [Header("D9 画面の小さな揺れ（初期OFF。衝突は頻度が高いため）")]
    public bool clashShakeEnabled = false;
    public float clashShakeDuration = 0.06f;
    public float clashShakeMagnitude = 0.03f;
    public float clashShakeMinInterval = 0.3f;

    [Header("⑩ ジャスト弾で倒した時の放射状の光")]
    public bool killBurstEnabled = true;
    public int killRayCount = 10;
    public float killRayLength = 2.4f;
    public float killRayWidth = 0.14f;
    public float killBurstDuration = 0.35f;
    public float killRingSize = 2.6f;
    public int killSparks = 18;

    // ---------- 共有インスタンス ----------
    private static ReflectedBulletFXManager s_instance;
    private static bool s_triedLoad;

    /// <summary>今ある管理役（無ければnull。読み込みを起こさない。弾が消える時・敵が倒れた時に使う）</summary>
    public static ReflectedBulletFXManager Existing => s_instance;

    public static ReflectedBulletFXManager Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            if (s_triedLoad) return null;
            s_triedLoad = true;
            var prefab = Resources.Load<ReflectedBulletFXManager>("ReflectFX");
            if (prefab == null) return null;
            s_instance = Instantiate(prefab);
            s_instance.name = "ReflectFX";
            return s_instance;
        }
    }

    // ---------- 外から呼ぶ入口 ----------
    /// <summary>線で反射された時（EnemyBulletFeedback.OnPaddleReflect）</summary>
    public static void NotifyReflect(EnemyBullet b)
    {
        if (b == null) return;
        var m = Instance;
        if (m == null || !m.fxEnabled) return;
        m.OnReflect(b, false);
    }

    /// <summary>ジャスト反射が成立した時（EnemyBulletFeedback.OnJustReflect）</summary>
    public static void NotifyJust(EnemyBullet b)
    {
        if (b == null) return;
        var m = Instance;
        if (m == null || !m.fxEnabled) return;
        m.OnReflect(b, true);
    }

    /// <summary>反射弾が敵に当たった時（EnemyBulletFeedback.OnEnemyHit）</summary>
    /// <returns>新しい演出を出し、旧VFXを止める設定ならtrue（呼び出し側は旧VFXを出さない）</returns>
    public static bool NotifyEnemyHit(EnemyBullet b, Vector3 pos, bool powered)
    {
        var m = Instance;
        if (m == null || !m.fxEnabled || b == null) return false;
        if (m.byBullet.TryGetValue(b, out Entry e)) m.OnEnemyHit(e, pos, powered);
        else
        {
            // 登録されていない反射弾（ドリル弾など）：弾の向き・並び順だけ読んで同じ演出を出す
            if (!(b.IsReflected || b.HasPaddleReflectedOnce)) return false;
            if (b.CachedPinnedReflect != null && !m.beamDrillEnemyHitFxEnabled) return false;
            var rb = b.GetComponent<Rigidbody2D>();
            Vector2 dir = rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f ? rb.linearVelocity.normalized : Vector2.up;
            m.GetSorting(b, out int layer, out int order);
            m.PlayEnemyHit(pos, powered, dir, layer, order, true);
        }
        if (!m.replaceOldEnemyHitVfx) return false;
        s_enemyHitHandledFrame = Time.frameCount;
        return true;
    }

    /// <summary>
    /// 反射したビーム・ドリルがエネミーにダメージを与える直前に呼ぶ。新しい演出を出す設定ならtrueを返し、
    /// このフレームの旧VFX（EnemyHitFeedbackのHit Vfx）を止める。trueの時はダメージが入った後に PlayExternalEnemyHit を呼ぶ
    /// </summary>
    public static bool BeginExternalEnemyHit()
    {
        var m = Instance;
        if (m == null || !m.fxEnabled || !m.beamDrillEnemyHitFxEnabled) return false;
        if (m.replaceOldEnemyHitVfx) s_enemyHitHandledFrame = Time.frameCount;
        return true;
    }

    /// <summary>反射したビーム・ドリルがエネミーに当たった時の演出（A系・着弾リング）。連続ヒットなので画面の揺れ・ヒットストップは出さない</summary>
    public static void PlayExternalEnemyHit(Vector3 pos, bool just, Vector2 dir, int layer, int order)
    {
        var m = Existing;
        if (m == null || !m.fxEnabled || !m.beamDrillEnemyHitFxEnabled) return;
        m.PlayEnemyHit(pos, just, dir, layer, order, false);
    }

    /// <summary>ビームの演出の並び順（ビーム本体より手前）</summary>
    public static int BeamFxSortingOrder { get { var m = Instance; return m != null ? m.beamFxSortingOrder : 1060; } }

    /// <summary>反射後のビームを弾の反射と同じ見た目にする設定か</summary>
    public static bool HandlesBeamReflectLook { get { var m = Instance; return m != null && m.fxEnabled && m.beamReflectLookEnabled; } }

    /// <summary>反射後のビームの色（ノーマル＝白〜シアン、ジャスト＝白〜オレンジ〜赤）。設定がOFFならfalse</summary>
    public static bool TryGetBeamReflectColors(bool just, out Color a, out Color b)
    {
        a = b = Color.white;
        if (!HandlesBeamReflectLook) return false;
        var m = s_instance;
        if (just) { a = Color.Lerp(m.justColor, Color.white, 0.35f); b = m.justColor2; }
        else { a = Color.Lerp(m.normalColor, Color.white, 0.55f); b = m.normalColor; }
        a.a = b.a = 1f;
        return true;
    }

    /// <summary>ビームが毎フレーム呼ぶ（EnemyBeamBullet.LateUpdate）。反射後の区間に沿って粒・火の粉・稲妻・先端の発光を出す</summary>
    public static void TickBeam(EnemyBeamBullet beam)
    {
        if (beam == null || !HandlesBeamReflectLook) return;
        s_instance.OnBeamTick(beam);
    }

    /// <summary>
    /// 反射したドリルがエネミーに刺さっている間のヒット（PinnedReflectBullet）。hitIndex＝刺さってからのヒット回数（1,2,3…）。
    /// ヒットのたびに演出を少しずつ大きくし、SEも鳴らす（ビームの当たり続けと同じ考え方）
    /// </summary>
    public static void PlayDrillEnemyHit(Vector3 pos, bool just, Vector2 dir, int hitIndex)
    {
        var m = Existing;
        if (m == null || !m.fxEnabled || !m.beamDrillEnemyHitFxEnabled) return;
        float grow = Mathf.Min(1f + Mathf.Max(0, hitIndex - 1) * m.drillHitGrowStep, Mathf.Max(1f, m.drillHitGrowMax));
        m.PlayEnemyHit(pos, just, dir, SortingLayer.NameToID("Default"), m.drillHitSortingOrder, false, m.drillHitBaseScale * grow);
        // SE：エネミー本体の被弾SE（EnemyDamageReceiver）がヒットごとに既に鳴っているので、ここでは専用SEが設定されている時だけ追加で鳴らす
        // （以前は空の時に線の反射SEを鳴らしていたが、被弾SEと2重に鳴っていたため削除）
        AudioClip clip = just && m.drillHitJustSe != null ? m.drillHitJustSe : m.drillHitSe;
        if (clip != null)
        {
            float vol = m.drillHitSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioOneShotPool.Play(clip, vol, pos, null, 0.05f);
        }
    }

    /// <summary>ビームの線反射で新しい演出を出す設定か（呼び出し側が旧VFXを出すか決める）</summary>
    public static bool HandlesBeamReflect { get { var m = Instance; return m != null && m.fxEnabled && m.beamReflectFxEnabled; } }

    /// <summary>弾（ドリル以外）の線反射で旧VFXを止める設定か</summary>
    public static bool HandlesBulletReflect { get { var m = Instance; return m != null && m.fxEnabled && m.replaceOldBulletReflectVfx; } }

    /// <summary>ドリル弾の線反射で新しい演出を出す設定か（呼び出し側が旧VFXを出すか決める）</summary>
    public static bool HandlesDrillReflect { get { var m = Instance; return m != null && m.fxEnabled && m.drillReflectFxEnabled; } }

    /// <summary>ドリル弾が線で反射した時（PaddleDot.PerformPinnedReflect）。newDir＝反射後の向き。出したらtrue（旧VFXは出さない）</summary>
    public static bool NotifyDrillReflect(EnemyBullet b, Vector3 point, PaddleDot.LineType lineType, bool isJust, Stroke stroke, PaddleDot dot, Vector2 newDir)
    {
        if (b == null || !HandlesDrillReflect) return false;
        var m = s_instance;
        m.GetSorting(b, out int layer, out int order);
        m.PlayLineReflect(b, point, lineType, isJust, stroke, dot, layer, order, newDir);
        return true;
    }

    private static int s_enemyHitHandledFrame = -1;
    /// <summary>このフレームに反射弾の敵ヒットを新しい演出で出したか（EnemyHitFeedbackが旧VFXを出すか決めるのに使う）</summary>
    public static bool EnemyHitHandledThisFrame => s_enemyHitHandledFrame == Time.frameCount;

    /// <summary>
    /// ビームが線で反射した時（EnemyBeamBullet）。弾の反射と同じ演出（B1〜B6）を出す。
    /// newDir＝反射後の向き。旧VFX（VFX_NormalReflect / VFX_JustReflect・反射ヒットVFX）は弾の反射と同じく残る
    /// </summary>
    public static void NotifyBeamReflect(Vector3 point, PaddleDot dot, bool isJust, Vector2 newDir)
    {
        var m = Instance;
        if (m == null || !m.fxEnabled || !m.beamReflectFxEnabled || dot == null) return;
        m.OnBeamReflect(point, dot, isJust, newDir);
    }

    /// <summary>弾が線で反射された時（PaddleDot。ドリル・ビーム以外）。B1〜B6を出す</summary>
    public static void NotifyLineReflect(EnemyBullet b, Vector3 point, PaddleDot.LineType lineType, bool isJust, Stroke stroke, PaddleDot dot)
    {
        if (b == null) return;
        var m = Instance;
        if (m == null || !m.fxEnabled) return;
        if (b.GetComponent<PinnedReflectBullet>() != null) return; // ドリル弾は専用の演出（DrillFX）
        m.OnLineReflect(b, point, lineType, isJust, stroke, dot);
    }

    /// <summary>新しい線の破壊演出を出す設定か（PaddleDrawerが旧VFXを出すか決めるのに使う）</summary>
    public static bool HandlesLineBreak
    {
        get { var m = Instance; return m != null && m.fxEnabled && m.lineBreakFxEnabled; }
    }

    /// <summary>線が貫通されて壊れる直前（PaddleDot。弾・ビームの貫通）。throughDir＝突き抜けた向き</summary>
    public static void NotifyLineBreak(Stroke stroke, PaddleDot hitDot, PaddleDot.LineType lineType, Vector3 point, Vector2 throughDir)
    {
        if (!HandlesLineBreak) return;
        s_instance.OnLineBreak(stroke, hitDot, lineType, point, throughDir);
    }

    /// <summary>
    /// 反射弾と未反射弾がぶつかった時（EnemyBullet.TryHandleBulletVsBulletDisappear）。pierced＝反射弾が突き抜けた（ジャスト＋C2・ドリル）。
    /// 演出を出す設定ならtrue（呼び出し側は旧VFXを出さない）
    /// </summary>
    public static bool NotifyBulletClash(EnemyBullet reflected, EnemyBullet unreflected, bool pierced)
    {
        if (reflected == null || unreflected == null) return false;
        var m = Instance;
        if (m == null || !m.fxEnabled || !m.clashFxEnabled) return false;
        m.OnBulletClash(reflected, unreflected, pierced);
        return true;
    }

    /// <summary>反射弾がブロックに当たった時に新しい演出を出す設定か（呼び出し側が旧VFXを出すか決めるのに使う）</summary>
    public static bool HandlesBlockHit
    {
        get { var m = Instance; return m != null && m.fxEnabled && m.blockHitFxEnabled; }
    }

    /// <summary>反射弾がブロック（WallHealth）に当たった瞬間（WallHealth.PlayHit）。線で反射した時と同じ演出（B2〜B5）を出す</summary>
    public static void NotifyBlockHit(EnemyBullet b, Vector3 point)
    {
        if (b == null || !HandlesBlockHit) return;
        s_instance.OnBlockHit(b, point);
    }

    /// <summary>未反射に戻った時（EnemyBulletFeedback.RevertToUnreflectedVisual）</summary>
    public static void NotifyReverted(EnemyBullet b)
    {
        var m = Existing;
        if (m == null || b == null || !m.byBullet.TryGetValue(b, out Entry e)) return;
        m.Remove(e);
    }

    /// <summary>敵がプレイヤーに倒された時（EnemyStats.Die）。同じフレームにジャスト弾が当たっていたら⑩を出す</summary>
    public static void NotifyEnemyKilled()
    {
        var m = Existing;
        if (m == null || !m.fxEnabled || !m.killBurstEnabled) return;
        if (Time.frameCount - m.lastJustHitFrame > 1) return;
        if (m.lastKillBurstFrame == m.lastJustHitFrame) return; // 同じ着弾で2回出さない（連動して倒れるボス等）
        m.lastKillBurstFrame = m.lastJustHitFrame;
        m.SpawnKillBurst(m.lastJustHitPos);
    }

    // ---------- 登録された弾 ----------
    private class Entry
    {
        public EnemyBullet b;
        public Transform tf, visualTf;
        public SpriteRenderer visual;
        public Rigidbody2D rb;
        public bool just, fancy, scaling;
        public Vector3 visualBaseScale;
        public Color visualBaseColor;
        public float age, popAge, emitAcc, seed, nextLightning;
        public SpriteRenderer core, coreWhiteSr;
        public SpriteRenderer[] ghosts;
        public TrailRenderer outerTrail;
        public Vector3[] hist; public float[] histT; public int histHead;
        public Vector2 lastDir;
        public Vector3 lastPos;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<EnemyBullet, Entry> byBullet = new Dictionary<EnemyBullet, Entry>();
    private readonly Stack<SpriteRenderer> freeSprites = new Stack<SpriteRenderer>();
    private readonly Stack<LineRenderer> freeLines = new Stack<LineRenderer>();
    private readonly Stack<TrailRenderer> freeTrails = new Stack<TrailRenderer>();
    private ParticleSystem[] particleList;
    private float lastParticleSpeed = float.NaN;
    private int fancyCount;
    private int lastJustHitFrame = -999, lastKillBurstFrame = -999;
    private Vector3 lastJustHitPos;

    private class Anim
    {
        public SpriteRenderer sr; public LineRenderer lr; public float t, dur, s0, s1, width, aspect = 1f; public Color c; public Vector3 a, b; public bool jagged, run;
        public Vector2 dir = Vector2.right;
        public SpriteRenderer src; public Transform follow; // A1：光らせるエネミーの絵（毎フレーム位置・絵を合わせる）
    }
    private int hitFxFrame = -1, hitFxCount, reflectFxFrame = -1, reflectFxCount;
    private float lastShakeTime = -999f, lastHitStopTime = -999f, lastReflectTime = -999f;
    private int comboCount;
    private readonly Dictionary<SpriteRenderer, float> lastFlashTime = new Dictionary<SpriteRenderer, float>();
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private struct PendingReflect { public EnemyBullet b; public Vector3 p; public Color c; public bool just; public float scale; public int layer, order; public Vector2 fallbackDir; }
    private readonly List<PendingReflect> pendingReflects = new List<PendingReflect>();
    private int clashFrame = -1, clashCount, clashCombo;
    private float lastClashTime = -999f, lastClashShakeTime = -999f;
    private int lineBreakFrame = -1, lineBreakCount;
    private float lastLineBreakShakeTime = -999f;
    private readonly List<PaddleDot> dotBuffer = new List<PaddleDot>();
    private readonly List<Stroke> brokenThisFrame = new List<Stroke>();
    // C1 連鎖崩壊：貫通点から左右に分けた2本の線を、それぞれ端へ向かって消していく
    private class Collapse
    {
        public LineRenderer left, right;
        public Vector3[] pts; public int impact;
        public float t, width; public Color c;
        public int debrisLeft; public float debrisAcc; public int lastL, lastR;
    }
    private readonly List<Collapse> collapses = new List<Collapse>();
    private Vector3[] tmpPts = new Vector3[64];
    private readonly List<Anim> anims = new List<Anim>();
    private static readonly Vector3[] s_boltPts = new Vector3[6];

    private void Awake()
    {
        if (s_instance == null) s_instance = this;
        particleList = new[] { sparkle, debris };
        foreach (var ps in particleList)
        {
            if (ps == null) continue;
            var em = ps.emission; em.enabled = false;
            if (!ps.isPlaying) ps.Play(false);
        }
    }

    private void OnDestroy()
    {
        if (s_instance == this) { s_instance = null; s_triedLoad = false; }
    }

    private void OnReflect(EnemyBullet b, bool just)
    {
        if (!b.gameObject.activeInHierarchy) return;
        if (byBullet.TryGetValue(b, out Entry e))
        {
            // 既に登録済み：ジャストになった時だけ強い見た目へ切り替える（ノーマルの再反射はポップだけ）
            if (just && !e.just) UpgradeToJust(e);
            if (popEnabled) e.popAge = 0f;
            return;
        }
        if (b.GetComponent<PinnedReflectBullet>() != null) return; // ドリル弾は専用の演出（DrillFX）

        // 未反射弾の演出が付いていたら先に外して、絵の大きさを元に戻してもらう
        var unref = BulletFXManager.Existing;
        if (unref != null) unref.OnBulletDisabled(b);

        var tag = b.GetComponent<BulletFXTag>();
        if (tag == null) tag = b.gameObject.AddComponent<BulletFXTag>();
        tag.bullet = b;

        e = new Entry { b = b, tf = b.transform, seed = Random.value * 10f };
        Transform v = b.transform.Find("Visual");
        e.visual = v != null ? v.GetComponent<SpriteRenderer>() : null;
        e.visualTf = e.visual != null ? e.visual.transform : null;
        e.visualBaseScale = e.visualTf != null ? e.visualTf.localScale : Vector3.one;
        e.visualBaseColor = e.visual != null ? e.visual.color : Color.white;
        e.rb = b.GetComponent<Rigidbody2D>();
        e.lastPos = e.tf.position;
        e.popAge = popEnabled ? 0f : 999f;

        e.fancy = fancyCount < maxFancyBullets && e.visual != null;
        if (e.fancy)
        {
            fancyCount++;
            if (coreEnabled)
            {
                int layer = e.visual.sortingLayerID, order = e.visual.sortingOrder;
                e.core = RentSprite(glowSprite, additiveSpriteMaterial, layer, order + 1);
                e.coreWhiteSr = RentSprite(glowSprite, additiveSpriteMaterial, layer, order + 2);
            }
        }
        entries.Add(e);
        byBullet[b] = e;
        if (just || b.DamageMultiplier > 1.0001f) UpgradeToJust(e);
    }

    private void UpgradeToJust(Entry e)
    {
        e.just = true;
        if (popEnabled) e.popAge = 0f;
        if (!e.fancy || e.visual == null) return;
        int layer = e.visual.sortingLayerID, order = e.visual.sortingOrder;
        if (ghostEnabled && ghostCount > 0 && e.ghosts == null)
        {
            e.ghosts = new SpriteRenderer[ghostCount];
            for (int i = 0; i < e.ghosts.Length; i++) { e.ghosts[i] = RentSprite(null, additiveSpriteMaterial, layer, order - 1); e.ghosts[i].enabled = false; }
            e.hist = new Vector3[24]; e.histT = new float[24];
            for (int i = 0; i < e.histT.Length; i++) e.histT[i] = -1f;
        }
        if (outerTrailEnabled && e.outerTrail == null && lineMaterial != null)
        {
            e.outerTrail = RentTrail(layer, order - 2);
            e.outerTrail.transform.position = e.tf.position;
            e.outerTrail.Clear();
            e.outerTrail.emitting = true;
        }
        e.nextLightning = e.age + Random.Range(lightningIntervalMin, lightningIntervalMax);
    }

    /// <summary>弾が消えた時（BulletFXTag.OnDisable）に呼ばれる</summary>
    public void OnBulletDisabled(EnemyBullet b)
    {
        if (b == null || !byBullet.TryGetValue(b, out Entry e)) return;
        Remove(e);
    }

    private void Remove(Entry e)
    {
        if (e.fancy) fancyCount = Mathf.Max(0, fancyCount - 1);
        if (e.visualTf != null && e.scaling) e.visualTf.localScale = e.visualBaseScale;
        if (e.visual != null)
        {
            // 色は元に戻す（透明度はフェード等で変わっている可能性があるのでそのまま）
            Color c = e.visualBaseColor; c.a = e.visual.color.a;
            e.visual.color = c;
        }
        ReleaseSprite(e.core); ReleaseSprite(e.coreWhiteSr);
        if (e.ghosts != null) foreach (var g in e.ghosts) ReleaseSprite(g);
        if (e.outerTrail != null) ReleaseTrail(e.outerTrail);
        e.core = e.coreWhiteSr = null; e.ghosts = null; e.outerTrail = null;
        entries.Remove(e);
        if (e.b != null) byBullet.Remove(e.b);
        else
        {
            var dead = new List<EnemyBullet>();
            foreach (var kv in byBullet) if (kv.Key == null) dead.Add(kv.Key);
            foreach (var k in dead) byBullet.Remove(k);
        }
    }

    // ---------- 毎フレーム ----------
    private void LateUpdate()
    {
        SlowMoTime.ParticleSpeed(particleList, ref lastParticleSpeed);
        float dt = SlowMoTime.DeltaTime;
        float thin = entries.Count <= particleThinStart ? 1f : Mathf.Clamp01(1f - (entries.Count - particleThinStart) / (float)Mathf.Max(1, maxFancyBullets));

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.b == null || !e.b.gameObject.activeInHierarchy || !fxEnabled) { Remove(e); continue; }
            if (e.age > 0.05f && !(e.b.IsReflected || e.b.HasPaddleReflectedOnce)) { Remove(e); continue; } // 未反射に戻った
            UpdateEntry(e, dt, thin);
        }
        ProcessPendingReflects();
        CleanupBeamLooks();
        UpdateAnims(dt);
        UpdateCollapses(dt);
        brokenThisFrame.Clear();
    }

    private void UpdateEntry(Entry e, float dt, float thin)
    {
        e.age += dt;
        e.popAge += dt;
        Vector3 pos = e.tf.position;
        Vector2 vel = e.rb != null && e.rb.simulated ? e.rb.linearVelocity : (Vector2)(pos - e.lastPos) / Mathf.Max(0.0001f, Time.deltaTime);
        float speed = vel.magnitude;
        Vector2 dir = speed > 0.01f ? vel / speed : (e.lastDir.sqrMagnitude > 0f ? e.lastDir : Vector2.up);
        float vAlpha = e.visual != null && e.visual.enabled ? e.visual.color.a : 0f; // 消える時のフェードに合わせる
        bool visible = vAlpha > 0.05f;
        float size = e.visual != null ? Mathf.Max(0.05f, Mathf.Max(e.visualBaseScale.x, e.visualBaseScale.y) * SpriteSize(e.visual) * ParentScale(e.visualTf)) : 0.3f;
        Color col = e.just ? justColor : normalColor;

        // ① ポップ（膨らんで白く光る）／③ 反射色
        float popK = popEnabled && e.popAge < popDuration ? e.popAge / popDuration : 1f;
        // 絵の大きさはポップ中だけ書き換え、終わったら1回だけ元に戻す（他の処理の大きさ変更を邪魔しない）
        if (e.visualTf != null && (popK < 1f || e.scaling))
        {
            float peak = e.just ? popScaleJust : popScaleNormal;
            float s = popK < 1f ? (popK < 0.35f ? Mathf.Lerp(1f, peak, popK / 0.35f) : Mathf.Lerp(peak, 1f, (popK - 0.35f) / 0.65f)) : 1f;
            e.visualTf.localScale = e.visualBaseScale * s;
            e.scaling = popK < 1f;
        }
        if (e.visual != null)
        {
            Color baseC = e.visualBaseColor;
            Color c = tintEnabled ? Color.Lerp(baseC, col, tintAmount) : baseC;
            if (popK < 1f) c = Color.Lerp(c, Color.white, popWhite * (1f - popK));
            c.a = e.visual.color.a;
            e.visual.color = c;
        }

        // ② 発光コア＋④ 速さの伸び
        if (e.core != null)
        {
            e.core.enabled = e.coreWhiteSr.enabled = visible;
            if (visible)
            {
                float pulse = 1f + corePulseAmount * Mathf.Sin(e.age * corePulseSpeed + e.seed);
                float stretch = stretchEnabled ? 1f + Mathf.Min(stretchMax - 1f, speed * stretchPerSpeed) : 1f;
                float popBoost = popK < 1f ? 1f + 0.6f * (1f - popK) : 1f;
                float d = Mathf.Min(size * (e.just ? coreSizeJust : coreSizeNormal), coreMaxSize) * pulse * popBoost;
                Vector3 cp = pos - (Vector3)(dir * size * 0.2f * (stretch - 1f));
                SetSprite(e.core, cp, dir, d * stretch, d);
                Color cc = col; cc.a = coreAlpha * vAlpha;
                e.core.color = cc;
                SetSprite(e.coreWhiteSr, pos, dir, d * 0.45f * Mathf.Lerp(1f, stretch, 0.5f), d * 0.45f);
                Color wc = Color.Lerp(col, Color.white, 0.8f); wc.a = coreWhite * vAlpha;
                e.coreWhiteSr.color = wc;
            }
        }

        if (e.just && e.fancy)
        {
            // ⑥ 炎のオーラ（火の粉が後ろへ舞って散る）
            if (emberEnabled && visible && sparkle != null)
            {
                e.emitAcc += emberRate * thin * dt;
                while (e.emitAcc >= 1f)
                {
                    e.emitAcc -= 1f;
                    Vector2 side = new Vector2(-dir.y, dir.x) * Random.Range(-1f, 1f);
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = pos + (Vector3)(Random.insideUnitCircle * size * 0.35f),
                        velocity = (Vector3)((-dir * 0.6f + side * 0.8f).normalized * emberSpeed * Random.Range(0.5f, 1.2f)),
                        startColor = Color.Lerp(justColor, justColor2, Random.value),
                        startSize = size * Random.Range(0.25f, 0.5f),
                    };
                    sparkle.Emit(ep, 1);
                }
            }

            // ⑦ 残像
            if (e.ghosts != null) UpdateGhosts(e, visible, vAlpha);

            // ⑧ 外側の色付きトレイル
            if (e.outerTrail != null)
            {
                e.outerTrail.transform.position = pos;
                e.outerTrail.emitting = visible && e.rb != null && e.rb.simulated;
                e.outerTrail.time = outerTrailTime;
                e.outerTrail.widthMultiplier = Mathf.Min(outerTrailWidth * Mathf.Max(0.3f, size / 0.3f), outerTrailMaxWidth);
            }

            // ⑨ 電光スパーク
            if (lightningEnabled && visible && e.age >= e.nextLightning)
            {
                e.nextLightning = e.age + Random.Range(lightningIntervalMin, lightningIntervalMax);
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 to = pos + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * size * lightningLengthMul;
                SpawnBolt(pos, to, lightningDuration, lightningWidth, lightningColor, e.visual);
            }
        }

        e.lastDir = dir;
        e.lastPos = pos;
    }

    private void UpdateGhosts(Entry e, bool visible, float vAlpha)
    {
        e.histHead = (e.histHead + 1) % e.hist.Length;
        e.hist[e.histHead] = e.tf.position;
        e.histT[e.histHead] = e.age;
        for (int i = 0; i < e.ghosts.Length; i++)
        {
            var g = e.ghosts[i];
            if (g == null) continue;
            float want = e.age - ghostDelay * (i + 1);
            int idx = -1;
            for (int k = 0; k < e.hist.Length; k++)
            {
                int j = (e.histHead - k + e.hist.Length) % e.hist.Length;
                if (e.histT[j] >= 0f && e.histT[j] <= want) { idx = j; break; }
            }
            bool on = visible && idx >= 0 && want > 0f;
            g.enabled = on;
            if (!on) continue;
            g.sprite = e.visual.sprite;
            g.transform.position = e.hist[idx];
            g.transform.rotation = e.visualTf.rotation;
            g.transform.localScale = e.visualTf.lossyScale;
            Color c = Color.Lerp(justColor, justColor2, i / (float)Mathf.Max(1, e.ghosts.Length - 1));
            c.a = ghostAlpha * vAlpha * (1f - i / (float)(e.ghosts.Length + 1));
            g.color = c;
        }
    }

    // ⑤ 着弾リング
    private void OnEnemyHit(Entry e, Vector3 pos, bool powered)
    {
        int layer = e.visual != null ? e.visual.sortingLayerID : 0, order = e.visual != null ? e.visual.sortingOrder + 3 : 0;
        PlayEnemyHit(pos, powered || e.just, e.lastDir.sqrMagnitude > 0.0001f ? e.lastDir : Vector2.up, layer, order, true);
    }

    // 敵ヒット演出（反射弾・ドリル・ビームで共通）。inDir＝当たった物が飛んできた向き。allowShake＝A6揺れ・A7ヒットストップを出すか
    private void PlayEnemyHit(Vector3 pos, bool just, Vector2 inDir, int layer, int order, bool allowShake, float scale = 1f)
    {
        scale = Mathf.Max(0.1f, scale);
        float countMul = Mathf.Max(1f, scale); // 大きい時は粒も増やす
        if (just) { lastJustHitFrame = Time.frameCount; lastJustHitPos = pos; }

        // 1フレームに出す数を制限（同時に多数当たった時の重なり防止。A6/A7は間隔制限があるので別扱い）
        if (hitFxFrame != Time.frameCount) { hitFxFrame = Time.frameCount; hitFxCount = 0; }
        bool fxAllowed = hitFxCount < maxHitFxPerFrame;
        hitFxCount++;

        Color col = just ? justColor : normalColor;
        if (fxAllowed)
        {
            // ⑤ 着弾リング
            if (hitRingEnabled)
            {
                float s = (just ? hitRingSizeJust : hitRingSizeNormal) * scale;
                SpawnRing(pos, s * 0.25f, s, hitRingDuration, col, layer, order);
                if (just) SpawnRing(pos, s * 0.1f, s * 0.6f, hitRingDuration * 0.8f, Color.Lerp(col, Color.white, 0.7f), layer, order);
                EmitBurst(pos, Mathf.RoundToInt((just ? hitSparksJust : hitSparksNormal) * countMul), Color.Lerp(col, Color.white, 0.3f), (just ? 4f : 2.5f) * Mathf.Sqrt(scale), 0.08f);
            }
            // A1 被弾フラッシュ
            if (hitFlashEnabled) TryHitFlash(pos, just);
            // A2 十字フレア（ジャストは斜めも加えた8方向）
            if (crossFlareEnabled)
            {
                float len = (just ? crossFlareLengthJust : crossFlareLengthNormal) * scale;
                float off = Random.Range(0f, 45f);
                int rays = just ? 4 : 2;
                Color fc = Color.Lerp(col, Color.white, 0.55f);
                for (int i = 0; i < rays; i++)
                {
                    float ang = off + i * 180f / rays;
                    float l = (just && i % 2 == 1) ? len * 0.6f : len;
                    SpawnSpriteAnim(glowSprite, pos, ang, l * 0.3f, l, crossFlareThickness, crossFlareDuration, fc, layer, order + 1);
                }
            }
            // A3 方向性のある火花（弾が飛んできた向きの延長へ）
            if (directionalSparksEnabled && sparkle != null)
            {
                EmitCone(pos, inDir, directionalSparkSpread, Mathf.RoundToInt((just ? directionalSparksJust : directionalSparksNormal) * countMul), Color.Lerp(col, Color.white, 0.35f), directionalSparkSpeed * (just ? 1.3f : 1f) * Mathf.Sqrt(scale), 0.07f);
            }
            // A4 残光
            if (afterglowEnabled)
            {
                float g = (just ? afterglowSizeJust : afterglowSizeNormal) * scale;
                Color ac = col; ac.a = afterglowAlpha;
                SpawnSpriteAnim(glowSprite, pos, 0f, g, g * 0.6f, 1f, afterglowDuration, ac, layer, order - 1);
            }
        }

        if (!just || !allowShake) return;
        // A6 画面の小さな揺れ
        if (justHitShakeEnabled && Time.unscaledTime - lastShakeTime >= justHitShakeMinInterval)
        {
            lastShakeTime = Time.unscaledTime;
            CameraShake.Shake(justHitShakeDuration, justHitShakeMagnitude);
        }
        // A7 極小ヒットストップ（撃破のヒットストップ中・一時停止中は出さない）
        if (justHitStopEnabled && Time.timeScale > 0f && Time.unscaledTime - lastHitStopTime >= justHitStopMinInterval && HitStop.Instance != null)
        {
            lastHitStopTime = Time.unscaledTime;
            HitStop.Instance.TriggerShort(justHitStopSeconds);
        }
    }

    // A1：当たったエネミーの絵を探し、同じ絵を加算で重ねて一瞬光らせる（エネミーのマテリアルは変えない）
    private void TryHitFlash(Vector3 pos, bool just)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        var filter = new ContactFilter2D { useTriggers = true };
        if (enemyLayer >= 0) filter.SetLayerMask(1 << enemyLayer);
        overlapResults.Clear();
        Physics2D.OverlapCircle(pos, hitFlashSearchRadius, filter, overlapResults);
        SpriteRenderer best = null; float bestD = float.MaxValue;
        foreach (var c in overlapResults)
        {
            if (c == null) continue;
            var sr = c.GetComponent<SpriteRenderer>();
            if (sr == null) sr = c.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) sr = c.GetComponentInParent<SpriteRenderer>();
            if (sr == null || !sr.enabled || sr.sprite == null || sr.drawMode != SpriteDrawMode.Simple) continue;
            float d = ((Vector2)c.ClosestPoint(pos) - (Vector2)pos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = sr; }
        }
        if (best == null) return;
        if (lastFlashTime.TryGetValue(best, out float t) && Time.unscaledTime - t < hitFlashMinInterval) return;
        lastFlashTime[best] = Time.unscaledTime;
        if (lastFlashTime.Count > 64) lastFlashTime.Clear();
        if (anims.Count >= maxActiveAnims) return;
        var fsr = RentSprite(best.sprite, additiveSpriteMaterial, best.sortingLayerID, best.sortingOrder + 1);
        CopyFlash(fsr, best);
        anims.Add(new Anim { sr = fsr, src = best, follow = best.transform, dur = hitFlashDuration, c = new Color(1f, 1f, 1f, just ? hitFlashAlphaJust : hitFlashAlphaNormal) });
    }

    private static void CopyFlash(SpriteRenderer dst, SpriteRenderer src)
    {
        dst.sprite = src.sprite;
        dst.flipX = src.flipX; dst.flipY = src.flipY;
        var t = src.transform;
        dst.transform.SetPositionAndRotation(t.position, t.rotation);
        dst.transform.localScale = t.lossyScale;
    }

    // ---------- B 線で反射した時 ----------
    private void OnLineReflect(EnemyBullet b, Vector3 point, PaddleDot.LineType lineType, bool isJust, Stroke stroke, PaddleDot dot)
    {
        GetSorting(b, out int layer, out int order);
        PlayLineReflect(b, point, lineType, isJust, stroke, dot, layer, order, null);
    }

    // ビームの反射：弾の反射と同じ演出。並び順は線の点に合わせ、反射後の向きは分かっているのでそのまま使う
    private void OnBeamReflect(Vector3 point, PaddleDot dot, bool isJust, Vector2 newDir)
    {
        // 線の点の並び順（0）だとビーム本体の奥に隠れるため、ビームより手前の並び順にする
        int layer = SortingLayer.NameToID("Default"), order = beamFxSortingOrder;
        Stroke stroke = dot.ParentStroke;
        PaddleDot.LineType lineType = stroke != null ? stroke.Type : PaddleDot.LineType.Normal;
        PlayLineReflect(null, point, lineType, isJust, stroke, dot, layer, order, newDir);

        // 反射した瞬間のポップ：反射した向きへビームが一瞬白く太く光る（弾の「反射した瞬間のポップ」に相当）
        if (HandlesBeamReflectLook && newDir.sqrMagnitude > 0.0001f)
        {
            Vector2 nd = newDir.normalized;
            float ang = Mathf.Atan2(nd.y, nd.x) * Mathf.Rad2Deg;
            float len = beamPopLength;
            Color pc = Color.Lerp(isJust ? justColor : normalColor, Color.white, 0.75f);
            SpawnSpriteAnim(glowSprite, point + (Vector3)(nd * len * 0.5f), ang, len * 0.6f, len, beamPopThickness, beamPopDuration, pc, layer, order + 1);
            SpawnSpriteAnim(glowSprite, point, 0f, 0.5f, 1.1f, 1f, beamPopDuration, Color.white, layer, order + 2);
        }
    }

    /// <summary>
    /// ビームが線に当たり続けている間、ダメージ判定の間隔ごとに呼ぶ（EnemyBeamBullet.TickPaddleReflectionVfx）。
    /// ドリルと同じく、ヒットのたびに線反射の演出（B1〜B5）を出す。連続反射の盛り上がり（B6）は数えない
    /// </summary>
    /// <param name="hitIndex">最初の反射から数えたヒット回数（1,2,3…）。回数ごとに演出を大きくする（Beam Tick Grow Step）</param>
    public static void NotifyBeamReflectTick(Vector3 point, PaddleDot dot, bool isJust, Vector2 newDir, int hitIndex = 0)
    {
        if (dot == null || !HandlesBeamReflect) return;
        var m = s_instance;
        Stroke stroke = dot.ParentStroke;
        PaddleDot.LineType lineType = stroke != null ? stroke.Type : PaddleDot.LineType.Normal;
        float grow = Mathf.Min(1f + Mathf.Max(0, hitIndex) * m.beamTickGrowStep, Mathf.Max(1f, m.beamTickGrowMax));
        m.PlayLineReflect(null, point, lineType, isJust, stroke, dot, SortingLayer.NameToID("Default"), m.beamFxSortingOrder, newDir, false, grow);
    }

    // ---------- E 反射したビームの見た目 ----------
    private class BeamLook { public SpriteRenderer tip; public float sparkAcc, emberAcc, nextBolt; public int lastFrame; }
    private readonly Dictionary<EnemyBeamBullet, BeamLook> beamLooks = new Dictionary<EnemyBeamBullet, BeamLook>();
    private readonly List<(Vector3 a, Vector3 b, float len)> beamSegs = new List<(Vector3, Vector3, float)>();
    private readonly List<EnemyBeamBullet> beamRemove = new List<EnemyBeamBullet>();
    private float beamClock;

    private void OnBeamTick(EnemyBeamBullet beam)
    {
        float dt = SlowMoTime.DeltaTime;
        if (!beamLooks.TryGetValue(beam, out BeamLook bl)) { bl = new BeamLook { nextBolt = beamClock }; beamLooks[beam] = bl; }
        bl.lastFrame = Time.frameCount;
        bool just = beam.IsJustReflected;
        Color col = just ? justColor : normalColor;
        int layer = SortingLayer.NameToID("Default"), order = beamFxSortingOrder;

        // 反射後の区間を集める
        beamSegs.Clear();
        float total = 0f; Vector3 tip = Vector3.zero; bool hasTip = false;
        int n = beam.VisualSegmentCount;
        for (int i = 0; i < n; i++)
        {
            if (!beam.GetVisualSegment(i, out LineRenderer line, out bool reflected, out bool openEnd, out bool hasNext, out float fade)) continue;
            if (!reflected || line == null || !line.enabled || fade < 0.3f) continue;
            Vector3 a = line.GetPosition(0), b = line.GetPosition(1);
            float len = Vector3.Distance(a, b);
            if (len < 0.01f) continue;
            beamSegs.Add((a, b, len));
            total += len;
            if (!hasNext) { tip = b; hasTip = true; }
        }
        if (beamSegs.Count == 0) { if (bl.tip != null) bl.tip.enabled = false; return; }

        // 光の粒（ノーマル＝シアン、ジャスト＝オレンジ）
        if (sparkle != null)
        {
            bl.sparkAcc += beamSparkleRate * dt;
            while (bl.sparkAcc >= 1f)
            {
                bl.sparkAcc -= 1f;
                PointOnBeam(total, out Vector3 p, out Vector2 d);
                Vector2 side = new Vector2(-d.y, d.x) * Random.Range(-1f, 1f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = p, velocity = (Vector3)(side * 0.8f - d * 0.6f),
                    startColor = Color.Lerp(col, Color.white, Random.Range(0f, 0.5f)), startSize = Random.Range(0.05f, 0.1f),
                };
                sparkle.Emit(ep, 1);
            }
        }
        if (just)
        {
            // 火の粉
            if (sparkle != null)
            {
                bl.emberAcc += beamEmberRate * dt;
                while (bl.emberAcc >= 1f)
                {
                    bl.emberAcc -= 1f;
                    PointOnBeam(total, out Vector3 p, out Vector2 d);
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = p, velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 1.2f), 0f) * emberSpeed,
                        startColor = Color.Lerp(justColor, justColor2, Random.value), startSize = Random.Range(0.06f, 0.13f),
                    };
                    sparkle.Emit(ep, 1);
                }
            }
            // ときどき走る稲妻
            if (lightningEnabled && beamClock >= bl.nextBolt)
            {
                bl.nextBolt = beamClock + Random.Range(beamBoltIntervalMin, beamBoltIntervalMax);
                PointOnBeam(total, out Vector3 p, out Vector2 d);
                float ang = Random.Range(0f, Mathf.PI * 2f);
                SpawnBoltAt(p, p + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * beamBoltLength, lightningDuration, lightningWidth, lightningColor, layer, order + 1);
            }
            // 先端の発光
            if (hasTip && glowSprite != null)
            {
                if (bl.tip == null) bl.tip = RentSprite(glowSprite, additiveSpriteMaterial, layer, order);
                bl.tip.enabled = true;
                float pulse = 1f + 0.2f * Mathf.Sin(beamClock * 16f);
                float s = beamTipGlowSize * pulse;
                SetSprite(bl.tip, tip, Vector2.right, s, s);
                Color tc = justColor; tc.a = coreAlpha;
                bl.tip.color = tc;
            }
            else if (bl.tip != null) bl.tip.enabled = false;
        }
        else if (bl.tip != null) bl.tip.enabled = false;
    }

    // 反射後の区間全体から、長さに比例してランダムな1点と、その区間の向きを選ぶ
    private void PointOnBeam(float total, out Vector3 p, out Vector2 dir)
    {
        float r = Random.Range(0f, total);
        foreach (var s in beamSegs)
        {
            if (r <= s.len)
            {
                float t = r / s.len;
                p = Vector3.Lerp(s.a, s.b, t);
                dir = ((Vector2)(s.b - s.a)).normalized;
                return;
            }
            r -= s.len;
        }
        var last = beamSegs[beamSegs.Count - 1];
        p = last.b; dir = ((Vector2)(last.b - last.a)).normalized;
    }

    // ビームが消えた・止まった時に先端の発光を片付ける
    private void CleanupBeamLooks()
    {
        beamClock += SlowMoTime.DeltaTime;
        if (beamLooks.Count == 0) return;
        beamRemove.Clear();
        foreach (var kv in beamLooks)
            if (kv.Key == null || Time.frameCount - kv.Value.lastFrame > 1) beamRemove.Add(kv.Key);
        foreach (var k in beamRemove)
        {
            if (beamLooks.TryGetValue(k, out BeamLook bl) && bl.tip != null) ReleaseSprite(bl.tip);
            beamLooks.Remove(k);
        }
    }

    private void PlayLineReflect(EnemyBullet b, Vector3 point, PaddleDot.LineType lineType, bool isJust, Stroke stroke, PaddleDot dot, int layer, int order, Vector2? knownDir, bool countCombo = true, float extraScale = 1f)
    {
        if (reflectFxFrame != Time.frameCount) { reflectFxFrame = Time.frameCount; reflectFxCount = 0; }
        if (reflectFxCount >= maxReflectFxPerFrame) return;
        reflectFxCount++;

        // B6 連続反射（ビームが線に当たり続けている間の毎回のヒットでは数えない）
        float scale = 1f;
        if (countCombo)
        {
            float now = Time.unscaledTime;
            comboCount = comboEnabled && now - lastReflectTime <= comboWindow ? comboCount + 1 : 1;
            lastReflectTime = now;
            scale = comboEnabled ? 1f + Mathf.Min(comboCount - 1, comboMaxSteps) * comboStep : 1f;
        }
        scale *= Mathf.Max(0.01f, extraScale); // ビームの当たり続け：ヒットのたびに大きくする

        Color col = isJust ? justColor : (lineType == PaddleDot.LineType.RedAccel ? redLineColor : normalColor);

        // B1 線を走る光（反射点から線に沿って両方向へ）
        if (lineRunEnabled && stroke != null && dot != null && lineMaterial != null && stroke.TryGetLocalDirection(dot, lineRunSampleRange, out Vector2 ld))
        {
            ld.Normalize();
            Color rc = Color.Lerp(col, Color.white, 0.4f);
            for (int s = -1; s <= 1; s += 2)
            {
                if (anims.Count >= maxActiveAnims) break;
                var lr = RentLine(layer, order, 2);
                anims.Add(new Anim { lr = lr, run = true, dur = lineRunDuration, a = point, b = point + (Vector3)(ld * s * lineRunLength * (isJust ? 1.4f : 1f) * extraScale), width = lineRunWidth * (isJust ? 1.4f : 1f) * extraScale, c = rc });
            }
        }

        PlayImpact(b, point, col, isJust, scale, comboCount, layer, order, knownDir);
    }

    // B3・B5・B2・B4（線で反射した時・ブロックに当たった時・ビームの反射で共通）。knownDir＝反射後の向きが分かっている時（ビーム）
    private void PlayImpact(EnemyBullet b, Vector3 point, Color col, bool isJust, float scale, int comboLevel, int layer, int order, Vector2? knownDir = null)
    {
        // B3 衝撃リング（ジャストは二重）
        if (reflectRingEnabled)
        {
            float rs = (isJust ? reflectRingSizeJust : reflectRingSizeNormal) * scale;
            Color rc = col; rc.a = Mathf.Min(1f, 0.8f + 0.05f * (comboLevel - 1));
            SpawnRing(point, rs * 0.2f, rs, reflectRingDuration, rc, layer, order);
            if (isJust) SpawnRing(point, rs * 0.1f, rs * 0.6f, reflectRingDuration * 0.8f, Color.Lerp(col, Color.white, 0.7f), layer, order);
        }

        // B5 ジャストのスターバースト
        if (isJust && justStarEnabled)
        {
            float off = Random.Range(0f, 180f);
            Color sc = Color.Lerp(justColor, Color.white, 0.5f);
            for (int i = 0; i < justStarRays; i++)
            {
                float ang = off + i * 180f / Mathf.Max(1, justStarRays);
                float l = justStarLength * scale * (i % 2 == 0 ? 1f : 0.6f);
                SpawnSpriteAnim(glowSprite, point, ang, l * 0.2f, l, justStarThickness, justStarDuration, sc, layer, order + 1);
            }
            if (sparkle != null)
            {
                var ep = new ParticleSystem.EmitParams { position = point, startColor = Color.Lerp(justColor, Color.white, 0.3f), startSize = 0.08f };
                int n = Mathf.Max(1, justStarRingParticles);
                for (int i = 0; i < n; i++)
                {
                    float a = (i / (float)n) * Mathf.PI * 2f;
                    ep.velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * justStarParticleSpeed * scale;
                    sparkle.Emit(ep, 1);
                }
            }
        }

        // B2・B4 は反射後の進行方向が決まってから（このフレームのLateUpdate）出す
        if (speedLinesEnabled || reflectSparksEnabled)
        {
            Vector2 fb = knownDir ?? (b != null ? (Vector2)(b.transform.position - point) : Vector2.up);
            pendingReflects.Add(new PendingReflect { b = knownDir.HasValue ? null : b, p = point, c = col, just = isJust, scale = scale, layer = layer, order = order, fallbackDir = fb.sqrMagnitude > 0.0001f ? fb.normalized : Vector2.up });
        }
    }

    private void GetSorting(EnemyBullet b, out int layer, out int order)
    {
        Transform v = b.transform.Find("Visual");
        var vsr = v != null ? v.GetComponent<SpriteRenderer>() : null;
        layer = vsr != null ? vsr.sortingLayerID : 0;
        order = vsr != null ? vsr.sortingOrder + 3 : 0;
    }

    // ブロックに当たった時：線の反射と同じ演出（B1は線が無いので無し、B6の連続カウントは進めない）
    private readonly Dictionary<EnemyBullet, float> lastBlockHitTime = new Dictionary<EnemyBullet, float>();
    private void OnBlockHit(EnemyBullet b, Vector3 point)
    {
        if (b.GetComponent<PinnedReflectBullet>() != null) return; // ドリル弾は専用の演出
        float now = Time.unscaledTime;
        if (lastBlockHitTime.TryGetValue(b, out float t) && now - t < blockHitMinInterval) return;
        lastBlockHitTime[b] = now;
        if (lastBlockHitTime.Count > 128) lastBlockHitTime.Clear();

        if (reflectFxFrame != Time.frameCount) { reflectFxFrame = Time.frameCount; reflectFxCount = 0; }
        if (reflectFxCount >= maxReflectFxPerFrame) return;
        reflectFxCount++;

        bool isJust = b.DamageMultiplier > 1.0001f;
        GetSorting(b, out int layer, out int order);
        PlayImpact(b, point, isJust ? justColor : normalColor, isJust, 1f, 1, layer, order);
    }

    private void ProcessPendingReflects()
    {
        for (int i = 0; i < pendingReflects.Count; i++)
        {
            var pr = pendingReflects[i];
            Vector2 dir = pr.fallbackDir;
            if (pr.b != null && pr.b.gameObject.activeInHierarchy)
            {
                var rb = pr.b.GetComponent<Rigidbody2D>();
                if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f) dir = rb.linearVelocity.normalized;
            }
            // B2 スピードライン
            if (speedLinesEnabled && lineMaterial != null)
            {
                Color lc = Color.Lerp(pr.c, Color.white, 0.5f);
                for (int k = 0; k < speedLineCount; k++)
                {
                    if (anims.Count >= maxActiveAnims) break;
                    float spread = speedLineCount > 1 ? Mathf.Lerp(-speedLineSpread, speedLineSpread, k / (float)(speedLineCount - 1)) : 0f;
                    Vector2 d = Quaternion.Euler(0f, 0f, spread + Random.Range(-3f, 3f)) * dir;
                    Vector3 start = pr.p + (Vector3)(d * 0.15f);
                    var lr = RentLine(pr.layer, pr.order, 2);
                    float len = speedLineLength * pr.scale * Random.Range(0.75f, 1.1f) * (pr.just ? 1.3f : 1f);
                    anims.Add(new Anim { lr = lr, run = true, dur = speedLineDuration, a = start, b = start + (Vector3)(d * len), width = speedLineWidth * (pr.just ? 1.4f : 1f), c = lc });
                }
            }
            // B4 扇状の火花
            if (reflectSparksEnabled && sparkle != null)
                EmitCone(pr.p, dir, reflectSparkSpread, pr.just ? reflectSparksJust : reflectSparksNormal, Color.Lerp(pr.c, Color.white, 0.35f), reflectSparkSpeed * (pr.just ? 1.3f : 1f), 0.07f);
        }
        pendingReflects.Clear();
    }

    // ---------- D 未反射弾と反射弾の衝突 ----------
    private void OnBulletClash(EnemyBullet refl, EnemyBullet unrefl, bool pierced)
    {
        if (clashFrame != Time.frameCount) { clashFrame = Time.frameCount; clashCount = 0; }
        if (clashCount >= maxClashesPerFrame) return;
        clashCount++;

        // D8 連続相殺
        float now = Time.unscaledTime;
        clashCombo = clashComboEnabled && now - lastClashTime <= clashComboWindow ? clashCombo + 1 : 1;
        lastClashTime = now;
        float scale = clashComboEnabled ? 1f + Mathf.Min(clashCombo - 1, clashComboMaxSteps) * clashComboStep : 1f;

        Vector3 pr = refl.transform.position, pu = unrefl.transform.position;
        Vector3 mid = (pr + pu) * 0.5f;
        Vector3 du = pr - pu; // 未反射弾の進む向き（反射弾の方へ）
        Vector2 dirU = du.sqrMagnitude > 0.0001f ? (Vector2)du.normalized : Vector2.down;
        Vector2 dirR = -dirU;  // 反射弾の進む向き（未反射弾の方へ）

        bool just = refl.DamageMultiplier > 1.0001f;
        Color pc = just ? justColor : normalColor;
        Color ec = BulletFXManager.TryGetBulletColor(unrefl, out Color auraC) ? auraC : clashEnemyColor;
        ec = Color.Lerp(ec, Color.white, clashEnemyBrighten); ec.a = 1f;

        Transform v = refl.transform.Find("Visual");
        var rsr = v != null ? v.GetComponent<SpriteRenderer>() : null;
        int layer = rsr != null ? rsr.sortingLayerID : 0, order = rsr != null ? rsr.sortingOrder + 3 : 20;

        // D6 消える弾の白い膨らみ（未反射弾は必ず消える。反射弾は突き抜けない時だけ消える）
        if (clashPuffEnabled)
        {
            SpawnPuff(unrefl, layer, order);
            if (!pierced) SpawnPuff(refl, layer, order);
        }
        // D1 中間点で2色の対消滅（それぞれの色の光が自分の側から中央へ膨らんで弾ける）
        if (clashAnnihilateEnabled)
        {
            float s = clashAnnihilateSize * scale;
            float gap = Mathf.Min(du.magnitude * 0.5f, s * 0.25f);
            SpawnSpriteAnim(glowSprite, mid - (Vector3)(dirU * gap), 0f, s * 0.4f, s, 1f, clashAnnihilateDuration, new Color(ec.r, ec.g, ec.b, 0.85f), layer, order);
            SpawnSpriteAnim(glowSprite, mid - (Vector3)(dirR * gap), 0f, s * 0.4f, s, 1f, clashAnnihilateDuration, new Color(pc.r, pc.g, pc.b, 0.85f), layer, order);
        }
        // D3 2色の衝撃リング
        if (clashRingEnabled)
        {
            float rs = clashRingSize * scale;
            SpawnRing(mid, rs * 0.2f, rs, clashRingDuration, ec, layer, order + 1);
            SpawnRing(mid, rs * 0.1f, rs * 0.7f, clashRingDuration * 0.85f, pc, layer, order + 1);
        }
        // D2 閃光とX字フレア（衝突の向きに対して斜めに交差）
        if (clashFlareEnabled)
        {
            SpawnSpriteAnim(glowSprite, mid, 0f, clashFlashSize * scale * 0.6f, clashFlashSize * scale, 1f, clashFlareDuration, Color.white, layer, order + 2);
            float baseAng = Mathf.Atan2(dirU.y, dirU.x) * Mathf.Rad2Deg;
            float len = clashFlareLength * scale;
            Color fc = Color.Lerp(pc, Color.white, 0.6f);
            SpawnSpriteAnim(glowSprite, mid, baseAng + 45f, len * 0.3f, len, clashFlareThickness, clashFlareDuration, fc, layer, order + 2);
            SpawnSpriteAnim(glowSprite, mid, baseAng - 45f, len * 0.3f, len, clashFlareThickness, clashFlareDuration, Color.Lerp(ec, Color.white, 0.5f), layer, order + 2);
        }
        // D5 相殺の電光（2発の間に走る。ジャストは2本）
        if (clashBoltEnabled)
        {
            Vector3 a = pu - (Vector3)(dirU * 0.15f), b = pr + (Vector3)(dirU * 0.15f);
            SpawnBolt(a, b, clashBoltDuration, clashBoltWidth, Color.Lerp(pc, Color.white, 0.5f), rsr);
            if (just) SpawnBolt(a, b, clashBoltDuration * 1.3f, clashBoltWidth * 0.7f, Color.Lerp(ec, Color.white, 0.5f), rsr);
        }
        // D4 押し合う火花
        if (clashSparksEnabled)
        {
            EmitCone(mid, dirU, clashSparkSpread, clashSparkCount, ec, clashSparkSpeed, 0.07f);
            EmitCone(mid, dirR, clashSparkSpread, clashSparkCount, Color.Lerp(pc, Color.white, 0.3f), clashSparkSpeed, 0.07f);
        }
        // D7 突き抜け：反射弾が一瞬光り（ポップ）、突き抜けた向きへ破片が飛ぶ
        if (pierced && clashPierceEnabled)
        {
            if (byBullet.TryGetValue(refl, out Entry e) && popEnabled) e.popAge = 0f;
            EmitCone(mid, dirR, clashPierceSpread, clashPierceDebris, Color.Lerp(ec, Color.white, 0.2f), clashPierceSpeed, lineDebrisSize, debris != null ? debris : sparkle);
        }
        // D9 画面の小さな揺れ
        if (clashShakeEnabled && Time.unscaledTime - lastClashShakeTime >= clashShakeMinInterval)
        {
            lastClashShakeTime = Time.unscaledTime;
            CameraShake.Shake(clashShakeDuration, clashShakeMagnitude);
        }
    }

    // D6：弾の絵を白く加算で重ね、膨らませながら消す
    private void SpawnPuff(EnemyBullet b, int layer, int order)
    {
        Transform v = b.transform.Find("Visual");
        var vsr = v != null ? v.GetComponent<SpriteRenderer>() : null;
        if (vsr == null || vsr.sprite == null || !vsr.enabled) return;
        Vector3 bs = vsr.bounds.size;
        float w = Mathf.Max(0.05f, Mathf.Max(bs.x, bs.y));
        float ang = v.eulerAngles.z;
        SpawnSpriteAnim(vsr.sprite, v.position, ang, w, w * clashPuffScale, 1f, clashPuffDuration, new Color(1f, 1f, 1f, clashPuffAlpha), layer, order + 1);
    }

    // ---------- C 線が貫通されて壊れた時 ----------
    private void OnLineBreak(Stroke stroke, PaddleDot hitDot, PaddleDot.LineType lineType, Vector3 point, Vector2 throughDir)
    {
        if (stroke != null)
        {
            if (brokenThisFrame.Contains(stroke)) return; // 同じ線に同時に当たった分は1回だけ
            brokenThisFrame.Add(stroke);
        }
        if (lineBreakFrame != Time.frameCount) { lineBreakFrame = Time.frameCount; lineBreakCount = 0; }
        if (lineBreakCount >= maxLineBreaksPerFrame) return;
        lineBreakCount++;

        // 線の点（消える前に写し取る）と色
        int n = stroke != null ? stroke.CopyActiveDots(dotBuffer) : 0;
        Color col = lineType == PaddleDot.LineType.RedAccel ? redLineColor : Color.Lerp(Color.white, normalColor, 0.5f);
        SpriteRenderer refSr = hitDot != null ? hitDot.GetComponent<SpriteRenderer>() : null;
        if (lineBreakUseDotColor && refSr != null) { col = refSr.color; col.a = 1f; }
        int layer = refSr != null ? refSr.sortingLayerID : 0, order = refSr != null ? refSr.sortingOrder + 2 : 20;
        float dotSize = refSr != null && refSr.sprite != null ? Mathf.Max(0.03f, Mathf.Min(refSr.bounds.size.x, refSr.bounds.size.y)) : 0.12f;

        // C1・C7 連鎖崩壊（点の位置を間引いて最大64点の線にする）
        if (lineCollapseEnabled && n >= 2 && lineMaterial != null)
        {
            int step = Mathf.Max(1, Mathf.CeilToInt(n / 64f));
            int m = (n - 1) / step + 1;
            var pts = new Vector3[m];
            int impact = 0; float best = float.MaxValue;
            for (int i = 0; i < m; i++)
            {
                pts[i] = dotBuffer[Mathf.Min(n - 1, i * step)].transform.position;
                float d = (pts[i] - point).sqrMagnitude;
                if (d < best) { best = d; impact = i; }
            }
            var cl = new Collapse { pts = pts, impact = impact, width = dotSize * lineCollapseWidthMul, c = col, debrisLeft = lineDebrisEnabled ? lineDebrisMax : 0, lastL = impact, lastR = impact };
            cl.left = RentLine(layer, order, 2);
            cl.right = RentLine(layer, order, 2);
            collapses.Add(cl);
        }

        // C3 貫通点の閃光とひび
        if (lineCrackEnabled)
        {
            SpawnSpriteAnim(glowSprite, point, 0f, lineCrackFlashSize * 0.6f, lineCrackFlashSize, 1f, lineCrackFlashDuration, Color.Lerp(col, Color.white, 0.8f), layer, order + 2);
            float off = Random.Range(0f, 360f);
            for (int i = 0; i < lineCrackCount; i++)
            {
                if (anims.Count >= maxActiveAnims) break;
                float ang = (off + i * 360f / Mathf.Max(1, lineCrackCount) + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
                Vector3 to = point + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * lineCrackLength * Random.Range(0.6f, 1.1f);
                var lr = RentLine(layer, order + 1, s_boltPts.Length);
                Vector3 d = to - point;
                Vector3 nrm = new Vector3(-d.y, d.x, 0f).normalized * d.magnitude * 0.15f;
                for (int k = 0; k < s_boltPts.Length; k++)
                {
                    float kk = k / (float)(s_boltPts.Length - 1);
                    s_boltPts[k] = point + d * kk + (k == 0 ? Vector3.zero : nrm * Random.Range(-1f, 1f));
                }
                lr.SetPositions(s_boltPts);
                anims.Add(new Anim { lr = lr, dur = lineCrackDuration, width = lineCrackWidth, c = Color.Lerp(col, Color.white, 0.6f), jagged = true });
            }
        }

        // C4 衝撃リング
        if (lineBreakRingEnabled) SpawnRing(point, lineBreakRingSize * 0.2f, lineBreakRingSize, lineBreakRingDuration, col, layer, order);

        // C5 突き抜け方向への飛散
        if (lineBreakSprayEnabled && throughDir.sqrMagnitude > 0.0001f)
            EmitCone(point, throughDir.normalized, lineBreakSpraySpread, lineBreakSprayCount, Color.Lerp(col, Color.white, 0.3f), lineBreakSpraySpeed, lineDebrisSize, debris != null ? debris : sparkle);

        // C8 画面の小さな揺れ
        if (lineBreakShakeEnabled && Time.unscaledTime - lastLineBreakShakeTime >= lineBreakShakeMinInterval)
        {
            lastLineBreakShakeTime = Time.unscaledTime;
            CameraShake.Shake(lineBreakShakeDuration, lineBreakShakeMagnitude);
        }
    }

    private void UpdateCollapses(float dt)
    {
        var ps = debris != null ? debris : sparkle;
        for (int i = collapses.Count - 1; i >= 0; i--)
        {
            var cl = collapses[i];
            cl.t += dt;
            float total = Mathf.Max(0f, lineFlashSeconds) + Mathf.Max(0.01f, lineCollapseSeconds);
            if (cl.t >= total)
            {
                ReleaseLine(cl.left); ReleaseLine(cl.right);
                collapses.RemoveAt(i);
                continue;
            }
            // C7：最初は白く光り、その後は線の色で砕けていく
            bool flashing = cl.t < lineFlashSeconds;
            float k = flashing ? 0f : Mathf.Clamp01((cl.t - lineFlashSeconds) / Mathf.Max(0.01f, lineCollapseSeconds));
            Color c = flashing ? Color.Lerp(cl.c, Color.white, 0.85f) : cl.c;
            c.a = flashing ? 1f : 1f - k * 0.5f;
            float w = cl.width * (flashing ? 1.4f : 1f);

            // 貫通点から端へ向かって消えていく（左は0..impact、右はimpact..末尾）
            int last = cl.pts.Length - 1;
            int l = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(cl.impact, -1f, k)), -1, cl.impact);
            int r = Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(cl.impact, last + 1, k)), cl.impact, last + 1);
            SetSegment(cl.left, cl.pts, 0, flashing ? cl.impact : l, w, c);
            SetSegment(cl.right, cl.pts, flashing ? cl.impact : r, last, w, c);

            // C2：砕けた所から破片を出す
            if (!flashing && cl.debrisLeft > 0 && ps != null)
            {
                for (int j = cl.lastL - 1; j > l && j >= 0 && cl.debrisLeft > 0; j--) { EmitDebris(ps, cl.pts[j], c); cl.debrisLeft--; }
                for (int j = cl.lastR + 1; j < r && j <= last && cl.debrisLeft > 0; j++) { EmitDebris(ps, cl.pts[j], c); cl.debrisLeft--; }
                if (cl.lastL == cl.impact && cl.lastR == cl.impact && cl.debrisLeft > 0) { EmitDebris(ps, cl.pts[cl.impact], c); cl.debrisLeft--; }
                cl.lastL = Mathf.Min(cl.lastL, Mathf.Max(l, 0));
                cl.lastR = Mathf.Max(cl.lastR, Mathf.Min(r, last));
            }
        }
    }

    private void SetSegment(LineRenderer lr, Vector3[] pts, int from, int to, float width, Color c)
    {
        int count = to - from + 1;
        if (count < 2) { lr.enabled = false; return; }
        lr.enabled = true;
        if (tmpPts.Length < count) tmpPts = new Vector3[count];
        System.Array.Copy(pts, from, tmpPts, 0, count);
        lr.positionCount = count;
        lr.SetPositions(tmpPts);
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = c;
    }

    private void EmitDebris(ParticleSystem ps, Vector3 p, Color c)
    {
        Vector2 d = Random.insideUnitCircle;
        var ep = new ParticleSystem.EmitParams
        {
            position = p,
            velocity = (Vector3)(d * lineDebrisSpeed + Vector2.up * lineDebrisSpeed * 0.4f),
            startColor = Color.Lerp(c, Color.white, Random.Range(0f, 0.4f)),
            startSize = lineDebrisSize * Random.Range(0.6f, 1.3f),
        };
        ps.Emit(ep, 1);
    }

    private void EmitCone(Vector3 pos, Vector2 dir, float spreadDeg, int count, Color color, float speed, float size)
    {
        EmitCone(pos, dir, spreadDeg, count, color, speed, size, sparkle);
    }

    private void EmitCone(Vector3 pos, Vector2 dir, float spreadDeg, int count, Color color, float speed, float size, ParticleSystem target)
    {
        var sparkle = target;
        if (sparkle == null || count <= 0) return;
        float baseAng = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        var ep = new ParticleSystem.EmitParams { position = pos, startColor = color, startSize = size };
        for (int i = 0; i < count; i++)
        {
            float a = (baseAng + Random.Range(-spreadDeg, spreadDeg)) * Mathf.Deg2Rad;
            ep.velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * speed * Random.Range(0.5f, 1.15f);
            sparkle.Emit(ep, 1);
        }
    }

    // ⑩ ジャスト弾で倒した時の放射状の光
    private void SpawnKillBurst(Vector3 pos)
    {
        int layer = 0, order = 30;
        float off = Random.Range(0f, 360f);
        for (int i = 0; i < killRayCount; i++)
        {
            float ang = (off + i * 360f / Mathf.Max(1, killRayCount) + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            float len = killRayLength * Random.Range(0.6f, 1.1f);
            var lr = RentLine(layer, order, 2);
            anims.Add(new Anim { lr = lr, dur = killBurstDuration, a = pos, b = pos + d * len, width = killRayWidth, c = i % 2 == 0 ? justColor : Color.Lerp(justColor, Color.white, 0.6f) });
        }
        SpawnRing(pos, killRingSize * 0.2f, killRingSize, killBurstDuration, justColor, layer, order);
        SpawnRing(pos, killRingSize * 0.1f, killRingSize * 0.55f, killBurstDuration * 0.7f, Color.white, layer, order + 1);
        EmitBurst(pos, killSparks, Color.Lerp(justColor, Color.white, 0.4f), 6f, 0.12f);
    }

    // ---------- 単発のアニメ（リング・稲妻・光の筋） ----------
    private void SpawnRing(Vector3 pos, float s0, float s1, float dur, Color c, int layer, int order)
    {
        SpawnSpriteAnim(ringSprite, pos, 0f, s0, s1, 1f, dur, c, layer, order);
    }

    // 単発のスプライト演出（リング・光の筋・残光）。aspect＝幅/長さ、angle＝向き（度）
    private void SpawnSpriteAnim(Sprite sprite, Vector3 pos, float angle, float s0, float s1, float aspect, float dur, Color c, int layer, int order)
    {
        if (sprite == null || anims.Count >= maxActiveAnims) return;
        var sr = RentSprite(sprite, additiveSpriteMaterial, layer, order);
        sr.transform.position = pos;
        float r = angle * Mathf.Deg2Rad;
        anims.Add(new Anim { sr = sr, dur = dur, s0 = s0, s1 = s1, aspect = aspect, c = c, dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r)) });
    }

    private void SpawnBolt(Vector3 from, Vector3 to, float dur, float width, Color c, SpriteRenderer refSr)
    {
        SpawnBoltAt(from, to, dur, width, c, refSr != null ? refSr.sortingLayerID : 0, refSr != null ? refSr.sortingOrder + 3 : 0);
    }

    private void SpawnBoltAt(Vector3 from, Vector3 to, float dur, float width, Color c, int layer, int order)
    {
        if (lineMaterial == null) return;
        var lr = RentLine(layer, order, s_boltPts.Length);
        Vector3 d = to - from;
        Vector3 n = new Vector3(-d.y, d.x, 0f).normalized * d.magnitude * 0.18f;
        for (int i = 0; i < s_boltPts.Length; i++)
        {
            float k = i / (float)(s_boltPts.Length - 1);
            s_boltPts[i] = from + d * k + (i == 0 || i == s_boltPts.Length - 1 ? Vector3.zero : n * Random.Range(-1f, 1f));
        }
        lr.SetPositions(s_boltPts);
        anims.Add(new Anim { lr = lr, dur = dur, width = width, c = c, jagged = true });
    }

    private void UpdateAnims(float dt)
    {
        for (int i = anims.Count - 1; i >= 0; i--)
        {
            var a = anims[i];
            a.t += dt;
            float k = a.t / Mathf.Max(0.01f, a.dur);
            if (k >= 1f)
            {
                if (a.sr != null) ReleaseSprite(a.sr);
                if (a.lr != null) ReleaseLine(a.lr);
                anims.RemoveAt(i);
                continue;
            }
            float eased = 1f - (1f - k) * (1f - k);
            if (a.sr != null && a.src != null)
            {
                // A1 被弾フラッシュ：エネミーの絵に合わせて動かしながら消える
                if (a.follow == null || !a.src.enabled) a.t = a.dur; else CopyFlash(a.sr, a.src);
                Color c = a.c; c.a *= 1f - k; a.sr.color = c;
            }
            else if (a.sr != null)
            {
                float s = Mathf.Lerp(a.s0, a.s1, eased);
                SetSprite(a.sr, a.sr.transform.position, a.dir, s, s * a.aspect);
                Color c = a.c; c.a *= 1f - k; a.sr.color = c;
            }
            if (a.lr != null)
            {
                Color c = a.c; c.a *= 1f - k;
                if (a.run)
                {
                    // 走る光：先端が先へ進み、少し遅れて根元が追いかけて消える
                    a.lr.SetPosition(0, Vector3.Lerp(a.a, a.b, Mathf.Clamp01(k * 1.4f - 0.4f)));
                    a.lr.SetPosition(1, Vector3.Lerp(a.a, a.b, eased));
                    a.lr.startWidth = a.width * 0.3f; a.lr.endWidth = a.width;
                    a.lr.startColor = new Color(c.r, c.g, c.b, 0f); a.lr.endColor = c;
                }
                else if (a.jagged)
                {
                    a.lr.startWidth = a.lr.endWidth = a.width;
                    a.lr.startColor = a.lr.endColor = c;
                }
                else
                {
                    // 光の筋：中心から外へ伸びながら、根元から消えていく
                    a.lr.SetPosition(0, Vector3.Lerp(a.a, a.b, Mathf.Clamp01(k * 1.3f - 0.3f)));
                    a.lr.SetPosition(1, Vector3.Lerp(a.a, a.b, eased));
                    a.lr.startWidth = a.width * (1f - k);
                    a.lr.endWidth = a.width * 0.3f * (1f - k);
                    a.lr.startColor = c; a.lr.endColor = new Color(c.r, c.g, c.b, 0f);
                }
            }
        }
    }

    // ---------- 共通 ----------
    private static float SpriteSize(SpriteRenderer sr)
    {
        if (sr == null || sr.sprite == null) return 0.3f;
        var s = sr.sprite.bounds.size;
        return Mathf.Max(s.x, s.y);
    }

    private static float ParentScale(Transform t)
    {
        if (t == null || t.parent == null) return 1f;
        var s = t.parent.lossyScale;
        return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
    }

    private static void SetSprite(SpriteRenderer sr, Vector3 pos, Vector2 dir, float lengthWorld, float widthWorld)
    {
        sr.transform.position = pos;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        float s = sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.x) : 1f;
        sr.transform.localScale = new Vector3(lengthWorld / s, widthWorld / s, 1f);
    }

    private void EmitBurst(Vector3 pos, int count, Color color, float speed, float size)
    {
        if (sparkle == null || count <= 0) return;
        var ep = new ParticleSystem.EmitParams { position = pos, startColor = color, startSize = size };
        for (int i = 0; i < count; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            ep.velocity = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * speed * Random.Range(0.4f, 1.1f);
            sparkle.Emit(ep, 1);
        }
    }

    private SpriteRenderer RentSprite(Sprite sprite, Material mat, int layer, int order)
    {
        SpriteRenderer sr = null;
        while (freeSprites.Count > 0 && sr == null) sr = freeSprites.Pop();
        if (sr == null)
        {
            var go = new GameObject("ReflectFX_Sprite");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
        }
        if (mat != null) sr.sharedMaterial = mat;
        sr.sprite = sprite;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        sr.color = Color.white;
        sr.gameObject.SetActive(true);
        sr.enabled = true;
        return sr;
    }

    private void ReleaseSprite(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.enabled = false;
        sr.gameObject.SetActive(false);
        freeSprites.Push(sr);
    }

    private LineRenderer RentLine(int layer, int order, int points)
    {
        LineRenderer lr = null;
        while (freeLines.Count > 0 && lr == null) lr = freeLines.Pop();
        if (lr == null)
        {
            var go = new GameObject("ReflectFX_Line");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
        }
        lr.positionCount = points;
        lr.sortingLayerID = layer;
        lr.sortingOrder = order;
        lr.gameObject.SetActive(true);
        return lr;
    }

    private void ReleaseLine(LineRenderer lr)
    {
        if (lr == null) return;
        lr.enabled = true; // 線の崩壊で非表示にした分を戻しておく
        lr.gameObject.SetActive(false);
        freeLines.Push(lr);
    }

    private TrailRenderer RentTrail(int layer, int order)
    {
        TrailRenderer tr = null;
        while (freeTrails.Count > 0 && tr == null) tr = freeTrails.Pop();
        if (tr == null)
        {
            var go = new GameObject("ReflectFX_Trail");
            go.transform.SetParent(transform, false);
            tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = lineMaterial;
            tr.minVertexDistance = 0.08f;
            tr.numCapVertices = 2;
            tr.alignment = LineAlignment.View;
        }
        var g = new Gradient();
        Color a = justColor2, b = justColor;
        g.SetKeys(new[] { new GradientColorKey(b, 0f), new GradientColorKey(a, 1f) },
                  new[] { new GradientAlphaKey(outerTrailAlpha, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        // 先端（0）を細く尖らせ、少し後ろで最大の太さ→末尾（1）で0。先端が平らに切れて四角く見えるのを防ぐ
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(Mathf.Clamp(outerTrailHeadTaper, 0.01f, 0.5f), 1f), new Keyframe(1f, 0f));
        tr.sortingLayerID = layer;
        tr.sortingOrder = order;
        tr.gameObject.SetActive(true);
        return tr;
    }

    private void ReleaseTrail(TrailRenderer tr)
    {
        if (tr == null) return;
        tr.emitting = false;
        tr.Clear();
        tr.gameObject.SetActive(false);
        freeTrails.Push(tr);
    }
}
