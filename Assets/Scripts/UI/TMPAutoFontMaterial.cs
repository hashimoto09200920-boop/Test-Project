using UnityEngine;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// TextMeshProのFont AssetとMaterialを自動同期するコンポーネント
    /// Font Assetを変更した際のMaterial Preset問題を回避
    ///
    /// 使い方：
    /// 1. TextMeshProコンポーネントと同じGameObjectにアタッチ
    /// 2. PlayモードまたはAwake時に自動的にFont AssetのデフォルトMaterialを適用
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways] // Editor/Playモード両方で動作
    public class TMPAutoFontMaterial : MonoBehaviour
    {
        private TMP_Text tmpText;

        private void Awake()
        {
            ApplyFontMaterial();
        }

        private void OnValidate()
        {
            // Editor上でInspectorが変更された時にも適用
            ApplyFontMaterial();
        }

        /// <summary>
        /// Font AssetのデフォルトMaterialを適用
        /// </summary>
        private void ApplyFontMaterial()
        {
            if (tmpText == null)
            {
                tmpText = GetComponent<TMP_Text>();
            }

            if (tmpText != null && tmpText.font != null)
            {
                // Font AssetのデフォルトMaterialを強制的に適用
                tmpText.fontSharedMaterial = tmpText.font.material;
            }
        }
    }
}
