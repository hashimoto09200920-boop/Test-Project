using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI
{
    /// <summary>
    /// GemRewardUIのタイトル"SELECT GEM"を、NEON DANCERのタイトルロゴ(TitleLogoLetters)と同じ方式で、
    /// 文字ごとに個別のネオン管画像(9枚)で組み立てる。各文字にはArea1〜9のテーマカラーに対応した
    /// ネオン効果(TitleNeonEffect)を、TitleLogoLettersと全く同じ設定値で付与する。
    /// 全てPlay前のInspectorで調整可能。
    /// </summary>
    [DisallowMultipleComponent]
    public class GemRewardTitleLetters : MonoBehaviour
    {
        [System.Serializable]
        public class LetterEntry
        {
            public string label;
            public Sprite sprite;
            [Tooltip("baseSizeに対する倍率（1=等倍、1.2なら20%大きい）")]
            public float sizeMultiplier = 1f;
            [Tooltip("固定の傾き（度）")]
            public float rotationDeg;
            [HideInInspector] public RectTransform rt;
            [HideInInspector] public Vector2 basePosition;
            [HideInInspector] public float wobblePhase;
        }

        [Header("Letters - SELECT GEM (9文字)")]
        public LetterEntry[] letters = new LetterEntry[9];

        [Header("Layout")]
        [Tooltip("文字1つあたりの基準サイズ(px)。sizeMultiplierを掛けた値が実際の表示サイズになる")]
        public float baseSize = 150f;
        [Tooltip("文字同士の横間隔(px)")]
        public float spacing = 20f;
        [Tooltip("sizeMultiplierが1から離れるほど、上下にずらす量(px)。大きい文字は上、小さい文字は下にずれる")]
        public float yOffsetPerSizeUnit = 40f;

        [Header("Wobble (ゆらゆら揺れ・Play中のみ)")]
        public bool wobbleEnabled = true;
        public float wobbleAmplitude = 6f;
        public float wobbleSpeed = 0.5f;
        [Tooltip("隣接する文字ごとに位相をずらす量。0だと全文字が同時に揺れ、大きいほど波打つように見える")]
        public float wobblePhaseStep = 0.6f;

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
        }

        /// <summary>指定時間でalphaを0→1にフェードインする</summary>
        public IEnumerator FadeInCoroutine(float duration)
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) yield break;
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        /// <summary>指定時間でalphaを現在値→0にフェードアウトする</summary>
        public IEnumerator FadeOutCoroutine(float duration)
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) yield break;
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (!wobbleEnabled || !Application.isPlaying) return;
            if (letters == null) return;
            for (int i = 0; i < letters.Length; i++)
            {
                var l = letters[i];
                if (l == null || l.rt == null) continue;
                float t = Time.unscaledTime * wobbleSpeed * Mathf.PI * 2f + l.wobblePhase;
                float dx = Mathf.Sin(t) * wobbleAmplitude;
                float dy = Mathf.Cos(t * 0.8f) * wobbleAmplitude * 0.6f;
                l.rt.anchoredPosition = l.basePosition + new Vector2(dx, dy);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Assets/Art/GemReward配下の①S_SelectGem.png〜⑨M_SelectGem.pngを、lettersに自動アサインする。
        /// </summary>
        [ContextMenu("1. Assign Letter Sprites (文字画像を自動アサイン)")]
        private void AssignLetterSprites()
        {
            string[] files = { "①S_SelectGem", "②E_SelectGem", "③L_SelectGem", "④E_SelectGem", "⑤C_SelectGem", "⑥T_SelectGem", "⑦G_SelectGem", "⑧E_SelectGem", "⑨M_SelectGem" };
            string[] labels = { "S", "E1", "L", "E2", "C", "T", "G", "E3", "M" };

            if (letters == null || letters.Length != 9) letters = new LetterEntry[9];

            for (int i = 0; i < 9; i++)
            {
                if (letters[i] == null) letters[i] = new LetterEntry();
                letters[i].label = labels[i];
                letters[i].sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/GemReward/{files[i]}.png");
            }

            EditorUtility.SetDirty(this);
            Debug.Log("[GemRewardTitleLetters] 文字画像を自動アサインしました。");
        }

        /// <summary>
        /// letters設定(sprite/sizeMultiplier/rotationDeg)に基づいて、1行に並んだ文字の子オブジェクトを
        /// Hierarchyへ生成・配置する。再実行すると既存の子を全て破棄して作り直す。
        /// </summary>
        [ContextMenu("2. Build Title Letters (文字を配置)")]
        private void BuildTitleLetters()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            float totalWidth = LayoutRow();
            foreach (var l in letters) CreateLetterObject(l);

            EditorUtility.SetDirty(this);
            Debug.Log($"[GemRewardTitleLetters] 文字を配置しました。全体幅={totalWidth:F0}px");
        }

        /// <summary>
        /// 1行分の文字のbasePosition(x,y)を計算する（中央揃え）。実際のGameObject生成はしない。
        /// </summary>
        private float LayoutRow()
        {
            if (letters == null || letters.Length == 0) return 0f;

            float[] widths = new float[letters.Length];
            float totalWidth = 0f;
            for (int i = 0; i < letters.Length; i++)
            {
                widths[i] = baseSize * Mathf.Max(0.01f, letters[i].sizeMultiplier);
                totalWidth += widths[i];
            }
            totalWidth += spacing * (letters.Length - 1);

            float cursorX = -totalWidth * 0.5f;
            for (int i = 0; i < letters.Length; i++)
            {
                float centerX = cursorX + widths[i] * 0.5f;
                float yOffset = (letters[i].sizeMultiplier - 1f) * yOffsetPerSizeUnit;
                letters[i].basePosition = new Vector2(centerX, yOffset);
                letters[i].wobblePhase = i * wobblePhaseStep;
                cursorX += widths[i] + spacing;
            }
            return totalWidth;
        }

        private void CreateLetterObject(LetterEntry l)
        {
            if (l.sprite == null)
            {
                Debug.LogWarning($"[GemRewardTitleLetters] '{l.label}'のspriteが未設定です。スキップします。");
                return;
            }

            var go = new GameObject(string.IsNullOrEmpty(l.label) ? l.sprite.name : l.label, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float size = baseSize * Mathf.Max(0.01f, l.sizeMultiplier);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = l.basePosition;
            rt.localRotation = Quaternion.Euler(0f, 0f, l.rotationDeg);

            var img = go.GetComponent<Image>();
            img.sprite = l.sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;

            l.rt = rt;
        }

        /// <summary>
        /// 9文字それぞれに、NEON DANCER(TitleLogoLetters)と全く同じ設定値でTitleNeonEffectを追加する。
        /// 火花の色だけ、Area1〜9のテーマカラーをその文字の並び順に固定で割り当てる(Area10は使わない、9文字のため)。
        /// Build Title Lettersで文字が配置済みであることが前提。再実行しても安全。
        /// </summary>
        [ContextMenu("3. Apply Neon Effect To Letters (各文字に点灯・点滅・火花を追加)")]
        private void ApplyNeonEffectToLetters()
        {
            // ★TitleLogoLetters.ApplyNeonEffectToLettersのsparkAreaColorsと同じ配列(Area1〜9のみ使用)
            Color[] areaColors =
            {
                new Color(0.608f, 0.561f, 0.780f, 1f), // Area1
                new Color(0.298f, 0.686f, 0.490f, 1f), // Area2
                new Color(0.553f, 0.600f, 0.682f, 1f), // Area3
                new Color(0.878f, 0.478f, 0.247f, 1f), // Area4
                new Color(0.698f, 0.227f, 0.322f, 1f), // Area5
                new Color(0.878f, 0.690f, 0.310f, 1f), // Area6
                new Color(0.310f, 0.561f, 0.878f, 1f), // Area7
                new Color(0.373f, 0.839f, 0.839f, 1f), // Area8
                new Color(0.639f, 0.682f, 0.878f, 1f), // Area9
            };

            int applied = 0;
            if (letters != null)
            {
                for (int i = 0; i < letters.Length; i++)
                {
                    var l = letters[i];
                    if (l == null || l.sprite == null) continue;

                    string childName = string.IsNullOrEmpty(l.label) ? l.sprite.name : l.label;
                    var child = transform.Find(childName);
                    if (child == null)
                    {
                        Debug.LogWarning($"[GemRewardTitleLetters] '{childName}'が見つかりませんでした。先に「Build Title Letters」を実行してください。");
                        continue;
                    }

                    float letterSize = baseSize * Mathf.Max(0.01f, l.sizeMultiplier);
                    Color sparkColor = areaColors[i % areaColors.Length];
                    ApplyNeonEffectToOneLetter(child.gameObject, letterSize, sparkColor);
                    applied++;
                }
            }

            EditorUtility.SetDirty(this);
            Debug.Log($"[GemRewardTitleLetters] {applied}文字に点灯シーケンス・点滅・火花演出を追加しました。");
        }

        private void ApplyNeonEffectToOneLetter(GameObject go, float letterSize, Color sparkColor)
        {
            var effect = go.GetComponent<TitleNeonEffect>();
            if (effect == null) effect = go.AddComponent<TitleNeonEffect>();

            var glow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/UI/SoftGlowCircle.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/UI/UIAdditiveGlow.mat");
            if (glow == null || mat == null)
            {
                Debug.LogWarning($"[GemRewardTitleLetters] {go.name}: SoftGlowCircle.png / UIAdditiveGlow.matが見つかりません（火花演出に必要）。");
            }

            var so = new SerializedObject(effect);
            so.FindProperty("powerOnSequenceEnabled").boolValue = true;
            so.FindProperty("powerOnStartDelay").floatValue = 0.8f;
            so.FindProperty("powerOnStartDelayBrightness").floatValue = 0f;
            so.FindProperty("powerOnFlickerCount").intValue = 4;
            so.FindProperty("powerOnFlickerMinInterval").floatValue = 0.03f;
            so.FindProperty("powerOnFlickerMaxInterval").floatValue = 0.15f;
            so.FindProperty("powerOnDimBrightness").floatValue = 0.15f;

            so.FindProperty("randomFlickerEnabled").boolValue = true;
            so.FindProperty("randomFlickerIntervalMin").floatValue = 2f;
            so.FindProperty("randomFlickerIntervalMax").floatValue = 4f;
            so.FindProperty("randomFlickerBlinkCountMin").intValue = 1;
            so.FindProperty("randomFlickerBlinkCountMax").intValue = 3;
            so.FindProperty("randomFlickerDimBrightness").floatValue = 0.3f;
            so.FindProperty("randomFlickerBlinkDuration").floatValue = 0.1f;

            so.FindProperty("breathingEnabled").boolValue = true;
            so.FindProperty("breathingSpeed").floatValue = 0.6f;
            so.FindProperty("breathingAmount").floatValue = 0.3f;

            so.FindProperty("waveEnabled").boolValue = false;

            so.FindProperty("glowSprite").objectReferenceValue = glow;
            so.FindProperty("additiveGlowMaterial").objectReferenceValue = mat;

            so.FindProperty("sparkEnabled").boolValue = true;
            so.FindProperty("sparkIntervalMin").floatValue = 10f;
            so.FindProperty("sparkIntervalMax").floatValue = 30f;
            so.FindProperty("sparkAreaWidth").floatValue = letterSize * 0.7f;
            so.FindProperty("sparkAreaHeight").floatValue = letterSize * 0.7f;
            so.FindProperty("sparkBurstCount").intValue = 16;
            so.FindProperty("sparkSizeMin").floatValue = 4f;
            so.FindProperty("sparkSizeMax").floatValue = 6f;
            so.FindProperty("sparkSpeedMin").floatValue = 60f;
            so.FindProperty("sparkSpeedMax").floatValue = 180f;
            so.FindProperty("sparkSizeMultiplier").floatValue = 1.2f;
            so.FindProperty("sparkLifetimeMin").floatValue = 0.2f;
            so.FindProperty("sparkLifetimeMax").floatValue = 0.4f;
            so.FindProperty("sparkGravity").floatValue = 300f;

            var colorsProp = so.FindProperty("sparkAreaColors");
            colorsProp.arraySize = 1;
            colorsProp.GetArrayElementAtIndex(0).colorValue = sparkColor;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(go);
        }
#endif
    }
}
