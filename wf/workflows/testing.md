# testing — 跑測試 / 驗證

[WORKFLOWS](../WORKFLOWS.md)｜[INDEX](../INDEX.md)

改完怎麼確認沒壞：有哪些驗證、各自的指令、哪些 agent 自己跑得了、哪些得交給使用者。

**何時用**：改完程式要驗、使用者說「跑測試」、重構前要記基準。
**何時不用**：環境還沒裝好、不知道指令哪來 → [dev-env](dev-env.md)；驗證紅了要查成因 → [investigation](investigation.md)。

## Done when

- 下表標「改完必跑」的列指令回傳 0。
- 「誰跑」是使用者的列，已在 [WAIT_USER](../../../../WAIT_USER.md) 各留一行（寫明跑什麼、什麼算過）。

## 驗證表

| 驗證 | 指令 | 誰跑 |
|------|------|------|
| 快速驗證（改完必跑）| `dotnet build housecarl.sln`（Linux 本機實測：0 error、十幾個既有 warning）| agent（在 worktree 跑）|
| 改到的那條 guard（改完必跑）| `/tmp/hc-gen/housecarl-generator <guard 名>`，例：`dialogue-encoding-guard`、`case-insensitive-asset-guard`、`skse-config-audit-guard`（generator 先 self-contained publish，見 [dev-env](dev-env.md)）| agent |
| 完整驗證（commit 前）| CI 同款：`dotnet build housecarl.sln -c Release` → `housecarl-generator ci-all` → `housecarl-generator freshness-capture-guard`（`.github/workflows/ci.yml`）| GitHub Actions（windows-latest）；Linux 本機 `ci-all` 只到 78/100，不當全綠 gate |
| 實機（MO2 load order 上讀寫）| 重 publish 到 `~/tools/housecarl/server/` 後，用 MCP 工具對 POSIX 鏡像實測 | 使用者 → [WAIT_USER](../../../../WAIT_USER.md)（需重啟 Claude／Codex session 才載入新 server）|

「誰跑」只有兩種值：**agent**（本機跑得動）、**使用者 → WAIT_USER**（要實機、外部服務、帳號、付費、目視）。判不準就當後者。

## 測試分類

本 repo 沒有 xunit 測試專案；驗證是 `src/housecarl-generator/` 裡的 probe／guard（`*Probe.cs`，`ci-all` 一次跑完），缺外部工具（Creation Kit 的 Papyrus compiler、BSArch）的部分自動 SKIP。**Linux 離線可跑的子集**＝`dotnet build`＋與改動直接相關的 self-contained guard；`ci-all` 在 Linux 有 19 個 Windows 路徑 fixture 失敗，是既有狀態不是回歸。**build／test 一律在 `git worktree` 裡跑**，主 checkout 的 `bin/` 不能被重建（母 repo 釘了 gitlink）。

- `fast`：本專案自己的 unit／source gate；每次小改都跑。
- `contract`：跨元件的真實 CLI／process 邊界；改 producer、consumer 或協定時跑。
- `full`：README 指定的完整離線 suite；commit 前或大改後跑。
- `external`：需要實機、外部服務、素材、帳號或人工感官驗收；agent 代跑不了的記到 [WAIT_USER](../../../../WAIT_USER.md)。

## 綠燈不等於有檢查

**一道檢查通過，可能是因為它根本沒在檢查。** 這不是假設：曾在一天內抓到四個恆真檢查——結構稽核、「鎖已釋放」的檢查指向已刪目錄、連結檢查器走到子 repo 指標就停、自製字元偵測。

**規則：新增或修改一道檢查時，要證明它能變紅。** 先餵一個**應該被擋**的輸入，確認 exit ≠ 0；再餵正確的輸入，確認 exit = 0。**沒做過這個雙向驗證的綠燈不算證據。**

兩個推論：檢查器的**涵蓋範圍要跟著結構走**——拆出子 repo、搬走目錄之後，回頭確認檢查器還看得到那些地方（見 [refactor/moving-things.md](refactor/moving-things.md)）；**靜態全過不等於畫面上是對的**，方框、亂碼、截斷、手感只有人眼看得出來，這類記到 [WAIT_USER](../../../../WAIT_USER.md)，不要自己宣稱通過。

## 交接

- 綠燈後回 [feature-dev](feature-dev/README.md) 接完剩下的步驟（code map → 文檔 → commit）。
- 同一個紅燈第二次撞到 → [common/gotchas](common/gotchas.md)。
