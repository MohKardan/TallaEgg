# Lessons

Mechanics that cost a working session once, written down so that they cost nobody a second one. Each
entry says what goes wrong, how it was found, and what to do instead.

This is the shared half of what agents have so far kept in their own tools' private memory, where no
other agent could see it. It is for **how things behave**: git, the build, the local data, the tools.
**Why the product is the way it is** belongs in [`../decisions/`](../decisions/README.md), and the
rules every agent follows belong in [`../../AGENTS.md`](../../AGENTS.md).

**Adding an entry:** open a PR. Say what happened, when, and the command or measurement that showed it.
Re-check an entry before quoting it somewhere permanent, because the fix may have landed since it was
written. Delete an entry once it no longer holds; a wrong lesson is worse than none.

---

## Git and GitHub

### Do branch work in a separate worktree

The main working tree can be switched to another branch by a parallel session while you are reading it.
On 2026-08-30, during a review of #171, it was switched twice, and a whole verification pass was done
against the wrong branch's version of a file (235 lines instead of 292). Nothing said so.

```
git worktree add <short path under your user profile> -b <branch> origin/main
# ...work there...
git worktree remove <path>
```

Keep the path **short**. Under a long temp directory the simulator dies with `Unable to load DLL
'Microsoft.Data.SqlClient.SNI.dll' … The filename or extension is too long` (2026-09-07), while the three
APIs start fine, so the failure only shows at `driver.ps1 smoke`. A worktree has no
`config/appsettings.global.json`, because the file is git-ignored. Copy it in to run the stack there, and
remove the worktree afterwards so that a second copy of live credentials is not left on disk.

### Merging from a worktree prints a fatal error after succeeding

`gh pr merge <n> --squash --delete-branch` run from inside a worktree fails with
`fatal: 'main' is already used by worktree at …`. **The merge has already happened on GitHub.** `gh`
fails only at the local checkout it attempts afterwards (observed 2026-09-21, #310). Check with
`gh pr view <n> --json state,mergedAt`, never by retrying. The clean way is to remove the worktree
first, then merge from the main tree.

### A squash-merged branch always looks unmerged

This repository squash-merges, so a branch's own commits never appear in `main`. As a result:

- `git branch -d` refuses a fully merged branch. Use `-D`, after confirming the PR is merged.
- `git log main..<branch>`, `git branch --merged` and `git merge-base --is-ancestor` all report it as
  unmerged. Judge by **the PR's state** (`gh pr view <n> --json state`) or by **content**:
  `git diff origin/main origin/<branch> --stat`, where empty output means every line is already in `main`.
- After a merge, delete the branch locally as well as on GitHub, then `git fetch --prune origin` and
  `git branch -vv`. Only `main` should remain, with no `[gone]` rows. `/code-review <n>` leaves a
  `pr-<n>` branch behind; delete that too.

### `gh pr merge` can refuse a branch that is not behind

On #187 (2026-09-01), GraphQL's `mergePullRequest` answered "Head branch is out of date" with
`mergeStateStatus: BLOCKED`. The branch was current and the required `test` check was green. The REST
endpoint enforces the same ruleset and merged it at once:
`gh api -X PUT repos/MohKardan/TallaEgg/pulls/<n>/merge -f merge_method=squash`.

### A hand-written squash subject loses the `(#N)` suffix

GitHub appends the PR number only when it composes the subject itself. With
`gh pr merge --squash --subject "…"`, include `(#<n>)` yourself. #244 is the one commit in the log
without it, and history is not rewritten to fix that.

---

## Build

### A restored file can make the build a silent no-op

When comparing before and after by swapping a file for an older copy, the copy can carry a
`LastWriteTime` older than the `obj/` output built from the version in between. MSBuild then considers
the project up to date, prints `Build succeeded. 0 Warning(s)`, and the service keeps running the code
you just replaced. This cost three stop/build/start cycles on #235 (2026-09-07).

After restoring a file, touch it (`(Get-Item <path>).LastWriteTime = Get-Date`) or delete the project's
`obj/`. When a measurement contradicts the source you can see, suspect the build before the measurement:
compare the DLL's timestamp with the source file's.

---

## Local data

### Local databases hold real development accounts; never reset them

The local `TallaEggUsers` database holds the shop's book account (the `SuperAdmin`, decision 011),
hand-made accounts and repro accounts alongside the simulator's `sim_user_*` rows. There is no backup.
**Ask before dropping, resetting or restoring any local database.**

### Reset or migrate the service databases together, or not at all

Users, Wallet and Orders share history: a trade in Orders produced ledger rows in Wallet that reference
it. When the databases were migrated at different points (#68), Orders lost trades that Wallet's ledger
still held. That later surfaced as a phantom 32.22 g gap in the profit-and-loss feature (#93), and
reconciling it by hand took a day. Before touching one database, check what the others hold that
refers to it.

### Scope integrity queries to the run you just did

`Orders`, `TradeSettlements` and `OutboxMessages` accumulate across every simulator run; only the
simulated users' `Transactions` and `Wallets` are wiped. So:

- Any settlement from an earlier run shows **2 transaction legs instead of 4**. The two simulated
  users' legs were deleted; the market maker's survive. A leg-count check over a wide window reports
  every one as broken.
- Filter by time (`WHERE CreatedAt > DATEADD(minute,-10,GETUTCDATE())`) and compare before/after counts,
  not absolute ones. First check when the run actually ended: a window that misses the run returns 0
  for every "should be 0" check and looks like a pass.

`AGENTS.md` → "Measurements that mislead" covers the residue in `Orders` itself.

### Use realistic figures in tests and scripts

An invented number does not stay in the test that wrote it. A script published a MAUA/IRT quote at
33.4M/gram when the market was about 22.2M (2026-08-31). Once the plausibility band shipped (#158), that
quote became the reference, the real feed read as 33.7% out of band, and every real price was held for
approval while the wrong one stayed in force. Check what the asset trades at before writing a price.
Pick a deliberately absurd value only to test a rejection, and do not leave it standing as the active
quote.

### A working trade does not prove a wallet was created at registration

`Wallet.Api` creates a wallet row the first time anything writes to it. So register → approve → grant
credit → trade produces correct balances even when registration created no wallets at all. That is how
#209 (registration's wallet call answered 401) stayed hidden. When testing anything wallet-related,
read the log of the registration itself.
