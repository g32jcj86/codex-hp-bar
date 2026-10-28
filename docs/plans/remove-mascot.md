# 移除菇菇寶貝角色圖片

使用者回饋：工作列只要 Claude Code 與 Codex 兩個儀表，不要角色圖片。

## 變更
1. `QuotaWidget`：不再畫角色圖片與後備像素菇菇；兩個儀表左移，圓心分別在 x=19、x=59 DIP（各 36×36、間距 4）。移除動畫計時器與 `SetMascot` 等只為角色存在的成員。
2. 視窗寬度 118 → 80 DIP：`MainWindow.xaml` Width 與 `TaskbarGeometry.Calculate`，同步更新 `QuotaTests` 期望值。
3. 刪除只為角色存在的程式碼：`MascotAnimation.cs`、`MascotAssetStorage.cs`、`MascotSettings`/`MascotAssetMode`、`AppSettings.Mascot`、`MainWindow` / `AppController` 中傳遞角色設定的部分，以及相關測試。舊 `settings.json` 若含 `Mascot` 欄位要能照常讀取（忽略即可）。
4. 素材：刪除只用於角色顯示的圖片（如 `Assets/mushroom.png`、`.gitignore` 中的 cat-pig 圖），但若某檔案被當作 EXE／視窗圖示（`ApplicationIcon`、`Icon=`）就保留。同步清理 `.csproj` 中對已刪檔案的引用。
5. 文件：`README.md`、`docs/` 下各指南、`CHANGELOG.md`（「未發布」補一條）移除角色圖片、GIF、4×4 動畫等說明；README 標題與開頭改成「Claude Code + Codex 額度監測器」的描述，「如何閱讀額度圓環」改成「由左到右依序是 Claude Code、Codex 儀表」。遵守 `scripts/validate-docs.ps1` 規則（相對連結存在、繁體用詞）。若 `docs/media/taskbar-demo.png` 已不符，先保留連結，不要刪圖。

## 不要做
- 不改額度讀取、Claude statusline、啟動流程。
- 不 commit、不 push、不啟動或關閉應用程式。

## 驗收
- `dotnet build` / `dotnet test` 通過（沙箱限制時用 `--no-restore`）。
- 最後回覆就是完成摘要。
