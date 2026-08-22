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
#endif
    }
}
