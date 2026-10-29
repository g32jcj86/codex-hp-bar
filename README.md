# Claude Code + Codex 額度監測器

Codex HP Bar 是 Windows 11 工具列上的免安裝額度監測器。左邊的橘色儀表是 **Claude Code**、右邊的綠色儀表是 **Codex**，外圈顯示每週額度、內圈顯示 5 小時額度，剩餘百分比直接寫在圓心。

![工具列示意：左為 Claude Code、右為 Codex](docs/media/taskbar-demo.png)

## 功能特色

- Codex 透過官方本機 app-server 讀取額度，不讀取登入權杖；Claude Code 讀取本機登入權杖查詢用量 API，兩者皆不使用畫面辨識。
- Claude Code 與 Codex 各一個同心圓環儀表，同時顯示每週與 5 小時額度，數字寫在圓心；只有每週限制時自動切換成單一圓環。
- 自動顯示於所有 Windows 工具列，支援多螢幕與每螢幕 DPI。
- 不需開著 Codex 桌面程式，常駐工具列持續顯示兩組額度。
- 可攜版自帶 .NET Runtime，解壓後直接執行，不需要安裝。
- 啟動後自動在背景待命並加入 Windows 登入自動啟動，無需設定。

## 系統需求

- Windows 11 x64。
- 已安裝並登入 Codex 桌面應用程式（只需安裝，額度透過其內建的 `codex app-server` 讀取，不必開著）。
- 若要顯示 Claude Code 額度：已用 Claude Pro／Max 帳號登入 Claude Code（桌面 app 或 CLI 皆可）。未登入時 Claude Code 儀表顯示灰圈。
- 若從原始碼建置，需要 .NET 10 SDK。

## 三步驟快速開始

1. 從 [Releases](https://github.com/g32jcj86/codex-hp-bar/releases) 下載 `CodexHpBar-v0.2.2-win-x64-portable.zip`。
2. 解壓縮後執行 `CodexHpBar.exe`。
3. 啟動後工具列即會顯示額度，不需開啟 Codex 桌面程式。

免安裝版本可以放在任何可寫入的位置。移動 EXE 後請執行新位置的程式，Startup 捷徑會自動修復。

## 第一次啟動會做什麼

啟動後自動在背景待命並加入 Windows 登入自動啟動，無需設定，也不會顯示設定視窗。

- **背景待命**：Codex 關閉時儀表隱藏，但程式保留在背景，等待下次開啟 Codex。
- **登入 Windows 時自動啟動**：自動建立或修復目前使用者的 Startup 捷徑。

程式會自動儲存設定。設定檔位於 `%LOCALAPPDATA%\CodexHpBar\settings.json`，其中不包含登入資料。

## 如何閱讀額度圓環

由左到右依序是 **Claude Code**、**Codex** 儀表，以品牌色區分：

![四種狀態：每週 + 5 小時、只有每週、低額度、無法取得](docs/media/taskbar-states.png)

| 儀表 | 外圈（每週） | 內圈（5 小時） |
|---|---|---|
| Claude Code | Claude 橘 `#D97757` | 淺橘 `#F2B8A0` |
| Codex | OpenAI 綠 `#10A37F` | 淺綠 `#7DD3B8` |

- 圓環從 12 點鐘方向順時針填滿剩餘額度；沒有 5 小時限制時只顯示外圈。
- 剩餘百分比直接寫在圓心：只有一圈時顯示一個數字；兩圈時上行是每週、下行是 5 小時，顏色與對應圓環相同。
- 青色圓點：該儀表任一額度剩餘 20% 以下。
- 黃色圓點：資料暫時無法更新，仍顯示最後一次成功取得的資料。
- 灰色空圈與 `--`：目前無法取得額度。

百分比代表剩餘量，計算方式為 `100 - 已用百分比`。將滑鼠停在小工具上，提示會分成 Claude Code 與 Codex 兩段，列出各額度的剩餘量、本機時間的重置時刻與最後更新時間；無法取得時則顯示原因。

### Claude Code 額度來源

Claude Code 額度透過 `https://api.anthropic.com/api/oauth/usage` 查詢，每 60 秒最多查詢一次，其餘時間沿用上次結果。

程式從 `%USERPROFILE%\.claude\.credentials.json` 讀取 `claudeAiOauth.accessToken` 與 `expiresAt`；若設定 `CLAUDE_CONFIG_DIR`，則改讀該目錄的 `.credentials.json`。權杖只保留於記憶體，只送往 `https://api.anthropic.com`，不寫入記錄或提示文字，也不儲存新的權杖副本。

程式不會自行使用 refresh token。權杖過期時，會在背景隱藏執行 `claude auth status`（每 10 分鐘最多一次），讓 Claude Code CLI 自行刷新登入資料後再重新讀取；若電腦上沒有 Claude Code CLI 或刷新失敗，請開啟 Claude Code 重新登入。每次嘗試都會在 `%LOCALAPPDATA%\CodexHpBar\claude-refresh.log` 記一行（時間、結束代碼、是否刷新成功、stderr 摘要），不含權杖或帳號資訊。HTTP 401／403 會顯示登入已失效；其他更新失敗時，有成功資料就保留並標示為陳舊資料。

這是未公開的用量介面，可能隨服務更新而變動。之所以不使用 Claude Code 的 status line，是因為 Claude 桌面 app 不會執行 status line 指令。

## 操作與設定

在儀表上按滑鼠右鍵：

- **立即更新**：立刻重新讀取額度。
- **關閉監測器**：關閉所有螢幕上的儀表與背景程序。

程式關閉後，可用下列命令清除設定與 Startup 捷徑；清除後程式會結束。下次正常啟動仍會自動啟用背景待命並重建捷徑：

```powershell
.\CodexHpBar.exe --reset-settings
```

## 命令列參數

| 參數 | 說明 |
| --- | --- |
| `--self-test` | 檢查 Codex app-server、額度讀取、工具列與 DPI，成功時回傳結束碼 `0`。 |
| `--reset-settings` | 清除設定與 Startup 捷徑後結束；下次正常啟動會重新建立預設設定與捷徑。 |
| `--demo dual` | 兩個儀表都顯示每週 + 5 小時雙圓環的示範資料。 |
| `--demo single` | 兩個儀表都只顯示每週額度單一圓環的示範資料。 |
| `--demo low` | 顯示低額度狀態（青色圓點）。 |
| `--demo offline` | 顯示無法取得額度的狀態。 |

示範模式不會讀取任何額度或登入資料。

## 驗證下載檔案

下載同一版本的 `SHA256SUMS.txt`，在 PowerShell 執行：

```powershell
Get-FileHash .\CodexHpBar-v0.2.2-win-x64-portable.zip -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

兩者雜湊值必須相同。本專案目前沒有商業程式碼簽章，Windows SmartScreen 可能顯示未知發行者；請只從本儲存庫 Release 下載並核對 SHA-256。

## 隱私與安全

- Codex 額度透過本機 `codex app-server --stdio` 的 `account/rateLimits/read` 取得；程式不讀取 Codex 的 `auth.json`，app-server 使用既有 Codex 登入狀態與官方服務通訊。
- Claude Code 額度需讀取本機 `.claude\.credentials.json` 的存取權杖，只送往 `https://api.anthropic.com` 查詢用量；權杖過期時交由 Claude Code CLI（`claude auth status`）自行刷新；本程式不使用 refresh token、不另存、不記錄權杖。
- 程式不記錄 Email、不開放網路連接埠。
- 設定檔只包含背景待命與開機啟動設定。

安全問題請參閱 [安全政策](SECURITY.md)。

EXE 版本資訊包含作者 `g32jcj86` 與專案網址 `https://github.com/g32jcj86/codex-hp-bar`。本版本未使用付費 Authenticode 程式碼簽章，因此 Windows 工作管理員或 SmartScreen 的「發行者」仍可能顯示空白或未知；請以 GitHub Release 的 SHA-256 清單驗證檔案。

## 選用安裝與移除

可攜版不需要安裝。若想固定放入本機程式目錄：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

完整移除正式安裝、Startup 捷徑與設定：

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

解除安裝腳本不會刪除使用者自行下載的可攜版。

## 已知限制

- Codex 額度需要 Codex Windows 桌面應用程式安裝於 `%LOCALAPPDATA%\OpenAI\Codex\bin` 的 `codex.exe`。
- Claude Code 額度使用未公開的用量介面，Anthropic 變更格式時可能需要更新本程式。
- Windows 11 沒有提供一般程式真正嵌入既有工具列的正式 API，因此使用不修改 Explorer 的透明覆蓋視窗。
- Codex app-server 通訊協定更新時，可能需要更新本程式。
- 未提供 ARM64 或 x86 版本。

## 更多文件

- [詳細使用指南](docs/使用指南.md)
- [疑難排解](docs/疑難排解.md)
- [開發指南](docs/開發指南.md)
- [Visual Studio 本機編譯指南](docs/Visual%20Studio%20本機編譯指南.md)
- [驗證摘要](docs/驗證摘要.md)
- [版本紀錄](CHANGELOG.md)
- [貢獻指南](CONTRIBUTING.md)
- [安全政策](SECURITY.md)

## 授權

本專案使用 [MIT License](LICENSE)。
