# Contributing / 開發指南（詳細）

感謝你對 RhythmClicker 的興趣！本文件說明如何為此專案貢獻、建立開發環境與專案流程規範。

1) 問題回報（Issue）
- 如發現 bug、崩潰或想提出新功能（feature），請先建立 Issue，內容應包含：
  - 問題摘要（英文或中文皆可）
  - 重現步驟（越詳盡越好）
  - 相關平台與環境（Windows 版本、.NET SDK、MonoGame 版本）

2) 分支與提交規範
- 請從 `main` 建立功能分支，分支命名範例：`feat/<短描述>`、`fix/<短描述>`、`chore/<短描述>`。
- commit 訊息請清楚描述變更，格式建議：`[type] 簡短描述`，例如 `[feat] 新增選單效果`、`[fix] 修正 miss 判定`。

3) Pull Request（PR）流程
- 在 PR 描述中包含變更摘要、如何測試，以及可能的回歸風險。
- 若 PR 為重大變動或破壞相容性，請在標題與描述中標注（breaking change），並先在 Issue 中討論。
- 指派 reviewers 並等待至少一位 maintainer 批准後合併。

4) 建置與本機環境
- 必要工具：.NET SDK（建議使用 .NET 6/7）、MonoGame DesktopGL（3.8.x）。
- 範例建置指令：
```
dotnet build RhythmClicker/ClickerGame.csproj -c Debug
dotnet run --project RhythmClicker/ClickerGame.csproj -c Debug
```
- 建議使用 Windows 進行開發與測試（目前 `TextRenderer` 使用 System.Drawing），若在 macOS/Linux 上開發，請先改用 `SpriteFont`。

5) 資產管理
- 本 export 不含大量資產（大型音訊、圖檔）。新增示例資源請放在 `Assets/` 並註明來源及授權。

6) 程式碼風格與品質
- 使用清晰命名、單一職責函式與小型類別。避免大型方法與過深繼承。
- 保持 nullable 參考類型設定（`<Nullable>enable</Nullable>`）。

7) 測試與驗證
- 本原型尚未加入 CI 或自動化測試，請在本機執行以下檢查：
  - 能載入並播放示例音訊
  - 按鍵判定（D/F/J/K）正確，miss 不阻塞
  - 進入/離開全螢幕（Esc 回選單）
  - 帳號註冊能存檔 `Accounts/accounts.json`

8) 帳號與安全性注意
- `AccountsManager` 目前使用 SHA256（無 salt）僅供示範，若要投入實際服務務必改用更安全的 hash（例如 PBKDF2、Argon2）並加上 salt 與適當的密碼策略。

9) 本地開發建議
- 如果你專注 UI：先改用 `SpriteFont` 與簡易按鈕元件以提升跨平台兼容性。
- 如果你專注音訊/同步：在不同硬體上測試音軌延遲與同步行為，考慮使用更精準的時間基準。

10) 聯絡與支援
- 請透過 Issue 提問或送 PR，維護者會在收到變更請求後回覆。

謝謝你的貢獻！
