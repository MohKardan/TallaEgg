# Request validation: what the published schemas say, and what the servers do

**Measured**: 2026-09-08, against `main` at `b8a2d14`, on the three running services.
**Method**: walking `GET /swagger/v1/swagger.json` on Users (5136), Wallet (60933) and Orders
(5140), and probing each writable endpoint over real HTTP — not read off the C#.

This document exists because the published schemas declare **no constraints at all**, while the
endpoints enforce roughly fifty. A client built from the document sends a body the document says
is valid and gets refused. That is a deliberate state, recorded in issue #242 and summarized in
[`../process/STANDARDS.md`](../process/STANDARDS.md) §2; this file is the detail that rule points
at, because the list of what is actually enforced is the part a client cannot discover any other
way.

Read it as a snapshot. It will drift, and "How to re-measure" at the end says how to check.

---

## 1. What the documents declare

```
required   (schema-level, an array of property names)   Users 0   Wallet 0   Orders 0
maxLength                                               Users 0   Wallet 0   Orders 0
minimum                                                 Users 0   Wallet 0   Orders 0
```

Zero as well, everywhere: `minLength`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`,
`pattern`, `minItems`, `maxItems`, `uniqueItems`, `multipleOf`.

The only validation keyword present in any of the three documents is `enum` — 7 occurrences, every
one generated from a C# enum type (`UserRole`, `UserStatus`, `OrderRole`, `OrderSide`,
`OrderStatus`, `OrderType`, `TradingType`) rather than declared by anyone.

`required: true` does appear 56 times, and it is not a counter-example: every occurrence is on a
**parameter** object, never on a schema. Those are path parameters, which OpenAPI requires to be
marked required. **No request-body property is required in any service.**

Scale: 51 paths, 39 object schemas, 195 properties. 24 writable endpoints — 4 in Users, 6 in
Wallet, 14 in Orders — taking 14 distinct request-body schemas between them, with 65 properties.

## 2. What a schema-valid body actually gets

There is no single answer. `{}` — valid against every request schema in all three services —
produces **five** different outcomes:

| outcome | endpoints |
|---|---|
| `400` with a Persian message | most of them |
| `404` | `POST /api/user/update-role`, `POST /api/outbox/{id}/abandon` |
| `200` with `success: false` | `POST /api/user/register` |
| `200` having **acted on C# defaults** | 4 endpoints — see §4 |
| `500` | 2 endpoints — see §3 |

Measured, `{}` to every writable endpoint whose failure path provably writes nothing:

| endpoint | status | body |
|---|---|---|
| `POST /api/user/register` | 200 | `{"success":false,"message":"کد دعوت معتبر نیست."}` |
| `POST /api/user/update-phone` | 200 | succeeded — §4 |
| `PUT  /api/user/status` | 200 | succeeded — §4 |
| `POST /api/user/update-role` | 404 | `{"success":false,"message":"کاربر یافت نشد."}` |
| `POST /api/wallet/deposit` | 400 | `{"success":false,"message":"کیف پول وجود ندارد","data":null}` |
| `POST /api/wallet/withdrawal` | 400 | `{"success":false,"message":"کیف پول وجود ندارد","data":null}` |
| `POST /api/wallet/lockBalance` | 400 | `{"success":false,"message":"کیف پول پیدا نشد","data":null}` |
| `POST /api/wallet/unlockBalance` | 400 | `{"success":false,"message":"کیف پول پیدا نشد","data":null}` |
| `POST /api/wallet/changeBalance` | 400 | `{"success":false,"message":"Invalid symbol ''. Expected BASE/QUOTE.","data":null}` |
| `POST /api/orders` | 400 | `{"success":false,"message":"نماد معاملاتی الزامی است"}` |
| `POST /api/quotes` | 400 | `{"success":false,"message":"خطا در انتشار مظنه.","data":null}` |
| `POST /api/quotes/accept` | 400 | `{"success":false,"message":"مقدار باید بزرگ‌تر از صفر باشد.","data":null}` |
| `POST /api/quotes/pending/{id}/approve` | 400 | `{"success":false,"message":"این مظنه پیدا نشد.","data":null}` |
| `POST /api/quotes/pending/{id}/reject` | 400 | `{"success":false,"message":"این مظنه پیدا نشد.","data":null}` |
| `POST /api/outbox/{id}/abandon` | 404 | `{"success":false,"message":"پیام یافت نشد.","data":null}` |

The refusal body is the platform's `ApiResponse<T>` envelope — `{"success", "message", "data"}` —
with `message` in Persian and meant to be shown to the customer. A client should read `message`,
and should not assume a non-200 status: `register` refuses inside a 200.

A body that is *almost* complete behaves the same way:

```
POST /api/orders   {"asset": "MAUA/IRT", "amount": 0.25}
-> 400 {"success":false,"message":"قیمت برای سفارش محدود الزامی است"}
```

## 3. Schema-valid bodies that answer 500

Two endpoints answer `500`, not `400`, for a body the schema permits. For a client this is worse
than an unpredictable 400: the envelope carries a generic message with nothing to show a customer.

```
POST /api/quotes/accept   {"quantity": 1}   -> 500 {"success":false,"message":"خطای داخلی سرور"}
POST /api/quotes          {}                -> 400, but through the generic catch, not the domain check
```

Both are a `NullReferenceException` on a null `symbol`.
[`QuoteFillService.cs:70`](../../src/Order/Orders.Application/Services/QuoteFillService.cs#L70)
does `symbol.Split('/')[0]` with no null check, and the quantity guard at
[`:59`](../../src/Order/Orders.Application/Services/QuoteFillService.cs#L59) runs first — so a body
that satisfies the quantity check reaches the split.

`POST /api/wallet/unlockBalance` has the same shape for a different reason: a negative `amount`
throws `ArgumentOutOfRangeException` at
[`WalletRepository.cs:343`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L343) and
surfaces as 500. That one is already stated in the endpoint's own Swagger description.

## 4. Schema-valid bodies that are accepted, having acted on C# defaults

The sharpest consequence of nothing being required: **an omitted property is not an error, it is a
value** — and on four endpoints that value is meaningful and destructive.

| endpoint | property omitted | what gets written |
|---|---|---|
| `POST /api/user/update-phone` | `phoneNumber`, `telegramId` | `""` onto the user whose TelegramId is `0` |
| `PUT /api/user/status` | `newStatus`, `telegramId` | `UserStatus.Pending` onto the user whose TelegramId is `0` |
| `POST /api/autoquote-settings/{Base}/{Quote}/enabled` | `isEnabled` | `false` — auto-quoting turned **off** |
| `POST /api/symbols/{Base}/{Quote}/active` | `isActive` | `false` — the symbol **disabled** |

The first two are not hypothetical. `TelegramId` is a `long`, its default is `0`, and the seeded
root administrator's TelegramId is `0` (migration `20250918224949_init`). An empty body addressed
to the platform's SuperAdmin row is a successful write answered `200` — confirmed by accident while
measuring this document, and reverted.

The last two were derived from the code rather than fired, because firing them would have disabled
a live trading symbol.

## 5. Every hand-written check, by endpoint

**shape** — expressible as a DataAnnotation on the DTO.
**state** — needs a database read or a runtime catalogue lookup.
**cross-field** — depends on another property of the same body.

### Users.Api — 4 writable endpoints, and **not one field-shape check**

Every refusal here is "this row does not exist". Nothing about the body itself is ever checked.

| endpoint | check | kind | outcome |
|---|---|---|---|
| `POST /api/user/register` | invitation code resolves to an existing user ([`UserService.cs:33`](../../src/User/Users.Application/UserService.cs#L33)) | state | **200** `success:false` «کد دعوت معتبر نیست.» |
| | a duplicate TelegramId is not checked; the unique index refuses it | — | 500 |
| `POST /api/user/update-phone` | user exists by TelegramId ([`UserService.cs:79`](../../src/User/Users.Application/UserService.cs#L79)) | state | 400 «کاربر یافت نشد.» |
| | phone format, non-emptiness, uniqueness — none | — | accepted |
| `PUT /api/user/status` | user exists by TelegramId ([`UserService.cs:98`](../../src/User/Users.Application/UserService.cs#L98)) | state | 400 «کاربر یافت نشد.» |
| | transition legality — none; any status may follow any other | — | accepted |
| `POST /api/user/update-role` | user exists by UserId ([`Program.cs:462`](../../src/User/Users.Api/Program.cs#L462)) | state | **404** «کاربر یافت نشد.» |
| | transition legality, and the caller's own role — none | — | accepted |

### Wallet.Api — 6 writable endpoints

| endpoint | check | kind | outcome |
|---|---|---|---|
| `POST /api/wallet/deposit` | asset is a known currency ([`WalletService.cs:55`](../../src/Wallet/Wallet.Application/WalletService.cs#L55)) | state | 400 «کیف پول وجود ندارد» |
| | `userId` non-empty, `asset` non-empty ([`Wallet.cs:63,66`](../../src/Wallet/Wallet.Core/Wallet.cs#L63)) | shape | **500** (ArgumentException) |
| | `amount > 0` ([`Wallet.cs:83`](../../src/Wallet/Wallet.Core/Wallet.cs#L83)) | shape | 400 «مقدار باید بزرگتر از صفر باشد» |
| `POST /api/wallet/withdrawal` | the three above ([`WalletService.cs:107`](../../src/Wallet/Wallet.Application/WalletService.cs#L107), [`Wallet.cs:92`](../../src/Wallet/Wallet.Core/Wallet.cs#L92)) | | |
| | `Balance - amount >= 0` ([`Wallet.cs:95`](../../src/Wallet/Wallet.Core/Wallet.cs#L95)) | state | 400 «مقدار کسر از حساب بیشتر از حد مجاز است» |
| `POST /api/wallet/lockBalance` | asset is a known currency ([`WalletRepository.cs:300`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L300)) | state | 400 «کیف پول پیدا نشد» |
| | `amount > 0` ([`Wallet.cs:116`](../../src/Wallet/Wallet.Core/Wallet.cs#L116)) | shape | 400 |
| | sufficiency — **deliberately none**; the credit ceiling lives in a separate `CREDIT_<ASSET>` row a single-asset write cannot see | — | accepted |
| `POST /api/wallet/unlockBalance` | asset is a known currency ([`WalletRepository.cs:337`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L337)) | state | 400 |
| | `amount >= 0` ([`WalletRepository.cs:343`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L343)) | shape | **500** (ArgumentOutOfRangeException) |
| | `amount <= LockedBalance` ([`WalletRepository.cs:350`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L350)) | state | 400, naming both figures (#52) |
| | `amount > 0` ([`Wallet.cs:136`](../../src/Wallet/Wallet.Core/Wallet.cs#L136)) | shape | 400 |
| `POST /api/wallet/changeBalance` | `symbol` parses as `BASE/QUOTE` ([`WalletRepository.cs:542`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L542)) | shape | 400 |
| | `quantity > 0` and `quoteQuantity > 0` ([`:548`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L548)) | shape | 400 |
| | `feeBuyer >= 0`, `feeSeller >= 0` ([`:550`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L550)) | shape | 400 |
| | fees are exactly zero ([`:568`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L568)) — a collected fee is credited to no account (#35) | shape | 400 |
| | buyer ≠ seller ([`:555`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L555)) | cross-field | 400 |
| | both assets known ([`:690`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L690)) | state | 400 |
| | both sides' collateral is actually locked ([`:704`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L704), [`:709`](../../src/Wallet/Wallet.Infrastructure/WalletRepository.cs#L709)) | state | 400 |
| `POST /api/wallet/create-default/{userId}` | none — takes no body | — | — |

### Orders.Api — 14 writable endpoints

| endpoint | check | kind | outcome |
|---|---|---|---|
| `POST /api/orders` | `asset` non-blank ([`Program.cs:660`](../../src/Order/Orders.Api/Program.cs#L660)) | shape | 400 «نماد معاملاتی الزامی است» |
| | `amount > 0` ([`Program.cs:663`](../../src/Order/Orders.Api/Program.cs#L663)) | shape | 400 «مقدار سفارش باید بیشتر از صفر باشد» |
| | `price > 0` ([`Program.cs:666`](../../src/Order/Orders.Api/Program.cs#L666), again at [`OrderService.cs:94`](../../src/Order/Orders.Application/OrderService.cs#L94)) | shape | 400 «قیمت برای سفارش محدود الزامی است» |
| | per-symbol `MinQuantity` / `MaxQuantity` ([`OrderService.cs:230,235`](../../src/Order/Orders.Application/OrderService.cs#L230)) | state | 400 |
| | `quantity * price >= MinNotional` ([`OrderService.cs:242`](../../src/Order/Orders.Application/OrderService.cs#L242)) | cross-field | 400 |
| | credit + balance across both assets ([`OrderService.cs:121,128`](../../src/Order/Orders.Application/OrderService.cs#L121)) | state | 400 |
| | `Order.Create` re-checks asset/amount/price/userId ([`Order.cs:38-47`](../../src/Order/Orders.Core/Order.cs#L38-L47)) | shape | 500, unreachable today |
| `POST /api/quotes` | `symbol` non-blank ([`Quote.cs:64`](../../src/Order/Orders.Core/Quote.cs#L64)) — but a null symbol NREs first | shape | 400 |
| | `buyPrice > 0`, `sellPrice > 0` ([`Quote.cs:67,70`](../../src/Order/Orders.Core/Quote.cs#L67)) | shape | 400 |
| | `buyPrice <= sellPrice` ([`Quote.cs:76`](../../src/Order/Orders.Core/Quote.cs#L76)) | cross-field | 400 |
| | `publishedByUserId` non-empty ([`Quote.cs:80`](../../src/Order/Orders.Core/Quote.cs#L80)) | shape¹ | 400 |
| | plausibility band against the last published mid ([`Program.cs:370`](../../src/Order/Orders.Api/Program.cs#L370)) | state | **200**, held for approval |
| `POST /api/quotes/pending/{id}/approve` | the pending quote exists ([`Program.cs:429`](../../src/Order/Orders.Api/Program.cs#L429)) | state | 400 |
| | not already resolved, not expired, `adminUserId` non-empty ([`PendingQuote.cs:177,180,183`](../../src/Order/Orders.Core/PendingQuote.cs#L177)) | state / shape¹ | 400 |
| `POST /api/quotes/pending/{id}/reject` | as above ([`PendingQuote.cs:198`](../../src/Order/Orders.Core/PendingQuote.cs#L198)) | state | 400 |
| `POST /api/quotes/accept` | `quantity > 0` ([`QuoteFillService.cs:59`](../../src/Order/Orders.Application/Services/QuoteFillService.cs#L59)) | shape | 400 |
| | `quantity` survives rounding to the symbol's precision ([`:73`](../../src/Order/Orders.Application/Services/QuoteFillService.cs#L73)) | state | 400 |
| | the symbol is in Dealer mode ([`:80`](../../src/Order/Orders.Application/Services/QuoteFillService.cs#L80)) | state | 400 |
| | an active quote exists ([`:85`](../../src/Order/Orders.Application/Services/QuoteFillService.cs#L85)) | state | 400 |
| | customer ≠ market maker ([`:113`](../../src/Order/Orders.Application/Services/QuoteFillService.cs#L113)) | state | 400 |
| | credit + balance ([`:134,146`](../../src/Order/Orders.Application/Services/QuoteFillService.cs#L134)) | state | 400 |
| | `symbol` non-blank — **no check at all** ([`:70`](../../src/Order/Orders.Application/Services/QuoteFillService.cs#L70)) | — | **500** |
| `POST /api/autoquote-settings/{Base}/{Quote}/spread` | `spreadPercent >= 0` ([`AutoQuoteSettings.cs:64`](../../src/Order/Orders.Core/AutoQuoteSettings.cs#L64)) | shape | 400 |
| | `updatedByUserId` — no check; `Guid.Empty` accepted | — | accepted |
| `POST /api/autoquote-settings/{Base}/{Quote}/enabled` | **none** | — | accepted; omitting `isEnabled` turns auto-quoting off |
| `POST /api/symbols/{Base}/{Quote}/active` | **none** | — | accepted; omitting `isActive` disables the symbol |
| `POST /api/outbox/{messageId}/abandon` | the message exists ([`Program.cs:1217`](../../src/Order/Orders.Api/Program.cs#L1217)) | state | **404** |
| | `reason` non-blank ([`OutboxMessage.cs:189`](../../src/Order/Orders.Core/OutboxMessage.cs#L189)) | shape | 400 |
| | status is `Failed` ([`OutboxMessage.cs:191`](../../src/Order/Orders.Core/OutboxMessage.cs#L191)) | state | 400 |
| `POST /api/orders/{orderId}/cancel` | order exists and is not Completed or Failed ([`OrderService.cs:498`](../../src/Order/Orders.Application/OrderService.cs#L498)) | state | 400 / 404 |
| `POST /api/orders/{orderId}/confirm` | order exists and is Pending | state | 400 |
| `POST /api/orders/user/{userId}/cancel-active` | **none** — an unknown user answers 200, "0 cancelled" | — | 200 |
| `POST /api/outbox/{messageId}/redrive`, `POST /api/outbox/redrive-all-failed` | message state | state | 400 |

¹ `Guid.Empty` on a non-nullable `Guid`. `[Required]` can never fail on one, so this is not in fact
expressible as a stock DataAnnotation — the trap PR #236 named when it deleted `[Required]` from
`OrderDto.Id` and `OrderDto.Side`.

## 6. Why it is this way

Not an oversight, and not a state anyone should "fix" without reading this first.

`OrderDto` used to carry `[Required]`, `[Range]` and `[StringLength]`. Minimal APIs do not execute
DataAnnotations — nothing in this repository calls `Validator.TryValidateObject`, registers a
validation filter, or references FluentValidation — so they never ran. They did reach the schema,
which advertised a length limit and a minimum the server never applied, and marked the wrong (alias)
properties as required. **PR #236 deleted them**, taking the `required`/`maxLength`/`minimum` count
to zero across all three services and ending `OrderDto`'s status as the one exception.

That was right for #235: attributes that lie are worse than no attributes. It leaves the gap this
document records, and #236 declined to close it, as did #237 after it, for the same reason — closing
it properly means running validation for real, which is a platform-wide decision that changes the
400 body of live trading endpoints.

Issue #242 settled it: **leave the behavior, write down the contract.** Two measurements decided it.

First, the counts in §5: about **16** of the checks are expressible as a DataAnnotation (`[Required]`
on a string, `[Range]`, one `[RegularExpression]`), across 7 of the 14 request schemas. About **31**
are cross-field, stateful, or catalogue lookups, and no attribute can carry them. So restoring the
attributes and running them would make the schema describe roughly a *third* of enforcement, not all
of it — and would leave every endpoint answering two different error shapes,
`HttpValidationProblemDetails` from the filter and `ApiResponse<T>` from everything the filter cannot
replace.

Second, the client side. The bot reads `message` out of the `ApiResponse<T>` envelope at 17 call
sites — 9 in
[`WalletApiClient.cs`](../../src/TallaEgg/TallaEgg.Infrastructure/Clients/WalletApiClient.cs), 8 in
[`OrderApiClient.cs`](../../TelegramBot/TallaEgg.TelegramBot.Infrastructure/Clients/OrderApiClient.cs)
— and shows it to the customer. Deserializing a `HttpValidationProblemDetails` body into
`ApiResponse<T>` does not throw: it finds no `message`, leaves it null, and the `?? "خطا در …"`
fallback fires. The bot would not break; it would quietly stop telling customers *why* an order was
refused, and no test would catch it.

So the honest name for that work is not "turn on validation" but **"unify the platform's error
contract"**, and it belongs to its own issue, reconsidered only if #97 finds these 400s genuinely
painful in practice.

## 7. What a client should therefore do

- **Do not treat the schema as a contract for validity.** Every property is optional and unbounded
  in the document; almost none of them are in the server.
- **Read `message`, not the status code.** Refusals arrive as 400, 404 and 200-with-`success:false`.
  The `success` field is the reliable signal.
- **Send every field you mean, including falsey ones.** Omitting `isEnabled` or `isActive` is not
  "leave it alone", it is "set it to false" (§4).
- **Expect an occasional 500 with no usable message** on the two paths in §3.
- Use this document, endpoint by endpoint, as the list of what will be refused.

## 8. How to re-measure

Nothing here is generated, so it goes stale silently. To check it:

```powershell
& .claude/skills/run-tallaegg/driver.ps1 start
# Users 5136, Wallet 60933, Orders 5140 — Swagger is Development-only in all three
Invoke-WebRequest http://localhost:5140/swagger/v1/swagger.json -UseBasicParsing |
    Select-Object -ExpandProperty Content | Out-File -Encoding utf8 orders.swagger.json
```

Then walk the JSON for the keywords in §1 — count `required` only where its value is an *array*,
since a boolean `required` is a parameter, not a schema constraint — and probe each endpoint in §5
with `{}`.

**Probe carefully.** The four endpoints in §4 accept an empty body and act on it: two write to the
seeded administrator's row, and two disable live trading configuration. Everything else in §2 fails
before it writes.
