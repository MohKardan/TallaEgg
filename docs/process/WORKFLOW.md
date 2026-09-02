# Development Workflow

How work gets picked up, reviewed and merged. Two developers, no ceremony beyond what earns its
place.

Rules for *writing* code are in [`STANDARDS.md`](STANDARDS.md). Rules for *reviewing* someone
else's are in [`CODE_REVIEW_GUIDE.md`](CODE_REVIEW_GUIDE.md). This file is only the flow between
them.

---

## What to work on

**GitHub issues are the only source of priorities.** `gh issue list`. No document in this repo
tracks live status, and any that appears to is out of date — that lesson is written up in
[`docs/OKR.md`](../OKR.md) under "درس‌های دوره".

Useful filters:

```
gh issue list --label audit-finding    # traceable to a numbered audit finding
gh issue list --label critical         # production blockers
```

Take one thing at a time. If you are blocked for more than an hour, say so on the issue and pick
up something else — a blocked task sitting open is fine, a blocked task nobody knows about is not.

---

## Branch, commit, PR

1. **Branch** off `main`, named per [`STANDARDS.md`](STANDARDS.md) §2 — `feat/`, `fix/`,
   `hotfix/`, `refactor/`, `docs/`, `chore/` or `release/`, plus a description.
2. **Commit** in the format in `STANDARDS.md` §3, referencing the issue.
3. **Open a PR** using [`PR_TEMPLATE.md`](PR_TEMPLATE.md). GitHub auto-loads the lean version;
   copy the Security and Deployment sections in for anything touching money, auth or schema.
4. **CI must pass.** `.github/workflows/build-and-test.yml` runs `dotnet build` and
   `dotnet test TallaEgg.sln` on every push and pull request.
5. **Get a review.** `main` requires one approval, and pushing new commits dismisses existing
   approvals, so re-request after changes.
6. **Squash merge**, then delete the branch.

### Before requesting review

- [ ] Builds with **zero warnings**; `dotnet test TallaEgg.sln` green
- [ ] No secrets, tokens or credentials in the diff
- [ ] Comments in English, explaining *why*
- [ ] Documentation updated in the same PR if behaviour changed
- [ ] The change is the smallest one that does the job — no uninvited refactors

### Changes that need a second reviewer

Money, authentication, secrets, TLS/CORS, or database schema. `CODE_REVIEW_GUIDE.md` §2 explains
what a line-by-line review of those looks like.

---

## Deployment

There is no `staging` branch and no automated deploy; `main` is what ships. Deployment is manual
and documented in [`../operations/WINDOWS_DEPLOYMENT.md`](../operations/WINDOWS_DEPLOYMENT.md) —
`publish-all.ps1` then `install-services.ps1`, which stops and recreates all four Windows
services.

To exercise the whole stack without deploying, use the `manual-test-run` workflow
(Actions → manual-test-run → Run workflow). It stands everything up against a throwaway SQL
Server for a chosen number of minutes.

---

## Conventions worth knowing

Not process, but the things that most often go wrong for someone new to this repo — the full list
is in [`AGENT.md`](../../AGENT.md) and [`CLAUDE.md`](../../CLAUDE.md):

- **Build the solution before running any service.** `dotnet test` only builds the test project's
  dependency graph, so an API's `bin` can be stale.
- **Never commit `config/appsettings.global.json`.** It holds live credentials and this repo is
  public.
- **Some code that looks dead is dormant by design** — zero commission, the OrderBook matching
  path, the quarantined stub behind its feature flag. `AGENT.md` lists them and why.
