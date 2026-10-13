# IPL Store: IPL franchise merchandise e-commerce

.NET 10 Web API + EF Core + SQL Server (or SQLite) + Redis + RabbitMQ + Razorpay (test mode) + OpenTelemetry.
React + TypeScript UI in `src/IplStore.Web` (Vite, react-router, TanStack Query, Vitest).

## Features
Product list, product details, search (name / SKU / franchise / category, price filter, sort, paging), server-side cart,
checkout with stock reservation, mock payment via Razorpay, order history, JWT auth with roles, admin restock.

## Architecture
```
Api  ->  Application  ->  Domain
 |            ^
 +-> Infrastructure (implements Application's ports)
```
| Layer | Responsibility |
|---|---|
| Domain | `Order`, `Product`, ... aggregates, domain events, exceptions. No dependencies. |
| Application | Use-case handlers (`CheckoutCommandHandler`, `CartService`, queries) and the ports they need (`ICartRepository`, `IInventoryService`, `IPaymentGateway`, `IMessagePublisher`). |
| Infrastructure | EF Core, Redis, Razorpay, RabbitMQ, outbox processor, background jobs, seeding. |
| Api | Controllers, auth, exception handling, health, OpenTelemetry. |

## Run it

### 0. Prerequisites
.NET 10 SDK, Docker Desktop (only for the full setup).

### 1. Zero-install mode (SQLite + in-memory cart, no Docker)
```powershell
cd src/IplStore.Api
dotnet user-secrets set "Persistence:Provider" "Sqlite"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=iplstore.db"
dotnet user-secrets set "Cart:Store" "InMemory"
dotnet user-secrets set "Workers:Enabled" "false"
dotnet user-secrets set "Telemetry:Enabled" "false"
# secrets required in every mode:
dotnet user-secrets set "Jwt:Secret" "<any random string of 32+ characters>"
dotnet user-secrets set "Razorpay:KeyId" "rzp_test_xxxxxxxx"
dotnet user-secrets set "Razorpay:KeySecret" "<your razorpay test secret>"
dotnet run
```

### 2. Full mode (SQL Server + Redis + RabbitMQ + dashboard)
```powershell
copy .env.example .env      # then edit SQL_SA_PASSWORD
docker compose -f docker-compose.infra.yml up -d
cd src/IplStore.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=IplStore;User Id=sa;Password=<same as .env>;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Secret" "<random 32+ chars>"
dotnet user-secrets set "Razorpay:KeyId" "rzp_test_xxxxxxxx"
dotnet user-secrets set "Razorpay:KeySecret" "<razorpay test secret>"
dotnet run
```
The app fails fast at startup if a required secret is missing. There are deliberately no hardcoded fallbacks.

### 3. Database schema
Development creates and seeds the database on startup (`Database:Initialize`). To use migrations with SQL Server:
```powershell
dotnet ef migrations add Initial -p src/IplStore.Infrastructure -s src/IplStore.Api
# drop the dev database once, then restart: the app now runs Migrate() instead of EnsureCreated()
```

### 4. Run the UI
```powershell
cd src/IplStore.Web
copy .env.example .env.local     # keeps the dev-only "Simulate payment" button on
npm install
npm run dev                      # http://localhost:5173 (proxies /api to http://localhost:5212)
npm test                         # 44 UI tests (Vitest + Testing Library)
npm run build                    # type-check + production build
```
Pages: catalog (search/sort/page) · product details · login · cart · checkout and payment · order history · order detail · admin restock.

### 5. Try the API directly
`src/IplStore.Api/IplStore.Api.http` walks through token -> search -> cart -> checkout -> history.
OpenAPI document: `/openapi/v1.json` (Development). Traces/metrics/logs: http://localhost:18888. Health: `/health/live`, `/health/ready`.

## Tests
```powershell
dotnet test --filter-not-trait "Category=Redis"   # everything that needs no Docker
dotnet test --filter-trait "Category=Redis"       # needs: docker compose -f docker-compose.infra.yml up -d redis
# (global.json opts in to the Microsoft.Testing.Platform runner required by xunit v3 on the .NET 10 SDK)
```
| Project | What it proves |
|---|---|
| `UnitTests/Domain` | Invariants, state machine, order numbers, outbox retry/dead-letter policy |
| `UnitTests/Application` | Checkout, idempotency, oversell protection, compensation, payment confirmation, expiry, catalog search/paging, cart rules, order-history isolation. Run on real SQL against in-memory SQLite. |
| `UnitTests/Infrastructure` | Atomic stock UPDATE, outbox claiming/back-off/dead-letter, Razorpay request/signature, DB constraints |
| `UnitTests/Api` | Exception to HTTP mapping (no leaks), JWT config validation |
| `src/IplStore.Web` tests | Login and route guard, catalog search, cart (never sends a price), checkout idempotency-key rules, Razorpay hand-off, order history |
| `IntegrationTests` | The real app over HTTP: auth, roles, ownership, full purchase journey, ProblemDetails. Redis tests prove the atomic cart. |

## Key design decisions (for the walkthrough)
| Concern | Decision | Why |
|---|---|---|
| Oversell | One conditional `UPDATE ... SET Stock = Stock - @q WHERE Stock >= @q` per line | No read-modify-write, no retries on a hot SKU; rows affected = 0 means insufficient stock |
| Deadlocks | Stock lines merged and processed in ProductId order | Concurrent multi-item orders always lock in the same order |
| Double checkout | `Idempotency-Key` header + unique (UserId, key) index | A retry or double-click returns the same order; the loser's transaction rolls back its reservation |
| Cart vs payment | The cart is cleared only when the payment is CONFIRMED, not at checkout. At most one open (unpaid) order per user: an unchanged cart reuses it, a changed cart releases it in the same transaction as the new reservation | A failed or abandoned payment never empties the customer's cart, and retrying never reserves stock twice |
| Payment failure | Order marked Failed and stock released (compensation) | No orphaned reservations |
| Abandoned checkout | Expiry job cancels unpaid orders after 15 min and releases stock | Stock cannot be held hostage |
| Late/duplicate payment confirmation | `MarkAsPaid` is idempotent; `RowVersion` makes it race safely with the expiry job | Webhooks and client callbacks can both fire |
| Cart | Redis hash + Lua script (atomic add with cap), 7-day TTL, AOF persistence | Survives API node loss; no lost updates from concurrent tabs |
| Prices | Never accepted from the client; cart stores only (productId, qty) | Tamper-proof |
| Events | Transactional outbox: domain events become rows in the same `SaveChanges`; relayed with leases, back-off, dead-lettering | No lost or phantom events; safe on N replicas |
| AuthN/Z | JWT bearer, secure-by-default fallback policy, `AdminOnly` policy; user id only from the token's `sub` | No IDOR; dev-only token issuer is replaced by Entra External ID in production |
| Errors | One `IExceptionHandler` -> RFC 7807 with traceId; 4xx quiet, 5xx logged, internals never returned | Safe and diagnosable |
| Catalog under spike | Output cache (30 s, per query string), bounded paging, indexed filters | Reads absorb a 30x spike without touching the DB for most requests |
| Redis down | `503 + Retry-After`, `/health/ready` goes unhealthy | Fails visibly and recovers automatically |

## Not done yet (planned)
Razorpay webhook endpoint, outbox consumers (receipt e-mail, invoice PDF), Dockerfile + compose for N API replicas behind a proxy, k6 load-test scripts, CI workflow, rate limiting, architecture diagrams/presentation.
