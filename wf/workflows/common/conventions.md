# conventions — 程式碼慣例（碰原始碼的工作流共用）

[common/README](README.md)｜[INDEX](../../INDEX.md)

碰原始碼的工作流（feature-dev / refactor / investigation）共用這套規矩：**寫碼時要遵守什麼**。哪個檔負責什麼領域、測試在哪 → [code-map](code-map.md)；真相層優先序也在 [code-map](code-map.md)。結構整理原則 → [STRUCTURE](../../STRUCTURE.md)。

## 慣例

| 項目 | 規矩 |
|------|------|
| 上游規矩優先 | 根 `AGENTS.md`／`CLAUDE.md` 的上游段落**照做**；本表只補 fork 差異，衝突以上游為準 |
| cornerstone | coverage 由 reflection 產生，**不手寫逐 record type 的 schema／write map**；Mutagen 缺口大聲回報 |
| 不准靜默失敗 | 吞例外、悄悄降級都算 bug（上游 Q3）|
| 命名 | MCP 工具 `housecarl_<snake_case>`，其餘照 `standards/HOUSECARL_NAMING.md` |
| 寫入 | 預設寫進**新的** mod 資料夾；in-place 只走 `in_place=true` 且守住同意握手 |
| 註解／commit 語言 | 程式碼、註解、commit 用**英文**（同上游）；`wf/` 文件繁中 |
| commit | atomic；Linux 修正與 `wf/` 改動分開 commit |
| 相依 | 版本鎖在各 `*.csproj`；加套件、升 Mutagen 先問|
