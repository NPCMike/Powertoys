# 本機單獨建置與啟動 PowerToys Run 紀錄

這份文件記錄本機從 clone PowerToys repository，到補齊建置依賴、單獨建置 PowerToys Run、排除啟動錯誤，最後用 `Alt + Space` 成功叫出 PowerToys Run 的完整過程。

環境：

- Repository: `C:\Users\NPCMike\Desktop\MF\5_tools\home\PowerToys`
- Shell: PowerShell
- Visual Studio: Visual Studio Community 2026
- 目標模組: PowerToys Run，也就是 repo 內的 `src\modules\launcher\PowerLauncher`
- 輸出執行檔: `x64\Debug\PowerToys.PowerLauncher.exe`

## 1. Clone repository

一開始在 `C:\Users\NPCMike\Desktop\MF\5_tools\home` 執行：

```powershell
git clone https://github.com/microsoft/PowerToys.git
```

clone 完後，repository 實際位置是：

```powershell
C:\Users\NPCMike\Desktop\MF\5_tools\home\PowerToys
```

第一次嘗試直接執行：

```powershell
.\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path .\src\modules\launcher
```

失敗原因是當時還停在 `home` 目錄，不是在 `PowerToys` repo 根目錄，所以 `.\tools\build\build.ps1` 找不到。

修正方式：

```powershell
cd .\PowerToys
```

## 2. 補齊 Git submodules

PowerToys 有一些依賴不是直接放在主 repo 裡，而是透過 Git submodule 掛進來。clone 主 repo 後，需要初始化 submodules：

```powershell
git submodule update --init --recursive
```

這次成功補上的 submodules 包含：

```text
deps/expected-lite
deps/spdlog
```

這一步只需要在第一次 clone 後執行一次。之後如果切 branch 或 pull 新版，再視情況重跑。

## 3. 還原 NuGet 與建置基本專案

PowerToys 官方建議第一次建置或缺 NuGet package 時執行：

```powershell
tools\build\build-essentials.cmd
```

第一次執行時，NuGet restore 成功，但 native build 失敗：

```text
error MSB8040: 此專案需要 Spectre 風險降低的程式庫
```

這代表 Visual Studio 缺少 MSVC Spectre-mitigated libraries，不是 PowerToys 原始碼缺檔。

## 4. 補 Visual Studio C++ Spectre 元件

檢查 Visual Studio 後，本機有：

```text
Visual Studio Community 2026
C:\Program Files\Microsoft Visual Studio\18\Community
```

PowerToys 的 `Cpp.Build.props` 中有：

```xml
<SpectreMitigation>Spectre</SpectreMitigation>
```

所以 C++ Spectre libraries 是必要依賴。

一開始只裝到 `14.44.35207` 的 Spectre：

```text
14.44.35207  HasLibSpectre=True   HasAtlmfcSpectre=True
14.50.35717  HasLibSpectre=False  HasAtlmfcSpectre=False
```

但 VS 2026 build 實際使用的是 `14.50.35717`，所以仍然失敗。

最後在 Visual Studio Installer 的「個別元件」中搜尋 `v14.50 C++ x64/x86 Spectre`，安裝這三項：

```text
適用於 x64/x86 (MSVC v14.50) 的 C++ Spectre 風險緩和程式庫
適用於 x64/x86 且具有 Spectre 緩解功能的 C++ ATL (MSVC v14.50)
適用於 x64/x86 且具有 Spectre 風險緩和功能的 C++ MFC (Microsoft Visual C++ v14.50)
```

重新檢查後變成：

```text
14.44.35207  HasLibSpectre=True  HasAtlmfcSpectre=True
14.50.35717  HasLibSpectre=True  HasAtlmfcSpectre=True
```

這表示 Spectre 依賴已補齊。

## 5. 處理繁中系統 code page 導致的 C4819

Spectre 問題解掉後，`build-essentials.cmd` 繼續往前跑，但在繁中 Windows code page 950 下遇到：

```text
warning C4819: 檔案含有無法在目前字碼頁 (950) 中表示的字元
error C2220: 以下警告視為錯誤處理
```

因為 PowerToys C++ 專案把 warning 當 error，所以 C4819 會中斷 build。

解法是在本次 PowerShell session 設定 MSVC 編譯參數：

```powershell
$env:CL='/utf-8'
```

這會讓 MSVC 把原始檔用 UTF-8 處理。

設定 `/utf-8` 後，曾遇到 precompiled header 參數不一致：

```text
error C2855: 命令列選項 '/source-charset' 和先行編譯標頭檔不一致
error C2855: 命令列選項 '/execution-charset' 和先行編譯標頭檔不一致
```

原因是前一次沒有 `/utf-8` 時已產生舊的 `.pch`。處理方式是清掉 repo 內可再生的 `.pch` 中間檔：

```powershell
Get-ChildItem -Path . -Recurse -Filter *.pch |
  Where-Object { $_.FullName -like "$((Get-Location).ProviderPath)*" } |
  Remove-Item -Force
```

之後再跑：

```powershell
$env:CL='/utf-8'
tools\build\build-essentials.cmd
```

結果：

- NuGet restore 成功
- `src\runner\runner.vcxproj` 成功
- `src\settings-ui\Settings.UI\PowerToys.Settings.csproj` 卡在 `Settings.UI.XamlIndexBuilder`

Settings UI 的錯誤不影響單獨執行 PowerToys Run，所以接著直接建 PowerLauncher。

## 6. 建置 PowerToys Run 主程式

PowerToys Run 主程式專案位置：

```powershell
src\modules\launcher\PowerLauncher\PowerLauncher.csproj
```

建置命令：

```powershell
$env:CL='/utf-8'
.\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path .\src\modules\launcher\PowerLauncher
```

成功後輸出檔：

```text
x64\Debug\PowerToys.PowerLauncher.exe
x64\Debug\PowerToys.PowerLauncher.dll
x64\Debug\PowerToys.PowerLauncher.deps.json
x64\Debug\PowerToys.PowerLauncher.runtimeconfig.json
```

## 7. 第一次啟動失敗：缺 RunPlugins

第一次啟動：

```powershell
Start-Process -FilePath .\x64\Debug\PowerToys.PowerLauncher.exe
```

程式很快退出。前景執行後看到錯誤：

```text
System.IO.DirectoryNotFoundException:
Could not find a part of the path
'C:\Users\NPCMike\Desktop\MF\5_tools\home\PowerToys\x64\Debug\RunPlugins'
```

原因是只建了 `PowerLauncher.csproj`，還沒建 PowerToys Run 的 plugins。PowerToys Run 啟動時會掃描：

```text
x64\Debug\RunPlugins
```

但該目錄尚未產生。

## 8. 建置 PowerToys Run plugins

PowerToys Run plugins 都在：

```powershell
src\modules\launcher\Plugins
```

官方 `build.ps1 -Path` 只掃指定資料夾第一層，不會遞迴，所以直接指定 `Plugins` 目錄會顯示：

```text
[BUILD] No local projects found
```

因此改成逐一建置所有非測試 plugin project：

```powershell
$env:CL='/utf-8'
$projects = Get-ChildItem .\src\modules\launcher\Plugins -Recurse -Filter *.csproj |
  Where-Object { $_.FullName -notmatch 'UnitTests?|\.UnitTest|Wox\.Test' }

foreach ($p in $projects) {
  Write-Host "BUILD_PLUGIN=$($p.FullName)"
  & .\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path $p.DirectoryName
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

建完後確認 `RunPlugins` 目錄存在，包含：

```text
Calculator
Folder
History
Indexer
OneNote
PowerToys
Program
Registry
Service
Shell
System
TimeDate
UnitConverter
Uri
ValueGenerator
VSCodeWorkspaces
WebSearch
WindowsSettings
WindowsTerminal
WindowWalker
```

## 9. 第二次啟動：PowerToys plugin 初始化錯誤

補齊 `RunPlugins` 後，PowerToys Run 本體可以啟動，但跳出：

```text
PowerToys Run 0.0.1.0 - Plugin Initialization Error
Fail to initialize plugins: PowerToys (0.0.1.0)
```

log 顯示：

```text
Fail to Init plugin: PowerToys
System.IO.FileNotFoundException: Unable to find the specified file.
at Microsoft.PowerToys.Settings.UI.Library.SettingsUtils.GetSettings[T]
at Microsoft.PowerToys.Run.Plugin.PowerToys.UtilityProvider..ctor()
```

原因是 `PowerToys` 這個 plugin 需要完整 PowerToys runner/settings 環境與設定檔；目前是單獨跑 PowerToys Run，所以它會找不到完整 PowerToys 設定。

其他 plugins 都已正常載入，log 也顯示：

```text
Registered global hotkey
```

所以只需要停用 `PowerToys` 這個 plugin。

## 10. 停用 PowerToys plugin

PowerToys Run module settings 檔案位置：

```powershell
$env:LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\settings.json
```

`PowerToys` plugin metadata：

```json
{
  "ID": "29DD65DB28C84A37BDEF1D2B43DA368B",
  "Name": "PowerToys",
  "ActionKeyword": "@"
}
```

停用方式：

```powershell
Get-Process PowerToys.PowerLauncher -ErrorAction SilentlyContinue | Stop-Process -Force

$path = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\PowerToys Run\settings.json'
$json = Get-Content $path -Raw | ConvertFrom-Json

foreach ($plugin in $json.plugins) {
  if ($plugin.Id -eq '29DD65DB28C84A37BDEF1D2B43DA368B' -or $plugin.Name -eq 'PowerToys') {
    $plugin.Disabled = $true
  }
}

$json | ConvertTo-Json -Depth 100 | Set-Content -Path $path -Encoding UTF8

Start-Process -FilePath .\x64\Debug\PowerToys.PowerLauncher.exe
```

確認結果：

```text
PowerToys plugin Disabled=True
PowerToys.PowerLauncher.exe 正在執行
Registered global hotkey
```

## 11. 熱鍵衝突：ChatGPT 也使用 Alt + Space

PowerToys Run 設定中 hotkey 是：

```json
"Hotkey": "Alt + Space"
```

module settings 中也對應：

```json
"open_powerlauncher": {
  "win": false,
  "ctrl": false,
  "alt": true,
  "shift": false,
  "code": 32,
  "key": ""
}
```

但 ChatGPT desktop app 也會使用 `Alt + Space`，所以如果 ChatGPT 正在背景常駐，按 `Alt + Space` 會叫出 ChatGPT，而不是 PowerToys Run。

這次決定保留 PowerToys Run 的 `Alt + Space`，並由使用者關閉 ChatGPT。關閉 ChatGPT 後，PowerToys Run 成功註冊 global hotkey。

## 12. 最終可用流程

之後要重新建置並啟動 PowerToys Run，可使用：

```powershell
cd C:\Users\NPCMike\Desktop\MF\5_tools\home\PowerToys

$env:CL='/utf-8'

.\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path .\src\modules\launcher\PowerLauncher

$projects = Get-ChildItem .\src\modules\launcher\Plugins -Recurse -Filter *.csproj |
  Where-Object { $_.FullName -notmatch 'UnitTests?|\.UnitTest|Wox\.Test' }

foreach ($p in $projects) {
  & .\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path $p.DirectoryName
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Start-Process -FilePath .\x64\Debug\PowerToys.PowerLauncher.exe
```

如果只是啟動已建好的版本：

```powershell
cd C:\Users\NPCMike\Desktop\MF\5_tools\home\PowerToys
Start-Process -FilePath .\x64\Debug\PowerToys.PowerLauncher.exe
```

然後按：

```text
Alt + Space
```

## 13. 最終狀態摘要

已完成：

- `git submodule update --init --recursive`
- Visual Studio C++ `v14.50` Spectre / ATL Spectre / MFC Spectre 安裝
- 用 `$env:CL='/utf-8'` 解掉繁中 code page C4819
- 清掉舊 `.pch` 中間檔，解掉 C2855
- `runner.vcxproj` build 成功
- `PowerLauncher.csproj` build 成功
- 所有非測試 PowerToys Run plugins build 成功
- `x64\Debug\RunPlugins` 補齊
- 停用單獨執行時會失敗的 `PowerToys` plugin
- `PowerToys.PowerLauncher.exe` 成功常駐
- `Alt + Space` global hotkey 註冊成功

仍需注意：

- 如果要跑完整 PowerToys，而不是單獨 PowerToys Run，仍需解決 `Settings.UI.XamlIndexBuilder` 的 build 問題。
- 單獨跑 PowerToys Run 時，`PowerToys` plugin 建議保持停用，否則會因找不到完整 PowerToys settings 而跳初始化錯誤。
- 每次在繁中 Windows 環境建 C++ 專案前，建議先設定：

```powershell
$env:CL='/utf-8'
```
