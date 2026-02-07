# Area Configuration System - 使い方ガイド

## 概要

このシステムでは、**ScriptableObject**を使用してArea毎のWave Stage設定を管理します。
複数のAreaを簡単に追加でき、Scene分割が不要なため管理が楽になります。

---

## セットアップ手順

### 1. AreaConfig アセットの作成

1. Projectウィンドウで右クリック
2. `Create > Game > Area Configuration` を選択
3. アセット名を `Area1Config` などに変更
4. Inspectorで以下を設定:
   - **Area Name**: "Forest Area" など
   - **Area Number**: 1
   - **Wave Stages**: EnemySpawnerと同じ設定
   - **Background Sprite** (オプション): エリア専用背景
   - **BGM Clip** (オプション): エリア専用BGM

### 2. 複数エリアの作成

- `Area1Config`, `Area2Config`, `Area3Config` などを作成
- それぞれ異なるWave Stages設定を持たせる

推奨フォルダ構成:
```
Assets/
  ├─ Data/
  │   ├─ AreaConfigs/
  │   │   ├─ Area1Config.asset
  │   │   ├─ Area2Config.asset
  │   │   └─ Area3Config.asset
```

---

## 使用方法

### A. GameSessionを使った自動読み込み（推奨）

#### 03_AreaSelect シーンでの設定

1. **AreaSelectManager** を空のGameObjectにアタッチ
2. Inspectorで設定:
   - **Available Areas**: 作成したAreaConfigをドラッグ&ドロップ
   - **Game Scene Name**: "05_Game"
3. UIボタンのOnClickに `AreaSelectManager.SelectArea1()` などを設定

#### 05_Game シーンでの設定

1. **EnemySpawner** のInspectorで:
   - **Use Wave System**: ON
   - **Use Area Config**: ON ✅
   - **Area Config**: 空欄でOK（GameSessionから自動読み込み）
   - **Wave Stages**: 空でもOK（上書きされる）

2. ゲーム実行時:
   - AreaSelectで選択されたエリアが自動的に読み込まれる
   - コンソールに `[EnemySpawner] Loaded area config from GameSession: Area 1` と表示

---

### B. 直接AreaConfigを設定する方法

1. **EnemySpawner** のInspectorで:
   - **Use Wave System**: ON
   - **Use Area Config**: ON
   - **Area Config**: 使用したいAreaConfigをドラッグ&ドロップ
   - **Wave Stages**: 空でOK

2. この方法は:
   - テスト時に便利
   - 単一エリアのゲームに最適
   - AreaSelectを経由せずに直接Gameシーンを実行できる

---

### C. スクリプトから動的に設定

```csharp
// GameSession経由
GameSession.SelectedArea = myAreaConfig;
SceneManager.LoadScene("05_Game");

// または直接設定
EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
spawner.SetAreaConfig(myAreaConfig);
```

---

## シーン遷移フロー

```
01_Title (タイトル画面)
   ↓
03_AreaSelect (エリア選択)
   ↓ プレイヤーがArea選択
   ↓ GameSession.SelectedArea = 選択されたArea
   ↓
05_Game (ゲーム本編)
   ↓ EnemySpawner.Start()で自動的にGameSessionから読み込み
   ↓ 選択されたエリアのWave Stagesでゲーム開始
```

---

## トラブルシューティング

### エラー: "Wave System is enabled but no Wave Stages are set"

**原因**: Area Configが設定されていない、またはWave Stagesが空

**解決方法**:
1. AreaConfigアセットを開き、Wave Stagesに設定があるか確認
2. EnemySpawnerの `Use Area Config` がONになっているか確認
3. GameSessionに正しくエリアが設定されているか確認

### エラー: "Area Config is invalid"

**原因**: AreaConfigのWave Stagesが空

**解決方法**:
- AreaConfigアセットを開き、Wave Stagesに最低1つのステージを設定

### ゲームシーンで設定が反映されない

**確認事項**:
1. AreaSelectManagerで `SelectArea()` が呼ばれているか
2. コンソールに `[GameSession] Area: ...` のログが出ているか
3. EnemySpawnerの `Use Area Config` がONか

---

## 拡張例

### 背景とBGMの切り替え

```csharp
// Gameシーンで背景・BGMを切り替える例
public class GameSceneManager : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private AudioSource bgmSource;

    private void Start()
    {
        if (GameSession.HasValidArea())
        {
            AreaConfig area = GameSession.SelectedArea;

            // 背景設定
            if (area.backgroundSprite != null)
            {
                backgroundRenderer.sprite = area.backgroundSprite;
            }
            else
            {
                Camera.main.backgroundColor = area.backgroundColor;
            }

            // BGM設定
            if (area.bgmClip != null)
            {
                bgmSource.clip = area.bgmClip;
                bgmSource.volume = area.bgmVolume;
                bgmSource.Play();
            }
        }
    }
}
```

### 難易度に応じた調整

```csharp
// 難易度に応じて敵のHPを調整する例
int enemyHP = baseHP * area.difficultyLevel;
```

---

## まとめ

✅ **メリット**:
- エリア追加が簡単（ScriptableObjectを作るだけ）
- Sceneは1つで管理が楽
- データとロジックが分離
- テストが容易

✅ **推奨構成**:
- AreaSelectManager: エリア選択UI管理
- GameSession: Scene間データ受け渡し
- EnemySpawner: AreaConfigから自動読み込み
- AreaConfig: エリア毎の設定を保存

これでArea毎に異なるWave構成を簡単に管理できます！
