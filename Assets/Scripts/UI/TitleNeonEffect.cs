using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI
{
    /// <summary>
    /// AREA SELECTタイトル（ネオン管ロゴ・単一画像のTitleImage）に、ネオンサインらしい演出を追加する。
    /// 1) 起動時の点灯シーケンス（チカチカしてから点灯）
    /// 2) 稀に起きる不安定なちらつき
    /// 3) 常時の緩やかな明るさの呼吸ゆらぎ
    /// 4) 文字列を横切る光のウェーブ（別レイヤーの加算グローを左右にスイープ。マスクは使わない）
    /// TitleImageと同じGameObjectに追加して使う。
    /// TitleImageは複数色が1枚に焼き込まれた単一画像のため、1〜3はImage.color全体の明るさ倍率で
    /// 表現する（色相はそのまま、RGBを一律にスケールするだけなので多色バランスは崩れない）。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TitleNeonEffect : MonoBehaviour
    {
        [Header("起動時の点灯シーケンス")]
        [SerializeField] private bool powerOnSequenceEnabled = true;
        [Tooltip("点滅を開始するまでの待機時間（秒）")]
        [SerializeField] private float powerOnStartDelay = 0.8f;
        [Tooltip("待機時間中の明るさ(0=完全消灯、1=フル点灯と同じ明るさ)")]
        [Range(0f, 1f)]
        [SerializeField] private float powerOnStartDelayBrightness = 0f;
        [SerializeField] private int powerOnFlickerCount = 4;
        [SerializeField] private float powerOnFlickerMinInterval = 0.03f;
        [SerializeField] private float powerOnFlickerMaxInterval = 0.15f;
        [Range(0f, 1f)]
        [SerializeField] private float powerOnDimBrightness = 0.15f;

        [Header("稀に起きる不安定なちらつき")]
        [SerializeField] private bool randomFlickerEnabled = true;
        [SerializeField] private float randomFlickerIntervalMin = 15f;
        [SerializeField] private float randomFlickerIntervalMax = 40f;
        [Tooltip("1回のちらつきで何回チカッとするか。この範囲でランダムに選ばれる")]
        [SerializeField] private int randomFlickerBlinkCountMin = 1;
        [SerializeField] private int randomFlickerBlinkCountMax = 3;
        [Range(0f, 1f)]
        [SerializeField] private float randomFlickerDimBrightness = 0.3f;
        [SerializeField] private float randomFlickerBlinkDuration = 0.06f;

        [Header("常時の呼吸ゆらぎ")]
        [SerializeField] private bool breathingEnabled = true;
        [SerializeField] private float breathingSpeed = 0.6f;
        [Tooltip("明るさの振れ幅（±）。0.06なら94%〜106%の間でゆっくり変化する")]
        [SerializeField] private float breathingAmount = 0.06f;

        [Header("文字を横切る光のウェーブ")]
        [SerializeField] private bool waveEnabled = false;
        [SerializeField] private float waveIntervalMin = 6f;
        [SerializeField] private float waveIntervalMax = 14f;
        [SerializeField] private float waveDuration = 1.2f;
        [SerializeField] private float waveWidth = 220f;
        [Tooltip("ウェーブの縦幅（px）")]
        [SerializeField] private float waveHeight = 300f;
        [Tooltip("ウェーブの縦位置（px）。0がタイトル中央、プラスで上、マイナスで下にずれる")]
        [SerializeField] private float waveOffsetY = 0f;
        [Range(0f, 1f)]
        [SerializeField] private float waveMaxAlpha = 0.8f;
        [Tooltip("Generate Glow Sprite / Generate Additive Material（AreaConstellationFX）で生成済みの素材を、Setup Neon Effectで自動アサインする")]
        [SerializeField] private Sprite glowSprite;
        [SerializeField] private Material additiveGlowMaterial;

        [Header("稀に走るスパーク演出（Area3ボス撃破エフェクト風・UI版）")]
        [SerializeField] private bool sparkEnabled = true;
        [Tooltip("次のスパークまでの待ち時間（秒）の範囲")]
        [SerializeField] private float sparkIntervalMin = 8f;
        [SerializeField] private float sparkIntervalMax = 25f;
        [Tooltip("出現位置の範囲（px）。文字から離れすぎないよう、実際の文字の大きさに合わせて調整する")]
        [SerializeField] private float sparkAreaWidth = 700f;
        [SerializeField] private float sparkAreaHeight = 100f;
        [Tooltip("1回のスパークで飛び散る粒の数")]
        [SerializeField] private int sparkBurstCount = 24;
        [Tooltip("粒のサイズ（px）の範囲。sparkSizeMultiplierで一括拡大できる")]
        [SerializeField] private float sparkSizeMin = 6f;
        [SerializeField] private float sparkSizeMax = 18f;
        [Tooltip("粒の飛び散る速さ（px/秒）の範囲。sparkSizeMultiplierで一括拡大できる")]
        [SerializeField] private float sparkSpeedMin = 80f;
        [SerializeField] private float sparkSpeedMax = 260f;
        [Tooltip("サイズ・速度に掛ける倍率（少しサイズアップしたい場合はここを1より大きくする）")]
        [SerializeField] private float sparkSizeMultiplier = 1.4f;
        [Tooltip("粒の寿命（秒）の範囲")]
        [SerializeField] private float sparkLifetimeMin = 0.2f;
        [SerializeField] private float sparkLifetimeMax = 0.5f;
        [Tooltip("重力（px/秒²）。下向きに落ちる強さ。0なら等速直線")]
        [SerializeField] private float sparkGravity = 300f;
        [Tooltip("Area1〜10のテーマカラー。1回のスパークごとにこの中からランダムで1色選ばれる")]
        [SerializeField]
        private Color[] sparkAreaColors = new Color[]
        {
            new Color(0.608f, 0.561f, 0.780f, 1f), // Area1 lavender
            new Color(0.298f, 0.686f, 0.490f, 1f), // Area2 green
            new Color(0.553f, 0.600f, 0.682f, 1f), // Area3 slate blue-grey
            new Color(0.878f, 0.478f, 0.247f, 1f), // Area4 orange
            new Color(0.698f, 0.227f, 0.322f, 1f), // Area5 crimson
            new Color(0.878f, 0.690f, 0.310f, 1f), // Area6 gold
            new Color(0.310f, 0.561f, 0.878f, 1f), // Area7 blue
            new Color(0.373f, 0.839f, 0.839f, 1f), // Area8 teal cyan
            new Color(0.639f, 0.682f, 0.878f, 1f), // Area9 periwinkle
            new Color(0.910f, 0.788f, 0.416f, 1f), // Area10 warm gold
        };

        private Image titleImage;
        private Color baseColor;
        private float breathingMul = 1f;
        private float flickerMul = 1f;
        private RectTransform waveRt;
        private Image waveImg;

        private void Awake()
        {
            titleImage = GetComponent<Image>();
            baseColor = titleImage.color;

            // ★点灯シーケンスを使う場合、Start()のコルーチンが動き出す前の数フレーム、
            //   一瞬フル点灯した状態が見えてしまわないよう、Awakeの時点で先に待機時間の明るさにしておく
            if (powerOnSequenceEnabled)
            {
                flickerMul = powerOnStartDelayBrightness;
                ApplyColor();
            }

            var waveHighlight = transform.Find("NeonWaveHighlight");
            if (waveHighlight != null)
            {
                waveRt = (RectTransform)waveHighlight;
                waveImg = waveHighlight.GetComponent<Image>();
                // ★Wave Width/HeightはSetup Neon Effect実行時の値がGameObjectに焼き込まれるだけなので、
                //   Inspectorで数値だけ変更してもサイズに反映されない。Play時に常に最新値を反映させる。
                waveRt.sizeDelta = new Vector2(waveWidth, waveHeight);
            }
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            if (powerOnSequenceEnabled) StartCoroutine(PowerOnSequence());
            if (breathingEnabled) StartCoroutine(BreathingLoop());
            if (randomFlickerEnabled) StartCoroutine(RandomFlickerLoop());
            if (waveEnabled && waveRt != null && waveImg != null) StartCoroutine(WaveLoop());
            if (sparkEnabled) StartCoroutine(SparkLoop());
        }

        private void ApplyColor()
        {
            float b = flickerMul * breathingMul;
            titleImage.color = new Color(baseColor.r * b, baseColor.g * b, baseColor.b * b, baseColor.a);
        }

        private IEnumerator PowerOnSequence()
        {
            // ★待機時間の明るさからスタートする（Awakeで既に設定済みだが、明示的にも合わせておく）
            flickerMul = powerOnStartDelayBrightness;
            ApplyColor();

            // ★この明るさをしばらく見せてから点滅を開始する
            if (powerOnStartDelay > 0f) yield return new WaitForSeconds(powerOnStartDelay);

            for (int i = 0; i < powerOnFlickerCount; i++)
            {
                yield return new WaitForSeconds(Random.Range(powerOnFlickerMinInterval, powerOnFlickerMaxInterval));
                flickerMul = (i % 2 == 0) ? 1f : powerOnDimBrightness;
                ApplyColor();
            }
            flickerMul = 1f;
            ApplyColor();
        }

        private IEnumerator BreathingLoop()
        {
            while (true)
            {
                breathingMul = 1f + Mathf.Sin(Time.unscaledTime * breathingSpeed * Mathf.PI * 2f) * breathingAmount;
                ApplyColor();
                yield return null;
            }
        }

        private IEnumerator RandomFlickerLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(randomFlickerIntervalMin, randomFlickerIntervalMax));
                int blinkCount = Random.Range(randomFlickerBlinkCountMin, randomFlickerBlinkCountMax + 1);
                for (int i = 0; i < blinkCount; i++)
                {
                    flickerMul = randomFlickerDimBrightness;
                    ApplyColor();
                    yield return new WaitForSeconds(randomFlickerBlinkDuration);
                    flickerMul = 1f;
                    ApplyColor();
                    yield return new WaitForSeconds(randomFlickerBlinkDuration);
                }
            }
        }

        private IEnumerator WaveLoop()
        {
            // ★マスクで矩形クリップすると、柔らかい丸グローが直線で切られて見えるため、
            //   マスクは使わずタイトル幅の左端〜右端をそのまま素直にスイープさせる。
            //   加算合成の柔らかいグローは多少はみ出しても不自然にならない。
            float titleWidth = ((RectTransform)transform).rect.width;
            float startX = -titleWidth * 0.5f;
            float endX = titleWidth * 0.5f;

            while (true)
            {
                yield return new WaitForSeconds(Random.Range(waveIntervalMin, waveIntervalMax));

                float elapsed = 0f;
                while (elapsed < waveDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / waveDuration);
                    waveRt.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, t), waveOffsetY);

                    float fade = Mathf.Sin(t * Mathf.PI); // 0→1→0
                    var c = waveImg.color;
                    c.a = fade * waveMaxAlpha;
                    waveImg.color = c;

                    yield return null;
                }

                var c2 = waveImg.color;
                c2.a = 0f;
                waveImg.color = c2;
            }
        }

        private IEnumerator SparkLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(sparkIntervalMin, sparkIntervalMax));
                StartCoroutine(SpawnSpark());
            }
        }

        // ★Area3ボス(IronNest)撃破時のVFX_Explosion_A_RingSparks（PS_Sparks）と同じ考え方で、
        //   1点から放射状に粒が飛び散り、重力で落ちながらフェードアウトする。
        //   実際のParticleSystemはScreen Space - OverlayのCanvas内では描画できないため、
        //   同じパラメータ思想（バースト数・速度・サイズ・寿命・重力）をUI Imageで再現する。
        private IEnumerator SpawnSpark()
        {
            if (glowSprite == null) yield break;

            // ★文字から離れすぎないよう、タイトル矩形全体ではなく専用の範囲だけを使う
            float x = Random.Range(-sparkAreaWidth * 0.5f, sparkAreaWidth * 0.5f);
            float y = Random.Range(-sparkAreaHeight * 0.5f, sparkAreaHeight * 0.5f);
            Vector2 origin = new Vector2(x, y);

            Color picked = sparkAreaColors != null && sparkAreaColors.Length > 0
                ? sparkAreaColors[Random.Range(0, sparkAreaColors.Length)]
                : Color.white;

            for (int i = 0; i < sparkBurstCount; i++)
            {
                StartCoroutine(SpawnSparkParticle(origin, picked));
            }
        }

        private IEnumerator SpawnSparkParticle(Vector2 origin, Color color)
        {
            var go = new GameObject("NeonSparkParticle", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = origin;

            float size = Random.Range(sparkSizeMin, sparkSizeMax) * sparkSizeMultiplier;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = glowSprite;
            if (additiveGlowMaterial != null) img.material = additiveGlowMaterial;
            img.color = color;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(sparkSpeedMin, sparkSpeedMax) * sparkSizeMultiplier;
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Mathf.Max(0.05f, Random.Range(sparkLifetimeMin, sparkLifetimeMax));
            Vector2 pos = origin;
            float elapsed = 0f;
            while (elapsed < lifetime)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                velocity.y -= sparkGravity * dt;
                pos += velocity * dt;
                rt.anchoredPosition = pos;

                float t = Mathf.Clamp01(elapsed / lifetime);
                var c = color;
                c.a = color.a * (1f - t);
                img.color = c;

                yield return null;
            }

            if (go != null) Destroy(go);
        }

#if UNITY_EDITOR
        /// <summary>
        /// AreaConstellationFXで生成済みのグロー素材（SoftGlowCircle/UIAdditiveGlow）を自動アサインし、
        /// ウェーブ用のオーバーレイ（NeonWaveMask/NeonWaveHighlight）をHierarchyに生成する。
        /// 再実行しても安全（既存があれば作り直す）。
        /// </summary>
        [ContextMenu("Setup Neon Effect (グロー素材アサイン＋Waveオーバーレイ生成)")]
        private void SetupNeonEffect()
        {
            glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/UI/SoftGlowCircle.png");
            additiveGlowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/UI/UIAdditiveGlow.mat");
            if (glowSprite == null || additiveGlowMaterial == null)
            {
                Debug.LogWarning("[TitleNeonEffect] SoftGlowCircle.png / UIAdditiveGlow.matが見つかりません。先にAreaConstellationFXの「Generate Glow Sprite」「Generate Additive Material」を実行してください。");
            }

            // ★旧バージョンで使っていたマスク用ラッパー（残っていれば削除）
            var existingMask = transform.Find("NeonWaveMask");
            if (existingMask != null) DestroyImmediate(existingMask.gameObject);

            var existingWave = transform.Find("NeonWaveHighlight");
            if (existingWave != null) DestroyImmediate(existingWave.gameObject);

            var waveGo = new GameObject("NeonWaveHighlight", typeof(RectTransform), typeof(Image));
            var waveRtLocal = (RectTransform)waveGo.transform;
            waveRtLocal.SetParent(transform, false);
            // ★中央基準(0.5, 0.5)にする。WaveLoop側の移動量計算(-titleWidth/2〜+titleWidth/2)が
            //   中央アンカー前提のため、ここが左端(0, 0.5)になっていると終点がずれて途中で消えて見えた。
            waveRtLocal.anchorMin = waveRtLocal.anchorMax = new Vector2(0.5f, 0.5f);
            waveRtLocal.pivot = new Vector2(0.5f, 0.5f);
            waveRtLocal.sizeDelta = new Vector2(waveWidth, waveHeight);

            var waveImgLocal = waveGo.GetComponent<Image>();
            waveImgLocal.raycastTarget = false;
            waveImgLocal.preserveAspect = false; // ★trueだとsizeDeltaが無視されHeightを変更しても反映されない
            if (glowSprite != null) waveImgLocal.sprite = glowSprite;
            if (additiveGlowMaterial != null) waveImgLocal.material = additiveGlowMaterial;
            waveImgLocal.color = new Color(1f, 1f, 1f, 0f);

            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(gameObject);
            Debug.Log("[TitleNeonEffect] グロー素材のアサインとWaveオーバーレイの生成が完了しました。");
        }
#endif
    }
}
