# RhythmClicker — MonoGame 節奏遊戲

> **最新版本：v0.2-beta** — 加入 Blue Archive 風格音樂、Discord 整合、統計系統、譜面編輯器等重大更新。

---

## 功能總覽

- **4 軌落鍵節奏玩法** — 使用 `D` `F` `J` `K` 四鍵對應四欄位
- **6 首內建歌曲** — 3 首原創範例 + 3 首 Blue Archive 風格合成音樂
- **4 種難度** — EZ / HD / DIFF / VDIFF
- **判定系統** — PERFECT / GREAT / GOOD 三階段判定 + 連擊數（Combo）
- **視覺特效** — 打擊粒子、畫面震動、節拍脈衝、5 級 Combo 階段色彩
- **Discord Rich Presence** — 自動顯示遊戲狀態（選單、遊玩中、編輯器、結算）
- **SQLite 統計系統** — 記錄每次遊玩、等級分佈、最佳分數與連擊
- **譜面編輯器** — 滑鼠拖曳放置方塊、匯入音檔、預覽播放、存儲為 .rcm 譜面
- **帳號系統** — 本機登入 / 註冊（AES-256-CBC 加密存儲）
- **5 語言支援** — 繁體中文 / English / 日本語 / 한국어 / Español
- **自訂 .rcm 譜面格式** — 加密譜面檔案，雙擊可匯入遊戲

---

## 快速上手

### 系統需求

- Windows 10/11（64-bit）
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) 或以上
- 建議安裝 [Visual Studio Code](https://code.visualstudio.com/) 或 Visual Studio 2022

### 安裝與執行

```bash
# 1. 複製專案
git clone https://github.com/keeiv/RhythmClicker.git
cd RhythmClicker

# 2. 還原 NuGet 套件
dotnet restore ClickerGame/ClickerGame.csproj

# 3. 建置
dotnet build ClickerGame/ClickerGame.csproj

# 4. 執行遊戲
dotnet run --project ClickerGame/ClickerGame.csproj
```

> 首次啟動時，遊戲會自動產生所有歌曲音訊與譜面檔案到 `Assets/` 目錄，需等待數秒。

---

## 操作方式

| 按鍵 | 功能 |
|------|------|
| `D` `F` `J` `K` | 打擊四欄位 |
| `◀` `▶` | 切換難度（EZ / HD / DIFF / VDIFF） |
| `Tab` | 切換歌曲 |
| `↑` `↓` | 選單導航 |
| `Enter` | 確認 |
| `Esc` | 返回 / 離開 |
| `F1`（帳號頁）| 切換登入 / 註冊模式 |

### 譜面編輯器

| 操作 | 功能 |
|------|------|
| 左鍵 | 放置音符方塊 |
| 拖曳 | 移動方塊位置 |
| 右鍵 | 刪除方塊 |
| 滾輪 | 捲動時間軸 |
| `Tab` | 切換文字欄位（名稱 / 作者 / BPM） |
| `Space` | 預覽播放 |
| `Ctrl+S` | 存儲譜面 |
| 拖放 `.wav` 檔案到視窗 | 匯入音檔 |

> 完成譜面至少需要：音檔 + 至少一個方塊 + 歌曲名稱 + 作者名

---

## 內建歌曲列表

| 歌曲 | BPM | 難度 | 風格 |
|------|-----|------|------|
| Example A | 120 | EZ HD DIFF VDIFF | 基礎節奏 |
| Example B | 130 | EZ HD DIFF | 中速節奏 |
| Example C | 100 | EZ HD DIFF VDIFF | 慢速節奏 |
| Unwelcome School | 170 | EZ HD DIFF VDIFF | BA 風格 · 明快搖滾 |
| Constant Moderato | 132 | EZ HD DIFF VDIFF | BA 風格 · 鋼琴流行 |
| Midsummer Daydream | 155 | EZ HD DIFF VDIFF | BA 風格 · 電子活力 |

> BA 風格歌曲為程式合成的原創音樂，模擬 Blue Archive 的明亮鋼琴音色與流行節拍。並非官方音樂。

---

## 專案結構

```
ClickerGame/
├── Game1.cs            # 主遊戲邏輯（狀態機、繪圖、輸入、音訊生成）
├── Beatmap.cs          # 譜面資料結構
├── AccountsManager.cs  # 帳號登入/註冊（加密存儲）
├── StatsDatabase.cs    # SQLite 遊玩統計
├── DiscordRpcManager.cs# Discord Rich Presence
├── RcFileManager.cs    # .rcm/.rcp/.rc 加密檔案格式
├── Localization.cs     # 多語言系統
├── TextRenderer.cs     # GDI 文字渲染
├── RenderCache.cs      # 漸層背景快取
├── ObjectPool.cs       # 物件池
├── GameConfig.cs       # 遊戲參數設定
└── Assets/             # 歌曲、譜面、圖示
```

---

## 如何貢獻

1. Fork 並建立分支
2. 修正或新增功能後，提出 PR
3. 請遵守 `CODING_STYLE.md` 中的規範

---

## 授權

MIT License — 詳見 [LICENSE](../LICENSE)