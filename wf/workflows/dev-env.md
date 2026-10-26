# dev-env — 開發環境、指令、外部工具

[WORKFLOWS](../WORKFLOWS.md)｜[INDEX](../INDEX.md)

這台機器上要能開發需要什麼、怎麼裝、跑什麼指令；外部工具設定與 env var 也收在這裡。

**何時用**：fresh clone、換機器、裝不起來、忘了指令、要加一個外部工具或環境變數。
**何時不用**：驗證 / 測試怎麼跑 → [testing](testing.md)；程式碼慣例 → [common/conventions](common/conventions.md)。

## Done when

- 照「流程（fresh clone 後）」走完，`dotnet build housecarl.sln` 回傳 0。
- 下面三張表沒有空欄；要使用者親自做的（帳號、授權、金鑰）在 [WAIT_USER](../../../../WAIT_USER.md) 各佔一行。

## 流程（fresh clone 後）

1. 母 repo `git submodule update --init projects/houseCARL`（gitlink 釘在 fork 的 commit；fork＝remote `fork`，上游＝remote `origin`）。要改碼先 `git worktree add`，不在主 checkout 施工。
2. 裝 .NET SDK（CI 用 9.0.x；本機 10.0 SDK 可 build net9.0）。`dotnet build` 會自動 restore NuGet。
3. 本機 MCP 註冊用 explicit paths 的 env var（下表）；不用 `set_mo2_instance` 指真實 MO2（Wine `Z:\` 路徑）。
4. 跑 `dotnet build housecarl.sln` 確認裝好了。

## 指令表

| 做什麼 | 指令 | 備註 |
|--------|------|------|
| 安裝依賴 | `dotnet restore housecarl.sln` | `dotnet build` 會自動做；改 `*.csproj` 版本後才需單獨跑 |
| build | `dotnet build housecarl.sln` | 驗 generator 改動前先 `dotnet clean`，否則會吃到舊 binary |
| 跑起來（Linux）| `dotnet publish src/housecarl-mcp/housecarl-mcp.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:Version=<plugin.json 版本> -o <暫存>` | stdio MCP；`--http` 聽 `127.0.0.1:7345`。**trimming 必須關**；publish 不產 `corpus.json`，要另跑 generator 補 |
| generator／probe | `dotnet publish src/housecarl-generator -c Release -r linux-x64 --self-contained true -o /tmp/hc-gen` | 不帶參數＝重生 corpus；帶 probe／guard 名＝跑該項 |
| 官方打包（Windows）| `./scripts/build-plugin.ps1` | 需 Windows＋PowerShell；本機不跑 |

驗證與測試指令不列這裡——連同「誰跑」一起在 [testing](testing.md)。

## 跨機 / 離線差異

| 環境 | 能跑 | 跑不了的 → 怎麼辦 |
|------|------|------------------|
| 家用 Manjaro（主力機）| build、self-contained publish、個別 guard、MCP 實機 | `ci-all` 只 78/100（Windows fixture）→ 看 CI；`build-plugin.ps1` 跑不了 → 手動 publish＋補 corpus |
| GitHub Actions（windows-latest）| build＋`ci-all`＋`freshness-capture-guard` | Papyrus compiler、BSArch 相關 probe 自動 SKIP |
| 公司機 | 只讀文件 | 沒有 Skyrim／MO2，不跑實機項 |

## 外部工具與 env var

| 名稱 | 用途 | 怎麼取得 / 設定 |
|------|------|----------------|
| .NET SDK | build／publish | CI 用 9.0.x；本機 SDK 10.0 可用。系統沒有 9.0 runtime，所以本機一律 self-contained publish |
| `HOUSECARL_DATA_DIR` | houseCARL 自己的資料目錄 | 本機 `~/.local/share/housecarl` |
| `HouseCarl__DataDir`／`HouseCarl__ModsDir`／`HouseCarl__ProfileDir` | explicit paths：遊戲 Data、MO2 mods、profile | 值與註冊指令以母 repo `agentctl/docs/housecarl.md` 為準 |
| `LocalAppData` | 舊 build 讀 localized 欄位必需；`67975616`（strings 走 MO2 VFS）起不帶也讀得到，留著無害 | 同上，`agentctl/docs/housecarl/localappdata.md` |

需要帳號、付費、授權才能取得的：守鐵律 2（授權來源），並在 [WAIT_USER](../../../../WAIT_USER.md) 記一行。

## 交接

- 環境就緒要開工 → [feature-dev](feature-dev/README.md)；先確認驗證跑得動 → [testing](testing.md)。
- 同一個裝機坑第二次撞到 → [common/gotchas](common/gotchas.md)。
