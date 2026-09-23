# 008 — Git history is not rewritten, including for leaked secrets

**Status:** accepted · **Settled by:** issue #105

## Decision

The history of this repository is not rewritten. Bot tokens and other secrets committed in the past
stay in the history; the remedy is to revoke them at the source, not to erase the commits.

## Why

Rewriting history breaks every existing clone, fork and reference, and a public repository's old
objects may already be mirrored elsewhere — so the rewrite buys less than it costs. Revoking a
leaked credential ends its usefulness immediately and everywhere, which erasing the commit does
not.

## What this rules out

- `git filter-repo`, BFG, force-pushes over `main`, or any other rewrite, for any reason including
  a newly found secret.
- Reading a token in an old commit as an open task to scrub.

## What it requires instead

- **Revoke it.** For a Telegram bot token, that is BotFather.
- **Do not assume a historical token is already dead.** A check in September 2026 found that three
  of four tokens in this repository's history were still live and two of the bots had been
  hijacked, after a document had stated they were all rotated. Measure before concluding: a
  read-only `getMe` answers 401 for a revoked token, and it never prints the token itself.
- **Keep token values and bot details out of issues, pull requests and commit messages** — the
  repository is public, so writing about a leak can extend it.

## Evidence

- Issue #105 — where the decision was taken and recorded.
- `tests/TallaEgg.AllServices.Tests/NoOutOfProcessLoggingTests.cs` — scans tracked source so no
  *new* token can be committed. It guards the working tree, not the history.
