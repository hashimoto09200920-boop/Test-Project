using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI
{
    /// <summary>
    /// "NEON DANCER"タイトルロゴを、文字ごとに個別のネオン管画像(10枚)で組み立てる。
    /// 文字ごとに不均一なサイズ・固定の傾き・上下オフセットを持たせ、
    /// Play中は位相をずらしたサイン波でゆらゆらと不規則に揺らす（波打つような動き）。
    /// 全てPlay前のInspectorで調整可能。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleLogoLetters : MonoBehaviour
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

        [Header("Letters - NEON (寒色4文字)")]
        public LetterEntry[] neonLetters = new LetterEntry[4];

        [Header("Letters - DANCER (暖色6文字)")]
        public LetterEntry[] dancerLetters = new LetterEntry[6];

        [Header("Layout")]
        [Tooltip("文字1つあたりの基準サイズ(px)。sizeMultiplierを掛けた値が実際の表示サイズになる")]
        public float baseSize = 150f;
        [Tooltip("文字同士の横間隔(px)")]
        public float spacing = 20f;
        [Tooltip("NEON行とDANCER行の縦間隔(px)")]
        public float lineSpacing = 190f;
        [Tooltip("sizeMultiplierが1から離れるほど、上下にずらす量(px)。大きい文字は上、小さい文字は下にずれる")]
        public float yOffsetPerSizeUnit = 40f;

        [Header("Wobble (ゆらゆら揺れ・Play中のみ)")]
        public bool wobbleEnabled = true;
        public float wobbleAmplitude = 6f;
        public float wobbleSpeed = 0.5f;
        [Tooltip("隣接する文字ごとに位相をずらす量。0だと全文字が同時に揺れ、大きいほど波打つように見える")]
        public float wobblePhaseStep = 0.6f;

        private void Update()
        {
            if (!wobbleEnabled || !Application.isPlaying) return;
            UpdateWobble(neonLetters);
            UpdateWobble(dancerLetters);
        }

        private void UpdateWobble(LetterEntry[] letters)
        {
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
        /// neonLetters/dancerLettersの設定(sprite/sizeMultiplier/rotationDeg)に基づいて、
        /// 実際にHierarchyへ文字の子オブジェクトを生成・配置する。再実行すると作り直す（非破壊ではない、
        /// 生成済みの子だけをクリアして再生成するので他の要素には影響しない）。
        /// </summary>
        [ContextMenu("Build Logo Letters (文字を配置)")]
        private void BuildLogoLetters()
        {
            // 既存の子を全部クリアしてから作り直す
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            float neonWidth = LayoutRow(neonLetters, 0f, out float neonRowHeight);
            float dancerWidth = LayoutRow(dancerLetters, -lineSpacing, out float dancerRowHeight);

            foreach (var l in neonLetters) CreateLetterObject(l);
            foreach (var l in dancerLetters) CreateLetterObject(l);

            EditorUtility.SetDirty(this);
            Debug.Log($"[TitleLogoLetters] 文字を配置しました。NEON幅={neonWidth:F0}px, DANCER幅={dancerWidth:F0}px");
        }

        /// <summary>
        /// 1行分の文字のbasePosition(x,y)を計算する（中央揃え）。実際のGameObject生成はしない。
        /// </summary>
        private float LayoutRow(LetterEntry[] letters, float centerY, out float rowHeight)
        {
            rowHeight = 0f;
            if (letters == null || letters.Length == 0) return 0f;

            float[] widths = new float[letters.Length];
            float totalWidth = 0f;
            for (int i = 0; i < letters.Length; i++)
            {
                widths[i] = baseSize * Mathf.Max(0.01f, letters[i].sizeMultiplier);
                totalWidth += widths[i];
                if (widths[i] > rowHeight) rowHeight = widths[i];
            }
            totalWidth += spacing * (letters.Length - 1);

            float cursorX = -totalWidth * 0.5f;
            for (int i = 0; i < letters.Length; i++)
            {
                float centerX = cursorX + widths[i] * 0.5f;
                float yOffset = (letters[i].sizeMultiplier - 1f) * yOffsetPerSizeUnit;
                letters[i].basePosition = new Vector2(centerX, centerY + yOffset);
                letters[i].wobblePhase = i * wobblePhaseStep;
                cursorX += widths[i] + spacing;
            }
            return totalWidth;
        }

        private void CreateLetterObject(LetterEntry l)
        {
            if (l.sprite == null)
            {
                Debug.LogWarning($"[TitleLogoLetters] '{l.label}'のspriteが未設定です。スキップします。");
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
        /// Assets/Art/Title配下の①N〜⑩R.pngを、neonLetters/dancerLettersに自動アサインする。
        /// サイズ・回転はここでは設定しない（Inspectorで手動調整、またはApply Default Letter Styleで一括設定）。
        /// </summary>
        [ContextMenu("1. Assign Letter Sprites (文字画像を自動アサイン)")]
        private void AssignLetterSprites()
        {
            string[] neonFiles = { "①N", "②E", "③O", "④N" };
            string[] neonLabels = { "N1", "E1", "O1", "N2" };
            string[] dancerFiles = { "⑤D", "⑥A", "⑦N", "⑧C", "⑨E", "⑩R" };
            string[] dancerLabels = { "D", "A", "N3", "C", "E2", "R" };

            if (neonLetters == null || neonLetters.Length != 4) neonLetters = new LetterEntry[4];
            if (dancerLetters == null || dancerLetters.Length != 6) dancerLetters = new LetterEntry[6];

            for (int i = 0; i < 4; i++)
            {
                if (neonLetters[i] == null) neonLetters[i] = new LetterEntry();
                neonLetters[i].label = neonLabels[i];
                neonLetters[i].sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Title/{neonFiles[i]}.png");
            }
            for (int i = 0; i < 6; i++)
            {
                if (dancerLetters[i] == null) dancerLetters[i] = new LetterEntry();
                dancerLetters[i].label = dancerLabels[i];
                dancerLetters[i].sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Title/{dancerFiles[i]}.png");
            }

            EditorUtility.SetDirty(this);
            Debug.Log("[TitleLogoLetters] 文字画像を自動アサインしました。");
        }

        /// <summary>
        /// 合意済みのデザイン（NEONの最初のNを大きく・母音は小さめ、DANCERも同様のルール）と、
        /// 交互の固定回転を一括で設定する。再実行しても安全（値を上書きするだけ）。
        /// </summary>
        [ContextMenu("2. Apply Default Letter Style (既定のサイズ・傾きを適用)")]
        private void ApplyDefaultLetterStyle()
        {
            if (neonLetters == null || neonLetters.Length != 4 || dancerLetters == null || dancerLetters.Length != 6)
            {
                Debug.LogWarning("[TitleLogoLetters] 先に「1. Assign Letter Sprites」を実行してください。");
                return;
            }

            // NEON: N=120%, E=80%, O=75%, N=100%
            float[] neonSizes = { 1.20f, 0.80f, 0.75f, 1.00f };
            // DANCER: D=115%, A=85%, N=95%, C=90%, E=80%, R=105%
            float[] dancerSizes = { 1.15f, 0.85f, 0.95f, 0.90f, 0.80f, 1.05f };
            // 隣接文字で向きを交互にするジグザグな固定回転（10文字通しでずらす）
            float[] rotations = { 5f, -4f, 3f, -5f, 4f, -3f, 5f, -4f, 3f, -5f };

            for (int i = 0; i < 4; i++)
            {
                neonLetters[i].sizeMultiplier = neonSizes[i];
                neonLetters[i].rotationDeg = rotations[i];
            }
            for (int i = 0; i < 6; i++)
            {
                dancerLetters[i].sizeMultiplier = dancerSizes[i];
                dancerLetters[i].rotationDeg = rotations[i + 4];
            }

            EditorUtility.SetDirty(this);
            Debug.Log("[TitleLogoLetters] 既定のサイズ・傾きを適用しました。続けて「Build Logo Letters」を実行してください。");
        }

        /// <summary>
        /// 10文字それぞれに、Pauseタイトル/Titleボタンと同じ仕組みの点灯シーケンス・ちらつき・呼吸ゆらぎ・
        /// 火花演出(TitleNeonEffect)を追加する。TitleNeonEffectは単一Image専用のコンポーネントのため、
        /// ロゴ全体ではなく文字ごとの子オブジェクトに個別に付与する。
        /// ・点灯シーケンス(powerOnSequenceEnabled)は各文字が独立したタイミングでランダムに点滅するため有効化する
        ///   (ボタン/Pauseタイトルでは無効化していたが、今回は「点灯シーケンス」自体が要望のため有効にする)
        /// ・火花の発生エリアは文字1つ分のサイズに合わせて縮小する(ボタン用の700x100は文字に対して大きすぎる)
        /// ・火花の色は、Area1〜10のテーマカラー配色順(NEON DANCERの文字色と同じ並び)からその文字自身の色1色に
        ///   固定する(ランダム10色だと文字ごとに色がバラついて統一感がなくなるため)
        /// ・10文字同時にSparkを付けると頻度が跳ね上がるため、1文字あたりの間隔をボタンの10倍程度に広げ、
        ///   ロゴ全体としての体感頻度をボタン1個分と揃える
        /// Build Logo Lettersで文字が配置済みであることが前提。再実行しても安全。
        /// </summary>
        [ContextMenu("3. Apply Neon Effect To Letters (各文字に点灯・点滅・火花を追加)")]
        private void ApplyNeonEffectToLetters()
        {
            // ★Areaノードの色順(TitleMenu.ApplyNeonEffectToButtonsのsparkAreaColorsと同じ配列)
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
                new Color(0.910f, 0.788f, 0.416f, 1f), // Area10
            };

            int colorIndex = 0;
            int applied = 0;
            applied += ApplyNeonEffectToArray(neonLetters, areaColors, ref colorIndex);
            applied += ApplyNeonEffectToArray(dancerLetters, areaColors, ref colorIndex);

            EditorUtility.SetDirty(this);
            Debug.Log($"[TitleLogoLetters] {applied}文字に点灯シーケンス・点滅・火花演出を追加しました。");
        }

        private int ApplyNeonEffectToArray(LetterEntry[] letters, Color[] areaColors, ref int colorIndex)
        {
            if (letters == null) return 0;
            int count = 0;
            foreach (var l in letters)
            {
                if (l == null || l.sprite == null) { colorIndex++; continue; }

                string childName = string.IsNullOrEmpty(l.label) ? l.sprite.name : l.label;
                var child = transform.Find(childName);
                if (child == null)
                {
                    Debug.LogWarning($"[TitleLogoLetters] '{childName}'が見つかりませんでした。先に「Build Logo Letters」を実行してください。");
                    colorIndex++;
                    continue;
                }

                float letterSize = baseSize * Mathf.Max(0.01f, l.sizeMultiplier);
                Color sparkColor = areaColors[colorIndex % areaColors.Length];
                ApplyNeonEffectToOneLetter(child.gameObject, letterSize, sparkColor);
                colorIndex++;
                count++;
            }
            return count;
        }

        private void ApplyNeonEffectToOneLetter(GameObject go, float letterSize, Color sparkColor)
        {
            var effect = go.GetComponent<TitleNeonEffect>();
            if (effect == null) effect = go.AddComponent<TitleNeonEffect>();

            var glow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/UI/SoftGlowCircle.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/UI/UIAdditiveGlow.mat");
            if (glow == null || mat == null)
            {
                Debug.LogWarning($"[TitleLogoLetters] {go.name}: SoftGlowCircle.png / UIAdditiveGlow.matが見つかりません（火花演出に必要）。");
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
