using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// TMP Font Asset に既存文字を消さずに文字を追加するエディタツール。
/// Window → TextMeshPro → Add Characters to Font Asset から開く。
/// </summary>
public class FontAssetCharacterAdder : EditorWindow
{
    private TMP_FontAsset _fontAsset;
    private string _characters = "";
    private string _resultMessage = "";
    private MessageType _messageType = MessageType.None;

    [MenuItem("Window/TextMeshPro/Add Characters to Font Asset")]
    public static void ShowWindow()
    {
        GetWindow<FontAssetCharacterAdder>("Font Character Adder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Font Asset に文字を追加", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _fontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "Font Asset", _fontAsset, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space();
        GUILayout.Label("追加する文字（区切り不要、そのまま貼り付け）", EditorStyles.boldLabel);
        _characters = EditorGUILayout.TextArea(_characters, GUILayout.Height(80));

        EditorGUILayout.Space();

        GUI.enabled = _fontAsset != null && _characters.Length > 0;
        if (GUILayout.Button("追加する"))
        {
            AddCharacters();
        }
        GUI.enabled = true;

        if (_resultMessage.Length > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_resultMessage, _messageType);
        }
    }

    private void AddCharacters()
    {
        bool success = _fontAsset.TryAddCharacters(_characters, out string missingCharacters);

        EditorUtility.SetDirty(_fontAsset);
        AssetDatabase.SaveAssets();

        if (string.IsNullOrEmpty(missingCharacters))
        {
            _resultMessage = $"追加完了。すべての文字が正常に追加されました。";
            _messageType = MessageType.Info;
        }
        else
        {
            _resultMessage = $"一部追加できなかった文字があります: {missingCharacters}\n（フォントファイルにこれらの文字が含まれていない可能性があります）";
            _messageType = MessageType.Warning;
        }
    }
}
