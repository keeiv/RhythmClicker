# RhythmClicker — 節奏遊戲

> **最新版本：v0.2-beta** — Blue Archive 風格音樂、Discord 整合、統計系統、譜面編輯器等重大更新。

一款使用 MonoGame (DesktopGL) 建置的 4 軌落鍵節奏遊戲。具備現代化暗色 UI、5 語言支援、帳號系統與 Discord Rich Presence。

## 下載

**[下載最新版本 (Windows x64)](https://github.com/keeiv/RhythmClicker/releases/latest)** — 解壓縮後直接執行 `ClickerGame.exe`，不需安裝 .NET。

## v0.2-beta 新功能

- **Blue Archive 風格音樂** — 3 首合成原創歌曲（Unwelcome School / Constant Moderato / Midsummer Daydream）
- **Discord Rich Presence** — 自動顯示遊戲狀態於 Discord
- **SQLite 統計系統** — 玩家遊玩記錄、等級分佈、最佳成績
- **譜面編輯器** — 滑鼠拖曳放置方塊、匯入音檔、驗證與存儲
- **4 難度** — EZ / HD / DIFF / VDIFF，左右鍵快速切換
- **帳號系統** — 本機登入/註冊 + AES-256-CBC 加密
- **UI 現代化** — 扁平按鈕、Pill 標籤、更線條化的版面

## 快速開始

```bash
git clone https://github.com/keeiv/RhythmClicker.git
cd RhythmClicker
dotnet restore ClickerGame/ClickerGame.csproj
dotnet run --project ClickerGame/ClickerGame.csproj
```

> 首次啟動會自動產生音訊與譜面，需等待數秒。

## 操作方式

| 按鍵 | 功能 |
|------|------|
| `D` `F` `J` `K` | 打擊四欄位 |
| `◀` `▶` | 切換難度 |
| `Tab` | 切換歌曲 |
| `↑` `↓` `Enter` | 選單導航 |
| `Esc` | 返回 / 離開 |

詳細操作與譜面編輯器使用說明請參閱 [`ClickerGame/README.md`](ClickerGame/README.md)。

## 回報問題或貢獻

想貢獻請先閱讀 `CONTRIBUTING.md`，有任何錯誤或改善建議請開 issue。

