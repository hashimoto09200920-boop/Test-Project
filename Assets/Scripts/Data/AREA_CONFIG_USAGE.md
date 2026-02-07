# AreaConfig 使い方ガイド（インデックスベース版）

## 重要な変更点

**FormationEntryが`Transform参照`から`インデックス`に変更されました**。

これにより、**AreaConfigにWave Stages設定を保存可能**になりました。

---

## 基本構造

```
EnemySpawner (Scene内)
├─ Spawn Points配列         ← Scene内のTransform
│   ├─ [0] Pos_01
│   ├─ [1] Pos_02
│   ├─ [2] Pos_03
│   └─ [3] Pos_04 ...
└─ Use Area Config: ON

AreaConfig (Project内アセット)
└─ Wave Stages
    └─ Formations
        └─ Entries
            ├─ Spawn Point Index: 0  ← Pos_01を指すインデックス
            ├─ Spawn Point Index: 1  ← Pos_02を指すインデックス
            └─ Spawn Point Index: 2  ← Pos_03を指すインデックス
```

---

## セットアップ手順

### 1. EnemySpawnerのSpawn Points設定（Scene側）

1. **05_Game** シーンを開く
2. **EnemySpawner** を選択
3. Inspector の **Spawn Points** を設定
   ```
   Spawn Points
   ├─ Size: 4
   ├─ Element 0: Pos_01
   ├─ Element 1: Pos_02
   ├─ Element 2: Pos_03
   └─ Element 3: Pos_04
   ```

**重要**: この配列の順番を覚えておく（インデックスとして使用）

---

### 2. AreaConfig作成と設定

#### AreaConfig作成
1. Project右クリック → `Create > Game > Area Configuration`
2. `Area1Config` と命名

#### Wave Stages設定
```
Area1Config
├─ Area Name: "Forest Area"
├─ Area Number: 1
└─ Wave Stages
    └─ Size: 1
        └─ Element 0 (Stage 1)
            ├─ Stage Name: "Stage 1"
            ├─ Formations
            │   └─ Size: 1
            │       └─ Element 0
            │           ├─ Formation Name: "First Wave"
            │           └─ Entries
            │               └─ Size: 3
            │                   ├─ Element 0
            │                   │   ├─ Spawn Point Index: 0  ← Pos_01
            │                   │   └─ Enemy Data: SnakeEnemy
            │                   ├─ Element 1
            │                   │   ├─ Spawn Point Index: 1  ← Pos_02
            │                   │   └─ Enemy Data: SniperEnemy
            │                   └─ Element 2
            │                       ├─ Spawn Point Index: 2  ← Pos_03
            │                       └─ Enemy Data: SnakeEnemy
            ├─ Time Limit: 60
            └─ Clear On Time Expired: ☑
```

---

## インデックスの対応表

EnemySpawnerの Spawn Points 配列とインデックスの対応:

| インデックス | Spawn Point名 | 説明 |
|------------|--------------|------|
| 0 | Pos_01 | 最初の位置 |
| 1 | Pos_02 | 2番目の位置 |
| 2 | Pos_03 | 3番目の位置 |
| 3 | Pos_04 | 4番目の位置 |
| ... | ... | ... |

**AreaConfigでは数値でインデックスを指定**:
- `Spawn Point Index: 0` → Pos_01を使用
- `Spawn Point Index: 1` → Pos_02を使用

---

## 設定例

### 例1: 3体の敵を異なる位置に配置

```
Formation Entry 0:
├─ Spawn Point Index: 0  ← 左側の位置
└─ Enemy Data: SnakeEnemy

Formation Entry 1:
├─ Spawn Point Index: 2  ← 中央の位置
└─ Enemy Data: BossEnemy

Formation Entry 2:
├─ Spawn Point Index: 4  ← 右側の位置
└─ Enemy Data: SnakeEnemy
```

### 例2: 同じ位置に複数の敵（時間差スポーン）

```
Formation Entry 0:
├─ Spawn Point Index: 1
└─ Enemy Data: SnakeEnemy

Formation Entry 1:
├─ Spawn Point Index: 1  ← 同じインデックス
└─ Enemy Data: SniperEnemy
```

---

## コピーツールの使用

既存のEnemySpawner設定をAreaConfigにコピーできます:

### 方法
1. Unity メニュー → `Tools > Area Config Helper`
2. **Source Spawner**: 05_GameのEnemySpawnerをドラッグ
3. **Target Area Config**: 作成したAreaConfigをドラッグ
4. **Copy Wave Stages** ボタンをクリック

**注意**:
- 旧形式（Transform参照）→ 新形式（インデックス）に変換されます
- Spawn PointsがEnemySpawnerに設定されている必要があります

---

## トラブルシューティング

### エラー: "Invalid spawn point index X"

**原因**: 指定されたインデックスがSpawn Points配列の範囲外

**解決方法**:
1. EnemySpawnerのSpawn Pointsのサイズを確認
2. AreaConfigのインデックスが `0 ～ (サイズ-1)` の範囲内か確認
3. 例: Spawn Points が4個なら、インデックスは0～3

### エラー: "Spawn Point at index X is null"

**原因**: Spawn Points配列の要素がNone(空)

**解決方法**:
1. EnemySpawnerのSpawn Pointsを確認
2. 全てのElementにTransformが設定されているか確認

---

## まとめ

### メリット
✅ AreaConfigに全てのWave設定を保存可能
✅ ScriptableObject として管理できる
✅ 複数Areaを簡単に切り替え可能
✅ Spawn PointsはScene内で一元管理

### 設定フロー
1. **Scene側**: EnemySpawnerにSpawn Pointsを設定（1回だけ）
2. **Asset側**: AreaConfigで各Areaの敵配置をインデックスで指定
3. **実行時**: GameSessionで選択したAreaConfigが読み込まれる

これでArea毎に異なるWave構成を完全に管理できます！
