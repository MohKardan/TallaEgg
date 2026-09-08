---
name: work-an-issue
description: Take one GitHub issue from reading to merged, through this repository's own pipeline — explain it plainly, get the owner's approval on a plan, implement, prove the fix by making it fail first, open a PR, self-review with /code-review, merge, clean up, and recommend what to do next. Use when asked to work, do, fix, or close a numbered issue.
---

One issue, start to finish, with two places where you stop and wait for the owner.

This skill exists because the same brief was written by hand fifteen times in a week, and each
time it absorbed something the previous run got wrong. Those lessons are the numbered rules
below. They are not style preferences — every one of them is a mistake this repository has
actually made.

## The pipeline

```
1  Read      AGENT.md, the process docs, the issue
2  Verify    check the issue's own claims against today's code
3  Explain   the problem, in a few sentences        ─┐ STOP 1
4  Plan      what you will do, as a short list      ─┘ wait for approval
5  Work      implement, with the failure proof
6  Test      build, tests, and the real thing
7  PR        open it, with the evidence
8  Review    /code-review, answer every finding
9  Merge     squash, delete the branch, verify
10 Report    open issues, and what to do next
```

Stop again, mid-work, whenever a decision turns out to be the owner's (rule 4).

---

# 1. Read first

In this order:

1. `AGENT.md` — all of it. Especially **"Business rules that look like bugs"**: four things that
   have each been reported as defects and are not. Reporting one of them again is the most common
   way a session wastes a day here.
2. `docs/process/STANDARDS.md` and `docs/process/PR_TEMPLATE.md`.
3. `gh issue view <n>` — the whole issue, including any correction blocks and the sections at the
   end. Issues in this repository often carry a "what I deliberately did not do" section, and it
   is usually the most important part.
4. Any issue or PR the issue links to. A lot of the reasoning lives there.

# 2. Verify before you believe

**The issue is evidence, not truth.** Two things are routinely wrong in it:

- **Line numbers go stale within days.** Merges shift them. #210's numbers pointed at a different
  endpoint by the time it was worked. Find the code yourself.
- **Claims made from reading rather than running.** Check them. If a claim turns out to be wrong,
  that is a finding — say so rather than working around it.

State what you confirmed and what you could not. If the issue's central claim does not hold,
**stop and say so** instead of building on it.

# 3–4. Explain, plan, and wait — STOP 1

Two short blocks. Be brief. The owner is deciding, not reading a report.

**The problem** — a few sentences in plain language. What is wrong, what it costs, and what
happens if nothing is done. No file dumps, no code blocks unless one line makes it clearer.

**What I will do** — a short list of concrete actions. Include:

- anything you will delete or change that is a **contract** (a request field, a wire name, a
  response shape, a config key that becomes required)
- anything that needs the owner to do something on the server
- anything you will deliberately **not** do, and why

Then wait. Do not start.

If the issue offers options, say which you recommend and why, in one sentence each. Do not pick
for the owner.

# 4. Stop again when a decision is the owner's

Some things are not yours to choose, even after the plan is approved:

- **Numbers** — a tolerance band, a retry window, an alert threshold, a deduplication interval.
  Propose one with a reason; do not adopt it.
- **Contract changes** — removing a request field, renaming a wire name, making a config key
  required. Note that making a key required can stop a service booting on a machine whose
  config you cannot see, so name every key that becomes required and wait.
- **Anything touching the live server** — reading is fine, changing is not.
- **Deleting anything the owner has not agreed to delete.**
- **Business rules.** If you are unsure what is correct behaviour, ask. Guessing here is how a
  correct feature gets "fixed".

# 5. Work

Small scope. Do what the issue asks and stop. This repository's habit — and the reason its issue
list keeps producing good issues — is that adjacent problems get **reported, not fixed**. #223
exists because #222 found it and left it alone. Keep that going.

Branch: `fix/`, `feat/`, `chore/`, `docs/` or `hotfix/` plus a short description. Never commit to
`main`.

Consider working in a git worktree. Parallel sessions switch the primary working tree mid-task,
and a file read from the wrong branch has caused real confusion.

# 6. Test — and prove the fix by making it fail

The rule this repository has learned hardest:

> **A check that has never gone red is not a check. A fix that was never seen failing is not
> known to fix anything.**

Where the issue describes a defect, reproduce it **before** changing anything, and show the
failure. Then fix, and show it pass. Both outputs go in the PR.

Some traps that have caught real sessions here:

- `dotnet run` sets the working directory to the project folder, which is exactly the condition
  under which a working-directory bug does **not** appear (#212).
- A test written for a race that passes before the fix means the race was never reproduced (#223).
  `WalletConcurrencyTests.CollidingContext` makes collisions deterministic — use it rather than
  relying on timing.
- A simulator run whose outbox still has a backlog settles nothing during the run and proves
  nothing (#183).
- The simulator only exercises the symbols it is configured for. A green run says nothing about
  a symbol it never traded.
- Casing, schema and wire-format defects are invisible in the C#. **Look at the wire** — the
  actual request body, the actual `swagger.json` — not the diff. Seven issues in this repository
  hid behind that.

Always:

```
dotnet build TallaEgg.sln --no-incremental     # 0 errors, 0 warnings — TreatWarningsAsErrors is on
dotnet test  TallaEgg.sln
```

Build before running anything: `dotnet test` only builds the test project's dependency graph, so
an API's `bin` can be stale. If a restored or reverted file has an older timestamp than its
output, MSBuild may skip it entirely and you will test the code you thought you replaced.

Use `/run-tallaegg` when the change touches behaviour rather than only text. Check the database
afterwards for the usual damage: duplicate settlements, orphan orders, collateral locked with no
order holding it, wallet balances disagreeing with transaction history.

Write tests. The suite is expected to grow.

# 7. Open the PR

Follow `docs/process/PR_TEMPLATE.md`. `closes #<n>` if the issue is fully done, `part of #<n>`
with a list of what remains if not.

The PR body must carry:

- **the before/after evidence** — the failure and the pass, with real output, not a description
- **what you found and did not fix**, so it can become an issue
- **a deployment note** when the change needs something done on the server, or must be deployed
  in a particular order relative to another change. Say it explicitly; several changes in this
  repository are only safe in one order.

# 8. Review

Run `/code-review high <PR#>` on your own work. You wrote it, so you are not independent evidence
about it — this is the least that can be done about that.

Answer every finding: fix it, or say why it stands.

Wait for CI. The required check is `test`; there is also a documentation link check.

# 9. Merge and clean up

Only if **all** of these hold: build clean with zero warnings, tests green, the real thing
exercised, no open review finding, CI green.

If any of them does not hold — or anything is ambiguous — **do not merge. Stop and ask.**

```
gh pr merge <PR#> --squash --delete-branch     # never --admin
git checkout main && git pull
git branch -D <branch>                         # -D: squash merges look unmerged to -d
git fetch --prune origin
git branch -vv                                 # nothing but main, no "[origin/...: gone]" rows
```

`--admin` bypasses a rule this repository deliberately has. Use it only if the owner says so.

# 10. Report and recommend

Finish with:

- **one line on what landed**
- **anything left for the owner** — a server step, a decision, an issue worth filing from what you
  found
- **the open issues**, grouped: ready to work / waiting on a decision or a number / waiting on the
  server or a customer
- **what to do next, and why** — one recommendation with a sentence of reasoning, not a survey

```
gh issue list --state open --limit 50
```

---

# Rules that are not negotiable

- **English** in code comments, commit messages, PR text and documentation. Persian only for what
  a user sees in the bot — never translate those strings.
- **Never commit `config/appsettings.global.json`.** It holds live credentials and this repository
  is public. Only the `.example.json` belongs in git. No secret value in any log, comment or PR
  body — not even part of one.
- **No personal identity anywhere**: names, handles, emails, personal paths, chat exports, server
  names or addresses. If you find any in a file, stop and ask; do not decide it is acceptable.
- **The bot tokens visible in this repository are dead** (#33). Do not report them as a leak.
  History is deliberately not rewritten (#105).
- **Never write a per-asset balance check.** Credit is cross-asset — a customer holding only
  `CREDIT_MAUA` can legitimately drive their IRT balance negative.
- **Scope stays narrow.** No refactor nobody asked for. Report what you find; fix what you were
  asked to fix.
