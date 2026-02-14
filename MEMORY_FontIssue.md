# TextMeshPro Font Asset/Material問題 - 完全ガイド

## 問題の症状
- Unity EditorでTextMeshProのFont Assetを変更しても日本語が表示されない
- Inspectorでは正しいMaterialが表示されているのに、実行時には文字化けする

## 根本原因

### Unity EditorのTextMeshPro仕様
1. Font Assetを変更すると、**Inspector上では**新しいFont AssetのデフォルトMaterialが表示される
2. **しかし実際のシーン値（m_sharedMaterial）は変更されない**
3. 実行時には古いm_sharedMaterial値が使われるため、文字化けが発生

### 具体例（2026/2/14発生）
- Font Asset: NotoSansJP-Regular SDF（日本語グリフあり）
- m_sharedMaterial: LiberationSans SDF（日本語グリフなし）← 古い値が残る
- 結果: 日本語が表示されない

## 解決方法

### 方法1: TMPAutoFontMaterialコンポーネント（推奨）
**新しいTextMeshProコンポーネントに使用**

```csharp
// 1. TextMeshProコンポーネントと同じGameObjectにアタッチ
gameObject.AddComponent<TMPAutoFontMaterial>();

// 2. 自動的にFont AssetのデフォルトMaterialが適用される
```

**ファイル**: `Assets/Scripts/UI/TMPAutoFontMaterial.cs`

### 方法2: Awakeで手動設定
**既存のコンポーネント（SkillCardUIなど）**

```csharp
private void Awake()
{
    if (textComponent != null && textComponent.font != null)
    {
        textComponent.fontSharedMaterial = textComponent.font.material;
    }
}
```

### 方法3: Unity Editorで手動修正
1. Hierarchy → TextMeshProコンポーネントを選択
2. Inspector → Material Preset → **None**に変更
3. 再度 Font AssetのデフォルトMaterialを選択
4. **Ctrl+S**で保存

## 再発防止策

### 新規TextMeshProコンポーネント作成時
1. **TMPAutoFontMaterialコンポーネントを必ずアタッチ**
2. または、ベースクラス（例: BaseCardUI）で実装

### Font Asset変更時
1. **必ずPlayモードで動作確認**
2. 日本語テキストが正しく表示されるか確認
3. Console Logで "Broken text PPtr" エラーが無いか確認

### コードレビュー時
- TextMeshProのFont Assetを変更するPRでは、Material Preset同期を確認

## トラブルシューティング

### "Broken text PPtr" エラーが出る
**原因**: シーンファイルのm_sharedMaterialが存在しないFileIDを参照
**解決**: TMPAutoFontMaterialを使用、またはシーンファイルのm_sharedMaterialを修正

### Inspectorでは正しいMaterialが表示されているのに文字化け
**原因**: Inspector表示とシーン実際値が不一致
**解決**: TMPAutoFontMaterialを使用、または Unity Editorを再起動してシーンをリロード

## 関連ファイル
- `Assets/Scripts/UI/TMPAutoFontMaterial.cs` - 自動同期コンポーネント
- `Assets/Scripts/UI/SkillCardUI.cs` - 実装例（Line 31-43）
- `Assets/Scenes/05_Game.unity` - 問題が発生したシーン

## 参考情報
- Unity TextMeshPro仕様: Font Asset変更時にMaterial Presetは自動更新されない
- シーンファイルのm_sharedMaterialは手動またはコードで更新が必要
