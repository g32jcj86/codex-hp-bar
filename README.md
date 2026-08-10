# Codex 菇菇寶貝 HP 額度監測器

Codex HP Bar 是 Windows 11 工具列上的免安裝額度監測器。它把 Codex 剩餘額度顯示成遊戲 HP 血條，並由菇菇寶貝陪伴工作。

![工具列示意](docs/media/taskbar-demo.png)

## 功能特色

- 透過 Codex 官方本機 app-server 讀取額度，不使用畫面辨識，也不讀取登入權杖。
- 同時支援短期與每週額度；只有一種限制時會自動切換成單一加粗血條。
- 預設使用菇菇寶貝，也可隨時切換本機靜態圖片、GIF 動圖或 4×4 連續動畫圖片。
- 自動顯示於所有 Windows 工具列，支援多螢幕與每螢幕 DPI。
- Codex 桌面程式關閉時自動隱藏。
- 可攜版自帶 .NET Runtime，解壓後直接執行，不需要安裝。
- 首次啟動才詢問背景待命與 Windows 登入自動啟動，不會預設加入 Startup。

## 系統需求

- Windows 11 x64。
- 已安裝並登入 Codex 桌面應用程式。
- 若從原始碼建置，需要 .NET 10 SDK。

## 三步驟快速開始

1. 從 [Releases](https://github.com/g32jcj86/codex-hp-bar/releases) 下載 `CodexHpBar-v0.2.1-win-x64-portable.zip`。
2. 解壓縮後執行 `CodexHpBar.exe`。
3. 在首次啟用視窗選擇是否允許背景待命及 Windows 登入自動啟動，再按「套用並啟用」。

免安裝版本可以放在任何可寫入的位置。若已啟用開機啟動，移動 EXE 後請再次開啟設定，讓程式修復 Startup 捷徑。

## 第一次啟動會做什麼

兩個選項預設都不勾選：

- **允許背景待命**：Codex 關閉時血條隱藏，但程式保留在背景，等待下次開啟 Codex。
- **登入 Windows 時自動啟動**：建立目前使用者的 Startup 捷徑。此選項會同時啟用背景待命。

只有按下「套用並啟用」後才會儲存設定。設定檔位於 `%LOCALAPPDATA%\CodexHpBar\settings.json`，其中不包含登入資料。

## 如何閱讀血條

- 單一血條：Codex 目前只回傳一種限制，例如每週額度。
- 雙層血條：上方是較短時間視窗，下方是較長時間視窗。
- 珊瑚紅：短期額度；莓紫色：每週額度。
- 黃色圓點：資料暫時無法更新，仍顯示五分鐘內最後一次成功資料。
- `--%`：目前無法取得額度。將滑鼠停在血條上可查看原因。

百分比代表剩餘量，計算方式為 `100 - usedPercent`。提示文字會列出本機時間的重置時刻與最後更新時間。

## 操作與設定

在血條上按滑鼠右鍵：

- **立即更新**：立刻重新讀取額度。
- **設定**：調整背景待命與開機啟動。
- **關閉監測器**：關閉所有螢幕上的血條與背景程序。

### 更換角色圖片

在設定視窗的「工具列角色圖片」選擇模式，套用後所有螢幕會立即更新：

- **內建菇菇寶貝**：使用專案內建的透明 PNG。
- **靜態圖片**：接受 PNG、JPG、BMP、ICO，固定顯示單張圖片。
- **GIF 動圖**：接受 GIF，依 GIF 的影格延遲循環播放；只有一張影格時視為靜態圖。
- **4×4 連續動畫圖片**：接受 PNG、JPG、BMP，必須無間距等分成 4 欄×4 列，共 16 格。

4×4 圖片採遊戲常見的 sprite sheet 規則：影格尺寸必須完全相同，從左至右、由上至下編號 0–15，播完後回到第 0 格循環；播放速度可設定 1–30 FPS，預設 8 FPS。4×4 模式不會再疊加程式額外的上下浮動，垂直移動完全由影格內容控制。透明背景 PNG 最適合工具列顯示，圖片檔上限為 20 MB、最長邊不可超過 4096 像素。

角色圖片設定會儲存在 `%LOCALAPPDATA%\CodexHpBar\settings.json`。選擇外部圖片後，程式會先複製到執行檔旁的 `MascotAssets` 資料夾，設定檔只記錄複製後的路徑；原始檔案不會被刪除，也不會上傳或執行圖片內容。

若要重新顯示首次啟用畫面：

```powershell
.\CodexHpBar.exe --reset-settings
```

## 命令列參數

| 參數 | 說明 |
| --- | --- |
| `--self-test` | 檢查 Codex app-server、額度讀取、工具列與 DPI，成功時回傳結束碼 `0`。 |
| `--reset-settings` | 清除設定與 Startup 捷徑，重新進入首次啟用流程。 |
| `--demo single` | 顯示單一血條示範資料。 |
| `--demo dual` | 顯示雙層血條示範資料。 |
| `--demo low` | 顯示低額度狀態。 |
| `--demo offline` | 顯示離線狀態。 |

## 驗證下載檔案

下載同一版本的 `SHA256SUMS.txt`，在 PowerShell 執行：

```powershell
Get-FileHash .\CodexHpBar-v0.2.1-win-x64-portable.zip -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

兩者雜湊值必須相同。本專案目前沒有商業程式碼簽章，Windows SmartScreen 可能顯示未知發行者；請只從本儲存庫 Release 下載並核對 SHA-256。

## 隱私與安全

- 程式只啟動本機 `codex app-server --stdio` 並呼叫 `account/rateLimits/read`。
- 程式不讀取 `auth.json`、不記錄 Email、不開放網路連接埠。
- app-server 使用既有 Codex 登入狀態與官方服務通訊。
- 設定檔只包含背景待命與開機啟動兩個布林值。

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

- 僅偵測 Codex Windows 桌面應用程式，不會因 CLI 或 IDE 擴充套件而顯示。
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

本專案使用 [MIT License](LICENSE)。菇菇寶貝素材由使用者提供，僅由本機程式載入顯示。
