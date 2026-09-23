# 007 — An in-flight order is lost on restart, deliberately

**Status:** accepted · **Related:** issues #65, #295

## Decision

The order conversation — chosen symbol, side, amount — lives only in the bot process, in
`InMemoryConversationStore`. A restart empties it and every in-flight order is dropped. It is not
persisted, and that is the intended behaviour, not a gap.

## Why

Prices move. Resuming an order the customer began before a restart would confirm it at a price that
is no longer published — the customer would be shown one number and filled at another. Losing the
conversation is the safe failure.

## What this rules out

- Adding persistence to the conversation store as an obvious improvement. Doing it properly means
  deciding what happens when the replayed confirmation meets a quote that has changed, and that
  question has to be answered first.
- Reading `InMemoryConversationStore` as a placeholder someone never finished.

## What it does not excuse

What the customer is *told* when this happens. Until #295 they got "خطا در پردازش سفارش. لطفاً
دوباره تلاش کنید" — an error naming no cause, whose advice could never work, since every further
tap landed on the same empty store. The reply now says the order expired and sends them to check
their accounting. Fixing the message is not the same as fixing the cause, and the cause is
deliberate.

## Evidence

- `TelegramBot/TallaEgg.TelegramBot.Infrastructure/Conversations/InMemoryConversationStore.cs` —
  the class doc comment states this decision.
- Issue #65 — the refactor that made the store injectable.
- Issue #295 — the reply, fixed; the cause, left alone.
