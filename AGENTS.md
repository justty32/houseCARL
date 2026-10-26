# AGENTS.md — houseCARL

Guidance for AI coding agents (Codex, and others that read `AGENTS.md`) working in this
repository. houseCARL's deep operating manual is **[`CLAUDE.md`](CLAUDE.md)** — read it first;
this file is the agent-agnostic companion, focused on a review checklist for pull requests.
For what houseCARL *is* and how to build it, see **[`README.md`](README.md)**.

> **Public-repo note.** `CLAUDE.md` references a private `dev/` working corpus (PRFAQ, plans,
> session handoffs) that is **gitignored — not part of this repository**, so links into `dev/`
> will not resolve in a clone. The code, standards, and skills are all here; the working notes
> are not.

## The cornerstone — read before touching coverage

houseCARL's load-bearing claim is **comprehensive, data-layer access** to the Skyrim load
order. For records that means: **every record type Mutagen models is readable and writable — by
construction, not by hand.** A build-time generator walks Mutagen's record interfaces and emits
the schema + write-validation automatically, so houseCARL's coverage *is* Mutagen's coverage.

Both prior builds hand-wired a schema and a write-translation per record type and never
finished; this rebuild exists to end that. So:

- **Never** hand-map a record type or reintroduce per-record-type write translations.
- **Never** ship "just the common record types" — full coverage is not a scope choice.
- A record type that's **absent** is a real upstream gap (Mutagen's delta vs xEdit) to
  **surface loudly**, never to guess around or silently skip.

If a change pressures any of the above, stop and raise it via the PRFAQ revalidation protocol
(`CLAUDE.md §4`) — don't work around it.

## Build & run

- **Full plugin build:** `./scripts/build-plugin.ps1` — regenerates the reflection rulebook,
  publishes the server (framework-dependent, **trimming off** — trimming would strip reflected
  types and silently lose coverage), bundles skills, packs the release zip. Needs the
  **.NET 9 SDK**, Windows, PowerShell.
- **Iterating on the generator/engine:** `dotnet build` then `dotnet run`. A
  `dotnet build && dotnet run` can serve a **stale** binary — run `dotnet clean` first when
  verifying a generator change, or the regenerated corpus won't reflect your edit.
- Runtime config is read from a user-config file beside the exe (not the blanked
  `appsettings.json`); the MO2 instance is set at runtime, never hard-coded.

## Conventions

- **MCP tools are `housecarl_<snake_case>`** — the prefix is locked (brand continuity); all
  names follow `standards/HOUSECARL_NAMING.md`.
- The brand string **"houseCARL"** lives in exactly one place in code (the server's config).
- **Atomic, focused commits** — one logical change per commit.
- **Writes are non-destructive by default** — every patch is a **new** MO2 mod folder; sources
  read-only. The one sanctioned exception is the **opt-in in-place lane** (`target=` +
  `in_place=true`, per-plugin consent, no backup), which rewrites an existing plugin only when the
  user explicitly asks.

## Review guidelines (for pull requests)

Reviewing a change to houseCARL — human's or agent's — check, in priority order:

1. **Cornerstone intact** — no hand-wired coverage, no per-record-type write map, no "subset for
   now." Coverage stays reflection-driven and complete by construction.
2. **No silent failure (Q3)** — no silently wrong answer, no silently degraded mode. A tool that
   can't do the thing says so plainly. A swallowed error or quiet fallback is a defect.
3. **No silent workarounds** — a stumbling block should have been *surfaced* (`CLAUDE.md §4`),
   not patched around in a way that trades away something that was supposed to hold.
4. **Writes stay non-destructive — or, in the in-place lane, safe-by-contract** — default writes go
   to a new plugin (originals untouched). The opt-in in-place lane (`target=` + `in_place=true`)
   deliberately rewrites an existing plugin and is sanctioned **only with its guards intact**: the
   explicit per-call flag, the persistent per-plugin consent handshake, the non-bypassable
   round-trip floor (verify the touched records, trust Mutagen for the rest), and the
   `editedInPlace` marker (never houseCARL-owned). Either way, masters are derived from the FormIDs
   actually referenced (plus the Skyrim.esm + Update.esm baseline every plugin carries, as the
   Creation Kit does), never hand-specified.
5. **Atomic & focused** — one logical change; the diff does what the message says, nothing extra.
6. **Correctness over performance** — where they conflict, correctness wins; perf concerns are
   raised factually, not as blockers.

Be concrete and candid: name the file and line, say what's wrong and why, and prefer surfacing a
real doubt over a tidy approval.

## Local fork layer — 本 fork 的 wf/ 工作流（上游沒有）

本段與 `wf/` 是 `justty32/houseCARL` fork 加的；上面各段與 [`CLAUDE.md`](CLAUDE.md) 是上游原文，**原意不動，衝突以上游為準**。

- 開場先跑 `grep -c '^- \[' wf/SESSION-LOG.md`：非 0 才開 [SESSION-LOG.md](wf/SESSION-LOG.md)；等使用者的項記母 repo [WAIT_USER.md](../../WAIT_USER.md)（本 repo 不另開）。
<!-- wf-insert:AGENTS -->
- **碰原始碼前**：慣例與 code map → [conventions](wf/workflows/common/conventions.md)、[code-map](wf/workflows/common/code-map.md)；環境與指令 → [dev-env](wf/workflows/dev-env.md)。
- **要你動手做事** → [WORKFLOWS.md](wf/WORKFLOWS.md) 依意圖派發，再讀該工作流入口檔。
- **想看專案結構** → [INDEX.md](wf/INDEX.md)。
- 使用者偏好與邊界 → [user.md](wf/workflows/common/user.md)。

鐵律（always-on，補在上游規矩之外）：

1. 重構 / 整理**不改原意**：開發＝行為不變且驗證綠燈（[testing](wf/workflows/testing.md)：`dotnet build housecarl.sln`＋相關 guard，在 worktree 跑）；非開發＝原意不變，照 `Done when:` 驗收。
2. **不可逆或對外的動作**（push、刪除、對外送出、對上游開 issue／PR、重 publish 已安裝的 server）要有**授權來源**：使用者當場確認，或他親自登記在清單裡。都沒有就先問。
3. **條列走資料檔、導航留 md**：給 AI 消化的表／清單 >1 KB 存 `.json`／`.csv`（契約 `wf-table/1`，見 [data-files](wf/workflows/common/data-files.md)），用 `wf/tools/tabledb.py` 讀寫；給人點的導航連結留 md。
4. **具體流程**在各工作流入口檔，不在頂層。

<!-- wf-kernel v0.6+368468a (2026-10-26 導入) -->
