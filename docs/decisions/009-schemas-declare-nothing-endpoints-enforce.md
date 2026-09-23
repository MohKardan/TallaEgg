# 009 — No request schema declares a constraint; the endpoints enforce them

**Status:** accepted · **Settled by:** issues #236, #242

## Decision

No request schema in any service declares `required`, `maxLength`, `minimum`, `pattern` or any
other validation keyword — the only one present anywhere is `enum`, generated from C# enum types.
Roughly fifty hand-written checks in the endpoints refuse bodies the schema permits.

The gap between the two is deliberate and was deferred twice before being taken as a decision.

## Why

Minimal APIs do not execute DataAnnotations, and nothing here calls `Validator.TryValidateObject`,
registers a validation filter, or uses FluentValidation. So attributes on a DTO reach the published
schema **without ever running** — the document advertises a limit the server does not apply, which
is worse than declaring nothing. #236 removed `OrderDto`'s attributes for exactly that reason.

Closing the gap the other way — adding a filter that enforces what the schema declares — breaks the
error contract. The bot reads `message` out of an `ApiResponse<T>` envelope, and a filter's
`HttpValidationProblemDetails` deserializes into that envelope without error and with a null
message, which would silently stop telling customers why an order was refused. Most of the fifty
checks cannot be expressed as attributes at all.

## What this rules out

- Adding DataAnnotations to a request DTO, or a schema filter that declares constraints the
  endpoints happen to enforce. Both recreate the "schema promises what the server does not run"
  shape, in a different file.
- Reading the empty constraint count as an oversight.

## What a client must therefore do

Expect a refusal it cannot predict from the document; read `success` rather than the status code,
since refusals arrive as 400, 404 **and** 200; not show `message` to a customer unconditionally,
because some are internal English; and send every field it means, including falsey ones — an
omitted property is the C# default, and on several endpoints that default destroys something.

## Evidence

- `docs/design/API_REQUEST_VALIDATION.md` — every check on all writable endpoints, with file, line
  and refusal message, and how to re-measure it.
- `docs/process/STANDARDS.md` §2 — the same decision, from the client's side.
- Issues #236, #237, #242.
