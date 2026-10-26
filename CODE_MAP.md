# Record-name source map

This focused map covers record-name reading and its regression checks.

| Entry | Responsibility |
|---|---|
| [LoadOrderResolver](src/housecarl-core/LoadOrderResolver.cs) | Overlay lifetime and strings source folder |
| [ExplicitStringsLookup](src/housecarl-core/ExplicitStringsLookup.cs) | Unix loose/BSA strings without Windows discovery; MO2-VFS winner first when injected |
| [RecordNameDisplay](src/housecarl-core/RecordNameDisplay.cs) | Strict UTF-8 display interpretation |
| [ReadEngine](src/housecarl-core/ReadEngine.cs), [FieldPredicate](src/housecarl-core/FieldPredicate.cs) | Raw tokens, display, semantic comparison |
| [LoadOrderService](src/housecarl-mcp/LoadOrderService.cs) | Resolved identity names; `ResolveStringsWinner` feeds the asset resolver to strings lookup |
| [RecordNamesProbe](src/housecarl-generator/RecordNamesProbe.cs), [RecordNameDisplayProbe](src/housecarl-generator/RecordNameDisplayProbe.cs), [StringsVfsProbe](src/housecarl-generator/StringsVfsProbe.cs) | Synthetic guards |
| [RecordNamesLiveProbe](src/housecarl-generator/RecordNamesLiveProbe.cs) | Opt-in supplied-file acceptance |

Contract and commands: [RECORD_NAMES.md](RECORD_NAMES.md).

## 領域表（全 repo）

| 領域 | 檔案 | 職責 | 測試在哪 |
|------|------|------|---------|
| 讀寫引擎 | `src/housecarl-core/*` | Mutagen 讀寫、MO2 load order／資產解析、NIF／BSA／Papyrus | `src/housecarl-generator/*Probe.cs` |
| 產生器與 probe | `src/housecarl-generator/*` | reflection 產 `corpus.json`；`CiAll.cs`＝probe 清單 | 同左（`ci-all`、各 `*-guard`）|
| MCP server | `src/housecarl-mcp/*`（工具在 `*Tools.cs`）| MCP 工具面、設定、Nexus 查詢 | 對應的 `*Probe.cs` |
| 安裝程式 | `src/housecarl-setup/*` | Windows 安裝程式| CI build |
| plugin 與 skills | `plugin/`、`.claude/skills/*` | plugin 原始碼與隨附 skill | — |
| 規範 | `standards/*` | 命名、skill 撰寫、文件衛生 | — |
| 打包 | `scripts/build-plugin.ps1` | 官方 Windows 打包| — |

## 真相層優先序

各專案可以改自己的優先序，但必須明確。預設：

```text
code/probes > schema/examples/fixtures > code map > docs > generated（`generated/` 不進版控）
```

- 上層與下層衝突時，**以上層為準並修正下層**。
- generated（產生出來的檔、html）永遠不是唯一真相。
- 原始來源與摘要衝突時，以**原始來源**為準。
- code map 是**導航不是規格**；行為以 code/tests 為準。

## 維護鏈：程式碼 > code map > 文檔

**優先級**（衝突或時間不夠時，依序保持一致）：程式碼 > code map > 文檔。
**code map 與程式碼衝突時以程式碼為準，立刻改 code map。**

1. **修改前**：先讀本表找到相關領域，只讀清單裡的檔——不讀無關領域的檔。
2. **修改後**：新增／刪除了原始碼檔案，或某檔職責顯著改變，必須同步更新本表。
3. 原始碼裡**不加**「對應 code map」的註釋（維護成本過高）；反向查找直接 grep 本檔。
4. 迭代期間本表可暫時落後，**commit 前必須對齊**（見 [feature-dev](wf/workflows/feature-dev/README.md)）。
