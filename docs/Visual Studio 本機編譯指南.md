# Visual Studio 本機編譯指南

本指南說明如何從一台尚未下載原始碼的 Windows 電腦開始，使用 Visual Studio 在本機編譯、測試及產生 Codex HP Bar 免安裝 Release。所有步驟都不需要系統管理員權限；安裝 Visual Studio 或工作負載時，Windows 仍可能要求授權。

## 一、準備開發環境

請先安裝下列工具：

- Windows 11 x64。
- Visual Studio 2026（18.x）Community、Professional 或 Enterprise。
- Visual Studio Installer 中的「.NET 桌面開發」工作負載。
- .NET 10 SDK x64。
- Git for Windows；也可以使用 Visual Studio 內建的 Git 功能。
- 選用：GitHub CLI，用於自行推送分支或下載 Release，不影響本機編譯。

開啟 PowerShell，確認 .NET SDK：

```powershell
dotnet --list-sdks
```

輸出中應至少有一筆 `10.0.x`。若沒有，請回到 Visual Studio Installer，選擇目前的 Visual Studio，按「修改」，加入「.NET 桌面開發」與 .NET 10 個別元件。

## 二、取得原始碼

### 方法 A：使用 Visual Studio 複製儲存庫

1. 啟動 Visual Studio。
2. 在開始畫面選擇「複製存放庫」。
3. 儲存庫位置輸入：

   ```text
   https://github.com/g32jcj86/codex-hp-bar.git
   ```

4. 選擇本機路徑後按「複製」。
5. 若未自動開啟方案，選擇「檔案」→「開啟」→「專案或方案」，開啟 `CodexHpBar.sln`。

### 方法 B：使用 PowerShell

```powershell
git clone https://github.com/g32jcj86/codex-hp-bar.git
cd codex-hp-bar
```

接著在檔案總管雙擊 `CodexHpBar.sln`，或在 Visual Studio 中開啟它。傳統 `.sln` 可避免部分 Visual Studio 版本對 `.slnx` 的 NuGet 自動還原警告。

## 三、認識方案內容

- `src/CodexHpBar`：WPF 主程式、工具列視窗、設定與 Codex app-server 連線。
- `src/CodexHpBar.Core`：額度模型、JSON 解析及可測試的定位計算。
- `tests/CodexHpBar.Tests`：單元與資料整合測試。
- `scripts/validate.ps1`：格式、Release 建置、測試、覆蓋率、弱點與文件檢查。
- `scripts/build-release.ps1`：建立 self-contained single-file Windows x64 成品。

## 四、還原與第一次建置

Visual Studio 通常會自動還原 NuGet 套件。若方案上方顯示黃色提示，按「還原」。也可以在「工具」→「NuGet 套件管理員」→「套件管理員主控台」執行：

```powershell
dotnet restore .\CodexHpBar.sln
```

部分 Visual Studio 18.7 環境即使套件已還原，輸出視窗仍可能短暫出現「NuGet 套件還原失敗」訊息。發布前實測在先執行上述 `dotnet restore` 後，Visual Studio 仍可成功重建 3 個專案且失敗數為 0；請以最後的重建摘要與「錯誤清單」為準。

在 Visual Studio 工具列選擇：

1. 組態：`Release`。
2. 平台：`x64`；若只看到 `Any CPU`，仍可先建置，正式發布腳本會固定使用 `win-x64`。
3. 選擇「建置」→「重建方案」。

建置必須顯示零警告、零錯誤。本專案把編譯警告視為錯誤。

## 五、執行測試

使用 Visual Studio：

1. 選擇「測試」→「測試總管」。
2. 按「執行所有測試」。
3. 確認所有測試均通過。

使用 PowerShell執行完整發布閘門：

```powershell
pwsh -NoProfile -File .\scripts\validate.ps1
```

此命令會檢查格式、Release 建置、測試、核心行覆蓋率至少 85%、NuGet 已知弱點、Markdown 連結與繁體中文用詞。

## 六、在 Visual Studio 執行程式

在方案總管以滑鼠右鍵按 `CodexHpBar` 專案，選擇「設定為啟始專案」，再按 `Ctrl+F5`。第一次正式啟動會顯示背景待命與開機啟動選項。

若要測試固定畫面，可在專案屬性的「偵錯」啟動引數填入其中一項：

```text
--demo single
--demo dual
--demo offline
--demo low
```

移除 `--demo` 後才會連線至真正的 Codex app-server，並依帳號目前政策自動顯示單血條或雙血條。

## 七、建立免安裝 Release

請先關閉正在執行的 Codex HP Bar，避免 Windows 鎖定舊 EXE。從儲存庫根目錄執行：

```powershell
pwsh -NoProfile -File .\scripts\build-release.ps1 -Version 0.2.1
```

腳本固定使用以下設定：

- `Release`。
- `win-x64`。
- self-contained，自帶 .NET Runtime。
- single-file。
- `IncludeNativeLibrariesForSelfExtract=true`。
- `PublishTrimmed=false`。

成品會出現在 `artifacts`：

- `CodexHpBar-v0.2.1-win-x64.exe`。
- `CodexHpBar-v0.2.1-win-x64-portable.zip`。
- `SHA256SUMS.txt`。

## 八、驗證本機 Release

先檢查單檔程式能否讀取本機 Codex app-server：

```powershell
.\artifacts\CodexHpBar-v0.2.1-win-x64.exe --self-test
$LASTEXITCODE
```

結束碼 `0` 表示通過。接著測試畫面：

```powershell
.\artifacts\CodexHpBar-v0.2.1-win-x64.exe --demo dual
```

關閉 demo 後，以不帶參數的正式模式啟動：

```powershell
.\artifacts\CodexHpBar-v0.2.1-win-x64.exe
```

## 九、開機啟動行為

「登入 Windows 時自動啟動」預設未勾選，因此只完成編譯或第一次執行並不會自動開機啟動。

只有在設定視窗勾選「登入 Windows 時自動啟動」並按「套用並啟用」後，程式才會在目前使用者的 Startup 資料夾建立 `Codex HP Bar.lnk`。取消該選項並套用後，捷徑會被刪除。

可以在 PowerShell 檢查目前狀態：

```powershell
$shortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'Codex HP Bar.lnk'
Test-Path -LiteralPath $shortcut
```

回傳 `True` 表示已建立開機啟動捷徑；`False` 表示不會隨 Windows 登入自動啟動。開機啟動依賴背景待命，因此勾選開機啟動時，程式會自動一併勾選背景待命。

## 十、常見建置問題

- 找不到 .NET 10：使用 Visual Studio Installer 補裝 .NET 10 SDK 與「.NET 桌面開發」。
- `CodexHpBar.sln` 無法開啟：更新 Visual Studio 2026，或改用 `dotnet build CodexHpBar.sln -c Release`。
- EXE 無法覆寫：先在血條右鍵選擇「關閉監測器」，再重新執行發布腳本。
- `--self-test` 回傳 `1`：確認 Codex 桌面程式已安裝、已登入，而且本機 Codex 目錄中存在 `codex.exe`。
- SmartScreen 顯示提示：本專案未簽章，請先依 `SHA256SUMS.txt` 驗證雜湊，再選擇「其他資訊」→「仍要執行」。
