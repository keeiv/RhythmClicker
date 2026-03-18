# Contributing / 開發指南

歡迎來到 RhythmClicker！感謝你願意協助改進此專案。以下為快速上手與貢獻流程。

## 快速概覽
- 本倉庫包含遊戲核心程式碼（ prototype 版），遊戲仍在開發中。
- 若要本機編譯與執行，請參照 `DEVELOPMENT.md` 中的建置指令。

## 建置與執行（開發者）
1. 複製或 fork 此倉庫到你的 GitHub 帳號，clone 到本機。
2. 在專案根目錄執行：

```
dotnet build RhythmClicker/ClickerGame.csproj -c Debug
dotnet run --project RhythmClicker/ClickerGame.csproj -c Debug
```

（本專案目前以 Windows / MonoGame 為主要測試平台；`TextRenderer` 使用 System.Drawing，跨平台時建議改用 `SpriteFont`。）

## 開發流程
- 請先建立 issue 說明你要處理的功能或修正（若是重大改動，先開 RFC 類型的 issue）。
- 開發前請以 `git checkout -b feat/描述-或-fix/描述` 建立分支。
- 完成後發 Pull Request（PR），PR 標題格式建議：`[feat] 新功能簡述` 或 `[fix] 錯誤簡述`。

## 程式碼風格
- 保持簡潔、直觀的命名與小型函式。使用 C# 最新語法特性（倉庫預設 `Nullable` 開啟）。
- 若修改公共 API（類別/方法簽章），請在 PR 描述中註明向下相容性情況。

## 測試與驗證
- 本原型尚無完整自動化測試；請在本地手動測試關鍵流程（載入曲目、播放、按鍵判定、存檔）。

## 授權與貢獻條款
- 此倉庫繼承上游授權（請確認 LICENSE 檔案）。PR 即表示你同意以專案所採用的授權方式貢獻程式碼。

## 聯絡
- 有任何問題請在 issue 中提出，或在 PR 中標註 Maintainers。
