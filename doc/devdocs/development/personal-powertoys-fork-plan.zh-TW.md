# 個人化 PowerToys Fork 架構改造計畫

這份文件記錄將 PowerToys 改造成個人 productivity toolbox 平台的具體規劃。目標不是一次把 PowerToys 改成完整外掛系統，而是先保留現有 runner、settings UI、hotkey、IPC 與 build 流程，再逐步收斂模組註冊與顯示邏輯，讓後續可以穩定新增、替換、停用或移除模組。

## 1. 目標範圍

目前預期保留或新增的核心功能如下：

| 功能 | 來源或靈感 | 初期策略 | 中長期策略 |
|------|------------|----------|------------|
| PowerToys Run | PowerToys 既有 `launcher` | 先保留既有模組 | 評估是否保留、改造，或改接新版 Command Palette |
| AltSnap 類視窗操作 | https://github.com/RamonUnch/AltSnap | 新增獨立 module interface，外部 service 承載主邏輯 | 整合個人化設定、熱鍵、DPI 與多螢幕行為 |
| Typeless 類語音輸入 | Whisper / whisper.cpp | 新增 `VoiceInput` module，先做最小可用聽寫 | 支援模型管理、VAD、語言選擇、快速貼上 |
| OCR | PowerOCR / 自寫替代 | 先保留 PowerOCR | 新增 `FastOCR` 後再替換 |
| Color Picker | PowerToys 既有 `colorPicker` / 自寫替代 | 先保留既有模組 | 新增 `FastColorPicker`，優先改善啟動速度 |
| Snipaste 類截圖標註 | Snipaste | 後期新增獨立截圖與標註 app/service | 支援截圖、馬賽克、畫筆、釘選、剪貼簿與存檔 |
| PID Shower | 自訂工具 | 作為第一個自製模組 | 用來驗證 registry、hotkey、settings、overlay 架構 |

## 2. 架構原則

1. 保留現有 runner 載入流程，不在第一階段改成完全動態外掛系統。
2. 優先建立集中式 module registry/manifest，把低風險 metadata 收斂到同一處。
3. 先支援「停用/不顯示/不載入」模組，再考慮真正刪除原始碼。
4. 對 GPO、installer、hotkey conflict、IPC schema、shell extension 這些敏感區保持顯式註冊。
5. 新的大型功能以獨立 service/app 承載，module interface 只負責 enable、disable、設定與啟動橋接。
6. 每次只遷移一類耦合點，避免一次牽動 runner、settings、installer、GPO、測試而失控。
7. 所有架構改造都要能通過 build，且能明確回復到上一個穩定階段。

## 3. 目前耦合點

PowerToys 的模組架構已經有清楚的 interface，但不是隨插即用外掛。模組名稱與 metadata 會散落在多個地方。

主要耦合點：

| 區域 | 常見檔案 | 說明 |
|------|----------|------|
| Runner 模組載入 | `src/runner/main.cpp`、`src/runner/modules.h`、`src/runner/modules.cpp` | 決定要載入哪些 module interface DLL |
| Settings window mapping | `src/runner/settings_window.h`、`src/runner/settings_window.cpp` | 把模組名稱映射到 settings 頁面 |
| Settings UI | `src/settings-ui/Settings.UI/`、`src/settings-ui/Settings.UI.Library/` | 模組設定模型、ViewModel、XAML 頁面 |
| Dashboard | `src/settings-ui/Settings.UI/ViewModels/DashboardViewModel.cs` | 首頁模組卡片、快捷動作與顯示資訊 |
| GPO | `src/common/utils/gpo.*`、`src/settings-ui/QuickAccess.UI/Helpers/ModuleGpoHelper.cs` | 群組原則啟停設定 |
| Hotkey | runner hotkey manager、各 module interface | 快捷鍵註冊、衝突檢查、啟停事件 |
| Installer / package | `installer/`、MSIX/WiX 相關檔案 | 決定安裝、註冊、shell extension 與部署內容 |
| Resource / localization | `.resw`、`.rc`、settings assets | 顯示名稱、icon、設定頁文字 |
| Tests | `src/settings-ui/*Tests`、各 module tests | 設定相容性、ViewModel、模組行為 |

## 4. 建議目標架構

先建立一層輕量 registry。第一版只管理低風險 metadata，不接管所有邏輯。

建議新增：

```text
src/common/modules/
  ModuleId.h
  ModuleId.cs
  ModuleRegistry.json
  EnabledModules.personal.json
```

第一版 `ModuleRegistry.json` 可以長這樣：

```json
[
  {
    "id": "Launcher",
    "key": "PowerToys Run",
    "dll": "PowerToys.PowerLauncherModuleInterface.dll",
    "settingsPage": "PowerLauncher",
    "displayNameResource": "PowerToysRun",
    "icon": "Assets/Settings/Icons/PowerToysRun.png",
    "category": "Launcher",
    "defaultEnabled": true,
    "isPersonalModule": false
  },
  {
    "id": "PidShower",
    "key": "PidShower",
    "dll": "PowerToys.PidShowerModuleInterface.dll",
    "settingsPage": "PidShower",
    "displayNameResource": "PidShower",
    "icon": "Assets/Settings/Icons/PidShower.png",
    "category": "System",
    "defaultEnabled": false,
    "isPersonalModule": true
  }
]
```

第一版 `EnabledModules.personal.json` 可以長這樣：

```json
[
  "Launcher",
  "PowerOCR",
  "ColorPicker",
  "PidShower",
  "AltSnap",
  "VoiceInput",
  "SnippingStudio"
]
```

注意：這份清單初期只控制「個人版要顯示或載入哪些模組」，不負責 GPO、installer、hotkey schema 或 migration。

## 5. 分階段路線圖

### Phase 0：建立穩定基準

目的：確保原始 PowerToys 可以建置，並留下可回復的 baseline。

工作項目：

1. 確認 submodules 完整：

```powershell
git submodule update --init --recursive
```

2. 第一次建置或 NuGet 缺失時執行：

```powershell
tools\build\build-essentials.cmd
```

3. 建置目前修改區域：

```powershell
tools\build\build.ps1 -Platform x64 -Configuration Debug
```

4. 紀錄目前可執行狀態與 build log。
5. 建立自己的長期分支，例如：

```powershell
git switch -c personal/main
```

完成條件：

- repo 可 build。
- `git status` 中沒有不明來源的大量變更。
- 已知道目前 runner 與 PowerToys Run 如何啟動。

### Phase 1：品牌與專案邊界

目的：把 fork 明確變成個人專案，但不先動模組架構。

工作項目：

1. 決定專案名稱，例如 `MikeToys`、`NpcToys` 或其他名稱。
2. 更新 README 中的專案定位。
3. 更新 about 頁面、display name、icon、版本字串。
4. 暫時保留原本 PowerToys 模組與載入方式。
5. 不急著動 installer，先以本機 debug/release build 運行。

完成條件：

- 程式仍可啟動。
- 使用者可看出這是自己的 fork。
- 原功能沒有因品牌調整壞掉。

### Phase 2：Module Registry v1

目的：建立集中式 metadata，不改核心生命週期。

工作項目：

1. 新增 `src/common/modules/ModuleRegistry.json`。
2. 先收錄少量模組，例如：
   - `Launcher`
   - `PowerOCR`
   - `ColorPicker`
   - `PidShower` placeholder
3. 新增讀取 registry 的共用 helper。
4. 把 runner 中 DLL 名稱清單的一部分改由 registry 供應。
5. 保留原有 hard-coded fallback，避免 registry 讀取失敗時 runner 完全不能啟動。

初期可以遷移：

- module id
- DLL filename
- settings page key
- icon path
- category
- default visible/enabled metadata

暫時不要遷移：

- GPO policy mapping
- hotkey conflict policy
- IPC message schema
- installer packaging
- shell extension registration
- settings migration

完成條件：

- 至少一個既有模組的 DLL 載入資訊可從 registry 取得。
- registry 壞掉時有清楚 log 或 fallback。
- build 成功。

### Phase 3：Enabled Modules 個人清單

目的：先做到不載入、不顯示不需要的模組，而不是直接刪除程式碼。

工作項目：

1. 新增 `src/common/modules/EnabledModules.personal.json`。
2. runner 只載入清單內模組。
3. settings dashboard 只顯示清單內模組。
4. settings navigation 不顯示清單外模組。
5. 原始模組資料夾先保留，不做刪除。

建議初始清單：

```json
[
  "Launcher",
  "PowerOCR",
  "ColorPicker"
]
```

等自製模組完成後再加入：

```json
[
  "Launcher",
  "PowerOCR",
  "ColorPicker",
  "PidShower",
  "AltSnap",
  "VoiceInput",
  "SnippingStudio"
]
```

完成條件：

- 清單外模組不被 runner 載入。
- 清單外模組不出現在 settings UI。
- 清單內既有模組仍可正常啟停。

### Phase 4：PID Shower 作為第一個自製模組

目的：用最小但完整的自製模組驗證整套擴充流程。

建議功能：

1. 熱鍵開關 overlay。
2. 顯示目前滑鼠所在視窗的 process name、PID、window title。
3. 設定頁提供：
   - 啟用/停用
   - hotkey
   - 顯示欄位
   - overlay 透明度
   - 字體大小
4. 不處理 installer 特殊註冊。
5. 不依賴 shell extension。

建議目錄：

```text
src/modules/PidShower/
  PidShowerModuleInterface/
  PidShowerService/
  PidShowerShared/
```

ModuleInterface 職責：

- 實作 `PowertoyModuleIface`
- 提供 `get_key`
- 提供 `get_config` / `set_config`
- 提供 `enable` / `disable`
- 註冊 hotkey
- 啟動或停止 `PidShowerService`

Service 職責：

- 取得游標位置
- 找出 window handle
- 查 process id
- 顯示 overlay
- 監看設定變化

完成條件：

- 可以從 settings 啟停。
- 可以用 hotkey 顯示/隱藏。
- 加入 `ModuleRegistry.json` 與 `EnabledModules.personal.json`。
- build 成功。

### Phase 5：AltSnap module

目的：整合 AltSnap 類視窗操作，但隔離風險。

建議方式：

```text
src/modules/AltSnap/
  AltSnapModuleInterface/
  AltSnapService/
  AltSnapShared/
```

原則：

1. 不把 AltSnap 主邏輯塞進 runner process。
2. ModuleInterface 只負責啟停與設定橋接。
3. AltSnapService 負責 mouse hook、window move/resize、多螢幕與 DPI。
4. 先支援少量設定，再逐步擴充。
5. 檢查與 FancyZones、AlwaysOnTop、MouseUtils 是否衝突。

需要特別驗證：

- 一般權限 app。
- 系統管理員權限 app。
- 多螢幕。
- 不同 DPI。
- 遊戲或全螢幕程式。
- FancyZones 同時啟用時的互動。

完成條件：

- 可在 settings 內啟停。
- 停用時 hook 完整釋放。
- 不影響 runner 穩定性。

### Phase 6：VoiceInput / Whisper

目的：新增 Typeless 類語音輸入功能。

建議目錄：

```text
src/modules/VoiceInput/
  VoiceInputModuleInterface/
  VoiceInputService/
  VoiceInput.UI/
  VoiceInput.Engine/
```

建議第一版功能：

1. hotkey 按住說話或切換聽寫。
2. 使用 whisper.cpp 作為本機推論 backend。
3. 支援模型路徑設定。
4. 辨識完成後貼到目前 focused control。
5. 顯示簡單錄音狀態視窗。

後續功能：

- VAD 自動偵測語音開始與結束。
- 多語言選擇。
- 模型下載與管理。
- 自訂 prompt。
- 標點補全。
- 字典與替換規則。

風險：

- 模型檔大小。
- CPU/GPU 推論效能。
- 音訊裝置權限。
- 貼上文字到 elevated app 的限制。
- 長時間錄音資源管理。

完成條件：

- 最小模型可本機辨識。
- hotkey 流程穩定。
- 停用時音訊與推論資源完整釋放。

### Phase 7：FastColorPicker 與 FastOCR

目的：逐步替換既有 PowerToys 模組。

策略：

1. 不原地重寫 `colorPicker` 或 `PowerOCR`。
2. 新增 `FastColorPicker` 與 `FastOCR`。
3. 兩者成熟後再從 enabled modules 清單移除舊模組。
4. 最後才考慮刪除舊程式碼。

FastColorPicker 第一版：

- hotkey 開啟。
- 快速取得游標像素色。
- 顯示 HEX/RGB/HSL。
- 點擊複製。
- 啟動速度優先。

FastOCR 第一版：

- hotkey 選取區域。
- OCR 後複製文字。
- 暫時可以先使用 Windows OCR API。
- 後續再抽換 OCR engine。

完成條件：

- 新舊模組可以並存。
- enabled modules 可切換使用哪一個。
- 設定與 hotkey 不互相衝突。

### Phase 8：SnippingStudio

目的：做 Snipaste 類截圖與標註工具。這是大型功能，建議放在後期。

建議目錄：

```text
src/modules/SnippingStudio/
  SnippingStudioModuleInterface/
  SnippingStudioService/
  SnippingStudio.UI/
  SnippingStudio.Canvas/
```

第一版功能：

1. hotkey 進入截圖模式。
2. 區域選取。
3. 複製到剪貼簿。
4. 簡單畫筆、矩形、箭頭。
5. 馬賽克或模糊。
6. 儲存圖片。

後續功能：

- 釘選圖片到桌面。
- 步驟標號。
- 文字工具。
- 多螢幕截圖。
- 歷史紀錄。
- 快速上傳或自訂 action。

完成條件：

- 截圖 overlay 不影響其他全螢幕 app。
- 多螢幕座標正確。
- 高 DPI 座標與畫布對齊。
- 可從 settings 啟停。

## 6. GitHub Fork 與 Push 策略

這個 repo 目前已經是從 `microsoft/PowerToys` clone 下來的 git repository，因此不要在現有資料夾直接執行：

```powershell
git init
```

也不要只用：

```powershell
echo "# Powertoys" >> README.md
git add README.md
git commit -m "first commit"
```

那組指令比較適合全新空資料夾，不適合這個已經有完整 git history 的 PowerToys clone。

建議做法：

```powershell
git remote rename origin upstream
git remote add origin git@github.com:NPCMike/Powertoys.git
git branch -M main
git push -u origin main
```

之後 remote 應該長這樣：

```text
origin    git@github.com:NPCMike/Powertoys.git
upstream  https://github.com/microsoft/PowerToys.git
```

用途：

| Remote | 用途 |
|--------|------|
| `origin` | 自己的 GitHub repo，用來 push 個人專案 |
| `upstream` | Microsoft PowerToys 原始 repo，用來日後同步更新 |

日後同步 upstream 的基本流程：

```powershell
git fetch upstream
git switch main
git merge upstream/main
```

如果個人改動越來越大，後期可以改成只 cherry-pick 上游安全修正或重要修正，不一定要完整 merge。

## 7. 建議分支策略

建議維持這幾種分支：

| 分支 | 用途 |
|------|------|
| `main` | 自己專案的穩定主線 |
| `personal/registry-v1` | 集中式 module registry |
| `personal/enabled-modules` | 個人版模組載入清單 |
| `feature/pid-shower` | 第一個自製模組 |
| `feature/altsnap` | AltSnap 整合 |
| `feature/voice-input` | Whisper 語音輸入 |
| `feature/fast-color-picker` | 快速取色器 |
| `feature/snipping-studio` | 截圖標註工具 |

每個 feature 分支都應該能獨立 build。不要在同一個 commit 同時做 registry、品牌、Whisper、AltSnap 和截圖工具。

## 8. 驗證清單

每次完成一個階段至少檢查：

- [ ] `git status` 只包含本次預期變更。
- [ ] 相關專案 build 成功，exit code 為 0。
- [ ] Runner 可以啟動。
- [ ] Settings UI 可以開啟。
- [ ] 清單內模組可以啟用/停用。
- [ ] 清單外模組不會出現在 settings UI。
- [ ] hotkey 沒有明顯衝突。
- [ ] 停用模組後背景 process 或 hook 有釋放。
- [ ] 沒有新增不必要第三方依賴。
- [ ] 若新增第三方依賴，確認 license 並更新 `NOTICE.md`。

## 9. 優先順序結論

建議實作順序：

1. 先確保原始 PowerToys build baseline 穩定。
2. 設定自己的 GitHub remote。
3. 做最小品牌切分。
4. 建立 `ModuleRegistry.json`。
5. 建立 `EnabledModules.personal.json`。
6. 用 `PidShower` 驗證自製模組流程。
7. 整合 AltSnap，但主邏輯放在 service。
8. 做 VoiceInput / Whisper。
9. 做 FastColorPicker 與 FastOCR。
10. 最後做 SnippingStudio。

這個順序的重點是先讓專案變得可控，再加入高風險功能。PID Shower 是最好的第一個自製模組，因為它足夠小，但會碰到 runner、settings、hotkey、service、overlay，能幫忙驗證未來所有自製模組需要的基礎能力。
