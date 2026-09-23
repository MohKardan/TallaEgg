# Decision Records

This directory is the shared working memory of the project: what we decided, why, and what we
accepted as a consequence. It is written for humans and AI agents alike, and both are expected to
read it before proposing a change that would undo one of these decisions.

`docs/process/STANDARDS.md` has asked for Architecture Decision Records since it was written. This
directory is that, widened: several of the records below are product decisions rather than
architectural ones, and those were the ones actually going missing.

## Why this exists

Every record here started as knowledge that lived somewhere an agent could not find it — a closed
pull request, a code comment three files away, a conversation nobody wrote down. Each time, the
cost was the same: work proposed that contradicted a decision already taken, and an owner who had
to catch it by hand.

The clearest example is the one that prompted this directory. In September 2026 an agent removed an
admin command and wrote that removing it was "the likely answer" because, in the dealer model, an
order exists only for the instant of a fill. That was true of the code and wrong about the product:
customer-to-customer order placement had existed before and may return. Nothing in the repository
said so, so nothing could have corrected it. That intent is now [`../product/DIRECTION.md`](../product/DIRECTION.md).

## What belongs here

A decision deserves a record when **someone could undo it without knowing they were undoing
anything** — when the code looks like a mistake, or the absence of something looks like a gap.

That is a narrower test than "important decision". The database is SQL Server and the bot is in
C#; nobody is going to change those by accident, and neither needs a record. That commission is
`0.00` on every trade *does*, because it looks exactly like an unfinished feature.

What does **not** belong here:

- **How to work in this repository** — build commands, test commands, code style, which project is
  runnable. That is [`../../AGENT.md`](../../AGENT.md) and [`../../CLAUDE.md`](../../CLAUDE.md).
  Those files say *how the agent operates*; this directory says *what we are building and why*.
  Mixing the two is what let these decisions get lost: a reader looking for product intent does not
  open a file about build commands.
- **What the code does today** — that is the code, and [`../architecture/`](../architecture/).
- **Open questions** — those are GitHub issues. A record states a decision that was taken.

## Format

One file per decision, `NNN-short-slug.md`, numbered in the order they were recorded (not the order
they were decided — several below were taken long before they were written down). Each carries:

- **Status** — `accepted`, or `superseded by NNN`. Records are never deleted or rewritten to say
  something else; a decision that changes gets a new record and the old one is marked superseded.
  Reading a wrong record and seeing it marked is safe. Finding no record at all is not.
- **Decision** — one or two sentences, in the present tense.
- **Why** — the reasoning, including what it costs.
- **What this rules out** — the concrete thing a reader might otherwise do. This is the part that
  does the work: a record that cannot be violated cannot be checked. If you cannot name what it
  forbids, the record is not specific enough yet.
- **Evidence** — where this is visible in the code, and the issue or pull request that settled it.

Keep each one short. A record nobody rereads is worth nothing, and the long version already exists
in the pull request it links to.

## The records

| # | Decision | Status |
|---|---|---|
| [001](001-dealer-model-customer-never-enters-a-price.md) | The customer trades on a published quote and never enters a price | accepted |
| [002](002-commission-is-zero-revenue-is-the-spread.md) | Commission is zero on every trade; the revenue is the spread | accepted |
| [003](003-market-maker-balance-is-the-shops-book.md) | The market maker's negative balance is the shop's book, not an overdraft | accepted |
| [004](004-credit-is-cross-asset.md) | Credit is stored per asset but backs a position in either currency | accepted |
| [005](005-balance-rules-live-where-both-rows-are-visible.md) | `Wallet.LockBalance` enforces no balance rule, and must not | accepted |
| [006](006-admin-role-bypasses-the-balance-check.md) | The `Admin` role places orders with no funds and no credit | accepted |
| [007](007-conversation-state-is-not-persisted.md) | An in-flight order is lost on restart, deliberately | accepted |
| [008](008-git-history-is-not-rewritten.md) | Git history is not rewritten, including for leaked secrets | accepted |
| [009](009-schemas-declare-nothing-endpoints-enforce.md) | No request schema declares a constraint; the endpoints enforce them | accepted |
| [010](010-two-spellings-for-a-trading-pair.md) | A trading pair is `asset`/`amount` in some schemas and `symbol`/`quantity` in others | accepted |
| [011](011-one-account-holds-the-shops-book.md) | One account holds the shop's book, and it is named `SuperAdmin` | accepted |

## Writing a new one

Take the next number, copy the shape of an existing record, and keep it to a screen. If you are an
agent: propose it to the owner before writing a record about product intent. Several of the records
here were confirmed with the owner on a specific date, and that date is in them, because a decision
somebody merely inferred is not the same thing as one that was taken.
