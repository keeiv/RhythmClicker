Development guide (詳細)
=======================

本文件針對有意在本專案上開發或協作的貢獻者提供更細緻的說明。

一、包含檔案（核心）
- `Game1.cs` — 遊戲主迴圈、場景切換（Menu/Playing/Result/Account）與遊玩邏輯。
- `Beatmap.cs` — beatmap 與 Note 定義。
- `AccountsManager.cs` — 本機帳號註冊/驗證（示範用途）。
- `TextRenderer.cs` — 運行時文字貼圖產生（Windows-only，建議改用 `SpriteFont`）。
- `ClickerGame.csproj` — 專案檔案，列出 NuGet 相依。
- `Assets/*.json` — 範例 beatmap 與歌曲清單。

二、環境與相依
- 建議使用 Windows 與 .NET 6/7、MonoGame DesktopGL (3.8.x)。
- 若要移除 System.Drawing 依賴以取得跨平台相容性，請改用 MonoGame `SpriteFont`：
   1. 在 `Content` 資料夾建立 `.spritefont`。
   2. 使用 MonoGame Content Pipeline 編譯字型資源。

三、建置與執行（本機）
1. 在專案根目錄執行：
```
dotnet build RhythmClicker/ClickerGame.csproj -c Debug
dotnet run --project RhythmClicker/ClickerGame.csproj -c Debug
```

四、常見開發任務
- 新增 UI 字型：使用 `SpriteFont` 取代 `TextRenderer`，並將字型放入 Content Pipeline。
- 調整判定：`Game1.cs` 中有 `hitWindow` 常數（目前為 0.30f），調整此值可改變擊打難度。
- 新增曲目：在 `Assets/` 放入音訊檔並新增對應的 beatmap JSON，更新 `Assets/songs.json`。

五、測試清單
- 能載入並播放範例歌曲。
- 按鍵 (D/F/J/K) 會觸發 key flash 並移除已擊中的 note。
- 未擊中的 note 會被移除且顯示 miss 反饋，不再阻塞輸入。
- Play 完成或全數音符清空後會跳至 Result 畫面。

六、提交與開發規範
- 詳見 `CONTRIBUTING.md`，包含 Issue、分支命名與 PR 流程。

七、安全提醒
- `AccountsManager` 儲存於 `Accounts/accounts.json`，目前僅示範用途；若要上線務必使用安全雜湊（PBKDF2/Argon2）並加 salt。

八、其他資源
- 若要將專案導向更完整的 osu!-like 體驗，可以考慮：
   - 把圖形/動畫抽成 Scene/Renderer 模組
   - 增加判定分級（Perfect/Great/Good/Miss）與連擊系統
   - 增加配置檔與設定頁面（解析度、音量、按鍵綁定）
