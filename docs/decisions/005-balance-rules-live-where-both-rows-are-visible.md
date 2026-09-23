# 005 — `Wallet.LockBalance` enforces no balance rule, and must not

**Status:** accepted

## Decision

`Wallet.LockBalance` does not check whether the wallet can afford what is being locked. The check
belongs in the caller. The commented-out guard inside that method is wrong code, correctly
disabled.

## Why

The credit ceiling for asset `A` lives in a **separate wallet row**, keyed `CREDIT_A`
([004](004-credit-is-cross-asset.md)). A `Wallet` entity is one asset's row and cannot see the
other, so it cannot evaluate the invariant it would be enforcing. Any rule it applied would be a
per-asset rule, which is the thing that rejects legitimate trades.

The check has to live where both rows are visible, which is the service, not the entity.

## What this rules out

- Uncommenting that guard, or writing an equivalent one inside the entity. It will look like an
  obvious missing safety check and it is not.
- Reading "the entity does not validate its own balance" as a layering mistake.

## Evidence

- `Wallet.LockBalance` in the Wallet service, and the commented-out guard in it.
- `ValidateCreditAndBalanceAsync`, which is where the rule actually lives.
