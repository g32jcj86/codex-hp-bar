# 移除首次啟用／設定視窗

使用者回饋：不需要設定畫面。背景待命與 Windows 登入自動啟動一律開啟，不再詢問。

## 變更
1. 刪除 `src/CodexHpBar/OnboardingWindow.xaml` 與 `OnboardingWindow.xaml.cs`。
2. `App.OnStartup`（非 demo 時）：
   - `settings = (settingsService.Load() ?? AppSettings.Default) with { BackgroundMode = true, StartWithWindows = true }`，正規化後若與檔案內容不同（含檔案不存在）就 `Save`。
   - 一律 `StartupManager.RepairIfNeeded(true)`（捷徑不存在或指向舊路徑時重建）。
   - 不再顯示任何對話框。
3. `AppSettings.Default` 改為 `BackgroundMode = true, StartWithWindows = true`；角色圖片設定仍從 `settings.json` 讀取（只是沒有 UI 可改），預設內建菇菇寶貝。
4. 右鍵選單移除「設定」項目；刪除 `MainWindow.SettingsRequested`、`AppController.ShowSettings` 及只為它存在的程式碼（`MascotAssetStorage` 若因此完全沒人用，連同它的測試一起刪除；若仍有使用就保留）。
5. 更新測試：與預設值相關的期望值改成新預設；移除只測已刪除功能的測試。
6. 文件：`README.md` 的「三步驟快速開始」「第一次啟動會做什麼」「操作與設定」、`docs/使用指南.md` 中提到設定視窗／首次啟用勾選項的段落，改成「啟動後自動在背景待命並加入 Windows 登入自動啟動，無需設定」；說明可用 `--reset-settings` 清除設定與 Startup 捷徑。`CHANGELOG.md` 的「未發布」補一條。執行 `scripts/validate-docs.ps1` 的規則（相對連結存在、不用簡體用詞）。

## 不要做
- 不 commit、不 push、不啟動或關閉應用程式。

## 驗收
- `dotnet build` / `dotnet test` 通過（沙箱限制時用 `--no-restore`）。
- 最後回覆就是完成摘要。
