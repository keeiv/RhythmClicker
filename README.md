# RhythmClicker — 核心匯出

本倉庫只包含 RhythmClicker 原型的核心原始碼（遊戲循環、beatmap 處理、基本 UI 與本機帳號管理）。

重要說明：遊戲仍在開發中，非完整版本。UI、資源、音訊與更多功能仍需補完與優化。

請參閱 `DEVELOPMENT.md` 與 `CONTRIBUTING.md` 取得建置、執行與協作說明。

快速開始（Windows）
1. 取得此 repository：`git clone https://github.com/keeiv/RhythmClicker.git`
2. 建置：
```
dotnet build RhythmClicker/ClickerGame.csproj -c Debug
```
3. 執行：
```
dotnet run --project RhythmClicker/ClickerGame.csproj -c Debug
```

跨平台注意事項
- `TextRenderer.cs` 使用 `System.Drawing.Common`（目前主要在 Windows 測試）；若要跨平台建議改用 MonoGame `SpriteFont`。

回報問題或貢獻
- 想貢獻請先閱讀 `CONTRIBUTING.md`，有任何錯誤或改善建議請開 issue。

