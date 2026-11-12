using System.Collections.Generic;
using Game.Skills;
using UnityEngine;

public class BlockItemManager : MonoBehaviour
{
    public static BlockItemManager Instance { get; private set; }

    [Header("Item Prefab")]
    [Tooltip("BlockItemコンポーネントを持つPrefab（Gold/Life共通）")]
    [SerializeField] private BlockItem itemPrefab;

    [Header("Icons")]
    [Tooltip("Goldアイテムのアイコンスプライト")]
    [SerializeField] private Sprite goldIcon;
    [Tooltip("Lifeアイテムのアイコンスプライト（B3スキルアイコン推奨）")]
    [SerializeField] private Sprite lifeIcon;
    [Tooltip("フロアが回復した時の「+N」に出すアイコン（HPStatusHUDのFloorHPRowと同じ B2.png 推奨）。未設定ならLifeアイコン（ハート）を使う")]
    [SerializeField] private Sprite floorHealIcon;
    [Tooltip("フロアの回復アイコンの大きさの微調整の倍率。1で、HUDと同じくハートと同じ正方形の枠に収まる大きさ（横幅がハートと同じ）")]
    [Min(0.01f)] [SerializeField] private float floorHealIconScale = 1f;

    [Header("金貨袋・宝箱（2026/11/10。出る割合は AreaConfig の「アイテムの出る割合」）")]
    [Tooltip("金貨袋の画像。未設定ならコインの画像")]
    [SerializeField] private Sprite goldBagIcon;
    [Tooltip("宝箱の画像（閉じた絵・浮いている間）。未設定ならコインの画像")]
    [SerializeField] private Sprite treasureChestIcon;
    [Tooltip("宝箱の画像（開いた絵・取った瞬間に切り替える）。未設定なら切り替えない")]
    [SerializeField] private Sprite treasureChestOpenIcon;
    [Tooltip("金貨袋のGold＝コインと同じランダムの量×この倍率")]
    [Min(1)] [SerializeField] private int goldBagMultiplier = 3;
    [Tooltip("宝箱のGold＝コインと同じランダムの量×この倍率")]
    [Min(1)] [SerializeField] private int treasureChestMultiplier = 10;

    [Header("ゴールドシューズ（2026/11/10。効果は GoldenShoesBuff。出る割合は AreaConfig の「アイテムの出る割合」）")]
    [Tooltip("ゴールドシューズの画像（浮いている間）。未設定ならコインの画像")]
    [SerializeField] private Sprite goldenShoesIcon;
    [Tooltip("ゴールドシューズを取った時の音。未設定ならAreaConfigのLife収集時SE")]
    [SerializeField] private AudioClip goldenShoesCollectSE;
    [Tooltip("取った所に出す文字（日本語）。コインの「+N」と同じ出方")]
    [SerializeField] private string goldenShoesPopupLabelJa = "線ゲージ消費量半減"; // 2026/11/10 文言変更。開いているシーンに前の文言が残らないよう項目名も変えた
    [Tooltip("取った所に出す文字（英語）")]
    [SerializeField] private string goldenShoesPopupTextEn = "HALF LINE COST";
    [Tooltip("文字の大きさ（コインの数字の大きさに対する倍率）")]
    [Range(0.2f, 2f)] [SerializeField] private float goldenShoesPopupTextSize = 0.8f;

    [Header("アクセラレーター（2026/11/10。効果は GoldBoltBuff。取った時の音はゴールドシューズと同じ）")]
    [Tooltip("アクセラレーターの画像（浮いている間）。未設定ならコインの画像")]
    [SerializeField] private Sprite goldBoltIcon;
    [Tooltip("取った所に出す文字（日本語）")]
    [SerializeField] private string goldBoltPopupLabelJa = "反射弾速上昇"; // 2026/11/11 文言変更。シーンに保存済みの前の文言（反射弾スピード1.5倍）が使われないよう項目名も変えた
    [Tooltip("取った所に出す文字（英語）")]
    [SerializeField] private string acceleratorPopupTextEn = "REFLECT SPEED UP"; // 2026/11/12 日本語（反射弾速上昇）に合わせて数字をなくした。シーンに保存済みの前の文言（REFLECT SPEED x1.5）が使われないよう項目名も変えた

    [Header("マルチバウンド（2026/11/11。効果は MultiBoundBuff。取った時の音はゴールドシューズと同じ）")]
    [Tooltip("マルチバウンドの画像（浮いている間）。未設定ならコインの画像")]
    [SerializeField] private Sprite multiBoundIcon;
    [Tooltip("取った所に出す文字（日本語）")]
    [SerializeField] private string multiBoundPopupTextJa = "反射可能回数上昇";
    [Tooltip("取った所に出す文字（英語）")]
    [SerializeField] private string multiBoundPopupTextEn = "BOUNCE LIMIT UP";

    [Header("スパイラルショット（2026/11/11。効果は ShotItemManager。取った時の音はゴールドシューズと同じ）")]
    [Tooltip("スパイラルショットの画像（浮いている間）。未設定ならコインの画像")]
    [SerializeField] private Sprite spiralShotIcon;
    [Tooltip("取った所に出す文字（日本語）")]
    [SerializeField] private string spiralShotPopupTextJa = "反射弾ドリル化";
    [Tooltip("取った所に出す文字（英語）")]
    [SerializeField] private string spiralShotPopupTextEn = "DRILL SHOT";

    [Header("ブラストショット（2026/11/11。効果は ShotItemManager。取った時の音はゴールドシューズと同じ）")]
    [Tooltip("ブラストショットの画像（浮いている間）。未設定ならコインの画像")]
    [SerializeField] private Sprite blastShotIcon;
    [Tooltip("取った所に出す文字（日本語）")]
    [SerializeField] private string blastShotPopupTextJa = "反射弾爆発化";
    [Tooltip("取った所に出す文字（英語）")]
    [SerializeField] private string blastShotPopupTextEn = "BLAST SHOT";

    [Header("お食事券（2026/11/11。フード画面で1枚でフードを無料で買える。円で取ると複数枚）")]
    [Tooltip("お食事券の画像（浮いている間・取った所の「+N」）。未設定ならコインの画像")]
    [SerializeField] private Sprite foodTicketIcon;
    [Tooltip("1個で手に入る枚数（円で取った時はコインと同じ倍率がかかる）")]
    [Min(1)] [SerializeField] private int foodTicketAmount = 1;

    [Header("スプリットショット（2026/11/11。効果は ShotItemManager。取った時の音はゴールドシューズと同じ）")]
    [Tooltip("スプリットショットの画像（浮いている間）。未設定ならコインの画像")]
    [SerializeField] private Sprite splitShotIcon;
    [Tooltip("取った所に出す文字（日本語）")]
    [SerializeField] private string splitShotPopupTextJa = "反射弾分裂化";
    [Tooltip("取った所に出す文字（英語）")]
    [SerializeField] private string splitShotPopupTextEn = "SPLIT SHOT";

    [Header("回復の「+N」表示（ステージクリア時の回復・セルフヒール・ライフアイテムで共通）")]
    [Tooltip("ダンサーの位置からの「ハート +N」表示のずれ（ワールド座標）")]
    [SerializeField] private Vector2 dancerHealPopupOffset = new Vector2(0f, 1.4f);
    [Tooltip("フロアの位置からの「床アイコン +N」表示のずれ（ワールド座標）")]
    [SerializeField] private Vector2 floorHealPopupOffset = new Vector2(0f, 0.6f);

    /// <summary>フロアの回復アイコン（ItemFXManagerがフロアへ飛ぶ粒に使う）。未設定ならnull</summary>
    public Sprite FloorHealIcon => floorHealIcon;

    [Header("Popup")]
    [Tooltip("収集時のポップアップテキストPrefab")]
    [SerializeField] private BlockItemPopupText popupTextPrefab;
    [Range(0.01f, 1f)]
    [Tooltip("Goldポップアップのワールドスケール")]
    [SerializeField] private float popupWorldScaleGold = 0.05f;
    [Range(0.01f, 1f)]
    [Tooltip("Lifeポップアップのワールドスケール")]
    [SerializeField] private float popupWorldScaleLife = 0.05f;

    private readonly List<BlockItem> activeItems = new List<BlockItem>();
    // ⑩ ポップアップの使い回し（表示が終わったものをしまっておき、次の取得で再利用する）
    private readonly Stack<BlockItemPopupText> freePopups = new Stack<BlockItemPopupText>();
    private EnemySpawner enemySpawner;
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        enemySpawner = FindObjectOfType<EnemySpawner>();
    }

    private void OnEnable()
    {
        WallHealth.OnAnyBlockBroken += OnBlockBroken;
        EnemySpawner.OnStageStarted += OnStageStarted;
        EnemySpawner.OnStageCleared += OnStageClearedMagnet;
    }

    private void OnDisable()
    {
        WallHealth.OnAnyBlockBroken -= OnBlockBroken;
        EnemySpawner.OnStageStarted -= OnStageStarted;
        EnemySpawner.OnStageCleared -= OnStageClearedMagnet;
    }

    /// <summary>パッシブ「アイテムマグネット」：Stageクリアの瞬間に、残っているアイテムを全部取る（線で取った時と同じ。全Stage・Area10も）</summary>
    private void OnStageClearedMagnet(int stageIndex)
    {
        if (!MissionPassives.Has(MissionPassiveType.AutoItemCollect)) return;
        // ★取ると OnItemCollectionStarted で activeItems から外れるので、先に写しを取ってから回す
        BlockItem[] snapshot = activeItems.ToArray();
        int n = 0;
        foreach (BlockItem item in snapshot)
        {
            if (item == null || item.IsCollected) continue;
            item.CollectByMagnet();
            n++;
        }
        if (n > 0) Debug.Log($"[Mission] アイテムマグネット：Stage{stageIndex + 1}クリアで{n}個取得");
    }

    // =========================================================
    // イベントハンドラ
    // =========================================================

    /// <summary>
    /// 出すアイテムの種類を決める（2026/11/10）。AreaConfig の「アイテムの出る割合」の重みの比で選ぶ。
    /// 一覧が空、または重みが全部0なら、旧方式（Gold Drop Rate の%でコイン、残りでハート）
    /// </summary>
    private static BlockItem.ItemType PickItemType(StageBlockConfig cfg)
    {
        var list = cfg.itemDropWeights;
        float total = 0f;
        if (list != null) foreach (var w in list) if (w != null && w.weight > 0f) total += w.weight;
        if (total <= 0f)
            return Random.Range(0f, 100f) < cfg.goldDropRate ? BlockItem.ItemType.Gold : BlockItem.ItemType.Life;

        float r = Random.Range(0f, total);
        foreach (var w in list)
        {
            if (w == null || w.weight <= 0f) continue;
            if (r < w.weight) return w.type;
            r -= w.weight;
        }
        // 端数で抜けた時は、重みのある最後の種類
        for (int i = list.Count - 1; i >= 0; i--) if (list[i] != null && list[i].weight > 0f) return list[i].type;
        return BlockItem.ItemType.Gold;
    }

    private void OnBlockBroken(Vector3 pos, bool shouldDrop)
    {
        if (!shouldDrop) return;
        StageBlockConfig cfg = enemySpawner?.CurrentAreaConfig?.stageBlockConfig;
        if (cfg == null || itemPrefab == null) return;

        BlockItem.ItemType type = PickItemType(cfg);
        bool isGold = BlockItem.IsGoldKind(type);

        int amount;
        Sprite icon;
        Sprite collectSprite = null;
        AudioClip se;
        float size;

        if (isGold)
        {
            // 金貨袋・宝箱は、コインと同じランダムの量に倍率をかける。音はコインと同じ
            int coin = Random.Range(cfg.goldDropMin, cfg.goldDropMax + 1);
            se = cfg.itemCollectSEGold;
            switch (type)
            {
                case BlockItem.ItemType.GoldBag:
                    amount = coin * goldBagMultiplier;
                    icon = goldBagIcon != null ? goldBagIcon : goldIcon;
                    size = cfg.goldBagItemSize;
                    break;
                case BlockItem.ItemType.TreasureChest:
                    amount = coin * treasureChestMultiplier;
                    icon = treasureChestIcon != null ? treasureChestIcon : goldIcon;
                    collectSprite = treasureChestOpenIcon;
                    size = cfg.treasureChestItemSize;
                    break;
                default:
                    amount = coin;
                    icon = goldIcon;
                    size = cfg.goldItemSize;
                    break;
            }
        }
        else if (type == BlockItem.ItemType.FoodTicket)
        {
            amount = foodTicketAmount;
            icon = foodTicketIcon != null ? foodTicketIcon : goldIcon;
            se = cfg.itemCollectSEGold; // 音はコインと同じ
            size = cfg.foodTicketItemSize;
        }
        else if (type == BlockItem.ItemType.SplitShot)
        {
            amount = 0;
            icon = splitShotIcon != null ? splitShotIcon : goldIcon;
            se = goldenShoesCollectSE != null ? goldenShoesCollectSE : cfg.itemCollectSELife; // 音はゴールドシューズと同じ
            size = cfg.splitShotItemSize;
        }
        else if (type == BlockItem.ItemType.BlastShot)
        {
            amount = 0;
            icon = blastShotIcon != null ? blastShotIcon : goldIcon;
            se = goldenShoesCollectSE != null ? goldenShoesCollectSE : cfg.itemCollectSELife; // 音はゴールドシューズと同じ
            size = cfg.blastShotItemSize;
        }
        else if (type == BlockItem.ItemType.SpiralShot)
        {
            amount = 0;
            icon = spiralShotIcon != null ? spiralShotIcon : goldIcon;
            se = goldenShoesCollectSE != null ? goldenShoesCollectSE : cfg.itemCollectSELife; // 音はゴールドシューズと同じ
            size = cfg.spiralShotItemSize;
        }
        else if (type == BlockItem.ItemType.MultiBound)
        {
            amount = 0;
            icon = multiBoundIcon != null ? multiBoundIcon : goldIcon;
            se = goldenShoesCollectSE != null ? goldenShoesCollectSE : cfg.itemCollectSELife; // 音はゴールドシューズと同じ
            size = cfg.multiBoundItemSize;
        }
        else if (type == BlockItem.ItemType.GoldBolt)
        {
            amount = 0;
            icon = goldBoltIcon != null ? goldBoltIcon : goldIcon;
            se = goldenShoesCollectSE != null ? goldenShoesCollectSE : cfg.itemCollectSELife; // 音はゴールドシューズと同じ
            size = cfg.goldBoltItemSize;
        }
        else if (type == BlockItem.ItemType.GoldenShoes)
        {
            amount = 0;
            icon = goldenShoesIcon != null ? goldenShoesIcon : goldIcon;
            se = goldenShoesCollectSE != null ? goldenShoesCollectSE : cfg.itemCollectSELife;
            size = cfg.goldenShoesItemSize;
        }
        else
        {
            amount = cfg.lifeDropAmount;
            icon = lifeIcon;
            se = cfg.itemCollectSELife;
            size = cfg.lifeItemSize;
        }

        BlockItem item = Instantiate(itemPrefab, pos, Quaternion.identity);
        item.itemType = type;
        item.amount = amount;
        item.icon = icon;
        item.collectSprite = collectSprite;
        item.collectSE = se;
        item.baseScale = size;

        // アイテムのWorld上のビジュアルを設定
        SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = icon;

        activeItems.Add(item);
        BreakFXManager.NotifyItemSpawned(pos, isGold || BlockItem.IsBuffKind(type) || BlockItem.IsShotKind(type) || type == BlockItem.ItemType.FoodTicket); // ゴールドシューズ・アクセラレーターも金色の柱 // アイテムが出た所の光の柱とキラキラ
    }

    private void OnStageStarted(int stageIndex)
    {
        ClearAllItems();
    }

    // =========================================================
    // アイテム管理
    // =========================================================

    public void ClearAllItems()
    {
        foreach (BlockItem item in activeItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        activeItems.Clear();
    }

    /// <summary>現在フィールドに残っている未収集アイテムをアルファフェードで消す（Area10ボスラッシュのボス切替専用）。</summary>
    public void FadeOutAllItems(float duration)
    {
        // ★item.FadeOutAndDestroy()内部でOnItemCollectionStarted()がactiveItemsを変更するため、
        //   列挙中の同一リストをforeachで回すと例外になる。スナップショットを取ってから空にする。
        BlockItem[] snapshot = activeItems.ToArray();
        activeItems.Clear();
        foreach (BlockItem item in snapshot)
        {
            if (item != null) item.FadeOutAndDestroy(duration);
        }
    }

    /// <summary>ライン収集でアイテムが収集を開始した時に呼ばれる（リストから除外）</summary>
    public void OnItemCollectionStarted(BlockItem item)
    {
        activeItems.Remove(item);
    }

    /// <summary>円成立時に円内のアイテムを一括収集（StrokeManagerから呼ばれる）</summary>
    public void CollectItemsInCircle(Bounds circleBounds, Vector3 circleCenter)
    {
        int collected = 0;
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            BlockItem item = activeItems[i];
            if (item == null) { activeItems.RemoveAt(i); continue; }
            if (item.IsCollected) { activeItems.RemoveAt(i); continue; }
            if (circleBounds.Contains(item.transform.position))
            {
                activeItems.RemoveAt(i);
                item.CollectFromCircle(circleCenter);
                collected++;
            }
        }
        // ⑬ 円で1つ以上取った時は、円の中心から大きな虹色の輪＋画面が一瞬光る（円1つにつき1回）
        if (collected > 0)
            ItemFXManager.NotifyCircleBonus(circleCenter, Mathf.Max(circleBounds.extents.x, circleBounds.extents.y));
    }

    // =========================================================
    // 効果適用
    // =========================================================

    /// <summary>BlockItemの収集完了時に呼ばれる（BlockItemから呼ばれる）</summary>
    /// <param name="isCircle">円で取った時true（ポップアップを強調する）</param>
    private void ReturnPopup(BlockItemPopupText popup)
    {
        if (popup == null) return;
        popup.gameObject.SetActive(false);
        freePopups.Push(popup);
    }

    public void ApplyEffect(BlockItem.ItemType type, int amount, Vector3 pos, Sprite icon, AudioClip se, bool isCircle = false)
    {
        // SE再生
        if (se != null && audioSource != null)
        {
            float vol = SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;
            audioSource.PlayOneShot(se, vol);
        }

        // 効果適用
        if (type == BlockItem.ItemType.FoodTicket) // お食事券：このプレイの分にためる（Areaクリアで持ち物へ）。「+N」はコインと同じ出し方
        {
            FoodTickets.AddSession(amount);
            MissionProgressManager.OnFoodTicketCollected();
            ShowPopup(BlockItem.ItemType.Gold, amount, pos, icon, isCircle);
            return;
        }
        if (BlockItem.IsShotKind(type)) // 弾の性質を変えるアイテム（スパイラルショット等。ShotItemManager）
        {
            if (type == BlockItem.ItemType.SpiralShot) MissionProgressManager.OnSpiralShotCollected();
            if (type == BlockItem.ItemType.BlastShot) MissionProgressManager.OnBlastShotCollected();
            if (type == BlockItem.ItemType.SplitShot) MissionProgressManager.OnSplitShotCollected();
            if (ShotItemManager.Instance != null) ShotItemManager.Instance.Acquire(BlockItem.ShotTypeOf(type), isCircle); // 円で取った時は回数が増える（2026/11/12）
            else Debug.LogWarning("[BlockItemManager] ShotItemManager がシーンにありません（Tools/アイテム/「弾を変えるアイテムの表示を作る」を05_Gameで実行してください）");
            bool enS = Game.Localization.LocalizationManager.CurrentLanguageStatic == Game.Localization.GameLanguage.English;
            BlockItemPopupText sp = GetPopup(pos);
            string shotText = type == BlockItem.ItemType.BlastShot ? (enS ? blastShotPopupTextEn : blastShotPopupTextJa)
                            : type == BlockItem.ItemType.SplitShot ? (enS ? splitShotPopupTextEn : splitShotPopupTextJa)
                                                                   : (enS ? spiralShotPopupTextEn : spiralShotPopupTextJa);
            if (sp != null) sp.ShowText(shotText, icon, isCircle, ReturnPopup, goldenShoesPopupTextSize);
            return;
        }
        if (type == BlockItem.ItemType.MultiBound) // マルチバウンド：跳ね返せる回数が一定時間2倍（MultiBoundBuff）
        {
            MissionProgressManager.OnMultiBoundCollected();
            if (MultiBoundBuff.Instance != null) MultiBoundBuff.Instance.Activate(isCircle); // 円で取った時は効果時間が長い（2026/11/12）
            else Debug.LogWarning("[BlockItemManager] MultiBoundBuff がシーンにありません（Tools/アイテム/「マルチバウンドの表示を作る」を05_Gameで実行してください）");
            bool enM = Game.Localization.LocalizationManager.CurrentLanguageStatic == Game.Localization.GameLanguage.English;
            BlockItemPopupText mp = GetPopup(pos);
            if (mp != null) mp.ShowText(enM ? multiBoundPopupTextEn : multiBoundPopupTextJa, icon, isCircle, ReturnPopup, goldenShoesPopupTextSize);
            return;
        }
        if (type == BlockItem.ItemType.GoldBolt) // アクセラレーター：跳ね返した弾が一定時間1.5倍速（GoldBoltBuff）
        {
            MissionProgressManager.OnGoldBoltCollected();
            if (GoldBoltBuff.Instance != null) GoldBoltBuff.Instance.Activate(isCircle); // 円で取った時は効果時間が長い（2026/11/12）
            else Debug.LogWarning("[BlockItemManager] GoldBoltBuff がシーンにありません（Tools/アイテム/「アクセラレーターの表示を作る」を05_Gameで実行してください）");
            bool enB = Game.Localization.LocalizationManager.CurrentLanguageStatic == Game.Localization.GameLanguage.English;
            BlockItemPopupText bp = GetPopup(pos);
            if (bp != null) bp.ShowText(enB ? acceleratorPopupTextEn : goldBoltPopupLabelJa, icon, isCircle, ReturnPopup, goldenShoesPopupTextSize);
            return;
        }
        if (type == BlockItem.ItemType.GoldenShoes) // ゴールドシューズ：線の消費が一定時間半分（GoldenShoesBuff）
        {
            MissionProgressManager.OnGoldenShoesCollected();
            if (GoldenShoesBuff.Instance != null) GoldenShoesBuff.Instance.Activate(isCircle); // 円で取った時は効果時間が長い（2026/11/12）
            else Debug.LogWarning("[BlockItemManager] GoldenShoesBuff がシーンにありません（Tools/アイテム/「ゴールドシューズの表示を作る」を05_Gameで実行してください）");
            // 取った所に「靴のアイコン＋線ゲージ消費量半減」（コイン・ハートの「+N」と同じ出方）
            bool en = Game.Localization.LocalizationManager.CurrentLanguageStatic == Game.Localization.GameLanguage.English;
            BlockItemPopupText tp = GetPopup(pos);
            if (tp != null) tp.ShowText(en ? goldenShoesPopupTextEn : goldenShoesPopupLabelJa, icon, isCircle, ReturnPopup, goldenShoesPopupTextSize);
            return;
        }
        if (BlockItem.IsGoldKind(type)) // コイン・金貨袋・宝箱
        {
            GoldManager.Instance?.AddSessionGold(amount);
        }
        else
        {
            PixelDancerController dancer = FindFirstObjectByType<PixelDancerController>();
            int dancerHealed = 0, floorHealed = 0;
            if (dancer != null) { int b = dancer.CurrentHP; dancer.Heal(amount); dancerHealed = dancer.CurrentHP - b; }
            FloorHealth floor = FindFirstObjectByType<FloorHealth>();
            if (floor != null) { int b = floor.CurrentHP; floor.Heal(amount); floorHealed = floor.CurrentHP - b; }

            // ダンサーとフロアの上の「+N」と回復エフェクト：粒が飛んでいる時は着いた時に出す（ItemFXManager）。飛んでいない方はここですぐ出す
            var pending = ItemFXManager.TakePendingLifeHeal();
            if (pending != null)
            {
                pending.dancerHealed = dancerHealed;
                pending.floorHealed = floorHealed;
                pending.ready = true;
            }
            if (pending == null || !pending.dancerTargeted) PlayDancerHealFx(dancerHealed);
            if (pending == null || !pending.floorTargeted) PlayFloorHealFx(floorHealed);
            return; // ライフはアイテムの位置には「+N」を出さない
        }

        // ポップアップ表示（ゴールド）
        // 宝箱は「+N」の横に開いた絵を出す（取った後なので。2026/11/11）
        Sprite popupIcon = type == BlockItem.ItemType.TreasureChest && treasureChestOpenIcon != null ? treasureChestOpenIcon : icon;
        ShowPopup(type, amount, pos, popupIcon, isCircle);
    }

    /// <summary>ダンサーが回復した時の演出（回復エフェクト＋ダンサーの上に「ハート +N」）。healedが0以下なら何もしない</summary>
    public void PlayDancerHealFx(int healed, bool playEffect = true)
    {
        if (healed <= 0) return;
        var dancer = FindFirstObjectByType<PixelDancerController>();
        if (dancer == null) return;
        if (playEffect) dancer.PlayHealEffect();
        ShowHealPopup(dancer.transform.position + (Vector3)dancerHealPopupOffset, healed, false);
    }

    /// <summary>フロアが回復した時の演出（回復エフェクト＋フロアの上に「床アイコン +N」）。healedが0以下なら何もしない</summary>
    public void PlayFloorHealFx(int healed, bool playEffect = true)
    {
        if (healed <= 0) return;
        var floor = FindFirstObjectByType<FloorHealth>();
        if (floor == null) return;
        if (playEffect) floor.PlayHealEffect();
        ShowHealPopup(floor.transform.position + (Vector3)floorHealPopupOffset, healed, true);
    }

    /// <summary>回復の「+N」表示だけを出す（回復処理はしない）</summary>
    /// <param name="isFloor">trueならフロアのアイコン（floorHealIcon）を出す。falseならダンサー用のハート</param>
    public void ShowHealPopup(Vector3 pos, int amount, bool isFloor = false)
    {
        if (isFloor && floorHealIcon != null)
        {
            // HUDと同じ考え方：ハートと同じ正方形の枠に収まる大きさにそろえる（長い方の辺をハートの長い方の辺に合わせる）
            float fit = 1f;
            if (lifeIcon != null)
            {
                Vector3 fs = floorHealIcon.bounds.size, hs = lifeIcon.bounds.size;
                float floorMax = Mathf.Max(fs.x, fs.y), heartMax = Mathf.Max(hs.x, hs.y);
                if (floorMax > 0.0001f) fit = heartMax / floorMax;
            }
            ShowPopup(BlockItem.ItemType.Life, amount, pos, floorHealIcon, false, fit * floorHealIconScale);
        }
        else ShowPopup(BlockItem.ItemType.Life, amount, pos, lifeIcon, false);
    }

    private void ShowPopup(BlockItem.ItemType type, int amount, Vector3 pos, Sprite icon, bool isCircle, float iconSizeMultiplier = 1f)
    {
        if (popupTextPrefab != null)
        {
            BlockItemPopupText popup = null;
            while (freePopups.Count > 0 && popup == null) popup = freePopups.Pop();
            if (popup == null) popup = Instantiate(popupTextPrefab, pos, Quaternion.identity);
            popup.transform.SetPositionAndRotation(pos, Quaternion.identity);
            popup.gameObject.SetActive(true);
            float popupScale = BlockItem.IsGoldKind(type) ? popupWorldScaleGold : popupWorldScaleLife;
            popup.transform.localScale = Vector3.one * popupScale;
            popup.Show(type, amount, icon, isCircle, ReturnPopup, iconSizeMultiplier);
        }
    }

    /// <summary>ポップアップを1つ用意する（使い回し・Goldと同じ大きさ）。Prefabが無ければnull</summary>
    private BlockItemPopupText GetPopup(Vector3 pos)
    {
        if (popupTextPrefab == null) return null;
        BlockItemPopupText popup = null;
        while (freePopups.Count > 0 && popup == null) popup = freePopups.Pop();
        if (popup == null) popup = Instantiate(popupTextPrefab, pos, Quaternion.identity);
        popup.transform.SetPositionAndRotation(pos, Quaternion.identity);
        popup.gameObject.SetActive(true);
        popup.transform.localScale = Vector3.one * popupWorldScaleGold;
        return popup;
    }

#if UNITY_EDITOR
    /// <summary>フロアの回復アイコンに B2.png（HPStatusHUD の FloorHPRow と同じ画像）を設定する</summary>
    [ContextMenu("フロアの回復アイコンを設定（B2.png）")]
    private void SetFloorHealIconFromB2()
    {
        const string path = "Assets/Art/Skill/B2.png";
        Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null) { Debug.LogError("[BlockItemManager] " + path + " のSpriteが見つかりません"); return; }
        UnityEditor.Undo.RecordObject(this, "フロアの回復アイコンを設定");
        floorHealIcon = sp;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log("[BlockItemManager] Floor Heal Icon に " + path + " を設定しました。シーンを保存してください。");
    }

    /// <summary>
    /// 金貨袋・宝箱の画像を設定する（2026/11/10）。
    /// ・宝箱の2枚は Sprite Mode を Single にする（2枚を同じ500×500の枠で使い、閉じた絵→開いた絵に切り替えた時に位置と大きさがずれないように）
    /// ・金貨袋はコイン（gold.png）と同じく Multiple のまま（絵の部分だけを切り出したスプライト）
    /// </summary>
    [ContextMenu("金貨袋・宝箱の画像を設定（Art/Gold）")]
    private void SetGoldBagAndChestIcons()
    {
        const string bagPath = "Assets/Art/Gold/金貨袋.png";
        const string closedPath = "Assets/Art/Gold/宝箱・閉じた絵.png";
        const string openPath = "Assets/Art/Gold/宝箱・開いた絵.png";

        foreach (var p in new[] { closedPath, openPath })
        {
            var imp = UnityEditor.AssetImporter.GetAtPath(p) as UnityEditor.TextureImporter;
            if (imp == null) { Debug.LogError("[BlockItemManager] " + p + " が見つかりません"); return; }
            if (imp.textureType != UnityEditor.TextureImporterType.Sprite || imp.spriteImportMode != UnityEditor.SpriteImportMode.Single)
            {
                imp.textureType = UnityEditor.TextureImporterType.Sprite;
                imp.spriteImportMode = UnityEditor.SpriteImportMode.Single;
                imp.SaveAndReimport();
            }
        }

        Sprite bag = FirstSprite(bagPath);
        Sprite closed = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(closedPath);
        Sprite open = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(openPath);
        if (bag == null || closed == null || open == null)
        {
            Debug.LogError($"[BlockItemManager] 画像が読めません（金貨袋={bag != null}・閉じた宝箱={closed != null}・開いた宝箱={open != null}）");
            return;
        }
        UnityEditor.Undo.RecordObject(this, "金貨袋・宝箱の画像を設定");
        goldBagIcon = bag;
        treasureChestIcon = closed;
        treasureChestOpenIcon = open;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[BlockItemManager] 金貨袋={bag.name}・宝箱（閉じた絵）={closed.name}・宝箱（開いた絵）={open.name} を設定しました（宝箱の2枚はSingle）。シーンを保存してください。");
    }

    /// <summary>ゴールドシューズの画像（Art/Gold/ゴールドシューズ.png・Multipleのまま）と取った時の音（Audio/Gold/シャキーン3）を設定する</summary>
    [ContextMenu("ゴールドシューズの画像と音を設定（ゴールドシューズ.png・シャキーン3）")]
    private void SetGoldenShoesIcon()
    {
        const string sePath = "Assets/Audio/Gold/シャキーン3.mp3";
        Sprite sp = GoldenShoesBuff.LoadShoesSpriteForEditor();
        AudioClip se = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(sePath);
        if (sp == null) return;
        if (se == null) { Debug.LogError("[BlockItemManager] " + sePath + " が見つかりません"); return; }
        UnityEditor.Undo.RecordObject(this, "ゴールドシューズの画像と音を設定");
        goldenShoesIcon = sp;
        goldenShoesCollectSE = se;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[BlockItemManager] ゴールドシューズの画像={sp.name}・取った時の音={se.name} を設定しました。シーンを保存してください。");
    }

    /// <summary>アクセラレーターの画像（Art/Gold/アクセラレーター.png・Multipleのまま）を設定する。取った時の音はゴールドシューズと同じ（Golden Shoes Collect SE）</summary>
    [ContextMenu("アクセラレーターの画像を設定（Art/Gold/アクセラレーター.png）")]
    private void SetGoldBoltIcon()
    {
        Sprite sp = GoldBoltBuff.LoadBoltSpriteForEditor();
        if (sp == null) return;
        UnityEditor.Undo.RecordObject(this, "アクセラレーターの画像を設定");
        goldBoltIcon = sp;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[BlockItemManager] アクセラレーターの画像={sp.name} を設定しました。シーンを保存してください。");
    }

    /// <summary>マルチバウンドの画像（Art/Gold/マルチバウンド.png・Multipleのまま）を設定する。取った時の音はゴールドシューズと同じ</summary>
    [ContextMenu("マルチバウンドの画像を設定（Art/Gold/マルチバウンド.png）")]
    private void SetMultiBoundIcon()
    {
        Sprite sp = MultiBoundBuff.LoadMultiBoundSpriteForEditor();
        if (sp == null) return;
        UnityEditor.Undo.RecordObject(this, "マルチバウンドの画像を設定");
        multiBoundIcon = sp;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[BlockItemManager] マルチバウンドの画像={sp.name} を設定しました。シーンを保存してください。");
    }

    /// <summary>スパイラルショットの画像（Art/Gold/スパイラルショット.png・Multipleのまま）を設定する</summary>
    [ContextMenu("スパイラルショットの画像を設定（Art/Gold/スパイラルショット.png）")]
    private void SetSpiralShotIcon()
    {
        Sprite sp = ItemBuffBase.LoadSpriteForEditor(ShotItemPaths.Spiral);
        if (sp == null) return;
        UnityEditor.Undo.RecordObject(this, "スパイラルショットの画像を設定");
        spiralShotIcon = sp;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[BlockItemManager] スパイラルショットの画像={sp.name} を設定しました。シーンを保存してください。");
    }

    /// <summary>ブラストショットの画像（Art/Gold/ブラストショット.png・Multipleのまま）を設定する</summary>
    [ContextMenu("ブラストショットの画像を設定（Art/Gold/ブラストショット.png）")]
    private void SetBlastShotIcon()
    {
        Sprite sp = ItemBuffBase.LoadSpriteForEditor(ShotItemPaths.Blast);
        if (sp == null) return;
        UnityEditor.Undo.RecordObject(this, "ブラストショットの画像を設定");
        blastShotIcon = sp;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[BlockItemManager] ブラストショットの画像={sp.name} を設定しました。シーンを保存してください。");
    }

    /// <summary>スプリットショットの画像（Art/Gold/スプリットショット.png・Multipleのまま）を設定する</summary>
    [ContextMenu("スプリットショットの画像を設定（Art/Gold/スプリットショット.png）")]
    private void SetSplitShotIcon()
    {
        Sprite sp = ItemBuffBase.LoadSpriteForEditor(ShotItemPaths.Split);
        if (sp == null) return;
        UnityEditor.Undo.RecordObject(this, "スプリットショットの画像を設定");
        splitShotIcon = sp;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[BlockItemManager] スプリットショットの画像={sp.name} を設定しました。シーンを保存してください。");
    }

    public const string FoodTicketSpritePath = "Assets/Art/Gold/お食事券.png";

    /// <summary>お食事券の画像（Art/Gold/お食事券.png・Multipleのまま）を設定する</summary>
    [ContextMenu("お食事券の画像を設定（Art/Gold/お食事券.png）")]
    private void SetFoodTicketIcon()
    {
        Sprite sp = ItemBuffBase.LoadSpriteForEditor(FoodTicketSpritePath);
        if (sp == null) return;
        UnityEditor.Undo.RecordObject(this, "お食事券の画像を設定");
        foodTicketIcon = sp;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[BlockItemManager] お食事券の画像={sp.name} を設定しました。シーンを保存してください。");
    }

    private static Sprite FirstSprite(string path)
    {
        foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) return s;
        return null;
    }
#endif
}
