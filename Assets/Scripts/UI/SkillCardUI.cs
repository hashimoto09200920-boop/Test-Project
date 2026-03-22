using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Game.Skills;

namespace Game.UI
{
    /// <summary>
    /// スキル選択カード（1枚分）
    /// </summary>
    public class SkillCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text skillNameText;
        [SerializeField] private Image skillNameFrameImage;
        [SerializeField] private Image iconBackgroundImage;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image descriptionFrameImage;
        [SerializeField] private TMP_Text effectValueText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;

        [Header("Category Background Sprites")]
        [SerializeField] private Sprite categoryASprite;
        [SerializeField] private Sprite categoryBSprite;
        [SerializeField] private Sprite categoryCSprite;

        [Header("Hover Effects")]
        [Tooltip("ホバー時の拡大倍率（例: 1.05 = 5%拡大）")]
        [SerializeField] private float hoverScale = 1.05f;
        [Tooltip("拡大縮小アニメーションの時間（秒）")]
        [SerializeField] private float hoverScaleDuration = 0.1f;
        [Tooltip("ホバー時に鳴らすSE")]
        [SerializeField] private AudioClip hoverSE;
        [Tooltip("ホバーSEの音量（SoundSettingsManagerのSEVolumeに掛け合わせる）")]
        [SerializeField] [Range(0f, 1f)] private float hoverSEVolume = 1f;
        [Tooltip("点滅速度（1秒あたりのサイクル数）")]
        [SerializeField] private float blinkSpeed = 2f;
        [Tooltip("点滅時にブレンドする色")]
        [SerializeField] private Color blinkColor = new Color(1f, 1f, 0.7f, 1f);
        [Tooltip("点滅の強さ（0=変化なし, 1=blinkColorに完全一致）")]
        [SerializeField] [Range(0f, 1f)] private float blinkIntensity = 0.5f;

        private AudioSource hoverAudioSource;
        private bool hoverEnabled = false;
        private bool hoverInitialized = false;
        private Coroutine blinkCoroutine;
        private Coroutine scaleCoroutine;
        private Vector3 originalScale;
        private Color originalBgColor;

        private SkillDefinition currentSkill;
        private System.Action<SkillDefinition> onSelected;

        private void Awake()
        {
            originalScale = transform.localScale;
            if (backgroundImage != null)
                originalBgColor = backgroundImage.color;
            hoverInitialized = true;

            hoverAudioSource = GetComponent<AudioSource>();
            if (hoverAudioSource == null)
            {
                hoverAudioSource = gameObject.AddComponent<AudioSource>();
                hoverAudioSource.playOnAwake = false;
            }

            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }

            // Font AssetのデフォルトMaterialを強制的に使用（Material Preset問題の回避）
            Debug.Log($"[SkillCardUI] Awake - skillNameText: {(skillNameText != null ? "OK" : "NULL")}, font: {(skillNameText != null && skillNameText.font != null ? skillNameText.font.name : "NULL")}, material: {(skillNameText != null && skillNameText.font != null && skillNameText.font.material != null ? skillNameText.font.material.name : "NULL")}");

            if (skillNameText != null && skillNameText.font != null)
            {
                var beforeMaterial = skillNameText.fontSharedMaterial;
                skillNameText.fontSharedMaterial = skillNameText.font.material;
                Debug.Log($"[SkillCardUI] skillNameText.fontSharedMaterial changed: {(beforeMaterial != null ? beforeMaterial.name : "NULL")} -> {(skillNameText.fontSharedMaterial != null ? skillNameText.fontSharedMaterial.name : "NULL")}");
            }
            if (descriptionText != null && descriptionText.font != null)
            {
                var beforeMaterial = descriptionText.fontSharedMaterial;
                descriptionText.fontSharedMaterial = descriptionText.font.material;
                Debug.Log($"[SkillCardUI] descriptionText.fontSharedMaterial changed: {(beforeMaterial != null ? beforeMaterial.name : "NULL")} -> {(descriptionText.fontSharedMaterial != null ? descriptionText.fontSharedMaterial.name : "NULL")}");
            }
            if (effectValueText != null && effectValueText.font != null)
            {
                var beforeMaterial = effectValueText.fontSharedMaterial;
                effectValueText.fontSharedMaterial = effectValueText.font.material;
                Debug.Log($"[SkillCardUI] effectValueText.fontSharedMaterial changed: {(beforeMaterial != null ? beforeMaterial.name : "NULL")} -> {(effectValueText.fontSharedMaterial != null ? effectValueText.fontSharedMaterial.name : "NULL")}");
            }
        }

        /// <summary>
        /// カードをセットアップ
        /// </summary>
        public void SetupCard(SkillDefinition skill, System.Action<SkillDefinition> onSelectedCallback)
        {
            Debug.Log($"[SkillCardUI] SetupCard called - skill: {(skill != null ? skill.skillName : "NULL")}");
            Debug.Log($"[SkillCardUI] SetupCard - skillNameText: {(skillNameText != null ? "OK" : "NULL")}, font: {(skillNameText != null && skillNameText.font != null ? skillNameText.font.name : "NULL")}, fontSharedMaterial: {(skillNameText != null && skillNameText.fontSharedMaterial != null ? skillNameText.fontSharedMaterial.name : "NULL")}");

            currentSkill = skill;
            onSelected = onSelectedCallback;

            if (skill == null) return;

            // テキストを設定
            if (skillNameText != null)
            {
                skillNameText.text = skill.skillName;
            }

            if (descriptionText != null)
            {
                // descriptionTemplateが設定されていればvalueを差し込んで表示、なければdescriptionをそのまま使用
                if (!string.IsNullOrEmpty(skill.descriptionTemplate))
                {
                    string valueStr;
                    if (skill.useRandomRange)
                    {
                        valueStr = skill.isMultiplier
                            ? $"{skill.effectValueMin:F2}~{skill.effectValueMax:F2}"
                            : $"{skill.effectValueMin:F1}~{skill.effectValueMax:F1}";
                    }
                    else
                    {
                        valueStr = skill.isMultiplier
                            ? $"{skill.effectValue:F2}"
                            : $"{skill.effectValue:F1}";
                    }
                    descriptionText.text = skill.descriptionTemplate.Replace("{value}", valueStr);
                }
                else
                {
                    descriptionText.text = skill.description;
                }
            }

            // effectValueTextはdescriptionに統合したため非表示
            if (effectValueText != null)
            {
                effectValueText.gameObject.SetActive(false);
            }

            // アイコンを設定
            if (iconImage != null && skill.icon != null)
            {
                iconImage.sprite = skill.icon;
                iconImage.enabled = true;
            }
            else if (iconImage != null)
            {
                iconImage.enabled = false;
            }

            // 背景Spriteをカテゴリで切り替え（未設定の場合は色のみ適用）
            if (backgroundImage != null)
            {
                Sprite bgSprite = skill.category switch
                {
                    SkillCategory.CategoryA => categoryASprite,
                    SkillCategory.CategoryB => categoryBSprite,
                    SkillCategory.CategoryC => categoryCSprite,
                    _ => null
                };
                backgroundImage.sprite = bgSprite;
            }
        }

        /// <summary>
        /// 表示フォントを差し替える（日本語フォント用）。SkillSelectionUI から呼ばれる。
        /// </summary>
        public void SetFont(TMP_FontAsset font)
        {
            if (font == null) return;
            if (skillNameText != null)
            {
                skillNameText.font = font;
                skillNameText.fontSharedMaterial = font.material;
            }
            if (descriptionText != null)
            {
                descriptionText.font = font;
                descriptionText.fontSharedMaterial = font.material;
            }
            if (effectValueText != null)
            {
                effectValueText.font = font;
                effectValueText.fontSharedMaterial = font.material;
            }
        }

        /// <summary>
        /// クリック時の処理
        /// </summary>
        private void OnClick()
        {
            if (currentSkill != null)
            {
                onSelected?.Invoke(currentSkill);
            }
        }

        // ─── Hover Effects ───────────────────────────────────────────

        /// <summary>
        /// ホバーエフェクトの有効/無効を切り替える（SkillSelectionUIから呼ばれる）
        /// </summary>
        public void SetHoverEnabled(bool enabled)
        {
            hoverEnabled = enabled;
        }

        /// <summary>
        /// ホバー状態をリセット（新しいカード表示前に呼ばれる）
        /// </summary>
        public void ResetHoverState()
        {
            hoverEnabled = false;
            StopBlinkCoroutine();
            StopScaleCoroutine();
            if (hoverInitialized)
            {
                transform.localScale = originalScale;
                if (backgroundImage != null)
                    backgroundImage.color = originalBgColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!hoverEnabled) return;

            // 拡大
            StartScaleTo(originalScale * hoverScale);

            // SE再生（SoundSettingsManagerで音量補正）
            if (hoverSE != null && hoverAudioSource != null)
            {
                float vol = hoverSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
                hoverAudioSource.PlayOneShot(hoverSE, vol);
            }

            // 点滅開始
            StopBlinkCoroutine();
            blinkCoroutine = StartCoroutine(BlinkCoroutine());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!hoverEnabled) return;

            // 元のサイズに戻す
            StartScaleTo(originalScale);

            // 点滅停止・色を元に戻す
            StopBlinkCoroutine();
            if (backgroundImage != null)
                backgroundImage.color = originalBgColor;
        }

        private void StartScaleTo(Vector3 target)
        {
            StopScaleCoroutine();
            scaleCoroutine = StartCoroutine(ScaleCoroutine(target));
        }

        private void StopBlinkCoroutine()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
        }

        private void StopScaleCoroutine()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }
        }

        private IEnumerator ScaleCoroutine(Vector3 target)
        {
            Vector3 start = transform.localScale;
            float elapsed = 0f;
            while (elapsed < hoverScaleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(start, target, elapsed / hoverScaleDuration);
                yield return null;
            }
            transform.localScale = target;
            scaleCoroutine = null;
        }

        private IEnumerator BlinkCoroutine()
        {
            if (backgroundImage == null) yield break;
            while (true)
            {
                float t = (Mathf.Sin(Time.unscaledTime * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f * blinkIntensity;
                backgroundImage.color = Color.Lerp(originalBgColor, blinkColor, t);
                yield return null;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// [Editor] Frame/Background ImageのRectTransformを対応テキスト/アイコンにコピー
        /// Inspector右クリック → "Sync Frame RectTransforms" で実行
        /// </summary>
        /// <summary>
        /// [Editor] 各要素をレイアウト通りに配置（SkillName上部・アイコン中央・Description下部）
        /// Inspector右クリック → "Apply Layout" で実行
        /// ※実行後 Ctrl+S でシーンを保存すること
        /// </summary>
        [ContextMenu("Apply Layout")]
        private void ApplyLayout()
        {
            // SkillName: 上部（上端アンカー、高さ100）
            if (skillNameText != null)
            {
                SetRectTransform(skillNameText.GetComponent<RectTransform>(),
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                    new Vector2(0, -50), new Vector2(-20, 100));
                UnityEditor.EditorUtility.SetDirty(skillNameText.gameObject);
            }
            if (skillNameFrameImage != null && skillNameText != null)
            {
                CopyRectTransform(skillNameText.GetComponent<RectTransform>(),
                    skillNameFrameImage.GetComponent<RectTransform>());
                UnityEditor.EditorUtility.SetDirty(skillNameFrameImage.gameObject);
            }

            // IconImage: 中央（中心アンカー、180×180）
            if (iconImage != null)
            {
                SetRectTransform(iconImage.GetComponent<RectTransform>(),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0, 40), new Vector2(180, 180));
                UnityEditor.EditorUtility.SetDirty(iconImage.gameObject);
            }
            if (iconBackgroundImage != null && iconImage != null)
            {
                CopyRectTransform(iconImage.GetComponent<RectTransform>(),
                    iconBackgroundImage.GetComponent<RectTransform>());
                UnityEditor.EditorUtility.SetDirty(iconBackgroundImage.gameObject);
            }

            // Description: 下部（下端アンカー、高さ180）
            if (descriptionText != null)
            {
                SetRectTransform(descriptionText.GetComponent<RectTransform>(),
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0.5f),
                    new Vector2(0, 100), new Vector2(-20, 180));
                UnityEditor.EditorUtility.SetDirty(descriptionText.gameObject);
            }
            if (descriptionFrameImage != null && descriptionText != null)
            {
                CopyRectTransform(descriptionText.GetComponent<RectTransform>(),
                    descriptionFrameImage.GetComponent<RectTransform>());
                UnityEditor.EditorUtility.SetDirty(descriptionFrameImage.gameObject);
            }

            Debug.Log("[SkillCardUI] ApplyLayout complete.");
        }

        private void SetRectTransform(RectTransform rt,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta        = sizeDelta;
        }

        [ContextMenu("Sync Frame RectTransforms")]
        private void SyncFrameRectTransforms()
        {
            if (skillNameFrameImage != null && skillNameText != null)
            {
                CopyRectTransform(
                    skillNameText.GetComponent<RectTransform>(),
                    skillNameFrameImage.GetComponent<RectTransform>());
                UnityEditor.EditorUtility.SetDirty(skillNameFrameImage.gameObject);
            }
            if (descriptionFrameImage != null && descriptionText != null)
            {
                CopyRectTransform(
                    descriptionText.GetComponent<RectTransform>(),
                    descriptionFrameImage.GetComponent<RectTransform>());
                UnityEditor.EditorUtility.SetDirty(descriptionFrameImage.gameObject);
            }
            if (iconBackgroundImage != null && iconImage != null)
            {
                CopyRectTransform(
                    iconImage.GetComponent<RectTransform>(),
                    iconBackgroundImage.GetComponent<RectTransform>());
                UnityEditor.EditorUtility.SetDirty(iconBackgroundImage.gameObject);
            }
            Debug.Log("[SkillCardUI] SyncFrameRectTransforms complete.");
        }

        private void CopyRectTransform(RectTransform src, RectTransform dst)
        {
            dst.anchorMin        = src.anchorMin;
            dst.anchorMax        = src.anchorMax;
            dst.pivot            = src.pivot;
            dst.anchoredPosition = src.anchoredPosition;
            dst.sizeDelta        = src.sizeDelta;
        }
#endif
    }
}
