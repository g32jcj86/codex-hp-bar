# 額度改為同心圓環顯示

## 目標
工作列小工具（`QuotaWidget`，150×38 DIP）改成：左側同心圓環、圓心放吉祥物，右側顯示可清楚區分的百分比文字。

## 規格
- **只改 `src/CodexHpBar/QuotaWidget.cs` 的 `OnRender` 與繪製輔助方法**，不動資料來源、Models、設定。
- 圓環區：左側 34×34 DIP 方塊（x=2, y=2，垂直置中於 38 高）。
  - 週限制 = `OrderedWindows` 中時長最長的那個，畫在**外圈**。
  - 5 小時限制 = 有兩個 window 時時長較短的那個，畫在**內圈**。
  - 只有一個 window 時只畫一個圈（週限制），圈可稍粗。
  - 沒有資料時畫一個灰色空圈，文字顯示 `--%`。
  - 每個圈：先畫深色底軌（沿用 `Track`），再從 12 點鐘方向順時針畫剩餘百分比的弧（`StreamGeometry` + `ArcTo`，`PenLineCap.Round`）。100% 畫完整圓、0% 不畫弧。
  - 兩圈時建議：外圈線寬 3、內圈線寬 3、兩圈間距 1；一圈時線寬 4。依此算出圓心剩餘直徑。
- 配色（兩個 AI 應用的經典 logo 色）：
  - 週限制（外圈）：OpenAI / ChatGPT 綠 `#10A37F`
  - 5 小時（內圈）：Claude 橘 `#D97757`
  - 取消原本「≤20% 改成 OrangeRed」的變色（會跟 Claude 橘撞色）；低量警示保留原本的青色小點，位置移到圓環右上角。
- 圓心：吉祥物（`_mascotAnimation.CurrentImage`，沒有就用 `DrawFallbackMushroom` 縮小版）用 `DrawImageUniform` 塞進圓心，並以圓形 `PushClip(EllipseGeometry)` 裁切，不可壓到圓環。保留原本 bob / frame offset 行為但幅度要縮到不會超出圓心。
- 百分比文字（右側 x≈40 到右緣）：
  - 每個 window 一行；兩行時上行 = 週、下行 = 5h，一行時垂直置中。
  - 用 `FormattedText`（`Segoe UI`，SemiBold，約 11 DIP），格式：`週 45%`、`5h 72%`，字色 = 對應圓環顏色。
  - 文字後方墊一個深色圓角底（`Track`，半透明可），讓淺色/深色工作列都看得清楚。
  - 注意 `OnRender` 已用 `ScaleTransform(1/scale)` 轉成裝置像素，`FormattedText` 的字級與 `PixelsPerDip` 要跟著換算，避免高 DPI 下字變小或糊。
- 舊的 `DrawBar`、像素字型 `Glyphs`/`DrawPixelText` 若不再使用就刪掉。
- `IsStale` 的金色小點保留在右上角。

## 驗收
- `dotnet build CodexHpBar.slnx -c Release` 通過、`dotnet test tests\CodexHpBar.Tests\CodexHpBar.Tests.csproj -c Release` 通過、`dotnet format CodexHpBar.slnx --verify-no-changes` 通過。
- 不要 commit、不要 push、不要啟動或關閉應用程式（CC 會處理）。
- 完成摘要寫到 `.agents/result.md`：改了哪些檔、build/test 結果、任何偏離本計畫的地方與疑問。
