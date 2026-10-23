# FSeaotterHime

個人相關群組自用的聊天機器人，同時整合了 Discord Bot 與 LINE Webhook 服務。

## 專案架構

- [`FSeaotterHime`](FSeaotterHime/)：共用核心邏輯（運勢抽籤等）。
- [`FSeaotterHime.Discord`](FSeaotterHime.Discord/)：Discord 機器人服務（Discord.Net）。
- [`FSeaotterHime.Line`](FSeaotterHime.Line/)：LINE Bot Webhook 服務（Oxpecker）。

## 開發與執行備忘

### 1. 設定檔

啟動前在各自的 `appsettings.json` 或 User Secrets 設定：

- **Discord** (`FSeaotterHime.Discord`)：
  - `Discord:Token`
- **LINE** (`FSeaotterHime.Line`)：
  - `LineBot:ChannelSecret`
  - `LineBot:ChannelAccessToken`

### 2. 啟動指令

- **執行 Discord 機器人**：
  ```powershell
  dotnet run --project FSeaotterHime.Discord/FSeaotterHime.Discord.fsproj
  ```
- **執行 LINE Webhook 服務**：
  ```powershell
  dotnet run --project FSeaotterHime.Line/FSeaotterHime.Line.fsproj
  ```

  *預設本機監聽* `http://localhost:5032`*，Webhook 路徑為* `/callback`*。*

### 3. 打包 Discord 單一執行檔(目標平台linux-x64)

在專案根目錄執行：

```powershell
dotnet publish FSeaotterHime.Discord/FSeaotterHime.Discord.fsproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

執行檔在 `FSeaotterHime.Discord/bin/Release/net10.0/linux-x64/publish/FSeaotterHime.Discord`。監聽位址讀同資料夾 `appsettings.json` 的 `Urls`，預設 `http://localhost:5033`。