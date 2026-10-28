# Claude Code 額度改用用量 API ＋ 提示視窗置頂

Claude 桌面 app 不會執行 status line，`claude-usage.json` 永遠不會產生。使用者同意改用本機登入權杖查詢用量 API。

## A. Claude 額度來源
已實測的請求與回應（節錄）：
```
GET https://api.anthropic.com/api/oauth/usage
Authorization: Bearer <accessToken>
anthropic-beta: oauth-2025-04-20
```
```json
{ "five_hour": { "utilization": 39.0, "resets_at": "2026-10-28T13:29:59.871539+00:00" },
  "seven_day": { "utilization": 6.0,  "resets_at": "2026-10-28T17:59:59.871576+00:00" },
  "seven_day_opus": null, "...": "其他欄位忽略" }
```
`utilization` 是已用百分比（0–100），任一 window 可能是 `null`。

1. 權杖：讀 `%USERPROFILE%\.claude\.credentials.json`（若有環境變數 `CLAUDE_CONFIG_DIR` 就改讀該目錄）的 `claudeAiOauth.accessToken` 與 `expiresAt`（Unix 毫秒）。
   - 檔案不存在 → Offline「找不到 Claude Code 登入資料」。
   - 已過期 → Offline「Claude Code 權杖已過期，開啟 Claude Code 後會自動更新」。**絕對不要自行用 refreshToken 刷新**（會讓 Claude Code 的登入失效）。
   - 權杖只存在記憶體、只送往 `https://api.anthropic.com`，不寫 log、不出現在錯誤訊息或 tooltip。
2. `ClaudeUsageParser.Parse(string json, DateTimeOffset now)` 改解析上述 API 回應：`five_hour` → `RateLimitWindow(utilization, 300, resets_at 秒)`、`seven_day` → `RateLimitWindow(utilization, 10080, ...)`；`null`、缺欄位或已過 `resets_at` 的 window 丟掉；`UpdatedAt = now`。更新 `QuotaTests` 中的解析測試（兩個 window、只有 seven_day、null window、過期、無效 JSON）。權杖讀取的純邏輯（解析 credentials JSON、判斷過期）也放 Core 並加測試。
3. App 新增 Claude 來源類別（共用一個 `HttpClient`，逾時 10 秒），由 `AppController.RefreshAsync` 呼叫，但**最多每 60 秒**真正打一次 API，其餘時間沿用上次結果。HTTP 401/403 → Offline「Claude Code 登入已失效」；其他失敗 → 若有上次成功資料就標 `IsStale = true` 保留，否則 Offline。
4. 刪除 status line 方案：`scripts/claude-statusline.ps1`、`SettingsService.ClaudeUsagePath` 與讀 `claude-usage.json` 的程式碼。
5. 文件：README 的「Claude Code 額度來源」改寫為用量 API 說明（讀取本機 `.credentials.json` 的權杖、只送往 api.anthropic.com、不刷新權杖、未公開介面可能變動）；功能特色中「不讀取登入權杖」改成精確描述（Codex 不讀權杖；Claude Code 需讀本機權杖查詢用量）。`CHANGELOG` 未發布區把 statusline 那條改成用量 API。

## B. 提示視窗被蓋住
`AppController` 每 50ms 呼叫 `MainWindow.BringAboveTaskbar()`（`SetWindowPos(HWND_TOPMOST)`），把小工具拉到 topmost 最上層，連自己的 tooltip popup 也被蓋住。
- `MainWindow` 改用 `ToolTip` 物件（內容照舊），`Opened`／`Closed` 時記錄／清除 tooltip popup 的 HWND（`PresentationSource.FromVisual(toolTip)` 取 `HwndSource.Handle`）。
- `BringAboveTaskbar()` 在拉自己之後，若 tooltip 開著，再對 tooltip HWND 呼叫一次 `SetWindowPos(HWND_TOPMOST, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)`。

## 不要做
- 不改 Codex 額度讀取、不 commit、不 push、不啟動或關閉應用程式。
- 沙箱內不要實際呼叫 API（CC 會在外面驗證）。

## 驗收
- `dotnet build` / `dotnet test` 通過（沙箱限制時用 `--no-restore`）。
- 最後回覆就是完成摘要。
