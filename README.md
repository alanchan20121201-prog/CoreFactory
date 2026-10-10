# 遊戲程式（2D 版）—— Godot 4.7 Mono / C#

本專案是**唯一的遊戲版本**（原 3D 版已於 v0.2.0 移除，原始檔封存於倉庫外）。

**模擬核心自帶**：`src/Core/` 是純 C#、零引擎依賴的模擬核心（原與 3D 版共用，現已併入本專案）。
`tools/Bench` 壓測直接連結 `game2d/src/Core`，確保壓的就是遊戲真正跑的那份模擬。

## 怎麼玩（發行版）

**單一檔案**：`dist/CoreFactory2D_v0.2.0.exe`（約 190 MB），直接雙擊即可玩，
**不需任何附帶資料夾**（.NET 組件與資源已全部內嵌進 EXE）。

## 怎麼開（開發）

1. 開 **Godot 4.7.2 Mono**（`桌面/Godot_v4.7.2-stable_mono_win64/`）
2. `Import` → 選本資料夾的 `project.godot` → Import & Edit
3. `F5` 執行（主場景 `scenes/Main.tscn`）

```bash
# 編譯（0 警告 0 錯誤）
cd game2d && dotnet build

# 自我測試（headless 可跑；退出碼 0 = 全過）
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path game2d -- --selftest

# 打包「單一 EXE」（dotnet/embed_build_outputs=true，內嵌全部 .NET 組件）
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path game2d \
  --export-release "Windows Desktop" "build/CoreFactory2D.exe"
```

## 操作

| 操作 | 鍵 |
| --- | --- |
| 平移視角 | `WASD` / 方向鍵（螢幕速度固定） |
| 縮放 | 滑鼠滾輪（以游標為錨點）；中鍵拖曳平移 |
| 選建造工具 | `1-9`,`0` 或點底部工具列；`[` `]` 循環全部工具 |
| 放置 | 左鍵（放置前有幽靈預覽，綠=可放 紅=不可放，並標出進/出料方向） |
| 拆除 | 右鍵；或 `X` 切換「拆除模式」後用左鍵拆 |
| 旋轉朝向 | `R`（綠=出貨、藍=進料） |
| 切換配方 | `Ctrl` + 滾輪（熔煉爐 / 組裝機） |
| 複製 / 貼上藍圖 | `C` / `V`（`CFBP1` 字串，可分享） |
| 小地圖 / 研究 | `M` / `U` |
| 圖鑑 / 說明 | `F1` / `H` |
| 設定 | `Esc`（語言/音量/威脅倍率/全螢幕/格線/重開教學） |
| 隱藏介面 / 格線 / 區塊邊界 | `F2` / `F3` / `F4` |
| 暫停模擬 | `空白` |

## 遊戲內容

- **完整建造循環**：採礦機 → 傳送帶 → 熔煉爐 → 組裝機 → 倉庫；電力網（太陽能/火力/蓄電池）與邏輯電路（感測器→比較器→執行器）
- **動態難度**：蟲潮會依你現在的實力（建築數/砲塔數/研究/擊殺）自動變強，無限玩下去也不單調（對數壓制避免滾雪球）
- **每顆星球有自己的蟲**：苔原星 = 潮蟲/迅蟲/毒液蟲；灰燼星 = 全蟲種且更兇（血量/傷害加成）。未來加星球、加蟲種、加建築礦物都很容易擴
- **5 種蟲潮**：潮蟲、迅蟲、毒液蟲、重甲蟲、巢主（後期菁英），波次越高越兇、越高階蟲種越多
- **新手教學**：首次啟動從零開始，左上角循序目標（放礦機→接帶→熔爐→倉庫→供電→砲塔→研究），完成自動前進並提示
- **中文 UI**：頂列狀態（含動態威脅、下波倒數）、懸停資訊欄（名稱+說明+狀態）、圖鑑、事件饋送、小地圖
- **程序化音樂與音效**：零音檔，即時合成氛圍音樂 + 放置/拆除/警報/研究/摧毀音效（音量可調）
- **晝夜與天氣視覺**：夜間環境光 + 路燈光暈、雨/灰燼/離子風暴粒子、閃電
- **礦脈分散生成**：兩層雜訊，礦脈成小簇散佈而非一整片

## 開發參數（放在 `--` 之後）

| 參數 | 作用 |
| --- | --- |
| `--selftest` | 跑 5 組回歸測試後退出 |
| `--shot=PREFIX` | 固定幀數截圖後退出 |
| `--seed=N` / `--planet=N` | 世界種子 / 星球（0 苔原 / 1 灰燼） |
| `--rig` | 建示範產線（開發/截圖用；預設關閉，正式開局從零開始） |
| `--aliens=N` | 在產線旁生成 N 隻蟲（會輪流出現全部 5 種） |

## 座標系統（改這裡之前先讀）

- 沿用 Core 的 `(X, Z)` 整數格座標：**X → 螢幕 x、Z → 螢幕 y，不翻轉、不交換**
- `North = -y`；1 格 = 32 px；區塊 = 32×32 格；座標軟上限 **±32760**
- 所有轉換集中在 `src/View2D/Grid/GridCoord.cs`

## 專案結構

```
game2d/
├── project.godot / CoreFactory2D.csproj / export_presets.cfg
├── data/core.json                # 可調數值包
├── build/                        # 匯出輸出（單一 EXE）
├── tools/inspect_shot.py         # 截圖像素驗收
└── src/View2D/
    ├── Main2D.cs                 # 接線 + tick + 建造互動 + 藍圖 + 事件偵測
    ├── CameraController2D.cs     # 平移 / 縮放 / 夾限（Ctrl+滾輪讓給配方）
    ├── Ambient2D.cs              # 晝夜環境光 + 天氣粒子 + 閃電
    ├── Audio2D.cs                # ★ 程序化音樂 + 音效（零音檔）
    ├── Fonts2D.cs                # CJK 系統字型（微軟雅黑）
    ├── Settings2D.cs             # 設定讀寫（user://settings.json）
    ├── Tutorial2D.cs             # 新手教學目標
    ├── Ui2D.cs                   # 工具列/頂列/資訊/圖鑑/研究/設定/小地圖/教學
    ├── StarterRig2D.cs / SelfTest2D.cs
    ├── Grid/                     # GridCoord / Chunk / ChunkManager / WorldGrid2D
    └── View/
        ├── Art2D.cs              # ★ 程序化向量精靈（建築/物品/礦脈/蟲）
        ├── ChunkView2D.cs        # 地形（抖色+裝飾+礦簇）/ 實體層
        ├── Overlay2D.cs          # 游標 + 放置幽靈 + 蟲潮 + 夜間燈光
        └── DrawLayer2D.cs / Palette2D.cs / DebugHud2D.cs
```

## 存檔格式

`SimWorld.SaveVersion = 5`：v4 + 蟲潮 `Kind`。舊 v4 存檔仍可讀。3D 版共用同一份 Core → 兩邊同步升級、互不破壞。
