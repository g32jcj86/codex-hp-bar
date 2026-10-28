# Claude Code + Codex 雙儀表

延續 `quota-rings.md`。使用者回饋：要 Claude Code 與 Codex **各一個**圓環儀表，用品牌色區分（不使用 logo），數字寫在圓圈**中間**，不要寫在旁邊。

## 版面（工作列小工具，高 38 DIP）
由左到右：吉祥物（沿用原本 34×34 槽位與動畫行為，回到 quota-rings 之前的畫法）→ Claude Code 儀表 → Codex 儀表。
- 每個儀表 36×36 DIP，兩個儀表間距 4 DIP。拿掉右側文字區與深色長條底。
- 視窗寬度從 150 縮成剛好容納內容（約 118 DIP）：改 `MainWindow.xaml` 的 Width 與 `TaskbarGeometry.Calculate` 的 150，並同步更新 `QuotaTests` 中依寬度計算的期望值。

## 儀表畫法（共用一個繪製方法，參數是 snapshot 與一組品牌色）
- 外圈 = 每週額度（`OrderedWindows` 中時長最長者）；內圈 = 5 小時額度（有兩個 window 時時長較短者）。只有一個 window 時只畫外圈。
- 圓環：深色底軌 + 從 12 點鐘順時針、依剩餘百分比畫的圓頭弧（沿用現有 `DrawRing`）。線寬 3 DIP，內外圈間距 1 DIP。
- 圓心先畫深色實心圓（`Track`）當文字底，再畫數字（不含 `%`，`FormattedText`、Segoe UI Bold，DPI 換算沿用 `DrawQuotaText` 的寫法）：
  - 只有一圈：一行，週剩餘數字，字級約 11 DIP，顏色 = 外圈色。
  - 兩圈：兩行置中堆疊，上行週（外圈色）、下行 5h（內圈色），字級約 8 DIP。`100` 也要塞得下圓心。
  - 沒資料：灰色空圈，中心顯示 `--`。
- 配色：
  - Claude Code：外圈 `#D97757`（Claude 橘），內圈 `#F2B8A0`（淺橘）。
  - Codex：外圈 `#10A37F`（OpenAI 綠），內圈 `#7DD3B8`（淺綠）。
- 低額度青點、stale 金點：畫在各自儀表右上角（只針對該儀表的 snapshot）。

## Claude Code 額度資料
資料來源是 Claude Code status line 的 stdin JSON（官方欄位）：
```json
{ "rate_limits": {
    "five_hour": { "used_percentage": 23.5, "resets_at": 1738425600 },
    "seven_day": { "used_percentage": 41.2, "resets_at": 1738857600 } } }
```
任何一個 window 都可能不存在。

1. 新增 `scripts/claude-statusline.ps1`（Windows PowerShell 5.1 可執行）：
   - 從 stdin 讀 JSON；若有 `rate_limits`，把 `{ "rate_limits": <原樣>, "updated_at": <unix 秒> }` 以 UTF-8（無 BOM）先寫暫存檔再 `Move-Item -Force` 覆蓋到 `%LOCALAPPDATA%\CodexHpBar\claude-usage.json`。沒有 `rate_limits` 就不動檔案。
   - stdout 輸出一行給 status line 顯示，例如 `Claude 5h 76% · 週 59%`（剩餘百分比；缺的 window 省略）。
2. `CodexHpBar.Core` 新增 `ClaudeUsageParser.Parse(string json, DateTimeOffset now)` 回傳 `QuotaSnapshot`：`five_hour` → `RateLimitWindow(used, 300, resets_at)`、`seven_day` → `RateLimitWindow(used, 10080, resets_at)`；`resets_at` 已過的 window 丟掉；`UpdatedAt` 取 `updated_at`；JSON 無效 → `QuotaSnapshot.Offline(錯誤訊息)`。在 `QuotaTests` 加測試：兩個 window、只有 seven_day、過期 window 被丟掉、無效 JSON。
3. App 端：`AppController.RefreshAsync` 每次也讀 `claude-usage.json`（不存在 → `Offline("尚未收到 Claude Code 額度")`），存成 `_claudeSnapshot`，與 Codex snapshot 一起傳給 `MainWindow.UpdateSnapshot(codex, claude)` → `QuotaWidget`（新增 `ClaudeSnapshot` 屬性）。檔案路徑與 `SettingsService` 用同一個 `%LOCALAPPDATA%\CodexHpBar` 資料夾。
4. Tooltip：分成「Claude Code」與「Codex」兩段，各自列出週 / 5 小時剩餘與重置時間（沿用 `TooltipBuilder` 格式）。
5. Demo：`--demo single/dual/low` 同時給 Claude 與 Codex 示範資料（Claude 用不同數字，方便辨認）。

## 不要做
- 不讀任何登入權杖或 `~/.claude/.credentials.json`。
- 不改 `~/.claude/settings.json`（CC 會自己設定 statusLine）。
- 不改 Codex 偵測／自動隱藏邏輯。
- 不 commit、不 push、不啟動或關閉應用程式。

## 驗收
- `dotnet build` 與 `dotnet test` 通過（restore 若因沙箱失敗就用 `--no-restore`，CC 會在沙箱外跑完整 `scripts/validate.ps1`）。
- **最後回覆**就是完成摘要：改了哪些檔、build/test 結果、偏離計畫之處與疑問（CC 會用 `-o` 收下，不要自己寫 `.agents/`）。
