# INDEX — houseCARL 專案地圖

houseCARL = **把 Skyrim SE load order 資料層開給 AI 的 C# MCP server（Mutagen、reflection-driven）；本 checkout 是 `justty32/houseCARL` fork，在上游 `Avick3110/houseCARL` 之上帶 Linux 修正**。本檔只描述**頂層**：每列一句話＋連結；產品說明在根 [README.md](../README.md)，上游操作手冊在根 [CLAUDE.md](../CLAUDE.md)。

## Repo 佈局

| 路徑 | 內容 |
|------|------|
| `src/` | 四個 .NET 專案：`housecarl-core`（讀寫引擎）、`housecarl-generator`（reflection 產生器＋probe）、`housecarl-mcp`（MCP server）、`housecarl-setup`（Windows 安裝程式）；逐領域見 [code-map](workflows/common/code-map.md) |
| `plugin/` | Claude Code plugin 原始碼（`dist/` 是打包產物，不進版控）|
| `.claude/skills/` | 隨 plugin 出貨的 skill（上游維護）|
| `standards/` | 命名、skill 撰寫、文件衛生規範 |
| `scripts/` | `build-plugin.ps1` 官方打包 |
| `.github/` | CI（windows-latest：build＋`ci-all`）與 issue 表單 |
| [`RECORD_NAMES.md`](../RECORD_NAMES.md)、[`CODE_MAP.md`](../CODE_MAP.md) | 本 fork 加的 record name 讀取契約（strings lookup、編碼、離線 guard 指令）與它的原始碼入口；英文，**上游沒有** |
| `wf/` | 本 fork 加的工作流骨架（派發見 [WORKFLOWS.md](WORKFLOWS.md)；共享區 [workflows/common/](workflows/common/README.md)）；**上游沒有** |
| `wf/tools/` | kernel 文件工具：`wf-lint.sh`（檢查，可選）、`tabledb.py`（資料檔 CRUD／連結）、`find_big_lists.py`、`fix_moved_links.py`；資料檔契約見 [workflows/common/data-files.md](workflows/common/data-files.md) |
| `.claude/commands/` | slash 指令適配層（可選）。Claude Code 只讀專案根的這層，非侵入式佈局也留在根；沒有 slash 機制的工具忽略本目錄，直接跑 `wf/tools/wf-lint.sh` |
<!-- wf-insert:INDEX -->

## 頂層文件

| 檔案 | 角色 |
|------|------|
| [WORKFLOWS.md](WORKFLOWS.md) | 派發器：意圖 → 工作流入口 |
| [STRUCTURE.md](STRUCTURE.md) | 結構整理參考（被動）：分層、膨脹即拆、四級成長、archive、工作流形式 |
| [SESSION-LOG.md](SESSION-LOG.md) | 本 fork 的 open 進度 |
| [母 repo WAIT_USER.md](../../../WAIT_USER.md) | 等使用者親自做 / 驗證的事，**以母 repo 那份為準**，本 repo 不另開 |
