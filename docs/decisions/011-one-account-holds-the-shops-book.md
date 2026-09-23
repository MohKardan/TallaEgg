# 011 — One account holds the shop's book, and it is named `SuperAdmin`

**Status:** accepted · **Confirmed with the product owner** (the rename was raised at the time and
deliberately deferred)

## Decision

All of the shop's gold and money sit in a **single** account, the one carrying
`UserRole.SuperAdmin`. Staff carry `UserRole.Admin`: they may publish quotes, charge credit and
approve customers, but hold no book of their own.

So "several market makers" means several `Admin` users, not several ledgers.

## Why

A gold shop is run by one or two colleagues who cover for each other, so more than one person needs
administrative powers. But the shop's position has to be one number. Splitting the book across
whoever happened to publish a quote would make the shop's exposure
([003](003-market-maker-balance-is-the-shops-book.md)) unreadable.

## The name is known to be wrong

`SuperAdmin` describes a permission level, not a ledger. That was raised when the decision was
taken, and the owner chose not to spend time renaming it while the minimum viable product was still
being finished.

**Treat the name as known-wrong and settled, not as an open question to re-raise.** Renaming it
touches role checks across three services and a seeded database row, for no behaviour change.

## What this rules out

- Reading `SuperAdmin` as "an administrator with more permissions" when reasoning about balances.
  For money, it means *the shop*.
- Giving a second account a book, or spreading the shop's holdings across admins.
- Proposing the rename as a tidy-up. It is a real piece of work with a deliberate "not now" on it,
  and **#35** already covers the related separation of a fee account.

## Evidence

- `UserRole` in the Users service; the seeded root administrator.
- Issue #35 — separating a fee/commission account from this role.
