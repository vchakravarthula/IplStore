# Review findings -> fix -> proving test

| # | Finding | Fix (file) | Test |
|---|---|---|---|
| 1 | Cart lost its items on every Redis round-trip; remove didn't remove; fire-and-forget writes; same-product race | Domain `Cart` removed (ephemeral state, not an aggregate). Redis hash + Lua atomic add-with-cap; every call awaited (`RedisCartRepository`) | `CartServiceTests.Remove_really_removes...`, `InMemoryCartRepositoryTests.Fifty_concurrent_adds...`, `RedisCartRepositoryTests.*` |
| 2 | `ShortOrderNumber` collided (27% duplicates in a tight loop) | Derived from the order's v7 GUID, 50 random bits (`OrderNumberGenerator`); unique index stays as backstop | `OrderNumberGeneratorTests.Generating_100k_numbers...` |
| 3 | RabbitMQ failures swallowed, outbox marked message sent, message lost | Publisher throws unless the broker confirms (`RabbitMqPublisher`); outbox only marks processed after success | `OutboxProcessorTests.A_broker_failure_never_marks...` |
| 4 | Payload serialized twice (double-encoded JSON) | Raw JSON string published as-is | `OutboxProcessorTests.Publishes_the_raw_payload_unmodified...` |
| 5 | Anonymous `deduct-stock` endpoint | Removed; stock only moves inside checkout/expiry; admin restock behind `AdminOnly` | `Admin_endpoints_need_the_admin_role` |
| 6 | No `[Authorize]`; userId from route/body (IDOR) | `[Authorize]` + fallback policy; id from token `sub` only; catalog `[AllowAnonymous]`; routes no longer contain a user id | `Protected_endpoints_reject_anonymous_callers`, `Two_users_never_see_each_others_cart`, `One_user_cannot_read_or_pay_another_users_order` |
| 7 | Token endpoint minted any role | Development-only, whitelist of 2 seeded users, role decided server-side | `The_token_endpoint_only_issues_tokens_for_seeded_users...` |
| 8 | Hardcoded secret fallbacks; JWT signed and validated with different config | `JwtSettings` validated at startup (no defaults) and used for BOTH signing and validation; connection string required; RabbitMQ creds required; Razorpay keys required | `JwtSettingsValidationTests.*`, whole `ApiEndToEndTests` (token round-trip) |
| 9 | `InvalidOperationException` -> 409 with message leaked; domain exceptions never thrown; double logging | Domain exceptions actually thrown; mapping table; 5xx never leak; one log line per failure | `GlobalExceptionHandlerTests.*` |
| 10 | Client-supplied price and currency | Request has no price; server prices from catalog; currency is a server constant | `Cart_ignores_any_client_supplied_price...` |

## Also fixed (from the High / Hygiene lists)
- Checkout: gateway call moved after the commit; order stores `GatewayOrderId`; compensation on gateway failure; idempotency key; stock via atomic UPDATE with ordered locking; payment confirmation endpoint with constant-time signature check; expiry job releasing stock.
- Order state machine with idempotent `MarkAsPaid`; `RowVersion` on Order.
- Outbox: lease-based claiming (safe with N replicas), exponential back-off, dead-lettering; events raised by the aggregate (`OrderPlaced`, `OrderPaid`) instead of the interceptor hard-coding Order.
- Razorpay: async `HttpClient` + resilience pipeline (no `Task.Run` around a sync SDK); paise rounded; vendor types removed from Application contracts.
- EF: indexes and max lengths, filtered unique indexes, `EnableRetryOnFailure`, v7 GUID keys, ghost `Carts`/`CartItems` tables gone, config for Franchise/Category.
- Redis: lazy connect with `AbortOnConnectFail=false`; connection failures become 503 + Retry-After; readiness probe.
- Catalog: paging (bounded), search (name/SKU/franchise/category), sort, filters, product details; output cache.
- Order history + order detail endpoints (ownership enforced).
- Hygiene: `.gitignore`, `Class1.cs`/`UnitTest1.cs`/`ValueObject`/`IProductRepository`/MediatR/FluentValidation/Serilog removed, single DI registration point per layer, xunit v3 in both test projects, tests reference the code.

## Deliberate trade-offs to be ready to discuss
- Cart/CartItem aggregate removed: atomicity matters more than behaviour for ephemeral per-user state.
- `IApplicationDbContext` exposes EF types (pragmatic Clean Architecture): handlers are tested against real SQL on SQLite instead of mocking `DbSet`.
- `ExecuteUpdate` for stock bypasses the change tracker by design; `RowVersion` still protects tracked admin edits.
- Order numbers are probabilistically unique (50 bits) with a unique index as the safety net; a SQL `SEQUENCE` is the alternative when strictly sequential numbers are required.
- At-least-once event delivery: consumers must be idempotent on `EventId`.
- RabbitMQ.Client 6.x (synchronous API with publisher confirms) is used deliberately; moving to 7.x async is a mechanical change.

## Behaviour change found during manual testing
- **Cart emptied when payment failed.** The cart was cleared at checkout, before the payment. Now it is cleared (only the paid items) when the payment is CONFIRMED.
  To keep that safe: at most one open order per user. Same cart and prices: the open order is reused; changed cart: the old order is expired and its stock released in the same
  transaction as the new reservation. Tests: `A_second_checkout_with_an_unchanged_cart_reuses_the_open_order...`, `Changing_the_cart_releases_the_old_open_order...`,
  `A_price_change_after_checkout_creates_a_fresh_order...`, `A_failed_checkout_that_cannot_reserve_leaves_the_old_open_order_untouched`,
  `Paying_removes_only_the_paid_items_from_the_cart`, `A_rejected_confirmation_keeps_the_cart...`, `Replaying_a_confirmation_does_not_remove_items...`,
  and the end-to-end `A_failed_or_abandoned_payment_leaves_the_cart_intact...`.
- Known trade-off: if the customer pays an order in one tab while changing the cart in another, the older order can be expired under them; the confirmation then returns 409 and the payment needs a refund (the webhook/reconciliation work is the real fix).
