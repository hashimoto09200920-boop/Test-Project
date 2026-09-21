using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyStats : MonoBehaviour
{
    [SerializeField] private int maxHp = 3;
    private int hp;

    [Header("HP Link (Optional)")]
    [Tooltip("設定すると、このEnemyStatsへのDamage/Healを全てここで指定した相手へ転送し、" +
             "自分自身のHPは変化させない（＝HPプールを共有する）。未設定なら従来通り自分自身のHPを使う。" +
             "既存の全エネミーはこの値を使わないため、設定しない限り挙動は変わらない")]
    [SerializeField] private EnemyStats damageRedirectTarget;
    [Tooltip("このEnemyStatsが倒れた瞬間に、一緒に倒す相手（複数指定可）。" +
             "HPプールを共有する相手同士で、片方が0になったらもう片方も強制的に倒す用途")]
    [SerializeField] private EnemyStats[] linkedDeathTargets;

    public int HP => damageRedirectTarget != null ? damageRedirectTarget.HP : hp;
    public int MaxHP => damageRedirectTarget != null ? damageRedirectTarget.MaxHP : maxHp;

    // =========================================================
    // Death Effects
    // =========================================================
    [Header("Death Effects")]
    [Tooltip("撃破時のエフェクトプレハブ（爆発など）")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Tooltip("時間経過消滅時のエフェクトプレハブ（フェードアウトなど）")]
    [SerializeField] private GameObject expireEffectPrefab;

    [Tooltip("エフェクトの自動削除時間（秒）")]
    [SerializeField] private float effectDestroySeconds = 2f;

    [Header("Fade Settings")]
    [Tooltip("出現時のフェードイン時間（秒）\n0以下で無効化")]
    [SerializeField] private float fadeInDuration = 0.5f;

    [Tooltip("時間経過消滅時のフェードアウト時間（秒）")]
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("Death SE")]
    [Tooltip("撃破時のSE")]
    [SerializeField] private AudioClip deathSeClip;

    [Tooltip("時間経過消滅時のSE")]
    [SerializeField] private AudioClip expireSeClip;

    [Tooltip("SEの音量")]
    [SerializeField] private float seVolume = 1f;

    // =========================================================
    // Gold
    // =========================================================
    private int goldReward = 0;

    public void ApplyGoldReward(int value) { goldReward = value; }

    // =========================================================
    // EnemySpawner通知用
    // =========================================================
    private EnemySpawner spawner;

    public void SetSpawner(EnemySpawner spawner)
    {
        this.spawner = spawner;
    }

    public EnemySpawner GetSpawner() => spawner;

    // =========================================================
    // DeathVFX カスタム設定（EnemySpawner から注入される）
    // =========================================================
    private bool _useCustomDeathVfx = false;
    private DeathVfxConfig _deathVfxConfig;

    public void ApplyDeathVfxConfig(bool useCustom, DeathVfxConfig config)
    {
        _useCustomDeathVfx = useCustom;
        _deathVfxConfig    = config;
    }

    public void SetDeathEffectPrefab(GameObject prefab)
    {
        if (prefab != null) deathEffectPrefab = prefab;
    }

    // =========================================================
    // 外部から設定するHPダメージ倍率（GravePoleEnemy のオーブバフ等で使用）
    // デフォルト1f = 倍率なし。既存エネミーには影響しない。
    // =========================================================
    [System.NonSerialized]
    public float incomingDamageMultiplier = 1f;

    // 死亡ガード（二重呼び出し防止）
    // =========================================================
    private bool isDead = false;

    // =========================================================
    // サブパーツ管理（複数パーツ敵用）
    // =========================================================
    private List<GameObject> subParts = new List<GameObject>();

    // HP％を取得（0～100）
    public float GetHpPercentage()
    {
        if (damageRedirectTarget != null) return damageRedirectTarget.GetHpPercentage();
        if (maxHp <= 0) return 0f;
        return ((float)hp / maxHp) * 100f;
    }

    private void Awake()
    {
        // 手置きテストやEnemySpawner未使用でも破綻しないよう初期化
        maxHp = Mathf.Max(1, maxHp);
        hp = maxHp;
    }

    private void Start()
    {
        // フェードイン効果を開始
        if (fadeInDuration > 0f)
        {
            StartCoroutine(FadeIn());
        }
    }

    public void ApplyMaxHp(int value)
    {
        maxHp = Mathf.Max(1, value);
        hp = maxHp;
    }

    public void SetFadeInDuration(float duration) => fadeInDuration = duration;

    /// <summary>共有Prefabのシリアライズ値を変えずに、このインスタンスだけフェードアウト秒数を上書きする（Area10ボスラッシュの時間切れ強制クリア専用）。</summary>
    public void SetFadeOutDuration(float duration) => fadeOutDuration = duration;

    /// <summary>
    /// HPプールの共有先を実行時に設定する（Tsukuyomi/Susanooのように別々にSpawnされる独立したPrefab同士は
    /// Prefab上で直接参照をドラッグ設定できないため、各コントローラーのStart()等から呼ぶ想定）
    /// </summary>
    public void SetDamageRedirectTarget(EnemyStats target) => damageRedirectTarget = target;

    /// <summary>
    /// 実際にダメージ処理が行われるEnemyShieldを返す（HP表示用）。damageRedirectTargetが設定されている
    /// 場合はダメージも転送先側で処理されるため、転送先のEnemyShieldを返す。それ以外は自分自身のもの
    /// </summary>
    public EnemyShield GetEffectiveShield() =>
        damageRedirectTarget != null ? damageRedirectTarget.GetComponent<EnemyShield>() : GetComponent<EnemyShield>();

    /// <summary>片方が倒れた時に一緒に倒す相手を実行時に追加登録する</summary>
    public void AddLinkedDeathTarget(EnemyStats target)
    {
        if (target == null) return;
        List<EnemyStats> list = new List<EnemyStats>(linkedDeathTargets ?? new EnemyStats[0]);
        if (!list.Contains(target)) list.Add(target);
        linkedDeathTargets = list.ToArray();
    }

    /// <summary>HPに実ダメージが入った時に発火するイベント。ボス固有の演出で使用。</summary>
    public event System.Action onDamageTaken;

    /// <summary>プレイヤーに倒された瞬間に発火するイベント（エフェクトスポーン前）。</summary>
    public event System.Action onKilled;

    /// <summary>
    /// 死亡エフェクトのスポーン位置を上書きする。
    /// 設定すると deathEffectPrefab をこれらの位置それぞれに生成する。
    /// null または空配列の場合は通常通り transform.position に1つ生成。
    /// </summary>
    [System.NonSerialized] public Vector3[] deathEffectPositions;

    public void Damage(int amount, bool isJust = false)
    {
        // ★HPプールを共有する相手が設定されている場合、ダメージ処理は全てそちら側で行う
        //   （シールド消費もdamageRedirectTarget側のGetComponent<EnemyShield>()で行われるため、
        //   自動的にシールドも共有される。自分自身のhpは一切変化させない）
        if (damageRedirectTarget != null)
        {
            damageRedirectTarget.Damage(amount, isJust);
            return;
        }

        // ★シールドがあればシールドから消費
        EnemyShield shield = GetComponent<EnemyShield>();
        int damageToHp = amount;

        if (shield != null && shield.IsEnabled)
        {
            damageToHp = shield.ApplyDamage(amount);
        }

        // 残りのダメージをHPに適用（incomingDamageMultiplier でバフ倍率を適用）
        int actualDamage = Mathf.Max(0, Mathf.RoundToInt(damageToHp * incomingDamageMultiplier));
        if (actualDamage > 0)
        {
            GetComponent<EnemySpriteShake>()?.TriggerShake(isJust);
            GetComponent<EnemyDamageReceiver>()?.TriggerHitSprite();
            onDamageTaken?.Invoke();
        }

        // SessionStats 記録
        if (shield != null && shield.IsEnabled && shield.LastShieldDamageDealt > 0)
            SessionStats.AddShieldDamage(shield.LastShieldDamageDealt);
        SessionStats.AddHpDamage(actualDamage);

        hp -= actualDamage;
        if (hp <= 0)
        {
            Die(isKilled: true);
        }
    }

    public void Heal(int amount)
    {
        if (damageRedirectTarget != null)
        {
            damageRedirectTarget.Heal(amount);
            return;
        }
        if (hp <= 0 || amount <= 0) return;
        hp = Mathf.Min(hp + amount, maxHp);
    }

    /// <summary>
    /// 敵を消滅させる
    /// </summary>
    /// <param name="isKilled">true=プレイヤーに倒された, false=時間経過で消滅</param>
    public void Die(bool isKilled)
    {
        if (isDead) return;
        isDead = true;

        // ★EnemyShooter/EnemyMoverは下のisKilled分岐やFadeOutAndDestroy()で無効化されるが、
        //   MarshalController/GuardBeastController等、独自のコルーチンで弾を撃つカスタム
        //   コントローラーはこれらの対象外のため、死亡処理に入っても発射を続けてしまうことがあった
        //   （時間切れによるステージクリア時、FadeOutAllBullets()はこの瞬間の弾しか消さないため、
        //   フェードアウト中に新しく撃たれた弾が次のステージまで残ってしまうバグの原因だった）。
        //   個別のコントローラーを1つずつ直す代わりに、死亡処理に入った瞬間、このGameObject上の
        //   自分以外の全スクリプトのコルーチンをここで一律停止することで、発射元を止める
        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
        {
            if (mb != null && mb != this)
            {
                mb.StopAllCoroutines();
            }
        }

        // ★HPプールを共有している相手（例：連動して倒れる兄弟ボス）も一緒に倒す。
        //   相手側のDie()も同じisDeadガードを持つため、相手から呼ばれた場合の二重処理は起きない
        if (linkedDeathTargets != null)
        {
            foreach (EnemyStats linked in linkedDeathTargets)
            {
                if (linked != null) linked.Die(isKilled);
            }
        }

        if (isKilled)
        {
            // onKilled を先に呼び、外部で deathEffectPositions を設定する機会を与える
            onKilled?.Invoke();

            // プレイヤーに倒された場合: エフェクトとSEを再生
            if (deathEffectPrefab != null)
            {
                Vector3[] positions = (deathEffectPositions != null && deathEffectPositions.Length > 0)
                    ? deathEffectPositions
                    : new Vector3[] { transform.position };

                foreach (var pos in positions)
                {
                    GameObject effect = Instantiate(deathEffectPrefab, pos, Quaternion.identity);

                    if (_useCustomDeathVfx && _deathVfxConfig != null)
                    {
                        effect.GetComponent<DeathVFXSettings>()?.ApplyConfig(_deathVfxConfig);
                    }

                    if (effectDestroySeconds > 0f)
                    {
                        Destroy(effect, effectDestroySeconds);
                    }
                }
            }

            if (deathSeClip != null)
            {
                // SoundSettingsManagerのSE音量を適用
                float finalVolume = seVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
                AudioSource.PlayClipAtPoint(deathSeClip, transform.position, finalVolume);
            }

            // EnemySpawnerに通知
            if (spawner != null)
            {
                spawner.OnEnemyDestroyed();
            }

            // ゴールドを付与
            GoldManager.Instance?.AddSessionGold(goldReward);

            SessionStats.AddEnemyKill();

            // ヒットストップ
            HitStop.Instance?.Trigger();

            // サブパーツを全て破壊
            foreach (GameObject subPart in subParts)
            {
                if (subPart != null)
                {
                    Destroy(subPart);
                }
            }

            Destroy(gameObject);
        }
        else
        {
            // 時間経過で消滅: フェードアウトアニメーション
            StartCoroutine(FadeOutAndDestroy());
        }
    }

    /// <summary>
    /// フェードインするコルーチン
    /// </summary>
    private IEnumerator FadeIn()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) yield break;

        Color originalColor = spriteRenderer.color;

        // 初期状態は透明
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        // 完全に不透明にする
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
    }

    /// <summary>
    /// フェードアウトして消滅するコルーチン
    /// </summary>
    private IEnumerator FadeOutAndDestroy()
    {
        // EnemySpawnerに通知（時間経過による消滅なので撃破数にはカウントしない）
        if (spawner != null)
        {
            spawner.NotifyEnemyDead();
        }

        // 攻撃と移動を停止
        EnemyShooter shooter = GetComponent<EnemyShooter>();
        if (shooter != null)
        {
            shooter.enabled = false;
        }

        EnemyMover mover = GetComponent<EnemyMover>();
        if (mover != null)
        {
            mover.enabled = false;
        }

        // ★SpriteRendererを取得（自分自身のみだと、見た目が子オブジェクト側にある
        //   Marshal/Dragon/GuardBeast等の複合コントローラー系エネミーで何も見つからずフェードが
        //   丸ごとスキップされ、時間切れ消滅時だけ予兆なく突然消えるバグがあった。
        //   子も含めて全SpriteRendererを対象にし、複数パーツの敵でも揃ってフェードするようにする）
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        if (spriteRenderers.Length > 0 && fadeOutDuration > 0f)
        {
            float elapsed = 0f;
            Color[] originalColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                originalColors[i] = spriteRenderers[i].color;
            }

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i] == null) continue;
                    Color c = originalColors[i];
                    spriteRenderers[i].color = new Color(c.r, c.g, c.b, alpha);
                }
                yield return null;
            }

            // 完全に透明にする
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null) continue;
                Color c = originalColors[i];
                spriteRenderers[i].color = new Color(c.r, c.g, c.b, 0f);
            }
        }

        // サブパーツを全て破壊
        foreach (GameObject subPart in subParts)
        {
            if (subPart != null)
            {
                Destroy(subPart);
            }
        }

        // メインパーツを破壊
        Destroy(gameObject);
    }

    /// <summary>
    /// サブパーツを登録（EnemyPartProxy から呼ばれる）
    /// メインパーツが破壊される時、サブパーツも一緒に破壊される
    /// </summary>
    public void RegisterSubPart(GameObject subPart)
    {
        if (subPart != null && !subParts.Contains(subPart))
        {
            subParts.Add(subPart);
        }
    }
}
